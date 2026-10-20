import { useEffect, useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import {
  assignScheduleBatchSupervisor,
  assignScheduleBatchWorker,
  getListScheduleRegistryQueryKey,
  getListSchedulesQueryKey,
} from '@/api/generated/endpoints'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  SearchableOptionInput,
  type SearchableOption,
} from '@/components/ui/searchable-option-input'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import {
  categoryLabel,
  formatAssetCategory,
} from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  type Schedule,
  type ScheduleAssignmentQueueBatch,
} from '@/features/schedules/schedule-contract'
import {
  useScheduleAssignmentBatches,
  useScheduleRegistryOptions,
  useScheduleSupervisorAssignmentOptions,
  useScheduleWorkerAssignmentOptions,
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

type AssignmentResult = {
  attemptedBatchCount: number
  assignedBatchIds: string[]
  failedBatchIds: string[]
  assignedScheduleCount: number
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
  const [buildingFilter, setBuildingFilter] = useState('')
  const [selection, setSelection] = useState<{
    scopeKey: string
    batchIds: string[]
  }>({ scopeKey: '', batchIds: [] })
  const [assignmentDialogOpen, setAssignmentDialogOpen] = useState(false)
  const [assignmentDialogBatches, setAssignmentDialogBatches] = useState<
    ScheduleBatch[] | null
  >(null)
  const [assignmentNotice, setAssignmentNotice] = useState('')
  const assignmentScopeKey = [
    role,
    selectedYear,
    search.assetCategory ?? '',
    search.department ?? '',
    search.pmCycle ?? '',
    search.assignmentFilter ?? '',
    buildingFilter,
  ].join('|')
  const selectedBatchIds = new Set(
    selection.scopeKey === assignmentScopeKey ? selection.batchIds : [],
  )
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
  const assignmentsUnavailable =
    scheduleBatches.isPlaceholderData || scheduleBatches.isFetching
  const assignmentRefreshMessage = scheduleBatches.isPlaceholderData
    ? 'Filters changed. Previous batch results are shown while the new results load; assignment is temporarily unavailable.'
    : 'Schedule batches are refreshing; assignment is temporarily unavailable.'
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
  const eligibleBatches = batches
    .filter((batch) => !batch.assignmentLocked)
    .filter((batch) => batch.unfinishedScheduleCount > 0)
    .filter((batch) =>
      role === 'GSD'
        ? batch.supervisorState === 'Unassigned' &&
          batch.inspectorState === 'Unassigned'
        : batch.supervisorState === 'Assigned' &&
          batch.supervisorOwnershipIsConsistent &&
          batch.supervisorUserId === currentUser.data?.id &&
          batch.inspectorState === 'Unassigned',
    )
    .sort((left, right) => {
      return (
        left.pmCycle.localeCompare(right.pmCycle) ||
        left.department.localeCompare(right.department) ||
        left.assetCategory.localeCompare(right.assetCategory)
      )
    })
  const buildingNames = [
    ...new Set(eligibleBatches.flatMap((batch) => batch.buildings)),
  ].sort((left, right) => left.localeCompare(right))
  const buildingOptions = buildingNames.map((building) => ({
    value: building,
    label: building,
  }))
  const visibleBatches = eligibleBatches.filter(
    (batch) =>
      !buildingFilter ||
      batch.buildings.some(
        (building) =>
          building.toLocaleLowerCase() === buildingFilter.toLocaleLowerCase(),
      ),
  )
  const selectedBatches = visibleBatches.filter((batch) =>
    selectedBatchIds.has(batch.id),
  )
  const visibleScheduleCount = visibleBatches.reduce(
    (total, batch) => total + batch.unfinishedScheduleCount,
    0,
  )
  const selectedScheduleCount = selectedBatches.reduce(
    (total, batch) => total + batch.scheduleCount,
    0,
  )
  const allVisibleSelected =
    visibleBatches.length > 0 &&
    visibleBatches.every((batch) => selectedBatchIds.has(batch.id))

  const clearSelection = () =>
    setSelection({ scopeKey: assignmentScopeKey, batchIds: [] })

  const updateFilters = (patch: Partial<ScheduleSearch>) => {
    clearSelection()
    setBuildingFilter('')
    setAssignmentDialogOpen(false)
    setAssignmentDialogBatches(null)
    setAssignmentNotice('')
    onSearchChange({
      ...search,
      ...patch,
      view: 'assignments',
      page: undefined,
    })
  }

  const updateBuildingFilter = (building: string) => {
    setBuildingFilter(building)
    clearSelection()
    setAssignmentDialogOpen(false)
    setAssignmentDialogBatches(null)
    setAssignmentNotice('')
  }

  const toggleBatch = (batchId: string, checked: boolean) => {
    const next = new Set(selectedBatchIds)
    if (checked) next.add(batchId)
    else next.delete(batchId)
    setSelection({ scopeKey: assignmentScopeKey, batchIds: [...next] })
    setAssignmentNotice('')
  }

  const selectAllVisible = (checked: boolean) => {
    setSelection({
      scopeKey: assignmentScopeKey,
      batchIds: checked ? visibleBatches.map((batch) => batch.id) : [],
    })
    setAssignmentNotice('')
  }

  const openSingleBatchAssignment = (batchId: string) => {
    const batch = visibleBatches.find((item) => item.id === batchId)
    if (!batch) return
    setSelection({ scopeKey: assignmentScopeKey, batchIds: [batch.id] })
    setAssignmentDialogBatches([batch])
    setAssignmentNotice('')
    setAssignmentDialogOpen(true)
  }

  const handleAssignmentComplete = (result: AssignmentResult) => {
    setSelection({
      scopeKey: assignmentScopeKey,
      batchIds: result.failedBatchIds,
    })
    setAssignmentDialogOpen(false)
    setAssignmentDialogBatches(null)
    setAssignmentNotice(
      result.failedBatchIds.length === 0
        ? `${role === 'GSD' ? 'Supervisor' : 'Inspector'} assigned to ${result.assignedBatchIds.length} ${result.assignedBatchIds.length === 1 ? 'batch' : 'batches'} (${result.assignedScheduleCount} schedules).`
        : `${result.assignedBatchIds.length} of ${result.attemptedBatchCount} batches assigned. ${result.failedBatchIds.length} failed; review the refreshed queue.`,
    )
  }

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
          {role === 'GSD' ? 'Supervisor Assignments' : 'Inspector Assignments'}
        </h1>
        <p className="mt-2 max-w-3xl text-[var(--text-secondary)]">
          {role === 'GSD'
            ? 'Assign Supervisors to pending PM batches.'
            : 'Assign Inspectors to your pending PM batches.'}
        </p>
      </div>

      <Card className="content-card space-y-3 p-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
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
          <SearchablePicker
            id="assignment-building-filter"
            label="Building"
            value={buildingFilter || undefined}
            options={buildingOptions}
            allLabel="All buildings"
            placeholder="Search buildings"
            description="A matching building shows the complete PM batch."
            disabled={assignmentsUnavailable || scheduleBatches.isError}
            loading={scheduleBatches.isPending}
            onChange={(value) => updateBuildingFilter(value ?? '')}
          />
        </div>
      </Card>

      {assignmentNotice && (
        <Card role="status" className="p-3 text-sm shadow-none">
          {assignmentNotice}
        </Card>
      )}

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
            ? 'Loading batches ready for Supervisor assignment…'
            : 'Loading batches ready for Inspector assignment…'}
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
        <>
          {scheduleBatches.data && assignmentsUnavailable && (
            <Card role="status" className="p-3 text-sm shadow-none">
              {assignmentRefreshMessage}
            </Card>
          )}
          <Card className="p-5 shadow-none">
            <h2 className="font-semibold">
              {assignmentsUnavailable
                ? 'Updating schedule batches'
                : 'No matching batches'}
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {assignmentsUnavailable
                ? 'The selected batch results are updating. Assignment is temporarily unavailable.'
                : role === 'GSD'
                  ? 'No unassigned batches are eligible for a Supervisor in the selected filters.'
                  : 'No batches assigned to you are eligible for an Inspector in the selected filters.'}
            </p>
          </Card>
        </>
      ) : (
        <div className="space-y-3">
          {scheduleBatches.data && assignmentsUnavailable && (
            <Card role="status" className="p-3 text-sm shadow-none">
              {assignmentRefreshMessage}
            </Card>
          )}
          <div className="flex flex-col justify-between gap-3 rounded-lg border border-[var(--border-soft)] bg-white p-3 sm:flex-row sm:items-center">
            <div className="flex items-center gap-3">
              <input
                aria-label="Select all visible batches"
                type="checkbox"
                checked={allVisibleSelected}
                disabled={assignmentsUnavailable}
                onChange={(event) => selectAllVisible(event.target.checked)}
                className="size-4 accent-[var(--primary)]"
              />
              <p className="text-sm text-[var(--text-secondary)]">
                {visibleBatches.length} pending batch
                {visibleBatches.length === 1 ? '' : 'es'} ·{' '}
                {visibleScheduleCount} unfinished schedules
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={assignmentsUnavailable || visibleBatches.length === 0}
                onClick={() => selectAllVisible(!allVisibleSelected)}
              >
                {allVisibleSelected
                  ? 'Clear visible selection'
                  : 'Select all visible'}
              </Button>
              {selectedBatches.length > 0 && (
                <>
                  <span className="text-sm font-medium">
                    {selectedBatches.length} selected · {selectedScheduleCount}{' '}
                    schedules
                  </span>
                  <Button
                    type="button"
                    disabled={assignmentsUnavailable}
                    onClick={() => {
                      setAssignmentDialogBatches(selectedBatches)
                      setAssignmentDialogOpen(true)
                    }}
                  >
                    Assign {role === 'GSD' ? 'Supervisor' : 'Inspector'} to{' '}
                    {selectedBatches.length}{' '}
                    {selectedBatches.length === 1 ? 'batch' : 'batches'}
                  </Button>
                </>
              )}
            </div>
          </div>

          {visibleBatches.map((batch) => {
            const isSelected = selectedBatchIds.has(batch.id)
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
                  <div className="flex items-start gap-3">
                    <input
                      aria-label={`Select ${batch.department} ${formatAssetCategory(batch.assetCategory)} ${formatPmCycle(batch.pmCycle)} batch`}
                      type="checkbox"
                      checked={isSelected}
                      disabled={assignmentsUnavailable}
                      onChange={(event) =>
                        toggleBatch(batch.id, event.target.checked)
                      }
                      className="mt-1 size-4 shrink-0 accent-[var(--primary)]"
                    />
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="font-semibold text-[var(--text-primary)]">
                          {batch.department} ·{' '}
                          {formatAssetCategory(batch.assetCategory)}
                        </h2>
                        {role === 'Supervisor' && (
                          <Badge variant="warning">Needs Inspector</Badge>
                        )}
                      </div>
                      <p className="mt-1 text-sm text-[var(--text-secondary)]">
                        {formatPmCycle(batch.pmCycle)} · Due{' '}
                        {formatPmCycleDueDate(batch.pmCycle)}
                      </p>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2 pl-7 sm:pl-0">
                    <Button
                      type="button"
                      disabled={assignmentsUnavailable}
                      onClick={() => openSingleBatchAssignment(batch.id)}
                    >
                      Assign {role === 'GSD' ? 'Supervisor' : 'Inspector'}
                    </Button>
                    {assignmentsUnavailable ? (
                      <Button variant="secondary" disabled>
                        View schedules in batch
                      </Button>
                    ) : (
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
                    )}
                  </div>
                </div>

                <dl className="grid gap-3 text-sm sm:grid-cols-2 xl:grid-cols-5">
                  <div>
                    <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                      Buildings
                    </dt>
                    <dd className="mt-0.5 font-medium">
                      {batch.buildings.length > 0
                        ? batch.buildings.join(', ')
                        : 'Not recorded'}
                    </dd>
                  </div>
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
              </Card>
            )
          })}
        </div>
      )}

      {assignmentDialogOpen &&
        assignmentDialogBatches &&
        assignmentDialogBatches.length > 0 && (
          <ScheduleBatchAssignmentDialog
            key={`${assignmentScopeKey}-${assignmentDialogBatches.map((batch) => batch.id).join(',')}`}
            batches={assignmentDialogBatches}
            role={role}
            onClose={() => {
              setAssignmentDialogOpen(false)
              setAssignmentDialogBatches(null)
            }}
            onComplete={handleAssignmentComplete}
          />
        )}
    </section>
  )
}

