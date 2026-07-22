export function formatInspectionDate(value: string | null | undefined) {
  if (!value) return 'Not recorded'

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function inspectionOutcome(isOperational: boolean) {
  return isOperational ? 'Operational' : 'Not operational'
}

export function excerpt(value: string | null, limit = 140) {
  if (!value?.trim()) return 'Not recorded'
  const normalized = value.replace(/\s+/g, ' ').trim()
  return normalized.length > limit
    ? `${normalized.slice(0, limit).trimEnd()}...`
    : normalized
}
