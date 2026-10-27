import { Link, useCanGoBack, useRouter } from '@tanstack/react-router'
import { ArrowLeft } from 'lucide-react'
import { useMemo } from 'react'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import type {
  PmPeriodDashboardAssetRowResponse,
  PmPeriodDashboardResponse,
} from '@/api/generated/models'
import { usePmPeriodDashboard } from '@/features/reports/pm-period-dashboard-queries'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'
import type { PmPeriodDashboardSearch } from './pm-period-dashboard'
import './pm-period-dashboard-records-print.css'

export type PmPeriodDashboardRecordsSearch = PmPeriodDashboardSearch & {
  assignment?: 'assigned' | 'unassigned' | 'unavailable' | 'overdue' | undefined
  formStatus?: 'Draft' | 'Submitted' | 'Acknowledged' | 'none' | undefined
  inspectionStatus?: 'completed' | 'outstanding' | undefined
  assignmentTiming?:
    | 'WithinPeriod'
    | 'Overdue'
    | 'CompletedWithinPeriod'
    | 'CompletedLate'
    | 'Unavailable'
    | undefined
  page?: number | undefined
}

type AssignmentAwareAssetRow = PmPeriodDashboardAssetRowResponse & {
  assignedInspectorUserId?: string | null
}

type DashboardWithAssignmentCounts = PmPeriodDashboardResponse & {
  assignmentPeriod?:
    | (NonNullable<PmPeriodDashboardResponse['assignmentPeriod']> & {
        unassignedUnfinishedCount?: number | string
        unassignedUnavailableCount?: number | string
      })
    | null
}

type FilteredCounts = {
  completed: number
  outstanding: number
  nonOperational: number
  overdueAssignment: number | null
  unassigned: number | null
}

const pageSize = 25
const selectClassName =
  'min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 py-2 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]'

function formatNumber(value: number | string) {
  return Number(value).toLocaleString()
}

function formatPercent(value: number | string | null) {
  return value === null ? 'Not available' : `${value}%`
}

function formatDate(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return 'Not recorded'
  return new Intl.DateTimeFormat('en-PH', {
    dateStyle: 'medium',
    timeZone: 'Asia/Manila',
  }).format(date)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return 'Not recorded'
  return new Intl.DateTimeFormat('en-PH', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Manila',
  }).format(date)
}

function formatGeneratedAt(value: Date) {
  return new Intl.DateTimeFormat('en-PH', {
    dateStyle: 'long',
    timeStyle: 'short',
    timeZone: 'Asia/Manila',
  }).format(value)
}

function conditionLabel(value: string) {
  switch (value) {
    case 'Operational':
      return 'Operational'
    case 'NonOperational':
      return 'Non-operational'
    case 'NotInspected':
      return 'No inspection result'
    default:
      return 'Not recorded'
  }
}

function timelinessLabel(value: string) {
  switch (value) {
    case 'OnTime':
      return 'Completed within scheduled month'
    case 'Late':
      return 'Completed after scheduled month'
    case 'Scheduled':
      return 'Still within scheduled month'
    case 'Pending':
      return 'Awaiting completion'
    case 'NotCompleted':
      return 'Unfinished after scheduled month'
    default:
      return 'Not available'
  }
}

function assignmentLabel(row: AssignmentAwareAssetRow) {
  if (row.assignedInspectorUserId === null) return 'Unassigned'
  if (typeof row.assignedInspectorUserId === 'string') return 'Assigned'
  return 'Assignee unavailable'
}

function assignmentTimingLabel(row: AssignmentAwareAssetRow) {
  switch (row.assignmentPeriodStatus) {
    case 'WithinPeriod':
      return 'Within 30 days'
    case 'Overdue':
      return 'Overdue'
    case 'CompletedWithinPeriod':
      return 'Completed within 30 days'
    case 'CompletedLate':
      return 'Completed after 30 days'
    case 'Cancelled':
      return 'Cancelled'
    default:
      return 'Timing unavailable'
  }
}

function assignmentFilterMatches(
  row: AssignmentAwareAssetRow,
  filter: PmPeriodDashboardRecordsSearch['assignment'],
) {
  switch (filter) {
    case 'assigned':
      return typeof row.assignedInspectorUserId === 'string'
    case 'unassigned':
      return row.assignedInspectorUserId === null
    case 'unavailable':
      return row.assignedInspectorUserId === undefined
    case 'overdue':
      return row.assignmentPeriodStatus === 'Overdue'
    default:
      return true
  }
}

