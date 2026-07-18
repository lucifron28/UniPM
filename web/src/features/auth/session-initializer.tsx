import { useEffect } from 'react'
import { ensureSessionInitialized } from '@/features/auth/auth-session-service'
import { useAuthStore } from '@/stores/auth-store'

export function SessionInitializer() {
  const status = useAuthStore((state) => state.status)

  useEffect(() => {
    void ensureSessionInitialized()
  }, [])

  if (status !== 'checking') return null
  return (
    <p className="sr-only" role="status" aria-live="polite">
      Restoring your session.
    </p>
  )
}
