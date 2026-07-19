import { create } from 'zustand'

export type AuthStatus = 'checking' | 'authenticated' | 'anonymous'

type AuthState = {
  accessToken: string | null
  status: AuthStatus
  initializationError: string | null
  beginSessionChecking: () => void
  establishSession: (token: string) => void
  markAnonymous: (initializationError?: string | null) => void
  clearSession: () => void
  clearInitializationError: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  status: 'checking',
  initializationError: null,
  beginSessionChecking: () =>
    set({ status: 'checking', initializationError: null }),
  establishSession: (accessToken) =>
    set({ accessToken, status: 'authenticated', initializationError: null }),
  markAnonymous: (initializationError = null) =>
    set({ accessToken: null, status: 'anonymous', initializationError }),
  clearSession: () =>
    set({ accessToken: null, status: 'anonymous', initializationError: null }),
  clearInitializationError: () => set({ initializationError: null }),
}))
