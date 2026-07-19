import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/test/server'
import { resetApiRuntimeForTests } from '@/api/http-client'
import { resetAuthSessionForTests } from '@/features/auth/auth-session-service'
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
  localStorage.clear()
  sessionStorage.clear()
  resetApiRuntimeForTests()
  resetAuthSessionForTests()
})
afterAll(() => server.close())
