import { render, screen, within } from '@testing-library/react'
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
        name: 'Deferred enrollment across all PM periods',
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
