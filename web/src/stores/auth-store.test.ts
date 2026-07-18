import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/stores/auth-store'

describe('auth store', () => {
  it('starts without a persisted access token', () => {
    expect(useAuthStore.getState().accessToken).toBeNull()
  })

  it('keeps an access token in memory', () => {
    useAuthStore.getState().setAccessToken('ephemeral-token')
    expect(useAuthStore.getState().accessToken).toBe('ephemeral-token')
  })

  it('clears its access token when the session ends', () => {
    useAuthStore.getState().setAccessToken('ephemeral-token')
    useAuthStore.getState().clearSession()
    expect(useAuthStore.getState().accessToken).toBeNull()
  })
})
