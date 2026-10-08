import { Link, Outlet, useRouterState } from '@tanstack/react-router'
import {
  Boxes,
  CalendarDays,
  ClipboardCheck,
  ClipboardList,
  LayoutDashboard,
} from 'lucide-react'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { BrandMark } from '@/components/brand-mark'
import { LogoutButton } from '@/features/auth/logout-button'
import { useCurrentUser } from '@/features/auth/current-user'
import { Skeleton } from '@/components/ui/skeleton'
import {
  canReadSchedules,
  canReviewForms,
  getRequiredAppRoles,
  hasAnyRole,
} from '@/features/auth/app-route-access'

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
  const currentUser = useCurrentUser()
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  })
  const roles = currentUser.isSuccess ? currentUser.data.roles : []
  const requiredRoles = getRequiredAppRoles(pathname)
  const isRoleRestrictedRoute = requiredRoles !== null
  const hasPageAccess =
    requiredRoles === null ||
    (currentUser.isSuccess && hasAnyRole(roles, requiredRoles))
  const showSchedules = currentUser.isSuccess && canReadSchedules(roles)
  const showFormReview = currentUser.isSuccess && canReviewForms(roles)

  return (
    <div className="min-h-screen bg-[var(--page-background)] lg:grid lg:grid-cols-[15.5rem_1fr]">
      <aside className="hidden border-r border-[var(--border-soft)] bg-[var(--sidebar-background)] lg:sticky lg:top-0 lg:flex lg:h-screen lg:min-h-0 lg:flex-col lg:self-start lg:overflow-hidden">
        <div className="border-b border-[var(--border-soft)] px-6 py-5">
          <BrandMark compact />
        </div>
        <nav
          aria-label="Primary"
          className="min-h-0 flex-1 overflow-y-auto p-4"
        >
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
          {showSchedules && (
            <Link
              to="/app/schedules"
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
              <CalendarDays aria-hidden="true" className="size-5" />
              Schedules
            </Link>
          )}
          <Link
            to="/app/inspections"
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
            <ClipboardCheck aria-hidden="true" className="size-5" />
            Inspections
          </Link>
          {showFormReview && (
            <Link
              to="/app/preventive-maintenance-forms"
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
              <ClipboardList aria-hidden="true" className="size-5" />
              Form review
            </Link>
          )}
        </nav>
        <div className="shrink-0 space-y-4 border-t border-[var(--border-soft)] p-4">
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
        <div className="border-b border-[var(--border-soft)] bg-white px-4 py-2.5 lg:hidden">
          <nav
            aria-label="Primary"
            className="flex items-center gap-2 overflow-x-auto"
          >
            <Link
              to="/app/dashboard"
              activeProps={{
                'aria-current': 'page',
                className:
                  'flex items-center gap-2 rounded-lg bg-[var(--primary-active)] px-3 py-2 text-xs font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
              inactiveProps={{
                className:
                  'flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
            >
              <LayoutDashboard aria-hidden="true" className="size-4" />
              Dashboard
            </Link>
            <Link
              to="/app/assets"
              activeProps={{
                'aria-current': 'page',
                className:
                  'flex items-center gap-2 rounded-lg bg-[var(--primary-active)] px-3 py-2 text-xs font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
              inactiveProps={{
                className:
                  'flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
            >
              <Boxes aria-hidden="true" className="size-4" />
              Assets
            </Link>
            {showSchedules && (
              <Link
                to="/app/schedules"
                activeProps={{
                  'aria-current': 'page',
                  className:
                    'flex items-center gap-2 rounded-lg bg-[var(--primary-active)] px-3 py-2 text-xs font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
                }}
                inactiveProps={{
                  className:
                    'flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
                }}
              >
                <CalendarDays aria-hidden="true" className="size-4" />
                Schedules
              </Link>
            )}
            <Link
              to="/app/inspections"
              activeProps={{
                'aria-current': 'page',
                className:
                  'flex items-center gap-2 rounded-lg bg-[var(--primary-active)] px-3 py-2 text-xs font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
              inactiveProps={{
                className:
                  'flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
              }}
            >
              <ClipboardCheck aria-hidden="true" className="size-4" />
              Inspections
            </Link>
            {showFormReview && (
              <Link
                to="/app/preventive-maintenance-forms"
                activeProps={{
                  'aria-current': 'page',
                  className:
                    'flex items-center gap-2 rounded-lg bg-[var(--primary-active)] px-3 py-2 text-xs font-semibold text-white shadow-sm',
                }}
                inactiveProps={{
                  className:
                    'flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)]',
                }}
              >
                <ClipboardList aria-hidden="true" className="size-4" />
                Forms
              </Link>
            )}
          </nav>
        </div>

        <div className="border-b border-[var(--border-soft)] bg-white px-4 py-4 lg:hidden">
          <UserIdentity />
        </div>

        <main className="px-4 py-8 sm:px-6 lg:px-10 lg:py-10">
          {isRoleRestrictedRoute && currentUser.isPending ? (
            <div role="status" className="text-sm text-[var(--text-neutral)]">
              Checking page access…
            </div>
          ) : isRoleRestrictedRoute &&
            (currentUser.isError || !currentUser.data) ? (
            <section
              aria-labelledby="app-access-error-title"
              className="mx-auto max-w-2xl rounded-xl border border-[var(--border-soft)] bg-white p-6 shadow-sm"
            >
              <h1
                id="app-access-error-title"
                className="text-xl font-semibold text-[var(--text-primary)]"
              >
                Unable to verify access
              </h1>
              <p className="mt-2 text-sm text-[var(--text-secondary)]">
                Your account roles could not be loaded. Try checking access
                again.
              </p>
              <Button
                type="button"
                className="mt-4"
                onClick={() => void currentUser.refetch()}
              >
                Retry access check
              </Button>
            </section>
          ) : isRoleRestrictedRoute && !hasPageAccess ? (
            <section
              aria-labelledby="app-access-denied-title"
              className="mx-auto max-w-2xl rounded-xl border border-[var(--border-soft)] bg-white p-6 shadow-sm"
            >
              <h1
                id="app-access-denied-title"
                className="text-xl font-semibold text-[var(--text-primary)]"
              >
                Access denied
              </h1>
              <p className="mt-2 text-sm text-[var(--text-secondary)]">
                This page requires one of these roles:{' '}
                {requiredRoles?.join(', ')}.
              </p>
            </section>
          ) : (
            <Outlet />
          )}
        </main>
      </div>
    </div>
  )
}
