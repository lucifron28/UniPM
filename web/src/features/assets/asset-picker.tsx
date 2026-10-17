import {
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { CSSProperties, KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
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
  const listboxId = useId()
  const inputId = id ?? `${listboxId}-input`
  const errorId = id ? `${id}-error` : `${listboxId}-error`
  const [searchText, setSearchText] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [isOpen, setIsOpen] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const [menuPosition, setMenuPosition] = useState<CSSProperties>({})
  const [activeOption, setActiveOption] = useState<{
    scope: string
    options: Asset[]
    index: number
  } | null>(null)
  const assets = useAssets({
    limit: 20,
    ...(eligibleForSchedule ? { status: 'Active', hasDepartment: true } : {}),
    ...(assetCategory ? { assetCategory } : {}),
    ...(department ? { department } : {}),
    ...(debouncedSearch ? { search: debouncedSearch } : {}),
  })
  const categories = useAssetCategories()
  const options = assets.data ?? EMPTY_ASSETS
  const scope = `${eligibleForSchedule}\u0000${assetCategory ?? ''}\u0000${department ?? ''}\u0000${debouncedSearch}`
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
  const selectedAssetIsIneligible = Boolean(
    eligibleForSchedule &&
    selectedAsset &&
    (selectedAsset.status !== 'Active' || !selectedAsset.department?.trim()),
  )

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(searchText.trim())
    }, 250)
    return () => window.clearTimeout(timeout)
  }, [searchText])

  useLayoutEffect(() => {
    if (!isOpen) return

    function updateMenuPosition() {
      const input = inputRef.current
      if (!input) return
      const bounds = input.getBoundingClientRect()
      const workspace = input.closest('.app-workspace')?.getBoundingClientRect()
      const topLimit = Math.max(8, workspace?.top ?? 8)
      const bottomLimit = Math.min(
        window.innerHeight - 8,
        workspace?.bottom ?? window.innerHeight - 8,
      )
      const below = bottomLimit - bounds.bottom - 4
      const above = bounds.top - topLimit - 4
      const showAbove = below < 160 && above > below
      const maxHeight = Math.max(96, Math.min(256, showAbove ? above : below))
      setMenuPosition({
        top: showAbove
          ? Math.max(topLimit, bounds.top - maxHeight - 4)
          : bounds.bottom + 4,
        left: Math.max(
          8,
          Math.min(bounds.left, window.innerWidth - bounds.width - 8),
        ),
        width: Math.min(bounds.width, window.innerWidth - 16),
        maxHeight,
      })
    }

    updateMenuPosition()
    window.addEventListener('resize', updateMenuPosition)
    document.addEventListener('scroll', updateMenuPosition, true)
    return () => {
      window.removeEventListener('resize', updateMenuPosition)
      document.removeEventListener('scroll', updateMenuPosition, true)
    }
  }, [isOpen])

  const chooseAsset = (asset: Asset) => {
    setSearchText('')
    setDebouncedSearch('')
    setActiveOption(null)
    setIsOpen(false)
    onChange(asset.id, asset)
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
        htmlFor={inputId}
        className="text-xs font-semibold text-[var(--text-secondary)]"
      >
        {label}
      </label>
      <div className="flex gap-2">
        <Input
          ref={inputRef}
          id={inputId}
          role="combobox"
          aria-label={label}
          aria-autocomplete="list"
          aria-haspopup="listbox"
          aria-expanded={isOpen}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
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
              setActiveOption(null)
            }}
            className="rounded-lg border border-[var(--border-control)] px-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)]"
          >
            Clear
          </button>
        )}
      </div>
      <p className="text-xs text-[var(--text-neutral)]">
        {eligibleForSchedule
          ? 'Only active assets with a department are available for scheduling.'
          : 'Search is limited to matching assets. Results narrow by category and department.'}
      </p>
      {error && (
        <p id={errorId} className="text-sm text-[var(--error)]" role="alert">
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
      {isOpen &&
        createPortal(
          <div
            className="fixed z-[60] overflow-auto rounded-md border border-[var(--border-soft)] bg-white p-1 shadow-md"
            style={menuPosition}
          >
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
                      aria-selected={
                        index === activeIndex || asset.id === value
                      }
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
          </div>,
          document.body,
        )}
    </div>
  )
}