function ScheduleBatchAssignmentDialog({
  batches,
  role,
  onClose,
  onComplete,
}: {
  batches: ScheduleBatch[]
  role: 'GSD' | 'Supervisor'
  onClose: () => void
  onComplete: (result: AssignmentResult) => void
}) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [selectedUserId, setSelectedUserId] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const supervisors = useScheduleSupervisorAssignmentOptions(
    role === 'GSD',
    search,
    selectedUserId || undefined,
  )
  const inspectors = useScheduleWorkerAssignmentOptions(
    role === 'Supervisor',
    search,
    selectedUserId || undefined,
  )
  const isGsd = role === 'GSD'
  const optionQuery = isGsd ? supervisors : inspectors
  const people = isGsd
    ? (supervisors.data?.supervisors ?? [])
    : (inspectors.data?.workers ?? [])
  const selectedPerson = isGsd
    ? supervisors.data?.selected
    : inspectors.data?.selected
  const peopleWithSelection =
    selectedPerson && !people.some((person) => person.id === selectedPerson.id)
      ? [selectedPerson, ...people]
      : people
  const nameCounts = new Map<string, number>()
  for (const person of peopleWithSelection) {
    nameCounts.set(
      person.displayName,
      (nameCounts.get(person.displayName) ?? 0) + 1,
    )
  }
  const personOptions: SearchableOption[] = peopleWithSelection.map(
    (person) => ({
      value: person.id,
      label:
        nameCounts.get(person.displayName) === 1
          ? person.displayName
          : `${person.displayName} · …${person.id.slice(-4)}`,
    }),
  )
  const selectedOption = personOptions.find(
    (option) => option.value === selectedUserId,
  )
  const totalScheduleCount = batches.reduce(
    (total, batch) => total + batch.scheduleCount,
    0,
  )
  const title = `Assign ${isGsd ? 'Supervisor' : 'Inspector'} to ${batches.length} ${batches.length === 1 ? 'batch' : 'batches'}`

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog || dialog.open) return

    dialog.showModal()
    return () => {
      if (dialog.open) dialog.close()
    }
  }, [])

  const handlePersonChange = (value: string) => {
    setSearch(value)
    const match = personOptions.find(
      (option) =>
        option.label.toLocaleLowerCase() === value.trim().toLocaleLowerCase(),
    )
    setSelectedUserId(match?.value ?? '')
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedUserId || !selectedOption || isSaving) return

    setIsSaving(true)
    const assignedBatchIds: string[] = []
    const failedBatchIds: string[] = []
    let assignedScheduleCount = 0
    try {
      for (const batch of batches) {
        try {
          const result = isGsd
            ? await assignScheduleBatchSupervisor(batch.representative.id, {
                supervisorUserId: selectedUserId,
              })
            : await assignScheduleBatchWorker(batch.representative.id, {
                workerUserId: selectedUserId,
              })
          assignedBatchIds.push(batch.id)
          assignedScheduleCount += result.scheduleIds.length
        } catch {
          failedBatchIds.push(batch.id)
        }
      }

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getListSchedulesQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getListScheduleRegistryQueryKey(),
        }),
      ])
      onComplete({
        attemptedBatchCount: batches.length,
        assignedBatchIds,
        failedBatchIds,
        assignedScheduleCount,
      })
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="schedule-batch-assignment-dialog-title"
      className="m-auto max-h-[min(90vh,52rem)] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto rounded-xl border border-[var(--border-soft)] bg-white p-0 text-[var(--text-primary)] shadow-2xl backdrop:bg-black/45"
      onCancel={(event) => {
        event.preventDefault()
        if (!isSaving) onClose()
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget && !isSaving) onClose()
      }}
    >
      <header className="flex items-start justify-between gap-4 border-b border-[var(--border-soft)] p-5">
        <div>
          <h2
            id="schedule-batch-assignment-dialog-title"
            className="text-xl font-bold"
          >
            {title}
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            {batches.length} canonical PM{' '}
            {batches.length === 1 ? 'batch' : 'batches'}
            {' · '}
            {totalScheduleCount} schedules in selected scope
          </p>
        </div>
        <Button
          autoFocus
          variant="secondary"
          disabled={isSaving}
          onClick={onClose}
        >
          Close
        </Button>
      </header>
      <form onSubmit={handleSubmit} className="space-y-4 p-5">
        <div>
          <SearchableOptionInput
            id="assignment-target-person"
            label={`Search active ${isGsd ? 'Supervisor' : 'Inspector'}`}
            value={search || selectedOption?.label || ''}
            options={personOptions}
            disabled={isSaving}
            placeholder={`Type a ${isGsd ? 'Supervisor' : 'Inspector'} name`}
            helperText={`Choose one active ${isGsd ? 'Supervisor' : 'Inspector'} for every selected batch.`}
            onChange={handlePersonChange}
          />
          {optionQuery.isPending ? (
            <p
              role="status"
              className="mt-2 text-xs text-[var(--text-secondary)]"
            >
              Loading eligible {isGsd ? 'Supervisors' : 'Inspectors'}…
            </p>
          ) : optionQuery.isError ? (
            <div
              role="alert"
              className="mt-2 flex items-center gap-2 text-sm text-[var(--error)]"
            >
              <span>Eligible accounts could not be loaded.</span>
              <button
                type="button"
                disabled={optionQuery.isFetching || isSaving}
                onClick={() => void optionQuery.refetch()}
                className="font-semibold underline underline-offset-2"
              >
                Retry
              </button>
            </div>
          ) : optionQuery.isFetching ? (
            <p
              role="status"
              className="mt-2 text-xs text-[var(--text-secondary)]"
            >
              Searching eligible accounts…
            </p>
          ) : people.length === 0 ? (
            <p
              role="status"
              className="mt-2 text-xs text-[var(--text-secondary)]"
            >
              No active matches. Search by name or ask an administrator to check
              the account.
            </p>
          ) : null}
        </div>

        <section aria-label="Selected batch scope" className="space-y-2">
          <div className="flex items-baseline justify-between gap-3">
            <h3 className="font-semibold">Review selected batches</h3>
            <p className="text-xs text-[var(--text-secondary)]">
              Assignment stays grouped by department, category, and PM cycle.
            </p>
          </div>
          <ul className="max-h-64 space-y-2 overflow-y-auto rounded-lg border border-[var(--border-soft)] p-3">
            {batches.map((batch) => (
              <li
                key={batch.id}
                className="border-b border-[var(--border-soft)] pb-2 last:border-0 last:pb-0"
              >
                <p className="text-sm font-medium">
                  {batch.department} ·{' '}
                  {formatAssetCategory(batch.assetCategory)} ·{' '}
                  {formatPmCycle(batch.pmCycle)}
                </p>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">
                  {batch.buildings.length > 1
                    ? `This batch spans ${batch.buildings.length} buildings. Assignment applies to all ${batch.scheduleCount} schedules.`
                    : `Assignment applies to all ${batch.scheduleCount} schedules in this batch.`}
                  {batch.buildings.length > 0 &&
                    ` Buildings: ${batch.buildings.join(', ')}.`}
                </p>
              </li>
            ))}
          </ul>
        </section>

        <div className="flex flex-wrap justify-end gap-2 border-t border-[var(--border-soft)] pt-4">
          <Button
            type="button"
            variant="secondary"
            disabled={isSaving}
            onClick={onClose}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            disabled={
              isSaving ||
              optionQuery.isFetching ||
              optionQuery.isError ||
              !selectedUserId ||
              !selectedOption
            }
          >
            {isSaving ? 'Assigning batches…' : title}
          </Button>
        </div>
      </form>
    </dialog>
  )
}
