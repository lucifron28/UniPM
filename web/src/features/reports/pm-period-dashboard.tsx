import { useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate } from '@tanstack/react-router'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { assetCategorySchema } from '@/features/assets/asset-contract'
import { useScheduleAssignmentBatches } from '@/features/schedules/schedule-queries'
import { AlertTriangle } from 'lucide-react'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import { Skeleton } from '@/components/ui/skeleton'
import type {
  PmPeriodDashboardAssetRowResponse,
  PmPeriodDashboardCycleGroupResponse,
  PmPeriodDashboardResponse,
} from '@/api/generated/models'
import {
  usePmPeriodDashboard,
  usePmPeriodDashboardCycles,
} from '@/features/reports/pm-period-dashboard-queries'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  formatPmCycle,
  formatPmCycleDueDate,
} from '@/features/schedules/schedule-presentation'
import { ReportChart, type ReportChartRow } from './report-charts'
import './pm-dashboard-print.css'
import type { PmPeriodDashboardRecordsSearch } from './pm-period-dashboard-records'

export type PmPeriodDashboardSearch = {
  assetCategory?: string | undefined
  year?: number | undefined
  pmCycle?: string | undefined
  department?: string | undefined
  condition?: 'Operational' | 'NonOperational' | 'NotInspected' | undefined
  timeliness?:
    'OnTime' | 'Late' | 'Scheduled' | 'Pending' | 'NotCompleted' | undefined
  search?: string | undefined
}

type DashboardMetricProps = {
  label: string
  value: ReactNode
  note?: string
  recordsSearch?: PmPeriodDashboardRecordsSearch | undefined
}

const selectClassName =
  'min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]'

const dashboardTitleId = 'dashboard-title'

function formatNumber(value: number | string) {
  return typeof value === 'number' ? value.toLocaleString() : value
}

function formatPmCycleMonth(pmCycle: string) {
  return formatPmCycle(pmCycle).replace(/\s+\d{4}$/, '')
}

function formatPercent(value: number | string | null) {
  return value === null ? 'Not measurable yet' : `${value}%`
}

function toChartNumber(value: number | string | null) {
  if (value === null) return null

  const parsed = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

type AssignmentPeriodSummary = {
  knownAssignmentCount: number | string
  unknownAssignmentCount: number | string
  completedWithinPeriod: number | string
  completedLate: number | string
  openWithinPeriod: number | string
  overdueUnfinished: number | string
  unassignedUnfinishedCount?: number | string
  unassignedUnavailableCount?: number | string
}

type DashboardBatchWithProgress =
  PmPeriodDashboardResponse['batches'][number] & {
    progressPercent?: number | string | null
  }

type DashboardWithAssignmentPeriod = Omit<
  PmPeriodDashboardResponse,
  'batches'
> & {
  batches: DashboardBatchWithProgress[]
  assignmentPeriod?: AssignmentPeriodSummary | null
}

function reportPeriodState(value: string) {
  switch (value) {
    case 'Future':
      return { label: 'Upcoming', tone: 'neutral' }
    case 'Active':
      return { label: 'In progress', tone: 'warning' }
    case 'Closed':
      return { label: 'Past deadline', tone: 'neutral' }
    default:
      return { label: value, tone: 'neutral' }
  }
}

function DashboardMetric({
  label,
  value,
  note,
  recordsSearch,
}: DashboardMetricProps) {
  return (
    <Card className="p-4 shadow-none">
      <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </p>
      <p className="mt-2 text-2xl font-bold tracking-tight text-[var(--text-primary)]">
        {recordsSearch ? (
          <Link
            to="/app/dashboard-records"
            search={recordsSearch}
            className="rounded-sm underline decoration-[var(--border-soft)] underline-offset-4 hover:decoration-current focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
          >
            {value}
          </Link>
        ) : (
          value
        )}
      </p>
      {note && (
        <p className="mt-1 text-xs text-[var(--text-secondary)]">{note}</p>
      )}
    </Card>
  )
}

function StatusText({
  value,
  tone = 'neutral',
}: {
  value: string
  tone?: string
}) {
  const toneClass =
    tone === 'success'
      ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
      : tone === 'warning'
        ? 'border-amber-200 bg-amber-50 text-amber-900'
        : tone === 'error'
          ? 'border-red-200 bg-red-50 text-red-800'
          : 'border-[var(--border-soft)] bg-[var(--surface-muted)] text-[var(--text-neutral)]'

  return (
    <span
      className={`inline-flex rounded-full border px-2 py-1 text-xs font-semibold ${toneClass}`}
    >
      {value}
    </span>
  )
}

function QueryError({
  message,
  onRetry,
}: {
  message: string
  onRetry: () => void
}) {
  return (
    <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
      <p className="font-semibold text-[var(--error)]">{message}</p>
      <Button type="button" className="mt-3" onClick={onRetry}>
        Retry
      </Button>
    </Card>
  )
}

function CyclesLoading() {
  return (
    <div
      role="status"
      aria-label="Loading PM period options"
      className="space-y-3"
    >
      <span className="sr-only">Loading PM period options...</span>
      <Skeleton className="h-24 w-full" />
      <div className="grid gap-3 sm:grid-cols-3">
        {Array.from({ length: 3 }, (_, index) => (
          <Skeleton key={index} className="h-20 w-full" />
        ))}
      </div>
    </div>
  )
}

function DashboardLoading() {
  return (
    <div
      role="status"
      aria-label="Loading PM period dashboard"
      className="space-y-3"
    >
      <span className="sr-only">Loading PM period dashboard...</span>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="h-24 w-full" />
        ))}
      </div>
      <Skeleton className="h-56 w-full" />
    </div>
  )
}

function groupForSelection(
  groups: PmPeriodDashboardCycleGroupResponse[],
  assetCategory: string | undefined,
  year: number | undefined,
) {
  return groups.find(
    (group) =>
      group.assetCategory === assetCategory &&
      (year === undefined || Number(group.year) === year),
  )
}

