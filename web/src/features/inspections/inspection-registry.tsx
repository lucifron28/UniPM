import { useEffect, useMemo } from 'react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  type RowData,
  useReactTable,
} from '@tanstack/react-table'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAssets } from '@/features/assets/asset-queries'
import type { Inspection } from '@/features/inspections/inspection-contract'
import { useInspections } from '@/features/inspections/inspection-queries'
import {
  excerpt,
  formatInspectionDate,
  inspectionOutcome,
} from '@/features/inspections/inspection-presentation'
import { useSchedules } from '@/features/schedules/schedule-queries'
import {
  fromDateTimeLocal,
  toDateTimeLocal,
} from '@/features/schedules/schedule-presentation'

export type InspectionSearch = {
  assetId?: string | undefined
  scheduleId?: string | undefined
  isOperational?: boolean | undefined
  dateFrom?: string | undefined
  dateTo?: string | undefined
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

const columnHelper = createColumnHelper<Inspection>()
const columns = [
  columnHelper.accessor('assetId', {
    header: 'Asset',
    cell: ({ getValue, table }) => {
      const assets = table.options.meta?.assets
      const asset = assets?.get(getValue())
      return (
        <div>
          <p className="font-semibold">{asset?.assetCode ?? getValue()}</p>
          <p className="text-xs text-[var(--text-neutral)]">
            {asset?.assetCategory ?? 'Category not recorded'}
          </p>
        </div>
      )
    },
  }),
  columnHelper.accessor('scheduleId', {
    header: 'Schedule',
    cell: ({ getValue, table }) => {
      const schedule = table.options.meta?.schedules.get(getValue())
      return schedule ? formatInspectionDate(schedule.scheduleDate) : getValue()
    },
  }),
  columnHelper.accessor('dateInspected', {
    header: 'Inspected',
    cell: ({ getValue }) => formatInspectionDate(getValue()),
  }),
  columnHelper.accessor('isOperational', {
    header: 'Recorded result',
    cell: ({ getValue }) => inspectionOutcome(getValue()),
  }),
  columnHelper.accessor('remarks', {
    header: 'Remarks',
    cell: ({ getValue }) => excerpt(getValue()),
  }),
  columnHelper.accessor('actionsRecommendations', {
    header: 'Actions and recommendations',
    cell: ({ getValue }) => excerpt(getValue()),
  }),
  columnHelper.display({
    id: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <Link
        to="/app/inspections/$inspectionId"
        params={{ inspectionId: row.original.id }}
        className="font-semibold text-[var(--primary)] hover:underline"
      >
        View details
      </Link>
    ),
  }),
]

declare module '@tanstack/react-table' {
  interface TableMeta<TData extends RowData> {
    assets?: Map<string, { assetCode: string; assetCategory: string }>
    schedules: Map<string, { scheduleDate: string }>
    rowData?: TData
  }
}

