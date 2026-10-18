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
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { BrandMark } from '@/components/brand-mark'
import { PageHeadingHostContext } from '@/components/layout/page-heading-context'
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

function fallbackPageHeading(pathname: string) {
  const [, area, section, item, action] = pathname.split('/')
  if (area !== 'app') {
    return { title: 'UniPM', description: 'Preventive maintenance portal' }
  }

  if (section === 'dashboard') {
    return {
      title: 'Preventive Maintenance Dashboard',
      description: 'Review preventive maintenance with filters or a question.',
    }
  }
  if (section === 'analytics') {
    return {
      title: 'Preventive Maintenance Dashboard',
      description: 'Choose how you want to review PM results.',
    }
  }
  if (section === 'assets') {
    return item === 'new'
      ? {
          title: 'Add asset',
          description: 'Register equipment for maintenance.',
        }
      : item
        ? {
            title: 'Asset details',
            description: 'Review the selected asset record.',
          }
        : {
            title: 'Assets',
            description:
              'Find equipment by category, status, location, or asset code.',
          }
  }
  if (section === 'schedules') {
    return item === 'new'
      ? {
          title: 'Add schedule',
          description: 'Create a preventive maintenance schedule.',
        }
      : item
        ? {
            title: 'Schedule details',
            description: 'Review maintenance timing and assignment.',
          }
        : {
            title: 'Schedules',
            description:
              'Review PM cycles, due dates, and recorded schedule status.',
          }
  }
  if (section === 'inspections') {
    return item
      ? {
          title: 'Inspection details',
          description: 'Review the inspection source record.',
        }
      : {
          title: 'Inspections',
          description:
            'Review inspection outcomes, source notes, and corrective follow-up.',
        }
  }
  if (section === 'preventive-maintenance-forms') {
    return action === 'review'
      ? {
          title: 'Review before acknowledgement',
          description: 'Review the submitted form and its inspection rows.',
        }
      : item
        ? {
            title: 'Form details',
            description: 'Review the preventive maintenance form.',
          }
        : {
            title: 'Form review',
            description:
              'Review submitted forms and open their inspection source rows.',
          }
  }
  return { title: 'UniPM', description: 'Preventive maintenance portal' }
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
  const [headingHost, setHeadingHost] = useState<HTMLDivElement | null>(null)
  const mobileToggleRef = useRef<HTMLButtonElement>(null)
  const mobileCloseRef = useRef<HTMLButtonElement>(null)
  const navigationRef = useRef<HTMLElement>(null)
  const activeIndicatorRef = useRef<HTMLSpanElement>(null)
  const roles = currentUser.isSuccess ? currentUser.data.roles : []
  const requiredRoles = getRequiredAppRoles(pathname)
  const isRoleRestrictedRoute = requiredRoles !== null
  const hasPageAccess =
    requiredRoles === null ||
    (currentUser.isSuccess && hasAnyRole(roles, requiredRoles))
  const showSchedules = currentUser.isSuccess && canReadSchedules(roles)
  const showFormReview = currentUser.isSuccess && canReviewForms(roles)
  const fallbackHeading = fallbackPageHeading(pathname)
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

  useLayoutEffect(() => {
    const navigation = navigationRef.current
    const indicator = activeIndicatorRef.current
    if (!navigation || !indicator) return

    const updateIndicator = () => {
      const activeLink = navigation.querySelector<HTMLElement>(
        '[aria-current="page"]',
      )
      if (!activeLink) {
        indicator.style.opacity = '0'
        return
      }

      const navigationBounds = navigation.getBoundingClientRect()
      const linkBounds = activeLink.getBoundingClientRect()
      indicator.style.width = `${linkBounds.width}px`
      indicator.style.height = `${linkBounds.height}px`
      indicator.style.transform = `translate3d(${linkBounds.left - navigationBounds.left + navigation.scrollLeft}px, ${linkBounds.top - navigationBounds.top + navigation.scrollTop}px, 0)`
      indicator.style.opacity = '1'
    }

    updateIndicator()
    const resizeObserver =
      typeof ResizeObserver === 'undefined'
        ? null
        : new ResizeObserver(updateIndicator)
    resizeObserver?.observe(navigation)
    const activeLink = navigation.querySelector<HTMLElement>(
      '[aria-current="page"]',
    )
    if (activeLink) resizeObserver?.observe(activeLink)
    window.addEventListener('resize', updateIndicator)

    return () => {
      resizeObserver?.disconnect()
      window.removeEventListener('resize', updateIndicator)
    }
  }, [pathname, sidebarCollapsed, showSchedules, showFormReview])

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
          className={`flex shrink-0 items-center gap-2 border-b border-[var(--border-soft)] p-3 ${sidebarCollapsed ? 'lg:flex-col lg:items-center' : 'lg:justify-between'}`}
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
          ref={navigationRef}
          id="primary-navigation"
          aria-label="Primary"
          className="relative flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto p-2"
        >
          <span
            ref={activeIndicatorRef}
            aria-hidden="true"
            className="sidebar-active-indicator"
          />
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
                    'sidebar-nav-link relative z-10 flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm font-semibold text-white',
                }}
                inactiveProps={{
                  className:
                    'sidebar-nav-link relative z-10 flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-muted)]',
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
        <header className="app-page-header relative z-30 flex min-h-20 items-center gap-3 px-4 py-4 sm:px-6 lg:px-6">
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
          <div className="page-heading-region min-w-0 flex-1">
            <div className="page-heading-fallback">
              <h1 className="text-2xl font-bold tracking-tight text-[var(--text-primary)] sm:text-[1.75rem]">
                {fallbackHeading.title}
              </h1>
              <p className="mt-1 hidden text-sm text-[var(--text-neutral)] sm:block">
                {fallbackHeading.description}
              </p>
            </div>
            <div ref={setHeadingHost} className="page-heading-host" />
          </div>
        </header>
        <main className="app-shell-main px-4 pb-4 sm:px-6 lg:min-h-0 lg:flex-1 lg:overflow-hidden lg:px-6 lg:pb-6">
          <div className="app-workspace rounded-xl border border-[var(--border-soft)] bg-white p-4 sm:p-5 lg:h-full lg:min-h-0 lg:overflow-x-hidden lg:overflow-y-auto lg:p-6">
            <PageHeadingHostContext.Provider value={headingHost}>
              {isRoleRestrictedRoute && currentUser.isPending ? (
                <div
                  role="status"
                  className="text-sm text-[var(--text-neutral)]"
                >
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
            </PageHeadingHostContext.Provider>
          </div>
        </main>
      </div>
    </div>
  )
}
