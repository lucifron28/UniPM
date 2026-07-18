import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  configureApiRuntime,
  customInstance,
  httpClient,
} from '@/api/http-client'
import { ApiError } from '@/api/problem-details'
import { server } from '@/test/server'

const resetRuntime = () =>
  configureApiRuntime({
    getAccessToken: () => null,
    onUnauthorized: () => undefined,
  })

afterEach(resetRuntime)

describe('HTTP client', () => {
  it('uses the configured API base URL and credentialed requests', () => {
    expect(httpClient.defaults.baseURL).toBe('http://localhost:5000')
    expect(httpClient.defaults.withCredentials).toBe(true)
  })

  it('does not attach an Authorization header without an in-memory token', async () => {
    server.use(
      http.get('http://localhost:5000/public', ({ request }) => {
        expect(request.headers.get('authorization')).toBeNull()
        return HttpResponse.json({ ok: true })
      }),
    )

    await expect(
      customInstance<{ ok: boolean }>({ url: '/public' }),
    ).resolves.toEqual({
      ok: true,
    })
  })

  it('attaches the current in-memory bearer token', async () => {
    configureApiRuntime({
      getAccessToken: () => 'access-token',
      onUnauthorized: () => undefined,
    })
    server.use(
      http.get('http://localhost:5000/protected', ({ request }) => {
        expect(request.headers.get('authorization')).toBe('Bearer access-token')
        return HttpResponse.json({ ok: true })
      }),
    )

    await expect(
      customInstance<{ ok: boolean }>({ url: '/protected' }),
    ).resolves.toEqual({ ok: true })
  })

  it('normalizes a terminal unauthorized response and invokes session cleanup', async () => {
    const onUnauthorized = vi.fn()
    configureApiRuntime({
      getAccessToken: () => 'access-token',
      onUnauthorized,
    })
    server.use(
      http.get('http://localhost:5000/unauthorized', () =>
        HttpResponse.json(
          { title: 'Unauthorized', status: 401 },
          { status: 401 },
        ),
      ),
    )

    await expect(
      customInstance({ url: '/unauthorized' }),
    ).rejects.toMatchObject({
      status: 401,
      problem: { title: 'Unauthorized' },
    } satisfies Partial<Pick<ApiError, 'status' | 'problem'>>)
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('forwards an AbortSignal to generated requests', async () => {
    server.use(
      http.get('http://localhost:5000/abortable', async ({ request }) => {
        await new Promise<void>((resolve) =>
          request.signal.addEventListener('abort', () => resolve(), {
            once: true,
          }),
        )
        return new HttpResponse(null, { status: 499 })
      }),
    )
    const controller = new AbortController()
    const request = customInstance({
      url: '/abortable',
      signal: controller.signal,
    })
    controller.abort()

    await expect(request).rejects.toBeInstanceOf(ApiError)
  })
})
