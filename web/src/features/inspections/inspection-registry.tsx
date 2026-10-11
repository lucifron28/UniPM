import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { Asset } from '@/features/assets/asset-contract'
import { AssetPicker } from '@/features/assets/asset-picker'
import { useAsset } from '@/features/assets/asset-queries'
import { categoryLabel } from '@/features/assets/asset-presentation'
import type {
  InspectionFollowUpStatus,
  InspectionRegistryItem,
} from '@/features/inspections/inspection-contract'
import {
  useInspectionRegistry,
  useInspectionRegistryOptions,
  useInspectionRegistrySummary,
} from '@/features/inspections/inspection-queries'
import {
  excerpt,
  formatInspectionDate,
  inspectionOutcome,
} from '@/features/inspections/inspection-presentation'
import {
  formatPmCycle,
  formatPmCycleDueDate,
  getCurrentManilaYear,
} from '@/features/schedules/schedule-presentation'
import {
  RegistryLoadingPanel,
  RegistryResultsPanel,
} from '@/features/shared/registry-results-panel'
import { RegistryActiveFilters } from '@/features/shared/registry-active-filters'
import type { RegistryActiveFilter } from '@/features/shared/registry-active-filters'
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
  wmsReferralStatus?: InspectionFollowUpStatus | undefined
  year?: number | undefined
  quarter?: 'Q1' | 'Q2' | 'Q3' | 'Q4' | undefined
  pmCycle?: string | undefined
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
        {count.toLocaleString()}
      </p>
    </Card>
  )
}

