import axios, { type AxiosRequestConfig } from 'axios'
import { environment } from '../config/env'
import { normalizeApiError } from './problem-details'

type ApiRuntime = {
  getAccessToken: () => string | null
  onUnauthorized: () => void
}
let runtime: ApiRuntime = {
  getAccessToken: () => null,
  onUnauthorized: () => undefined,
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
  const token = runtime.getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    const normalized = normalizeApiError(error)
    if (normalized.status === 401) runtime.onUnauthorized()
    return Promise.reject(normalized)
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
