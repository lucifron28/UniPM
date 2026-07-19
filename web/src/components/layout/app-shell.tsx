import { Link, Outlet } from '@tanstack/react-router'
import { Boxes, LayoutDashboard } from 'lucide-react'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { BrandMark } from '@/components/brand-mark'
import { LogoutButton } from '@/features/auth/logout-button'
import { useCurrentUser } from '@/features/auth/current-user'
import { Skeleton } from '@/components/ui/skeleton'

function initials(displayName: string) {
  return displayName
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

export function UserIdentity() {
  const currentUser = useCurrentUser()

  if (currentUser.isPending) {
    return (
      <div className="flex items-center gap-3" role="status">
        <span className="sr-only">Loading signed-in user.</span>
        <Skeleton className="size-10 rounded-full" />
        <div className="space-y-2">
          <Skeleton className="h-3 w-28" />
          <Skeleton className="h-3 w-36" />
        </div>
      </div>
    )
  }

  if (currentUser.isError || !currentUser.data) {
    return (
      <Alert className="py-2 text-xs">
        Signed-in user details are temporarily unavailable.
      </Alert>
    )
  }

  const user = currentUser.data
  return (
    <div className="flex min-w-0 items-center gap-3">
      <div
        aria-hidden="true"
        className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--primary)] text-sm font-bold text-white"
      >
        {initials(user.displayName)}
      </div>
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-[var(--text-primary)]">
          {user.displayName}
        </p>
        <p className="truncate text-xs text-[var(--text-neutral)]">
          {user.email}
        </p>
        {user.roles.length > 0 && (
          <div
            className="mt-1 flex flex-wrap gap-1"
            aria-label="Assigned roles"
          >
            {user.roles.map((role) => (
              <Badge key={role} className="px-2 py-0.5 text-[0.65rem]">
                {role}
              </Badge>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

export function AppShell() {
  return (
    <div className="min-h-screen bg-[var(--page-background)] lg:grid lg:grid-cols-[15.5rem_1fr]">
      <aside className="hidden border-r border-[var(--border-soft)] bg-[var(--sidebar-background)] lg:flex lg:min-h-screen lg:flex-col">
        <div className="border-b border-[var(--border-soft)] px-6 py-5">
          <BrandMark compact />
        </div>
        <nav aria-label="Primary" className="flex-1 p-4">
          <Link
            to="/app/dashboard"
            activeProps={{
              'aria-current': 'page',
              className:
                'flex items-center gap-3 rounded-lg bg-[var(--primary-active)] px-4 py-3 text-sm font-semibold text-white shadow-sm',
            }}
            inactiveProps={{
              className:
                'flex items-center gap-3 rounded-lg px-4 py-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)]',
            }}
          >
            <LayoutDashboard aria-hidden="true" className="size-5" />
            Dashboard
          </Link>
          <Link
            to="/app/assets"
            activeProps={{
              'aria-current': 'page',
              className:
                'mt-1 flex items-center gap-3 rounded-lg bg-[var(--primary-active)] px-4 py-3 text-sm font-semibold text-white shadow-sm',
            }}
            inactiveProps={{
              className:
                'mt-1 flex items-center gap-3 rounded-lg px-4 py-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)]',
            }}
          >
            <Boxes aria-hidden="true" className="size-5" />
            Assets
          </Link>
        </nav>
        <div className="space-y-4 border-t border-[var(--border-soft)] p-4">
          <UserIdentity />
          <LogoutButton />
        </div>
      </aside>

      <div className="min-w-0">
        <header className="flex min-h-18 items-center justify-between gap-4 border-b border-[var(--border-soft)] bg-white px-4 py-3 sm:px-6 lg:px-8">
          <div className="lg:hidden">
            <BrandMark compact />
          </div>
          <div className="hidden lg:block">
            <p className="text-sm font-semibold text-[var(--text-primary)]">
              Preventive Maintenance Portal
            </p>
            <p className="text-xs text-[var(--text-neutral)]">
              Authenticated institutional session
            </p>
          </div>
          <div className="flex items-center gap-2 lg:hidden">
            <LogoutButton />
          </div>
        </header>

        <div className="border-b border-[var(--border-soft)] bg-white px-4 py-4 lg:hidden">
          <UserIdentity />
        </div>

        <main className="px-4 py-8 sm:px-6 lg:px-10 lg:py-10">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
