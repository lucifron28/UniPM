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
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { Asset, AssetCategory } from '@/features/assets/asset-contract'
import { assetStatusCodes } from '@/features/assets/asset-contract'
import { useAssetCategories, useAssets } from '@/features/assets/asset-queries'
import {
  assetSearchText,
  categoryLabel,
} from '@/features/assets/asset-presentation'
import { useCurrentUser } from '@/features/auth/current-user'

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

const columns = [
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
    header: 'QR value',
    cell: ({ getValue }) => getValue() ?? 'Not generated',
  }),
  columnHelper.accessor('status', { header: 'Status' }),
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
        className="font-semibold text-[var(--primary)] hover:underline"
      >
        View details
      </Link>
    ),
  }),
]

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

export function AssetRegistry({
  search,
  onSearchChange,
}: {
  search: AssetSearch
  onSearchChange: (next: AssetSearch) => void
}) {
  const currentUser = useCurrentUser()
  const categories = useAssetCategories()
  const allAssets = useAssets()
  const serverFilters = {
    ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
    ...(search.status ? { status: search.status } : {}),
    ...(search.building ? { building: search.building } : {}),
    ...(search.department ? { department: search.department } : {}),
  }
  const filteredAssets = useAssets(serverFilters)
  const [text, setText] = useState(search.text ?? '')
  const [building, setBuilding] = useState(search.building ?? '')
  const [department, setDepartment] = useState(search.department ?? '')

  useEffect(() => setText(search.text ?? ''), [search.text])
  useEffect(() => setBuilding(search.building ?? ''), [search.building])
  useEffect(() => setDepartment(search.department ?? ''), [search.department])

  const canCreate = currentUser.data?.roles.includes('GSD') ?? false
  const categoryByCode = useMemo(
    () =>
      new Map(categories.data?.map((category) => [category.code, category])),
    [categories.data],
  )
  const records = filteredAssets.data ?? []
  const textFiltered = records.filter((asset) =>
    assetSearchText(asset).includes(text.trim().toLocaleLowerCase()),
  )
  const pageSize = 10
  const pageCount = Math.max(1, Math.ceil(textFiltered.length / pageSize))
  const page = Math.min(Math.max(search.page ?? 1, 1), pageCount)
  const pageData = textFiltered.slice((page - 1) * pageSize, page * pageSize)
  // TanStack Table intentionally exposes mutable table methods to the renderer.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: pageData.map((asset) => ({
      ...asset,
      category: categoryByCode.get(asset.assetCategory),
    })),
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  const buildings = [
    ...new Set(
      (allAssets.data ?? [])
        .map((asset) => asset.building)
        .filter((value): value is string => Boolean(value)),
    ),
  ].sort()
  const departments = [
    ...new Set(
      (allAssets.data ?? [])
        .map((asset) => asset.department)
        .filter((value): value is string => Boolean(value)),
    ),
  ].sort()

  const apply = () =>
    onSearchChange({
      ...search,
      text: text || undefined,
      building: building || undefined,
      department: department || undefined,
      page: 1,
    })

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
            Browse the current controlled asset registry. Search and pagination
            are handled in the browser; category, status, building, and
            department remain server filters.
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

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <SummaryCard label="All assets" count={allAssets.data?.length ?? 0} />
        {assetStatusCodes.map((status) => (
          <SummaryCard
            key={status}
            label={status}
            count={
              (allAssets.data ?? []).filter((asset) => asset.status === status)
                .length
            }
          />
        ))}
        {(categories.data ?? []).map((category) => (
          <SummaryCard
            key={category.code}
            label={category.displayName}
            count={
              (allAssets.data ?? []).filter(
                (asset) => asset.assetCategory === category.code,
              ).length
            }
          />
        ))}
      </div>

      <Card className="p-4 shadow-none">
        <form
          className="grid gap-3 md:grid-cols-2 xl:grid-cols-6"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <label className="sr-only" htmlFor="asset-search">
            Search assets
          </label>
          <div className="relative xl:col-span-2">
            <Search
              aria-hidden="true"
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--text-neutral)]"
            />
            <Input
              id="asset-search"
              value={text}
              onChange={(event) => setText(event.target.value)}
              placeholder="Asset code, QR, building, department, or location"
              className="pl-9"
            />
          </div>
          <select
            aria-label="Asset category"
            value={search.assetCategory ?? ''}
            onChange={(event) =>
              onSearchChange({
                ...search,
                assetCategory: (event.target.value || undefined) as
                  Asset['assetCategory'] | undefined,
                page: 1,
              })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All categories</option>
            {(categories.data ?? []).map((category) => (
              <option key={category.code} value={category.code}>
                {category.displayName}
              </option>
            ))}
          </select>
          <select
            aria-label="Asset status"
            value={search.status ?? ''}
            onChange={(event) =>
              onSearchChange({
                ...search,
                status: (event.target.value || undefined) as
                  Asset['status'] | undefined,
                page: 1,
              })
            }
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All statuses</option>
            {assetStatusCodes.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
          <select
            aria-label="Building"
            value={building}
            onChange={(event) => setBuilding(event.target.value)}
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All buildings</option>
            {buildings.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
          <select
            aria-label="Department"
            value={department}
            onChange={(event) => setDepartment(event.target.value)}
            className="min-h-10 rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
          >
            <option value="">All departments</option>
            {departments.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
          <div className="flex gap-2 xl:col-span-6">
            <Button type="submit">Apply filters</Button>
            <Button
              type="button"
              className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
              onClick={() => {
                setText('')
                setBuilding('')
                setDepartment('')
                onSearchChange({ page: 1 })
              }}
            >
              Clear
            </Button>
          </div>
        </form>
        {(search.assetCategory ||
          search.status ||
          search.building ||
          search.department ||
          search.text) && (
          <p
            className="mt-4 text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            Active filters:{' '}
            {[
              search.assetCategory,
              search.status,
              search.building,
              search.department,
              search.text,
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
        )}
      </Card>

      {filteredAssets.isPending ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </div>
      ) : filteredAssets.isError ? (
        <Card
          role="alert"
          className="border-[var(--error)] text-[var(--error)]"
        >
          <p>Assets could not be loaded. Please try again.</p>
          <Button
            type="button"
            className="mt-4"
            onClick={() => void filteredAssets.refetch()}
          >
            Retry
          </Button>
        </Card>
      ) : textFiltered.length === 0 ? (
        <Card>
          <h2 className="font-semibold">No assets match these filters.</h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Adjust the filters or clear them to return to the full registry.
          </p>
        </Card>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-xl border border-[var(--border-soft)] bg-white lg:block">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
                {table.getHeaderGroups().map((headerGroup) => (
                  <tr key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <th
                        key={header.id}
                        className="px-4 py-3 font-semibold text-[var(--text-primary)]"
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
                        className="px-4 py-3 text-[var(--text-secondary)]"
                      >
                        {index === 0 ? (
                          <Link
                            to="/app/assets/$assetId"
                            params={{ assetId: row.original.id }}
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
          </div>
          <div className="grid gap-3 lg:hidden">
            {pageData.map((asset) => (
              <Link
                key={asset.id}
                to="/app/assets/$assetId"
                params={{ assetId: asset.id }}
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
                <p className="mt-3 text-xs font-semibold text-[var(--text-neutral)]">
                  {asset.status}
                </p>
              </Link>
            ))}
          </div>
          <div className="flex items-center justify-between">
            <p className="text-sm text-[var(--text-secondary)]">
              Showing {Math.min((page - 1) * pageSize + 1, textFiltered.length)}
              –{Math.min(page * pageSize, textFiltered.length)} of{' '}
              {textFiltered.length} filtered assets · page {page} of {pageCount}
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                disabled={page <= 1}
                onClick={() => onSearchChange({ ...search, page: page - 1 })}
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
        </>
      )}
    </section>
  )
}
