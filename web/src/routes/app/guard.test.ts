import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/stores/auth-store'
import { requireAppAccess } from '@/routes/app/guard'

describe('protected app route guard', () => {
  it('allows an authenticated in-memory session', () => {
    useAuthStore.getState().setAccessToken('access-token')
    expect(() =>
      requireAppAccess(
        () => useAuthStore.getState().accessToken,
        '/app/dashboard',
      ),
    ).not.toThrow()
  })

  it('redirects an unauthenticated session to the login placeholder', () => {
    expect(() => requireAppAccess(() => null, '/app/dashboard')).toThrow()
  })

  it('reevaluates access after the session is cleared', () => {
    useAuthStore.getState().setAccessToken('access-token')
    useAuthStore.getState().clearSession()
    expect(() =>
      requireAppAccess(
        () => useAuthStore.getState().accessToken,
        '/app/dashboard',
      ),
    ).toThrow()
  })
})
