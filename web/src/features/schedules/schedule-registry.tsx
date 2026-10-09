import { useEffect, useMemo, useState } from 'react'
import { CalendarPlus } from 'lucide-react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { Asset } from '@/features/assets/asset-contract'
import { useAssetCategories, useAssets } from '@/features/assets/asset-queries'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import { categoryLabel } from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  RegistryLoadingPanel,
  RegistryResultsPanel,
} from '@/features/shared/registry-results-panel'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useGenerateSchedules,
  useScheduleQuarters,
  useSchedules,
  useScheduleStatuses,
} from '@/features/schedules/schedule-queries'
import {
  fromDateTimeLocal,
  formatPmCycle,
  formatPmCycleDueDate,
  toDateTimeLocal,
} from '@/features/schedules/schedule-presentation'

export type ScheduleSearch = {
  assetId?: string | undefined
  assetCategory?: Asset['assetCategory'] | undefined
  department?: string | undefined
  search?: string | undefined
  status?: Schedule['status'] | undefined
  from?: string | undefined
  to?: string | undefined
  quarter?: NonNullable<Schedule['quarter']> | undefined
  year?: number | undefined
  page?: number | undefined
}

const scheduleAccessDeniedMessage =
  'Schedule reads require a GSD, Inspector, or Supervisor role. Admin is a technical system administration role and cannot read operational schedules.'

function isScheduleAccessDenied(error: unknown) {
  return error instanceof ApiError && error.status === 403
}

function SummaryCard({ label, count }: { label: string; count: number }) {
  return (
    <Card className="p-4 shadow-none">
      <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </p>
      <p className="mt-2 text-2xl font-bold text-[var(--text-primary)]">
        {count}
      </p>
    </Card>
  )
}

const columnHelper = createColumnHelper<Schedule>()
function statusVariant(
  status: Schedule['status'],
): 'neutral' | 'success' | 'warning' | 'danger' {
  if (status === 'Completed') return 'success'
  if (status === 'Overdue') return 'danger'
  if (status === 'Due' || status === 'Ongoing') return 'warning'
  return 'neutral'
}

export function getCurrentManilaYear(now = new Date()) {
  return Number(
    new Intl.DateTimeFormat('en', {
      timeZone: 'Asia/Manila',
      year: 'numeric',
    }).format(now),
  )
}

const createColumns = (search: ScheduleSearch) => [
  columnHelper.accessor('asset', {
    header: 'Asset',
    cell: ({ row }) => (
      <div>
        <p className="font-semibold">
          {row.original.asset?.assetCode ?? row.original.assetId}
        </p>
        <p className="text-xs text-[var(--text-neutral)]">
          {row.original.asset?.assetCategory ?? 'Category not recorded'}
        </p>
      </div>
    ),
  }),
  columnHelper.accessor('pmCycle', {
    header: 'Scheduled month',
    cell: ({ getValue }) => formatPmCycle(getValue()),
  }),
  columnHelper.accessor('scheduleDate', {
    header: 'Due date',
    cell: ({ row }) => formatPmCycleDueDate(row.original.pmCycle),
  }),
  columnHelper.accessor('periodType', { header: 'Period' }),
  columnHelper.display({
    id: 'periodMetadata',
    header: 'Period metadata',
    cell: ({ row }) =>
      [
        row.original.quarter,
        row.original.semester,
        row.original.year,
        row.original.academicYear,
      ]
        .filter((value) => value !== null)
        .join(' / ') || 'Not recorded',
  }),
  columnHelper.accessor('status', {
    header: 'Recorded status',
    cell: ({ getValue }) => (
      <Badge variant={statusVariant(getValue())}>{getValue()}</Badge>
    ),
  }),
  columnHelper.display({
    id: 'location',
    header: 'Location',
    cell: ({ row }) =>
      [
        row.original.asset?.building,
        row.original.asset?.department,
        row.original.asset?.location,
      ]
        .filter(Boolean)
        .join(' / ') || 'Not recorded',
  }),
  columnHelper.display({
    id: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <Link
        to="/app/schedules/$scheduleId"
        params={{ scheduleId: row.original.id }}
        search={{
          ...search,
          returnContext: { kind: 'scheduleRegistry', search },
        }}
        className="font-semibold text-[var(--primary)] hover:underline"
      >
        View details
      </Link>
    ),
  }),
]

