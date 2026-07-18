import { z } from 'zod'

const apiBaseUrlSchema = z
  .string()
  .trim()
  .url()
  .refine((value) => {
    const url = new URL(value)
    return (
      (url.protocol === 'http:' || url.protocol === 'https:') &&
      !url.username &&
      !url.password
    )
  }, 'VITE_API_BASE_URL must be an absolute HTTP or HTTPS URL without credentials.')

export function parseEnvironment(input: { VITE_API_BASE_URL?: string }) {
  const apiBaseUrl = apiBaseUrlSchema.parse(
    input.VITE_API_BASE_URL?.trim() || 'http://localhost:5000',
  )
  return { apiBaseUrl: apiBaseUrl.replace(/\/+$/, '') }
}

export const environment = parseEnvironment(
  import.meta.env as { VITE_API_BASE_URL?: string },
)
