import { useEffect, useId, useMemo, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { Input } from '@/components/ui/input'
import {
  useAsset,
  useAssetCategories,
  useAssets,
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
  onChange,
}: {
  value?: string | undefined
  assetCategory?: Asset['assetCategory'] | undefined
  department?: string | undefined
  onChange: (assetId?: string) => void
}) {
  const listboxId = useId()
  const [searchText, setSearchText] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const [activeOption, setActiveOption] = useState<{
    scope: string
    options: Asset[]
    index: number
  } | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const assets = useAssets({
    limit: 20,
    ...(assetCategory ? { assetCategory } : {}),
    ...(department ? { department } : {}),
    ...(debouncedSearch ? { search: debouncedSearch } : {}),
  })
  const categories = useAssetCategories()
  const options = assets.data ?? EMPTY_ASSETS
  const scope = `${assetCategory ?? ''}\u0000${department ?? ''}\u0000${debouncedSearch}`
  const activeIndex =
    activeOption?.scope === scope && activeOption.options === options
      ? activeOption.index
      : -1
  const isSearchSettling = searchText.trim() !== debouncedSearch
  const resultsAreCurrent = !assets.isPlaceholderData && !isSearchSettling
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
  const inputValue =
    searchText || (selectedAsset ? assetLabel(selectedAsset) : '')
  const selectedAssetIsIncompatible = Boolean(
    selectedAsset &&
    ((assetCategory && selectedAsset.assetCategory !== assetCategory) ||
      (department &&
        selectedAsset.department?.toLocaleLowerCase() !==
          department.toLocaleLowerCase())),
  )

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(searchText.trim())
    }, 250)
    return () => window.clearTimeout(timeout)
  }, [searchText])

  const chooseAsset = (asset: Asset) => {
    setNotice(null)
    setSearchText('')
    setDebouncedSearch('')
    setActiveOption(null)
    setIsOpen(false)
    onChange(asset.id)
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setIsOpen(true)
      if (resultsAreCurrent && options.length > 0) {
        setActiveOption({
          scope,
          options,
          index: Math.min(activeIndex + 1, options.length - 1),
        })
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      if (resultsAreCurrent && options.length > 0) {
        setActiveOption({ scope, options, index: Math.max(activeIndex - 1, 0) })
      }
    } else if (
      event.key === 'Enter' &&
      isOpen &&
      resultsAreCurrent &&
      options[activeIndex]
    ) {
      event.preventDefault()
      chooseAsset(options[activeIndex])
    } else if (event.key === 'Escape') {
      setIsOpen(false)
      setActiveOption(null)
    }
  }

  return (
    <div className="relative grid gap-1">
      <label
        htmlFor={`${listboxId}-input`}
        className="text-xs font-semibold text-[var(--text-secondary)]"
      >
        Specific asset
      </label>
      <div className="flex gap-2">
        <Input
          id={`${listboxId}-input`}
          role="combobox"
          aria-label="Specific asset"
          aria-autocomplete="list"
          aria-haspopup="listbox"
          aria-expanded={isOpen}
          aria-controls={
            isOpen && resultsAreCurrent && options.length > 0
              ? listboxId
              : undefined
          }
          aria-activedescendant={
            isOpen &&
            resultsAreCurrent &&
            activeIndex >= 0 &&
            options[activeIndex]
              ? `${listboxId}-option-${activeIndex}`
              : undefined
          }
          value={inputValue}
          maxLength={256}
          placeholder="Search by asset code or details"
          onFocus={() => setIsOpen(true)}
          onBlur={() => {
            setIsOpen(false)
            setActiveOption(null)
          }}
          onChange={(event) => {
            setSearchText(event.target.value)
            setNotice(null)
            setActiveOption(null)
            setIsOpen(true)
            if (value) onChange(undefined)
          }}
          onKeyDown={handleKeyDown}
        />
        {(value || searchText) && (
          <button
            type="button"
            onClick={() => {
              onChange(undefined)
              setSearchText('')
              setDebouncedSearch('')
              setNotice(null)
              setActiveOption(null)
            }}
            className="rounded-lg border border-[var(--border-control)] px-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)]"
          >
            Clear
          </button>
        )}
      </div>
      <p className="text-xs text-[var(--text-neutral)]">
        Search is limited to matching assets. Results narrow by category and
        department.
      </p>
      {notice && (
        <p className="text-sm text-[var(--text-secondary)]" role="status">
          {notice}
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
      {isOpen && (
        <div className="absolute top-full z-20 mt-1 max-h-64 w-full overflow-auto rounded-lg border border-[var(--border-soft)] bg-white p-1 shadow-lg">
          {assets.isPending || !resultsAreCurrent ? (
            <p
              className="px-3 py-2 text-sm text-[var(--text-neutral)]"
              role="status"
            >
              Updating asset results…
            </p>
          ) : assets.isError ? (
            <p className="px-3 py-2 text-sm text-[var(--error)]" role="alert">
              Asset search is unavailable.
            </p>
          ) : options.length === 0 ? (
            <p className="px-3 py-2 text-sm text-[var(--text-neutral)]">
              No matching assets.
            </p>
          ) : (
            <ul id={listboxId} role="listbox" aria-label="Matching assets">
              {options.map((asset, index) => (
                <li key={asset.id} role="none">
                  <button
                    id={`${listboxId}-option-${index}`}
                    type="button"
                    role="option"
                    tabIndex={-1}
                    aria-selected={index === activeIndex || asset.id === value}
                    onMouseDown={(event) => event.preventDefault()}
                    onMouseEnter={() =>
                      setActiveOption({ scope, options, index })
                    }
                    onClick={() => chooseAsset(asset)}
                    className={`w-full rounded-md px-3 py-2 text-left text-sm ${
                      index === activeIndex
                        ? 'bg-[var(--page-background)]'
                        : 'hover:bg-[var(--page-background)]'
                    }`}
                  >
                    <span className="block font-semibold text-[var(--text-primary)]">
                      {asset.assetCode}
                    </span>
                    <span className="block text-xs text-[var(--text-neutral)]">
                      {categoryLabel(
                        categoryByCode.get(asset.assetCategory),
                        asset.assetCategory,
                      )}
                      {[asset.department, asset.building, asset.location]
                        .filter(Boolean)
                        .map((value) => ` · ${value}`)
                        .join('')}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
