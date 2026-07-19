import axios from 'axios'

export type ApiProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
  extensions?: Record<string, unknown>
}

export class ApiError extends Error {
  readonly status: number | undefined
  readonly problem: ApiProblemDetails | undefined
  readonly classification: 'problem' | 'network' | 'cancelled' | 'unknown'

  constructor(
    message: string,
    status?: number,
    problem?: ApiProblemDetails,
    classification: 'problem' | 'network' | 'cancelled' | 'unknown' = 'unknown',
  ) {
    super(message)
    this.status = status
    this.problem = problem
    this.classification = classification
  }
}

function strings(value: unknown): string[] | undefined {
  return Array.isArray(value) && value.every((item) => typeof item === 'string')
    ? value
    : undefined
}

export function normalizeApiError(error: unknown): ApiError {
  if (axios.isCancel(error))
    return new ApiError(
      'The request was cancelled.',
      undefined,
      undefined,
      'cancelled',
    )
  if (!axios.isAxiosError(error))
    return new ApiError('Something went wrong. Please try again.')
  if (!error.response)
    return new ApiError(
      'The service could not be reached. Please try again.',
      undefined,
      undefined,
      'network',
    )
  const body = error.response.data
  if (typeof body !== 'object' || body === null || Array.isArray(body)) {
    return new ApiError(
      'The request could not be completed.',
      error.response.status,
    )
  }
  const record = body as Record<string, unknown>
  const errors = Object.fromEntries(
    Object.entries(record.errors ?? {}).flatMap(([key, value]) => {
      const fieldErrors = strings(value)
      return fieldErrors ? [[key, fieldErrors]] : []
    }),
  )
  const problem: ApiProblemDetails = {
    status:
      typeof record.status === 'number' ? record.status : error.response.status,
    ...(typeof record.type === 'string' ? { type: record.type } : {}),
    ...(typeof record.title === 'string' ? { title: record.title } : {}),
    ...(typeof record.detail === 'string' ? { detail: record.detail } : {}),
    ...(typeof record.instance === 'string'
      ? { instance: record.instance }
      : {}),
    ...(Object.keys(errors).length > 0 ? { errors } : {}),
  }
  return new ApiError(
    problem.detail ?? problem.title ?? 'The request could not be completed.',
    error.response.status,
    problem,
    'problem',
  )
}
