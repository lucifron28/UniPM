export function formatScheduleDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeZone: 'Asia/Manila',
  }).format(new Date(value))
}

export function getCurrentManilaYear(now = new Date()) {
  return Number(
    new Intl.DateTimeFormat('en', {
      timeZone: 'Asia/Manila',
      year: 'numeric',
    }).format(now),
  )
}

const pmCyclePattern = /^(\d{4})-(0[1-9]|1[0-2])$/

function getPmCycleParts(value: string | null | undefined) {
  const match = pmCyclePattern.exec(value ?? '')
  return match ? { year: Number(match[1]), month: Number(match[2]) } : null
}

export function formatPmCycle(value: string | null | undefined) {
  const cycle = getPmCycleParts(value)
  if (!cycle) return 'Not recorded'

  return new Intl.DateTimeFormat(undefined, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(cycle.year, cycle.month - 1, 1)))
}

export function getPmCycleDueDate(value: string | null | undefined) {
  const cycle = getPmCycleParts(value)
  return cycle ? new Date(Date.UTC(cycle.year, cycle.month, 0, 12)) : null
}

export function formatPmCycleDueDate(value: string | null | undefined) {
  const dueDate = getPmCycleDueDate(value)
  if (!dueDate) return 'Not recorded'

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeZone: 'Asia/Manila',
  }).format(dueDate)
}

export function formatMonthName(month: number) {
  if (!Number.isInteger(month) || month < 1 || month > 12) return ''

  return new Intl.DateTimeFormat(undefined, {
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(2000, month - 1, 1)))
}

export function formatScheduleDateTime(value: string | null) {
  return value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
        timeZone: 'Asia/Manila',
      }).format(new Date(value))
    : 'Not recorded'
}

export function formatAssignmentPeriodDateTime(value: string | null) {
  if (!value) return 'Not recorded'

  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return 'Not recorded'

  const parts = new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
    timeZone: 'Asia/Manila',
  }).formatToParts(date)
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? ''

  return `${part('day')} ${part('month')} ${part('year')}, ${part('hour')}:${part('minute')}:${part('second')} Asia/Manila`
}

export function formatAssignmentPeriodStatus(value: string | null | undefined) {
  switch (value) {
    case 'Unavailable':
      return 'Assignment period unavailable'
    case 'WithinPeriod':
      return 'Within 30 calendar days'
    case 'Overdue':
      return 'Overdue and unfinished'
    case 'CompletedWithinPeriod':
      return 'Completed within 30 calendar days'
    case 'CompletedLate':
      return 'Completed after 30 calendar days'
    case 'Cancelled':
      return 'Schedule cancelled'
    default:
      return value
        ? value.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
        : 'Not available'
  }
}

export function toDateTimeLocal(value: string | undefined) {
  if (!value) return ''
  const date = new Date(value)
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function fromDateTimeLocal(value: string) {
  return value ? new Date(value).toISOString() : undefined
}
