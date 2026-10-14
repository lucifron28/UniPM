import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { Plus, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { Asset, AssetCategory } from '@/features/assets/asset-contract'
import { assetStatusCodes } from '@/features/assets/asset-contract'
import {
  useAssetCategories,
  useAssetRegistry,
  useAssetRegistryOptions,
  useAssetRegistrySummary,
} from '@/features/assets/asset-queries'
import { categoryLabel } from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  RegistryLoadingPanel,
  RegistryResultsPanel,
} from '@/features/shared/registry-results-panel'
import { RegistryActiveFilters } from '@/features/shared/registry-active-filters'
import type { RegistryActiveFilter } from '@/features/shared/registry-active-filters'

export type AssetSearch = {
  assetCategory?: Asset['assetCategory'] | undefined
  status?: Asset['status'] | undefined
  building?: string | undefined
  department?: string | undefined
  text?: string | undefined
  page?: number | undefined
}

type AssetRow = Asset & { category: AssetCategory | undefined }

const columnHelper = createColumnHelper<AssetRow>()
const EMPTY_FILTER_OPTIONS: string[] = []

const createColumns = (search: AssetSearch) => [
  columnHelper.accessor('assetCode', { header: 'Asset code' }),
  columnHelper.accessor('assetCategory', {
    header: 'Category',
    cell: ({ row, getValue }) =>
      categoryLabel(row.original.category, getValue()),
  }),
  columnHelper.accessor('location', {
    header: 'Building / location',
    cell: ({ row, getValue }) =>
      [row.original.building, getValue()].filter(Boolean).join(' · ') ||
      'Not recorded',
  }),
  columnHelper.accessor('department', {
    header: 'Department',
    cell: ({ getValue }) => getValue() ?? 'Not recorded',
  }),
  columnHelper.accessor('qrCodeValue', {
    header: 'QR label',
    cell: ({ getValue }) => (
      <Badge variant={getValue() ? 'success' : 'neutral'}>
        {getValue() ? 'Ready' : 'Unavailable'}
      </Badge>
    ),
  }),
  columnHelper.accessor('status', {
    header: 'Status',
    cell: ({ getValue }) => (
      <Badge variant={getValue() === 'Active' ? 'success' : 'neutral'}>
        {getValue()}
      </Badge>
    ),
  }),
  columnHelper.accessor('updatedAt', {
    header: 'Updated',
    cell: ({ getValue }) =>
      new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(
        new Date(getValue()),
      ),
  }),
  columnHelper.display({
    id: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <Link
        to="/app/assets/$assetId"
        params={{ assetId: row.original.id }}
        search={{ ...search, returnContext: { kind: 'assetRegistry', search } }}
        className="font-semibold text-[var(--primary)] hover:underline"
      >
        View details
      </Link>
    ),
  }),
]

function SummaryCard({
  category,
  selected,
  onSelect,
}: {
  category: {
    assetCategory: Asset['assetCategory']
    displayName: string
    totalCount: number
    activeCount: number
    inactiveCount: number
    retiredCount: number
  }
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onSelect}
      className={`rounded-xl border bg-white p-4 text-left transition-colors hover:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none ${
        selected
          ? 'border-[var(--primary)] ring-1 ring-[var(--primary)]'
          : 'border-[var(--border-soft)]'
      }`}
    >
      <span className="block text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {category.displayName}
      </span>
      <span className="mt-2 block text-2xl font-bold text-[var(--text-primary)]">
        {category.totalCount.toLocaleString()}
      </span>
      <span className="mt-1 block text-xs text-[var(--text-secondary)]">
        Overall total · Active {category.activeCount.toLocaleString()} ·
        Inactive {category.inactiveCount.toLocaleString()} · Retired{' '}
        {category.retiredCount.toLocaleString()}
      </span>
    </button>
  )
}

