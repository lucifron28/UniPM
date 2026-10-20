import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link } from '@tanstack/react-router'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import { Skeleton } from '@/components/ui/skeleton'
import type {
  PmPeriodDashboardAssetRowResponse,
  PmPeriodDashboardBatchResponse,
  PmPeriodDashboardCycleGroupResponse,
  PmPeriodDashboardResponse,
} from '@/api/generated/models'
import {
  usePmPeriodDashboard,
  usePmPeriodDashboardCycles,
} from '@/features/reports/pm-period-dashboard-queries'
import { useCurrentUser } from '@/features/auth/current-user'
import { useScheduleEnrollmentDeferrals } from '@/features/schedules/schedule-queries'
import {
  formatPmCycle,
  formatPmCycleDueDate,
} from '@/features/schedules/schedule-presentation'
import './pm-dashboard-print.css'

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
}

const selectClassName =
  'min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]'

const conditionOptions = [
  { value: '', label: 'All conditions' },
  { value: 'Operational', label: 'Operational' },
  { value: 'NonOperational', label: 'Non-operational' },
  { value: 'NotInspected', label: 'Not inspected' },
]

const timelinessOptions = [
  { value: '', label: 'All timeliness' },
  { value: 'OnTime', label: 'Completed on time' },
  { value: 'Late', label: 'Completed late' },
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'Pending', label: 'Pending' },
  { value: 'NotCompleted', label: 'Not completed' },
]

const dashboardTitleId = 'dashboard-title'

function formatDate(
  value: string | null | undefined,
  withTime = false,
  timeZone?: string,
) {
  if (!value) return 'Not recorded'

  return new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    ...(withTime ? { timeStyle: 'short' } : {}),
    ...(timeZone ? { timeZone } : {}),
  }).format(new Date(value))
}

function formatNumber(value: number | string) {
  return typeof value === 'number' ? value.toLocaleString() : value
}

function formatPmCycleMonth(pmCycle: string) {
  return formatPmCycle(pmCycle).replace(/\s+\d{4}$/, '')
}

function formatPercent(value: number | string | null) {
  return value === null ? 'Not measurable yet' : `${value}%`
}

function formatCondition(value: string) {
  switch (value) {
    case 'NonOperational':
      return 'Non-operational'
    case 'NotInspected':
      return 'Not inspected'
    default:
      return value
  }
}

function formatTimeliness(value: string) {
  switch (value) {
    case 'OnTime':
      return 'Completed on time'
    case 'Late':
      return 'Completed late'
    case 'Scheduled':
      return 'Scheduled'
    case 'Pending':
      return 'Pending'
    case 'NotCompleted':
      return 'Not completed'
    default:
      return value
  }
}

function formatPeriodState(value: string) {
  switch (value) {
    case 'Future':
      return 'Future'
    case 'Active':
      return 'Active'
    case 'Closed':
      return 'Closed'
    default:
      return value
  }
}

function formatExecutionStatus(value: string) {
  return value === 'NotCompleted' ? 'Not completed' : value
}

function formatFormStatus(value: string | null | undefined) {
  if (!value) return 'No form linked'
  return value === 'Submitted' ? 'Awaiting acknowledgement' : value
}

function formatAcknowledgementStatus(
  formId: string | null | undefined,
  formStatus: string | null | undefined,
  isAcknowledged: boolean,
) {
  if (isAcknowledged) return 'Acknowledged'
  if (formStatus === 'Submitted') return 'Awaiting acknowledgement'
  if (formStatus === 'Draft') return 'Draft not submitted'
  return formId ? 'No acknowledgement recorded' : 'No form linked'
}

function formatLocation(
  row: Pick<PmPeriodDashboardAssetRowResponse, 'building' | 'location'>,
) {
  return (
    [row.building, row.location].filter(Boolean).join(' · ') || 'Not recorded'
  )
}

