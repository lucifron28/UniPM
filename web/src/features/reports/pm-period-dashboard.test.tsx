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
import { afterEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { configureApiRuntime, resetApiRuntimeForTests } from '@/api/http-client'
import type {
  PmPeriodDashboardAssetRowResponse,
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

function dashboardAsset(
  overrides: Partial<PmPeriodDashboardAssetRowResponse> = {},
): PmPeriodDashboardAssetRowResponse {
  return {
    scheduleId: '33333333-3333-4333-8333-333333333301',
    assetId: '33333333-3333-4333-8333-333333333302',
    inspectionId: '33333333-3333-4333-8333-333333333303',
    assetCode: 'FE-PENDING',
    assetCategory: 'fire-extinguisher',
    building: 'CCMS Building',
    location: 'CL-01',
    department: 'CCMS',
    pmCycle: '2026-06',
    scheduleDate: '2026-06-01T04:00:00Z',
    deadline: '2026-06-30T16:00:00Z',
    scheduleStatus: 'Completed',
    executionStatus: 'Completed',
    isInspected: true,
    inspectionCompletedAt: '2026-06-20T04:00:00Z',
    timeliness: 'OnTime',
    condition: 'NonOperational',
    remarks: 'Pressure is below the operating range.',
    actionsRecommendations: 'Arrange a pressure check.',
    formId: '33333333-3333-4333-8333-333333333304',
    formStatus: 'Acknowledged',
    isAcknowledged: true,
    acknowledgedAt: '2026-07-02T04:00:00Z',
    correctiveFollowUpStatus: 'CorrectiveFollowUpPending',
    externalPmNumber: null,
    ...overrides,
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
  deferralSummary = { total: 0, pendingCount: '0', reviewedCount: '0' },
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
        total: deferralSummary.total,
        pendingCount: deferralSummary.pendingCount,
        reviewedCount: deferralSummary.reviewedCount,
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

function expectMetricValue(label: string, value: string) {
  const labelElement = screen
    .getAllByText(label, { exact: true })
    .find((element) => ['P', 'DT'].includes(element.tagName))

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

  it('shows concise upcoming metrics and breakdown for Future periods', () => {
    renderState('Future')

    expectMetricValue('Inspected / scheduled', '3 / 5')
    expectMetricValue('Progress', '60%')
    expectMetricValue('On-time compliance', 'Not measurable yet')
    expectMetricValue('Non-operational', '1')
    expect(
      screen.getByRole('heading', { name: 'Inspection breakdown' }),
    ).toBeInTheDocument()
    expectMetricValue('Scheduled', '2')
    expect(
      screen.queryByText('Not completed', { exact: true }),
    ).not.toBeInTheDocument()
  })

  it('shows pending inspections for Active periods', () => {
    renderState('Active')

    expectMetricValue('Inspected / scheduled', '3 / 5')
    expectMetricValue('Progress', '60%')
    expectMetricValue('On-time compliance', 'Not measurable yet')
    expectMetricValue('Pending', '2')
    expect(
      screen.queryByText('Not completed', { exact: true }),
    ).not.toBeInTheDocument()
  })

  it('uses final completion terminology for Closed periods', () => {
    renderState('Closed')

    expectMetricValue('Inspected / scheduled', '3 / 5')
    expectMetricValue('Progress', '60%')
    expectMetricValue('On-time compliance', '50%')
    expectMetricValue('Non-operational', '1')
    expectMetricValue('Completed on time', '2')
    expectMetricValue('Completed late', '1')
    expectMetricValue('Not completed', '1')
    expect(
      screen.queryByText('Remaining', { exact: true }),
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
    const analyticsUrl = new URL(
      screen
        .getByRole('link', { name: 'Ask a question' })
        .getAttribute('href')!,
      'http://localhost',
    )
    expect(analyticsUrl.pathname).toBe('/app/analytics')
    expect(JSON.parse(analyticsUrl.searchParams.get('returnContext')!)).toEqual(
      { kind: 'dashboard', search: {} },
    )
  })

  it('keeps deferred enrollment review outside the PM dashboard', async () => {
    renderInteractiveDashboard(
      {},
      { total: 3, pendingCount: '2', reviewedCount: '1' },
    )

    expect(
      await screen.findByRole('combobox', { name: 'Asset category' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Deferred enrollment/i)).not.toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Review deferred cycles' }),
    ).not.toBeInTheDocument()
  })

  it('lists corrective follow-up and links to its inspection and form', async () => {
    const inspectionId = '33333333-3333-4333-8333-333333333311'
    const formId = '33333333-3333-4333-8333-333333333312'
    const pendingInspectionId = '33333333-3333-4333-8333-333333333310'
    const referred = dashboardAsset({
      inspectionId,
      assetCode: 'FE-REFERRED',
      formId,
      correctiveFollowUpStatus: 'ReferredToWms',
      externalPmNumber: 'WMS-PM-2026-014',
    })
    const pending = dashboardAsset({
      inspectionId: pendingInspectionId,
      formId: null,
      assetCode: 'FE-PENDING',
    })
    const operational = dashboardAsset({
      inspectionId: '33333333-3333-4333-8333-333333333313',
      assetCode: 'FE-OPERATIONAL',
      condition: 'Operational',
      correctiveFollowUpStatus: null,
      externalPmNumber: null,
    })
    const unacknowledged = dashboardAsset({
      inspectionId: '33333333-3333-4333-8333-333333333314',
      assetCode: 'FE-UNACKNOWLEDGED',
      isAcknowledged: false,
      correctiveFollowUpStatus: null,
    })

    renderPresentationWithRouter(
      {
        ...dashboardFor('Closed'),
        assets: [pending, referred, operational, unacknowledged],
      },
      {
        assetCategory: 'fire-extinguisher',
        year: 2026,
        pmCycle: '2026-06',
        department: 'CCMS',
      },
    )

    expect(await screen.findByText('FE-PENDING')).toBeInTheDocument()
    expect(screen.getByText('FE-REFERRED')).toBeInTheDocument()
    expect(screen.getByText('WMS-PM-2026-014')).toBeInTheDocument()
    expect(screen.queryByText('FE-OPERATIONAL')).not.toBeInTheDocument()
    expect(screen.queryByText('FE-UNACKNOWLEDGED')).not.toBeInTheDocument()
    const inspectionLinks = screen.getAllByRole('link', {
      name: 'View inspection',
    })
    expect(
      inspectionLinks.some((link) =>
        link
          .getAttribute('href')
          ?.includes(`/app/inspections/${inspectionId}?`),
      ),
    ).toBe(true)
    const formLink = screen.getByRole('link', { name: 'Open PM form' })
    expect(formLink).toHaveAttribute(
      'href',
      expect.stringContaining(`/app/preventive-maintenance-forms/${formId}?`),
    )
    expect(formLink).toHaveAttribute(
      'href',
      expect.stringContaining('readonly=true'),
    )
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
    ).toEqual(['Choose a scheduled month', 'June', 'December'])
    expect(
      screen.getByRole('button', { name: 'Generate dashboard' }),
    ).toBeDisabled()
    expect(dashboardRequests).toHaveLength(0)

    await user.selectOptions(year, '2025')
    expect(
      within(month)
        .getAllByRole('option')
        .map((option) => option.textContent?.trim()),
    ).toEqual(['Choose a scheduled month', 'February', 'November'])

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
    expect(
      await screen.findByText('3 / 5', { exact: true }),
    ).toBeInTheDocument()

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
      screen.getByRole('heading', { name: 'Inspection breakdown' }),
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
