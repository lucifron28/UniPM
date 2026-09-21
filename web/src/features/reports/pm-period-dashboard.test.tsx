import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type {
  PmPeriodDashboardBatchResponse,
  PmPeriodDashboardResponse,
} from '@/api/generated/models'
import { PmPeriodDashboardPresentation } from './pm-period-dashboard'

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

function expectMetricLabel(label: string) {
  expect(
    screen
      .getAllByText(label, { exact: true })
      .some((element) => element.tagName === 'P'),
  ).toBe(true)
}

describe('PM period dashboard period terminology', () => {
  it('uses Remaining and not Not completed for Future periods', () => {
    renderState('Future')

    expectMetricLabel('Scheduled')
    expectMetricLabel('Remaining')
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
    expectMetricLabel('Remaining')
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
    expect(screen.getByText('50%', { exact: true })).toBeInTheDocument()
    expect(
      screen.getByRole('columnheader', { name: 'Not completed' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('columnheader', { name: 'Remaining' }),
    ).not.toBeInTheDocument()
  })
})
