import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import {
  configureApiRuntime,
  customInstance,
  httpClient,
} from '@/api/http-client'
import { ApiError } from '@/api/problem-details'
import {
  clearLocalSession,
  getSessionGeneration,
  refreshAccessToken,
} from '@/features/auth/auth-session-service'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const baseUrl = 'http://localhost:5000'
const refreshUrl = `${baseUrl}/api/v1/auth/refresh`
const session = {
  accessToken: 'synthetic-refreshed-token',
  expiresAtUtc: '2026-07-18T12:00:00Z',
  user: {
    id: '11111111-1111-4111-8111-111111111111',
    email: 'fictional.user@example.test',
    displayName: 'Fictional User',
    roles: ['Inspector'],
  },
}

function configureRuntime(
  overrides: Partial<Parameters<typeof configureApiRuntime>[0]> = {},
) {
  configureApiRuntime({
    getAccessToken: () => null,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
    ...overrides,
  })
}

describe('HTTP client', () => {
  it('uses the configured API base URL and credentialed requests', () => {
    expect(httpClient.defaults.baseURL).toBe(baseUrl)
    expect(httpClient.defaults.withCredentials).toBe(true)
  })

  it('attaches only the current in-memory bearer token to ordinary requests', async () => {
    configureRuntime({ getAccessToken: () => 'synthetic-access-token' })
    server.use(
      http.get(`${baseUrl}/protected`, ({ request }) => {
        expect(request.headers.get('authorization')).toBe(
          'Bearer synthetic-access-token',
        )
        return HttpResponse.json({ ok: true })
      }),
      http.post(`${baseUrl}/api/v1/auth/login`, ({ request }) => {
        expect(request.headers.get('authorization')).toBeNull()
        return HttpResponse.json(session)
      }),
    )

    await expect(customInstance({ url: '/protected' })).resolves.toEqual({
      ok: true,
    })
    await customInstance({ url: '/api/v1/auth/login', method: 'POST' })
  })

  it('refreshes and replays an ordinary request once with its request data intact', async () => {
    let requestCount = 0
    const onTerminalUnauthorized = vi.fn()
    configureRuntime({
      getAccessToken: () =>
        requestCount === 0
          ? 'synthetic-expired-token'
          : 'synthetic-refreshed-token',
      refreshAccessToken: async () => 'synthetic-refreshed-token',
      onTerminalUnauthorized,
    })
    server.use(
      http.post(`${baseUrl}/protected`, async ({ request }) => {
        requestCount += 1
        expect(new URL(request.url).searchParams.get('page')).toBe('2')
        expect(request.headers.get('x-request-purpose')).toBe('test')
        expect(await request.json()).toEqual({ finding: 'fictional' })
        if (requestCount === 1) {
          expect(request.headers.get('authorization')).toBe(
            'Bearer synthetic-expired-token',
          )
          return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
        }
        expect(request.headers.get('authorization')).toBe(
          'Bearer synthetic-refreshed-token',
        )
        return HttpResponse.json({ ok: true })
      }),
    )

    await expect(
      customInstance({
        url: '/protected',
        method: 'POST',
        params: { page: 2 },
        headers: { 'X-Request-Purpose': 'test' },
        data: { finding: 'fictional' },
      }),
    ).resolves.toEqual({ ok: true })
    expect(requestCount).toBe(2)
    expect(onTerminalUnauthorized).not.toHaveBeenCalled()
  })

  it('uses the coordinator single flight for concurrent unauthorized requests', async () => {
    let refreshCount = 0
    let protectedCount = 0
    useAuthStore.getState().establishSession('synthetic-expired-token')
    configureApiRuntime({
      getAccessToken: () => useAuthStore.getState().accessToken,
      getSessionGeneration,
      refreshAccessToken,
      onTerminalUnauthorized: clearLocalSession,
    })
    server.use(
      http.post(refreshUrl, () => {
        refreshCount += 1
        return HttpResponse.json(session)
      }),
      http.get(`${baseUrl}/protected/:id`, ({ request }) => {
        protectedCount += 1
        return request.headers.get('authorization') ===
          'Bearer synthetic-refreshed-token'
          ? HttpResponse.json({ ok: true })
          : HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
    )

    await expect(
      Promise.all([
        customInstance({ url: '/protected/one' }),
        customInstance({ url: '/protected/two' }),
      ]),
    ).resolves.toEqual([{ ok: true }, { ok: true }])
    expect(refreshCount).toBe(1)
    expect(protectedCount).toBe(4)
  })

  it('does not refresh or replay a request from an older session generation', async () => {
    let generation = 1
    let releaseUnauthorized!: () => void
    const unauthorizedGate = new Promise<void>((resolve) => {
      releaseUnauthorized = resolve
    })
    const refresh = vi.fn(async () => 'session-b-refreshed-token')
    configureRuntime({
      getAccessToken: () =>
        generation === 1 ? 'session-a-token' : 'session-b-token',
      getSessionGeneration: () => generation,
      refreshAccessToken: refresh,
    })
    let requests = 0
    server.use(
      http.get(`${baseUrl}/session-bound`, async () => {
        requests += 1
        await unauthorizedGate
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
    )

    const sessionARequest = customInstance({ url: '/session-bound' })
    await vi.waitFor(() => expect(requests).toBe(1))
    generation = 2
    releaseUnauthorized()

    await expect(sessionARequest).rejects.toMatchObject({ status: 401 })
    expect(refresh).not.toHaveBeenCalled()
    expect(requests).toBe(1)
  })

  it('does not replay when the session generation changes during refresh', async () => {
    let generation = 1
    let releaseRefresh!: (token: string) => void
    const refreshResult = new Promise<string>((resolve) => {
      releaseRefresh = resolve
    })
    const refresh = vi.fn(() => refreshResult)
    configureRuntime({
      getAccessToken: () => 'session-a-token',
      getSessionGeneration: () => generation,
      refreshAccessToken: refresh,
    })
    let requests = 0
    server.use(
      http.get(`${baseUrl}/generation-change`, () => {
        requests += 1
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
    )

    const sessionARequest = customInstance({ url: '/generation-change' })
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledWith(1))
    generation = 2
    releaseRefresh('session-a-refreshed-token')

    await expect(sessionARequest).rejects.toMatchObject({ status: 401 })
    expect(requests).toBe(1)
  })

  it('does not loop when the replay also returns unauthorized', async () => {
    const refresh = vi.fn(async () => 'synthetic-refreshed-token')
    const terminal = vi.fn()
    configureRuntime({
      getAccessToken: () => 'synthetic-expired-token',
      refreshAccessToken: refresh,
      onTerminalUnauthorized: terminal,
    })
    let requests = 0
    server.use(
      http.get(`${baseUrl}/protected`, () => {
        requests += 1
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
    )

    await expect(customInstance({ url: '/protected' })).rejects.toMatchObject({
      status: 401,
    })
    expect(refresh).toHaveBeenCalledOnce()
    expect(terminal).toHaveBeenCalledOnce()
    expect(requests).toBe(2)
  })

  it.each([
    ['/api/v1/auth/login', 'POST'],
    ['/api/v1/auth/refresh', 'POST'],
    ['/api/v1/auth/logout', 'POST'],
  ])('never refreshes an authentication endpoint: %s', async (url, method) => {
    const refresh = vi.fn()
    configureRuntime({ refreshAccessToken: refresh })
    server.use(
      http.post(`${baseUrl}${url}`, () =>
        HttpResponse.json({ title: 'Unauthorized' }, { status: 401 }),
      ),
    )

    await expect(customInstance({ url, method })).rejects.toMatchObject({
      status: 401,
    })
    expect(refresh).not.toHaveBeenCalled()
  })

  it.each([403, 500])(
    'does not refresh an HTTP %s response',
    async (status) => {
      const refresh = vi.fn()
      configureRuntime({ refreshAccessToken: refresh })
      server.use(
        http.get(`${baseUrl}/failure`, () =>
          HttpResponse.json({ title: 'Failure' }, { status }),
        ),
      )

      await expect(customInstance({ url: '/failure' })).rejects.toMatchObject({
        status,
      })
      expect(refresh).not.toHaveBeenCalled()
    },
  )

  it('does not replay an aborted request after the shared refresh completes', async () => {
    let releaseRefresh!: (token: string) => void
    const refresh = new Promise<string>((resolve) => {
      releaseRefresh = resolve
    })
    configureRuntime({
      getAccessToken: () => 'synthetic-expired-token',
      refreshAccessToken: () => refresh,
    })
    let requests = 0
    server.use(
      http.get(`${baseUrl}/abortable`, () => {
        requests += 1
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
    )
    const controller = new AbortController()
    const request = customInstance({
      url: '/abortable',
      signal: controller.signal,
    })
    await vi.waitFor(() => expect(requests).toBe(1))
    controller.abort()
    releaseRefresh('synthetic-refreshed-token')

    await expect(request).rejects.toMatchObject({
      classification: 'cancelled',
    } satisfies Partial<ApiError>)
    expect(requests).toBe(1)
  })

  it('keeps safe ProblemDetails for a terminal response', async () => {
    configureRuntime()
    server.use(
      http.get(`${baseUrl}/invalid`, () =>
        HttpResponse.json(
          { title: 'Validation failed', status: 400 },
          { status: 400 },
        ),
      ),
    )

    await expect(customInstance({ url: '/invalid' })).rejects.toMatchObject({
      status: 400,
      problem: { title: 'Validation failed' },
    })
  })
})
