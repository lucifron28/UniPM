import { Input } from '@/components/ui/input'

export type SearchableOption = {
  value: string
  label: string
}

const suggestionLimit = 10

export function SearchableOptionInput({
  id,
  label,
  value,
  options,
  onChange,
  placeholder,
  disabled = false,
  helperText,
}: {
  id: string
  label: string
  value: string
  options: SearchableOption[]
  onChange: (value: string) => void
  placeholder: string
  disabled?: boolean
  helperText: string
}) {
  const normalizedQuery = value.trim().toLocaleLowerCase()
  const matchingOptions = normalizedQuery
    ? options.filter((option) =>
        option.label.toLocaleLowerCase().includes(normalizedQuery),
      )
    : options
  const visibleOptions = matchingOptions.slice(0, suggestionLimit)
  const suggestionsId = `${id}-suggestions`
  const helperId = `${id}-helper`

  return (
    <div className="grid gap-1 text-sm font-medium text-[var(--text-primary)]">
      <label htmlFor={id}>{label}</label>
      <Input
        id={id}
        type="text"
        list={suggestionsId}
        autoComplete="off"
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        aria-describedby={helperId}
        onChange={(event) => onChange(event.target.value)}
      />
      <datalist id={suggestionsId}>
        {visibleOptions.map((option) => (
          <option key={option.value || option.label} value={option.label} />
        ))}
      </datalist>
      <p
        id={helperId}
        className="text-xs font-normal text-[var(--text-secondary)]"
      >
        {normalizedQuery && matchingOptions.length === 0
          ? 'No matching choices. Try another name or clear the field.'
          : matchingOptions.length >= suggestionLimit
            ? `Showing up to ${suggestionLimit} matches. Type more to narrow the suggestions.`
            : helperText}
      </p>
    </div>
  )
}
