import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { requireAppAccess } from '@/routes/app/guard'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const refreshUrl = 'http://localhost:5000/api/v1/auth/refresh'
const session = {
  accessToken: 'synthetic-restored-token',
  expiresAtUtc: '2026-07-18T12:00:00Z',
  user: {
    id: '11111111-1111-4111-8111-111111111111',
    email: 'fictional.user@example.test',
    displayName: 'Fictional User',
    roles: ['Inspector'],
  },
}

describe('protected app route guard', () => {
  it('waits for successful session restoration before allowing access', async () => {
    server.use(http.post(refreshUrl, () => HttpResponse.json(session)))
    await expect(requireAppAccess('/app/dashboard')).resolves.toBeUndefined()
    expect(useAuthStore.getState().status).toBe('authenticated')
  })

  it('redirects only after anonymous restoration resolves', async () => {
    server.use(
      http.post(refreshUrl, () =>
        HttpResponse.json({ title: 'Unauthorized' }, { status: 401 }),
      ),
    )
    await expect(requireAppAccess('/app/dashboard')).rejects.toBeDefined()
    expect(useAuthStore.getState().status).toBe('anonymous')
  })

  it('reevaluates access after the session is cleared', async () => {
    useAuthStore.getState().establishSession('synthetic-access-token')
    useAuthStore.getState().clearSession()
    await expect(requireAppAccess('/app/dashboard')).rejects.toBeDefined()
  })
})
