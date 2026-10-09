import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  type RowData,
  useReactTable,
} from '@tanstack/react-table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { Asset } from '@/features/assets/asset-contract'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import { useAssetCategories, useAssets } from '@/features/assets/asset-queries'
import { categoryLabel } from '@/features/assets/asset-presentation'
import type { Inspection } from '@/features/inspections/inspection-contract'
import { useInspections } from '@/features/inspections/inspection-queries'
import {
  excerpt,
  formatInspectionDate,
  inspectionOutcome,
} from '@/features/inspections/inspection-presentation'
import { useSchedules } from '@/features/schedules/schedule-queries'
import {
  formatPmCycle,
  formatPmCycleDueDate,
} from '@/features/schedules/schedule-presentation'
import {
  RegistryLoadingPanel,
  RegistryResultsPanel,
} from '@/features/shared/registry-results-panel'
import {
  fromDateTimeLocal,
  toDateTimeLocal,
} from '@/features/schedules/schedule-presentation'

export type InspectionSearch = {
  assetId?: string | undefined
  scheduleId?: string | undefined
  assetCategory?: Asset['assetCategory'] | undefined
  department?: string | undefined
  search?: string | undefined
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
const createColumns = (search: InspectionSearch) => [
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
    header: 'Scheduled month / due date',
    cell: ({ getValue, table }) => {
      const schedule = table.options.meta?.schedules.get(getValue())
      return schedule ? (
        <div>
          <p>{formatPmCycle(schedule.pmCycle)}</p>
          <p className="text-xs text-[var(--text-neutral)]">
            Due date: {formatPmCycleDueDate(schedule.pmCycle)}
          </p>
        </div>
      ) : (
        getValue()
      )
    },
  }),
  columnHelper.accessor('dateInspected', {
    header: 'Actual inspection date',
    cell: ({ getValue }) => formatInspectionDate(getValue()),
  }),
  columnHelper.accessor('isOperational', {
    header: 'Recorded result',
    cell: ({ getValue }) => (
      <Badge variant={getValue() ? 'success' : 'danger'}>
        {inspectionOutcome(getValue())}
      </Badge>
    ),
  }),
  columnHelper.accessor('remarks', {
    header: 'Remarks',
    cell: ({ getValue }) => excerpt(getValue()),
  }),
  columnHelper.accessor('actionsRecommendations', {
    header: 'Recommendation',
    cell: ({ getValue }) => excerpt(getValue()),
  }),
  columnHelper.display({
    id: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <Link
        to="/app/inspections/$inspectionId"
        params={{ inspectionId: row.original.id }}
        search={{
          ...search,
          returnContext: { kind: 'inspectionRegistry', search },
        }}
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
    schedules: Map<string, { pmCycle?: string | undefined }>
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
    options?: { replace?: boolean; preserveScroll?: boolean },
  ) => void
}) {
  const assets = useAssets()
  const categories = useAssetCategories()
  const schedules = useSchedules()
  const allInspections = useInspections()
  const [draft, setDraft] = useState<Omit<InspectionSearch, 'page'>>(() => ({
    assetId: search.assetId,
    scheduleId: search.scheduleId,
    assetCategory: search.assetCategory,
    department: search.department,
    search: search.search,
    isOperational: search.isOperational,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
  }))

  useEffect(() => {
    setDraft({
      assetId: search.assetId,
      scheduleId: search.scheduleId,
      assetCategory: search.assetCategory,
      department: search.department,
      search: search.search,
      isOperational: search.isOperational,
      dateFrom: search.dateFrom,
      dateTo: search.dateTo,
    })
  }, [
    search.assetId,
    search.scheduleId,
    search.assetCategory,
    search.department,
    search.search,
    search.isOperational,
    search.dateFrom,
    search.dateTo,
  ])

  const filteredInspections = useInspections({
    ...(search.assetId ? { assetId: search.assetId } : {}),
    ...(search.scheduleId ? { scheduleId: search.scheduleId } : {}),
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.search ? { search: search.search } : {}),
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
          { pmCycle: schedule.pmCycle },
        ]),
      ),
    [schedules.data],
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
  const dateRangeIsValid =
    !draft.dateFrom || !draft.dateTo || draft.dateFrom <= draft.dateTo
  const pageSize = 10
  const pageCount = Math.max(1, Math.ceil(records.length / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = useMemo(
    () => records.slice((page - 1) * pageSize, page * pageSize),
    [page, records],
  )

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
    meta: { assets: assetMap, schedules: scheduleMap },
  })

  useEffect(() => {
    if (
      filteredInspections.isSuccess &&
      !filteredInspections.isPlaceholderData &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true, preserveScroll: true },
      )
    }
  }, [
    filteredInspections.isSuccess,
    filteredInspections.isPlaceholderData,
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
        <form
          className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <Input
            aria-label="Search inspections"
            placeholder="Asset code, location, remarks, or recommendation"
            value={draft.search ?? ''}
            maxLength={256}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                search: event.target.value,
              }))
            }
            className="md:col-span-2"
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
            aria-label="Schedule"
            value={draft.scheduleId ?? ''}
            disabled={schedules.isError}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                scheduleId: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All schedules</option>
            {(schedules.data ?? []).map((schedule) => (
              <option key={schedule.id} value={schedule.id}>
                {schedule.asset?.assetCode ?? schedule.assetId} -{' '}
                {formatPmCycle(schedule.pmCycle)} · Due{' '}
                {formatPmCycleDueDate(schedule.pmCycle)}
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
            aria-label="Recorded operational result"
            value={
              draft.isOperational === undefined
                ? ''
                : draft.isOperational
                  ? 'true'
                  : 'false'
            }
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                isOperational:
                  event.target.value === ''
                    ? undefined
                    : event.target.value === 'true',
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All recorded results</option>
            <option value="true">Operational</option>
            <option value="false">Not operational</option>
          </select>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            Inspected from
            <input
              type="datetime-local"
              value={toDateTimeLocal(draft.dateFrom)}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  dateFrom: fromDateTimeLocal(event.target.value),
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <label className="grid gap-1 text-xs font-semibold text-[var(--text-secondary)]">
            Inspected to
            <input
              type="datetime-local"
              value={toDateTimeLocal(draft.dateTo)}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  dateTo: fromDateTimeLocal(event.target.value),
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
            />
          </label>
          <div className="flex flex-wrap items-end gap-2 md:col-span-2 xl:col-span-4">
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
        {(assets.isError || categories.isError || schedules.isError) && (
          <div
            role="alert"
            className="mt-3 flex flex-wrap gap-3 text-sm text-[var(--error)]"
          >
            {assets.isError && (
              <Button type="button" onClick={() => void assets.refetch()}>
                Retry asset context
              </Button>
            )}
            {categories.isError && (
              <Button type="button" onClick={() => void categories.refetch()}>
                Retry category options
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

      {records.length === 0 &&
        filteredInspections.isFetching &&
        !filteredInspections.isPending && (
          <p
            className="min-h-5 text-sm text-[var(--text-neutral)]"
            role="status"
          >
            Updating results...
          </p>
        )}
      {filteredInspections.isPending ? (
        <RegistryLoadingPanel breakpoint="md" viewportSize="standard">
          <span className="sr-only">Loading inspections...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </RegistryLoadingPanel>
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
        <RegistryResultsPanel
          label="Inspections"
          breakpoint="md"
          viewportSize="standard"
          isUpdating={filteredInspections.isFetching}
          desktopContent={
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
                      <td key={cell.id} className="px-5 py-2 align-top">
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
          mobileContent={pageData.map((inspection) => {
            const asset = assetMap.get(inspection.assetId)
            return (
              <Card key={inspection.id} className="space-y-3 shadow-none">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">
                      {asset?.assetCode ?? inspection.assetId}
                    </p>
                    <p className="text-sm text-[var(--text-secondary)]">
                      Actual inspection date:{' '}
                      {formatInspectionDate(inspection.dateInspected)}
                    </p>
                  </div>
                  <Badge
                    variant={inspection.isOperational ? 'success' : 'danger'}
                  >
                    {inspectionOutcome(inspection.isOperational)}
                  </Badge>
                </div>
                <p className="text-sm text-[var(--text-secondary)]">
                  {excerpt(inspection.remarks)}
                </p>
                <Link
                  to="/app/inspections/$inspectionId"
                  params={{ inspectionId: inspection.id }}
                  search={{
                    ...search,
                    returnContext: { kind: 'inspectionRegistry', search },
                  }}
                  className="text-sm font-semibold text-[var(--primary)] hover:underline"
                >
                  View details
                </Link>
              </Card>
            )
          })}
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
