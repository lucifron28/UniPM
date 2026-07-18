import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/stores/auth-store'

describe('auth store', () => {
  it('starts in the checking state without a persisted token', () => {
    expect(useAuthStore.getState()).toMatchObject({
      accessToken: null,
      status: 'checking',
      initializationError: null,
    })
  })

  it('keeps an authenticated access token only in memory', () => {
    useAuthStore.getState().establishSession('synthetic-access-token')
    expect(useAuthStore.getState()).toMatchObject({
      accessToken: 'synthetic-access-token',
      status: 'authenticated',
    })
    expect(localStorage).toHaveLength(0)
    expect(sessionStorage).toHaveLength(0)
  })

  it('clears token and restoration errors when the local session ends', () => {
    useAuthStore
      .getState()
      .markAnonymous('A safe restoration message for testing.')
    useAuthStore.getState().establishSession('synthetic-access-token')
    useAuthStore.getState().clearSession()
    expect(useAuthStore.getState()).toMatchObject({
      accessToken: null,
      status: 'anonymous',
      initializationError: null,
    })
  })
})