function DashboardMetrics({
  dashboard,
  search,
}: {
  dashboard: DashboardWithAssignmentPeriod
  search: PmPeriodDashboardSearch
}) {
  const scope = {
    assetCategory: search.assetCategory,
    year: search.year,
    pmCycle: search.pmCycle,
    department: search.department,
  }
  const hasRecordScope = Boolean(search.assetCategory && search.pmCycle)
  const recordScope = hasRecordScope ? scope : undefined
  const rows = dashboard.assets as Array<
    PmPeriodDashboardAssetRowResponse & {
      assignedInspectorUserId?: string | null
    }
  >
  const unassignedCount =
    dashboard.assignmentPeriod?.unassignedUnfinishedCount ??
    (rows.length > 0 &&
    rows.every((row) => row.assignedInspectorUserId !== undefined)
      ? rows.filter(
          (row) => !row.isInspected && row.assignedInspectorUserId === null,
        ).length
      : null)
  const overdueCount = dashboard.assignmentPeriod?.overdueUnfinished
  const outstandingCount =
    dashboard.periodState === 'Closed'
      ? dashboard.notCompleted
      : dashboard.remaining

  return (
    <section aria-label="PM period summary" className="space-y-3">
      <div className="pm-dashboard-metrics grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <DashboardMetric
          label="Scheduled"
          value={formatNumber(dashboard.scheduled)}
          note="Schedules in this period and department"
          recordsSearch={recordScope}
        />
        <DashboardMetric
          label="Completed"
          value={formatNumber(dashboard.inspected)}
          note="Schedules with a completed inspection"
          recordsSearch={
            recordScope
              ? { ...recordScope, inspectionStatus: 'completed' }
              : undefined
          }
        />
        <DashboardMetric
          label="Outstanding"
          value={formatNumber(outstandingCount)}
          note="Not completed in the selected period"
          recordsSearch={
            recordScope
              ? { ...recordScope, inspectionStatus: 'outstanding' }
              : undefined
          }
        />
        <DashboardMetric
          label="Overdue assignment"
          value={
            overdueCount === undefined
              ? 'Not available'
              : formatNumber(overdueCount)
          }
          note="Unfinished after the inspector 30-day target"
          recordsSearch={
            overdueCount === undefined || !recordScope
              ? undefined
              : {
                  ...recordScope,
                  inspectionStatus: 'outstanding',
                  assignment: 'overdue',
                }
          }
        />
        <DashboardMetric
          label="Unassigned"
          value={
            unassignedCount === null
              ? 'Not available'
              : formatNumber(unassignedCount)
          }
          note="Unfinished schedules without an assigned inspector"
          recordsSearch={
            unassignedCount === null || !recordScope
              ? undefined
              : {
                  ...recordScope,
                  inspectionStatus: 'outstanding',
                  assignment: 'unassigned',
                }
          }
        />
        <DashboardMetric
          label="Non-operational"
          value={
            dashboard.inspectionResultsAvailable
              ? formatNumber(dashboard.nonOperational)
              : 'Not available'
          }
          note={
            dashboard.inspectionResultsAvailable
              ? 'Recorded inspection results'
              : 'No inspection results are available'
          }
          recordsSearch={
            dashboard.inspectionResultsAvailable && recordScope
              ? { ...recordScope, condition: 'NonOperational' }
              : undefined
          }
        />
      </div>
      <p className="text-xs text-[var(--text-secondary)]">
        Supporting rates: inspection progress{' '}
        {formatPercent(dashboard.progressPercent)} · completed within the
        scheduled month {formatPercent(dashboard.onTimeCompliancePercent)}
      </p>
    </section>
  )
}

