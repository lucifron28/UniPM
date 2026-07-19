import { createFileRoute, redirect } from '@tanstack/react-router'
import {
  ensureSessionInitialized,
  hasAuthenticatedSession,
} from '@/features/auth/auth-session-service'

export const Route = createFileRoute('/')({
  beforeLoad: async () => {
    await ensureSessionInitialized()
    throw redirect({
      to: hasAuthenticatedSession() ? '/app/dashboard' : '/login',
      replace: true,
    })
  },
})
