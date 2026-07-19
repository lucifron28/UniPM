import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { LogOut } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { logout } from '@/features/auth/auth-session-service'

export function LogoutButton() {
  const [isPending, setIsPending] = useState(false)
  const navigate = useNavigate()

  return (
    <Button
      type="button"
      disabled={isPending}
      aria-label="Sign out"
      onClick={async () => {
        setIsPending(true)
        const confirmed = await logout()
        if (!confirmed) {
          toast.warning(
            'You are signed out locally, but server-session revocation could not be confirmed. A full reload may restore the session if the refresh cookie is still valid.',
          )
        }
        await navigate({ to: '/login', replace: true })
      }}
      className="min-h-9 bg-transparent px-3 text-[var(--primary)] shadow-none hover:bg-[var(--surface-muted)]"
    >
      <LogOut aria-hidden="true" className="mr-2 size-4" />
      {isPending ? 'Signing out...' : 'Sign out'}
    </Button>
  )
}
