import { describe, expect, it } from 'vitest'
import { isInternalAppRedirect } from '@/routes/login-redirect'

describe('internal login redirects', () => {
  it.each(['/app', '/app/dashboard', '/app/assets/123'])(
    'accepts internal app routes: %s',
    (value) => {
      expect(isInternalAppRedirect(value)).toBe(true)
    },
  )

  it.each([
    '/',
    '//evil.example',
    '/\\evil.example',
    'https://evil.example/app',
    '/app\u0000/dashboard',
  ])('rejects unsafe or non-app redirects: %s', (value) => {
    expect(isInternalAppRedirect(value)).toBe(false)
  })
})
