import { useEffect, useMemo, useState } from 'react'
import { Eye } from 'lucide-react'
import { PageHeading } from '@/components/layout/page-heading'
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
import { useAsset } from '@/features/assets/asset-queries'
import { AssetPicker } from '@/features/assets/asset-picker'
import {
  categoryLabel,
  formatAssetCategory,
} from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  RegistryLoadingPanel,
  RegistryResultsPanel,
} from '@/features/shared/registry-results-panel'
import { RegistryActiveFilters } from '@/features/shared/registry-active-filters'
import type { RegistryActiveFilter } from '@/features/shared/registry-active-filters'
import type {
  Schedule,
  ScheduleRegistryOptions,
  ScheduleRegistryItem,
} from '@/features/schedules/schedule-contract'
import { quartersForScheduledMonths } from '@/features/schedules/schedule-contract'
import {
  useGenerateSchedules,
  useScheduleRegistry,
  useScheduleRegistryOptions,
  useScheduleRegistrySummary,
  useScheduleStatuses,
} from '@/features/schedules/schedule-queries'
import { ScheduleEnrollmentDeferralReview } from '@/features/schedules/schedule-enrollment-deferral-review'
import { ScheduleCoverageReview } from '@/features/schedules/schedule-coverage-review'
import {
  fromDateTimeLocal,
  formatPmCycle,
  formatPmCycleDueDate,
  getCurrentManilaYear,
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
  pmCycle?: string | undefined
  view?: 'assignments' | 'history' | undefined
  assignmentFilter?: 'needs-supervisor' | 'needs-inspector' | undefined
  page?: number | undefined
}

const scheduleAccessDeniedMessage =
  'Schedule reads require a GSD, Inspector, or Supervisor role. Admin is a technical system administration role and cannot read operational schedules.'

function isScheduleAccessDenied(error: unknown) {
  return error instanceof ApiError && error.status === 403
}

const columnHelper = createColumnHelper<ScheduleRegistryItem>()
const EMPTY_SCHEDULE_CATEGORIES: ScheduleRegistryOptions['categories'] = []
const EMPTY_FILTER_OPTIONS: string[] = []
function statusVariant(
  status: Schedule['status'],
): 'neutral' | 'success' | 'warning' | 'danger' {
  if (status === 'Completed') return 'success'
  if (status === 'Overdue') return 'danger'
  if (status === 'Due' || status === 'Ongoing') return 'warning'
  return 'neutral'
}