function matchesSearch(
  row: AssignmentAwareAssetRow,
  search: PmPeriodDashboardRecordsSearch,
) {
  if (
    search.assignmentTiming &&
    row.assignmentPeriodStatus !== search.assignmentTiming
  )
    return false
  if (search.inspectionStatus === 'completed' && !row.isInspected) return false
  if (search.inspectionStatus === 'outstanding' && row.isInspected) return false
  if (search.condition && row.condition !== search.condition) return false
  if (search.timeliness && row.timeliness !== search.timeliness) return false
  if (!assignmentFilterMatches(row, search.assignment)) return false
  if (search.formStatus === 'none' && row.formId !== null) return false
  if (search.formStatus && search.formStatus !== 'none') {
    if (row.formStatus !== search.formStatus) return false
  }

  const term = search.search?.trim().toLocaleLowerCase()
  if (!term) return true
  return [row.assetCode, row.department, row.building, row.location]
    .filter((value): value is string => Boolean(value))
    .some((value) => value.toLocaleLowerCase().includes(term))
}

function filteredCounts(rows: AssignmentAwareAssetRow[]): FilteredCounts {
  const hasAssigneeData = rows.every(
    (row) => row.assignedInspectorUserId !== undefined,
  )
  const hasTimingData = rows.every(
    (row) => row.assignmentPeriodStatus !== undefined,
  )
  return {
    completed: rows.filter((row) => row.isInspected).length,
    outstanding: rows.filter((row) => !row.isInspected).length,
    nonOperational: rows.filter((row) => row.condition === 'NonOperational')
      .length,
    overdueAssignment: hasTimingData
      ? rows.filter(
          (row) => !row.isInspected && row.assignmentPeriodStatus === 'Overdue',
        ).length
      : rows.length === 0
        ? 0
        : null,
    unassigned: hasAssigneeData
      ? rows.filter(
          (row) => !row.isInspected && row.assignedInspectorUserId === null,
        ).length
      : rows.length === 0
        ? 0
        : null,
  }
}

function scopeSearch(search: PmPeriodDashboardRecordsSearch) {
  return {
    assetCategory: search.assetCategory,
    year: search.year,
    pmCycle: search.pmCycle,
    department: search.department,
  }
}

function compareDepartment(left: string | null, right: string | null) {
  if (!left) return right ? 1 : 0
  if (!right) return -1
  return left.localeCompare(right)
}

function filterSummary(search: PmPeriodDashboardRecordsSearch) {
  const filters = [
    search.assignmentTiming
      ? `Assignment timing: ${assignmentTimingLabel({ assignmentPeriodStatus: search.assignmentTiming } as AssignmentAwareAssetRow)}`
      : undefined,
    search.search?.trim() ? `Search: ${search.search.trim()}` : undefined,
    search.inspectionStatus
      ? `Inspection: ${search.inspectionStatus}`
      : undefined,
    search.condition
      ? `Condition: ${conditionLabel(search.condition)}`
      : undefined,
    search.assignment
      ? `Inspector assignment: ${search.assignment === 'overdue' ? 'overdue timing' : search.assignment}`
      : undefined,
    search.formStatus
      ? `PM form: ${search.formStatus === 'none' ? 'none recorded' : search.formStatus}`
      : undefined,
    search.timeliness
      ? `Scheduled-month timing: ${timelinessLabel(search.timeliness)}`
      : undefined,
  ].filter((value): value is string => Boolean(value))
  return filters.length > 0 ? filters.join(' · ') : 'All records in scope'
}