export function InspectionRegistry({
  search,
  onSearchChange,
}: {
  search: InspectionSearch
  onSearchChange: (
    next: InspectionSearch,
    options?: { replace?: boolean },
  ) => void
}) {
  const assets = useAssets()
  const schedules = useSchedules()
  const allInspections = useInspections()
  const filteredInspections = useInspections({
    ...(search.assetId ? { assetId: search.assetId } : {}),
    ...(search.scheduleId ? { scheduleId: search.scheduleId } : {}),
    ...(search.isOperational === undefined
      ? {}
      : { isOperational: search.isOperational }),
    ...(search.dateFrom ? { dateFrom: search.dateFrom } : {}),
    ...(search.dateTo ? { dateTo: search.dateTo } : {}),
  })

  const records = useMemo(
    () => filteredInspections.data ?? [],
    [filteredInspections.data],
  )
  const assetMap = useMemo(
    () =>
      new Map(
        (assets.data ?? []).map((asset) => [
          asset.id,
          { assetCode: asset.assetCode, assetCategory: asset.assetCategory },
        ]),
      ),
    [assets.data],
  )
  const scheduleMap = useMemo(
    () =>
      new Map(
        (schedules.data ?? []).map((schedule) => [
          schedule.id,
          { scheduleDate: schedule.scheduleDate },
        ]),
      ),
    [schedules.data],
  )
  const pageSize = 10
  const pageCount = Math.max(1, Math.ceil(records.length / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = useMemo(
    () => records.slice((page - 1) * pageSize, page * pageSize),
    [page, records],
  )

  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: pageData,
    columns,
    getCoreRowModel: getCoreRowModel(),
    meta: { assets: assetMap, schedules: scheduleMap },
  })

  useEffect(() => {
    if (
      filteredInspections.isSuccess &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true },
      )
    }
  }, [filteredInspections.isSuccess, onSearchChange, pageCount, search])

  const setFilter = (next: Partial<InspectionSearch>) =>
    onSearchChange({ ...search, ...next, page: 1 })

  return (
    <section aria-labelledby="inspections-title" className="space-y-6">
      <div>
        <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
          Source-record review
        </p>
        <h1
          id="inspections-title"
          className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
        >
          Inspections
        </h1>
        <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
          Review recorded inspection outcomes and source notes. This page does
          not submit, approve, or change inspection records.
        </p>
      </div>

      {allInspections.isPending ? (
        <div className="grid gap-3 sm:grid-cols-3" role="status">
          <span className="sr-only">Loading inspection summary...</span>
          {Array.from({ length: 3 }, (_, index) => (
            <Card key={index} className="p-4 shadow-none">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="mt-2 h-8 w-12" />
            </Card>
          ))}
        </div>
      ) : allInspections.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="font-semibold text-[var(--error)]">
            Inspection summary is currently unavailable.
          </p>
          <Button
            type="button"
            className="mt-3"
            onClick={() => void allInspections.refetch()}
          >
            Retry summary
          </Button>
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-3">
          <SummaryCard
            label="All inspection records"
            count={allInspections.data.length}
          />
          <SummaryCard
            label="Operational"
            count={
              allInspections.data.filter((item) => item.isOperational).length
            }
          />
          <SummaryCard
            label="Not operational"
            count={
              allInspections.data.filter((item) => !item.isOperational).length
            }
          />
        </div>
      )}

      <Card className="p-4 shadow-none">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          <select
            aria-label="Asset"
            value={search.assetId ?? ''}
            disabled={assets.isError}
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
            aria-label="Schedule"
            value={search.scheduleId ?? ''}
            disabled={schedules.isError}
            onChange={(event) =>
              setFilter({ scheduleId: event.target.value || undefined })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All schedules</option>
            {(schedules.data ?? []).map((schedule) => (
              <option key={schedule.id} value={schedule.id}>
                {schedule.asset?.assetCode ?? schedule.assetId} -{' '}
                {formatInspectionDate(schedule.scheduleDate)}
              </option>
            ))}
          </select>
          <select
            aria-label="Recorded operational result"
            value={
              search.isOperational === undefined
                ? ''
                : search.isOperational
                  ? 'true'
                  : 'false'
            }
            onChange={(event) =>
              setFilter({
                isOperational:
                  event.target.value === ''
                    ? undefined
                    : event.target.value === 'true',
              })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All recorded results</option>
            <option value="true">Operational</option>
            <option value="false">Not operational</option>
          </select>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            Inspected from
            <input
              type="datetime-local"
              value={toDateTimeLocal(search.dateFrom)}
              onChange={(event) =>
                setFilter({ dateFrom: fromDateTimeLocal(event.target.value) })
              }
              className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            Inspected to
            <input
              type="datetime-local"
              value={toDateTimeLocal(search.dateTo)}
              onChange={(event) =>
                setFilter({ dateTo: fromDateTimeLocal(event.target.value) })
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
        {(assets.isError || schedules.isError) && (
          <div
            role="alert"
            className="mt-3 flex flex-wrap gap-3 text-sm text-[var(--error)]"
          >
            {assets.isError && (
              <Button type="button" onClick={() => void assets.refetch()}>
                Retry asset context
              </Button>
            )}
            {schedules.isError && (
              <Button type="button" onClick={() => void schedules.refetch()}>
                Retry schedule context
              </Button>
            )}
          </div>
        )}
      </Card>

      {filteredInspections.isPending ? (
        <Card role="status" className="space-y-3 p-5 shadow-none">
          <span className="sr-only">Loading inspections...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </Card>
      ) : filteredInspections.isError ? (
        <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
          <h2 className="font-bold text-[var(--error)]">
            Inspections unavailable
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            The inspection registry could not be loaded.
          </p>
          <Button
            type="button"
            className="mt-4"
            onClick={() => void filteredInspections.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : records.length === 0 ? (
        <Card className="p-8 text-center shadow-none">
          <h2 className="font-bold">
            {allInspections.isSuccess && allInspections.data.length === 0
              ? 'No inspection records are available yet.'
              : 'No inspection records match these filters.'}
          </h2>
        </Card>
      ) : (
        <div className="space-y-4">
          <Card className="hidden overflow-hidden p-0 shadow-none md:block">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[960px] text-left text-sm">
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
                        <td key={cell.id} className="px-5 py-4 align-top">
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
          <div className="space-y-3 md:hidden">
            {pageData.map((inspection) => {
              const asset = assetMap.get(inspection.assetId)
              return (
                <Card key={inspection.id} className="space-y-3 shadow-none">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-semibold">
                        {asset?.assetCode ?? inspection.assetId}
                      </p>
                      <p className="text-sm text-[var(--text-secondary)]">
                        {formatInspectionDate(inspection.dateInspected)}
                      </p>
                    </div>
                    <span className="rounded-full bg-[var(--page-background)] px-3 py-1 text-xs font-semibold">
                      {inspectionOutcome(inspection.isOperational)}
                    </span>
                  </div>
                  <p className="text-sm text-[var(--text-secondary)]">
                    {excerpt(inspection.remarks)}
                  </p>
                  <Link
                    to="/app/inspections/$inspectionId"
                    params={{ inspectionId: inspection.id }}
                    className="text-sm font-semibold text-[var(--primary)] hover:underline"
                  >
                    View details
                  </Link>
                </Card>
              )
            })}
          </div>
          {pageCount > 1 && (
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm text-[var(--text-secondary)]">
                Page {page} of {pageCount}
              </p>
              <div className="flex gap-2">
                <Button
                  type="button"
                  disabled={page === 1}
                  onClick={() => onSearchChange({ ...search, page: page - 1 })}
                >
                  Previous
                </Button>
                <Button
                  type="button"
                  disabled={page === pageCount}
                  onClick={() => onSearchChange({ ...search, page: page + 1 })}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  )
}
