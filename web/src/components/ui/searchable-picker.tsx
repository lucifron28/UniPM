import { createPortal } from 'react-dom'
import {
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { CSSProperties, KeyboardEvent } from 'react'
import { Input } from '@/components/ui/input'

const MAX_PANEL_HEIGHT = 280
const VIEWPORT_GUTTER = 8

export type SearchablePickerOption<T = unknown> = {
  value: string
  label: string
  description?: string
  item?: T
}

type SearchablePickerProps<T> = {
  id?: string
  label: string
  value?: string | undefined
  options: readonly SearchablePickerOption<T>[]
  selectedOption?: SearchablePickerOption<T> | undefined
  allLabel?: string | undefined
  placeholder?: string | undefined
  description?: string | undefined
  disabled?: boolean | undefined
  loading?: boolean | undefined
  loadingMore?: boolean | undefined
  loadingMessage?: string | undefined
  errorMessage?: string | undefined
  emptyMessage?: string | undefined
  retryLabel?: string | undefined
  hasMore?: boolean | undefined
  clearOnSearch?: boolean | undefined
  onRetry?: (() => void) | undefined
  onLoadMore?: (() => void) | undefined
  onSearchChange?: ((search: string) => void) | undefined
  onChange: (value?: string, option?: SearchablePickerOption<T>) => void
}

type PopupPosition = CSSProperties & { maxHeight: number }

export function SearchablePicker<T>({
  id,
  label,
  value,
  options,
  selectedOption,
  allLabel,
  placeholder,
  description,
  disabled = false,
  loading = false,
  loadingMore = false,
  loadingMessage = 'Loading options…',
  errorMessage,
  emptyMessage = 'No matching options.',
  retryLabel = 'Retry',
  hasMore = false,
  clearOnSearch = false,
  onRetry,
  onLoadMore,
  onSearchChange,
  onChange,
}: SearchablePickerProps<T>) {
  const generatedId = useId()
  const inputId = id ?? `${generatedId}-input`
  const listboxId = `${inputId}-listbox`
  const descriptionId = `${inputId}-description`
  const inputRef = useRef<HTMLInputElement>(null)
  const listboxRef = useRef<HTMLDivElement>(null)
  const searchChangeRef = useRef(onSearchChange)
  const [isOpen, setIsOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [settledQuery, setSettledQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(-1)
  const [popupPosition, setPopupPosition] = useState<PopupPosition>({
    position: 'fixed',
    left: 0,
    top: 0,
    width: 0,
    maxHeight: 0,
    visibility: 'hidden',
    zIndex: 50,
  })

  useEffect(() => {
    searchChangeRef.current = onSearchChange
  }, [onSearchChange])

  useEffect(() => {
    if (!searchChangeRef.current) return
    const timeout = window.setTimeout(() => {
      const normalized = query.trim()
      setSettledQuery(normalized)
      searchChangeRef.current?.(normalized)
    }, 250)
    return () => window.clearTimeout(timeout)
  }, [query])

  const selected =
    (value && selectedOption?.value === value ? selectedOption : undefined) ??
    (value ? options.find((option) => option.value === value) : undefined)
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const filteredOptions = useMemo(() => {
    if (onSearchChange) return options
    if (!normalizedQuery) return options
    return options.filter((option) =>
      `${option.label} ${option.description ?? ''}`
        .toLocaleLowerCase()
        .includes(normalizedQuery),
    )
  }, [normalizedQuery, onSearchChange, options])
  const showAllOption = Boolean(
    allLabel &&
    (!normalizedQuery ||
      allLabel.toLocaleLowerCase().includes(normalizedQuery)),
  )
  const visibleOptions: (SearchablePickerOption<T> | null)[] = [
    ...(showAllOption ? [null] : []),
    ...filteredOptions,
  ]
  const isSearchSettling = Boolean(
    onSearchChange && query.trim() !== settledQuery,
  )
  const showLoading =
    isSearchSettling || (loading && filteredOptions.length === 0)
  const showError = Boolean(errorMessage && filteredOptions.length === 0)
  const canShowList = !showLoading && !showError && visibleOptions.length > 0
  const displayValue = isOpen ? query : (selected?.label ?? allLabel ?? '')

  const close = useCallback(() => {
    setIsOpen(false)
    setQuery('')
    setSettledQuery('')
    setActiveIndex(-1)
    searchChangeRef.current?.('')
  }, [])

  const open = useCallback(() => {
    setIsOpen(true)
    setQuery('')
    setSettledQuery('')
    setActiveIndex(-1)
    searchChangeRef.current?.('')
  }, [])

  const choose = useCallback(
    (option: SearchablePickerOption<T> | null) => {
      close()
      onChange(option?.value, option ?? undefined)
    },
    [close, onChange],
  )

  const updatePopupPosition = useCallback(() => {
    const input = inputRef.current
    if (!input) return

    const rect = input.getBoundingClientRect()
    const availableBelow = Math.max(
      0,
      window.innerHeight - rect.bottom - VIEWPORT_GUTTER * 2,
    )
    const availableAbove = Math.max(0, rect.top - VIEWPORT_GUTTER * 2)
    const opensAbove =
      availableBelow < Math.min(MAX_PANEL_HEIGHT, 180) &&
      availableAbove > availableBelow
    const availableHeight = opensAbove ? availableAbove : availableBelow
    const maxHeight = Math.min(MAX_PANEL_HEIGHT, availableHeight)
    const width = Math.min(
      rect.width,
      Math.max(0, window.innerWidth - VIEWPORT_GUTTER * 2),
    )
    const left = Math.max(
      VIEWPORT_GUTTER,
      Math.min(rect.left, window.innerWidth - VIEWPORT_GUTTER - width),
    )

    setPopupPosition({
      position: 'fixed',
      left,
      width,
      maxHeight,
      visibility: maxHeight > 0 ? 'visible' : 'hidden',
      zIndex: 50,
      ...(opensAbove
        ? { bottom: window.innerHeight - rect.top + 4 }
        : { top: rect.bottom + 4 }),
    })
  }, [])

  useLayoutEffect(() => {
    if (!isOpen) return
    updatePopupPosition()
    window.addEventListener('resize', updatePopupPosition)
    window.addEventListener('scroll', updatePopupPosition, true)
    return () => {
      window.removeEventListener('resize', updatePopupPosition)
      window.removeEventListener('scroll', updatePopupPosition, true)
    }
  }, [isOpen, updatePopupPosition])

  useEffect(() => {
    if (activeIndex < 0) return
    listboxRef.current
      ?.querySelector<HTMLElement>(`[data-option-index="${activeIndex}"]`)
      ?.scrollIntoView?.({ block: 'nearest' })
  }, [activeIndex])

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Escape') {
      if (isOpen) {
        event.preventDefault()
        close()
      }
      return
    }

    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      if (!isOpen) open()
      if (visibleOptions.length === 0) return
      setActiveIndex((current) => {
        if (event.key === 'ArrowDown') {
          return current < 0
            ? 0
            : Math.min(current + 1, visibleOptions.length - 1)
        }
        return current < 0
          ? visibleOptions.length - 1
          : Math.max(current - 1, 0)
      })
      return
    }

    if (event.key === 'Home' && isOpen && visibleOptions.length > 0) {
      event.preventDefault()
      setActiveIndex(0)
      return
    }
    if (event.key === 'End' && isOpen && visibleOptions.length > 0) {
      event.preventDefault()
      setActiveIndex(visibleOptions.length - 1)
      return
    }
    if (
      event.key === 'Enter' &&
      isOpen &&
      !isSearchSettling &&
      activeIndex >= 0 &&
      visibleOptions[activeIndex] !== undefined
    ) {
      event.preventDefault()
      choose(visibleOptions[activeIndex] ?? null)
    }
  }

  const clear = () => {
    onChange(undefined)
    inputRef.current?.focus()
    close()
  }

  const popup =
    isOpen && typeof document !== 'undefined'
      ? createPortal(
          <div
            className="overflow-hidden rounded-lg border border-[var(--border-soft)] bg-white shadow-xl"
            style={popupPosition}
            data-searchable-picker-popup
          >
            {(showLoading || showError || visibleOptions.length === 0) && (
              <div className="px-3 py-2 text-sm">
                {showLoading ? (
                  <p className="text-[var(--text-neutral)]" role="status">
                    {loadingMessage}
                  </p>
                ) : showError ? (
                  <div className="text-[var(--error)]" role="alert">
                    <p>{errorMessage}</p>
                    {onRetry && (
                      <button
                        type="button"
                        className="mt-1 font-semibold underline"
                        onMouseDown={(event) => event.preventDefault()}
                        onClick={onRetry}
                      >
                        {retryLabel}
                      </button>
                    )}
                  </div>
                ) : (
                  <p className="text-[var(--text-neutral)]" role="status">
                    {emptyMessage}
                  </p>
                )}
              </div>
            )}
            {canShowList && (
              <div
                id={listboxId}
                ref={listboxRef}
                role="listbox"
                aria-label={`Matching ${label.toLocaleLowerCase()}`}
                className="overflow-y-auto overscroll-contain py-1"
                style={{
                  maxHeight: Math.max(
                    0,
                    popupPosition.maxHeight -
                      (hasMore ? 48 : 0) -
                      (errorMessage ? 52 : 0),
                  ),
                }}
              >
                {visibleOptions.map((option, index) => {
                  const optionValue = option?.value ?? ''
                  const optionLabel = option?.label ?? allLabel ?? ''
                  const isSelected = value
                    ? optionValue === value
                    : option === null
                  return (
                    <div role="none" key={optionValue || '__all'}>
                      <button
                        id={`${listboxId}-option-${index}`}
                        type="button"
                        role="option"
                        tabIndex={-1}
                        data-option-index={index}
                        aria-selected={isSelected}
                        onMouseDown={(event) => event.preventDefault()}
                        onMouseEnter={() => setActiveIndex(index)}
                        onClick={() => choose(option)}
                        className={`w-full px-3 py-2 text-left text-sm ${
                          index === activeIndex
                            ? 'bg-[var(--page-background)]'
                            : 'hover:bg-[var(--page-background)]'
                        } ${isSelected ? 'font-semibold text-[var(--primary)]' : 'text-[var(--text-primary)]'}`}
                      >
                        <span className="block truncate">{optionLabel}</span>
                        {option?.description && (
                          <span className="mt-0.5 block truncate text-xs font-normal text-[var(--text-neutral)]">
                            {option.description}
                          </span>
                        )}
                      </button>
                    </div>
                  )
                })}
              </div>
            )}
            {errorMessage && filteredOptions.length > 0 && (
              <div
                className="border-t border-[var(--border-soft)] px-3 py-2 text-sm text-[var(--error)]"
                role="alert"
              >
                <p>{errorMessage}</p>
                {onRetry && (
                  <button
                    type="button"
                    className="mt-1 font-semibold underline"
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={onRetry}
                  >
                    {retryLabel}
                  </button>
                )}
              </div>
            )}
            {hasMore && !isSearchSettling && (
              <div className="border-t border-[var(--border-soft)] p-1">
                <button
                  type="button"
                  className="w-full rounded-md px-3 py-2 text-left text-sm font-semibold text-[var(--primary)] hover:bg-[var(--page-background)] disabled:opacity-60"
                  disabled={loadingMore}
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={onLoadMore}
                >
                  {loadingMore ? 'Loading more options…' : 'Load more options'}
                </button>
              </div>
            )}
          </div>,
          document.body,
        )
      : null

  return (
    <div className="grid min-w-0 gap-1">
      <label
        htmlFor={inputId}
        className="text-sm font-semibold text-[var(--text-primary)]"
      >
        {label}
      </label>
      <div className="flex min-w-0 gap-2">
        <Input
          ref={inputRef}
          id={inputId}
          role="combobox"
          aria-label={label}
          aria-autocomplete="list"
          aria-haspopup="listbox"
          aria-expanded={isOpen}
          aria-controls={isOpen && canShowList ? listboxId : undefined}
          aria-activedescendant={
            isOpen &&
            canShowList &&
            activeIndex >= 0 &&
            visibleOptions[activeIndex] !== undefined
              ? `${listboxId}-option-${activeIndex}`
              : undefined
          }
          aria-describedby={description ? descriptionId : undefined}
          value={displayValue}
          maxLength={256}
          placeholder={
            isOpen
              ? `Search ${label.toLocaleLowerCase()}`
              : (placeholder ?? `Select ${label.toLocaleLowerCase()}`)
          }
          disabled={disabled}
          onFocus={open}
          onBlur={() => {
            setIsOpen(false)
            setQuery('')
            setActiveIndex(-1)
            searchChangeRef.current?.('')
          }}
          onChange={(event) => {
            const nextQuery = event.target.value
            setQuery(nextQuery)
            setActiveIndex(-1)
            setIsOpen(true)
            if (clearOnSearch && value) onChange(undefined)
          }}
          onKeyDown={handleKeyDown}
          className="min-h-10 min-w-0 flex-1 px-3 text-sm"
        />
        {(value || query) && (
          <button
            type="button"
            aria-label={`Clear ${label.toLocaleLowerCase()} selection`}
            disabled={disabled}
            onMouseDown={(event) => event.preventDefault()}
            onClick={clear}
            className="shrink-0 rounded-lg border border-[var(--border-control)] px-3 text-sm font-semibold text-[var(--text-secondary)] hover:bg-[var(--page-background)] disabled:opacity-60"
          >
            Clear
          </button>
        )}
      </div>
      {description && (
        <p id={descriptionId} className="text-xs text-[var(--text-neutral)]">
          {description}
        </p>
      )}
      {popup}
    </div>
  )
}