export function AssetRegistry({
  search,
  onSearchChange,
}: {
  search: AssetSearch
  onSearchChange: (
    next: AssetSearch,
    options?: { replace?: boolean; preserveScroll?: boolean },
  ) => void
}) {
  const currentUser = useCurrentUser()
  const categories = useAssetCategories()
  const summary = useAssetRegistrySummary()
  const pageSize = 25
  const serverFilters = {
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.status ? { status: search.status } : {}),
    ...(search.building ? { building: search.building } : {}),
    ...(search.department ? { department: search.department } : {}),
    ...(search.text ? { search: search.text } : {}),
    page: search.page ?? 1,
    pageSize,
  }
  const registry = useAssetRegistry(serverFilters)
  const [text, setText] = useState(search.text ?? '')
  const [building, setBuilding] = useState(search.building ?? '')
  const [department, setDepartment] = useState(search.department ?? '')
  const [filterNotice, setFilterNotice] = useState<string | null>(null)
  const [assetCategory, setAssetCategory] = useState<
    Asset['assetCategory'] | ''
  >(search.assetCategory ?? '')
  const [status, setStatus] = useState<Asset['status'] | ''>(
    search.status ?? '',
  )
  const filterOptions = useAssetRegistryOptions({
    ...(assetCategory ? { assetCategory } : {}),
    ...(department ? { department } : {}),
  })

  useEffect(() => setText(search.text ?? ''), [search.text])
  useEffect(() => setBuilding(search.building ?? ''), [search.building])
  useEffect(() => setDepartment(search.department ?? ''), [search.department])
  useEffect(
    () => setFilterNotice(null),
    [
      search.assetCategory,
      search.building,
      search.department,
      search.status,
      search.text,
    ],
  )
  useEffect(
    () => setAssetCategory(search.assetCategory ?? ''),
    [search.assetCategory],
  )
  useEffect(() => setStatus(search.status ?? ''), [search.status])

  const canCreate = currentUser.data?.roles.includes('GSD') ?? false
  const categoryByCode = useMemo(
    () =>
      new Map(categories.data?.map((category) => [category.code, category])),
    [categories.data],
  )

  const records = registry.data?.items ?? []
  const totalCount = registry.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = records

  const changePage = (nextPage: number) => {
    onSearchChange(
      { ...search, page: nextPage > 1 ? nextPage : undefined },
      { preserveScroll: true },
    )
  }

  useEffect(() => {
    if (
      registry.isSuccess &&
      !registry.isPlaceholderData &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        {
          ...search,
          page: pageCount > 1 ? pageCount : undefined,
        },
        { replace: true, preserveScroll: true },
      )
    }
  }, [
    registry.isSuccess,
    registry.isPlaceholderData,
    search,
    pageCount,
    onSearchChange,
  ])

  const tableColumns = useMemo(() => createColumns(search), [search])
  const tableData = useMemo(
    () =>
      pageData.map((asset) => ({
        ...asset,
        category: categoryByCode.get(asset.assetCategory),
      })),
    [pageData, categoryByCode],
  )

  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: tableData,
    columns: tableColumns,
    getCoreRowModel: getCoreRowModel(),
  })

  const buildings = filterOptions.data?.buildings ?? EMPTY_FILTER_OPTIONS
  const departments = filterOptions.data?.departments ?? EMPTY_FILTER_OPTIONS

  useEffect(() => {
    if (
      !department ||
      !filterOptions.isSuccess ||
      filterOptions.isFetching ||
      departments.includes(department)
    ) {
      return
    }
    setDepartment('')
    setFilterNotice(
      'The department filter was cleared because it does not match the selected category.',
    )
  }, [
    department,
    departments,
    filterOptions.isFetching,
    filterOptions.isSuccess,
  ])

  useEffect(() => {
    if (
      !building ||
      !filterOptions.isSuccess ||
      filterOptions.isFetching ||
      buildings.includes(building)
    ) {
      return
    }
    setBuilding('')
    setFilterNotice(
      'The building filter was cleared because it does not match the selected category or department.',
    )
  }, [building, buildings, filterOptions.isFetching, filterOptions.isSuccess])

  const apply = () =>
    onSearchChange(
      {
        assetCategory: assetCategory || undefined,
        status: status || undefined,
        text: text.trim() || undefined,
        building: building || undefined,
        department: department || undefined,
        page: 1,
      },
      { preserveScroll: true },
    )

  const clear = () => {
    setText('')
    setBuilding('')
    setDepartment('')
    setAssetCategory('')
    setStatus('')
    setFilterNotice(null)
    onSearchChange({ page: 1 }, { preserveScroll: true })
  }

  const activeFilters: RegistryActiveFilter[] = [
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
    ...(search.building
      ? [
          {
            id: 'building',
            label: 'Building',
            value: search.building,
            onRemove: () =>
              onSearchChange(
                { ...search, building: undefined, page: 1 },
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
    ...(search.text
      ? [
          {
            id: 'search',
            label: 'Search',
            value: search.text,
            onRemove: () =>
              onSearchChange(
                { ...search, text: undefined, page: 1 },
                { preserveScroll: true },
              ),
          },
        ]
      : []),
  ]

  return (
    <section aria-labelledby="assets-title" className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Asset registry
          </p>
          <h1
            id="assets-title"
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
          >
            Assets
          </h1>
          <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
            Find equipment by category, status, location, or asset code.
          </p>
        </div>
        {canCreate && (
          <Button asChild>
            <Link to="/app/assets/new">
              <Plus aria-hidden="true" className="mr-2 size-4" />
              Add asset
            </Link>
          </Button>
        )}
      </div>

      {summary.isPending ? (
        <div
          className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"
          role="status"
          aria-label="Loading summary statistics"
        >
          <span className="sr-only">Loading summary statistics...</span>
          {Array.from({ length: 4 }, (_, i) => (
            <Card key={i} className="p-4 shadow-none">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="mt-2 h-8 w-12" />
            </Card>
          ))}
        </div>
      ) : summary.isError ? (
        <Card
          role="alert"
          className="border-[var(--error)] p-4 text-[var(--error)] shadow-none"
        >
          <p className="font-semibold">
            Summary statistics are currently unavailable.
          </p>
          <Button
            type="button"
            className="mt-3 text-xs"
            onClick={() => void summary.refetch()}
          >
            Retry summary
          </Button>
        </Card>
      ) : (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            Overall by asset category
          </h2>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {summary.data.categories.map((category) => (
              <SummaryCard
                key={category.assetCategory}
                category={category}
                selected={search.assetCategory === category.assetCategory}
                onSelect={() => {
                  const nextCategory =
                    search.assetCategory === category.assetCategory
                      ? undefined
                      : category.assetCategory
                  setAssetCategory(nextCategory ?? '')
                  setStatus('')
                  setText('')
                  setBuilding('')
                  setDepartment('')
                  setFilterNotice(null)
                  onSearchChange(
                    { assetCategory: nextCategory, page: 1 },
                    { preserveScroll: true },
                  )
                }}
              />
            ))}
          </div>
        </div>
      )}

      <Card className="p-4 shadow-none">
        <form
          className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-6"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <div className="grid min-w-0 gap-1 xl:col-span-2">
            <label
              className="text-sm font-semibold text-[var(--text-primary)]"
              htmlFor="asset-search"
            >
              Search assets
            </label>
            <div className="relative min-w-0">
              <Search
                aria-hidden="true"
                className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--text-neutral)]"
              />
              <Input
                id="asset-search"
                value={text}
                maxLength={256}
                onChange={(event) => setText(event.target.value)}
                placeholder="Asset code, QR, building, department, or location"
                className="pl-9"
              />
            </div>
          </div>
          <div className="grid min-w-0 gap-1 text-sm font-semibold text-[var(--text-primary)]">
            <label htmlFor="asset-category">Asset category</label>
            {categories.isError ? (
              <div className="flex flex-col gap-1">
                <select
                  disabled
                  id="asset-category"
                  aria-label="Asset category"
                  className="min-h-10 w-full min-w-0 rounded-lg border border-[var(--error)] bg-white px-3 text-sm opacity-60"
                >
                  <option value="">Categories unavailable</option>
                </select>
                <button
                  type="button"
                  className="text-left text-xs font-semibold text-[var(--error)] underline"
                  onClick={() => void categories.refetch()}
                >
                  Retry categories
                </button>
              </div>
            ) : (
              <select
                id="asset-category"
                value={assetCategory}
                onChange={(event) => {
                  setAssetCategory(
                    (event.target.value || '') as Asset['assetCategory'] | '',
                  )
                  setFilterNotice(null)
                }}
                className="min-h-10 w-full min-w-0 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
              >
                <option value="">All categories</option>
                {(categories.data ?? []).map((category) => (
                  <option key={category.code} value={category.code}>
                    {category.displayName}
                  </option>
                ))}
              </select>
            )}
          </div>
          <label className="grid min-w-0 gap-1 text-sm font-semibold text-[var(--text-primary)]">
            <span>Status</span>
            <select
              value={status}
              onChange={(event) => {
                setStatus((event.target.value || '') as Asset['status'] | '')
                setFilterNotice(null)
              }}
              className="min-h-10 w-full min-w-0 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All statuses</option>
              {assetStatusCodes.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </label>
          <label className="grid min-w-0 gap-1 text-sm font-semibold text-[var(--text-primary)]">
            <span>Building</span>
            <select
              value={building}
              onChange={(event) => {
                setBuilding(event.target.value)
                setFilterNotice(null)
              }}
              className="min-h-10 w-full min-w-0 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All buildings</option>
              {buildings.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <label className="grid min-w-0 gap-1 text-sm font-semibold text-[var(--text-primary)]">
            <span>Department</span>
            <select
              value={department}
              onChange={(event) => {
                setDepartment(event.target.value)
                setFilterNotice(null)
              }}
              className="min-h-10 w-full min-w-0 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
            >
              <option value="">All departments</option>
              {departments.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <div className="flex gap-2 xl:col-span-6">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
          </div>
        </form>
        {filterOptions.isError && (
          <p className="mt-3 text-sm text-[var(--error)]" role="alert">
            Location filters could not be loaded.{' '}
            <button
              type="button"
              className="font-semibold underline"
              onClick={() => void filterOptions.refetch()}
            >
              Retry
            </button>
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
        <RegistryActiveFilters filters={activeFilters} />
      </Card>

      <p className="text-sm text-[var(--text-secondary)]" aria-live="polite">
        Filtered results: {totalCount.toLocaleString()} · Overall assets:{' '}
        {summary.data?.totalCount.toLocaleString() ?? 'Loading'}
      </p>
      {registry.isPending ? (
        <RegistryLoadingPanel
          breakpoint="lg"
          viewportSize="compact"
          label="Loading assets"
        >
          <span className="sr-only">Loading asset records...</span>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </RegistryLoadingPanel>
      ) : registry.isError ? (
        <Card
          role="alert"
          className="border-[var(--error)] p-4 text-[var(--error)] shadow-none"
        >
          <p className="font-semibold">
            Assets could not be loaded. Please try again.
          </p>
          <Button
            type="button"
            className="mt-3 text-xs"
            onClick={() => void registry.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : records.length === 0 && summary.data?.totalCount === 0 ? (
        <Card className="p-6 text-center shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No assets are registered yet.
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            There are currently no assets registered in the system repository.
          </p>
        </Card>
      ) : records.length === 0 ? (
        <Card className="p-6 text-center shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No assets match the current filters.
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Adjust the filters or clear them to return to the full registry.
          </p>
        </Card>
      ) : (
        <RegistryResultsPanel
          label="Assets"
          breakpoint="lg"
          viewportSize="compact"
          isUpdating={registry.isFetching}
          desktopContent={
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
                {table.getHeaderGroups().map((headerGroup) => (
                  <tr key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <th
                        key={header.id}
                        scope="col"
                        className="px-4 py-3 font-semibold whitespace-nowrap text-[var(--text-primary)]"
                      >
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
              <tbody>
                {table.getRowModel().rows.map((row) => (
                  <tr
                    key={row.id}
                    className="border-b border-[var(--border-soft)] last:border-0 hover:bg-[var(--page-background)]"
                  >
                    {row.getVisibleCells().map((cell, index) => (
                      <td
                        key={cell.id}
                        className="px-4 py-3 whitespace-nowrap text-[var(--text-secondary)]"
                      >
                        {index === 0 ? (
                          <Link
                            to="/app/assets/$assetId"
                            params={{ assetId: row.original.id }}
                            search={{
                              ...search,
                              returnContext: { kind: 'assetRegistry', search },
                            }}
                            className="font-semibold text-[var(--primary)] hover:underline"
                          >
                            {flexRender(
                              cell.column.columnDef.cell,
                              cell.getContext(),
                            )}
                          </Link>
                        ) : (
                          flexRender(
                            cell.column.columnDef.cell,
                            cell.getContext(),
                          )
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
              {pageData.map((asset) => (
                <Link
                  key={asset.id}
                  to="/app/assets/$assetId"
                  params={{ assetId: asset.id }}
                  search={{
                    ...search,
                    returnContext: { kind: 'assetRegistry', search },
                  }}
                  className="rounded-xl border border-[var(--border-soft)] bg-white p-4 shadow-sm"
                >
                  <p className="font-semibold text-[var(--primary)]">
                    {asset.assetCode}
                  </p>
                  <p className="mt-1 text-sm text-[var(--text-secondary)]">
                    {categoryLabel(
                      categoryByCode.get(asset.assetCategory),
                      asset.assetCategory,
                    )}
                  </p>
                  <p className="mt-1 text-sm text-[var(--text-neutral)]">
                    {[asset.building, asset.department, asset.location]
                      .filter(Boolean)
                      .join(' · ') || 'Not recorded'}
                  </p>
                  <div className="mt-3 flex gap-2">
                    <Badge
                      variant={
                        asset.status === 'Active' ? 'success' : 'neutral'
                      }
                    >
                      {asset.status}
                    </Badge>
                    <Badge variant={asset.qrCodeValue ? 'success' : 'neutral'}>
                      {asset.qrCodeValue ? 'QR ready' : 'QR unavailable'}
                    </Badge>
                  </div>
                </Link>
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
  )
}
