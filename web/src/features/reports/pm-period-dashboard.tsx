import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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

export type PmPeriodDashboardSearch = {
  assetCategory?: string | undefined
  year?: number | undefined
  pmCycle?: string | undefined
  department?: string | undefined
  condition?: string | undefined
  timeliness?: string | undefined
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

function formatCategory(value: string) {
  return value
    .split('-')
    .map((part) => `${part.slice(0, 1).toUpperCase()}${part.slice(1)}`)
    .join(' ')
}

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

function formatPmCycle(value: string) {
  const [yearText, monthText] = value.split('-')
  const year = Number(yearText)
  const month = Number(monthText)
  if (!Number.isInteger(year) || !Number.isInteger(month)) return value

  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, 1)))
}

function formatNumber(value: number | string) {
  return typeof value === 'number' ? value.toLocaleString() : value
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

function latestGroup(groups: PmPeriodDashboardCycleGroupResponse[]) {
  return [...groups].sort(
    (left, right) =>
      Number(right.year) - Number(left.year) ||
      (latestCycle(right) ?? '').localeCompare(latestCycle(left) ?? '') ||
      left.assetCategory.localeCompare(right.assetCategory),
  )[0]
}

function latestCycle(group: PmPeriodDashboardCycleGroupResponse | undefined) {
  return [...(group?.cycles ?? [])].sort((left, right) =>
    right.pmCycle.localeCompare(left.pmCycle),
  )[0]?.pmCycle
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
}: {
  batches: PmPeriodDashboardBatchResponse[]
  periodState: string
}) {
  const isClosed = periodState === 'Closed'

  return (
    <Card className="p-4 shadow-none sm:p-5">
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
                'On time',
                'Late',
                isClosed ? 'Not completed' : 'Remaining',
                'Form / acknowledgement',
                'Action',
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
                <td className="px-3 py-3">
                  {batch.formId && batch.formStatus === 'Submitted' ? (
                    <Link
                      to="/app/preventive-maintenance-forms/$formId/review"
                      params={{ formId: batch.formId }}
                      search={{
                        assetCategory: batch.assetCategory,
                        pmCycle: batch.pmCycle,
                        ...(batch.department
                          ? { department: batch.department }
                          : {}),
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
                        readonly: batch.formStatus === 'Acknowledged',
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
}: {
  assets: PmPeriodDashboardAssetRowResponse[]
}) {
  return (
    <Card className="p-4 shadow-none sm:p-5">
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
                'Cycle / schedule',
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
                    className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                  >
                    {asset.assetCode}
                  </Link>
                  <p className="mt-1 text-xs text-[var(--text-neutral)]">
                    {formatCategory(asset.assetCategory)}
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
                  <p>Scheduled {formatDate(asset.scheduleDate)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    {asset.scheduleStatus}
                  </p>
                </td>
                <td className="space-y-1 px-3 py-3 text-[var(--text-secondary)]">
                  <p>{formatExecutionStatus(asset.executionStatus)}</p>
                  <p className="text-xs text-[var(--text-neutral)]">
                    {asset.isInspected
                      ? formatDate(asset.inspectionCompletedAt, true)
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
                    {asset.formId ? `Form ${asset.formId}` : 'No form linked'}
                    {' · '}
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
      ? 'This period is before its month-end deadline. Unfinished rows are marked Scheduled, and on-time compliance is not measurable yet.'
      : dashboard.periodState === 'Active'
        ? 'This period is in progress. Unfinished rows are marked Pending, and on-time compliance is not measurable yet.'
        : dashboard.periodState === 'Closed'
          ? 'This period is closed. Completed rows retain their final on-time or late result; unfinished rows are marked Not completed.'
          : 'The backend returned an unrecognized period state. Review the row-level status values for this period.'

  return (
    <Card role="status" className="p-4 shadow-none sm:p-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Period state
          </p>
          <p className="mt-1 text-lg font-semibold text-[var(--text-primary)]">
            {state}
          </p>
        </div>
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
      </div>
      <p className="mt-3 text-sm text-[var(--text-secondary)]">{message}</p>
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
        note="Backend-reported unfinished scheduled work"
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
        <Card role="status" className="p-4 shadow-none">
          <p className="text-sm font-semibold text-[var(--text-primary)]">
            No completed inspection results yet
          </p>
          <p className="mt-2 text-xs text-[var(--text-secondary)]">
            Operational and non-operational counts are not meaningful for this
            period yet.
          </p>
        </Card>
      )}
      <DashboardMetric
        label="Progress"
        value={formatPercent(dashboard.progressPercent)}
        note="Backend-reported inspection progress"
      />
      <DashboardMetric
        label="On-time compliance"
        value={
          dashboard.complianceMeasurable
            ? formatPercent(dashboard.onTimeCompliancePercent)
            : 'Not measurable yet'
        }
        note={
          dashboard.complianceMeasurable
            ? 'Backend-reported result'
            : 'Backend reports this period is not measurable yet'
        }
      />
    </div>
  )
}

export function PmPeriodDashboardPresentation({
  dashboard,
  showBatch = true,
}: {
  dashboard: PmPeriodDashboardResponse
  showBatch?: boolean
}) {
  return (
    <>
      <PeriodStateSummary dashboard={dashboard} />
      <DashboardMetrics dashboard={dashboard} />
      {showBatch && (
        <BatchOverview
          batches={dashboard.batches}
          periodState={dashboard.periodState}
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
  const defaultGroup = useMemo(() => latestGroup(cycles), [cycles])
  const categoryOptions = useMemo(
    () => [...new Set(cycles.map((group) => group.assetCategory))].sort(),
    [cycles],
  )
  const selectedCategory = categoryOptions.includes(search.assetCategory ?? '')
    ? search.assetCategory
    : defaultGroup?.assetCategory
  const yearOptions = useMemo(
    () =>
      cycles
        .filter((group) => group.assetCategory === selectedCategory)
        .map((group) => Number(group.year))
        .sort((left, right) => right - left),
    [cycles, selectedCategory],
  )
  const selectedYear = yearOptions.includes(search.year ?? Number.NaN)
    ? search.year
    : yearOptions[0]
  const selectedGroup = groupForSelection(
    cycles,
    selectedCategory,
    selectedYear,
  )
  const selectedCycle = selectedGroup?.cycles.some(
    (cycle) => cycle.pmCycle === search.pmCycle,
  )
    ? search.pmCycle
    : latestCycle(selectedGroup)

  const [department, setDepartment] = useState(search.department ?? '')
  const [text, setText] = useState(search.search ?? '')

  // Route changes reset the submitted filter drafts to the URL-backed values.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setDepartment(search.department ?? ''), [search.department])
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setText(search.search ?? ''), [search.search])

  useEffect(() => {
    if (
      !cyclesQuery.isSuccess ||
      !selectedCategory ||
      selectedYear === undefined ||
      !selectedCycle
    ) {
      return
    }

    if (
      search.assetCategory === selectedCategory &&
      search.year === selectedYear &&
      search.pmCycle === selectedCycle
    ) {
      return
    }

    onSearchChange(
      {
        ...search,
        assetCategory: selectedCategory,
        year: selectedYear,
        pmCycle: selectedCycle,
      },
      { replace: true },
    )
  }, [
    cyclesQuery.isSuccess,
    onSearchChange,
    search,
    selectedCategory,
    selectedCycle,
    selectedYear,
  ])

  const dashboardFilters =
    selectedCategory && selectedCycle
      ? {
          pmCycle: selectedCycle,
          assetCategory: selectedCategory,
          ...(search.department ? { department: search.department } : {}),
          ...(search.condition ? { condition: search.condition } : {}),
          ...(search.timeliness ? { timeliness: search.timeliness } : {}),
          ...(search.search ? { search: search.search } : {}),
        }
      : undefined
  const dashboardQuery = usePmPeriodDashboard(dashboardFilters)

  const departmentOptions = useMemo(
    () =>
      [
        ...(dashboardQuery.data?.batches.map((batch) => batch.department) ??
          []),
        ...(dashboardQuery.data?.assets.map((asset) => asset.department) ?? []),
      ]
        .filter((value): value is string => Boolean(value))
        .filter((value, index, values) => values.indexOf(value) === index)
        .sort(),
    [dashboardQuery.data],
  )

  const handleCategoryChange = (assetCategory: string) => {
    const nextGroup = latestGroup(
      cycles.filter((group) => group.assetCategory === assetCategory),
    )
    onSearchChange({
      ...search,
      assetCategory,
      year: nextGroup ? Number(nextGroup.year) : undefined,
      pmCycle: latestCycle(nextGroup),
    })
  }

  const handleYearChange = (year: string) => {
    const nextYear = Number(year)
    const nextGroup = groupForSelection(cycles, selectedCategory, nextYear)
    onSearchChange({
      ...search,
      year: nextGroup ? Number(nextGroup.year) : undefined,
      pmCycle: latestCycle(nextGroup),
    })
  }

  if (cyclesQuery.isPending) {
    return (
      <section
        aria-labelledby="dashboard-title"
        className="max-w-7xl space-y-5"
      >
        <DashboardHeader />
        <CyclesLoading />
      </section>
    )
  }

  if (cyclesQuery.isError) {
    return (
      <section
        aria-labelledby="dashboard-title"
        className="max-w-7xl space-y-5"
      >
        <DashboardHeader />
        <QueryError
          message="PM period options could not be loaded."
          onRetry={() => void cyclesQuery.refetch()}
        />
      </section>
    )
  }

  if (cycles.length === 0) {
    return (
      <section
        aria-labelledby="dashboard-title"
        className="max-w-7xl space-y-5"
      >
        <DashboardHeader />
        <Card className="p-6 shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No PM periods are scheduled
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            The dashboard will show a category, year, and cycle once the system
            has scheduled PM work.
          </p>
        </Card>
      </section>
    )
  }

  return (
    <section aria-labelledby="dashboard-title" className="max-w-7xl space-y-5">
      <DashboardHeader />

      <Card className="p-4 shadow-none sm:p-5">
        <div className="grid gap-4 md:grid-cols-3">
          <div>
            <Label htmlFor="pm-dashboard-category">Category</Label>
            <select
              id="pm-dashboard-category"
              value={selectedCategory ?? ''}
              onChange={(event) => handleCategoryChange(event.target.value)}
              className={`${selectClassName} mt-2`}
            >
              {categoryOptions.map((category) => (
                <option key={category} value={category}>
                  {formatCategory(category)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <Label htmlFor="pm-dashboard-year">Year</Label>
            <select
              id="pm-dashboard-year"
              value={selectedYear ?? ''}
              onChange={(event) => handleYearChange(event.target.value)}
              className={`${selectClassName} mt-2`}
            >
              {yearOptions.map((year) => (
                <option key={year} value={year}>
                  {year}
                </option>
              ))}
            </select>
          </div>
          <div>
            <Label htmlFor="pm-dashboard-cycle">PM period</Label>
            <select
              id="pm-dashboard-cycle"
              value={selectedCycle ?? ''}
              onChange={(event) =>
                onSearchChange({ ...search, pmCycle: event.target.value })
              }
              className={`${selectClassName} mt-2`}
            >
              {(selectedGroup?.cycles ?? []).map((cycle) => (
                <option key={cycle.pmCycle} value={cycle.pmCycle}>
                  {formatPmCycle(cycle.pmCycle)} ·{' '}
                  {formatNumber(cycle.scheduled)} scheduled
                </option>
              ))}
            </select>
          </div>
        </div>
        {selectedCycle && (
          <p className="mt-4 text-sm text-[var(--text-secondary)]">
            <span className="font-semibold text-[var(--text-primary)]">
              Month-end deadline (Asia/Manila):
            </span>{' '}
            {dashboardQuery.isPending
              ? 'Loading...'
              : dashboardQuery.data
                ? formatDate(dashboardQuery.data.deadline, false, 'Asia/Manila')
                : 'Unavailable'}
          </p>
        )}
      </Card>

      <Card className="p-4 shadow-none sm:p-5">
        <form
          className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"
          onSubmit={(event) => {
            event.preventDefault()
            onSearchChange({
              ...search,
              department: department.trim() || undefined,
              search: text.trim() || undefined,
            })
          }}
        >
          <div>
            <Label htmlFor="pm-dashboard-department">Department</Label>
            <Input
              id="pm-dashboard-department"
              list="pm-dashboard-departments"
              value={department}
              onChange={(event) => setDepartment(event.target.value)}
              placeholder="All departments"
              className="mt-2"
            />
            <datalist id="pm-dashboard-departments">
              {departmentOptions.map((value) => (
                <option key={value} value={value} />
              ))}
            </datalist>
          </div>
          <div>
            <Label htmlFor="pm-dashboard-condition">Condition</Label>
            <select
              id="pm-dashboard-condition"
              value={search.condition ?? ''}
              onChange={(event) =>
                onSearchChange({
                  ...search,
                  condition: event.target.value || undefined,
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
            <Label htmlFor="pm-dashboard-timeliness">Timeliness / status</Label>
            <select
              id="pm-dashboard-timeliness"
              value={search.timeliness ?? ''}
              onChange={(event) =>
                onSearchChange({
                  ...search,
                  timeliness: event.target.value || undefined,
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
            <Label htmlFor="pm-dashboard-search">Search</Label>
            <Input
              id="pm-dashboard-search"
              value={text}
              onChange={(event) => setText(event.target.value)}
              placeholder="Asset, building, location, or inspection"
              className="mt-2"
            />
          </div>
          <div className="flex gap-2 md:col-span-2 xl:col-span-4">
            <Button type="submit">Apply filters</Button>
            <Button
              type="button"
              className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
              onClick={() => {
                setDepartment('')
                setText('')
                onSearchChange({
                  ...search,
                  department: undefined,
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

      {dashboardQuery.isPending ? (
        <DashboardLoading />
      ) : dashboardQuery.isError ? (
        <QueryError
          message="The selected PM period could not be loaded."
          onRetry={() => void dashboardQuery.refetch()}
        />
      ) : dashboardQuery.data ? (
        <>
          <PmPeriodDashboardPresentation
            dashboard={dashboardQuery.data}
            showBatch={dashboardQuery.data.assets.length > 0}
          />
          {dashboardQuery.data.assets.length === 0 ? (
            <Card className="p-6 shadow-none">
              <h2 className="text-lg font-semibold text-[var(--text-primary)]">
                No scheduled assets match
              </h2>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                Adjust the server-backed filters to view scheduled assets for
                this PM period.
              </p>
            </Card>
          ) : (
            <AssetRows assets={dashboardQuery.data.assets} />
          )}
        </>
      ) : null}
    </section>
  )
}

function DashboardHeader() {
  return (
    <div>
      <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
        PM period dashboard
      </p>
      <h1
        id="dashboard-title"
        className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)] sm:text-4xl"
      >
        Preventive maintenance compliance
      </h1>
      <p className="mt-2 max-w-3xl text-[var(--text-secondary)]">
        Review scheduled work, inspection completion, operational condition, and
        department batch acknowledgement from the backend read model.
      </p>
      <p className="mt-2 max-w-4xl text-sm text-[var(--text-secondary)]">
        Official metrics use Category + PM period + optional Department.
        Condition, timeliness/status, and search filters affect only the asset
        table; Department recalculates the official metrics and the table.
      </p>
    </div>
  )
}
