import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { AppShell } from '@/components/layout/app-shell'
import { routeTree } from '@/routeTree.gen'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const meUrl = '*/api/v1/auth/me'

function currentUser(roles: readonly string[]) {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    email: 'admin@example.test',
    displayName: 'Synthetic Admin',
    roles: [...roles],
  }
}

function setupAuth() {
  useAuthStore.getState().establishSession('synthetic-test-token')
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
}

function renderShell(initialEntry = '/') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const rootRoute = createRootRoute({ component: () => <AppShell /> })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: [initialEntry] }),
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

function renderAppRoute(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const router = createRouter({
    routeTree,
    context: {
      queryClient,
      getAccessToken: () => useAuthStore.getState().accessToken,
    },
    history: createMemoryHistory({ initialEntries: [initialEntry] }),
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('app shell role access and layout', () => {
  beforeEach(() => {
    window.localStorage.removeItem('unipm.sidebar.collapsed')
    setupAuth()
  })

  it('starts expanded by default and hides operational navigation from Admin', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['Admin']))))

    renderShell()

    await screen.findByLabelText('Account menu')
    const sidebar = screen.getByRole('complementary')
    const nav = within(sidebar).getByRole('navigation', { name: 'Primary' })
    expect(
      sidebar.querySelector('.sidebar-active-indicator'),
    ).toBeInTheDocument()
    expect(
      within(sidebar).getByRole('button', { name: 'Collapse navigation' }),
    ).toHaveAttribute('aria-expanded', 'true')
    expect(
      within(nav).getByRole('link', { name: 'Dashboard' }),
    ).not.toHaveAttribute('title')
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).toHaveClass(
      'sidebar-nav-link',
    )
    expect(
      within(nav)
        .getByRole('link', { name: 'Dashboard' })
        .querySelector('span'),
    ).toHaveClass('sidebar-nav-label')
    expect(within(nav).getByRole('link', { name: 'Assets' })).toBeVisible()
    expect(within(nav).getByRole('link', { name: 'Inspections' })).toBeVisible()
    expect(within(nav).queryByRole('link', { name: 'Schedules' })).toBeNull()
    expect(within(nav).queryByRole('link', { name: 'Form review' })).toBeNull()
  })

  it('preserves a saved collapsed preference and keeps account actions at the sidebar bottom', async () => {
    window.localStorage.setItem('unipm.sidebar.collapsed', 'true')
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))))

    renderShell()

    await screen.findByLabelText('Account menu')
    const sidebar = screen.getByRole('complementary')
    const shell = sidebar.parentElement!
    const header = document.querySelector('.app-page-header')!
    const workspace = document.querySelector('.app-workspace')!
    const account = screen.getByLabelText('Account menu')
    expect(shell).toHaveClass('app-shell')
    expect(header.contains(workspace)).toBe(false)
    expect(header.contains(account)).toBe(false)
    expect(account.closest('details')?.parentElement).toBe(
      sidebar.lastElementChild,
    )
    expect(account).toHaveAttribute('title', 'Synthetic Admin · GSD')
    expect(workspace.closest('main')).toBeTruthy()
    expect(sidebar.querySelector('nav')).toHaveClass('overflow-y-auto')

    const expand = within(sidebar).getByRole('button', {
      name: 'Expand navigation',
    })
    expect(expand).toHaveAttribute('aria-expanded', 'false')
    fireEvent.click(expand)
    expect(
      within(sidebar).getByRole('button', { name: 'Collapse navigation' }),
    ).toHaveAttribute('aria-expanded', 'true')
    expect(window.localStorage.getItem('unipm.sidebar.collapsed')).toBe('false')
    expect(
      within(sidebar).getAllByText('Institutional operations'),
    ).toHaveLength(2)

    fireEvent.click(account)
    expect(account.closest('details')).toHaveAttribute('open')
    expect(screen.getByText('admin@example.test')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible()
    expect(
      within(sidebar).getByRole('button', { name: 'Sign out' }),
    ).toBeVisible()
    expect(account.closest('details')?.lastElementChild).toHaveClass(
      'bottom-full',
    )
  })

  it('opens responsive navigation and closes it with Escape', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))))
    renderShell()
    await screen.findByLabelText('Account menu')

    const open = screen.getByRole('button', { name: 'Open navigation' })
    const sidebar = screen.getByRole('complementary')
    expect(open).toHaveAttribute('aria-expanded', 'false')
    fireEvent.click(open)
    expect(open).toHaveAttribute('aria-expanded', 'true')
    expect(sidebar.classList.contains('flex')).toBe(true)
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(open).toHaveAttribute('aria-expanded', 'false')
  })

  it.each([
    { path: '/app/schedules', roles: ['Admin'] },
    {
      path: '/app/schedules/11111111-1111-4111-8111-111111111111',
      roles: ['DepartmentHead'],
    },
    { path: '/app/schedules/new', roles: ['Inspector'] },
    { path: '/app/assets/new', roles: ['Admin'] },
    { path: '/app/preventive-maintenance-forms', roles: ['Admin'] },
    { path: '/app/analytics', roles: ['Admin'] },
    { path: '/app/analytics', roles: ['Inspector'] },
    { path: '/app/analytics', roles: ['Supervisor'] },
    { path: '/app/analytics', roles: ['DepartmentHead'] },
  ] as const)(
    'denies route access before loading page data at $path',
    async ({ path, roles }) => {
      let operationalRequests = 0
      server.use(
        http.get('*/api/v1/*', ({ request }) => {
          const pathname = new URL(request.url).pathname
          if (pathname === '/api/v1/auth/me') {
            return HttpResponse.json(currentUser(roles))
          }
          operationalRequests += 1
          return HttpResponse.json([])
        }),
      )

      renderAppRoute(path)

      expect(
        await screen.findByRole('heading', { name: 'Access denied' }),
      ).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /Retry/ })).toBeNull()
      expect(operationalRequests).toBe(0)
    },
  )

  it('opens Ask about PM results from the dashboard without running a query on page load', async () => {
    let operationalRequests = 0
    let analyticsRequests = 0
    server.use(
      http.get('*/api/v1/*', ({ request }) => {
        const pathname = new URL(request.url).pathname
        if (pathname === '/api/v1/auth/me') {
          return HttpResponse.json(currentUser(['GSD']))
        }
        operationalRequests += 1
        return HttpResponse.json([])
      }),
      http.post('*/api/v1/analytics/pm/v2/*', () => {
        analyticsRequests += 1
        return HttpResponse.json({ status: 'Unsupported' })
      }),
    )

    renderAppRoute('/app/analytics')

    expect(
      await screen.findByRole('heading', { name: 'Ask about PM results' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Ask a question' }),
    ).toHaveAttribute('aria-pressed', 'true')
    expect(screen.queryByRole('link', { name: 'PM Analytics' })).toBeNull()
    expect(screen.getByRole('link', { name: 'Dashboard' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(operationalRequests).toBe(0)
    expect(analyticsRequests).toBe(0)
  })

  it('does not deny a role-restricted route while roles are loading', async () => {
    let releaseCurrentUser!: () => void
    const userResponse = new Promise<void>((resolve) => {
      releaseCurrentUser = resolve
    })
    server.use(
      http.get(meUrl, async () => {
        await userResponse
        return HttpResponse.json(currentUser(['Admin']))
      }),
    )

    renderShell('/app/schedules')

    expect(await screen.findByText('Checking page access…')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
    releaseCurrentUser()
    expect(
      await screen.findByRole('heading', { name: 'Access denied' }),
    ).toBeInTheDocument()
  })

  it('reports current-user errors separately from role denial', async () => {
    server.use(http.get(meUrl, () => new HttpResponse(null, { status: 500 })))

    renderShell('/app/schedules')

    expect(
      await screen.findByRole('heading', { name: 'Unable to verify access' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Retry access check' }),
    ).toBeInTheDocument()
  })

  it('allows an assigned operational role alongside Admin', async () => {
    let assignmentBatchReads = 0
    server.use(
      http.get('*/api/v1/*', ({ request }) => {
        const pathname = new URL(request.url).pathname
        if (pathname === '/api/v1/auth/me') {
          return HttpResponse.json(currentUser(['Admin', 'GSD']))
        }
        if (pathname === '/api/v1/schedules/assignment-batches') {
          assignmentBatchReads += 1
          return HttpResponse.json({ totalScheduleCount: 0, batches: [] })
        }
        if (pathname === '/api/v1/schedules') {
          return HttpResponse.json([])
        }
        if (pathname === '/api/v1/schedules/registry/options') {
          return HttpResponse.json({
            categories: [],
            quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
            departments: [],
            years: [],
          })
        }
        if (pathname === '/api/v1/schedules/registry/summary') {
          return HttpResponse.json({
            totalCount: 0,
            dueCount: 0,
            ongoingCount: 0,
            overdueCount: 0,
            completedCount: 0,
            cancelledCount: 0,
          })
        }
        if (pathname === '/api/v1/schedules/coverage-review') {
          return HttpResponse.json({
            items: [],
            page: 1,
            pageSize: 10,
            total: 0,
          })
        }
        if (pathname === '/api/v1/schedules/enrollment-deferrals') {
          return HttpResponse.json({
            items: [],
            page: 1,
            pageSize: 10,
            total: 0,
            pendingCount: 0,
            reviewedCount: 0,
          })
        }
        return HttpResponse.json([])
      }),
    )

    renderAppRoute('/app/schedules')

    await waitFor(() => expect(assignmentBatchReads).toBeGreaterThan(0))
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
    const scheduleHeading = await screen.findByRole('heading', {
      name: 'PM schedule batches',
    })
    expect(
      document.querySelector('.app-workspace')?.contains(scheduleHeading),
    ).toBe(true)
  })

  it('keeps batch assignment scoped to loaded filters and counts only visible schedules', async () => {
    const currentYear = Number(
      new Intl.DateTimeFormat('en-US', {
        timeZone: 'Asia/Manila',
        year: 'numeric',
      }).format(new Date()),
    )
    const previousYear = currentYear - 1
    const pmCycle = `${currentYear}-08`
    const assignedSupervisorId = '22222222-2222-4222-8222-222222222222'
    const makeBatch = (
      department: string,
      scheduleCount: number,
      supervisorUserId: string | null,
    ) => {
      const departmentCode = department === 'CCMS' ? '33333333' : '44444444'
      const assetId = `${departmentCode}-3333-4333-8333-333333333333`
      const scheduleId = `${departmentCode}-4444-4444-8444-444444444444`
      return {
        batchKey: `${pmCycle}|${department}|fire-extinguisher`,
        department,
        assetCategory: 'fire-extinguisher',
        pmCycle,
        scheduleCount,
        completedCount: 0,
        ongoingCount: 0,
        overdueCount: 0,
        supervisorState: supervisorUserId ? 'Assigned' : 'Unassigned',
        supervisorUserId,
        supervisorDisplayName: supervisorUserId ? 'Synthetic Supervisor' : null,
        inspectorState: 'Unassigned',
        inspectorUserId: null,
        inspectorDisplayName: null,
        assignmentLocked: false,
        supervisorOwnershipIsConsistent: true,
        representativeSchedule: {
          id: scheduleId,
          assetId,
          scheduleDate: `${currentYear}-08-31T23:59:59+08:00`,
          pmCycle,
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q3',
          semester: null,
          year: currentYear,
          academicYear: null,
          assignedToUserId: null,
          assignedSupervisorUserId: supervisorUserId,
          completedAt: null,
          createdAt: `${currentYear}-08-01T08:00:00+08:00`,
          updatedAt: `${currentYear}-08-01T08:00:00+08:00`,
          asset: {
            id: assetId,
            assetCode: `${department}-FE-001`,
            assetCategory: 'fire-extinguisher',
            building: `${department} Building`,
            department,
            location: 'Synthetic common area',
          },
        },
      }
    }
    const currentBatches = [
      makeBatch('CCMS', 2, null),
      makeBatch('Library', 3, assignedSupervisorId),
    ]
    let releasePreviousYearResponse!: () => void
    const previousYearResponseGate = new Promise<void>((resolve) => {
      releasePreviousYearResponse = resolve
    })
    let signalPreviousYearRequest!: () => void
    const previousYearRequestStarted = new Promise<void>((resolve) => {
      signalPreviousYearRequest = resolve
    })

    server.use(
      http.get('*/api/v1/*', async ({ request }) => {
        const url = new URL(request.url)
        const pathname = url.pathname
        if (pathname === '/api/v1/auth/me') {
          return HttpResponse.json(currentUser(['GSD']))
        }
        if (pathname === '/api/v1/schedules/assignment-batches') {
          if (url.searchParams.get('year') === String(previousYear)) {
            signalPreviousYearRequest()
            await previousYearResponseGate
            return HttpResponse.json({ totalScheduleCount: 0, batches: [] })
          }
          return HttpResponse.json({
            totalScheduleCount: 5,
            batches: currentBatches,
          })
        }
        if (pathname === '/api/v1/schedules/registry/options') {
          return HttpResponse.json({
            categories: [
              {
                code: 'fire-extinguisher',
                displayName: 'Fire extinguishers',
                scheduledMonths: [2, 5, 8, 11],
              },
            ],
            quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
            departments: ['CCMS', 'Library'],
            years: [String(previousYear), String(currentYear)],
          })
        }
        if (pathname === '/api/v1/schedules/supervisor-assignment-options') {
          return HttpResponse.json({
            supervisors: [
              { id: assignedSupervisorId, displayName: 'Synthetic Supervisor' },
            ],
          })
        }
        if (pathname === '/api/v1/schedules/registry/summary') {
          return HttpResponse.json({
            totalCount: 0,
            dueCount: 0,
            ongoingCount: 0,
            overdueCount: 0,
            completedCount: 0,
            cancelledCount: 0,
          })
        }
        return HttpResponse.json([])
      }),
    )

    renderAppRoute('/app/schedules')

    expect(
      await screen.findByText(/2 batches · 5 schedules in view/),
    ).toBeVisible()
    fireEvent.click(
      screen.getByRole('checkbox', {
        name: 'Only show batches needing a Supervisor',
      }),
    )
    expect(
      await screen.findByText(/1 batch · 2 schedules in view/),
    ).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Assign Supervisor' }))
    expect(
      await screen.findByRole('heading', { name: 'Supervisor assignment' }),
    ).toBeVisible()

    fireEvent.change(
      screen.getByRole('combobox', { name: 'Assignment year' }),
      {
        target: { value: String(previousYear) },
      },
    )
    await previousYearRequestStarted

    expect(
      await screen.findByText(/Filters changed\. Previous batch results/),
    ).toBeVisible()
    expect(
      screen.getByRole('button', { name: 'Assign Supervisor' }),
    ).toBeDisabled()
    expect(
      screen.queryByRole('heading', { name: 'Supervisor assignment' }),
    ).not.toBeInTheDocument()

    releasePreviousYearResponse()
    expect(
      await screen.findByRole('heading', { name: 'No matching batches' }),
    ).toBeVisible()
  })
})