const columnHelper = createColumnHelper<InspectionRegistryItem>()
const createColumns = (search: InspectionSearch) => [
  columnHelper.accessor('asset', {
    header: 'Asset',
    cell: ({ row }) => (
      <div>
        {row.original.asset ? (
          <Link
            to="/app/assets/$assetId"
            params={{ assetId: row.original.asset.id }}
            search={{ returnContext: { kind: 'inspectionRegistry', search } }}
            className="font-semibold text-[var(--primary)] hover:underline"
          >
            {row.original.asset.assetCode}
          </Link>
        ) : (
          <p className="font-semibold">Asset details unavailable</p>
        )}
        <p className="text-xs text-[var(--text-neutral)]">
          {row.original.asset?.assetCategory ?? 'Category not recorded'}
        </p>
        <p className="text-xs text-[var(--text-neutral)]">
          {[row.original.asset?.department, row.original.asset?.building]
            .filter(Boolean)
            .join(' · ') || 'Location not recorded'}
        </p>
      </div>
    ),
  }),
  columnHelper.accessor('schedule', {
    header: 'Scheduled month / due date',
    cell: ({ row }) =>
      row.original.schedule ? (
        <div>
          <p>{formatPmCycle(row.original.schedule.pmCycle)}</p>
          <p className="text-xs text-[var(--text-neutral)]">
            Due date: {formatPmCycleDueDate(row.original.schedule.pmCycle)}
          </p>
        </div>
      ) : (
        'Schedule details unavailable'
      ),
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
  columnHelper.accessor('correctiveFollowUpStatus', {
    header: 'Corrective follow-up',
    cell: ({ getValue, row }) => (
      <div>
        <Badge
          variant={
            getValue() === 'ReferredToWms'
              ? 'success'
              : getValue() === 'CorrectiveFollowUpPending'
                ? 'warning'
                : 'neutral'
          }
        >
          {getValue() === 'ReferredToWms'
            ? 'Referred to WMS'
            : getValue() === 'CorrectiveFollowUpPending'
              ? 'Corrective follow-up pending'
              : 'No referral required'}
        </Badge>
        {row.original.externalPmNumber && (
          <p className="mt-1 text-xs text-[var(--text-secondary)]">
            WMS PM: {row.original.externalPmNumber}
          </p>
        )}
      </div>
    ),
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
  const pageSize = 25
  const [draft, setDraft] = useState<Omit<InspectionSearch, 'page'>>(() => ({
    assetId: search.assetId,
    scheduleId: search.scheduleId,
    assetCategory: search.assetCategory,
    department: search.department,
    search: search.search,
    isOperational: search.isOperational,
    wmsReferralStatus: search.wmsReferralStatus,
    year: search.year,
    quarter: search.quarter,
    pmCycle: search.pmCycle,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
  }))
  const [filterNotice, setFilterNotice] = useState<string | null>(null)
  const filterOptions = useInspectionRegistryOptions({
    ...(draft.assetCategory ? { assetCategory: draft.assetCategory } : {}),
    ...(draft.department ? { department: draft.department } : {}),
  })
  const categories = filterOptions.data?.categories ?? []
  const departments = filterOptions.data?.departments ?? []
  const years = filterOptions.data?.years ?? []
  const summary = useInspectionRegistrySummary()
  const registry = useInspectionRegistry({
    ...(search.assetId ? { assetId: search.assetId } : {}),
    ...(search.scheduleId ? { scheduleId: search.scheduleId } : {}),
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.search ? { search: search.search } : {}),
    ...(search.isOperational === undefined
      ? {}
      : { isOperational: search.isOperational }),
    ...(search.wmsReferralStatus
      ? { wmsReferralStatus: search.wmsReferralStatus }
      : {}),
    ...(search.year === undefined ? {} : { year: search.year }),
    ...(search.quarter ? { quarter: search.quarter } : {}),
    ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
    ...(search.dateFrom ? { dateFrom: search.dateFrom } : {}),
    ...(search.dateTo ? { dateTo: search.dateTo } : {}),
    page: search.page ?? 1,
    pageSize,
  })
  const selectedAsset = useAsset(search.assetId ?? '', Boolean(search.assetId))

  useEffect(() => {
    setDraft({
      assetId: search.assetId,
      scheduleId: search.scheduleId,
      assetCategory: search.assetCategory,
      department: search.department,
      search: search.search,
      isOperational: search.isOperational,
      wmsReferralStatus: search.wmsReferralStatus,
      year: search.year,
      quarter: search.quarter,
      pmCycle: search.pmCycle,
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
    search.wmsReferralStatus,
    search.year,
    search.quarter,
    search.pmCycle,
    search.dateFrom,
    search.dateTo,
  ])

  const records = registry.data?.items ?? []
  const totalCount = registry.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const page = Math.min(Math.max(search.page ?? 1, 1), pageCount)
  const yearOptions = Array.from(
    new Set([
      ...years,
      ...(draft.year ? [String(draft.year)] : []),
      ...(draft.pmCycle ? [draft.pmCycle.slice(0, 4)] : []),
    ]),
  ).sort((left, right) => Number(right) - Number(left))
  const selectedCategory = categories.find(
    (category) => category.code === draft.assetCategory,
  )
  const allowedMonths = Array.from(
    { length: 12 },
    (_, index) => index + 1,
  ).filter(
    (month) =>
      (!selectedCategory || selectedCategory.scheduledMonths.includes(month)) &&
      (!draft.quarter ||
        `Q${Math.floor((month - 1) / 3) + 1}` === draft.quarter),
  )
  const dateRangeIsValid =
    !draft.dateFrom || !draft.dateTo || draft.dateFrom <= draft.dateTo
  const pmCycleYear = draft.pmCycle
    ? Number(draft.pmCycle.slice(0, 4))
    : undefined
  const pmCycleQuarter = draft.pmCycle
    ? (`Q${Math.floor((Number(draft.pmCycle.slice(5, 7)) - 1) / 3) + 1}` as const)
    : undefined
  const pmCycleMatchesPeriod =
    !draft.pmCycle ||
    ((!draft.year || pmCycleYear === draft.year) &&
      (!draft.quarter || pmCycleQuarter === draft.quarter))

  const changePage = (nextPage: number) => {
    onSearchChange(
      { ...search, page: nextPage > 1 ? nextPage : undefined },
      { preserveScroll: true },
    )
  }

  const columns = useMemo(() => createColumns(search), [search])
  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: records,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  useEffect(() => {
    if (
      registry.isSuccess &&
      !registry.isPlaceholderData &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true, preserveScroll: true },
      )
    }
  }, [
    registry.isSuccess,
    registry.isPlaceholderData,
    onSearchChange,
    pageCount,
    search,
  ])

  useEffect(() => {
    if (
      !draft.department ||
      !filterOptions.isSuccess ||
      filterOptions.isFetching ||
      departments.includes(draft.department)
    ) {
      return
    }
    setDraft((current) => ({
      ...current,
      department: undefined,
      assetId: undefined,
    }))
    setFilterNotice(
      'The department and specific asset were cleared because they do not match the selected category.',
    )
  }, [
    departments,
    draft.department,
    filterOptions.isFetching,
    filterOptions.isSuccess,
  ])

  useEffect(() => {
    if (!draft.pmCycle) return
    const month = Number(draft.pmCycle.slice(5, 7))
    if (allowedMonths.includes(month)) return
    setDraft((current) => ({ ...current, pmCycle: undefined }))
    setFilterNotice(
      'The PM month was cleared because it does not match the selected category or quarter.',
    )
  }, [allowedMonths, draft.pmCycle])

  const apply = () => {
    if (!dateRangeIsValid || !pmCycleMatchesPeriod) return
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
    setFilterNotice(null)
    onSearchChange({ page: 1 }, { preserveScroll: true })
  }

  const activeFilters: RegistryActiveFilter[] = [
    ...(search.assetId
      ? [
          {
            id: 'assetId',
            label: 'Specific asset',
            value: selectedAsset.data?.assetCode ?? 'Selected asset',
            onRemove: () =>
              onSearchChange(
                { ...search, assetId: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.scheduleId
      ? [
          {
            id: 'scheduleId',
            label: 'Linked schedule',
            value: 'Specific schedule',
            onRemove: () =>
              onSearchChange(
                { ...search, scheduleId: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.assetCategory
      ? [
          {
            id: 'assetCategory',
            label: 'Asset category',
            value: categoryLabel(
              categories.find((item) => item.code === search.assetCategory),
              search.assetCategory,
            ),
            onRemove: () =>
              onSearchChange(
                { ...search, assetCategory: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.department
      ? [
          {
            id: 'department',
            label: 'Department',
            value: search.department,
            onRemove: () =>
              onSearchChange(
                { ...search, department: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.search
      ? [
          {
            id: 'search',
            label: 'Search',
            value: search.search,
            onRemove: () =>
              onSearchChange(
                { ...search, search: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.isOperational === undefined
      ? []
      : [
          {
            id: 'isOperational',
            label: 'Recorded result',
            value: search.isOperational ? 'Operational' : 'Not operational',
            onRemove: () =>
              onSearchChange(
                { ...search, isOperational: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]),
    ...(search.wmsReferralStatus
      ? [
          {
            id: 'wmsReferralStatus',
            label: 'Corrective follow-up',
            value:
              search.wmsReferralStatus === 'ReferredToWms'
                ? 'Referred to WMS'
                : search.wmsReferralStatus === 'CorrectiveFollowUpPending'
                  ? 'Pending'
                  : 'No referral required',
            onRemove: () =>
              onSearchChange(
                { ...search, wmsReferralStatus: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.year === undefined
      ? []
      : [
          {
            id: 'year',
            label: 'PM calendar year',
            value: String(search.year),
            onRemove: () =>
              onSearchChange(
                { ...search, year: undefined, pmCycle: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]),
    ...(search.quarter
      ? [
          {
            id: 'quarter',
            label: 'PM calendar quarter',
            value: search.quarter,
            onRemove: () =>
              onSearchChange(
                { ...search, quarter: undefined, pmCycle: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.pmCycle
      ? [
          {
            id: 'pmCycle',
            label: 'Exact PM month',
            value: formatPmCycle(search.pmCycle),
            onRemove: () =>
              onSearchChange(
                { ...search, pmCycle: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.dateFrom
      ? [
          {
            id: 'dateFrom',
            label: 'Inspected from',
            value: formatInspectionDate(search.dateFrom),
            onRemove: () =>
              onSearchChange(
                { ...search, dateFrom: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.dateTo
      ? [
          {
            id: 'dateTo',
            label: 'Inspected to',
            value: formatInspectionDate(search.dateTo),
            onRemove: () =>
              onSearchChange(
                { ...search, dateTo: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
  ]

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
          Review acknowledged inspection outcomes and source notes. This page
          does not submit, approve, or change inspection records.
        </p>
      </div>

      {summary.isPending ? (
        <div className="grid gap-3 sm:grid-cols-3" role="status">
          <span className="sr-only">Loading inspection summary...</span>
          {Array.from({ length: 3 }, (_, index) => (
            <Card key={index} className="p-4 shadow-none">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="mt-2 h-8 w-12" />
            </Card>
          ))}
        </div>
      ) : summary.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="font-semibold text-[var(--error)]">
            Inspection summary is currently unavailable.
          </p>
          <Button
            type="button"
            className="mt-3"
            onClick={() => void summary.refetch()}
          >
            Retry summary
          </Button>
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-3">
          <SummaryCard
            label="All acknowledged records"
            count={summary.data.totalCount}
          />
          <SummaryCard
            label="Operational"
            count={summary.data.operationalCount}
          />
          <SummaryCard
            label="Not operational"
            count={summary.data.nonOperationalCount}
          />
        </div>
      )}

      <Card className="p-4 shadow-none">
        <form
          className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)] md:col-span-2">
            Search
            <Input
              aria-label="Search inspections"
              placeholder="Asset code, location, remarks, recommendation, or WMS PM number"
              value={draft.search ?? ''}
              maxLength={256}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  search: event.target.value,
                }))
              }
            />
          </label>
          <AssetPicker
            value={draft.assetId}
            assetCategory={draft.assetCategory}
            department={draft.department}
            onChange={(assetId) =>
              setDraft((current) => ({ ...current, assetId }))
            }
          />
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Asset category
            <select
              aria-label="Asset category"
              value={draft.assetCategory ?? ''}
              disabled={filterOptions.isError || filterOptions.isPending}
              onChange={(event) => {
                setDraft((current) => ({
                  ...current,
                  assetCategory: (event.target.value || undefined) as
                    Asset['assetCategory'] | undefined,
                }))
                setFilterNotice(null)
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All asset categories</option>
              {categories.map((category) => (
                <option key={category.code} value={category.code}>
                  {category.displayName}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Department
            <select
              aria-label="Department"
              value={draft.department ?? ''}
              disabled={filterOptions.isError || filterOptions.isPending}
              onChange={(event) => {
                setDraft((current) => ({
                  ...current,
                  department: event.target.value || undefined,
                }))
                setFilterNotice(null)
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All departments</option>
              {departments.map((department) => (
                <option key={department} value={department}>
                  {department}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Recorded result
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
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Corrective follow-up
            <select
              aria-label="Corrective follow-up status"
              value={draft.wmsReferralStatus ?? ''}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  wmsReferralStatus: (event.target.value || undefined) as
                    InspectionFollowUpStatus | undefined,
                }))
              }
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All follow-up statuses</option>
              <option value="NoReferralRequired">No referral required</option>
              <option value="CorrectiveFollowUpPending">
                Corrective follow-up pending
              </option>
              <option value="ReferredToWms">Referred to WMS</option>
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            PM calendar year
            <select
              aria-label="PM calendar year"
              value={draft.year ?? pmCycleYear ?? ''}
              onChange={(event) => {
                setDraft((current) => ({
                  ...current,
                  year: event.target.value
                    ? Number(event.target.value)
                    : undefined,
                  pmCycle: undefined,
                }))
                if (draft.pmCycle)
                  setFilterNotice(
                    'The exact PM month was cleared because the year changed.',
                  )
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All years</option>
              {yearOptions.map((year) => (
                <option key={year} value={year}>
                  {year}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            PM calendar quarter
            <select
              aria-label="PM calendar quarter"
              value={draft.quarter ?? pmCycleQuarter ?? ''}
              onChange={(event) => {
                setDraft((current) => ({
                  ...current,
                  quarter: (event.target.value ||
                    undefined) as InspectionSearch['quarter'],
                  pmCycle: undefined,
                }))
                if (draft.pmCycle)
                  setFilterNotice(
                    'The exact PM month was cleared because the quarter changed.',
                  )
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All quarters</option>
              {['Q1', 'Q2', 'Q3', 'Q4'].map((quarter) => (
                <option key={quarter} value={quarter}>
                  {quarter}
                </option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Exact PM month
            <select
              aria-label="Exact PM month"
              value={draft.pmCycle?.slice(5, 7) ?? ''}
              onChange={(event) => {
                const month = Number(event.target.value)
                if (!month) {
                  setDraft((current) => ({ ...current, pmCycle: undefined }))
                  setFilterNotice(null)
                  return
                }
                const year = draft.year ?? getCurrentManilaYear()
                const quarter =
                  `Q${Math.floor((month - 1) / 3) + 1}` as NonNullable<
                    InspectionSearch['quarter']
                  >
                setDraft((current) => ({
                  ...current,
                  year,
                  quarter,
                  pmCycle: `${year}-${String(month).padStart(2, '0')}`,
                }))
                setFilterNotice(null)
              }}
              className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All months</option>
              {allowedMonths.map((month) => (
                <option key={month} value={String(month).padStart(2, '0')}>
                  {new Intl.DateTimeFormat(undefined, {
                    month: 'long',
                    timeZone: 'UTC',
                  }).format(new Date(Date.UTC(2024, month - 1, 1)))}
                </option>
              ))}
            </select>
          </label>
          <details className="rounded-lg border border-[var(--border-soft)] p-3 md:col-span-2 xl:col-span-4">
            <summary className="cursor-pointer text-sm font-semibold text-[var(--text-primary)]">
              Actual inspection dates (advanced)
            </summary>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
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
            </div>
          </details>
          <div className="flex flex-wrap items-end gap-2 md:col-span-2 xl:col-span-4">
            <Button
              type="submit"
              disabled={!dateRangeIsValid || !pmCycleMatchesPeriod}
            >
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
        {!pmCycleMatchesPeriod && (
          <p className="mt-3 text-sm text-[var(--error)]" role="alert">
            The exact PM month must match the selected calendar year and
            quarter.
          </p>
        )}
        {filterNotice && (
          <p
            className="mt-3 text-sm text-[var(--text-secondary)]"
            role="status"
          >
            {filterNotice}
          </p>
        )}
        {filterOptions.isError && (
          <div
            role="alert"
            className="mt-3 flex flex-wrap gap-3 text-sm text-[var(--error)]"
          >
            <span>Inspection filter options are unavailable.</span>
            <Button type="button" onClick={() => void filterOptions.refetch()}>
              Retry filter options
            </Button>
          </div>
        )}
        <RegistryActiveFilters filters={activeFilters} />
      </Card>

      <p className="text-sm text-[var(--text-secondary)]" aria-live="polite">
        Filtered results: {totalCount.toLocaleString()} · Overall acknowledged
        inspection records:{' '}
        {summary.data?.totalCount.toLocaleString() ?? 'Loading'}
      </p>
      {registry.isPending ? (
        <RegistryLoadingPanel breakpoint="md" viewportSize="standard">
          <span className="sr-only">Loading inspections...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </RegistryLoadingPanel>
      ) : registry.isError ? (
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
            onClick={() => void registry.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : records.length === 0 ? (
        <Card className="p-8 text-center shadow-none">
          <h2 className="font-bold">
            {records.length === 0 && summary.data?.totalCount === 0
              ? 'No acknowledged inspection records are available yet.'
              : 'No inspection records match these filters.'}
          </h2>
        </Card>
      ) : (
        <RegistryResultsPanel
          label="Inspections"
          breakpoint="md"
          viewportSize="standard"
          isUpdating={registry.isFetching}
          desktopContent={
            <table className="w-full min-w-[960px] text-left text-sm">
              <thead className="bg-[var(--page-background)] text-xs tracking-wide text-[var(--text-neutral)] uppercase">
                {table.getHeaderGroups().map((headerGroup) => (
                  <tr key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <th key={header.id} scope="col" className="px-5 py-3">
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
          mobileContent={records.map((inspection) => (
            <Card key={inspection.id} className="space-y-3 shadow-none">
              <div className="flex items-start justify-between gap-3">
                <div>
                  {inspection.asset ? (
                    <Link
                      to="/app/assets/$assetId"
                      params={{ assetId: inspection.asset.id }}
                      search={{
                        returnContext: { kind: 'inspectionRegistry', search },
                      }}
                      className="font-semibold text-[var(--primary)] hover:underline"
                    >
                      {inspection.asset.assetCode}
                    </Link>
                  ) : (
                    <p className="font-semibold">Asset details unavailable</p>
                  )}
                  <p className="text-xs text-[var(--text-neutral)]">
                    {inspection.asset?.assetCategory ?? 'Category not recorded'}
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
                Scheduled month: {formatPmCycle(inspection.schedule?.pmCycle)}
              </p>
              <p className="text-sm text-[var(--text-secondary)]">
                {excerpt(inspection.remarks)}
              </p>
              <Badge
                variant={
                  inspection.correctiveFollowUpStatus === 'ReferredToWms'
                    ? 'success'
                    : inspection.correctiveFollowUpStatus ===
                        'CorrectiveFollowUpPending'
                      ? 'warning'
                      : 'neutral'
                }
              >
                {inspection.correctiveFollowUpStatus === 'ReferredToWms'
                  ? 'Referred to WMS'
                  : inspection.correctiveFollowUpStatus ===
                      'CorrectiveFollowUpPending'
                    ? 'Corrective follow-up pending'
                    : 'No referral required'}
              </Badge>
              {inspection.externalPmNumber && (
                <p className="text-sm text-[var(--text-secondary)]">
                  WMS PM: {inspection.externalPmNumber}
                </p>
              )}
              <Link
                to="/app/inspections/$inspectionId"
                params={{ inspectionId: inspection.id }}
                search={{
                  ...search,
                  returnContext: { kind: 'inspectionRegistry', search },
                }}
                className="inline-block font-semibold text-[var(--primary)] hover:underline"
              >
                View details
              </Link>
            </Card>
          ))}
          pagination={{
            page,
            pageSize,
            total: totalCount,
            onPageChange: changePage,
          }}
        />
      )}
    </section>
  )
}
