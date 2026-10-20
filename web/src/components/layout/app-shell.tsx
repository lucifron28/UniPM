import { Link, Outlet, useRouterState } from '@tanstack/react-router'
import {
  Boxes,
  CalendarDays,
  ChevronDown,
  ClipboardCheck,
  ClipboardList,
  LayoutDashboard,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { BrandMark } from '@/components/brand-mark'
import { QuickNavigation } from '@/components/layout/quick-navigation'
import { LogoutButton } from '@/features/auth/logout-button'
import { useCurrentUser } from '@/features/auth/current-user'
import { Skeleton } from '@/components/ui/skeleton'
import {
  canReadSchedules,
  canReviewForms,
  getRequiredAppRoles,
  hasAnyRole,
} from '@/features/auth/app-route-access'

const sidebarPreferenceKey = 'unipm.sidebar.collapsed'

function initials(displayName: string) {
  return displayName
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

function initialSidebarPreference() {
  try {
    return window.localStorage.getItem(sidebarPreferenceKey) === 'true'
  } catch {
    return false
  }
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

function AccountMenu({ sidebarCollapsed }: { sidebarCollapsed: boolean }) {
  const currentUser = useCurrentUser()
  const detailsRef = useRef<HTMLDetailsElement>(null)

  useEffect(() => {
    function handleOutsideClick(event: PointerEvent) {
      if (
        detailsRef.current?.open &&
        event.target instanceof Node &&
        !detailsRef.current.contains(event.target)
      ) {
        detailsRef.current.open = false
      }
    }
    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape' && detailsRef.current?.open) {
        detailsRef.current.open = false
        detailsRef.current.querySelector('summary')?.focus()
      }
    }
    document.addEventListener('pointerdown', handleOutsideClick)
    document.addEventListener('keydown', handleEscape)
    return () => {
      document.removeEventListener('pointerdown', handleOutsideClick)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [])

  if (currentUser.isPending) {
    return (
      <div
        className={`flex w-full shrink-0 items-center gap-2 ${sidebarCollapsed ? 'lg:justify-center' : ''}`}
        role="status"
      >
        <span className="sr-only">Loading signed-in user.</span>
        <Skeleton className="size-9 rounded-full" />
        <Skeleton
          className={`h-4 w-24 ${sidebarCollapsed ? 'hidden' : 'hidden sm:block'}`}
        />
      </div>
    )
  }
  if (currentUser.isError || !currentUser.data) {
    return (
      <div
        className={`flex w-full shrink-0 items-center gap-2 ${sidebarCollapsed ? 'lg:justify-center' : ''}`}
      >
        <span
          className={`text-xs text-[var(--text-neutral)] ${sidebarCollapsed ? 'lg:sr-only' : ''}`}
        >
          Account unavailable
        </span>
        <LogoutButton />
      </div>
    )
  }

  const user = currentUser.data
  return (
    <details ref={detailsRef} className="relative w-full shrink-0">
      <summary
        aria-label="Account menu"
        title={`${user.displayName} · ${user.roles.join(', ') || 'Account'}`}
        className={`flex min-h-11 w-full cursor-pointer list-none items-center gap-2 rounded-md px-2 text-left hover:bg-[var(--surface-muted)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)] [&::-webkit-details-marker]:hidden ${sidebarCollapsed ? 'lg:justify-center lg:px-0' : ''}`}
      >
        <span
          aria-hidden="true"
          className="flex size-9 shrink-0 items-center justify-center rounded-full bg-[var(--primary)] text-xs font-bold text-white"
        >
          {initials(user.displayName)}
        </span>
        <span
          className={`min-w-0 ${sidebarCollapsed ? 'lg:hidden' : 'hidden sm:block'}`}
        >
          <span className="block max-w-36 truncate text-sm font-semibold text-[var(--text-primary)]">
            {user.displayName}
          </span>
          <span className="block max-w-36 truncate text-xs text-[var(--text-neutral)]">
            {user.roles.join(', ') || 'Account'}
          </span>
        </span>
        <ChevronDown
          aria-hidden="true"
          className={`size-4 text-[var(--text-neutral)] ${sidebarCollapsed ? 'lg:hidden' : ''}`}
        />
        <span className="sr-only">
          {user.displayName}, {user.roles.join(', ') || 'Account'}
        </span>
      </summary>
      <div className="absolute bottom-full left-0 z-50 mb-2 w-[min(18rem,calc(100vw-2rem))] rounded-md border border-[var(--border-soft)] bg-white p-3 shadow-md">
        <p className="text-xs font-semibold tracking-wide text-[var(--text-neutral)] uppercase">
          Signed in as
        </p>
        <p className="mt-1 text-sm font-semibold break-all text-[var(--text-primary)]">
          {user.displayName}
        </p>
        <p className="text-xs break-all text-[var(--text-neutral)]">
          {user.email}
        </p>
        {user.roles.length > 0 && (
          <div
            className="mt-3 flex flex-wrap gap-1"
            aria-label="Assigned roles"
          >
            {user.roles.map((role) => (
              <Badge key={role} className="px-2 py-0.5 text-[0.65rem]">
                {role}
              </Badge>
            ))}
          </div>
        )}
        <div className="mt-3 border-t border-[var(--border-soft)] pt-2">
          <LogoutButton />
        </div>
      </div>
    </details>
  )
}

export function AppShell() {
  const currentUser = useCurrentUser()
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  })
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    initialSidebarPreference,
  )
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false)
  const mobileToggleRef = useRef<HTMLButtonElement>(null)
  const mobileCloseRef = useRef<HTMLButtonElement>(null)
  const roles = currentUser.isSuccess ? currentUser.data.roles : []
  const requiredRoles = getRequiredAppRoles(pathname)
  const isRoleRestrictedRoute = requiredRoles !== null
  const hasPageAccess =
    requiredRoles === null ||
    (currentUser.isSuccess && hasAnyRole(roles, requiredRoles))
  const showSchedules = currentUser.isSuccess && canReadSchedules(roles)
  const showFormReview = currentUser.isSuccess && canReviewForms(roles)
  const navigationItems = [
    {
      to: '/app/dashboard',
      label: 'Dashboard',
      icon: LayoutDashboard,
      visible: true,
    },
    { to: '/app/assets', label: 'Assets', icon: Boxes, visible: true },
    {
      to: '/app/schedules',
      label: 'Schedules',
      icon: CalendarDays,
      visible: showSchedules,
    },
    {
      to: '/app/inspections',
      label: 'Inspections',
      icon: ClipboardCheck,
      visible: true,
    },
    {
      to: '/app/preventive-maintenance-forms',
      label: 'Form review',
      icon: ClipboardList,
      visible: showFormReview,
    },
  ] as const

  useEffect(() => {
    try {
      window.localStorage.setItem(
        sidebarPreferenceKey,
        String(sidebarCollapsed),
      )
    } catch {
      // Browsers with storage disabled still support the current session.
    }
  }, [sidebarCollapsed])

  useEffect(() => {
    if (!mobileNavigationOpen) return
    mobileCloseRef.current?.focus()
    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMobileNavigationOpen(false)
        mobileToggleRef.current?.focus()
      }
    }
    document.addEventListener('keydown', handleEscape)
    return () => document.removeEventListener('keydown', handleEscape)
  }, [mobileNavigationOpen])

  return (
    <div
      className="app-shell min-h-dvh bg-[var(--page-background)]"
      data-sidebar-collapsed={sidebarCollapsed}
    >
      {mobileNavigationOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          className="fixed inset-0 z-40 bg-black/35 lg:hidden"
          onClick={() => {
            setMobileNavigationOpen(false)
            mobileToggleRef.current?.focus()
          }}
        />
      )}
      <aside
        aria-label="Application navigation"
        className={`app-sidebar fixed inset-y-0 left-0 z-50 w-[min(18rem,calc(100vw-3rem))] flex-col border-r border-[var(--border-soft)] bg-[var(--sidebar-background)] lg:relative lg:z-auto lg:flex lg:h-full lg:min-h-0 lg:w-auto ${mobileNavigationOpen ? 'flex' : 'hidden'}`}
      >
        <div
          className={`flex shrink-0 items-center gap-2 border-b border-[var(--border-soft)] p-3 ${sidebarCollapsed ? 'lg:flex-col lg:items-center' : 'lg:justify-between lg:px-6 lg:py-5'}`}
        >
          <div className="min-w-0 flex-1 lg:hidden">
            <BrandMark compact />
          </div>
          {sidebarCollapsed ? (
            <img
              src="/unipm-logo.png"
              alt="UniPM"
              className="hidden size-10 object-contain lg:block"
            />
          ) : (
            <BrandMark compact className="hidden min-w-0 lg:flex" />
          )}
          <button
            ref={mobileCloseRef}
            type="button"
            aria-label="Close navigation"
            className="sidebar-control flex size-9 shrink-0 items-center justify-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-muted)] focus-visible:outline-2 focus-visible:outline-[var(--primary)] lg:hidden"
            onClick={() => {
              setMobileNavigationOpen(false)
              mobileToggleRef.current?.focus()
            }}
          >
            <X aria-hidden="true" className="size-5" />
          </button>
          <button
            type="button"
            aria-label={
              sidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'
            }
            aria-controls="primary-navigation"
            aria-expanded={!sidebarCollapsed}
            className="sidebar-control hidden size-9 shrink-0 items-center justify-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-muted)] focus-visible:outline-2 focus-visible:outline-[var(--primary)] lg:flex"
            onClick={() => setSidebarCollapsed((value) => !value)}
          >
            {sidebarCollapsed ? (
              <PanelLeftOpen aria-hidden="true" className="size-5" />
            ) : (
              <PanelLeftClose aria-hidden="true" className="size-5" />
            )}
          </button>
        </div>
        <nav
          id="primary-navigation"
          aria-label="Primary"
          className="flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto p-2"
        >
          <QuickNavigation
            roles={roles}
            sidebarCollapsed={sidebarCollapsed}
            onNavigate={() => {
              if (!mobileNavigationOpen) return
              mobileToggleRef.current?.focus()
              setMobileNavigationOpen(false)
            }}
          />
          {navigationItems
            .filter((item) => item.visible)
            .map(({ to, label, icon: Icon }) => (
              <Link
                key={to}
                to={to}
                title={sidebarCollapsed ? label : undefined}
                onClick={() => setMobileNavigationOpen(false)}
                activeProps={{
                  'aria-current': 'page',
                  className:
                    'sidebar-nav-link flex min-h-10 items-center gap-3 rounded-lg bg-[var(--primary-active)] px-4 py-3 text-sm font-semibold text-white shadow-sm',
                }}
                inactiveProps={{
                  className:
                    'sidebar-nav-link flex min-h-10 items-center gap-3 rounded-lg px-4 py-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)]',
                }}
              >
                <Icon aria-hidden="true" className="size-5 shrink-0" />
                <span className="sidebar-nav-label">{label}</span>
              </Link>
            ))}
        </nav>
        <div className="mt-auto shrink-0 border-t border-[var(--border-soft)] p-2">
          <AccountMenu sidebarCollapsed={sidebarCollapsed} />
        </div>
      </aside>

      <div className="app-shell-body min-w-0 lg:min-h-0">
        <header className="app-page-header relative z-30 flex min-h-18 items-center justify-between gap-4 border-b border-[var(--border-soft)] bg-white px-4 py-3 sm:px-6 lg:px-8">
          <div className="flex min-w-0 items-center gap-3">
            <button
              ref={mobileToggleRef}
              type="button"
              aria-label="Open navigation"
              aria-controls="primary-navigation"
              aria-expanded={mobileNavigationOpen}
              className="sidebar-control flex size-10 shrink-0 items-center justify-center rounded-md border border-[var(--border-soft)] bg-white text-[var(--text-primary)] focus-visible:outline-2 focus-visible:outline-[var(--primary)] lg:hidden"
              onClick={() => setMobileNavigationOpen(true)}
            >
              <Menu aria-hidden="true" className="size-5" />
            </button>
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
          </div>
          <div className="flex items-center gap-2 lg:hidden">
            <LogoutButton />
          </div>
        </header>
        <main className="app-shell-main px-4 py-8 sm:px-6 lg:min-h-0 lg:flex-1 lg:overflow-hidden lg:px-10 lg:py-8">
          <div className="app-workspace min-w-0 lg:h-full lg:min-h-0 lg:overflow-x-hidden lg:overflow-y-auto">
            {isRoleRestrictedRoute && currentUser.isPending ? (
              <div role="status" className="text-sm text-[var(--text-neutral)]">
                Checking page access…
              </div>
            ) : isRoleRestrictedRoute &&
              (currentUser.isError || !currentUser.data) ? (
              <section
                aria-labelledby="app-access-error-title"
                className="mx-auto max-w-2xl rounded-lg border border-[var(--border-soft)] bg-white p-6"
              >
                <h2
                  id="app-access-error-title"
                  className="text-xl font-semibold text-[var(--text-primary)]"
                >
                  Unable to verify access
                </h2>
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
                className="mx-auto max-w-2xl rounded-lg border border-[var(--border-soft)] bg-white p-6"
              >
                <h2
                  id="app-access-denied-title"
                  className="text-xl font-semibold text-[var(--text-primary)]"
                >
                  Access denied
                </h2>
                <p className="mt-2 text-sm text-[var(--text-secondary)]">
                  This page requires one of these roles:{' '}
                  {requiredRoles?.join(', ')}.
                </p>
              </section>
            ) : (
              <Outlet />
            )}
          </div>
        </main>
      </div>
    </div>
  )
}
