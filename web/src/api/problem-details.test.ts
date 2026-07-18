import { AxiosError } from 'axios'
import { describe, expect, it } from 'vitest'
import { normalizeApiError } from '@/api/problem-details'
describe('ProblemDetails normalization', () => {
  it('degrades unknown payloads safely', () =>
    expect(normalizeApiError(new Error('raw')).message).toBe(
      'Something went wrong. Please try again.',
    ))

  it('preserves safe RFC 7807 details', () => {
    const error = new AxiosError('bad request')
    error.response = {
      data: {
        type: 'https://example.test/problems/validation',
        title: 'Validation failed',
        status: 400,
        detail: 'Choose a valid asset.',
      },
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: error.config!,
    }

    const normalized = normalizeApiError(error)
    expect(normalized.status).toBe(400)
    expect(normalized.message).toBe('Choose a valid asset.')
    expect(normalized.problem).toMatchObject({
      title: 'Validation failed',
      detail: 'Choose a valid asset.',
    })
  })

  it('does not expose an arbitrary Axios response body', () => {
    const error = new AxiosError('server error')
    error.response = {
      data: { secret: 'do not show this' },
      status: 500,
      statusText: 'Server Error',
      headers: {},
      config: error.config!,
    }

    expect(normalizeApiError(error).message).toBe(
      'The request could not be completed.',
    )
  })

  it('classifies a missing Axios response as a safe network error', () => {
    const error = new AxiosError('network down')
    expect(normalizeApiError(error)).toMatchObject({
      classification: 'network',
      message: 'The service could not be reached. Please try again.',
    })
  })

  it('keeps valid field errors in the problem details', () => {
    const error = new AxiosError('bad request')
    error.response = {
      data: {
        title: 'Validation failed',
        errors: { assetCode: ['Required'] },
      },
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: error.config!,
    }

    expect(normalizeApiError(error).problem?.errors).toEqual({
      assetCode: ['Required'],
    })
  })

  it('ignores malformed field-error values', () => {
    const error = new AxiosError('bad request')
    error.response = {
      data: { errors: { assetCode: 'not an array' } },
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: error.config!,
    }

    expect(normalizeApiError(error).problem?.errors).toBeUndefined()
  })

  it('uses a generic message for array response bodies', () => {
    const error = new AxiosError('server error')
    error.response = {
      data: ['unstructured'],
      status: 500,
      statusText: 'Server Error',
      headers: {},
      config: error.config!,
    }

    expect(normalizeApiError(error).message).toBe(
      'The request could not be completed.',
    )
  })
})
