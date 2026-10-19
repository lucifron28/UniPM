import { useMemo, useState } from 'react'
import {
  SearchablePicker,
  type SearchablePickerOption,
} from '@/components/ui/searchable-picker'
import {
  useAsset,
  useAssetCategories,
  useAssetRegistrySearch,
} from '@/features/assets/asset-queries'
import { categoryLabel } from '@/features/assets/asset-presentation'
import type { Asset } from '@/features/assets/asset-contract'

const EMPTY_ASSETS: Asset[] = []

function assetLabel(asset: Asset) {
  return [asset.assetCode, asset.department, asset.building, asset.location]
    .filter(Boolean)
    .join(' · ')
}

export function AssetPicker({
  value,
  assetCategory,
  department,
  eligibleForSchedule = false,
  id,
  label = 'Specific asset',
  error,
  onChange,
}: {
  value?: string | undefined
  assetCategory?: Asset['assetCategory'] | undefined
  department?: string | undefined
  eligibleForSchedule?: boolean
  id?: string
  label?: string
  error?: string | undefined
  onChange: (assetId?: string, asset?: Asset) => void
}) {
  const [search, setSearch] = useState('')
  const assets = useAssetRegistrySearch({
    ...(eligibleForSchedule ? { status: 'Active', hasDepartment: true } : {}),
    ...(assetCategory ? { assetCategory } : {}),
    ...(department ? { department } : {}),
    ...(search ? { search } : {}),
  })
  const categories = useAssetCategories()
  const options = useMemo(
    () => assets.data?.pages.flatMap((page) => page.items) ?? EMPTY_ASSETS,
    [assets.data],
  )
  const selectedFromOptions = options.find((asset) => asset.id === value)
  const selectedDetail = useAsset(
    value ?? '',
    Boolean(value && !selectedFromOptions),
  )
  const selectedAsset = selectedFromOptions ?? selectedDetail.data
  const categoryByCode = useMemo(
    () =>
      new Map(categories.data?.map((category) => [category.code, category])),
    [categories.data],
  )
  const pickerOptions = useMemo<SearchablePickerOption<Asset>[]>(
    () =>
      options.map((asset) => ({
        value: asset.id,
        label: assetLabel(asset),
        description: categoryLabel(
          categoryByCode.get(asset.assetCategory),
          asset.assetCategory,
        ),
        item: asset,
      })),
    [options, categoryByCode],
  )
  const selectedOption: SearchablePickerOption<Asset> | undefined =
    selectedAsset
      ? {
          value: selectedAsset.id,
          label: assetLabel(selectedAsset),
          description: categoryLabel(
            categoryByCode.get(selectedAsset.assetCategory),
            selectedAsset.assetCategory,
          ),
          item: selectedAsset,
        }
      : value
        ? {
            value,
            label: selectedDetail.isPending
              ? 'Loading selected asset…'
              : 'Selected asset',
          }
        : undefined
  const selectedAssetIsIncompatible = Boolean(
    selectedAsset &&
    ((assetCategory && selectedAsset.assetCategory !== assetCategory) ||
      (department &&
        selectedAsset.department?.toLocaleLowerCase() !==
          department.toLocaleLowerCase())),
  )
  const selectedAssetIsIneligible = Boolean(
    eligibleForSchedule &&
    selectedAsset &&
    (selectedAsset.status !== 'Active' || !selectedAsset.department?.trim()),
  )

  return (
    <div className="grid min-w-0 gap-1">
      <SearchablePicker
        {...(id ? { id } : {})}
        label={label}
        value={value}
        selectedOption={selectedOption}
        options={pickerOptions}
        placeholder="Search by asset code or details"
        description={
          eligibleForSchedule
            ? 'Only active assets with a department are available for scheduling.'
            : 'Search results narrow by category and department.'
        }
        loading={
          assets.isPending || (assets.isFetching && options.length === 0)
        }
        loadingMore={assets.isFetchingNextPage}
        loadingMessage="Searching assets…"
        errorMessage={
          assets.isError ? 'Asset search is unavailable.' : undefined
        }
        emptyMessage="No matching assets."
        onRetry={() => void assets.refetch()}
        hasMore={assets.hasNextPage}
        onLoadMore={() => void assets.fetchNextPage()}
        onSearchChange={setSearch}
        clearOnSearch
        onChange={(assetId, option) => {
          if (!assetId || !option?.item) {
            onChange(undefined)
            return
          }
          onChange(assetId, option.item)
        }}
      />
      {error && (
        <p className="text-sm text-[var(--error)]" role="alert">
          {error}
        </p>
      )}
      {value && selectedAssetIsIncompatible && (
        <p className="text-sm text-[var(--error)]" role="alert">
          The selected asset does not match the current category or department.
          Clear it or choose a matching asset before applying these filters.
        </p>
      )}
      {value && !selectedAsset && selectedDetail.isError && (
        <p className="text-sm text-[var(--text-secondary)]" role="status">
          The selected asset is unavailable in this registry.
        </p>
      )}
      {value && selectedAssetIsIneligible && (
        <p className="text-sm text-[var(--error)]" role="alert">
          The selected asset is no longer eligible for scheduling. Clear it or
          choose another active asset with a department.
        </p>
      )}
    </div>
  )
}