function DashboardCharts({
  dashboard,
  search,
  onOpenRecords,
}: {
  dashboard: DashboardWithAssignmentPeriod
  search?: PmPeriodDashboardSearch | undefined
  onOpenRecords?: ((search: PmPeriodDashboardRecordsSearch) => void) | undefined
}) {
  const [comparison, setComparison] = useState('')
  const closed = dashboard.periodState === 'Closed'
  const periodDescription = `${formatAssetCategory(dashboard.assetCategory)} · ${formatPmCycle(dashboard.pmCycle)} · ${dashboard.department ?? 'All departments'}`
  const cohortDescription = `Selected scope: ${periodDescription}. Groups use current asset department metadata. Condition, timeliness and text filters in the detailed rows do not change these server totals.`
  const departmentRows: ReportChartRow[] = dashboard.batches.map((batch) => {
    const department = batch.department ?? 'Department not recorded'
    const id = `${batch.assetCategory}:${batch.pmCycle}:${batch.department ?? 'not-recorded'}`

    return {
      id,
      label: department,
      values: [
        {
          seriesId: 'scheduled',
          seriesLabel: 'Scheduled',
          value: toChartNumber(batch.scheduled),
          unit: 'schedules',
        },
        {
          seriesId: 'inspected',
          seriesLabel: 'Inspected',
          value: toChartNumber(batch.inspected),
          unit: 'schedules',
        },
      ],
    }
  })
  const outcomeRows: ReportChartRow[] = dashboard.batches.map((batch) => {
    const department = batch.department ?? 'Department not recorded'
    const id = `${batch.assetCategory}:${batch.pmCycle}:${batch.department ?? 'not-recorded'}`

    return {
      id,
      label: department,
      values: [
        {
          seriesId: 'completed-on-time',
          seriesLabel: 'Within scheduled month',
          value: toChartNumber(batch.completedOnTime),
          unit: 'schedules',
        },
        {
          seriesId: 'completed-late',
          seriesLabel: 'After scheduled month',
          value: toChartNumber(batch.completedLate),
          unit: 'schedules',
        },
        closed
          ? {
              seriesId: 'not-completed',
              seriesLabel: 'Unfinished',
              value: toChartNumber(batch.notCompleted),
              unit: 'schedules',
            }
          : {
              seriesId: 'remaining',
              seriesLabel: 'Still due',
              value: toChartNumber(batch.remaining),
              unit: 'schedules',
            },
      ],
    }
  })
  const complianceRows: ReportChartRow[] = dashboard.batches.map((batch) => {
    const compliance = dashboard.complianceMeasurable
      ? toChartNumber(batch.onTimeCompliancePercent)
      : null
    const completedOnTime = toChartNumber(batch.completedOnTime)
    const scheduled = toChartNumber(batch.scheduled)

    return {
      id: `${batch.assetCategory}:${batch.pmCycle}:${batch.department ?? 'not-recorded'}`,
      label: batch.department ?? 'Department not recorded',
      values: [
        {
          seriesId: 'on-time-compliance',
          seriesLabel: 'Completed within scheduled month',
          value: compliance,
          unit: '%',
          ...(compliance === null
            ? {
                unavailableReason: dashboard.complianceMeasurable
                  ? 'No measurable scheduled denominator is available.'
                  : 'Available after the PM deadline.',
              }
            : {
                ...(completedOnTime !== null
                  ? { numerator: completedOnTime }
                  : {}),
                ...(scheduled !== null ? { denominator: scheduled } : {}),
              }),
        },
      ],
    }
  })
  const progressRows: ReportChartRow[] = dashboard.batches.map((batch) => {
    const progress = toChartNumber(batch.progressPercent ?? null)
    const inspected = toChartNumber(batch.inspected)
    const scheduled = toChartNumber(batch.scheduled)

    return {
      id: `${batch.assetCategory}:${batch.pmCycle}:${batch.department ?? 'not-recorded'}`,
      label: batch.department ?? 'Department not recorded',
      values: [
        {
          seriesId: 'inspection-progress',
          seriesLabel: 'Inspection progress',
          value: progress,
          unit: '%',
          ...(progress === null
            ? { unavailableReason: 'Department progress is not available.' }
            : {
                ...(inspected !== null ? { numerator: inspected } : {}),
                ...(scheduled !== null ? { denominator: scheduled } : {}),
              }),
        },
      ],
    }
  })
  const hasDepartmentProgress = dashboard.batches.some(
    (batch) => batch.progressPercent !== undefined,
  )
  const handleDrilldown = onOpenRecords
    ? (row: ReportChartRow, value: ReportChartRow['values'][number]) => {
        const batch = dashboard.batches.find(
          (candidate) =>
            (candidate.department ?? 'Department not recorded') === row.label,
        )
        const department = batch
          ? (batch.department ?? undefined)
          : (search?.department ?? undefined)
        const searchForRecords: PmPeriodDashboardRecordsSearch = {
          assetCategory: search?.assetCategory ?? dashboard.assetCategory,
          year:
            search?.year ??
            (Number(dashboard.pmCycle.slice(0, 4)) || undefined),
          pmCycle: search?.pmCycle ?? dashboard.pmCycle,
          ...(department ? { department } : {}),
        }
        switch (value.seriesId) {
          case 'completed-on-time':
            searchForRecords.timeliness = 'OnTime'
            break
          case 'completed-late':
            searchForRecords.timeliness = 'Late'
            break
          case 'not-completed':
          case 'remaining':
            searchForRecords.inspectionStatus = 'outstanding'
            break
          case 'assignment-completed-within':
            searchForRecords.assignmentTiming = 'CompletedWithinPeriod'
            break
          case 'assignment-completed-late':
            searchForRecords.assignmentTiming = 'CompletedLate'
            break
          case 'assignment-open-within':
            searchForRecords.assignmentTiming = 'WithinPeriod'
            break
          case 'known-assignment-period':
            break
          case 'unknown-assignment-period':
            searchForRecords.assignmentTiming = 'Unavailable'
            break
          case 'assignment-overdue-unfinished':
            searchForRecords.inspectionStatus = 'outstanding'
            searchForRecords.assignment = 'overdue'
            break
          case 'non-operational':
            searchForRecords.condition = 'NonOperational'
            break
          case 'operational':
            searchForRecords.condition = 'Operational'
            break
          default:
            break
        }
        onOpenRecords(searchForRecords)
      }
    : undefined
  const conditionValuesAvailable = dashboard.inspectionResultsAvailable
  const conditionRows: ReportChartRow[] = [
    {
      id: `condition:${dashboard.assetCategory}:${dashboard.pmCycle}:${dashboard.department ?? 'all-departments'}`,
      label: dashboard.department ?? 'All departments',
      values: [
        {
          seriesId: 'operational',
          seriesLabel: 'Operational',
          value: conditionValuesAvailable
            ? toChartNumber(dashboard.operational)
            : null,
          unit: 'inspection results',
          ...(!conditionValuesAvailable
            ? { unavailableReason: 'No inspection results are available.' }
            : {}),
        },
        {
          seriesId: 'non-operational',
          seriesLabel: 'Non-operational',
          value: conditionValuesAvailable
            ? toChartNumber(dashboard.nonOperational)
            : null,
          unit: 'inspection results',
          ...(!conditionValuesAvailable
            ? { unavailableReason: 'No inspection results are available.' }
            : {}),
        },
      ],
    },
  ]
  const assignmentPeriod = dashboard.assignmentPeriod
  const assignmentScope = dashboard.department ?? 'All departments'
  const knownAssignmentCount = toChartNumber(
    assignmentPeriod?.knownAssignmentCount ?? null,
  )
  const unknownAssignmentCount = toChartNumber(
    assignmentPeriod?.unknownAssignmentCount ?? null,
  )
  const unassignedUnavailableCount = toChartNumber(
    assignmentPeriod?.unassignedUnavailableCount ?? null,
  )
  const missingTimingCount =
    unknownAssignmentCount !== null && unassignedUnavailableCount !== null
      ? Math.max(0, unknownAssignmentCount - unassignedUnavailableCount)
      : unknownAssignmentCount
  const assignmentCoverageRows: ReportChartRow[] = assignmentPeriod
    ? [
        {
          id: `assignment-coverage:${dashboard.assetCategory}:${dashboard.pmCycle}:${assignmentScope}`,
          label: assignmentScope,
          values: [
            {
              seriesId: 'known-assignment-period',
              seriesLabel: 'Measurable assignment period',
              value: toChartNumber(assignmentPeriod.knownAssignmentCount),
              unit: 'schedules',
            },
            {
              seriesId: 'unknown-assignment-period',
              seriesLabel: 'Unavailable assignment period',
              value: toChartNumber(assignmentPeriod.unknownAssignmentCount),
              unit: 'schedules',
            },
          ],
        },
      ]
    : []
  const assignmentOutcomeRows: ReportChartRow[] = assignmentPeriod
    ? [
        {
          id: `assignment-outcomes:${dashboard.assetCategory}:${dashboard.pmCycle}:${assignmentScope}`,
          label: assignmentScope,
          values: [
            {
              seriesId: 'assignment-completed-within',
              seriesLabel: 'Completed within 30 days',
              value: toChartNumber(assignmentPeriod.completedWithinPeriod),
              unit: 'schedules',
            },
            {
              seriesId: 'assignment-completed-late',
              seriesLabel: 'Completed after 30 days',
              value: toChartNumber(assignmentPeriod.completedLate),
              unit: 'schedules',
            },
            {
              seriesId: 'assignment-open-within',
              seriesLabel: 'Outstanding within 30 days',
              value: toChartNumber(assignmentPeriod.openWithinPeriod),
              unit: 'schedules',
            },
            {
              seriesId: 'assignment-overdue-unfinished',
              seriesLabel: 'Overdue after 30 days',
              value: toChartNumber(assignmentPeriod.overdueUnfinished),
              unit: 'schedules',
            },
          ],
        },
      ]
    : []

  const comparisons = [
    {
      id: 'month',
      label: 'Completion within the scheduled month',
      subtitle: `Inspection counts by department. Scheduled month ends ${formatPmCycleDueDate(dashboard.pmCycle)}. Inspector assignment deadlines are separate.`,
      rows: outcomeRows,
      variant: 'stacked' as const,
      note: 'These outcomes compare completion to the scheduled month, not the 30-day inspector assignment period.',
    },
    {
      id: 'compliance',
      label: 'Completion within the scheduled month (%)',
      subtitle:
        'Percentage of schedules completed by the end of the scheduled month, by department. This is not the inspector assignment deadline.',
      rows: complianceRows,
      variant: 'bar' as const,
      note: 'Completed by the original PM deadline / scheduled. Available after the deadline.',
    },
    {
      id: 'condition',
      label: 'Condition recorded during inspection',
      subtitle:
        'Counts of inspected assets recorded as operational or non-operational.',
      rows: conditionRows,
      variant: 'stacked' as const,
      note: 'Recorded inspection findings only. Uninspected schedules have no condition result.',
    },
    {
      id: 'workload',
      label: 'Scheduled and completed inspections',
      subtitle:
        'Inspection counts by department. Completed inspections are included in scheduled work.',
      rows: departmentRows,
      variant: 'grouped' as const,
      note: 'Inspected work is part of the scheduled total; these counts overlap.',
    },
    ...(assignmentPeriod
      ? [
          {
            id: 'coverage',
            label: 'Assignment-period data availability',
            subtitle:
              'Schedules with usable timing records compared with schedules that cannot be assessed.',
            rows: assignmentCoverageRows,
            variant: 'grouped' as const,
            note: 'Unavailable outcomes lack a required assignment or server completion timestamp. Counts, not percentages.',
          },
        ]
      : []),
  ]
  const selected = comparisons.find((item) => item.id === comparison)

  return (
    <section aria-labelledby="pm-dashboard-charts" className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2
            id="pm-dashboard-charts"
            className="text-lg font-semibold text-[var(--text-primary)]"
          >
            Preventive maintenance overview
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            {periodDescription}
          </p>
        </div>
        <details className="max-w-xl text-sm text-[var(--text-secondary)] print:hidden">
          <summary className="min-h-10 cursor-pointer py-2 font-medium text-[var(--primary)] focus-visible:outline-2">
            About these figures
          </summary>
          <p className="pb-2">{cohortDescription}</p>
        </details>
      </div>
      {dashboard.batches.length === 0 ? (
        <p
          role="status"
          className="rounded-md border border-[var(--border-soft)] px-3 py-3 text-sm text-[var(--text-secondary)]"
        >
          No department groups are available for this period.
        </p>
      ) : (
        <>
          <div className="report-chart-grid grid gap-4 xl:grid-cols-2">
            <ReportChart
              title={
                hasDepartmentProgress
                  ? 'Inspection completion by department'
                  : 'Scheduled and completed inspections'
              }
              subtitle={
                hasDepartmentProgress
                  ? 'Completed inspections out of scheduled inspections. Bars use a 0 to 100% scale.'
                  : 'Inspection counts by department. Completed inspections are part of scheduled work.'
              }
              groupLabel="Department"
              description={
                hasDepartmentProgress
                  ? 'Inspected / scheduled. Percentage scale: 0 to 100%.'
                  : 'Inspected work is part of the scheduled total.'
              }
              variant={hasDepartmentProgress ? 'bar' : 'grouped'}
              rows={hasDepartmentProgress ? progressRows : departmentRows}
              {...(handleDrilldown ? { onDrilldown: handleDrilldown } : {})}
              compact
            />
            <Card className="p-5 shadow-none">
              <h3 className="font-semibold">Inspector assignment turnaround</h3>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                Each inspector has 30 calendar days from the first saved
                assignment. Reassignment does not restart the period.
              </p>
              {knownAssignmentCount === null || knownAssignmentCount === 0 ? (
                <p role="status" className="mt-4 text-sm">
                  No assignment-period results are available for this scope.
                  Missing timing does not mean inspectors completed zero work.
                </p>
              ) : (
                <ReportChart
                  title="Assignment-period outcomes"
                  subtitle="Counts of schedules with usable assignment timing."
                  groupLabel="Scope"
                  variant="grouped"
                  rows={assignmentOutcomeRows}
                  {...(handleDrilldown ? { onDrilldown: handleDrilldown } : {})}
                  compact
                />
              )}
              {missingTimingCount !== null && missingTimingCount > 0 && (
                <p className="mt-3 text-sm text-[var(--text-secondary)]">
                  {formatNumber(missingTimingCount)} schedules have unavailable
                  timing and are excluded from the 30-day outcomes
                  {unassignedUnavailableCount === null
                    ? '; this may include unassigned work'
                    : ''}
                  .
                </p>
              )}
              {assignmentPeriod?.unassignedUnfinishedCount !== undefined &&
                Number(assignmentPeriod.unassignedUnfinishedCount) > 0 && (
                  <p className="mt-2 text-sm text-[var(--text-secondary)]">
                    Currently unassigned schedules may include previously
                    assigned work, so they can also appear in the timing counts.
                  </p>
                )}
            </Card>
          </div>
          <div className="flex flex-wrap items-center gap-3 border-t border-[var(--border-soft)] pt-4 print:hidden">
            <Label htmlFor="dashboard-comparison">More comparisons</Label>
            <select
              id="dashboard-comparison"
              className={`${selectClassName} sm:w-auto sm:min-w-64`}
              value={selected?.id ?? ''}
              onChange={(event) => setComparison(event.target.value)}
            >
              <option value="">Choose a comparison</option>
              {comparisons.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.label}
                </option>
              ))}
            </select>
          </div>
          {selected && (
            <ReportChart
              key={selected.id}
              title={selected.label}
              subtitle={selected.subtitle}
              description={selected.note}
              variant={selected.variant}
              rows={selected.rows}
              {...(handleDrilldown ? { onDrilldown: handleDrilldown } : {})}
              groupLabel={
                ['month', 'compliance', 'workload'].includes(selected.id)
                  ? 'Department'
                  : 'Scope'
              }
              compact
            />
          )}
        </>
      )}
    </section>
  )
}

