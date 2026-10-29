import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import {
  categoryLabel,
  formatAssetCategory,
} from '@/features/assets/asset-presentation'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import {
  useScheduleAssignmentBatches,
  useInfiniteScheduleSupervisorAssignmentOptions,
  useInfiniteScheduleWorkerAssignmentOptions,
  useScheduleRegistry,
  useScheduleRegistryOptions,
} from '@/features/schedules/schedule-queries'
import {
  formatPmCycle,
  formatPmCycleDueDate,
  getCurrentManilaYear,
} from '@/features/schedules/schedule-presentation'
import type {
  Schedule,
  ScheduleAssignmentQueueBatch,
} from '@/features/schedules/schedule-contract'
import { getListScheduleRegistryQueryKey } from '@/api/generated/endpoints'

const BATCH_PAGE_SIZE = 10
const DETAIL_PAGE_SIZE = 10
type AssignmentRole = 'GSD' | 'Supervisor'
type AssigneeOption = { id: string; displayName: string }

export function ScheduleOngoingWork({
  search,
  role,
  onSearchChange,
}: {
  search: ScheduleSearch
  role: AssignmentRole
  onSearchChange: (next: ScheduleSearch) => void
}) {
  const currentYear = getCurrentManilaYear()
  const selectedYear = search.year ?? currentYear
  const page = search.page ?? 1
  const [moreFiltersOpen, setMoreFiltersOpen] = useState(false)
  const [openBatchSelection, setOpenBatchSelection] = useState<{
    batchKey: string
    routeKey: string
  } | null>(null)
  const searchInputRef = useRef<HTMLInputElement>(null)
  const openerRef = useRef<HTMLButtonElement | null>(null)
  const searchDebounceRef = useRef<number | null>(null)
  const currentRouteState = useRef({ search, onSearchChange })
  const queryClient = useQueryClient()
  const routeKey = JSON.stringify(search)
  const openBatchKey =
    openBatchSelection?.routeKey === routeKey
      ? openBatchSelection.batchKey
      : null
  const options = useScheduleRegistryOptions()
  const categories = useMemo(
    () => options.data?.categories ?? [],
    [options.data],
  )
  const departments = useMemo(
    () => options.data?.departments ?? [],
    [options.data],
  )
  const selectedCategory = categories.find(
    (category) => category.code === search.assetCategory,
  )
  const availableMonths = selectedCategory
    ? selectedCategory.scheduledMonths
    : [
        ...new Set(categories.flatMap((category) => category.scheduledMonths)),
      ].sort((left, right) => left - right)
  const cycleOptions = availableMonths.map((month) => {
    const value = `${selectedYear}-${String(month).padStart(2, '0')}`
    return { value, label: formatPmCycle(value) }
  })
  const batchesQuery = useScheduleAssignmentBatches({
    year: selectedYear,
    tracking: true,
    page,
    pageSize: BATCH_PAGE_SIZE,
    ...(search.search ? { search: search.search } : {}),
    ...(search.progress ? { progress: search.progress } : {}),
    ...(search.overdueOnly ? { overdueOnly: true } : {}),
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.building ? { building: search.building } : {}),
    ...(search.inspectorUserId
      ? { inspectorUserId: search.inspectorUserId }
      : {}),
    ...(role === 'GSD' && search.supervisorUserId
      ? { supervisorUserId: search.supervisorUserId }
      : {}),
  })
  const batchesUnavailable =
    batchesQuery.isPlaceholderData || batchesQuery.isFetching
  const batches = batchesQuery.data?.batches ?? []
  const openBatch = openBatchKey
    ? (batches.find((batch) => batch.batchKey === openBatchKey) ?? null)
    : null
  const buildingOptions = batchesQuery.data?.buildingOptions ?? []
  const years = [
    ...new Set([
      ...(options.data?.years ?? []).map(Number),
      currentYear,
      selectedYear,
    ]),
  ].sort((left, right) => right - left)
  const inspectorOptions = usePagedAssigneeOptions(
    'Inspector',
    true,
    search.inspectorUserId,
  )
  const supervisorOptions = usePagedAssigneeOptions(
    'Supervisor',
    role === 'GSD',
    search.supervisorUserId,
  )

  useEffect(() => {
    currentRouteState.current = { search, onSearchChange }
  }, [onSearchChange, search])

  useEffect(() => {
    if (searchInputRef.current) {
      searchInputRef.current.value = search.search ?? ''
    }
  }, [search.search])

  useEffect(
    () => () => {
      if (searchDebounceRef.current !== null) {
        window.clearTimeout(searchDebounceRef.current)
      }
    },
    [],
  )

  useEffect(() => {
    if (!search.pmCycle || options.isPending || options.isError) return
    const [cycleYear, cycleMonth] = search.pmCycle.split('-').map(Number)
    const eligibleMonths = selectedCategory
      ? selectedCategory.scheduledMonths
      : categories.flatMap((category) => category.scheduledMonths)
    if (
      cycleYear !== selectedYear ||
      cycleMonth === undefined ||
      !eligibleMonths.includes(cycleMonth)
    ) {
      currentRouteState.current.onSearchChange({
        ...currentRouteState.current.search,
        pmCycle: undefined,
        page: undefined,
        view: 'tracking',
      })
    }
  }, [
    categories,
    options.isError,
    options.isPending,
    search.pmCycle,
    selectedCategory,
    selectedYear,
  ])

  const closeBatch = useCallback(() => {
    setOpenBatchSelection(null)
    requestAnimationFrame(() => {
      if (openerRef.current?.isConnected) openerRef.current.focus()
      else searchInputRef.current?.focus()
      openerRef.current = null
    })
  }, [setOpenBatchSelection])

  useEffect(() => {
    if (!openBatch) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closeBatch()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [closeBatch, openBatch])

  const updateFilters = (patch: Partial<ScheduleSearch>) => {
    setOpenBatchSelection(null)
    onSearchChange({
      ...search,
      ...patch,
      year: selectedYear,
      page: undefined,
      view: 'tracking',
    })
  }

  const clearFilters = () => {
    if (searchDebounceRef.current !== null) {
      window.clearTimeout(searchDebounceRef.current)
      searchDebounceRef.current = null
    }
    if (searchInputRef.current) searchInputRef.current.value = ''
    setOpenBatchSelection(null)
    onSearchChange({ year: currentYear, view: 'tracking' })
  }

  const activeFilters = [
    ...(search.search
      ? [{ label: `Search: ${search.search}`, patch: { search: undefined } }]
      : []),
    ...(search.assetCategory
      ? [
          {
            label: `Category: ${formatAssetCategory(search.assetCategory)}`,
            patch: { assetCategory: undefined, pmCycle: undefined },
          },
        ]
      : []),
    ...(search.pmCycle
      ? [
          {
            label: `PM month: ${formatPmCycle(search.pmCycle)}`,
            patch: { pmCycle: undefined },
          },
        ]
      : []),
    ...(search.department
      ? [
          {
            label: `Department: ${search.department}`,
            patch: { department: undefined },
          },
        ]
      : []),
    ...(search.building
      ? [
          {
            label: `Building: ${search.building}`,
            patch: { building: undefined },
          },
        ]
      : []),
    ...(search.progress
      ? [
          {
            label: `Progress: ${search.progress === 'not-started' ? 'Not started' : 'In progress'}`,
            patch: { progress: undefined },
          },
        ]
      : []),
    ...(search.overdueOnly
      ? [{ label: 'Overdue only', patch: { overdueOnly: undefined } }]
      : []),
    ...(search.inspectorUserId
      ? [
          {
            label: `Inspector: ${inspectorOptions.selected?.displayName ?? 'Selected inspector'}`,
            patch: { inspectorUserId: undefined },
          },
        ]
      : []),
    ...(role === 'GSD' && search.supervisorUserId
      ? [
          {
            label: `Supervisor: ${supervisorOptions.selected?.displayName ?? 'Selected supervisor'}`,
            patch: { supervisorUserId: undefined },
          },
        ]
      : []),
  ]

  const pageCount = Math.max(
    1,
    Math.ceil((batchesQuery.data?.totalBatchCount ?? 0) / BATCH_PAGE_SIZE),
  )
  const summary = batchesQuery.data?.summary
  const scopeText = `${(batchesQuery.data?.totalBatchCount ?? 0).toLocaleString()} matching authorized batch${batchesQuery.data?.totalBatchCount === 1 ? '' : 'es'}`
  const updatedAt = batchesQuery.dataUpdatedAt
    ? new Date(batchesQuery.dataUpdatedAt).toLocaleTimeString([], {
        hour: 'numeric',
        minute: '2-digit',
      })
    : null

  return (
    <section aria-labelledby="schedule-tracking-title" className="space-y-4">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
        <div>
          <h2
            id="schedule-tracking-title"
            className="text-xl font-bold text-[var(--text-primary)]"
          >
            Work tracking
          </h2>
          <p className="mt-2 max-w-3xl text-sm text-[var(--text-secondary)]">
            Assigned work, saved inspection progress, and overdue schedules.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <p
            className="text-xs text-[var(--text-secondary)]"
            aria-live="polite"
          >
            {updatedAt
              ? `Latest saved progress · Updated at ${updatedAt}`
              : 'Loading saved progress'}
          </p>
          <Button
            type="button"
            variant="secondary"
            disabled={batchesQuery.isFetching}
            onClick={async () => {
              await Promise.all([
                batchesQuery.refetch(),
                queryClient.invalidateQueries({
                  queryKey: getListScheduleRegistryQueryKey(),
                }),
              ])
            }}
          >
            {batchesQuery.isFetching ? 'Refreshing…' : 'Refresh'}
          </Button>
        </div>
      </div>

      <details className="text-sm text-[var(--text-secondary)]">
        <summary className="w-fit cursor-pointer font-medium text-[var(--primary)]">
          About saved progress
        </summary>
        <p className="mt-2 max-w-3xl">
          Offline inspection changes appear after synchronization. Completed
          inspection rows may still belong to draft or submitted forms.
        </p>
      </details>

      <section aria-label="Filtered work summary" className="space-y-2">
        <p className="text-xs text-[var(--text-secondary)]">
          Summary across {scopeText}, before pagination.
        </p>
        {batchesQuery.isPending ? (
          <Card role="status" className="p-3 text-sm shadow-none">
            Loading work summary…
          </Card>
        ) : batchesQuery.isError ? (
          <Card role="alert" className="p-3 text-sm shadow-none">
            Work summary is unavailable until results load.
          </Card>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            <SummaryShortcut
              label="Batches not started"
              value={summary?.notStartedBatchCount ?? 0}
              active={search.progress === 'not-started'}
              onClick={() =>
                updateFilters({
                  progress:
                    search.progress === 'not-started'
                      ? undefined
                      : 'not-started',
                })
              }
            />
            <SummaryShortcut
              label="Batches in progress"
              value={summary?.inProgressBatchCount ?? 0}
              active={search.progress === 'in-progress'}
              onClick={() =>
                updateFilters({
                  progress:
                    search.progress === 'in-progress'
                      ? undefined
                      : 'in-progress',
                })
              }
            />
            <SummaryShortcut
              label="Schedules ongoing"
              value={summary?.ongoingScheduleCount ?? 0}
              active={search.progress === 'in-progress'}
              onClick={() =>
                updateFilters({
                  progress:
                    search.progress === 'in-progress'
                      ? undefined
                      : 'in-progress',
                })
              }
            />
            <SummaryShortcut
              label="Unfinished schedules overdue"
              value={summary?.overdueScheduleCount ?? 0}
              active={Boolean(search.overdueOnly)}
              onClick={() =>
                updateFilters({
                  overdueOnly: search.overdueOnly ? undefined : true,
                })
              }
            />
          </div>
        )}
      </section>

      <Card className="content-card space-y-3 p-4">
        <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
          Search assigned work
          <Input
            ref={searchInputRef}
            type="search"
            defaultValue={search.search ?? ''}
            placeholder="Department, building, asset code, inspector, supervisor, or PM cycle"
            onChange={(event) => {
              const value = event.currentTarget.value
              if (searchDebounceRef.current !== null) {
                window.clearTimeout(searchDebounceRef.current)
              }
              searchDebounceRef.current = window.setTimeout(() => {
                const current = currentRouteState.current
                const normalized = value.trim()
                if (normalized === (current.search.search ?? '')) return
                setOpenBatchSelection(null)
                current.onSearchChange({
                  ...current.search,
                  search: normalized || undefined,
                  page: undefined,
                  view: 'tracking',
                })
              }, 250)
            }}
          />
        </label>
        <div className="grid items-end gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            Year
            <select
              aria-label="Tracking year"
              value={String(selectedYear)}
              onChange={(event) =>
                updateFilters({
                  year: Number(event.target.value),
                  pmCycle: undefined,
                })
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              {years.map((year) => (
                <option key={year} value={year}>
                  {year}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
            Progress
            <select
              aria-label="Tracking progress"
              value={search.progress ?? ''}
              onChange={(event) =>
                updateFilters({
                  progress: (event.target.value ||
                    undefined) as ScheduleSearch['progress'],
                })
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
            >
              <option value="">All active</option>
              <option value="not-started">Not started</option>
              <option value="in-progress">In progress</option>
            </select>
          </label>
          <label className="flex min-h-10 items-center gap-2 text-sm font-medium text-[var(--text-primary)]">
            <input
              type="checkbox"
              checked={Boolean(search.overdueOnly)}
              onChange={(event) =>
                updateFilters({
                  overdueOnly: event.target.checked || undefined,
                })
              }
              className="size-4 accent-[var(--primary)]"
            />
            Overdue only
          </label>
          <Button
            type="button"
            variant="secondary"
            aria-expanded={moreFiltersOpen}
            onClick={() => setMoreFiltersOpen((open) => !open)}
          >
            {moreFiltersOpen ? 'Hide more filters' : 'More filters'}
          </Button>
        </div>
        {moreFiltersOpen && (
          <div className="grid gap-3 border-t border-[var(--border-soft)] pt-3 sm:grid-cols-2 xl:grid-cols-3">
            <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
              Asset category
              <select
                aria-label="Tracking category"
                value={search.assetCategory ?? ''}
                disabled={
                  batchesUnavailable || options.isPending || options.isError
                }
                onChange={(event) =>
                  updateFilters({
                    assetCategory: (event.target.value ||
                      undefined) as ScheduleSearch['assetCategory'],
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
                aria-label="Tracking PM month"
                value={search.pmCycle ?? ''}
                disabled={
                  batchesUnavailable || options.isPending || options.isError
                }
                onChange={(event) =>
                  updateFilters({ pmCycle: event.target.value || undefined })
                }
                className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
              >
                <option value="">All scheduled months</option>
                {cycleOptions.map((cycle) => (
                  <option key={cycle.value} value={cycle.value}>
                    {cycle.label}
                  </option>
                ))}
              </select>
            </label>
            <SearchablePicker
              id="tracking-department-filter"
              label="Department"
              value={search.department}
              options={departments.map((department) => ({
                value: department,
                label: department,
              }))}
              allLabel="All departments"
              placeholder="Search departments"
              disabled={batchesUnavailable}
              loading={options.isPending}
              errorMessage={
                options.isError ? 'Departments could not be loaded.' : undefined
              }
              onRetry={() => void options.refetch()}
              onChange={(value) => updateFilters({ department: value })}
            />
            <SearchablePicker
              id="tracking-building-filter"
              label="Building"
              value={search.building}
              options={buildingOptions.map((building) => ({
                value: building,
                label: building,
              }))}
              allLabel="All buildings"
              placeholder="Search buildings"
              description="A matching building returns the complete PM batch."
              disabled={batchesUnavailable}
              loading={batchesQuery.isPending}
              errorMessage={
                batchesQuery.isError
                  ? 'Building options could not be loaded.'
                  : undefined
              }
              onRetry={() => void batchesQuery.refetch()}
              onChange={(value) => updateFilters({ building: value })}
            />
            <SearchablePicker
              id="tracking-inspector-filter"
              label="Inspector"
              value={search.inspectorUserId}
              selectedOption={
                inspectorOptions.selected
                  ? {
                      value: inspectorOptions.selected.id,
                      label: inspectorOptions.selected.displayName,
                    }
                  : undefined
              }
              options={inspectorOptions.items.map((option) => ({
                value: option.id,
                label: option.displayName,
              }))}
              allLabel="All inspectors"
              placeholder="Search inspectors"
              disabled={batchesUnavailable}
              loading={inspectorOptions.loading}
              loadingMore={inspectorOptions.loadingMore}
              hasMore={inspectorOptions.hasMore}
              errorMessage={
                inspectorOptions.error
                  ? 'Inspectors could not be loaded.'
                  : undefined
              }
              onRetry={inspectorOptions.retry}
              onLoadMore={inspectorOptions.loadMore}
              onSearchChange={inspectorOptions.search}
              onChange={(value) => updateFilters({ inspectorUserId: value })}
            />
            {role === 'GSD' && (
              <SearchablePicker
                id="tracking-supervisor-filter"
                label="Supervisor"
                value={search.supervisorUserId}
                selectedOption={
                  supervisorOptions.selected
                    ? {
                        value: supervisorOptions.selected.id,
                        label: supervisorOptions.selected.displayName,
                      }
                    : undefined
                }
                options={supervisorOptions.items.map((option) => ({
                  value: option.id,
                  label: option.displayName,
                }))}
                allLabel="All supervisors"
                placeholder="Search supervisors"
                disabled={batchesUnavailable}
                loading={supervisorOptions.loading}
                loadingMore={supervisorOptions.loadingMore}
                hasMore={supervisorOptions.hasMore}
                errorMessage={
                  supervisorOptions.error
                    ? 'Supervisors could not be loaded.'
                    : undefined
                }
                onRetry={supervisorOptions.retry}
                onLoadMore={supervisorOptions.loadMore}
                onSearchChange={supervisorOptions.search}
                onChange={(value) => updateFilters({ supervisorUserId: value })}
              />
            )}
          </div>
        )}
        {options.isError && (
          <div
            role="alert"
            className="flex flex-wrap items-center gap-3 text-sm text-[var(--error)]"
          >
            <span>Schedule filter options could not be loaded.</span>
            <Button
              type="button"
              variant="secondary"
              onClick={() => void options.refetch()}
            >
              Retry options
            </Button>
          </div>
        )}
      </Card>

      {activeFilters.length > 0 && (
        <div
          className="flex flex-wrap items-center gap-2"
          aria-label="Active filters"
        >
          {activeFilters.map((filter) => (
            <Button
              key={filter.label}
              type="button"
              variant="secondary"
              className="min-h-8 px-2 py-1 text-xs"
              onClick={() => updateFilters(filter.patch)}
              aria-label={`Remove ${filter.label} filter`}
            >
              {filter.label} ×
            </Button>
          ))}
          <Button type="button" variant="secondary" onClick={clearFilters}>
            Clear filters
          </Button>
        </div>
      )}

      {batchesUnavailable && batchesQuery.data && (
        <Card role="status" className="p-3 text-sm shadow-none">
          Filters changed. Previous results remain visible while the matching
          batches load; opening details is paused.
        </Card>
      )}

      {batchesQuery.isPending ? (
        <Card role="status" className="p-4 text-sm shadow-none">
          Loading assigned PM batches…
        </Card>
      ) : batchesQuery.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="font-semibold">Progress could not be loaded.</p>
          <Button
            type="button"
            variant="secondary"
            className="mt-3"
            disabled={batchesQuery.isFetching}
            onClick={() => void batchesQuery.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : batches.length === 0 ? (
        <Card className="p-5 shadow-none">
          <h2 className="font-semibold">
            No active assigned batches match these filters.
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Completed work is available in Schedule history.
          </p>
          <Button
            type="button"
            variant="secondary"
            className="mt-3"
            onClick={clearFilters}
          >
            Clear filters
          </Button>
        </Card>
      ) : (
        <section aria-label="Assigned work results" className="space-y-3">
          <p
            className="text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            {batchesQuery.data.totalBatchCount.toLocaleString()} active assigned
            batches · page {page} of {pageCount}
          </p>
          <div className="hidden overflow-x-auto rounded-xl border border-[var(--border-soft)] lg:block">
            <table className="w-full min-w-[960px] text-left text-sm">
              <thead className="bg-[var(--primary)] text-xs tracking-wide text-white uppercase">
                <tr>
                  <th scope="col" className="px-4 py-3">
                    Batch
                  </th>
                  <th scope="col" className="px-4 py-3">
                    Building
                  </th>
                  <th scope="col" className="px-4 py-3">
                    Assigned personnel
                  </th>
                  <th scope="col" className="px-4 py-3">
                    Inspection progress
                  </th>
                  <th scope="col" className="px-4 py-3">
                    Attention
                  </th>
                  <th scope="col" className="px-4 py-3">
                    Action
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-soft)] bg-white">
                {batches.map((batch) => (
                  <tr key={batch.batchKey}>
                    <td className="px-4 py-3 align-top">
                      <p className="font-semibold text-[var(--text-primary)]">
                        {batch.department} ·{' '}
                        {formatAssetCategory(batch.assetCategory)}
                      </p>
                      <p className="mt-1 text-xs text-[var(--text-secondary)]">
                        {formatPmCycle(batch.pmCycle)} · Due{' '}
                        {formatPmCycleDueDate(batch.pmCycle)}
                      </p>
                    </td>
                    <td className="max-w-52 px-4 py-3 align-top text-[var(--text-secondary)]">
                      {formatBuildings(batch.buildings)}
                    </td>
                    <td className="max-w-64 px-4 py-3 align-top text-[var(--text-secondary)]">
                      <p>
                        Inspector:{' '}
                        {batch.inspectorDisplayName ??
                          assignmentLabel(batch.inspectorState)}
                      </p>
                      <p className="mt-1">
                        Supervisor:{' '}
                        {batch.supervisorDisplayName ??
                          assignmentLabel(batch.supervisorState)}
                      </p>
                    </td>
                    <td className="min-w-48 px-4 py-3 align-top">
                      <ProgressBar
                        completed={batch.completedCount}
                        total={batch.activeScheduleCount}
                        cancelled={batch.cancelledCount}
                      />
                    </td>
                    <td className="px-4 py-3 align-top">
                      <div className="flex flex-wrap gap-1.5">
                        {batch.ongoingCount > 0 && (
                          <Badge variant="warning">
                            {batch.ongoingCount} in progress
                          </Badge>
                        )}
                        {batch.overdueCount > 0 && (
                          <Badge variant="danger">
                            {batch.overdueCount} overdue
                          </Badge>
                        )}
                        {batch.ongoingCount === 0 &&
                          batch.overdueCount === 0 && (
                            <Badge variant="neutral">No attention flags</Badge>
                          )}
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">
                      <Button
                        type="button"
                        variant="secondary"
                        className="whitespace-nowrap"
                        disabled={batchesUnavailable}
                        onClick={(event) => {
                          openerRef.current = event.currentTarget
                          setOpenBatchSelection({
                            batchKey: batch.batchKey,
                            routeKey,
                          })
                        }}
                      >
                        View progress
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="grid gap-3 lg:hidden">
            {batches.map((batch) => (
              <Card
                key={batch.batchKey}
                className="content-card space-y-3 p-4 shadow-none"
              >
                <div>
                  <h2 className="font-semibold text-[var(--text-primary)]">
                    {batch.department} ·{' '}
                    {formatAssetCategory(batch.assetCategory)}
                  </h2>
                  <p className="mt-1 text-sm text-[var(--text-secondary)]">
                    {formatPmCycle(batch.pmCycle)} ·{' '}
                    {formatBuildings(batch.buildings)}
                  </p>
                </div>
                <ProgressBar
                  completed={batch.completedCount}
                  total={batch.activeScheduleCount}
                  cancelled={batch.cancelledCount}
                />
                <div className="space-y-1 text-sm text-[var(--text-secondary)]">
                  <p>
                    Inspector:{' '}
                    {batch.inspectorDisplayName ??
                      assignmentLabel(batch.inspectorState)}
                  </p>
                  <p>
                    Supervisor:{' '}
                    {batch.supervisorDisplayName ??
                      assignmentLabel(batch.supervisorState)}
                  </p>
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {batch.ongoingCount > 0 && (
                    <Badge variant="warning">
                      {batch.ongoingCount} in progress
                    </Badge>
                  )}
                  {batch.overdueCount > 0 && (
                    <Badge variant="danger">{batch.overdueCount} overdue</Badge>
                  )}
                </div>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={batchesUnavailable}
                  onClick={(event) => {
                    openerRef.current = event.currentTarget
                    setOpenBatchSelection({
                      batchKey: batch.batchKey,
                      routeKey,
                    })
                  }}
                >
                  View progress
                </Button>
              </Card>
            ))}
          </div>

          <div className="flex items-center justify-between gap-3">
            <Button
              type="button"
              variant="secondary"
              disabled={page <= 1 || batchesUnavailable}
              onClick={() => updateFilters({ page: page - 1 })}
            >
              Previous
            </Button>
            <span className="text-sm text-[var(--text-secondary)]">
              Page {page} of {pageCount}
            </span>
            <Button
              type="button"
              variant="secondary"
              disabled={page >= pageCount || batchesUnavailable}
              onClick={() => updateFilters({ page: page + 1 })}
            >
              Next
            </Button>
          </div>
        </section>
      )}

      {openBatch && (
        <ScheduleTrackingProgressPanel
          batch={openBatch}
          onClose={closeBatch}
          registrySearch={search}
        />
      )}
    </section>
  )
}

function usePagedAssigneeOptions(
  role: 'Inspector' | 'Supervisor',
  enabled: boolean,
  selectedId?: string,
) {
  const [term, setTerm] = useState('')
  const inspectorQuery = useInfiniteScheduleWorkerAssignmentOptions(
    enabled && role === 'Inspector',
    term,
    selectedId,
  )
  const supervisorQuery = useInfiniteScheduleSupervisorAssignmentOptions(
    enabled && role === 'Supervisor',
    term,
    selectedId,
  )
  const query = role === 'Supervisor' ? supervisorQuery : inspectorQuery
  const pages = query.data?.pages ?? []
  const items: AssigneeOption[] = pages.flatMap((page) =>
    'supervisors' in page ? page.supervisors : page.workers,
  )
  const selected = pages[0]?.selected ?? undefined

  const search = (value: string) => {
    setTerm(value)
  }

  return {
    items,
    selected,
    loading: query.isPending,
    loadingMore: query.isFetchingNextPage,
    error: query.isError,
    hasMore: query.hasNextPage ?? false,
    retry: () => void query.refetch(),
    loadMore: () => void query.fetchNextPage(),
    search,
  }
}

function SummaryShortcut({
  label,
  value,
  active,
  onClick,
}: {
  label: string
  value: number
  active: boolean
  onClick: () => void
}) {
  return (
    <Card
      className={`p-0 shadow-none ${active ? 'border-[var(--primary)] ring-1 ring-[var(--primary)]' : ''}`}
    >
      <button
        type="button"
        aria-pressed={active}
        onClick={onClick}
        className="w-full rounded-xl p-3 text-left hover:bg-[var(--surface-muted)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
      >
        <span className="block text-xs font-medium text-[var(--text-secondary)]">
          {label}
        </span>
        <span className="mt-1 block text-2xl font-bold text-[var(--text-primary)] tabular-nums">
          {value.toLocaleString()}
        </span>
      </button>
    </Card>
  )
}

function ProgressBar({
  completed,
  total,
  cancelled,
}: {
  completed: number
  total: number
  cancelled: number
}) {
  const percent =
    total > 0 ? Math.min(100, Math.round((completed / total) * 100)) : 0
  return (
    <div>
      <div className="flex items-center justify-between gap-2 text-xs">
        <span className="font-medium text-[var(--text-primary)]">
          {completed} of {total} completed
        </span>
        <span className="text-[var(--text-secondary)] tabular-nums">
          {percent}%
        </span>
      </div>
      <div
        role="progressbar"
        aria-label="Inspection completion"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        className="mt-1.5 h-2 overflow-hidden rounded-full bg-[var(--surface-muted)]"
      >
        <div
          className="h-full rounded-full bg-[var(--primary)] transition-[width]"
          style={{ width: `${percent}%` }}
        />
      </div>
      {cancelled > 0 && (
        <p className="mt-1 text-xs text-[var(--text-secondary)]">
          {cancelled} cancelled, excluded from progress
        </p>
      )}
    </div>
  )
}

function ScheduleTrackingProgressPanel({
  batch,
  onClose,
  registrySearch,
}: {
  batch: ScheduleAssignmentQueueBatch
  onClose: () => void
  registrySearch: ScheduleSearch
}) {
  const dialogRef = useRef<HTMLElement>(null)
  const [assetText, setAssetText] = useState('')
  const [assetSearch, setAssetSearch] = useState('')
  const [status, setStatus] = useState<Schedule['status'] | ''>('')
  const [page, setPage] = useState(1)
  const detail = useScheduleRegistry({
    department: batch.department,
    assetCategory: batch.assetCategory,
    pmCycle: batch.pmCycle,
    page,
    pageSize: DETAIL_PAGE_SIZE,
    ...(assetSearch ? { search: assetSearch } : {}),
    ...(status ? { status } : {}),
  })
  const detailPageCount = Math.max(
    1,
    Math.ceil((detail.data?.totalCount ?? 0) / DETAIL_PAGE_SIZE),
  )

  useEffect(() => {
    dialogRef.current
      ?.querySelector<HTMLButtonElement>('[data-close-batch-panel]')
      ?.focus()
    const dialog = dialogRef.current
    if (!dialog) return
    const trapTab = (event: KeyboardEvent) => {
      if (event.key !== 'Tab') return
      const focusable = Array.from(
        dialog.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ),
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (!first || !last) return
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    dialog.addEventListener('keydown', trapTab)
    return () => dialog.removeEventListener('keydown', trapTab)
  }, [])

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      const normalized = assetText.trim()
      if (normalized !== assetSearch) {
        setAssetSearch(normalized)
        setPage(1)
      }
    }, 250)
    return () => window.clearTimeout(timeout)
  }, [assetSearch, assetText])

  const historySearch: ScheduleSearch = {
    view: 'history',
    year: Number(batch.pmCycle.slice(0, 4)),
    assetCategory: batch.assetCategory,
    department: batch.department,
    pmCycle: batch.pmCycle,
  }

  return (
    <div className="fixed inset-0 z-50 bg-black/40" role="presentation">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="tracking-panel-title"
        ref={dialogRef}
        className="ml-auto flex h-full w-full max-w-3xl flex-col overflow-hidden bg-white shadow-2xl"
      >
        <header className="flex items-start justify-between gap-4 border-b border-[var(--border-soft)] bg-[#f7f2ef] p-4 sm:p-6">
          <div>
            <h2
              id="tracking-panel-title"
              className="text-lg font-bold text-[var(--text-primary)]"
            >
              {batch.department} · {formatAssetCategory(batch.assetCategory)}
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {formatPmCycle(batch.pmCycle)} · Due{' '}
              {formatPmCycleDueDate(batch.pmCycle)}
            </p>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {formatBuildings(batch.buildings)}
            </p>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              Inspector:{' '}
              {batch.inspectorDisplayName ??
                assignmentLabel(batch.inspectorState)}{' '}
              · Supervisor:{' '}
              {batch.supervisorDisplayName ??
                assignmentLabel(batch.supervisorState)}
            </p>
            <div className="mt-3 max-w-sm">
              <ProgressBar
                completed={batch.completedCount}
                total={batch.activeScheduleCount}
                cancelled={batch.cancelledCount}
              />
            </div>
          </div>
          <Button
            data-close-batch-panel
            type="button"
            variant="secondary"
            onClick={onClose}
          >
            Close
          </Button>
        </header>

        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto p-4 sm:p-6">
          <div className="mb-4 grid gap-3 sm:grid-cols-[1fr_12rem]">
            <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
              Search assets
              <Input
                value={assetText}
                type="search"
                placeholder="Asset code or location"
                onChange={(event) => setAssetText(event.target.value)}
              />
            </label>
            <label className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
              Schedule status
              <select
                value={status}
                onChange={(event) => {
                  setStatus(event.target.value as Schedule['status'] | '')
                  setPage(1)
                }}
                className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3"
              >
                <option value="">All statuses</option>
                <option value="Due">Not started</option>
                <option value="Overdue">Overdue</option>
                <option value="Ongoing">In progress</option>
                <option value="Completed">Completed</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </label>
          </div>

          {detail.isPending || detail.isPlaceholderData ? (
            <p
              role="status"
              className="py-8 text-center text-sm text-[var(--text-secondary)]"
            >
              Loading asset progress…
            </p>
          ) : detail.isError ? (
            <div role="alert" className="m-auto text-center">
              <p className="text-sm text-[var(--text-secondary)]">
                Asset progress could not be loaded.
              </p>
              <Button
                type="button"
                variant="secondary"
                className="mt-3"
                onClick={() => void detail.refetch()}
              >
                Retry
              </Button>
            </div>
          ) : detail.data.items.length === 0 ? (
            <p className="py-8 text-center text-sm text-[var(--text-secondary)]">
              No schedules match this asset search and status.
            </p>
          ) : (
            <>
              <p className="mb-2 text-xs text-[var(--text-secondary)]">
                {detail.data.totalCount.toLocaleString()} schedules · page{' '}
                {page} of {detailPageCount}
              </p>
              <div className="overflow-x-auto rounded-lg border border-[var(--border-soft)]">
                <table className="w-full min-w-[620px] text-left text-sm">
                  <thead className="bg-[var(--surface-muted)] text-xs text-[var(--text-neutral)]">
                    <tr>
                      <th scope="col" className="px-3 py-2">
                        Asset
                      </th>
                      <th scope="col" className="px-3 py-2">
                        Location
                      </th>
                      <th scope="col" className="px-3 py-2">
                        Status
                      </th>
                      <th scope="col" className="px-3 py-2">
                        Timing
                      </th>
                      <th scope="col" className="px-3 py-2">
                        Action
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--border-soft)]">
                    {detail.data.items.map((schedule) => (
                      <tr key={schedule.id}>
                        <td className="px-3 py-3 font-semibold text-[var(--primary)]">
                          {schedule.asset?.assetCode ??
                            'Asset details unavailable'}
                        </td>
                        <td className="px-3 py-3 text-[var(--text-secondary)]">
                          {[schedule.asset?.building, schedule.asset?.location]
                            .filter(Boolean)
                            .join(' · ') || 'Location not recorded'}
                        </td>
                        <td className="px-3 py-3">
                          <ScheduleStatusBadge status={schedule.status} />
                        </td>
                        <td className="px-3 py-3 text-xs text-[var(--text-secondary)]">
                          Due{' '}
                          {new Date(schedule.scheduleDate).toLocaleDateString()}
                          {schedule.completedAt ? (
                            <span className="block">
                              Saved{' '}
                              {new Date(schedule.completedAt).toLocaleString()}
                            </span>
                          ) : null}
                        </td>
                        <td className="px-3 py-3">
                          <Link
                            to="/app/schedules/$scheduleId"
                            params={{ scheduleId: schedule.id }}
                            search={{
                              returnContext: {
                                kind: 'scheduleRegistry',
                                search: { ...registrySearch, view: 'tracking' },
                              },
                            }}
                            className="font-semibold text-[var(--primary)] hover:underline"
                          >
                            View details
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-3 flex items-center justify-between gap-3">
                <Button
                  type="button"
                  variant="secondary"
                  disabled={page <= 1 || detail.isFetching}
                  onClick={() => setPage((value) => value - 1)}
                >
                  Previous
                </Button>
                <span className="text-sm text-[var(--text-secondary)]">
                  Page {page} of {detailPageCount}
                </span>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={page >= detailPageCount || detail.isFetching}
                  onClick={() => setPage((value) => value + 1)}
                >
                  Next
                </Button>
              </div>
            </>
          )}
          <div className="mt-6 border-t border-[var(--border-soft)] pt-4">
            <Link
              to="/app/schedules"
              search={historySearch}
              className="font-semibold text-[var(--primary)] hover:underline"
            >
              Open batch history
            </Link>
          </div>
        </div>
      </section>
    </div>
  )
}

function ScheduleStatusBadge({ status }: { status: Schedule['status'] }) {
  const label =
    status === 'Due'
      ? 'Not started'
      : status === 'Ongoing'
        ? 'In progress'
        : status
  const variant =
    status === 'Completed'
      ? 'success'
      : status === 'Overdue'
        ? 'danger'
        : status === 'Ongoing'
          ? 'warning'
          : 'neutral'
  return <Badge variant={variant}>{label}</Badge>
}

function formatBuildings(buildings: readonly string[]) {
  if (buildings.length === 0) return 'Building not recorded'
  return buildings.length <= 2
    ? buildings.join(', ')
    : `${buildings[0]} + ${buildings.length - 1} more buildings`
}

function assignmentLabel(state: string) {
  return state === 'Assigned' ? 'Assigned' : state
}
