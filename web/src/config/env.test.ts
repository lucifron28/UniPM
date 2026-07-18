import { describe, expect, it } from 'vitest'
import { parseEnvironment } from '@/config/env'
describe('environment', () => {
  it('accepts the local API URL', () =>
    expect(
      parseEnvironment({ VITE_API_BASE_URL: ' http://localhost:5000/ ' })
        .apiBaseUrl,
    ).toBe('http://localhost:5000'))
  it('rejects malformed and credential-bearing URLs', () => {
    expect(() => parseEnvironment({ VITE_API_BASE_URL: 'bad' })).toThrow()
    expect(() =>
      parseEnvironment({
        VITE_API_BASE_URL: 'http://user:pass@localhost:5000',
      }),
    ).toThrow()
  })

  it('uses the local API default when no value is supplied', () =>
    expect(parseEnvironment({}).apiBaseUrl).toBe('http://localhost:5000'))

  it('rejects unsupported protocols', () => {
    expect(() =>
      parseEnvironment({ VITE_API_BASE_URL: 'ftp://localhost:5000' }),
    ).toThrow()
  })

  it('rejects a URL without a host', () => {
    expect(() => parseEnvironment({ VITE_API_BASE_URL: 'http://' })).toThrow()
  })
})
