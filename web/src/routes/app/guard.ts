import { redirect } from '@tanstack/react-router'
import {
  ensureSessionInitialized,
  hasAuthenticatedSession,
} from '@/features/auth/auth-session-service'

export async function requireAppAccess(pathname: string) {
  await ensureSessionInitialized()
  if (hasAuthenticatedSession()) return

  throw redirect({
    to: '/login',
    search: { redirect: pathname.startsWith('/app') ? pathname : '/app' },
  })
}