function isCorrectiveFollowUpRow(asset: PmPeriodDashboardAssetRowResponse) {
  return (
    asset.inspectionId !== null &&
    asset.inspectionCompletedAt !== null &&
    asset.condition === 'NonOperational' &&
    asset.isAcknowledged &&
    (asset.correctiveFollowUpStatus === 'CorrectiveFollowUpPending' ||
      asset.correctiveFollowUpStatus === 'ReferredToWms')
  )
}

function CorrectiveFollowUpSection({
  dashboard,
  search,
}: {
  dashboard: PmPeriodDashboardResponse
  search?: PmPeriodDashboardSearch
}) {
  const rows = dashboard.assets.filter(isCorrectiveFollowUpRow)
  const awaitingReferral = rows.filter(
    (row) => row.correctiveFollowUpStatus === 'CorrectiveFollowUpPending',
  ).length
  const referredToWms = rows.filter(
    (row) => row.correctiveFollowUpStatus === 'ReferredToWms',
  ).length
  const returnContext = { kind: 'dashboard' as const, search: search ?? {} }

  return (
    <section aria-labelledby="pm-dashboard-corrective-follow-up">
      <Card className="content-card p-4 sm:p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2
              id="pm-dashboard-corrective-follow-up"
              className="text-lg font-semibold text-[var(--text-primary)]"
            >
              Corrective follow-up
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              Acknowledged non-operational findings for this PM period.
            </p>
          </div>
          <dl className="grid min-w-[15rem] grid-cols-2 gap-2">
            <div className="rounded-md bg-[var(--surface-muted)] px-3 py-2">
              <dt className="text-xs text-[var(--text-secondary)]">
                Awaiting WMS referral
              </dt>
              <dd className="mt-1 text-lg font-semibold text-[var(--text-primary)]">
                {formatNumber(awaitingReferral)}
              </dd>
            </div>
            <div className="rounded-md bg-[var(--surface-muted)] px-3 py-2">
              <dt className="text-xs text-[var(--text-secondary)]">
                Referred to WMS
              </dt>
              <dd className="mt-1 text-lg font-semibold text-[var(--text-primary)]">
                {formatNumber(referredToWms)}
              </dd>
            </div>
          </dl>
        </div>

        {rows.length === 0 ? (
          <p
            role="status"
            className="mt-4 rounded-md border border-[var(--border-soft)] px-3 py-4 text-sm text-[var(--text-secondary)]"
          >
            No acknowledged non-operational findings are recorded for this PM
            period.
          </p>
        ) : (
          <div className="mt-4 overflow-x-auto rounded-lg border border-[var(--border-soft)]">
            <table className="w-full min-w-[880px] text-left text-sm">
              <caption className="sr-only">
                Corrective follow-up for acknowledged non-operational findings
              </caption>
              <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
                <tr>
                  {[
                    'Asset',
                    'Department / location',
                    'Finding / recommendation',
                    'Follow-up status',
                    'WMS PM number',
                    'Action',
                  ].map((heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="px-3 py-2.5 font-semibold text-[var(--text-primary)]"
                    >
                      {heading}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const statusLabel =
                    row.correctiveFollowUpStatus === 'ReferredToWms'
                      ? 'Referred to WMS'
                      : 'Awaiting WMS referral'
                  const finding =
                    [row.remarks, row.actionsRecommendations]
                      .filter((value): value is string =>
                        Boolean(value?.trim()),
                      )
                      .join(' · ') || 'No finding or recommendation recorded'
                  const location =
                    [row.department, row.building, row.location]
                      .filter(Boolean)
                      .join(' · ') || 'Not recorded'

                  return (
                    <tr
                      key={row.inspectionId}
                      className="border-b border-[var(--border-soft)] align-top last:border-0"
                    >
                      <th
                        scope="row"
                        className="px-3 py-3 font-semibold whitespace-nowrap text-[var(--text-primary)]"
                      >
                        {row.assetCode}
                      </th>
                      <td className="max-w-64 px-3 py-3 text-[var(--text-secondary)]">
                        {location}
                      </td>
                      <td
                        className="max-w-80 px-3 py-3 text-[var(--text-secondary)]"
                        title={finding}
                      >
                        <p className="line-clamp-2">{finding}</p>
                      </td>
                      <td className="px-3 py-3">
                        <StatusText
                          value={statusLabel}
                          tone={
                            row.correctiveFollowUpStatus ===
                            'CorrectiveFollowUpPending'
                              ? 'warning'
                              : 'neutral'
                          }
                        />
                      </td>
                      <td className="px-3 py-3 text-[var(--text-secondary)]">
                        {row.externalPmNumber ?? 'Not recorded'}
                      </td>
                      <td className="space-y-1 px-3 py-3">
                        <Link
                          to="/app/inspections/$inspectionId"
                          params={{ inspectionId: row.inspectionId! }}
                          search={{ returnContext }}
                          className="block font-semibold whitespace-nowrap text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                        >
                          View inspection
                        </Link>
                        {row.formId && (
                          <Link
                            to="/app/preventive-maintenance-forms/$formId"
                            params={{ formId: row.formId }}
                            search={{ readonly: true, returnContext }}
                            className="block font-semibold whitespace-nowrap text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                          >
                            Open PM form
                          </Link>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </section>
  )
}

export function PmPeriodDashboardPresentation({
  dashboard,
  search,
  onOpenRecords,
}: {
  dashboard: PmPeriodDashboardResponse
  search?: PmPeriodDashboardSearch | undefined
  onOpenRecords?: ((search: PmPeriodDashboardRecordsSearch) => void) | undefined
}) {
  return (
    <>
      <DashboardMetrics dashboard={dashboard} search={search ?? {}} />
      <DashboardCharts
        dashboard={dashboard}
        search={search}
        onOpenRecords={onOpenRecords}
      />
      <CorrectiveFollowUpSection
        dashboard={dashboard}
        {...(search ? { search } : {})}
      />
    </>
  )
}

export function PmPeriodDashboard({
  search,
  onSearchChange,
}: {
  search: PmPeriodDashboardSearch
  onSearchChange: (
    next: PmPeriodDashboardSearch,
    options?: { replace?: boolean },
  ) => void
}) {
  const navigate = useNavigate()
  const cyclesQuery = usePmPeriodDashboardCycles()
  const cycles = useMemo(() => cyclesQuery.data ?? [], [cyclesQuery.data])
  const currentUser = useCurrentUser()
  const canAskNla = Boolean(
    currentUser.data?.id && currentUser.data.roles.includes('GSD'),
  )
  const categoryOptions = useMemo(
    () =>
      [
        ...new Set(
          cycles
            .filter((group) => group.cycles.length > 0)
            .map((group) => group.assetCategory),
        ),
      ].sort((left, right) =>
        formatAssetCategory(left).localeCompare(formatAssetCategory(right)),
      ),
    [cycles],
  )
  const hasRequestedScope = Boolean(
    search.assetCategory && search.year !== undefined && search.pmCycle,
  )
  const [draftCategoryValue, setDraftCategoryValue] = useState(
    hasRequestedScope ? (search.assetCategory ?? '') : '',
  )
  const [draftYearValue, setDraftYearValue] = useState(
    hasRequestedScope && search.year !== undefined ? String(search.year) : '',
  )
  const [draftCycleValue, setDraftCycleValue] = useState(
    hasRequestedScope ? (search.pmCycle ?? '') : '',
  )
  const [isChangingSelection, setIsChangingSelection] = useState(false)

  const draftCategory = categoryOptions.includes(draftCategoryValue)
    ? draftCategoryValue
    : ''
  const yearOptions = useMemo(
    () =>
      cycles
        .filter(
          (group) =>
            group.assetCategory === draftCategory && group.cycles.length > 0,
        )
        .map((group) => Number(group.year))
        .sort((left, right) => right - left),
    [cycles, draftCategory],
  )
  const draftYear = yearOptions.includes(Number(draftYearValue))
    ? Number(draftYearValue)
    : undefined
  const draftGroup = groupForSelection(cycles, draftCategory, draftYear)
  const draftCycle = draftGroup?.cycles.some(
    (cycle) => cycle.pmCycle === draftCycleValue,
  )
    ? draftCycleValue
    : ''
  const categoryPickerOptions = useMemo(
    () =>
      categoryOptions.map((category) => ({
        value: category,
        label: formatAssetCategory(category),
      })),
    [categoryOptions],
  )
  const selectedCategoryOption = categoryPickerOptions.find(
    (option) => option.value === draftCategory,
  )

  const generatedGroup = groupForSelection(
    cycles,
    search.assetCategory,
    search.year,
  )
  const generatedCycle = generatedGroup?.cycles.find(
    (cycle) => cycle.pmCycle === search.pmCycle,
  )
  const hasValidGeneratedScope =
    cyclesQuery.isSuccess && hasRequestedScope && Boolean(generatedCycle)
  const reportScopeFilters =
    hasValidGeneratedScope && search.assetCategory && search.pmCycle
      ? { assetCategory: search.assetCategory, pmCycle: search.pmCycle }
      : undefined
  const dashboardFilters = reportScopeFilters
    ? {
        ...reportScopeFilters,
        ...(search.department ? { department: search.department } : {}),
      }
    : undefined
  const scopeDashboardQuery = usePmPeriodDashboard(reportScopeFilters)
  const dashboardQuery = usePmPeriodDashboard(dashboardFilters)
  const showGeneratedReport = hasValidGeneratedScope && !isChangingSelection
  const assignmentRole = currentUser.data?.roles.includes('GSD')
    ? 'GSD'
    : currentUser.data?.roles.includes('Supervisor')
      ? 'Supervisor'
      : null
  const attentionQuery = useScheduleAssignmentBatches(
    {
      year: search.year ?? 1,
      ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
      ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
      ...(search.department ? { department: search.department } : {}),
    },
    Boolean(showGeneratedReport && assignmentRole && search.year),
  )
  const canGenerate = Boolean(
    draftCategory && draftYear !== undefined && draftCycle,
  )

  const departmentOptions = useMemo(
    () =>
      [
        ...new Set(
          [
            ...(scopeDashboardQuery.data?.batches.map(
              (batch) => batch.department,
            ) ?? []),
            ...(scopeDashboardQuery.data?.assets.map(
              (asset) => asset.department,
            ) ?? []),
          ].filter((value): value is string => Boolean(value)),
        ),
      ].sort(),
    [scopeDashboardQuery.data],
  )
  const dashboardReturnSearch: PmPeriodDashboardSearch = {
    assetCategory: search.assetCategory,
    year: search.year,
    pmCycle: search.pmCycle,
    department: search.department,
    condition: search.condition,
    timeliness: search.timeliness,
    search: search.search,
  }

  const handleGenerate = () => {
    if (!canGenerate || draftYear === undefined) return

    if (
      search.assetCategory === draftCategory &&
      search.year === draftYear &&
      search.pmCycle === draftCycle
    ) {
      setIsChangingSelection(false)
      return
    }

    onSearchChange({
      ...search,
      assetCategory: draftCategory,
      year: draftYear,
      pmCycle: draftCycle,
      department: undefined,
      condition: undefined,
      timeliness: undefined,
      search: undefined,
    })
    setIsChangingSelection(false)
  }

  const handleChangeSelection = () => {
    setDraftCategoryValue(search.assetCategory ?? '')
    setDraftYearValue(search.year === undefined ? '' : String(search.year))
    setDraftCycleValue(search.pmCycle ?? '')
    setIsChangingSelection(true)
  }

  if (cyclesQuery.isPending) {
    return (
      <section aria-labelledby="dashboard-title" className="space-y-4">
        <DashboardHeader canAskNla={canAskNla} search={search} />
        <CyclesLoading />
      </section>
    )
  }

  if (cyclesQuery.isError) {
    return (
      <section aria-labelledby="dashboard-title" className="space-y-4">
        <DashboardHeader canAskNla={canAskNla} search={search} />
        <QueryError
          message="PM period options could not be loaded."
          onRetry={() => void cyclesQuery.refetch()}
        />
      </section>
    )
  }

  if (categoryOptions.length === 0) {
    return (
      <section aria-labelledby="dashboard-title" className="space-y-4">
        <DashboardHeader canAskNla={canAskNla} search={search} />
        <Card className="p-6 shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No PM periods are scheduled
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Schedule a PM period to make it available for report generation.
          </p>
        </Card>
      </section>
    )
  }

  return (
    <section aria-labelledby="dashboard-title" className="space-y-4">
      <DashboardHeader canAskNla={canAskNla} search={search} />

      {showGeneratedReport ? (
        <>
          <div className="pm-dashboard-report space-y-4">
            <header className="pm-summary-print-heading">
              <p>UniPM · Preventive maintenance</p>
              <h1>PM period summary report</h1>
              <p>
                {formatAssetCategory(search.assetCategory ?? '')} ·{' '}
                {search.pmCycle ? formatPmCycle(search.pmCycle) : ''} ·{' '}
                {search.department ?? 'All departments'}
              </p>
              {dashboardQuery.dataUpdatedAt > 0 && (
                <p>
                  Data loaded{' '}
                  {new Intl.DateTimeFormat('en-PH', {
                    dateStyle: 'medium',
                    timeStyle: 'short',
                    timeZone: 'Asia/Manila',
                  }).format(dashboardQuery.dataUpdatedAt)}{' '}
                  Manila time.
                </p>
              )}
            </header>
            <section aria-labelledby="pm-dashboard-reporting-period">
              <Card className="content-card p-3 sm:p-4">
                <div className="flex flex-wrap items-center justify-between gap-x-5 gap-y-3">
                  <div>
                    <p className="text-xs font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
                      Reporting period
                    </p>
                    <div className="mt-1 flex flex-wrap items-center gap-2">
                      <h2
                        id="pm-dashboard-reporting-period"
                        className="text-base font-semibold text-[var(--text-primary)]"
                      >
                        {formatAssetCategory(search.assetCategory ?? '')} ·{' '}
                        {search.pmCycle
                          ? formatPmCycle(search.pmCycle)
                          : 'Not selected'}
                      </h2>
                      {dashboardQuery.data && (
                        <StatusText
                          value={
                            reportPeriodState(dashboardQuery.data.periodState)
                              .label
                          }
                          tone={
                            reportPeriodState(dashboardQuery.data.periodState)
                              .tone
                          }
                        />
                      )}
                    </div>
                    <p className="mt-1 text-xs text-[var(--text-secondary)]">
                      Due (Asia/Manila){' '}
                      {dashboardQuery.isPending
                        ? 'Loading...'
                        : dashboardQuery.data
                          ? formatPmCycleDueDate(dashboardQuery.data.pmCycle)
                          : 'Unavailable'}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2 print:hidden">
                    <Button asChild variant="secondary">
                      <Link
                        to="/app/dashboard-records"
                        search={dashboardReturnSearch}
                      >
                        View records
                      </Link>
                    </Button>
                    <Button
                      type="button"
                      disabled={!dashboardQuery.data}
                      onClick={() => window.print()}
                    >
                      Print summary
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={handleChangeSelection}
                    >
                      Change period
                    </Button>
                  </div>
                </div>
                <div className="mt-3 flex flex-wrap items-end justify-between gap-3 border-t border-[var(--border-soft)] pt-3">
                  <p className="text-xs text-[var(--text-secondary)]">
                    Scheduled, inspected, and follow-up totals for this period.
                  </p>
                  <div className="w-full sm:max-w-xs">
                    <Label htmlFor="pm-dashboard-department">Department</Label>
                    <select
                      id="pm-dashboard-department"
                      value={search.department ?? ''}
                      onChange={(event) =>
                        onSearchChange({
                          ...search,
                          department: event.target.value || undefined,
                          condition: undefined,
                          timeliness: undefined,
                          search: undefined,
                        })
                      }
                      disabled={scopeDashboardQuery.isPending}
                      className={`${selectClassName} mt-1`}
                    >
                      <option value="">All departments</option>
                      {search.department &&
                        !departmentOptions.includes(search.department) && (
                          <option value={search.department}>
                            {search.department}
                          </option>
                        )}
                      {departmentOptions.map((value) => (
                        <option key={value} value={value}>
                          {value}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              </Card>
            </section>

            {assignmentRole &&
              (attentionQuery.isPending ||
              attentionQuery.isPlaceholderData ||
              attentionQuery.isFetching ? (
                <p
                  role="status"
                  className="rounded-md border border-[var(--border-soft)] p-3 text-sm print:hidden"
                >
                  Loading assignment attention...
                </p>
              ) : attentionQuery.isError ||
                attentionQuery.data?.summary.pendingAssignmentBatchCount ===
                  undefined ||
                attentionQuery.data?.summary.urgentAssignmentBatchCount ===
                  undefined ? (
                <p
                  role="status"
                  className="rounded-md border border-[var(--border-soft)] p-3 text-sm print:hidden"
                >
                  Assignment attention is unavailable.{' '}
                  <button
                    type="button"
                    className="cursor-pointer underline focus-visible:outline-2"
                    onClick={() => void attentionQuery.refetch()}
                  >
                    Retry
                  </button>
                </p>
              ) : attentionQuery.data.summary.pendingAssignmentBatchCount >
                0 ? (
                <div
                  className={`flex flex-wrap items-center gap-3 rounded-md border border-l-4 p-4 print:hidden ${attentionQuery.data.summary.urgentAssignmentBatchCount ? 'border-red-300 border-l-red-600 bg-red-50 text-red-900' : attentionQuery.data.batches.some((batch) => batch.attention?.priority === 1) ? 'border-amber-300 border-l-amber-600 bg-amber-50 text-amber-900' : 'border-slate-200 border-l-slate-500 bg-slate-50 text-slate-900'}`}
                >
                  <AlertTriangle
                    aria-hidden="true"
                    className="size-5 shrink-0"
                  />
                  <div className="min-w-0 flex-1">
                    <h2 className="font-semibold">Assignment attention</h2>
                    <p className="text-sm">
                      {attentionQuery.data.summary.pendingAssignmentBatchCount}{' '}
                      {attentionQuery.data.summary
                        .pendingAssignmentBatchCount === 1
                        ? 'batch needs'
                        : 'batches need'}{' '}
                      {assignmentRole === 'GSD'
                        ? 'a Supervisor'
                        : 'an Inspector'}
                      .{' '}
                      {attentionQuery.data.summary.urgentAssignmentBatchCount ??
                        0}{' '}
                      {attentionQuery.data.summary
                        .urgentAssignmentBatchCount === 1
                        ? 'is'
                        : 'are'}{' '}
                      urgent.
                    </p>
                    <p className="text-xs">
                      {formatAssetCategory(search.assetCategory ?? '')} ·{' '}
                      {search.pmCycle ? formatPmCycle(search.pmCycle) : ''} ·{' '}
                      {search.department ?? 'All departments'}
                    </p>
                  </div>
                  <Link
                    to="/app/schedules"
                    search={{
                      view: 'assignments',
                      year: search.year,
                      assetCategory: assetCategorySchema.shape.code.parse(
                        search.assetCategory,
                      ),
                      pmCycle: search.pmCycle,
                      department: search.department,
                    }}
                    className="cursor-pointer rounded px-3 py-2 font-semibold underline-offset-2 hover:bg-black/5 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
                  >
                    Review assignments →
                  </Link>
                </div>
              ) : null)}

            {dashboardQuery.isPending ? (
              <DashboardLoading />
            ) : dashboardQuery.isError ? (
              <QueryError
                message="The selected PM period could not be loaded."
                onRetry={() => void dashboardQuery.refetch()}
              />
            ) : dashboardQuery.data ? (
              <>
                {dashboardQuery.data.scheduled === 0 && (
                  <p
                    role="status"
                    className="rounded-md border border-[var(--border-soft)] px-3 py-2 text-sm text-[var(--text-secondary)]"
                  >
                    No schedules are in this department for the selected PM
                    period.
                  </p>
                )}
                <PmPeriodDashboardPresentation
                  dashboard={dashboardQuery.data}
                  search={dashboardReturnSearch}
                  onOpenRecords={(recordsSearch) =>
                    void navigate({
                      to: '/app/dashboard-records',
                      search: recordsSearch,
                    })
                  }
                />
              </>
            ) : null}
          </div>
        </>
      ) : (
        <>
          <section aria-labelledby="pm-dashboard-reporting-period">
            <Card className="content-card p-4 sm:p-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
                    Reporting period
                  </p>
                  <h2
                    id="pm-dashboard-reporting-period"
                    className="mt-1 text-xl font-semibold text-[var(--text-primary)]"
                  >
                    Choose a scheduled PM period
                  </h2>
                  <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
                    Choose a category, year, and scheduled month. The report
                    loads only after you generate it.
                  </p>
                </div>
                <div className="flex flex-wrap gap-2 print:hidden">
                  {hasValidGeneratedScope && isChangingSelection && (
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={() => setIsChangingSelection(false)}
                    >
                      Cancel
                    </Button>
                  )}
                </div>
              </div>

              {hasRequestedScope && !hasValidGeneratedScope && (
                <p
                  role="status"
                  className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
                >
                  This saved report period is no longer available. Choose a
                  scheduled period and generate a new report.
                </p>
              )}

              <div className="mt-6 space-y-4">
                <div className="grid min-w-0 gap-4 md:grid-cols-1 lg:grid-cols-[minmax(0,45fr)_minmax(8rem,20fr)_minmax(0,35fr)] lg:items-start">
                  <SearchablePicker
                    id="pm-dashboard-category"
                    label="Asset category"
                    value={draftCategory || undefined}
                    options={categoryPickerOptions}
                    selectedOption={selectedCategoryOption}
                    placeholder="Select an asset category"
                    emptyMessage="No scheduled categories match your search."
                    onChange={(value) => {
                      setDraftCategoryValue(value ?? '')
                      setDraftYearValue('')
                      setDraftCycleValue('')
                    }}
                  />

                  <div className="min-w-0">
                    <Label
                      htmlFor="pm-dashboard-year"
                      className="text-sm font-semibold tracking-normal text-[var(--text-primary)] normal-case"
                    >
                      Year
                    </Label>
                    <select
                      id="pm-dashboard-year"
                      value={draftYear === undefined ? '' : String(draftYear)}
                      disabled={!draftCategory || yearOptions.length === 0}
                      onChange={(event) => {
                        setDraftYearValue(event.target.value)
                        setDraftCycleValue('')
                      }}
                      className={`${selectClassName} mt-1`}
                    >
                      <option value="">
                        {draftCategory
                          ? 'Choose a year'
                          : 'Choose category first'}
                      </option>
                      {yearOptions.map((year) => (
                        <option key={year} value={year}>
                          {year}
                        </option>
                      ))}
                    </select>
                    {draftCategory && yearOptions.length === 0 && (
                      <p
                        role="status"
                        className="mt-1 text-xs text-[var(--text-secondary)]"
                      >
                        No years with scheduled maintenance are available for
                        this category.
                      </p>
                    )}
                  </div>

                  <div className="min-w-0">
                    <Label
                      htmlFor="pm-dashboard-month"
                      className="text-sm font-semibold tracking-normal text-[var(--text-primary)] normal-case"
                    >
                      Scheduled month
                    </Label>
                    <select
                      id="pm-dashboard-month"
                      value={draftCycle}
                      disabled={
                        draftYear === undefined || !draftGroup?.cycles.length
                      }
                      onChange={(event) =>
                        setDraftCycleValue(event.target.value)
                      }
                      className={`${selectClassName} mt-1`}
                    >
                      <option value="">
                        {draftYear === undefined
                          ? 'Choose year first'
                          : 'Choose a scheduled month'}
                      </option>
                      {(draftGroup?.cycles ?? []).map((cycle) => (
                        <option key={cycle.pmCycle} value={cycle.pmCycle}>
                          {formatPmCycleMonth(cycle.pmCycle)}
                        </option>
                      ))}
                    </select>
                    {draftYear !== undefined && !draftGroup?.cycles.length && (
                      <p
                        role="status"
                        className="mt-1 text-xs text-[var(--text-secondary)]"
                      >
                        No scheduled months are available for this category and
                        year.
                      </p>
                    )}
                  </div>
                </div>

                <div className="flex gap-2 border-t border-[var(--border-soft)] pt-4">
                  <Button
                    type="button"
                    disabled={!canGenerate}
                    onClick={handleGenerate}
                  >
                    Generate dashboard
                  </Button>
                </div>
              </div>
            </Card>
          </section>
        </>
      )}
    </section>
  )
}

export function DashboardHeader({
  canAskNla,
  search = {},
}: {
  canAskNla: boolean
  search?: PmPeriodDashboardSearch
}) {
  return (
    <PageHeading
      id={dashboardTitleId}
      tabIndex={-1}
      eyebrow="UniPM"
      title="Preventive Maintenance Dashboard"
      description="Choose a scheduled PM period to review."
      actions={
        canAskNla ? (
          <Button asChild>
            <Link
              to="/app/analytics"
              search={{ returnContext: { kind: 'dashboard', search } }}
            >
              Ask a question
            </Link>
          </Button>
        ) : undefined
      }
    />
  )
}
