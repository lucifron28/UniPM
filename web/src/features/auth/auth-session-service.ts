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

type RefreshPurpose = 'initialization' | 'access'

type RefreshFlight = {
  generation: number
  controller: AbortController
  promise: Promise<string | null>
}

let runtime: SessionRuntime = { invalidateRoutes: () => undefined }
let sessionGeneration = 0
let initializationPromise: Promise<void> | null = null
let refreshFlight: RefreshFlight | null = null
let cookieMutationGeneration: number | null = null
let cookieMutationTail: Promise<void> = Promise.resolve()

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

async function settleRefresh(flight: RefreshFlight | null) {
  if (!flight) return

  try {
    await flight.promise
  } catch {
    // Login or logout remains the next and final cookie writer after failure.
  }
}

function enqueueCookieMutation<T>(operation: () => Promise<T>): Promise<T> {
  const previous = cookieMutationTail
  let release!: () => void
  cookieMutationTail = new Promise<void>((resolve) => {
    release = resolve
  })

  return (async () => {
    await previous
    try {
      return await operation()
    } finally {
      release()
    }
  })()
}

function createRefreshFlight(
  generation: number,
  purpose: RefreshPurpose,
): RefreshFlight {
  const controller = new AbortController()
  const flight: RefreshFlight = {
    generation,
    controller,
    promise: Promise.resolve(null),
  }
  flight.promise = (async () => {
    try {
      const response = await refreshSession(controller.signal)
      return establishSession(response, generation)?.accessToken ?? null
    } catch (error) {
      if (generation !== sessionGeneration) return null

      if (purpose === 'initialization') {
        if (error instanceof ApiError && error.status === 401) {
          clearSessionForGeneration(generation)
          return null
        }

        if (error instanceof ApiError && error.status === 403) {
          clearSessionForGeneration(generation, RESTORATION_ORIGIN_ERROR)
          return null
        }

        if (error instanceof AuthSessionResponseError) return null

        clearSessionForGeneration(generation, RESTORATION_NETWORK_ERROR)
        return null
      }

      clearSessionForGeneration(generation)
      throw error
    }
  })().finally(() => {
    if (refreshFlight === flight) refreshFlight = null
  })

  refreshFlight = flight
  return flight
}

async function refreshForGeneration(
  generation: number,
  purpose: RefreshPurpose,
): Promise<string | null> {
  while (refreshFlight) {
    if (refreshFlight.generation === generation) return refreshFlight.promise

    const staleFlight = refreshFlight
    await settleRefresh(staleFlight)
    if (generation !== sessionGeneration) return null
  }

  if (cookieMutationGeneration !== null) return null
  return createRefreshFlight(generation, purpose).promise
}

export async function ensureSessionInitialized(): Promise<void> {
  if (useAuthStore.getState().status !== 'checking') return
  if (initializationPromise) return initializationPromise

  const generation = sessionGeneration
  initializationPromise = (async () => {
    await refreshForGeneration(generation, 'initialization')
  })().finally(() => {
    initializationPromise = null
  })

  return initializationPromise
}

export async function authenticate(
  credentials: Required<Pick<LoginRequest, 'email' | 'password'>>,
): Promise<AuthUserResponse> {
  const staleFlight = refreshFlight
  sessionGeneration += 1
  const generation = sessionGeneration
  cookieMutationGeneration = generation
  useAuthStore.getState().markAnonymous()
  try {
    return await enqueueCookieMutation(async () => {
      await settleRefresh(staleFlight)
      const response = await login(credentials)
      const session = establishSession(response, generation)
      if (!session) throw new AuthSessionResponseError()
      return session.user
    })
  } finally {
    if (cookieMutationGeneration === generation) {
      cookieMutationGeneration = null
    }
  }
}

export async function refreshAccessToken(): Promise<string | null> {
  const generation = sessionGeneration
  return refreshForGeneration(generation, 'access')
}

export async function logout(): Promise<boolean> {
  const staleFlight = refreshFlight
  clearLocalSession()
  const generation = sessionGeneration
  cookieMutationGeneration = generation
  try {
    return await enqueueCookieMutation(async () => {
      await settleRefresh(staleFlight)
      try {
        await requestLogout()
        return true
      } catch {
        return false
      }
    })
  } finally {
    if (cookieMutationGeneration === generation) {
      cookieMutationGeneration = null
    }
  }
}

export function resetAuthSessionForTests() {
  refreshFlight?.controller.abort()
  sessionGeneration = 0
  initializationPromise = null
  refreshFlight = null
  cookieMutationGeneration = null
  cookieMutationTail = Promise.resolve()
  runtime = { invalidateRoutes: () => undefined }
  queryClient.clear()
  useAuthStore.setState({
    accessToken: null,
    status: 'checking',
    initializationError: null,
  })
}
