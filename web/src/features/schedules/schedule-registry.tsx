import { useEffect, useMemo } from 'react'
import { CalendarPlus } from 'lucide-react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAssets } from '@/features/assets/asset-queries'
import { useCurrentUser } from '@/features/auth/current-user'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useScheduleQuarters,
  useSchedules,
  useScheduleStatuses,
} from '@/features/schedules/schedule-queries'
import {
  fromDateTimeLocal,
  formatScheduleDate,
  toDateTimeLocal,
} from '@/features/schedules/schedule-presentation'

export type ScheduleSearch = {
  assetId?: string | undefined
  status?: Schedule['status'] | undefined
  from?: string | undefined
  to?: string | undefined
  quarter?: NonNullable<Schedule['quarter']> | undefined
  year?: number | undefined
  page?: number | undefined
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
const columns = [
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
  columnHelper.accessor('scheduleDate', {
    header: 'Date',
    cell: ({ getValue }) => formatScheduleDate(getValue()),
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
  columnHelper.accessor('status', { header: 'Recorded status' }),
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
    options?: { replace?: boolean },
  ) => void
}) {
  const currentUser = useCurrentUser()
  const assets = useAssets()
  const statuses = useScheduleStatuses()
  const quarters = useScheduleQuarters()
  const allSchedules = useSchedules()
  const filteredSchedules = useSchedules({
    ...(search.assetId ? { assetId: search.assetId } : {}),
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

  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: pageData,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  useEffect(() => {
    if (filteredSchedules.isSuccess && search.page && search.page > pageCount) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true },
      )
    }
  }, [filteredSchedules.isSuccess, onSearchChange, pageCount, search])

  const setFilter = (next: Partial<ScheduleSearch>) =>
    onSearchChange({ ...search, ...next, page: 1 })

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
            Browse recorded schedule dates and statuses. The interface does not
            infer overdue state or change workflow status.
          </p>
        </div>
        {canCreate && (
          <Button asChild>
            <Link to="/app/schedules/new">
              <CalendarPlus aria-hidden="true" className="mr-2 size-4" />
              Add schedule
            </Link>
          </Button>
        )}
      </div>

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
            Schedule summary is currently unavailable.
          </p>
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
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <select
            aria-label="Asset"
            value={search.assetId ?? ''}
            onChange={(event) =>
              setFilter({ assetId: event.target.value || undefined })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All assets</option>
            {(assets.data ?? []).map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.assetCode}
              </option>
            ))}
          </select>
          <select
            aria-label="Schedule status"
            value={search.status ?? ''}
            onChange={(event) =>
              setFilter({
                status: (event.target.value || undefined) as
                  Schedule['status'] | undefined,
              })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
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
              value={toDateTimeLocal(search.from)}
              onChange={(event) =>
                setFilter({ from: fromDateTimeLocal(event.target.value) })
              }
              className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            To
            <input
              type="datetime-local"
              value={toDateTimeLocal(search.to)}
              onChange={(event) =>
                setFilter({ to: fromDateTimeLocal(event.target.value) })
              }
              className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <select
            aria-label="Quarter"
            value={search.quarter ?? ''}
            onChange={(event) =>
              setFilter({
                quarter: (event.target.value || undefined) as
                  NonNullable<Schedule['quarter']> | undefined,
              })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
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
              value={search.year ?? ''}
              onChange={(event) =>
                setFilter({
                  year: event.target.value
                    ? Number(event.target.value)
                    : undefined,
                })
              }
              className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <div className="flex items-end">
            <Button
              type="button"
              className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
              onClick={() => onSearchChange({ page: 1 })}
            >
              Clear filters
            </Button>
          </div>
        </div>
      </Card>

      {filteredSchedules.isPending ? (
        <Card role="status" className="space-y-3 p-5 shadow-none">
          <span className="sr-only">Loading schedules...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </Card>
      ) : filteredSchedules.isError ? (
        <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
          <h2 className="font-bold text-[var(--error)]">
            Schedules unavailable
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            The schedule registry could not be loaded.
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
        <div className="space-y-4">
          <Card className="hidden overflow-hidden p-0 shadow-none md:block">
            <div className="overflow-x-auto">
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
                        <td key={cell.id} className="px-5 py-4">
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
            </div>
          </Card>
          <div className="grid gap-3 md:hidden">
            {pageData.map((schedule) => (
              <Card key={schedule.id} className="space-y-3 shadow-none">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h2 className="font-bold">
                      {schedule.asset?.assetCode ?? schedule.assetId}
                    </h2>
                    <p className="text-xs text-[var(--text-neutral)]">
                      {schedule.asset?.assetCategory ?? 'Category not recorded'}
                    </p>
                  </div>
                  <span className="rounded-full bg-[var(--page-background)] px-2 py-1 text-xs font-semibold">
                    {schedule.status}
                  </span>
                </div>
                <p className="text-sm text-[var(--text-secondary)]">
                  {formatScheduleDate(schedule.scheduleDate)} /{' '}
                  {schedule.periodType}
                </p>
                <Link
                  to="/app/schedules/$scheduleId"
                  params={{ scheduleId: schedule.id }}
                  className="inline-block font-semibold text-[var(--primary)] hover:underline"
                >
                  View details
                </Link>
              </Card>
            ))}
          </div>
          <div className="flex items-center justify-between border-t border-[var(--border-soft)] px-5 py-4">
            <p className="text-sm text-[var(--text-secondary)]">
              Page {page} of {pageCount}
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                disabled={page <= 1}
                onClick={() =>
                  onSearchChange({
                    ...search,
                    page: page - 1 > 1 ? page - 1 : undefined,
                  })
                }
              >
                Previous
              </Button>
              <Button
                type="button"
                disabled={page >= pageCount}
                onClick={() => onSearchChange({ ...search, page: page + 1 })}
              >
                Next
              </Button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
