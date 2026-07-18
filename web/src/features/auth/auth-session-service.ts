import type { AuthUserResponse, LoginRequest } from '@/api/generated/models'
import {
  login,
  logout as requestLogout,
  refreshSession,
} from '@/api/generated/endpoints'
import { ApiError } from '@/api/problem-details'
import { queryClient } from '@/app/query-client'
import { currentUserQueryKey } from '@/features/auth/current-user'
import { parseAuthSession } from '@/features/auth/auth-session-schema'
import { useAuthStore } from '@/stores/auth-store'

const RESTORATION_NETWORK_ERROR =
  'Your previous session could not be restored. You can still sign in.'
const RESTORATION_ORIGIN_ERROR =
  'Session restoration is unavailable because this web origin is not allowed.'
const MALFORMED_SESSION_ERROR =
  'The authentication response could not be verified. Please try again.'

type SessionRuntime = {
  invalidateRoutes: () => void
}

let runtime: SessionRuntime = { invalidateRoutes: () => undefined }
let sessionGeneration = 0
let initializationPromise: Promise<void> | null = null
let refreshPromise: Promise<string | null> | null = null

export class AuthSessionResponseError extends Error {
  constructor() {
    super(MALFORMED_SESSION_ERROR)
  }
}

export function configureAuthSessionRuntime(next: SessionRuntime) {
  runtime = next
}

export function getSessionGeneration() {
  return sessionGeneration
}

export function hasAuthenticatedSession() {
  const state = useAuthStore.getState()
  return state.status === 'authenticated' && state.accessToken !== null
}

function notifyRouteChange() {
  runtime.invalidateRoutes()
}

function establishSession(value: unknown, generation: number) {
  let session
  try {
    session = parseAuthSession(value)
  } catch {
    if (generation === sessionGeneration) clearSessionForGeneration(generation)
    throw new AuthSessionResponseError()
  }

  if (generation !== sessionGeneration) return null

  useAuthStore.getState().establishSession(session.accessToken)
  queryClient.setQueryData(currentUserQueryKey, session.user)
  notifyRouteChange()
  return session
}

function clearSessionForGeneration(
  generation: number,
  initializationError: string | null = null,
) {
  if (generation !== sessionGeneration) return false
  sessionGeneration += 1
  useAuthStore.getState().markAnonymous(initializationError)
  queryClient.clear()
  notifyRouteChange()
  return true
}

export function clearLocalSession(expectedGeneration?: number) {
  if (
    expectedGeneration !== undefined &&
    expectedGeneration !== sessionGeneration
  ) {
    return false
  }

  sessionGeneration += 1
  useAuthStore.getState().clearSession()
  queryClient.clear()
  notifyRouteChange()
  return true
}

export async function ensureSessionInitialized(): Promise<void> {
  if (useAuthStore.getState().status !== 'checking') return
  if (initializationPromise) return initializationPromise

  const generation = sessionGeneration
  initializationPromise = (async () => {
    try {
      const response = await refreshSession()
      establishSession(response, generation)
    } catch (error) {
      if (generation !== sessionGeneration) return

      if (error instanceof ApiError && error.status === 401) {
        clearSessionForGeneration(generation)
        return
      }

      if (error instanceof ApiError && error.status === 403) {
        clearSessionForGeneration(generation, RESTORATION_ORIGIN_ERROR)
        return
      }

      if (error instanceof AuthSessionResponseError) return

      clearSessionForGeneration(generation, RESTORATION_NETWORK_ERROR)
    }
  })().finally(() => {
    initializationPromise = null
  })

  return initializationPromise
}

export async function authenticate(
  credentials: Required<Pick<LoginRequest, 'email' | 'password'>>,
): Promise<AuthUserResponse> {
  sessionGeneration += 1
  const generation = sessionGeneration
  useAuthStore.getState().markAnonymous()
  const response = await login(credentials)
  const session = establishSession(response, generation)
  if (!session) throw new AuthSessionResponseError()
  return session.user
}

export async function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) return refreshPromise

  const generation = sessionGeneration
  refreshPromise = (async () => {
    try {
      const response = await refreshSession()
      return establishSession(response, generation)?.accessToken ?? null
    } catch (error) {
      if (generation === sessionGeneration)
        clearSessionForGeneration(generation)
      throw error
    }
  })().finally(() => {
    refreshPromise = null
  })

  return refreshPromise
}

export async function logout(): Promise<boolean> {
  clearLocalSession()
  try {
    await requestLogout()
    return true
  } catch {
    return false
  }
}

export function resetAuthSessionForTests() {
  sessionGeneration = 0
  initializationPromise = null
  refreshPromise = null
  runtime = { invalidateRoutes: () => undefined }
  queryClient.clear()
  useAuthStore.setState({
    accessToken: null,
    status: 'checking',
    initializationError: null,
  })
}
