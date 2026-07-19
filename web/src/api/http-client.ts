import axios, {
  AxiosHeaders,
  CanceledError,
  type AxiosRequestConfig,
} from 'axios'
import { environment } from '../config/env'
import { normalizeApiError } from './problem-details'

type ApiRuntime = {
  getAccessToken: () => string | null
  getSessionGeneration: () => number
  refreshAccessToken: (expectedGeneration: number) => Promise<string | null>
  onTerminalUnauthorized: (requestGeneration?: number) => void
}

let runtime: ApiRuntime = {
  getAccessToken: () => null,
  getSessionGeneration: () => 0,
  refreshAccessToken: async () => null,
  onTerminalUnauthorized: () => undefined,
}

export function resetApiRuntimeForTests() {
  runtime = {
    getAccessToken: () => null,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  }
}

declare module 'axios' {
  interface AxiosRequestConfig {
    skipAuthRefresh?: boolean
    authRetryAttempted?: boolean
    authSessionGeneration?: number
  }
}

const authPaths = new Set([
  '/api/v1/auth/login',
  '/api/v1/auth/refresh',
  '/api/v1/auth/logout',
])

function requestPath(config: AxiosRequestConfig) {
  try {
    return new URL(
      config.url ?? '',
      config.baseURL ?? environment.apiBaseUrl,
    ).pathname.replace(/\/+$/, '')
  } catch {
    return ''
  }
}

function isAuthEndpoint(config: AxiosRequestConfig) {
  return authPaths.has(requestPath(config))
}

export function configureApiRuntime(next: ApiRuntime) {
  runtime = next
}

export const httpClient = axios.create({
  baseURL: environment.apiBaseUrl,
  withCredentials: true,
  headers: { Accept: 'application/json' },
})

httpClient.interceptors.request.use((config) => {
  config.authSessionGeneration ??= runtime.getSessionGeneration()
  const token = runtime.getAccessToken()
  if (token && !isAuthEndpoint(config)) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    const normalized = normalizeApiError(error)
    if (!axios.isAxiosError(error) || normalized.status !== 401) {
      return Promise.reject(normalized)
    }

    const config = error.config
    if (!config || config.skipAuthRefresh || isAuthEndpoint(config)) {
      return Promise.reject(normalized)
    }

    const requestGeneration = config.authSessionGeneration
    if (
      requestGeneration === undefined ||
      requestGeneration !== runtime.getSessionGeneration()
    ) {
      return Promise.reject(normalized)
    }

    if (config.authRetryAttempted) {
      runtime.onTerminalUnauthorized(requestGeneration)
      return Promise.reject(normalized)
    }

    config.authRetryAttempted = true
    try {
      const token = await runtime.refreshAccessToken(requestGeneration)
      if (config.signal?.aborted) {
        return Promise.reject(normalizeApiError(new CanceledError()))
      }
      if (!token || requestGeneration !== runtime.getSessionGeneration()) {
        return Promise.reject(normalized)
      }

      const replayConfig = {
        ...config,
        headers: AxiosHeaders.from(config.headers),
      }
      replayConfig.headers.set('Authorization', `Bearer ${token}`)
      return httpClient.request(replayConfig)
    } catch (refreshError) {
      return Promise.reject(normalizeApiError(refreshError))
    }
  },
)

type GeneratedRequestConfig = Omit<AxiosRequestConfig, 'signal'> & {
  signal?: AbortSignal | undefined
}

export async function customInstance<T>(
  config: GeneratedRequestConfig,
): Promise<T> {
  const { signal, ...requestConfig } = config
  const response = await httpClient.request<T>({
    ...requestConfig,
    ...(signal ? { signal } : {}),
  })
  return response.data
}
