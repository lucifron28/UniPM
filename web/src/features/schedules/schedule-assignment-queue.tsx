import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  categoryLabel,
  formatAssetCategory,
} from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'
import { ScheduleBatchAssignment } from '@/features/schedules/schedule-batch-assignment'
import {
  type Schedule,
  type ScheduleAssignmentQueueBatch,
} from '@/features/schedules/schedule-contract'
import {
  useScheduleAssignmentBatches,
  useScheduleRegistryOptions,
} from '@/features/schedules/schedule-queries'
import {
  formatPmCycle,
  formatPmCycleDueDate,
  getCurrentManilaYear,
} from '@/features/schedules/schedule-presentation'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'

type ScheduleBatch = ScheduleAssignmentQueueBatch & {
  id: string
  representative: Schedule
}

export function ScheduleAssignmentQueue({
  role,
  search,
  onSearchChange,
}: {
  role: 'GSD' | 'Supervisor'
  search: ScheduleSearch
  onSearchChange: (next: ScheduleSearch) => void
}) {
  const currentUser = useCurrentUser()
  const currentYear = getCurrentManilaYear()
  const selectedYear = search.year ?? currentYear
  const [selectedBatchId, setSelectedBatchId] = useState<string | null>(null)
  const onlyNeedsSupervisor =
    role === 'GSD' && search.assignmentFilter === 'needs-supervisor'
  const onlyNeedsInspector =
    role === 'Supervisor' && search.assignmentFilter === 'needs-inspector'
  const options = useScheduleRegistryOptions({
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
  })
  const scheduleBatches = useScheduleAssignmentBatches({
    year: selectedYear,
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
  })
  const categories = options.data?.categories ?? []
  const selectedCategory = categories.find(
    (category) => category.code === search.assetCategory,
  )
  const availableMonths = selectedCategory
    ? selectedCategory.scheduledMonths
    : [
        ...new Set(categories.flatMap((category) => category.scheduledMonths)),
      ].sort((left, right) => left - right)
  const pmCycleOptions = availableMonths.map(
    (month) => `${selectedYear}-${String(month).padStart(2, '0')}`,
  )
  const departments = useMemo(
    () => options.data?.departments ?? [],
    [options.data?.departments],
  )

  useEffect(() => {
    if (!search.pmCycle || !selectedCategory || options.isPending) return
    const month = Number(search.pmCycle.slice(5, 7))
    if (selectedCategory.scheduledMonths.includes(month)) return
    onSearchChange({ ...search, pmCycle: undefined, page: undefined })
  }, [onSearchChange, options.isPending, search, selectedCategory])

  useEffect(() => {
    if (!search.department || !options.isSuccess || options.isFetching) return
    if (departments.includes(search.department)) return
    onSearchChange({ ...search, department: undefined, page: undefined })
  }, [
    departments,
    onSearchChange,
    options.isFetching,
    options.isSuccess,
    search,
  ])

  const batches: ScheduleBatch[] = (scheduleBatches.data?.batches ?? []).map(
    (batch) => ({
      ...batch,
      id: batch.batchKey,
      representative: batch.representativeSchedule,
    }),
  )
  const visibleBatches = batches
    .filter(
      (batch) =>
        role !== 'GSD' ||
        !onlyNeedsSupervisor ||
        batch.supervisorState !== 'Assigned',
    )
    .filter(
      (batch) =>
        !onlyNeedsInspector ||
        role !== 'Supervisor' ||
        batch.inspectorState !== 'Assigned',
    )
    .sort((left, right) => {
      if (role === 'Supervisor') {
        const leftNeedsInspector = left.inspectorState !== 'Assigned'
        const rightNeedsInspector = right.inspectorState !== 'Assigned'
        if (leftNeedsInspector !== rightNeedsInspector) {
          return leftNeedsInspector ? -1 : 1
        }
      }
      return (
        left.pmCycle.localeCompare(right.pmCycle) ||
        left.department.localeCompare(right.department) ||
        left.assetCategory.localeCompare(right.assetCategory)
      )
    })

  const updateFilters = (patch: Partial<ScheduleSearch>) =>
    onSearchChange({
      ...search,
      ...patch,
      view: 'assignments',
      page: undefined,
    })

  return (
    <section
      aria-labelledby="schedule-assignment-queue-title"
      className="space-y-4"
    >
      <div>
        <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
          Batch assignment
        </p>
        <h1
          id="schedule-assignment-queue-title"
          className="mt-1 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
        >
          {role === 'GSD' ? 'PM schedule batches' : 'My assigned batches'}
        </h1>
        <p className="mt-2 max-w-3xl text-[var(--text-secondary)]">
          {role === 'GSD'
            ? 'Review current-year batches by department, asset category, and PM month. Assign a Supervisor to an eligible batch.'
            : 'Review your batches and assign an Inspector to a batch that has not started inspection work.'}
        </p>
      </div>

      <Card className="content-card space-y-3 p-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            Year
            <select
              aria-label="Assignment year"
              value={String(selectedYear)}
              onChange={(event) => {
                const year = Number(event.target.value)
                updateFilters({ year, pmCycle: undefined })
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              {[
                ...new Set([
                  ...(options.data?.years ?? []).map(Number),
                  currentYear,
                  selectedYear,
                ]),
              ]
                .sort((left, right) => right - left)
                .map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            Asset category
            <select
              aria-label="Assignment asset category"
              value={search.assetCategory ?? ''}
              disabled={options.isPending || options.isError}
              onChange={(event) =>
                updateFilters({
                  assetCategory: (event.target.value ||
                    undefined) as ScheduleSearch['assetCategory'],
                  department: undefined,
                  pmCycle: undefined,
                })
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              <option value="">All categories</option>
              {categories.map((category) => (
                <option key={category.code} value={category.code}>
                  {categoryLabel(category, category.code)}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            PM month
            <select
              aria-label="Assignment PM month"
              value={search.pmCycle ?? ''}
              onChange={(event) =>
                updateFilters({ pmCycle: event.target.value || undefined })
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              <option value="">All scheduled months</option>
              {pmCycleOptions.map((pmCycle) => (
                <option key={pmCycle} value={pmCycle}>
                  {formatPmCycle(pmCycle)}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            Department
            <select
              aria-label="Assignment department"
              value={search.department ?? ''}
              disabled={options.isPending || options.isError}
              onChange={(event) =>
                updateFilters({ department: event.target.value || undefined })
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              <option value="">All departments</option>
              {departments.map((department) => (
                <option key={department} value={department}>
                  {department}
                </option>
              ))}
            </select>
          </label>
        </div>
        {role === 'GSD' && (
          <label className="inline-flex min-h-9 items-center gap-2 text-sm text-[var(--text-primary)]">
            <input
              type="checkbox"
              checked={onlyNeedsSupervisor}
              onChange={(event) =>
                updateFilters({
                  assignmentFilter: event.target.checked
                    ? 'needs-supervisor'
                    : undefined,
                })
              }
            />
            Only show batches needing a Supervisor
          </label>
        )}
        {role === 'Supervisor' && (
          <label className="inline-flex min-h-9 items-center gap-2 text-sm text-[var(--text-primary)]">
            <input
              type="checkbox"
              checked={onlyNeedsInspector}
              onChange={(event) =>
                updateFilters({
                  assignmentFilter: event.target.checked
                    ? 'needs-inspector'
                    : undefined,
                })
              }
            />
            Only show batches needing an Inspector
          </label>
        )}
      </Card>

      {options.isError && (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="text-sm font-semibold text-[var(--error)]">
            Assignment filters could not be loaded.
          </p>
          <Button
            type="button"
            variant="secondary"
            className="mt-3"
            onClick={() => void options.refetch()}
          >
            Retry filters
          </Button>
        </Card>
      )}

      {scheduleBatches.isPending ? (
        <Card role="status" className="p-4 shadow-none">
          {role === 'GSD'
            ? onlyNeedsSupervisor
              ? 'Loading batches needing a Supervisor…'
              : 'Loading schedule batches…'
            : onlyNeedsInspector
              ? 'Loading batches needing an Inspector…'
              : 'Loading your assigned batches…'}
        </Card>
      ) : scheduleBatches.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="text-sm font-semibold text-[var(--error)]">
            Schedule batches could not be loaded.
          </p>
          <Button
            type="button"
            variant="secondary"
            className="mt-3"
            disabled={scheduleBatches.isFetching}
            onClick={() => void scheduleBatches.refetch()}
          >
            Retry batches
          </Button>
        </Card>
      ) : visibleBatches.length === 0 ? (
        <Card className="p-5 shadow-none">
          <h2 className="font-semibold">No matching batches</h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            {role === 'GSD'
              ? 'No batches need a Supervisor for the selected filters.'
              : 'No batches assigned to you match the selected filters.'}
          </p>
        </Card>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-[var(--text-secondary)]">
            {visibleBatches.length} batch
            {visibleBatches.length === 1 ? '' : 'es'} ·{' '}
            {scheduleBatches.data?.totalScheduleCount ?? 0} schedules in view
          </p>
          {visibleBatches.map((batch) => {
            const isSelected = selectedBatchId === batch.id
            const needsInspector = batch.inspectorState !== 'Assigned'
            const canAssign =
              !batch.assignmentLocked &&
              (role === 'GSD' ||
                (batch.supervisorOwnershipIsConsistent &&
                  batch.supervisorUserId === currentUser.data?.id))
            const supervisorName =
              batch.supervisorState === 'Assigned' && batch.supervisorUserId
                ? role === 'Supervisor' &&
                  batch.supervisorUserId === currentUser.data?.id
                  ? currentUser.data.displayName
                  : (batch.supervisorDisplayName ?? 'Assigned Supervisor')
                : batch.supervisorState
            const inspectorName =
              batch.inspectorState === 'Assigned' && batch.inspectorUserId
                ? (batch.inspectorDisplayName ?? 'Assigned Inspector')
                : batch.inspectorState

            return (
              <Card key={batch.id} className="space-y-3 p-4 shadow-none">
                <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="font-semibold text-[var(--text-primary)]">
                        {batch.department} ·{' '}
                        {formatAssetCategory(batch.assetCategory)}
                      </h2>
                      {role === 'Supervisor' && (
                        <Badge variant={needsInspector ? 'warning' : 'success'}>
                          {needsInspector
                            ? 'Needs Inspector'
                            : 'Inspector assigned'}
                        </Badge>
                      )}
                    </div>
                    <p className="mt-1 text-sm text-[var(--text-secondary)]">
                      {formatPmCycle(batch.pmCycle)} · Due{' '}
                      {formatPmCycleDueDate(batch.pmCycle)}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant={isSelected ? 'secondary' : 'primary'}
                      disabled={!canAssign && !isSelected}
                      onClick={() =>
                        setSelectedBatchId(isSelected ? null : batch.id)
                      }
                    >
                      {isSelected
                        ? 'Close assignment'
                        : role === 'GSD'
                          ? 'Assign Supervisor'
                          : 'Assign Inspector'}
                    </Button>
                    <Button asChild variant="secondary">
                      <Link
                        to="/app/schedules"
                        search={{
                          view: 'history',
                          year: selectedYear,
                          department: batch.department,
                          assetCategory: batch.assetCategory,
                          pmCycle: batch.pmCycle,
                          page: 1,
                        }}
                      >
                        View schedules in batch
                      </Link>
                    </Button>
                  </div>
                </div>

                <dl className="grid gap-3 text-sm sm:grid-cols-2 xl:grid-cols-4">
                  <div>
                    <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                      Schedules completed
                    </dt>
                    <dd className="mt-0.5 font-medium">
                      {batch.completedCount} / {batch.scheduleCount}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                      Ongoing · overdue
                    </dt>
                    <dd className="mt-0.5 font-medium">
                      {batch.ongoingCount} · {batch.overdueCount}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                      Supervisor
                    </dt>
                    <dd className="mt-0.5 font-medium">{supervisorName}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                      Inspector
                    </dt>
                    <dd className="mt-0.5 font-medium">{inspectorName}</dd>
                  </div>
                </dl>

                {batch.assignmentLocked && (
                  <p
                    role="status"
                    className="text-sm text-[var(--text-secondary)]"
                  >
                    Assignment is locked because inspection work has started or
                    a schedule is completed or cancelled.
                  </p>
                )}
                {role === 'Supervisor' &&
                  !batch.supervisorOwnershipIsConsistent && (
                    <p
                      role="status"
                      className="text-sm text-[var(--text-secondary)]"
                    >
                      This batch has inconsistent Supervisor assignments. Ask
                      GSD to review it.
                    </p>
                  )}

                {isSelected && canAssign && (
                  <ScheduleBatchAssignment
                    key={`${batch.id}-${batch.supervisorUserId ?? ''}-${batch.inspectorUserId ?? ''}`}
                    schedule={batch.representative}
                    canAssignSupervisor={role === 'GSD'}
                    canAssignWorker={role === 'Supervisor'}
                    hasSupervisorRole={role === 'Supervisor'}
                    supervisorAssignmentIsMixed={
                      batch.supervisorState === 'Partially assigned' ||
                      batch.supervisorState === 'Mixed'
                    }
                    inspectorAssignmentIsMixed={
                      batch.inspectorState === 'Partially assigned' ||
                      batch.inspectorState === 'Mixed'
                    }
                    knownBatchScheduleCount={batch.scheduleCount}
                  />
                )}
              </Card>
            )
          })}
        </div>
      )}
    </section>
  )
}