function DashboardMetric({ label, value, note }: DashboardMetricProps) {
  return (
    <Card className="p-4 shadow-none">
      <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </p>
      <p className="mt-2 text-2xl font-bold tracking-tight text-[var(--text-primary)]">
        {value}
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
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {Array.from({ length: 5 }, (_, index) => (
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

function BatchOverview({
  batches,
  periodState,
  dashboardSearch,
}: {
  batches: PmPeriodDashboardBatchResponse[]
  periodState: string
  dashboardSearch?: PmPeriodDashboardSearch | undefined
}) {
  const isClosed = periodState === 'Closed'

  return (
    <Card className="content-card p-4 sm:p-5">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            Department batch acknowledgement
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Batch context is persisted by department and PM period. Forms ready
            for acknowledgement remain visible until they are acknowledged.
          </p>
        </div>
        <p className="text-xs text-[var(--text-neutral)]">
          {batches.length} {batches.length === 1 ? 'batch' : 'batches'} returned
        </p>
      </div>

      <div className="mt-4 overflow-x-auto rounded-lg border border-[var(--border-soft)]">
        <table className="w-full min-w-[920px] text-left text-sm">
          <caption className="sr-only">
            Department batch acknowledgement overview
          </caption>
          <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
            <tr>
              {[
                'Department',
                'Scheduled',
                'Inspected',
                'Completed on time',
                'Late',
                isClosed ? 'Not completed' : 'Remaining',
                'Form / acknowledgement',
                'Action',
              ].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  data-print-hide={heading === 'Action' ? '' : undefined}
                  className="px-3 py-3 font-semibold text-[var(--text-primary)]"
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {batches.map((batch) => (
              <tr
                key={`${batch.department ?? 'unassigned'}-${batch.pmCycle}`}
                className="border-b border-[var(--border-soft)] last:border-0"
              >
                <td className="px-3 py-3 font-semibold text-[var(--text-primary)]">
                  {batch.department ?? 'Not recorded'}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatNumber(batch.scheduled)}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatNumber(batch.inspected)}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatNumber(batch.completedOnTime)}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatNumber(batch.completedLate)}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatNumber(
                    isClosed ? batch.notCompleted : batch.remaining,
                  )}
                </td>
                <td className="space-y-2 px-3 py-3 text-[var(--text-secondary)]">
                  <p>{formatFormStatus(batch.formStatus)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    {batch.fileNumber ?? 'No file number'}
                    {' · '}
                    {batch.isAcknowledged
                      ? `Acknowledged ${formatDate(batch.acknowledgedAt)}`
                      : formatAcknowledgementStatus(
                          batch.formId,
                          batch.formStatus,
                          batch.isAcknowledged,
                        )}
                  </p>
                </td>
                <td data-print-hide className="px-3 py-3">
                  {batch.formId && batch.formStatus === 'Submitted' ? (
                    <Link
                      to="/app/preventive-maintenance-forms/$formId/review"
                      params={{ formId: batch.formId }}
                      search={{
                        ...(dashboardSearch ?? {}),
                        returnContext: {
                          kind: 'dashboard',
                          search: dashboardSearch ?? {},
                        },
                      }}
                      className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                    >
                      Review batch
                    </Link>
                  ) : batch.formId ? (
                    <Link
                      to="/app/preventive-maintenance-forms/$formId"
                      params={{ formId: batch.formId }}
                      search={{
                        ...dashboardSearch,
                        readonly: batch.formStatus === 'Acknowledged',
                        returnContext: {
                          kind: 'dashboard',
                          search: dashboardSearch ?? {},
                        },
                      }}
                      className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                    >
                      View batch
                    </Link>
                  ) : (
                    <span className="text-xs text-[var(--text-neutral)]">
                      No form action
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function AssetRows({
  assets,
  dashboardSearch,
}: {
  assets: PmPeriodDashboardAssetRowResponse[]
  dashboardSearch?: PmPeriodDashboardSearch | undefined
}) {
  return (
    <Card className="content-card p-4 sm:p-5">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            Scheduled assets
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Every scheduled asset is listed, including rows without a completed
            inspection.
          </p>
        </div>
        <p className="text-xs text-[var(--text-neutral)]">
          {assets.length} rows returned
        </p>
      </div>

      <div className="mt-4 overflow-x-auto rounded-lg border border-[var(--border-soft)]">
        <table className="w-full min-w-[1240px] text-left text-sm">
          <caption className="sr-only">
            Scheduled PM assets and inspection status
          </caption>
          <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
            <tr>
              {[
                'Asset',
                'Department',
                'Building / location',
                'Scheduled month / due date',
                'Inspection completion',
                'Timeliness',
                'Condition',
                'Form / batch context',
              ].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="px-3 py-3 font-semibold text-[var(--text-primary)]"
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {assets.map((asset) => (
              <tr
                key={asset.scheduleId}
                className="border-b border-[var(--border-soft)] align-top last:border-0"
              >
                <td className="px-3 py-3">
                  <Link
                    to="/app/assets/$assetId"
                    params={{ assetId: asset.assetId }}
                    search={{
                      returnContext: {
                        kind: 'dashboard',
                        search: dashboardSearch ?? {},
                      },
                    }}
                    className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                  >
                    {asset.assetCode}
                  </Link>
                  <p className="mt-1 text-xs text-[var(--text-neutral)]">
                    {formatAssetCategory(asset.assetCategory)}
                  </p>
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {asset.department ?? 'Not recorded'}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatLocation(asset)}
                </td>
                <td className="space-y-1 px-3 py-3 text-[var(--text-secondary)]">
                  <p className="font-semibold text-[var(--text-primary)]">
                    {formatPmCycle(asset.pmCycle)}
                  </p>
                  <p>Due date: {formatPmCycleDueDate(asset.pmCycle)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    {asset.scheduleStatus}
                  </p>
                </td>
                <td className="space-y-1 px-3 py-3 text-[var(--text-secondary)]">
                  <p>{formatExecutionStatus(asset.executionStatus)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    {asset.isInspected
                      ? `Actual inspection date: ${formatDate(asset.inspectionCompletedAt, true)}`
                      : 'Not inspected'}
                  </p>
                </td>
                <td className="px-3 py-3">
                  <StatusText
                    value={formatTimeliness(asset.timeliness)}
                    tone={
                      asset.timeliness === 'OnTime'
                        ? 'success'
                        : asset.timeliness === 'Late'
                          ? 'warning'
                          : 'neutral'
                    }
                  />
                </td>
                <td className="px-3 py-3">
                  <StatusText
                    value={formatCondition(asset.condition)}
                    tone={
                      asset.condition === 'NonOperational' ? 'error' : 'neutral'
                    }
                  />
                </td>
                <td className="space-y-1 px-3 py-3 text-[var(--text-secondary)]">
                  <p>{formatFormStatus(asset.formStatus)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    Acknowledgement:{' '}
                    {formatAcknowledgementStatus(
                      asset.formId,
                      asset.formStatus,
                      asset.isAcknowledged,
                    )}
                  </p>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function PeriodStateSummary({
  dashboard,
}: {
  dashboard: PmPeriodDashboardResponse
}) {
  const state = formatPeriodState(dashboard.periodState)
  const message =
    dashboard.periodState === 'Future'
      ? 'This period is before its month-end deadline. Unfinished rows are marked Scheduled, and the compliance rate is not measurable yet.'
      : dashboard.periodState === 'Active'
        ? 'This period is in progress. Unfinished rows are marked Pending, and the compliance rate is not measurable yet.'
        : dashboard.periodState === 'Closed'
          ? 'This period is closed. Completed rows retain their final on-time or late result; unfinished rows are marked Not completed.'
          : 'The backend returned an unrecognized period state. Review the row-level status values for this period.'

  return (
    <Card
      role="status"
      className="flex flex-wrap items-center gap-x-3 gap-y-1 p-3 shadow-none"
    >
      <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        Period state
      </p>
      <p className="font-semibold text-[var(--text-primary)]">{state}</p>
      <StatusText
        value={state}
        tone={
          dashboard.periodState === 'Closed'
            ? 'success'
            : dashboard.periodState === 'Active'
              ? 'warning'
              : 'neutral'
        }
      />
      <p className="basis-full text-sm text-[var(--text-secondary)]">
        {message}
      </p>
    </Card>
  )
}

function DashboardMetrics({
  dashboard,
}: {
  dashboard: PmPeriodDashboardResponse
}) {
  const isClosed = dashboard.periodState === 'Closed'

  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      <DashboardMetric
        label="Progress"
        value={formatPercent(dashboard.progressPercent)}
        note="Inspected / scheduled asset cycles"
      />
      <DashboardMetric
        label="Compliance rate"
        value={
          dashboard.complianceMeasurable
            ? formatPercent(dashboard.onTimeCompliancePercent)
            : 'Not measurable yet'
        }
        note={
          dashboard.complianceMeasurable
            ? 'Completed on time / scheduled asset cycles'
            : 'Backend reports this period is not measurable yet'
        }
      />
      <DashboardMetric
        label="Scheduled"
        value={formatNumber(dashboard.scheduled)}
      />
      <DashboardMetric
        label="Inspected"
        value={formatNumber(dashboard.inspected)}
      />
      <DashboardMetric
        label="Completed on time"
        value={formatNumber(dashboard.completedOnTime)}
      />
      <DashboardMetric
        label="Completed late"
        value={formatNumber(dashboard.completedLate)}
      />
      {isClosed && (
        <DashboardMetric
          label="Not completed"
          value={formatNumber(dashboard.notCompleted)}
        />
      )}
      <DashboardMetric
        label="Remaining"
        value={formatNumber(dashboard.remaining)}
        note="Unfinished scheduled work"
      />
      {dashboard.inspectionResultsAvailable ? (
        <>
          <DashboardMetric
            label="Operational"
            value={formatNumber(dashboard.operational)}
          />
          <DashboardMetric
            label="Non-operational"
            value={formatNumber(dashboard.nonOperational)}
          />
        </>
      ) : (
        <Card role="status" className="content-card rounded-lg p-4">
          <p className="text-sm font-semibold text-[var(--text-primary)]">
            No completed inspection results yet
          </p>
          <p className="mt-2 text-xs text-[var(--text-secondary)]">
            Operational and non-operational counts are not meaningful for this
            period yet.
          </p>
        </Card>
      )}
    </div>
  )
}

function DeferredEnrollmentDashboardSummary() {
  const currentUser = useCurrentUser()
  const canReview = currentUser.data?.roles.includes('GSD') ?? false
  const deferrals = useScheduleEnrollmentDeferrals(1, canReview, {
    pageSize: 1,
  })

  if (!canReview) return null
  if (deferrals.isPending) {
    return (
      <p className="text-sm text-[var(--text-neutral)]" role="status">
        Checking deferred enrollment…
      </p>
    )
  }
  if (deferrals.isError) {
    return (
      <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
        <p className="font-semibold text-[var(--error)]">
          Deferred enrollment totals are unavailable.
        </p>
        <Button
          type="button"
          className="mt-3"
          onClick={() => void deferrals.refetch()}
        >
          Retry deferred totals
        </Button>
      </Card>
    )
  }

  const pendingCount = Number(deferrals.data.pendingCount)
  const reviewedCount = Number(deferrals.data.reviewedCount)
  if (pendingCount + reviewedCount === 0) return null

  return (
    <Card
      aria-labelledby="dashboard-deferred-enrollment-title"
      className="content-card flex flex-wrap items-center justify-between gap-3 rounded-lg p-3"
    >
      <div className="min-w-0 flex-1">
        <h2
          id="dashboard-deferred-enrollment-title"
          className="text-sm font-semibold text-[var(--text-primary)]"
        >
          Deferred enrollment: {pendingCount} need review · {reviewedCount}{' '}
          reviewed
        </h2>
        <p className="mt-1 text-xs text-[var(--text-secondary)]">
          These cycles have no schedule, are not completed maintenance, and stay
          outside scheduled and compliance totals.
        </p>
      </div>
      <Button asChild variant="secondary" className="h-9 min-h-0 px-3">
        <Link to="/app/schedules" hash="schedule-enrollment-deferrals">
          Review deferred cycles
        </Link>
      </Button>
    </Card>
  )
}

export function PmPeriodDashboardPresentation({
  dashboard,
  showBatch = true,
  search,
}: {
  dashboard: PmPeriodDashboardResponse
  showBatch?: boolean
  search?: PmPeriodDashboardSearch
}) {
  return (
    <>
      <DashboardMetrics dashboard={dashboard} />
      <PeriodStateSummary dashboard={dashboard} />
      {showBatch && (
        <BatchOverview
          batches={dashboard.batches}
          periodState={dashboard.periodState}
          dashboardSearch={search}
        />
      )}
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
  const [text, setText] = useState(search.search ?? '')

  // Route changes reset the submitted search draft to its URL-backed value.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setText(search.search ?? ''), [search.search])

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
        ...(search.condition ? { condition: search.condition } : {}),
        ...(search.timeliness ? { timeliness: search.timeliness } : {}),
        ...(search.search ? { search: search.search } : {}),
      }
    : undefined
  const scopeDashboardQuery = usePmPeriodDashboard(reportScopeFilters)
  const dashboardQuery = usePmPeriodDashboard(dashboardFilters)
  const showGeneratedReport = hasValidGeneratedScope && !isChangingSelection
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
        <DashboardHeader canAskNla={canAskNla} />
        <DeferredEnrollmentDashboardSummary />
        <CyclesLoading />
      </section>
    )
  }

  if (cyclesQuery.isError) {
    return (
      <section aria-labelledby="dashboard-title" className="space-y-4">
        <DashboardHeader canAskNla={canAskNla} />
        <DeferredEnrollmentDashboardSummary />
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
        <DashboardHeader canAskNla={canAskNla} />
        <Card className="p-6 shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No PM periods are scheduled
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Schedule a PM period to make it available for report generation.
          </p>
        </Card>
        <DeferredEnrollmentDashboardSummary />
      </section>
    )
  }

  return (
    <section aria-labelledby="dashboard-title" className="space-y-4">
      <DashboardHeader canAskNla={canAskNla} />

      {showGeneratedReport ? (
        <>
          <div className="pm-dashboard-report space-y-4">
            <section aria-labelledby="pm-dashboard-reporting-period">
              <Card className="content-card p-3 sm:p-4">
                <div className="flex flex-wrap items-center justify-between gap-x-5 gap-y-3">
                  <div>
                    <p className="text-xs font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
                      Reporting period
                    </p>
                    <h2
                      id="pm-dashboard-reporting-period"
                      className="mt-1 text-base font-semibold text-[var(--text-primary)]"
                    >
                      {formatAssetCategory(search.assetCategory ?? '')} ·{' '}
                      {search.pmCycle
                        ? formatPmCycle(search.pmCycle)
                        : 'Not selected'}
                    </h2>
                    <p className="mt-1 text-xs text-[var(--text-secondary)]">
                      PM year {search.year ?? 'Not selected'} · Due
                      (Asia/Manila){' '}
                      {dashboardQuery.isPending
                        ? 'Loading...'
                        : dashboardQuery.data
                          ? formatPmCycleDueDate(dashboardQuery.data.pmCycle)
                          : 'Unavailable'}
                    </p>
                    {generatedCycle && (
                      <p className="mt-1 text-xs text-[var(--text-secondary)]">
                        {formatNumber(generatedCycle.scheduled)} scheduled
                        assets
                      </p>
                    )}
                  </div>
                  <div className="flex flex-wrap gap-2 print:hidden">
                    <Button
                      type="button"
                      disabled={!dashboardQuery.data}
                      onClick={() => window.print()}
                    >
                      Export dashboard
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
              </Card>
            </section>

            {dashboardQuery.isPending ? (
              <>
                <DeferredEnrollmentDashboardSummary />
                <DashboardLoading />
              </>
            ) : dashboardQuery.isError ? (
              <>
                <DeferredEnrollmentDashboardSummary />
                <QueryError
                  message="The selected PM period could not be loaded."
                  onRetry={() => void dashboardQuery.refetch()}
                />
              </>
            ) : dashboardQuery.data ? (
              <>
                <section
                  aria-labelledby="pm-dashboard-period-status"
                  className="space-y-2"
                >
                  <div>
                    <h2
                      id="pm-dashboard-period-status"
                      className="text-xl font-bold text-[var(--text-primary)]"
                    >
                      Period overview
                    </h2>
                    <p className="mt-1 text-sm text-[var(--text-secondary)]">
                      Progress and compliance lead the report. Period state and
                      the remaining backend-reported measures follow.
                    </p>
                  </div>
                  <PmPeriodDashboardPresentation
                    dashboard={dashboardQuery.data}
                    showBatch={false}
                    search={search}
                  />
                </section>

                <DeferredEnrollmentDashboardSummary />

                <section aria-labelledby="pm-dashboard-detailed-filters">
                  <Card className="content-card p-4 sm:p-5">
                    <div className="grid gap-4 border-b border-[var(--border-soft)] pb-4 md:grid-cols-[minmax(0,18rem)_1fr] md:items-end">
                      <div>
                        <Label htmlFor="pm-dashboard-department">
                          Department (report scope)
                        </Label>
                        <select
                          id="pm-dashboard-department"
                          value={search.department ?? ''}
                          onChange={(event) =>
                            onSearchChange({
                              ...search,
                              department: event.target.value || undefined,
                            })
                          }
                          disabled={scopeDashboardQuery.isPending}
                          className={`${selectClassName} mt-2`}
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
                      <p className="text-xs text-[var(--text-secondary)]">
                        Department is part of the report scope and updates
                        summary totals. The row-level filters below narrow
                        records only.
                      </p>
                    </div>
                    <div>
                      <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
                        Row-level filters
                      </p>
                      <h2
                        id="pm-dashboard-detailed-filters"
                        className="mt-1 text-xl font-semibold text-[var(--text-primary)]"
                      >
                        Detailed filters
                      </h2>
                      <p className="mt-1 text-sm text-[var(--text-secondary)]">
                        Narrow the scheduled asset records without changing the
                        report-scope totals above.
                      </p>
                    </div>
                    <form
                      className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-3"
                      onSubmit={(event) => {
                        event.preventDefault()
                        onSearchChange({
                          ...search,
                          search: text.trim() || undefined,
                        })
                      }}
                    >
                      <div>
                        <Label htmlFor="pm-dashboard-condition">
                          Condition
                        </Label>
                        <select
                          id="pm-dashboard-condition"
                          value={search.condition ?? ''}
                          onChange={(event) =>
                            onSearchChange({
                              ...search,
                              condition: (event.target.value ||
                                undefined) as PmPeriodDashboardSearch['condition'],
                            })
                          }
                          className={`${selectClassName} mt-2`}
                        >
                          {conditionOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <Label htmlFor="pm-dashboard-timeliness">
                          Timeliness / status
                        </Label>
                        <select
                          id="pm-dashboard-timeliness"
                          value={search.timeliness ?? ''}
                          onChange={(event) =>
                            onSearchChange({
                              ...search,
                              timeliness: (event.target.value ||
                                undefined) as PmPeriodDashboardSearch['timeliness'],
                            })
                          }
                          className={`${selectClassName} mt-2`}
                        >
                          {timelinessOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <Label htmlFor="pm-dashboard-search">
                          Search assets
                        </Label>
                        <Input
                          id="pm-dashboard-search"
                          value={text}
                          onChange={(event) => setText(event.target.value)}
                          placeholder="Asset, building, location, or inspection"
                          className="mt-2"
                        />
                      </div>
                      <div className="flex flex-wrap gap-2 md:col-span-2 xl:col-span-3">
                        <Button type="submit">Apply search</Button>
                        <Button
                          type="button"
                          variant="secondary"
                          onClick={() => {
                            setText('')
                            onSearchChange({
                              ...search,
                              condition: undefined,
                              timeliness: undefined,
                              search: undefined,
                            })
                          }}
                        >
                          Clear filters
                        </Button>
                      </div>
                    </form>
                  </Card>
                </section>

                {(search.condition || search.timeliness || search.search) && (
                  <Card className="hidden p-4 shadow-none sm:p-5 print:block">
                    <h3 className="text-sm font-semibold text-[var(--text-primary)]">
                      Asset-list filters
                    </h3>
                    <dl className="mt-3 grid gap-3 text-sm sm:grid-cols-3">
                      {search.condition && (
                        <div>
                          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                            Condition
                          </dt>
                          <dd className="mt-1 font-medium text-[var(--text-primary)]">
                            {formatCondition(search.condition)}
                          </dd>
                        </div>
                      )}
                      {search.timeliness && (
                        <div>
                          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                            Timeliness / status
                          </dt>
                          <dd className="mt-1 font-medium text-[var(--text-primary)]">
                            {formatTimeliness(search.timeliness)}
                          </dd>
                        </div>
                      )}
                      {search.search && (
                        <div>
                          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                            Search
                          </dt>
                          <dd className="mt-1 font-medium whitespace-pre-wrap text-[var(--text-primary)]">
                            {search.search}
                          </dd>
                        </div>
                      )}
                    </dl>
                    <p className="mt-3 text-xs text-[var(--text-secondary)]">
                      Active filters narrow asset rows only. Metric totals use
                      the selected asset category and PM cycle, plus Department
                      when selected.
                    </p>
                  </Card>
                )}

                {dashboardQuery.data.assets.length > 0 && (
                  <BatchOverview
                    batches={dashboardQuery.data.batches}
                    periodState={dashboardQuery.data.periodState}
                    dashboardSearch={search}
                  />
                )}
                {dashboardQuery.data.assets.length === 0 ? (
                  <Card className="p-6 shadow-none">
                    <h2 className="text-lg font-semibold text-[var(--text-primary)]">
                      No scheduled assets match
                    </h2>
                    <p className="mt-1 text-sm text-[var(--text-secondary)]">
                      {search.department
                        ? `No scheduled assets match the selected filters for ${search.department}.`
                        : 'No scheduled assets match the selected report filters.'}
                    </p>
                  </Card>
                ) : (
                  <AssetRows
                    assets={dashboardQuery.data.assets}
                    dashboardSearch={search}
                  />
                )}
              </>
            ) : null}
          </div>
        </>
      ) : (
        <>
          <DeferredEnrollmentDashboardSummary />
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
                          {formatPmCycleMonth(cycle.pmCycle)} —{' '}
                          {formatNumber(cycle.scheduled)} scheduled
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

export function DashboardHeader({ canAskNla }: { canAskNla: boolean }) {
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
            <Link to="/app/analytics">Ask a question</Link>
          </Button>
        ) : undefined
      }
    />
  )
}