const createColumns = (search: ScheduleSearch) => [
  columnHelper.accessor('asset', {
    header: 'Asset',
    cell: ({ row }) => (
      <div>
        {row.original.asset ? (
          <Link
            to="/app/assets/$assetId"
            params={{ assetId: row.original.asset.id }}
            search={{ returnContext: { kind: 'scheduleRegistry', search } }}
            className="font-semibold text-[var(--primary)] hover:underline"
          >
            {row.original.asset.assetCode}
          </Link>
        ) : (
          <p className="font-semibold">Asset details unavailable</p>
        )}
        <p className="text-xs text-[var(--text-neutral)]">
          {row.original.asset
            ? formatAssetCategory(row.original.asset.assetCategory)
            : 'Category not recorded'}
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
        className="inline-flex min-h-9 items-center gap-2 rounded-lg border border-[color-mix(in_srgb,var(--primary)_30%,white)] bg-white px-2.5 text-xs font-semibold text-[var(--primary)] hover:border-[var(--primary)] hover:bg-[color-mix(in_srgb,var(--primary)_5%,white)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:ring-offset-2 focus-visible:outline-none"
      >
        <Eye aria-hidden="true" className="size-3.5" />
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
  const statuses = useScheduleStatuses()
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
    pmCycle: search.pmCycle,
  }))
  const [filterNotice, setFilterNotice] = useState<string | null>(null)
  const [showMoreFilters, setShowMoreFilters] = useState(() =>
    Boolean(search.assetId || search.from || search.to || search.pmCycle),
  )
  const filterOptions = useScheduleRegistryOptions({
    ...(draft.assetCategory ? { assetCategory: draft.assetCategory } : {}),
    ...(draft.department ? { department: draft.department } : {}),
  })
  const categories = filterOptions.data?.categories ?? EMPTY_SCHEDULE_CATEGORIES
  const summary = useScheduleRegistrySummary()
  const pageSize = 25
  const registryFilters = {
    ...(search.assetId ? { assetId: search.assetId } : {}),
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.search ? { search: search.search } : {}),
    ...(search.status ? { status: search.status } : {}),
    ...(search.from ? { from: search.from } : {}),
    ...(search.to ? { to: search.to } : {}),
    ...(search.quarter ? { quarter: search.quarter } : {}),
    ...(search.year ? { year: search.year } : {}),
    ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
    page: search.page ?? 1,
    pageSize,
  }
  const registry = useScheduleRegistry(registryFilters)
  const selectedAsset = useAsset(search.assetId ?? '', Boolean(search.assetId))
  const draftSelectedAsset = useAsset(
    draft.assetId ?? '',
    Boolean(draft.assetId),
  )
  const draftAssetMatchesFilters = Boolean(
    !draft.assetId ||
    (draftSelectedAsset.isSuccess &&
      (!draft.assetCategory ||
        draftSelectedAsset.data.assetCategory === draft.assetCategory) &&
      (!draft.department ||
        draftSelectedAsset.data.department?.toLocaleLowerCase() ===
          draft.department.toLocaleLowerCase())),
  )

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
      pmCycle: search.pmCycle,
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
    search.pmCycle,
  ])

  const canGenerate = currentUser.data?.roles.includes('GSD') ?? false
  const parsedGenerationYear = Number(generationYear)
  const generationYearIsValid =
    /^\d{4}$/.test(generationYear) && parsedGenerationYear === currentManilaYear
  const records = registry.data?.items ?? []
  const totalCount = registry.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = records
  const departments = filterOptions.data?.departments ?? EMPTY_FILTER_OPTIONS
  const quarterOptions = filterOptions.data?.quarters ?? EMPTY_FILTER_OPTIONS
  const yearOptions = Array.from(
    new Set([
      ...(filterOptions.data?.years ?? EMPTY_FILTER_OPTIONS),
      ...(draft.year ? [String(draft.year)] : []),
      ...(draft.pmCycle ? [draft.pmCycle.slice(0, 4)] : []),
    ]),
  ).sort((left, right) => Number(right) - Number(left))
  const categoryByCode = useMemo(
    () => new Map(categories.map((category) => [category.code, category])),
    [categories],
  )
  const selectedCategory = categories.find(
    (category) => category.code === draft.assetCategory,
  )
  const availableQuarters = useMemo<ReadonlySet<string>>(
    () =>
      selectedCategory
        ? quartersForScheduledMonths(selectedCategory.scheduledMonths)
        : new Set(quarterOptions),
    [quarterOptions, selectedCategory],
  )
  const allowedMonths = useMemo(() => {
    return Array.from({ length: 12 }, (_, index) => index + 1).filter(
      (month) =>
        (!selectedCategory ||
          selectedCategory.scheduledMonths.includes(month)) &&
        (!draft.quarter ||
          `Q${Math.floor((month - 1) / 3) + 1}` === draft.quarter),
    )
  }, [draft.quarter, selectedCategory])
  const dateRangeIsValid = !draft.from || !draft.to || draft.from <= draft.to
  const pmCycleYear = draft.pmCycle
    ? Number(draft.pmCycle.slice(0, 4))
    : undefined
  const pmCycleQuarter = draft.pmCycle
    ? (`Q${Math.floor((Number(draft.pmCycle.slice(5, 7)) - 1) / 3) + 1}` as NonNullable<
        Schedule['quarter']
      >)
    : undefined

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
    const invalidQuarter = Boolean(
      draft.quarter &&
      selectedCategory &&
      !availableQuarters.has(draft.quarter),
    )
    const invalidPmCycle = Boolean(
      draft.pmCycle &&
      !allowedMonths.includes(Number(draft.pmCycle.slice(5, 7))),
    )
    if (!invalidQuarter && !invalidPmCycle) return
    setDraft((current) => ({
      ...current,
      ...(invalidQuarter ? { quarter: undefined } : {}),
      ...(invalidPmCycle ? { pmCycle: undefined } : {}),
    }))
    setFilterNotice(
      invalidQuarter
        ? 'The quarter and PM month were cleared because the selected category has no scheduled CPMP cycle in that quarter.'
        : 'The PM month was cleared because it does not match the selected category or quarter.',
    )
  }, [
    allowedMonths,
    availableQuarters,
    draft.pmCycle,
    draft.quarter,
    selectedCategory,
  ])

  useEffect(() => {
    if (!filterOptions.isSuccess || !search.assetCategory || !search.quarter) {
      return
    }
    const appliedCategory = categories.find(
      (category) => category.code === search.assetCategory,
    )
    if (
      !appliedCategory ||
      quartersForScheduledMonths(appliedCategory.scheduledMonths).has(
        search.quarter,
      )
    ) {
      return
    }

    onSearchChange(
      { ...search, quarter: undefined, pmCycle: undefined, page: 1 },
      { replace: true, preserveScroll: true },
    )
    setFilterNotice(
      'The quarter filter was cleared because the selected category has no scheduled CPMP cycle in that quarter.',
    )
  }, [categories, filterOptions.isSuccess, onSearchChange, search])

  const pmCycleMatchesPeriod =
    !draft.pmCycle ||
    ((!draft.year || Number(draft.pmCycle.slice(0, 4)) === draft.year) &&
      (!draft.quarter ||
        `Q${Math.floor((Number(draft.pmCycle.slice(5, 7)) - 1) / 3) + 1}` ===
          draft.quarter))

  const apply = () => {
    if (!dateRangeIsValid || !pmCycleMatchesPeriod || !draftAssetMatchesFilters)
      return
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
    ...(search.assetCategory
      ? [
          {
            id: 'assetCategory',
            label: 'Asset category',
            value: categoryLabel(
              categoryByCode.get(search.assetCategory),
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
    ...(search.status
      ? [
          {
            id: 'status',
            label: 'Status',
            value: search.status,
            onRemove: () =>
              onSearchChange(
                { ...search, status: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.quarter
      ? [
          {
            id: 'quarter',
            label: 'Calendar quarter',
            value: search.quarter,
            onRemove: () =>
              onSearchChange(
                { ...search, quarter: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.year
      ? [
          {
            id: 'year',
            label: 'Calendar year',
            value: String(search.year),
            onRemove: () =>
              onSearchChange(
                { ...search, year: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.pmCycle
      ? [
          {
            id: 'pmCycle',
            label: 'PM month',
            value: formatPmCycle(search.pmCycle),
            onRemove: () =>
              onSearchChange(
                { ...search, pmCycle: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.from
      ? [
          {
            id: 'from',
            label: 'Due from',
            value: search.from.slice(0, 10),
            onRemove: () =>
              onSearchChange(
                { ...search, from: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
    ...(search.to
      ? [
          {
            id: 'to',
            label: 'Due to',
            value: search.to.slice(0, 10),
            onRemove: () =>
              onSearchChange(
                { ...search, to: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
  ]

  const summarySection = (
    <section aria-labelledby="schedule-summary-title" className="space-y-2">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h2
          id="schedule-summary-title"
          className="text-sm font-semibold text-[var(--text-primary)]"
        >
          Schedule summary
        </h2>
        <p className="text-xs text-[var(--text-secondary)]">
          Overall schedule totals, independent of registry filters.
        </p>
      </div>
      {summary.isPending ? (
        <div
          className="grid grid-cols-2 gap-2 rounded-lg border border-[var(--border-soft)] bg-white p-3 sm:grid-cols-3 xl:grid-cols-6"
          role="status"
          aria-label="Loading schedule summary"
        >
          <span className="sr-only">Loading schedule summary...</span>
          {Array.from({ length: 6 }, (_, index) => (
            <div key={index} className="space-y-2 px-2 py-1">
              <Skeleton className="h-3 w-16" />
              <Skeleton className="h-5 w-10" />
            </div>
          ))}
        </div>
      ) : summary.isError ? (
        <Card role="alert" className="border-[var(--error)] p-4 shadow-none">
          <p className="font-semibold text-[var(--error)]">
            {isScheduleAccessDenied(summary.error)
              ? scheduleAccessDeniedMessage
              : 'Schedule summary is currently unavailable.'}
          </p>
          <Button
            type="button"
            className="mt-3"
            onClick={() => {
              void summary.refetch()
            }}
          >
            Retry summary
          </Button>
        </Card>
      ) : (
        <dl
          aria-label="Overall schedule totals"
          className="grid grid-cols-2 divide-x divide-y divide-[var(--border-soft)] rounded-lg border border-[var(--border-soft)] bg-white sm:grid-cols-3 xl:grid-cols-6"
        >
          {[
            ['All schedules', summary.data.totalCount],
            ['Due', summary.data.dueCount],
            ['Ongoing', summary.data.ongoingCount],
            ['Overdue', summary.data.overdueCount],
            ['Completed', summary.data.completedCount],
            ['Cancelled', summary.data.cancelledCount],
          ].map(([label, value]) => (
            <div key={label} className="px-3 py-2">
              <dt className="text-xs text-[var(--text-neutral)]">{label}</dt>
              <dd className="mt-0.5 text-lg leading-5 font-semibold text-[var(--text-primary)]">
                {Number(value).toLocaleString()}
              </dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  )

  return (
    <section aria-labelledby="schedules-title" className="space-y-4">
      <PageHeading
        id="schedules-title"
        eyebrow="Preventive maintenance"
        title="Schedules"
        description="Review current and historical PM cycles generated from CPMP frequencies, due dates, and recorded schedule status."
        actions={
          canGenerate ? (
            <Button asChild variant="secondary">
              <a href="#schedule-management">Manage schedules</a>
            </Button>
          ) : undefined
        }
      />

      <section aria-labelledby="schedule-filters-title" className="space-y-2">
        <div>
          <h2
            id="schedule-filters-title"
            className="text-xl font-bold text-[var(--text-primary)]"
          >
            Find schedules
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Search by asset, then refine by status or PM cycle.
          </p>
        </div>
        <Card className="content-card p-3">
          <form
            className="space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              apply()
            }}
          >
            <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
              Search
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
              />
            </label>
            <div className="grid grid-cols-1 gap-3 md:grid-cols-3 xl:grid-cols-5">
              <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
                Asset category
                <select
                  aria-label="Asset category"
                  value={draft.assetCategory ?? ''}
                  disabled={filterOptions.isError || filterOptions.isPending}
                  onChange={(event) => {
                    const assetCategory = (event.target.value || undefined) as
                      Asset['assetCategory'] | undefined
                    setDraft((current) => ({
                      ...current,
                      assetCategory,
                      assetId: undefined,
                    }))
                    setFilterNotice(
                      draft.assetId
                        ? 'Specific asset was cleared because the category filter changed.'
                        : null,
                    )
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
                Schedule status
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
              </label>
              <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
                Calendar year
                <select
                  aria-label="Calendar year"
                  value={draft.year ?? pmCycleYear ?? ''}
                  onChange={(event) => {
                    setDraft((current) => ({
                      ...current,
                      year: event.target.value
                        ? Number(event.target.value)
                        : undefined,
                      pmCycle: undefined,
                    }))
                    if (draft.pmCycle) {
                      setFilterNotice(
                        'The exact PM month was cleared because the year changed.',
                      )
                    }
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
                Department
                <select
                  aria-label="Department"
                  value={draft.department ?? ''}
                  disabled={filterOptions.isError}
                  onChange={(event) => {
                    setDraft((current) => ({
                      ...current,
                      department: event.target.value || undefined,
                      assetId: undefined,
                    }))
                    setFilterNotice(
                      draft.assetId
                        ? 'Specific asset was cleared because the department filter changed.'
                        : null,
                    )
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
                Calendar quarter
                <select
                  aria-label="Calendar quarter"
                  aria-describedby="schedule-quarter-hint"
                  value={draft.quarter ?? pmCycleQuarter ?? ''}
                  onChange={(event) => {
                    setDraft((current) => ({
                      ...current,
                      quarter: (event.target.value || undefined) as
                        NonNullable<Schedule['quarter']> | undefined,
                      pmCycle: undefined,
                    }))
                    if (draft.pmCycle) {
                      setFilterNotice(
                        'The exact PM month was cleared because the quarter changed.',
                      )
                    }
                  }}
                  className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
                >
                  <option value="">All quarters</option>
                  {quarterOptions.map((quarter) => (
                    <option
                      key={quarter}
                      value={quarter}
                      disabled={!availableQuarters.has(quarter)}
                    >
                      {availableQuarters.has(quarter)
                        ? quarter
                        : `${quarter} — no scheduled CPMP cycle`}
                    </option>
                  ))}
                </select>
                <span
                  id="schedule-quarter-hint"
                  className="text-xs font-normal text-[var(--text-secondary)]"
                >
                  {draft.assetCategory
                    ? 'Disabled quarters have no scheduled cycle for this category.'
                    : 'Choose a category to limit quarters to its scheduled PM cycles.'}
                </span>
              </label>
            </div>
            <details
              open={showMoreFilters}
              onToggle={(event) => setShowMoreFilters(event.currentTarget.open)}
              className="rounded-lg border border-[var(--border-soft)] px-4 py-3"
            >
              <summary className="cursor-pointer font-semibold text-[var(--text-primary)] outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:ring-offset-2">
                More filters
              </summary>
              <div
                id="schedule-more-filters"
                className="mt-3 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3"
              >
                <AssetPicker
                  value={draft.assetId}
                  assetCategory={draft.assetCategory}
                  department={draft.department}
                  onChange={(assetId) =>
                    setDraft((current) => ({ ...current, assetId }))
                  }
                />
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
                <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
                  Exact PM month
                  <select
                    aria-label="Exact PM month"
                    value={draft.pmCycle?.slice(5, 7) ?? ''}
                    onChange={(event) => {
                      const month = Number(event.target.value)
                      if (!month) {
                        setDraft((current) => ({
                          ...current,
                          pmCycle: undefined,
                        }))
                        setFilterNotice(null)
                        return
                      }
                      const year = draft.year ?? currentManilaYear
                      const quarter =
                        `Q${Math.floor((month - 1) / 3) + 1}` as NonNullable<
                          Schedule['quarter']
                        >
                      setDraft((current) => ({
                        ...current,
                        year,
                        quarter,
                        pmCycle: `${year}-${String(month).padStart(2, '0')}`,
                      }))
                      setFilterNotice(null)
                    }}
                    className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm font-normal"
                  >
                    <option value="">All months</option>
                    {allowedMonths.map((month) => (
                      <option
                        key={month}
                        value={String(month).padStart(2, '0')}
                      >
                        {new Intl.DateTimeFormat(undefined, {
                          month: 'long',
                          timeZone: 'UTC',
                        }).format(new Date(Date.UTC(2024, month - 1, 1)))}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </details>
            <div className="flex flex-wrap items-end gap-2">
              <Button
                type="submit"
                disabled={
                  !dateRangeIsValid ||
                  !pmCycleMatchesPeriod ||
                  !draftAssetMatchesFilters
                }
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
          {draft.assetId && !draftAssetMatchesFilters && (
            <p className="mt-3 text-sm text-[var(--error)]" role="alert">
              {draftSelectedAsset.isPending
                ? 'Checking the selected asset before applying filters.'
                : 'The selected asset could not be verified or does not match the category or department. Clear it or choose another asset.'}
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
          {(filterOptions.isError || statuses.isError) && (
            <div
              className="mt-3 flex flex-wrap gap-3 text-sm text-[var(--error)]"
              role="alert"
            >
              {filterOptions.isError && (
                <Button
                  type="button"
                  onClick={() => void filterOptions.refetch()}
                >
                  Retry registry options
                </Button>
              )}
              {statuses.isError && (
                <Button type="button" onClick={() => void statuses.refetch()}>
                  Retry status options
                </Button>
              )}
            </div>
          )}
          <RegistryActiveFilters filters={activeFilters} />
        </Card>
      </section>

      {summarySection}

      <section aria-labelledby="schedule-records-title" className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2
              id="schedule-records-title"
              className="text-xl font-bold text-[var(--text-primary)]"
            >
              Schedule records
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              Filtered schedule records and their current recorded status.
            </p>
          </div>
          <p
            className="text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            Filtered results: {totalCount.toLocaleString()} · Overall schedules:{' '}
            {registry.data?.overallCount.toLocaleString() ?? 'Loading'}
          </p>
        </div>
        {registry.isPending ? (
          <RegistryLoadingPanel breakpoint="md" viewportSize="standard">
            <span className="sr-only">Loading schedules...</span>
            {Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </RegistryLoadingPanel>
        ) : registry.isError ? (
          <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
            <h2 className="font-bold text-[var(--error)]">
              Schedules unavailable
            </h2>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              {isScheduleAccessDenied(registry.error)
                ? scheduleAccessDeniedMessage
                : 'The schedule registry could not be loaded.'}
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
                ? 'No schedules are recorded yet.'
                : 'No schedules match these filters.'}
            </h2>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              {records.length === 0 && summary.data?.totalCount === 0
                ? 'An authorized schedule manager can add the first record.'
                : 'Clear the filters to return to all recorded schedules.'}
            </p>
          </Card>
        ) : (
          <RegistryResultsPanel
            label="Schedules"
            breakpoint="md"
            viewportSize="standard"
            isUpdating={registry.isFetching}
            desktopContent={
              <table className="w-full min-w-[760px] text-left text-sm">
                <thead className="bg-[var(--primary)] text-xs tracking-wide text-white uppercase">
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
                  <Card key={schedule.id} className="content-card space-y-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h2 className="font-bold">
                          {schedule.asset ? (
                            <Link
                              to="/app/assets/$assetId"
                              params={{ assetId: schedule.asset.id }}
                              search={{
                                returnContext: {
                                  kind: 'scheduleRegistry',
                                  search,
                                },
                              }}
                              className="text-[var(--primary)] hover:underline"
                            >
                              {schedule.asset.assetCode}
                            </Link>
                          ) : (
                            'Asset details unavailable'
                          )}
                        </h2>
                        <p className="text-xs text-[var(--text-neutral)]">
                          {schedule.asset
                            ? formatAssetCategory(schedule.asset.assetCategory)
                            : 'Category not recorded'}
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
                      className="inline-flex min-h-9 items-center gap-2 rounded-lg border border-[color-mix(in_srgb,var(--primary)_30%,white)] bg-white px-2.5 text-xs font-semibold text-[var(--primary)] hover:border-[var(--primary)] hover:bg-[color-mix(in_srgb,var(--primary)_5%,white)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:ring-offset-2 focus-visible:outline-none"
                    >
                      <Eye aria-hidden="true" className="size-3.5" />
                      View details
                    </Link>
                  </Card>
                ))}
              </div>
            }
            pagination={{
              page,
              pageSize,
              total: totalCount,
              onPageChange: changePage,
            }}
          />
        )}
      </section>

      {canGenerate && (
        <section
          id="schedule-management"
          aria-labelledby="schedule-management-title"
          className="space-y-5 border-t border-[var(--border-soft)] pt-8"
        >
          <div>
            <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
              GSD tools
            </p>
            <h2
              id="schedule-management-title"
              className="mt-1 text-xl font-bold text-[var(--text-primary)]"
            >
              Schedule management
            </h2>
            <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
              Generate approved schedule cycles and review coverage or deferred
              enrollment items without changing registry filters.
            </p>
          </div>

          <section
            aria-labelledby="schedule-generation-title"
            className="space-y-3"
          >
            <div>
              <h3
                id="schedule-generation-title"
                className="text-lg font-bold text-[var(--text-primary)]"
              >
                Generate missing schedules
              </h3>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                Generate only missing schedules for the current Manila calendar
                year.
              </p>
            </div>
            <Card className="p-4 shadow-none">
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
                      const summary =
                        `Year ${result.year}: created ${result.createdSchedules} schedules; ` +
                        `${result.existingSchedules} already existed; ` +
                        `${result.deferredSchedules} deferred for GSD review.`
                      const coverageReviewCount = Number(
                        result.cyclesRequiringGsdCoverageReview,
                      )
                      const coverageReview =
                        coverageReviewCount > 0
                          ? ` ${coverageReviewCount} earlier current-year cycle${coverageReviewCount === 1 ? '' : 's'} remain uncreated until GSD confirms the approved scheduling coverage start date.`
                          : ''
                      setGenerationMessage(summary + coverageReview)
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
                    min={currentManilaYear}
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
              {generationMessage && (
                <p
                  className={`mt-3 text-sm ${generationFailed ? 'text-[var(--error)]' : 'text-[var(--text-secondary)]'}`}
                  role={generationFailed ? 'alert' : 'status'}
                >
                  {generationMessage}
                </p>
              )}
            </Card>
          </section>

          <ScheduleCoverageReview />
          <ScheduleEnrollmentDeferralReview />
        </section>
      )}
    </section>
  )
}
