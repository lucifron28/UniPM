import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { configureApiRuntime, resetApiRuntimeForTests } from '@/api/http-client'
import type {
  PmPeriodDashboardBatchResponse,
  PmPeriodDashboardResponse,
} from '@/api/generated/models'
import {
  DashboardHeader,
  PmPeriodDashboardPresentation,
  type PmPeriodDashboardSearch,
} from './pm-period-dashboard'
import {
  formatPmCycle,
  formatPmCycleDueDate,
} from '@/features/schedules/schedule-presentation'
import { PmPeriodDashboard } from './pm-period-dashboard'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

type PeriodState = 'Future' | 'Active' | 'Closed'

const batch: PmPeriodDashboardBatchResponse = {
  department: 'CCMS',
  assetCategory: 'fire-extinguisher',
  pmCycle: '2026-06',
  scheduled: 5,
  inspected: 3,
  completedOnTime: 2,
  onTimeCompliancePercent: 40,
  completedLate: 1,
  notCompleted: 1,
  remaining: 2,
  formId: null,
  formStatus: 'Submitted',
  fileNumber: 'PM-2026-001',
  fieldWorkCompletedAt: '2026-06-30T07:00:00Z',
  submittedAt: '2026-06-30T08:00:00Z',
  isAcknowledged: false,
  acknowledgedAt: null,
}

function dashboardFor(periodState: PeriodState): PmPeriodDashboardResponse {
  const isClosed = periodState === 'Closed'

  return {
    pmCycle: '2026-06',
    assetCategory: 'fire-extinguisher',
    department: null,
    deadline: '2026-06-30T16:00:00Z',
    periodState,
    complianceMeasurable: isClosed,
    inspectionResultsAvailable: true,
    scheduled: 5,
    inspected: 3,
    completedOnTime: 2,
    completedLate: 1,
    notCompleted: isClosed ? 1 : 0,
    remaining: isClosed ? 0 : 2,
    operational: 2,
    nonOperational: 1,
    onTimeCompliancePercent: isClosed ? 50 : null,
    progressPercent: 60,
    batches: [batch],
    assets: [],
  }
}

function renderState(periodState: PeriodState) {
  return render(
    <PmPeriodDashboardPresentation dashboard={dashboardFor(periodState)} />,
  )
}

function renderPresentationWithRouter(
  dashboard: PmPeriodDashboardResponse,
  search?: PmPeriodDashboardSearch,
) {
  const rootRoute = createRootRoute({
    component: () => (
      <PmPeriodDashboardPresentation
        dashboard={dashboard}
        {...(search ? { search } : {})}
      />
    ),
  })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })

  return render(<RouterProvider router={router} />)
}

const availableCycles = [
  {
    assetCategory: 'fire-extinguisher',
    year: 2025,
    cycles: [
      { pmCycle: '2025-02', scheduled: 4 },
      { pmCycle: '2025-11', scheduled: 8 },
    ],
  },
  {
    assetCategory: 'fire-extinguisher',
    year: 2026,
    cycles: [
      { pmCycle: '2026-06', scheduled: 7 },
      { pmCycle: '2026-12', scheduled: 12 },
    ],
  },
  {
    assetCategory: 'fire-alarm',
    year: 2026,
    cycles: [{ pmCycle: '2026-06', scheduled: 3 }],
  },
]

function renderInteractiveDashboard(
  initialSearch: PmPeriodDashboardSearch = {},
) {
  const user = userEvent.setup()
  const dashboardRequests: Array<{
    assetCategory: string | null
    pmCycle: string | null
  }> = []

  useAuthStore
    .getState()
    .establishSession('synthetic-gsd-period-dashboard-token')
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
  server.use(
    http.get('*/api/v1/auth/me', () =>
      HttpResponse.json({
        id: '11111111-1111-4111-8111-111111111111',
        email: 'gsd@example.test',
        displayName: 'GSD Personnel',
        roles: ['GSD'],
      }),
    ),
    http.get('*/api/v1/pm-period-dashboard/cycles', () =>
      HttpResponse.json(availableCycles),
    ),
    http.get('*/api/v1/schedules/enrollment-deferrals', () =>
      HttpResponse.json({
        page: 1,
        pageSize: 1,
        total: 0,
        pendingCount: '0',
        reviewedCount: '0',
        items: [],
      }),
    ),
    http.get('*/api/v1/pm-period-dashboard', ({ request }) => {
      const url = new URL(request.url)
      const assetCategory = url.searchParams.get('assetCategory')
      const pmCycle = url.searchParams.get('pmCycle')
      dashboardRequests.push({ assetCategory, pmCycle })

      return HttpResponse.json({
        ...dashboardFor('Closed'),
        ...(assetCategory ? { assetCategory } : {}),
        ...(pmCycle ? { pmCycle } : {}),
      })
    }),
  )

  function DashboardHarness() {
    const [search, setSearch] = useState(initialSearch)
    return <PmPeriodDashboard search={search} onSearchChange={setSearch} />
  }

  const rootRoute = createRootRoute({ component: DashboardHarness })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return { user, dashboardRequests }
}