export function ScheduleRegistry({
  search,
  onSearchChange,
}: {
  search: ScheduleSearch
  onSearchChange: (
    next: ScheduleSearch,
    options?: { replace?: boolean; preserveScroll?: boolean },
  ) => void
}) {
  const currentUser = useCurrentUser()
  const scheduleGeneration = useGenerateSchedules()
  const currentManilaYear = getCurrentManilaYear()
  const [generationYear, setGenerationYear] = useState(() =>
    String(currentManilaYear),
  )
  const [generationMessage, setGenerationMessage] = useState<string | null>(
    null,
  )
  const [generationFailed, setGenerationFailed] = useState(false)
  const assets = useAssets()
  const categories = useAssetCategories()
  const statuses = useScheduleStatuses()
  const quarters = useScheduleQuarters()
  const allSchedules = useSchedules()
  const [draft, setDraft] = useState<Omit<ScheduleSearch, 'page'>>(() => ({
    assetId: search.assetId,
    assetCategory: search.assetCategory,
    department: search.department,
    search: search.search,
    status: search.status,
    from: search.from,
    to: search.to,
    quarter: search.quarter,
    year: search.year,
  }))

  useEffect(() => {
    setDraft({
      assetId: search.assetId,
      assetCategory: search.assetCategory,
      department: search.department,
      search: search.search,
      status: search.status,
      from: search.from,
      to: search.to,
      quarter: search.quarter,
      year: search.year,
    })
  }, [
    search.assetId,
    search.assetCategory,
    search.department,
    search.search,
    search.status,
    search.from,
    search.to,
    search.quarter,
    search.year,
  ])

  const filteredSchedules = useSchedules({
    ...(search.assetId ? { assetId: search.assetId } : {}),
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.search ? { search: search.search } : {}),
    ...(search.status ? { status: search.status } : {}),
    ...(search.from ? { from: search.from } : {}),
    ...(search.to ? { to: search.to } : {}),
    ...(search.quarter ? { quarter: search.quarter } : {}),
    ...(search.year ? { year: search.year } : {}),
  })

  const canCreate =
    currentUser.data?.roles.some(
      (role) => role === 'GSD' || role === 'Supervisor',
    ) ?? false
  const canGenerate = currentUser.data?.roles.includes('GSD') ?? false
  const parsedGenerationYear = Number(generationYear)
  const generationYearIsValid =
    /^\d{4}$/.test(generationYear) &&
    parsedGenerationYear >= 2000 &&
    parsedGenerationYear <= currentManilaYear
  const pageSize = 10
  const records = useMemo(
    () => filteredSchedules.data ?? [],
    [filteredSchedules.data],
  )
  const pageCount = Math.max(1, Math.ceil(records.length / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = useMemo(
    () => records.slice((page - 1) * pageSize, page * pageSize),
    [records, page],
  )

  const departments = useMemo(
    () =>
      [
        ...new Set(
          (assets.data ?? [])
            .map((asset) => asset.department)
            .filter((value): value is string => Boolean(value)),
        ),
      ].sort(),
    [assets.data],
  )
  const categoryByCode = useMemo(
    () =>
      new Map(categories.data?.map((category) => [category.code, category])),
    [categories.data],
  )
  const dateRangeIsValid = !draft.from || !draft.to || draft.from <= draft.to

  const changePage = (nextPage: number) => {
    onSearchChange(
      { ...search, page: nextPage > 1 ? nextPage : undefined },
      { preserveScroll: true },
    )
  }

  const tableColumns = useMemo(() => createColumns(search), [search])

  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: pageData,
    columns: tableColumns,
    getCoreRowModel: getCoreRowModel(),
  })

  useEffect(() => {
    if (
      filteredSchedules.isSuccess &&
      !filteredSchedules.isPlaceholderData &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true, preserveScroll: true },
      )
    }
  }, [
    filteredSchedules.isSuccess,
    filteredSchedules.isPlaceholderData,
    onSearchChange,
    pageCount,
    search,
  ])

  const apply = () => {
    if (!dateRangeIsValid) return
    onSearchChange(
      {
        ...draft,
        search: draft.search?.trim() || undefined,
        page: 1,
      },
      { preserveScroll: true },
    )
  }

  const clear = () => {
    setDraft({})
    onSearchChange({ page: 1 }, { preserveScroll: true })
  }

  return (
    <section aria-labelledby="schedules-title" className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Preventive maintenance
          </p>
          <h1
            id="schedules-title"
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
          >
            Schedules
          </h1>
          <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
            Browse scheduled PM months, due dates, and recorded statuses. The
            interface does not infer overdue state or change workflow status.
          </p>
        </div>
        {(canCreate || canGenerate) && (
          <div className="flex flex-wrap items-end gap-3">
            {canGenerate && (
              <form
                className="flex flex-wrap items-end gap-2"
                onSubmit={(event) => {
                  event.preventDefault()
                  if (!generationYearIsValid) return
                  setGenerationMessage(null)
                  setGenerationFailed(false)
                  scheduleGeneration.mutate(parsedGenerationYear, {
                    onSuccess: (result) => {
                      setGenerationFailed(false)
                      setGenerationMessage(
                        `Year ${result.year}: created ${result.createdSchedules} missing schedules; ${result.existingSchedules} already existed.`,
                      )
                    },
                    onError: () => {
                      setGenerationFailed(true)
                      setGenerationMessage(
                        'Schedule generation failed. Try again.',
                      )
                    },
                  })
                }}
              >
                <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
                  Generation year
                  <Input
                    type="number"
                    min={2000}
                    max={currentManilaYear}
                    step={1}
                    value={generationYear}
                    aria-label="Generation year"
                    onChange={(event) => setGenerationYear(event.target.value)}
                    className="h-10 w-28 px-3"
                  />
                </label>
                <Button
                  type="submit"
                  disabled={
                    !generationYearIsValid || scheduleGeneration.isPending
                  }
                >
                  {scheduleGeneration.isPending
                    ? 'Generating…'
                    : 'Generate missing schedules'}
                </Button>
              </form>
            )}
            {canCreate && (
              <Button asChild>
                <Link to="/app/schedules/new">
                  <CalendarPlus aria-hidden="true" className="mr-2 size-4" />
                  Add schedule
                </Link>
              </Button>
            )}
          </div>
        )}
      </div>
      {canGenerate && generationMessage && (
        <p
          className={`-mt-4 text-sm ${generationFailed ? 'text-[var(--error)]' : 'text-[var(--text-secondary)]'}`}
          role={generationFailed ? 'alert' : 'status'}
        >
          {generationMessage}
        </p>
      )}

      {allSchedules.isPending || statuses.isPending ? (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" role="status">
          <span className="sr-only">Loading schedule summary...</span>
          {Array.from({ length: 3 }, (_, index) => (
            <Card key={index} className="p-4 shadow-none">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="mt-2 h-8 w-12" />
            </Card>
          ))}
        </div>
      ) : allSchedules.isError || statuses.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="font-semibold text-[var(--error)]">
            {allSchedules.isError && isScheduleAccessDenied(allSchedules.error)
              ? scheduleAccessDeniedMessage
              : 'Schedule summary is currently unavailable.'}
          </p>
          <Button
            type="button"
            className="mt-3"
            onClick={() => {
              void allSchedules.refetch()
              void statuses.refetch()
            }}
          >
            Retry summary
          </Button>
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          <SummaryCard label="All schedules" count={allSchedules.data.length} />
          {statuses.data.map((status) => (
            <SummaryCard
              key={status.code}
              label={status.displayName}
              count={
                allSchedules.data.filter(
                  (schedule) => schedule.status === status.code,
                ).length
              }
            />
          ))}
        </div>
      )}

      <Card className="p-4 shadow-none">
        <form
          className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <Input
            aria-label="Search schedules"
            placeholder="Asset code, location, department, or PM cycle"
            value={draft.search ?? ''}
            maxLength={256}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                search: event.target.value,
              }))
            }
            className="xl:col-span-2"
          />
          <select
            aria-label="Asset"
            value={draft.assetId ?? ''}
            disabled={assets.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                assetId: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All assets</option>
            {(assets.data ?? []).map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.assetCode}
              </option>
            ))}
          </select>
          <select
            aria-label="Asset category"
            value={draft.assetCategory ?? ''}
            disabled={categories.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                assetCategory: (event.target.value || undefined) as
                  Asset['assetCategory'] | undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All asset categories</option>
            {assetCategoryCodes.map((categoryCode) => (
              <option key={categoryCode} value={categoryCode}>
                {categoryLabel(categoryByCode.get(categoryCode), categoryCode)}
              </option>
            ))}
          </select>
          <select
            aria-label="Department"
            value={draft.department ?? ''}
            disabled={assets.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                department: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All departments</option>
            {departments.map((department) => (
              <option key={department} value={department}>
                {department}
              </option>
            ))}
          </select>
          <select
            aria-label="Schedule status"
            value={draft.status ?? ''}
            disabled={statuses.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                status: (event.target.value || undefined) as
                  Schedule['status'] | undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All statuses</option>
            {(statuses.data ?? []).map((status) => (
              <option key={status.code} value={status.code}>
                {status.displayName}
              </option>
            ))}
          </select>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            From
            <input
              type="datetime-local"
              value={toDateTimeLocal(draft.from)}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  from: fromDateTimeLocal(event.target.value),
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            To
            <input
              type="datetime-local"
              value={toDateTimeLocal(draft.to)}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  to: fromDateTimeLocal(event.target.value),
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <select
            aria-label="Quarter"
            value={draft.quarter ?? ''}
            disabled={quarters.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                quarter: (event.target.value || undefined) as
                  NonNullable<Schedule['quarter']> | undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All quarters</option>
            {(quarters.data ?? []).map((quarter) => (
              <option key={quarter.code} value={quarter.code}>
                {quarter.displayName}
              </option>
            ))}
          </select>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            Year
            <input
              type="number"
              min="2000"
              max={new Date().getUTCFullYear() + 5}
              value={draft.year ?? ''}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  year: event.target.value
                    ? Number(event.target.value)
                    : undefined,
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <div className="flex flex-wrap items-end gap-2 xl:col-span-4">
            <Button type="submit" disabled={!dateRangeIsValid}>
              Apply filters
            </Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
          </div>
        </form>
        {!dateRangeIsValid && (
          <p className="mt-3 text-sm text-[var(--error)]" role="alert">
            The start date must be before or equal to the end date.
          </p>
        )}
        {(assets.isError ||
          categories.isError ||
          statuses.isError ||
          quarters.isError) && (
          <div
            className="mt-3 flex flex-wrap gap-3 text-sm text-[var(--error)]"
            role="alert"
          >
            {assets.isError && (
              <Button type="button" onClick={() => void assets.refetch()}>
                Retry asset options
              </Button>
            )}
            {categories.isError && (
              <Button type="button" onClick={() => void categories.refetch()}>
                Retry category options
              </Button>
            )}
            {statuses.isError && (
              <Button type="button" onClick={() => void statuses.refetch()}>
                Retry status options
              </Button>
            )}
            {quarters.isError && (
              <Button type="button" onClick={() => void quarters.refetch()}>
                Retry quarter options
              </Button>
            )}
          </div>
        )}
      </Card>

      {records.length === 0 &&
        filteredSchedules.isFetching &&
        !filteredSchedules.isPending && (
          <p
            className="min-h-5 text-sm text-[var(--text-neutral)]"
            role="status"
          >
            Updating results...
          </p>
        )}
      {filteredSchedules.isPending ? (
        <RegistryLoadingPanel breakpoint="md" viewportSize="standard">
          <span className="sr-only">Loading schedules...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </RegistryLoadingPanel>
      ) : filteredSchedules.isError ? (
        <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
          <h2 className="font-bold text-[var(--error)]">
            Schedules unavailable
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            {isScheduleAccessDenied(filteredSchedules.error)
              ? scheduleAccessDeniedMessage
              : 'The schedule registry could not be loaded.'}
          </p>
          <Button
            type="button"
            className="mt-4"
            onClick={() => void filteredSchedules.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : records.length === 0 ? (
        <Card className="p-8 text-center shadow-none">
          <h2 className="font-bold">
            {allSchedules.isSuccess && allSchedules.data.length === 0
              ? 'No schedules are recorded yet.'
              : 'No schedules match these filters.'}
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            {allSchedules.isSuccess && allSchedules.data.length === 0
              ? 'An authorized schedule manager can add the first record.'
              : 'Clear the filters to return to all recorded schedules.'}
          </p>
        </Card>
      ) : (
        <RegistryResultsPanel
          label="Schedules"
          breakpoint="md"
          viewportSize="standard"
          isUpdating={filteredSchedules.isFetching}
          desktopContent={
            <table className="w-full min-w-[760px] text-left text-sm">
              <thead className="bg-[var(--page-background)] text-xs tracking-wide text-[var(--text-neutral)] uppercase">
                {table.getHeaderGroups().map((headerGroup) => (
                  <tr key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <th key={header.id} className="px-5 py-3">
                        {header.isPlaceholder
                          ? null
                          : flexRender(
                              header.column.columnDef.header,
                              header.getContext(),
                            )}
                      </th>
                    ))}
                  </tr>
                ))}
              </thead>
              <tbody className="divide-y divide-[var(--border-soft)]">
                {table.getRowModel().rows.map((row) => (
                  <tr key={row.id}>
                    {row.getVisibleCells().map((cell) => (
                      <td key={cell.id} className="px-5 py-2">
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext(),
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          }
          mobileContent={
            <div className="grid gap-3">
              {pageData.map((schedule) => (
                <Card key={schedule.id} className="space-y-3 shadow-none">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h2 className="font-bold">
                        {schedule.asset?.assetCode ?? schedule.assetId}
                      </h2>
                      <p className="text-xs text-[var(--text-neutral)]">
                        {schedule.asset?.assetCategory ??
                          'Category not recorded'}
                      </p>
                    </div>
                    <Badge variant={statusVariant(schedule.status)}>
                      {schedule.status}
                    </Badge>
                  </div>
                  <p className="text-sm text-[var(--text-secondary)]">
                    Scheduled month: {formatPmCycle(schedule.pmCycle)}
                    <br />
                    Due date: {formatPmCycleDueDate(schedule.pmCycle)}
                  </p>
                  <Link
                    to="/app/schedules/$scheduleId"
                    params={{ scheduleId: schedule.id }}
                    search={{
                      ...search,
                      returnContext: { kind: 'scheduleRegistry', search },
                    }}
                    className="inline-block font-semibold text-[var(--primary)] hover:underline"
                  >
                    View details
                  </Link>
                </Card>
              ))}
            </div>
          }
          pagination={{
            page,
            pageSize,
            total: records.length,
            onPageChange: changePage,
          }}
        />
      )}
    </section>
  )
}