function RecordTable({
  rows,
  search,
}: {
  rows: AssignmentAwareAssetRow[]
  search: PmPeriodDashboardRecordsSearch
}) {
  const returnContext = {
    kind: 'dashboard' as const,
    search: scopeSearch(search),
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-[var(--border-soft)]">
      <table className="pm-records-table w-full min-w-[800px] table-fixed text-left text-xs sm:text-sm">
        <caption className="sr-only">PM period asset records</caption>
        <colgroup>
          {['10%', '15%', '11%', '21%', '16%', '12%', '7%', '8%'].map(
            (width, index) => (
              <col key={index} style={{ width }} />
            ),
          )}
        </colgroup>
        <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
          <tr>
            {[
              'Asset',
              'Department / location',
              'Inspection / condition',
              'Inspector assignment',
              'Scheduled month',
              'Completed',
              'Form status',
              'Details',
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
          {rows.map((row) => (
            <tr
              key={row.scheduleId}
              className="border-b border-[var(--border-soft)] align-top last:border-0"
            >
              <th
                scope="row"
                className="px-3 py-3 font-semibold text-[var(--text-primary)]"
              >
                {row.assetCode}
              </th>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                <p>{row.department ?? 'Department not recorded'}</p>
                <p className="mt-1 text-xs">
                  {[row.building, row.location].filter(Boolean).join(' · ') ||
                    'Location not recorded'}
                </p>
              </td>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                <p>{row.isInspected ? 'Completed' : 'Outstanding'}</p>
                <p className="mt-1 text-xs">{conditionLabel(row.condition)}</p>
              </td>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                <p>{assignmentLabel(row)}</p>
                <p className="mt-1 text-xs">
                  First assigned: {formatDateTime(row.firstInspectorAssignedAt)}
                </p>
                <p className="mt-1 text-xs">
                  Assignment due: {formatDateTime(row.inspectorAssignmentDueAt)}
                </p>
                <p className="mt-1 text-xs">
                  Assignment timing: {assignmentTimingLabel(row)}
                </p>
              </td>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                <p>Due: {formatDate(row.deadline)}</p>
                <p className="mt-1 text-xs">
                  Timing: {timelinessLabel(row.timeliness)}
                </p>
              </td>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                {formatDateTime(row.inspectionCompletedAt)}
              </td>
              <td className="px-3 py-3 text-[var(--text-secondary)]">
                {row.formStatus ?? 'No form recorded'}
              </td>
              <td className="space-y-1 px-3 py-3">
                <Link
                  to="/app/schedules/$scheduleId"
                  params={{ scheduleId: row.scheduleId }}
                  search={{ assetId: row.assetId, returnContext }}
                  className="block font-semibold whitespace-nowrap text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                >
                  Schedule
                </Link>
                {row.inspectionId && (
                  <Link
                    to="/app/inspections/$inspectionId"
                    params={{ inspectionId: row.inspectionId }}
                    search={{
                      assetId: row.assetId,
                      scheduleId: row.scheduleId,
                      returnContext,
                    }}
                    className="block font-semibold whitespace-nowrap text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                  >
                    Inspection
                  </Link>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function SummaryMetrics({
  dashboard,
  counts,
  filteredCount,
}: {
  dashboard: DashboardWithAssignmentCounts
  counts: FilteredCounts
  filteredCount: number
}) {
  const outstanding =
    dashboard.periodState === 'Closed'
      ? dashboard.notCompleted
      : dashboard.remaining

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <Card className="space-y-3 p-4 shadow-none sm:p-5">
        <div>
          <h2 className="text-base font-semibold text-[var(--text-primary)]">
            Scope totals
          </h2>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">
            Full selected category, cycle, and department. Record filters do not
            change these totals.
          </p>
        </div>
        <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
          <Metric label="Scheduled" value={formatNumber(dashboard.scheduled)} />
          <Metric label="Completed" value={formatNumber(dashboard.inspected)} />
          <Metric label="Outstanding" value={formatNumber(outstanding)} />
          <Metric
            label="Within scheduled month"
            value={formatPercent(dashboard.onTimeCompliancePercent)}
          />
        </dl>
      </Card>

      <Card className="space-y-3 p-4 shadow-none sm:p-5">
        <div>
          <h2 className="text-base font-semibold text-[var(--text-primary)]">
            Filtered records
          </h2>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">
            Counts below use the rows that match the current filters.
          </p>
        </div>
        <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
          <Metric label="Matching rows" value={formatNumber(filteredCount)} />
          <Metric label="Completed" value={formatNumber(counts.completed)} />
          <Metric
            label="Outstanding"
            value={formatNumber(counts.outstanding)}
          />
          <Metric
            label="Overdue assignment"
            value={
              counts.overdueAssignment === null
                ? 'Not available'
                : formatNumber(counts.overdueAssignment)
            }
          />
          <Metric
            label="Unassigned"
            value={
              counts.unassigned === null
                ? 'Not available'
                : formatNumber(counts.unassigned)
            }
          />
          <Metric
            label="Non-operational"
            value={formatNumber(counts.nonOperational)}
          />
        </dl>
      </Card>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md bg-[var(--surface-muted)] px-3 py-2">
      <dt className="text-xs text-[var(--text-secondary)]">{label}</dt>
      <dd className="mt-1 font-semibold text-[var(--text-primary)] tabular-nums">
        {value}
      </dd>
    </div>
  )
}

function scopeLabel(search: PmPeriodDashboardRecordsSearch) {
  return [
    search.assetCategory
      ? formatAssetCategory(search.assetCategory)
      : 'Asset category not selected',
    search.pmCycle ? formatPmCycle(search.pmCycle) : 'PM cycle not selected',
    search.department ?? 'All departments',
  ].join(' · ')
}

function RecordsBackButton({
  search,
}: {
  search: PmPeriodDashboardRecordsSearch
}) {
  const router = useRouter()
  const canGoBack = useCanGoBack()
  return (
    <Button
      type="button"
      variant="secondary"
      className="min-h-12 gap-2 border-[var(--primary)] px-5 text-base text-[var(--primary)] print:hidden"
      onClick={() => {
        if (canGoBack) router.history.back()
        else
          void router.navigate({
            to: '/app/dashboard',
            search: scopeSearch(search),
          })
      }}
    >
      <ArrowLeft className="h-5 w-5" aria-hidden="true" />
      Back
    </Button>
  )
}

export function PmPeriodDashboardRecords({
  search,
  onSearchChange,
}: {
  search: PmPeriodDashboardRecordsSearch
  onSearchChange: (
    next: PmPeriodDashboardRecordsSearch,
    options?: { replace?: boolean },
  ) => void
}) {
  const hasScope = Boolean(search.assetCategory && search.pmCycle)
  const apiFilters = hasScope
    ? {
        assetCategory: search.assetCategory!,
        pmCycle: search.pmCycle!,
        ...(search.department ? { department: search.department } : {}),
      }
    : undefined
  const dashboardQuery = usePmPeriodDashboard(apiFilters)
  const dashboard = dashboardQuery.data as
    DashboardWithAssignmentCounts | undefined
  const rows = useMemo(
    () =>
      ([...(dashboard?.assets ?? [])] as AssignmentAwareAssetRow[]).sort(
        (left, right) =>
          compareDepartment(left.department, right.department) ||
          left.assetCode.localeCompare(right.assetCode) ||
          left.scheduleId.localeCompare(right.scheduleId),
      ),
    [dashboard?.assets],
  )
  const filteredRows = useMemo(
    () => rows.filter((row) => matchesSearch(row, search)),
    [rows, search],
  )
  const counts = useMemo(() => filteredCounts(filteredRows), [filteredRows])
  const pageCount = Math.max(1, Math.ceil(filteredRows.length / pageSize))
  const currentPage = Math.min(Math.max(search.page ?? 1, 1), pageCount)
  const pageRows = filteredRows.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize,
  )
  const generatedAt = useMemo(() => new Date(), [dashboardQuery.dataUpdatedAt])

  const updateSearch = (next: PmPeriodDashboardRecordsSearch) =>
    onSearchChange({ ...next, page: undefined }, { replace: true })

  if (!hasScope) {
    return (
      <section className="space-y-4">
        <RecordsBackButton search={search} />
        <PageHeading
          id="pm-dashboard-records-title"
          eyebrow="PM records"
          title="Choose a reporting period"
          description="Select a category, year, and scheduled month on the dashboard first."
        />
      </section>
    )
  }

  if (dashboardQuery.isPending) {
    return (
      <section
        className="space-y-4"
        aria-labelledby="pm-dashboard-records-title"
      >
        <RecordsBackButton search={search} />
        <PageHeading
          id="pm-dashboard-records-title"
          eyebrow="PM records"
          title="Loading period records"
          description={scopeLabel(search)}
        />
        <Card className="p-5 text-sm text-[var(--text-secondary)] shadow-none">
          Loading scheduled and inspection records…
        </Card>
      </section>
    )
  }

  if (dashboardQuery.isError || !dashboard) {
    return (
      <section
        className="space-y-4"
        aria-labelledby="pm-dashboard-records-title"
      >
        <RecordsBackButton search={search} />
        <PageHeading
          id="pm-dashboard-records-title"
          eyebrow="PM records"
          title="Records could not be loaded"
          description={scopeLabel(search)}
        />
        <Card className="p-5 shadow-none">
          <p className="text-sm text-[var(--text-secondary)]">
            The selected PM period records could not be loaded.
          </p>
          <Button
            className="mt-3"
            type="button"
            variant="secondary"
            onClick={() => void dashboardQuery.refetch()}
          >
            Retry
          </Button>
        </Card>
      </section>
    )
  }

  const scope = scopeSearch(search)
  const scopeOutstanding =
    dashboard.periodState === 'Closed'
      ? dashboard.notCompleted
      : dashboard.remaining
  const recordsSummary = `${filteredRows.length.toLocaleString()} matching ${filteredRows.length === 1 ? 'record' : 'records'}`

  return (
    <div className="pm-records-page">
      <section
        className="pm-records-screen space-y-4"
        aria-labelledby="pm-dashboard-records-title"
      >
        <RecordsBackButton search={search} />
        <PageHeading
          id="pm-dashboard-records-title"
          eyebrow="PM records"
          title="Period records"
          description={scopeLabel(search)}
          actions={
            <div className="flex flex-wrap gap-2">
              <Button type="button" onClick={() => window.print()}>
                Print detailed report
              </Button>
            </div>
          }
        />

        <SummaryMetrics
          dashboard={dashboard}
          counts={counts}
          filteredCount={filteredRows.length}
        />

        <Card className="space-y-4 p-4 shadow-none sm:p-5">
          <div>
            <h2 className="text-base font-semibold text-[var(--text-primary)]">
              Filter records
            </h2>
            <p className="mt-1 text-xs text-[var(--text-secondary)]">
              These filters apply in this page. Scheduled-month timing and
              inspector assignment timing are shown separately.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="xl:col-span-2">
              <Label htmlFor="pm-record-search">Asset or location</Label>
              <input
                id="pm-record-search"
                type="search"
                maxLength={256}
                value={search.search ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    search: event.target.value || undefined,
                  })
                }
                placeholder="Search code, department, building, or location"
                className={`${selectClassName} mt-1`}
              />
            </div>
            <div>
              <Label htmlFor="pm-record-inspection-status">Inspection</Label>
              <select
                id="pm-record-inspection-status"
                value={search.inspectionStatus ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    inspectionStatus:
                      (event.target
                        .value as PmPeriodDashboardRecordsSearch['inspectionStatus']) ||
                      undefined,
                  })
                }
                className={`${selectClassName} mt-1`}
              >
                <option value="">All statuses</option>
                <option value="completed">Completed</option>
                <option value="outstanding">Outstanding</option>
              </select>
            </div>
            <div>
              <Label htmlFor="pm-record-condition">Condition</Label>
              <select
                id="pm-record-condition"
                value={search.condition ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    condition:
                      (event.target
                        .value as PmPeriodDashboardSearch['condition']) ||
                      undefined,
                  })
                }
                className={`${selectClassName} mt-1`}
              >
                <option value="">All conditions</option>
                <option value="Operational">Operational</option>
                <option value="NonOperational">Non-operational</option>
                <option value="NotInspected">No inspection result</option>
              </select>
            </div>
            <div>
              <Label htmlFor="pm-record-assignment">Inspector assignment</Label>
              <select
                id="pm-record-assignment"
                value={search.assignment ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    assignment:
                      (event.target
                        .value as PmPeriodDashboardRecordsSearch['assignment']) ||
                      undefined,
                  })
                }
                className={`${selectClassName} mt-1`}
              >
                <option value="">All assignment states</option>
                <option value="assigned">Assigned</option>
                <option value="unassigned">Unassigned</option>
                <option value="unavailable">Assignee unavailable</option>
                <option value="overdue">Overdue assignment timing</option>
              </select>
            </div>
            <div>
              <Label htmlFor="pm-record-form-status">PM form</Label>
              <select
                id="pm-record-form-status"
                value={search.formStatus ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    formStatus:
                      (event.target
                        .value as PmPeriodDashboardRecordsSearch['formStatus']) ||
                      undefined,
                  })
                }
                className={`${selectClassName} mt-1`}
              >
                <option value="">All form statuses</option>
                <option value="none">No form recorded</option>
                <option value="Draft">Draft</option>
                <option value="Submitted">Submitted</option>
                <option value="Acknowledged">Acknowledged</option>
              </select>
            </div>
            <div>
              <Label htmlFor="pm-record-month-timing">Scheduled month</Label>
              <select
                id="pm-record-month-timing"
                value={search.timeliness ?? ''}
                onChange={(event) =>
                  updateSearch({
                    ...search,
                    timeliness:
                      (event.target
                        .value as PmPeriodDashboardSearch['timeliness']) ||
                      undefined,
                  })
                }
                className={`${selectClassName} mt-1`}
              >
                <option value="">All month timings</option>
                <option value="OnTime">Completed within month</option>
                <option value="Late">Completed after month</option>
                <option value="Scheduled">Still within month</option>
                <option value="Pending">Awaiting completion</option>
                <option value="NotCompleted">Unfinished after month</option>
              </select>
            </div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-[var(--text-secondary)]">
            <p>{recordsSummary} in this scope.</p>
            {(search.search ||
              search.condition ||
              search.timeliness ||
              search.assignment ||
              search.formStatus ||
              search.inspectionStatus ||
              search.assignmentTiming) && (
              <Button
                type="button"
                variant="secondary"
                onClick={() => updateSearch(scope)}
              >
                Clear filters
              </Button>
            )}
          </div>
        </Card>

        <Card className="p-4 shadow-none sm:p-5">
          <div className="mb-3 flex flex-wrap items-end justify-between gap-2">
            <div>
              <h2 className="text-base font-semibold text-[var(--text-primary)]">
                Matching schedule records
              </h2>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">
                Showing{' '}
                {filteredRows.length === 0
                  ? 0
                  : (currentPage - 1) * pageSize + 1}
                –{Math.min(currentPage * pageSize, filteredRows.length)} of{' '}
                {filteredRows.length.toLocaleString()} filtered rows. The table
                separates scheduled-month timing from inspector assignment
                timing.
              </p>
            </div>
            <p className="text-xs text-[var(--text-secondary)]">
              Scope total: {formatNumber(dashboard.scheduled)} scheduled,{' '}
              {formatNumber(dashboard.inspected)} completed,{' '}
              {formatNumber(scopeOutstanding)} outstanding
            </p>
          </div>
          {pageRows.length === 0 ? (
            <p
              role="status"
              className="rounded-md border border-[var(--border-soft)] px-3 py-4 text-sm text-[var(--text-secondary)]"
            >
              No schedule rows match these filters.
            </p>
          ) : (
            <RecordTable rows={pageRows} search={search} />
          )}
          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3">
            <p className="text-xs text-[var(--text-secondary)]">
              Page {currentPage} of {pageCount} · {pageSize} rows per page
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={currentPage <= 1}
                onClick={() =>
                  onSearchChange(
                    { ...search, page: currentPage - 1 },
                    { replace: true },
                  )
                }
              >
                Previous
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={currentPage >= pageCount}
                onClick={() =>
                  onSearchChange(
                    { ...search, page: currentPage + 1 },
                    { replace: true },
                  )
                }
              >
                Next
              </Button>
            </div>
          </div>
        </Card>
      </section>

      <section
        className="pm-records-print"
        aria-label="Printable PM records report"
      >
        <header className="pm-records-print__header">
          <p className="pm-records-print__eyebrow">
            UniPM · Preventive maintenance
          </p>
          <h1>PM period records report</h1>
          <p>{scopeLabel(search)}</p>
          <p>Generated {formatGeneratedAt(generatedAt)}</p>
          <p>Filters: {filterSummary(search)}</p>
        </header>
        <section
          className="pm-records-print__summary"
          aria-label="Report summary"
        >
          <h2>Scope totals</h2>
          <p>
            {formatNumber(dashboard.scheduled)} scheduled ·{' '}
            {formatNumber(dashboard.inspected)} completed ·{' '}
            {formatNumber(scopeOutstanding)} outstanding · completed within the
            scheduled month {formatPercent(dashboard.onTimeCompliancePercent)}
          </p>
          <h2>Filtered results</h2>
          <p>
            {filteredRows.length.toLocaleString()} matching rows ·{' '}
            {counts.completed.toLocaleString()} completed ·{' '}
            {counts.outstanding.toLocaleString()} outstanding ·{' '}
            {counts.overdueAssignment === null
              ? 'overdue assignment count unavailable'
              : `${counts.overdueAssignment.toLocaleString()} overdue assignments`}{' '}
            ·{' '}
            {counts.unassigned === null
              ? 'unassigned count unavailable'
              : `${counts.unassigned.toLocaleString()} unassigned`}{' '}
            · {counts.nonOperational.toLocaleString()} non-operational
          </p>
          <p className="pm-records-print__note">
            Scheduled-month timing uses the PM cycle deadline. Inspector
            assignment timing uses the separate 30-day assignment period.
          </p>
        </section>
        <div className="pm-records-print__table">
          {filteredRows.length === 0 ? (
            <p>No schedule rows match these filters.</p>
          ) : (
            <RecordTable rows={filteredRows} search={search} />
          )}
        </div>
      </section>
    </div>
  )
}