function expectSearchParams(
  searchParams: URLSearchParams,
  expected: Record<string, string>,
) {
  for (const [key, value] of Object.entries(expected)) {
    expect(searchParams.get(key)).toBe(value)
  }
}

function expectMetricLabel(label: string) {
  expect(
    screen
      .getAllByText(label, { exact: true })
      .some((element) => element.tagName === 'P'),
  ).toBe(true)
}

function expectMetricValue(label: string, value: string) {
  const labelElement = screen
    .getAllByText(label, { exact: true })
    .find((element) => element.tagName === 'P')

  if (!labelElement?.parentElement) {
    throw new Error(`Metric card not found: ${label}`)
  }

  expect(
    within(labelElement.parentElement).getByText(value, { exact: true }),
  ).toBeInTheDocument()
}

describe('PM period dashboard period terminology', () => {
  afterEach(() => {
    useAuthStore.getState().clearSession()
    resetApiRuntimeForTests()
  })

  it('uses Remaining and not Not completed for Future periods', () => {
    renderState('Future')

    expectMetricLabel('Scheduled')
    expectMetricLabel('Remaining')
    expectMetricValue('Scheduled', '5')
    expectMetricValue('Remaining', '2')
    expectMetricLabel('Compliance rate')
    expectMetricValue('Compliance rate', 'Not measurable yet')
    expect(
      screen.getByText('Not measurable yet', { exact: true }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Not completed', { exact: true }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('columnheader', { name: 'Remaining' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('columnheader', { name: 'Not completed' }),
    ).not.toBeInTheDocument()
  })

  it('uses Remaining and not Not completed for Active periods', () => {
    renderState('Active')

    expectMetricLabel('Progress')
    expectMetricLabel('Compliance rate')
    expectMetricLabel('Remaining')
    expectMetricValue('Progress', '60%')
    expectMetricValue('Compliance rate', 'Not measurable yet')
    expectMetricValue('Remaining', '2')
    expect(
      screen.getByText('Not measurable yet', { exact: true }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Not completed', { exact: true }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('columnheader', { name: 'Remaining' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('columnheader', { name: 'Not completed' }),
    ).not.toBeInTheDocument()
  })

  it('uses final completion terminology for Closed periods', () => {
    renderState('Closed')

    expectMetricLabel('Completed on time')
    expectMetricLabel('Completed late')
    expectMetricLabel('Not completed')
    expectMetricValue('Scheduled', '5')
    expectMetricValue('Inspected', '3')
    expectMetricValue('Completed on time', '2')
    expectMetricValue('Completed late', '1')
    expectMetricValue('Not completed', '1')
    expectMetricValue('Remaining', '0')
    expectMetricValue('Operational', '2')
    expectMetricValue('Non-operational', '1')
    expectMetricValue('Compliance rate', '50%')
    expect(screen.getByText('50%', { exact: true })).toBeInTheDocument()
    expect(
      screen.getByRole('columnheader', { name: 'Not completed' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('columnheader', { name: 'Remaining' }),
    ).not.toBeInTheDocument()
  })

  it('formats a cycle-derived leap-February month and deadline', () => {
    expect(formatPmCycle('2028-02')).toBe('February 2028')
    expect(formatPmCycleDueDate('2028-02')).toBe('Feb 29, 2028')
  })

  it('links GSD to the report builder from the dashboard heading', async () => {
    const rootRoute = createRootRoute({
      component: () => <DashboardHeader canAskNla />,
    })
    const router = createRouter({
      routeTree: rootRoute,
      history: createMemoryHistory({ initialEntries: ['/'] }),
    })
    render(<RouterProvider router={router} />)

    expect(
      await screen.findByRole('heading', {
        name: 'Preventive Maintenance Dashboard',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Ask a question' }),
    ).toHaveAttribute('href', '/app/analytics')
  })

  it('shows deferred enrollment counts separately from PM period metrics', async () => {
    useAuthStore.getState().establishSession('synthetic-gsd-dashboard-token')
    configureApiRuntime({
      getAccessToken: () => useAuthStore.getState().accessToken,
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    server.use(
      http.get('*/api/v1/auth/me', () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Personnel',
          roles: ['GSD'],
        }),
      ),
      http.get('*/api/v1/pm-period-dashboard/cycles', () =>
        HttpResponse.json([]),
      ),
      http.get('*/api/v1/schedules/enrollment-deferrals', () =>
        HttpResponse.json({
          page: 1,
          pageSize: 1,
          total: 3,
          pendingCount: '2',
          reviewedCount: '1',
          items: [],
        }),
      ),
    )

    const rootRoute = createRootRoute({
      component: () => (
        <PmPeriodDashboard search={{}} onSearchChange={vi.fn()} />
      ),
    })
    const router = createRouter({
      routeTree: rootRoute,
      history: createMemoryHistory({ initialEntries: ['/'] }),
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })

    render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    )

    expect(
      await screen.findByRole('heading', {
        name: /Deferred enrollment: 2 need review · 1 reviewed/,
      }),
    ).toBeInTheDocument()
    expect(screen.getByText(/2 need review · 1 reviewed/)).toBeInTheDocument()
    expect(screen.getByText(/not completed maintenance/)).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Review deferred cycles' }),
    ).toHaveAttribute(
      'href',
      expect.stringContaining('#schedule-enrollment-deferrals'),
    )
    expect(screen.getByText('No PM periods are scheduled')).toBeInTheDocument()
    expect(screen.queryByText('Compliance rate')).not.toBeInTheDocument()
  })

  it('routes acknowledged batches to read-only form detail', async () => {
    const formId = '22222222-2222-4222-8222-222222222222'
    const acknowledgedBatch: PmPeriodDashboardBatchResponse = {
      ...batch,
      formId,
      formStatus: 'Acknowledged',
      isAcknowledged: true,
      acknowledgedAt: '2026-07-01T08:00:00Z',
    }

    renderPresentationWithRouter({
      ...dashboardFor('Closed'),
      batches: [acknowledgedBatch],
    })

    const link = await screen.findByRole('link', { name: 'View batch' })
    expect(link).toHaveAttribute(
      'href',
      expect.stringContaining(`/app/preventive-maintenance-forms/${formId}?`),
    )
    expect(link).toHaveAttribute(
      'href',
      expect.stringContaining('readonly=true'),
    )
    expect(link).not.toHaveAttribute('href', expect.stringContaining('/review'))
  })

  it('preserves dashboard scope and filters when opening a submitted batch review', async () => {
    const reviewFormId = '22222222-2222-4222-8222-222222222222'
    const reviewSearch: PmPeriodDashboardSearch = {
      assetCategory: 'fire-extinguisher',
      year: 2026,
      pmCycle: '2026-06',
      department: 'GSD',
      condition: 'NonOperational',
      timeliness: 'Late',
      search: 'FE-TEST-001',
    }

    renderPresentationWithRouter(
      {
        ...dashboardFor('Closed'),
        batches: [{ ...batch, formId: reviewFormId }],
      },
      reviewSearch,
    )

    const link = await screen.findByRole('link', { name: 'Review batch' })
    const reviewUrl = new URL(link.getAttribute('href')!, 'http://localhost')
    expect(reviewUrl.pathname).toBe(
      `/app/preventive-maintenance-forms/${reviewFormId}/review`,
    )
    expectSearchParams(reviewUrl.searchParams, {
      assetCategory: 'fire-extinguisher',
      year: '2026',
      pmCycle: '2026-06',
      department: 'GSD',
      condition: 'NonOperational',
      timeliness: 'Late',
      search: 'FE-TEST-001',
    })
  })
})

describe('PM period dashboard period selection', () => {
  afterEach(() => {
    useAuthStore.getState().clearSession()
    resetApiRuntimeForTests()
  })

  it('limits dependent period options and waits for explicit generation', async () => {
    const { user, dashboardRequests } = renderInteractiveDashboard()
    const category = await screen.findByRole('combobox', {
      name: 'Asset category',
    })

    await user.click(category)
    await user.type(category, 'ext')
    await user.click(
      await screen.findByRole('option', { name: 'Fire extinguishers' }),
    )

    const year = screen.getByRole('combobox', { name: 'Year' })
    expect(
      within(year)
        .getAllByRole('option')
        .map((option) => option.textContent?.trim()),
    ).toEqual(['Choose a year', '2026', '2025'])

    await user.selectOptions(year, '2026')
    const month = screen.getByRole('combobox', { name: 'Scheduled month' })
    expect(
      within(month)
        .getAllByRole('option')
        .map((option) => option.textContent?.trim()),
    ).toEqual([
      'Choose a scheduled month',
      'June — 7 scheduled',
      'December — 12 scheduled',
    ])
    expect(
      screen.getByRole('button', { name: 'Generate dashboard' }),
    ).toBeDisabled()
    expect(dashboardRequests).toHaveLength(0)

    await user.selectOptions(year, '2025')
    expect(
      within(month)
        .getAllByRole('option')
        .map((option) => option.textContent?.trim()),
    ).toEqual([
      'Choose a scheduled month',
      'February — 4 scheduled',
      'November — 8 scheduled',
    ])

    await user.click(category)
    await user.type(category, 'alarm')
    await user.click(
      await screen.findByRole('option', { name: 'Fire alarm systems' }),
    )
    expect(year).toHaveValue('')
    expect(month).toBeDisabled()
    expect(
      screen.getByRole('button', { name: 'Generate dashboard' }),
    ).toBeDisabled()

    await user.selectOptions(year, '2026')
    await user.selectOptions(month, '2026-06')
    await user.click(screen.getByRole('button', { name: 'Generate dashboard' }))

    expect(
      await screen.findByRole('heading', {
        name: 'Fire alarm systems · June 2026',
      }),
    ).toBeInTheDocument()
    expect(dashboardRequests).toContainEqual({
      assetCategory: 'fire-alarm',
      pmCycle: '2026-06',
    })
  })

  it('preserves a generated report when period changes are cancelled', async () => {
    const { user, dashboardRequests } = renderInteractiveDashboard({
      assetCategory: 'fire-extinguisher',
      year: 2026,
      pmCycle: '2026-06',
    })

    expect(
      await screen.findByRole('heading', {
        name: 'Fire extinguishers · June 2026',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('7 scheduled assets')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Change period' }))
    const month = screen.getByRole('combobox', { name: 'Scheduled month' })
    await user.selectOptions(month, '2026-12')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(
      screen.getByRole('heading', {
        name: 'Fire extinguishers · June 2026',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Period overview' }),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Change period' }))
    const changedMonth = screen.getByRole('combobox', {
      name: 'Scheduled month',
    })
    await user.selectOptions(changedMonth, '2026-12')
    expect(changedMonth).toHaveValue('2026-12')
    await user.click(screen.getByRole('button', { name: 'Generate dashboard' }))

    expect(
      await screen.findByRole('heading', {
        name: 'Fire extinguishers · December 2026',
      }),
    ).toBeInTheDocument()
    expect(dashboardRequests).toContainEqual({
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-12',
    })
  })

  it('explains an unavailable saved period and prevents accidental generation', async () => {
    const { dashboardRequests } = renderInteractiveDashboard({
      assetCategory: 'retired-category',
      year: 2024,
      pmCycle: '2024-06',
    })

    expect(
      await screen.findByText(
        'This saved report period is no longer available. Choose a scheduled period and generate a new report.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Year' })).toBeDisabled()
    expect(
      screen.getByRole('combobox', { name: 'Scheduled month' }),
    ).toBeDisabled()
    expect(
      screen.getByRole('button', { name: 'Generate dashboard' }),
    ).toBeDisabled()
    expect(dashboardRequests).toHaveLength(0)
  })
})
