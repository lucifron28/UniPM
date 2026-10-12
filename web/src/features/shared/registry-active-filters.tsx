import { X } from 'lucide-react'

export type RegistryActiveFilter = {
  id: string
  label: string
  value: string
  onRemove: () => void
}

export function RegistryActiveFilters({
  filters,
}: {
  filters: RegistryActiveFilter[]
}) {
  if (filters.length === 0) return null

  return (
    <div
      aria-label="Active filters"
      className="mt-4 flex flex-wrap items-center gap-2"
      aria-live="polite"
    >
      <span className="text-sm font-semibold text-[var(--text-secondary)]">
        Active filters:
      </span>
      {filters.map((filter) => (
        <span
          key={filter.id}
          className="inline-flex items-center gap-1 rounded-full border border-[var(--border-soft)] bg-[var(--page-background)] py-1 pr-1 pl-3 text-sm text-[var(--text-primary)]"
        >
          <span>
            {filter.label}: {filter.value}
          </span>
          <button
            type="button"
            aria-label={`Remove ${filter.label} filter`}
            onClick={filter.onRemove}
            className="inline-flex size-7 items-center justify-center rounded-full text-[var(--text-neutral)] hover:bg-white hover:text-[var(--text-primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
          >
            <X aria-hidden="true" className="size-4" />
          </button>
        </span>
      ))}
    </div>
  )
}
