import { z } from 'zod'

const apiBaseUrlSchema = z
  .string()
  .trim()
  .refine((value) => {
    if (value === '/') return true

    try {
      const url = new URL(value)
      return (
        (url.protocol === 'http:' || url.protocol === 'https:') &&
        !url.username &&
        !url.password
      )
    } catch {
      return false
    }
  }, 'VITE_API_BASE_URL must be / or an absolute HTTP or HTTPS URL without credentials.')

const defaultApiBaseUrl = import.meta.env.PROD ? '/' : 'http://localhost:5000'

export function parseEnvironment(input: {
  VITE_API_BASE_URL?: string
  defaultApiBaseUrl?: string
}) {
  const apiBaseUrl = apiBaseUrlSchema.parse(
    input.VITE_API_BASE_URL?.trim() ||
      input.defaultApiBaseUrl ||
      defaultApiBaseUrl,
  )
  return { apiBaseUrl: apiBaseUrl.replace(/\/+$/, '') }
}

export const environment = parseEnvironment(
  import.meta.env as { VITE_API_BASE_URL?: string },
)
