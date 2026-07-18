import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/app/query-client'
import { currentUserQueryKey } from '@/features/auth/current-user'
import {
  authenticate,
  configureAuthSessionRuntime,
  ensureSessionInitialized,
  logout,
  refreshAccessToken,
} from '@/features/auth/auth-session-service'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const apiBase = 'http://localhost:5000/api/v1/auth'
const user = {
  id: '11111111-1111-4111-8111-111111111111',
  email: 'fictional.inspector@example.test',
  displayName: 'Fictional Inspector',
  roles: ['Inspector'],
}
const session = {
  accessToken: 'synthetic-session-token',
  expiresAtUtc: '2026-07-18T12:00:00Z',
  user,
}

function deferred() {
  let resolve!: () => void
  const promise = new Promise<void>((next) => {
    resolve = next
  })
  return { promise, resolve }
}

describe('authentication session coordinator', () => {
  it('treats a missing refresh cookie as an ordinary anonymous bootstrap', async () => {
    server.use(
      http.post(`${apiBase}/refresh`, () =>
        HttpResponse.json({ title: 'Unauthorized' }, { status: 401 }),
      ),
    )

    await ensureSessionInitialized()
    expect(useAuthStore.getState()).toMatchObject({
      status: 'anonymous',
      accessToken: null,
      initializationError: null,
    })
  })

  it('restores one session, sends no refresh body, and seeds current user', async () => {
    let refreshCount = 0
    server.use(
      http.post(`${apiBase}/refresh`, async ({ request }) => {
        refreshCount += 1
        expect(await request.text()).toBe('')
        return HttpResponse.json(session)
      }),
    )

    await Promise.all([
      ensureSessionInitialized(),
      ensureSessionInitialized(),
      ensureSessionInitialized(),
    ])

    expect(refreshCount).toBe(1)
    expect(useAuthStore.getState()).toMatchObject({
      status: 'authenticated',
      accessToken: session.accessToken,
    })
    expect(queryClient.getQueryData(currentUserQueryKey)).toEqual(user)
  })

  it('rejects malformed restoration data without keeping a partial token', async () => {
    server.use(
      http.post(`${apiBase}/refresh`, () =>
        HttpResponse.json({ ...session, user: { ...user, id: 'not-a-uuid' } }),
      ),
    )

    await ensureSessionInitialized()
    expect(useAuthStore.getState()).toMatchObject({
      status: 'anonymous',
      accessToken: null,
    })
    expect(queryClient.getQueryData(currentUserQueryKey)).toBeUndefined()
  })

  it.each([
    [
      403,
      'Session restoration is unavailable because this web origin is not allowed.',
    ],
    [
      'network',
      'Your previous session could not be restored. You can still sign in.',
    ],
  ])(
    'keeps login usable after a %s restoration failure',
    async (kind, message) => {
      server.use(
        http.post(`${apiBase}/refresh`, () =>
          kind === 'network'
            ? HttpResponse.error()
            : HttpResponse.json(
                { title: 'Forbidden' },
                { status: typeof kind === 'number' ? kind : 500 },
              ),
        ),
      )

      await ensureSessionInitialized()
      expect(useAuthStore.getState()).toMatchObject({
        status: 'anonymous',
        accessToken: null,
        initializationError: message,
      })
    },
  )

  it('does not let a stale bootstrap overwrite a newer successful login', async () => {
    const releaseRefresh = deferred()
    server.use(
      http.post(`${apiBase}/refresh`, async () => {
        await releaseRefresh.promise
        return HttpResponse.json({
          ...session,
          accessToken: 'stale-bootstrap-token',
        })
      }),
      http.post(`${apiBase}/login`, () =>
        HttpResponse.json({ ...session, accessToken: 'new-login-token' }),
      ),
    )

    const bootstrap = ensureSessionInitialized()
    await authenticate({
      email: 'fictional.inspector@example.test',
      password: 'synthetic-password',
    })
    releaseRefresh.resolve()
    await bootstrap

    expect(useAuthStore.getState().accessToken).toBe('new-login-token')
  })

  it('prevents a late refresh from restoring a session after logout', async () => {
    const releaseRefresh = deferred()
    server.use(
      http.post(`${apiBase}/refresh`, async () => {
        await releaseRefresh.promise
        return HttpResponse.json(session)
      }),
      http.post(
        `${apiBase}/logout`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    )
    useAuthStore.getState().establishSession('expired-synthetic-token')

    const refresh = refreshAccessToken()
    await logout()
    releaseRefresh.resolve()

    await expect(refresh).resolves.toBeNull()
    expect(useAuthStore.getState()).toMatchObject({
      status: 'anonymous',
      accessToken: null,
    })
  })

  it('does not let a stale refresh failure clear a newer login', async () => {
    const releaseRefresh = deferred()
    server.use(
      http.post(`${apiBase}/refresh`, async () => {
        await releaseRefresh.promise
        return HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
      }),
      http.post(`${apiBase}/login`, () =>
        HttpResponse.json({ ...session, accessToken: 'new-login-token' }),
      ),
    )
    useAuthStore.getState().establishSession('expired-synthetic-token')

    const refresh = refreshAccessToken()
    await authenticate({
      email: 'fictional.inspector@example.test',
      password: 'synthetic-password',
    })
    releaseRefresh.resolve()

    await expect(refresh).rejects.toMatchObject({ status: 401 })
    expect(useAuthStore.getState()).toMatchObject({
      status: 'authenticated',
      accessToken: 'new-login-token',
    })
  })

  it('clears shared refresh state after both failure and success', async () => {
    let attempt = 0
    server.use(
      http.post(`${apiBase}/refresh`, () => {
        attempt += 1
        return attempt === 1
          ? HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })
          : HttpResponse.json(session)
      }),
    )
    useAuthStore.getState().establishSession('expired-synthetic-token')

    await expect(refreshAccessToken()).rejects.toMatchObject({ status: 401 })
    useAuthStore.getState().establishSession('another-expired-token')
    await expect(refreshAccessToken()).resolves.toBe(session.accessToken)
    expect(attempt).toBe(2)
  })

  it('clears all query data and remains locally signed out when logout fails', async () => {
    const invalidateRoutes = vi.fn()
    configureAuthSessionRuntime({ invalidateRoutes })
    queryClient.setQueryData(['private-records'], [{ id: 'fictional' }])
    useAuthStore.getState().establishSession('synthetic-session-token')
    server.use(http.post(`${apiBase}/logout`, () => HttpResponse.error()))

    await expect(logout()).resolves.toBe(false)
    expect(queryClient.getQueryData(['private-records'])).toBeUndefined()
    expect(useAuthStore.getState().status).toBe('anonymous')
    expect(invalidateRoutes).toHaveBeenCalled()
  })
})
