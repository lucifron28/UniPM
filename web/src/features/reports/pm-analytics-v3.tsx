import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from '@tanstack/react-router'
import {
  executePmAnalyticsV3Report,
  getPmAnalyticsV3Catalog,
  getPmAnalyticsV3FilterValues,
  interpretPmAnalyticsV3Question,
  previewPmAnalyticsV3Report,
} from '@/api/generated/endpoints'
import type {
  PmAnalyticsV3AggregateFilter,
  PmAnalyticsV3CatalogResponse,
  PmAnalyticsV3Column,
  PmAnalyticsV3ExecutionResponse,
  PmAnalyticsV3FilterValuesResponse,
  PmAnalyticsV3InterpretationResponse,
  PmAnalyticsV3MeasureResult,
  PmAnalyticsV3Mode,
  PmAnalyticsV3NumericOperator,
  PmAnalyticsV3Period,
  PmAnalyticsV3PeriodKind,
  PmAnalyticsV3Presentation,
  PmAnalyticsV3PreviewResponse,
  PmAnalyticsV3ReportPlan,
  PmAnalyticsV3SortDirection,
  PmAnalyticsV3Top,
} from '@/api/generated/models'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'

type Mode = PmAnalyticsV3Mode
type Presentation = PmAnalyticsV3Presentation
type PeriodKind = PmAnalyticsV3PeriodKind
type NumericOperator = PmAnalyticsV3NumericOperator
type Direction = PmAnalyticsV3SortDirection
type Period = Omit<
  PmAnalyticsV3Period,
  'year' | 'month' | 'quarter' | 'startCycle' | 'endCycle'
> & {
  basis: 'pmCycle'
  year: number | null
  month: number | null
  quarter: number | null
  startCycle: string | null
  endCycle: string | null
}
type AggregateFilter = Omit<PmAnalyticsV3AggregateFilter, 'value'> & {
  value: number
}
type Top = Omit<PmAnalyticsV3Top, 'limit'> & { limit: number }
type ReportPlan = Omit<
  PmAnalyticsV3ReportPlan,
  'groupBy' | 'period' | 'top' | 'aggregateFilters'
> & {
  groupBy: string[]
  period: Period
  top: Top | null
  aggregateFilters: AggregateFilter[]
}
type Catalog = Omit<PmAnalyticsV3CatalogResponse, 'filterFields'> & {
  filterFields: Array<
    Omit<
      PmAnalyticsV3CatalogResponse['filterFields'][number],
      'maximumLength'
    > & {
      maximumLength: number
    }
  >
}
type Column = PmAnalyticsV3Column
type MeasureResult = PmAnalyticsV3MeasureResult
type PreviewResponse = PmAnalyticsV3PreviewResponse
type ExecutionResponse = PmAnalyticsV3ExecutionResponse
type Interpretation = PmAnalyticsV3InterpretationResponse
type FilterValues = PmAnalyticsV3FilterValuesResponse

const inputClass =
  'mt-1 block min-h-10 w-full rounded-md border border-[var(--border-soft)] bg-white px-3 py-2 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)] disabled:cursor-not-allowed disabled:bg-[var(--surface-muted)]'
const checkboxClass =
  'h-4 w-4 rounded border-[var(--border-soft)] accent-[var(--primary)]'
const baseFilterFields: Array<{
  id: string
  label: string
  searchable?: boolean
}> = [
  { id: 'department', label: 'Department' },
  { id: 'building', label: 'Building' },
  { id: 'location', label: 'General location' },
  { id: 'assetCode', label: 'Asset code' },
  { id: 'assetId', label: 'Asset ID', searchable: false },
]
const defaultRecordColumns = [
  'scheduleId',
  'assetId',
  'assetCode',
  'assetCategory',
  'department',
  'building',
  'pmCycle',
  'deadline',
  'completedAt',
  'condition',
  'timeliness',
]

const measuresWithSourceRows = new Set([
  'scheduledCount',
  'completedCount',
  'completedOnTimeCount',
  'completedLateCount',
  'nonOperationalCount',
  'remainingCount',
  'missedCount',
  'progressPercent',
  'onTimeCompliancePercent',
])

function createMeasureSourcePlan(
  plan: ReportPlan,
  groupValues: Record<string, unknown> | null,
  measureId: string,
  cyclesByCategory: Record<string, string[]>,
): ReportPlan | null {
  if (!measuresWithSourceRows.has(measureId)) return null

  const baseFilters = [...plan.baseFilters]
  const matchingFilters =
    measureId === 'scheduledCount' ? [] : [...plan.matchingFilters]
  let categories = [...plan.categories]
  let period = plan.period

  const setBaseFilter = (field: string, value: unknown) => {
    const existing = baseFilters.filter((filter) => filter.field !== field)
    existing.push(
      value === null
        ? { field, operator: 'IsNull', values: [] }
        : { field, operator: 'Equal', values: [String(value)] },
    )
    baseFilters.splice(0, baseFilters.length, ...existing)
  }

  if (groupValues) {
    for (const dimension of plan.groupBy) {
      const value = groupValues[dimension]
      if (dimension === 'assetCategory') {
        if (typeof value !== 'string') return null
        categories = [value]
      } else if (dimension === 'department' || dimension === 'building') {
        setBaseFilter(dimension, value ?? null)
      } else if (dimension === 'pmCycle' && typeof value === 'string') {
        period = {
          basis: 'pmCycle',
          kind: 'Range',
          year: null,
          month: null,
          quarter: null,
          startCycle: value,
          endCycle: value,
        }
        categories = categories.filter((category) =>
          cyclesByCategory[category]?.includes(value),
        )
      } else if (dimension === 'calendarQuarter' && typeof value === 'string') {
        const match = /^(\d{4})-Q([1-4])$/.exec(value)
        if (!match) return null
        const year = Number(match[1])
        const quarter = Number(match[2])
        period = {
          basis: 'pmCycle',
          kind: 'Quarter',
          year,
          month: null,
          quarter,
          startCycle: null,
          endCycle: null,
        }
        categories = categories.filter((category) =>
          cyclesByCategory[category]?.some((cycle) => {
            const cycleQuarter =
              Math.floor((Number(cycle.slice(5, 7)) - 1) / 3) + 1
            return cycle.startsWith(`${year}-`) && cycleQuarter === quarter
          }),
        )
      } else if (dimension === 'calendarYear') {
        const year = Number(value)
        if (!Number.isInteger(year)) return null
        period = {
          basis: 'pmCycle',
          kind: 'Year',
          year,
          month: null,
          quarter: null,
          startCycle: null,
          endCycle: null,
        }
        categories = categories.filter((category) =>
          cyclesByCategory[category]?.some((cycle) =>
            cycle.startsWith(`${year}-`),
          ),
        )
      }
    }
  }

  if (categories.length === 0) return null

  const addMatchingFilter = (field: string, values: string[]) => {
    const existing = matchingFilters.find((filter) => filter.field === field)
    if (!existing) {
      matchingFilters.push({
        field,
        operator: values.length > 1 ? 'In' : 'Equal',
        values,
      })
      return true
    }
    if (existing.operator === 'IsNull') return false
    const intersection = (existing.values ?? []).filter((value) =>
      values.includes(value),
    )
    if (intersection.length === 0) return false
    matchingFilters.splice(matchingFilters.indexOf(existing), 1, {
      field,
      operator: intersection.length > 1 ? 'In' : 'Equal',
      values: intersection,
    })
    return true
  }

  const measureCondition: Record<string, [string, string[]]> = {
    completedCount: ['condition', ['Operational', 'NonOperational']],
    completedOnTimeCount: ['timeliness', ['OnTime']],
    completedLateCount: ['timeliness', ['Late']],
    nonOperationalCount: ['condition', ['NonOperational']],
    remainingCount: ['timeliness', ['Remaining']],
    missedCount: ['timeliness', ['Missed']],
    progressPercent: ['condition', ['Operational', 'NonOperational']],
    onTimeCompliancePercent: ['timeliness', ['OnTime']],
  }
  const condition = measureCondition[measureId]
  if (condition && !addMatchingFilter(condition[0], condition[1])) return null

  return {
    ...plan,
    mode: 'Records',
    categories,
    period,
    measures: [],
    groupBy: [],
    baseFilters,
    matchingFilters,
    aggregateFilters: [],
    orderBy: [],
    top: null,
    presentation: 'Table',
    columns: defaultRecordColumns,
  }
}

function manilaParts() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Manila',
    year: 'numeric',
    month: 'numeric',
  }).formatToParts(new Date())
  return {
    year: Number(parts.find((part) => part.type === 'year')?.value),
    month: Number(parts.find((part) => part.type === 'month')?.value),
  }
}

function initialPlan(): ReportPlan {
  const { year } = manilaParts()
  return {
    schemaVersion: 3,
    catalogVersion: 1,
    subject: 'pmExecution',
    mode: 'Aggregate',
    period: {
      basis: 'pmCycle',
      kind: 'Year',
      year,
      month: null,
      quarter: null,
      startCycle: null,
      endCycle: null,
    },
    categories: [],
    measures: [],
    groupBy: [],
    baseFilters: [],
    matchingFilters: [],
    aggregateFilters: [],
    orderBy: [],
    top: null,
    presentation: 'Summary',
    columns: [],
  }
}

function normalizeCatalog(value: PmAnalyticsV3CatalogResponse): Catalog {
  return {
    ...value,
    filterFields: value.filterFields.map((field) => ({
      ...field,
      maximumLength: Number(field.maximumLength),
    })),
  }
}

function normalizePlan(value: PmAnalyticsV3ReportPlan): ReportPlan {
  return {
    ...value,
    schemaVersion: Number(value.schemaVersion),
    catalogVersion: Number(value.catalogVersion),
    period: {
      ...value.period,
      basis: 'pmCycle',
      year: value.period.year == null ? null : Number(value.period.year),
      month: value.period.month == null ? null : Number(value.period.month),
      quarter:
        value.period.quarter == null ? null : Number(value.period.quarter),
      startCycle: value.period.startCycle ?? null,
      endCycle: value.period.endCycle ?? null,
    },
    groupBy: value.groupBy ?? [],
    baseFilters: value.baseFilters.map((filter) => ({
      ...filter,
      values: filter.values ?? [],
    })),
    matchingFilters: value.matchingFilters.map((filter) => ({
      ...filter,
      values: filter.values ?? [],
    })),
    aggregateFilters: value.aggregateFilters.map((filter) => ({
      ...filter,
      value: Number(filter.value),
    })),
    top:
      value.top === null
        ? null
        : { ...value.top, limit: Number(value.top.limit) },
  }
}

function getErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    const code = error.problem?.extensions?.code
    if (code === 'PreviewUnavailable') {
      return 'This preview expired or is no longer available. Create and review a new preview before running the report.'
    }
    if (code === 'PreviewStale') {
      return 'The selected report values changed. Update the plan and create a new preview before running it.'
    }
    if (code === 'ProviderUnavailable' || code === 'Timeout') {
      return 'Question interpretation is unavailable. You can complete the report plan below and preview it manually.'
    }
    if (code === 'InvalidOutput') {
      return 'The question could not be interpreted safely. Edit the report plan or try a more specific question.'
    }
    if (code === 'ExecutionLimitExceeded') {
      return 'This report exceeds the current row limit. Narrow the period or add a population filter, then preview again.'
    }
    const validation = error.problem?.errors
      ? Object.values(error.problem.errors).flat().join(' ')
      : ''
    if (validation) return validation
    const detail = error.problem?.detail || error.message
    const extensions = error.problem?.extensions
    if (extensions?.code === 'UnmeasurableThreshold') {
      const groups = Array.isArray(extensions.groups)
        ? extensions.groups.filter(
            (group): group is string => typeof group === 'string',
          )
        : []
      const totalGroupCount =
        typeof extensions.totalGroupCount === 'number'
          ? extensions.totalGroupCount
          : groups.length
      if (groups.length === 0) return detail
      const remainder =
        totalGroupCount > groups.length
          ? ` Showing ${groups.length} of ${totalGroupCount} affected groups.`
          : ''
      return `${detail}${remainder} Affected groups: ${groups.join('; ')}.`
    }
    return detail
  }
  return 'The request could not be completed. Please try again.'
}

function formatLabel(value: string | null | undefined, catalog?: Catalog) {
  if (!value) return 'Unknown'
  const entry = [
    ...(catalog?.categories ?? []),
    ...(catalog?.measures ?? []),
    ...(catalog?.dimensions ?? []),
    ...(catalog?.recordColumns ?? []),
  ].find((item) => item.id === value)
  if (entry) return entry.label
  const labels: Record<string, string> = {
    OnTime: 'On time',
    Late: 'Late',
    NonOperational: 'Non-operational',
    NotInspected: 'Not inspected',
    Operational: 'Operational',
    Remaining: 'Remaining',
    Missed: 'Missed',
    Future: 'Future',
    Draft: 'Draft',
    Submitted: 'Submitted',
    Acknowledged: 'Acknowledged',
    Due: 'Due',
    Ongoing: 'Ongoing',
    Completed: 'Completed',
    Overdue: 'Overdue',
  }
  return labels[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatNumber(value: number | string | null | undefined) {
  if (value === null || value === undefined) return 'Not measurable'
  return new Intl.NumberFormat('en-PH', { maximumFractionDigits: 2 }).format(
    Number(value),
  )
}

function formatValue(value: unknown, column: Column, catalog: Catalog) {
  if (value === null || value === undefined || value === '')
    return 'Not recorded'
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (typeof value === 'number') {
    return formatNumber(value) + (column.unit === 'percent' ? '%' : '')
  }
  if (typeof value === 'string') {
    if (column.valueType === 'date-time') {
      const date = new Date(value)
      if (!Number.isNaN(date.getTime())) {
        return new Intl.DateTimeFormat('en-PH', {
          dateStyle: 'medium',
          timeStyle: 'short',
          timeZone: 'Asia/Manila',
        }).format(date)
      }
    }
    return formatLabel(value, catalog)
  }
  return String(value)
}

function planPeriodDescription(period: Period) {
  if (period.kind === 'Year') return String(period.year ?? '')
  if (period.kind === 'Month') {
    const month = period.month
      ? new Intl.DateTimeFormat('en-PH', { month: 'long' }).format(
          new Date(Date.UTC(2020, period.month - 1, 1)),
        )
      : 'month'
    return `${month} ${period.year ?? ''}`.trim()
  }
  if (period.kind === 'Quarter')
    return `Q${period.quarter ?? ''} ${period.year ?? ''}`.trim()
  return `${period.startCycle ?? 'start'} through ${period.endCycle ?? 'end'} PM cycle`
}

function isPeriodReady(period: Period) {
  if (period.kind === 'Range') {
    const start = /^(\d{4})-(0[1-9]|1[0-2])$/.exec(period.startCycle ?? '')
    const end = /^(\d{4})-(0[1-9]|1[0-2])$/.exec(period.endCycle ?? '')
    if (!start || !end) return false
    const startYear = Number(start[1])
    const endYear = Number(end[1])
    const startMonth = Number(start[2])
    const endMonth = Number(end[2])
    const months = (endYear - startYear) * 12 + endMonth - startMonth + 1
    return startYear >= 2000 && endYear <= 9998 && months >= 1 && months <= 12
  }
  if (period.year === null || period.year < 2000 || period.year > 9998) {
    return false
  }
  if (period.kind === 'Month')
    return period.month !== null && period.month >= 1 && period.month <= 12
  if (period.kind === 'Quarter')
    return period.quarter !== null && period.quarter >= 1 && period.quarter <= 4
  return true
}

function findFilter(plan: ReportPlan, field: string) {
  return plan.baseFilters.find((filter) => filter.field === field)
}

function findMatchingFilter(plan: ReportPlan, field: string) {
  return plan.matchingFilters.find((filter) => filter.field === field)
}

function BaseFilterPicker({
  field,
  label,
  categories,
  values,
  isNull,
  maximumLength,
  allowNull,
  searchable = true,
  disabled,
  onChange,
}: {
  field: string
  label: string
  categories: string[]
  values: string[]
  isNull: boolean
  maximumLength: number
  allowNull: boolean
  searchable?: boolean
  disabled: boolean
  onChange: (values: string[], isNull: boolean) => void
}) {
  const [search, setSearch] = useState('')
  const [options, setOptions] = useState<{
    queryKey: string
    values: FilterValues
  } | null>(null)
  const [failedQueryKey, setFailedQueryKey] = useState<string | null>(null)
  const categoryKey = categories.join(',')
  const trimmedSearch = search.trim()
  const shouldSearch =
    searchable &&
    !isNull &&
    !disabled &&
    categories.length > 0 &&
    trimmedSearch.length >= 2
  const queryKey = JSON.stringify([field, categoryKey, trimmedSearch])
  const currentOptions =
    shouldSearch && options?.queryKey === queryKey ? options.values : null
  const failed = shouldSearch && failedQueryKey === queryKey
  const listId = `pm-v3-${field}-catalog-values`

  useEffect(() => {
    if (!shouldSearch) return

    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      void getPmAnalyticsV3FilterValues(
        { field, categories, search: trimmedSearch },
        controller.signal,
      )
        .then((result) => {
          if (!controller.signal.aborted) {
            setOptions({ queryKey, values: result })
            setFailedQueryKey(null)
          }
        })
        .catch(() => {
          if (!controller.signal.aborted) setFailedQueryKey(queryKey)
        })
    }, 250)

    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [categories, categoryKey, field, queryKey, shouldSearch, trimmedSearch])

  const addValue = () => {
    const candidate = search.trim()
    if (
      !candidate ||
      values.length >= 20 ||
      values.some((value) => value.toLowerCase() === candidate.toLowerCase())
    )
      return
    onChange([...values, candidate], false)
    setSearch('')
  }

  return (
    <div className="space-y-2">
      <Label htmlFor={`pm-v3-base-${field}`}>{label}</Label>
      <div className="flex gap-2">
        <input
          id={`pm-v3-base-${field}`}
          className={inputClass}
          value={search}
          maxLength={maximumLength}
          list={searchable ? listId : undefined}
          disabled={disabled || isNull || values.length >= 20}
          onChange={(event) => {
            setSearch(event.target.value)
            setFailedQueryKey(null)
          }}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault()
              addValue()
            }
          }}
          placeholder={
            searchable ? 'Search current asset values' : 'Enter an asset GUID'
          }
          autoComplete="off"
        />
        <Button
          type="button"
          variant="secondary"
          disabled={disabled || isNull || !search.trim() || values.length >= 20}
          onClick={addValue}
        >
          Add
        </Button>
      </div>
      {searchable && (
        <datalist id={listId}>
          {currentOptions?.values.map((option) => (
            <option key={option} value={option} />
          ))}
        </datalist>
      )}
      {values.length > 0 && (
        <ul className="flex flex-wrap gap-2">
          {values.map((value) => (
            <li
              key={value}
              className="inline-flex items-center gap-2 rounded-full border border-[var(--border-soft)] px-2 py-1 text-xs text-[var(--text-primary)]"
            >
              {value}
              <button
                type="button"
                aria-label={`Remove ${value}`}
                className="font-bold text-[var(--text-secondary)]"
                onClick={() =>
                  onChange(
                    values.filter((item) => item !== value),
                    false,
                  )
                }
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      )}
      {allowNull && (
        <label className="flex items-center gap-2 text-xs text-[var(--text-secondary)]">
          <input
            className={checkboxClass}
            type="checkbox"
            checked={isNull}
            disabled={disabled || values.length > 0}
            onChange={(event) => {
              if (event.target.checked) setSearch('')
              onChange([], event.target.checked)
            }}
          />
          Unknown value only
        </label>
      )}
      <p className="min-h-4 text-xs text-[var(--text-neutral)]">
        {failed
          ? 'Catalog suggestions are unavailable; values are checked when previewing.'
          : currentOptions?.hasMore
            ? 'More values match. Add letters to narrow the search.'
            : searchable &&
                search.trim().length >= 2 &&
                currentOptions?.values.length === 0
              ? 'No current catalog value matches this search.'
              : values.length > 1
                ? 'The population includes rows matching any selected value.'
                : 'Values are resolved against current asset records.'}
      </p>
    </div>
  )
}

export function PmAnalyticsV3Panel() {
  const [catalog, setCatalog] = useState<Catalog | null>(null)
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const [catalogLoading, setCatalogLoading] = useState(true)
  const [catalogAttempt, setCatalogAttempt] = useState(0)
  const [plan, setPlan] = useState<ReportPlan>(initialPlan)
  const [question, setQuestion] = useState('')
  const [interpretation, setInterpretation] = useState<Interpretation | null>(
    null,
  )
  const [preview, setPreview] = useState<PreviewResponse | null>(null)
  const [response, setResponse] = useState<ExecutionResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [pendingLabel, setPendingLabel] = useState('')
  const [drilldownNotice, setDrilldownNotice] = useState<string | null>(null)
  const activeController = useRef<AbortController | null>(null)
  const requestId = useRef(0)

  useEffect(() => {
    const controller = new AbortController()
    void getPmAnalyticsV3Catalog(controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          const currentCatalog = normalizeCatalog(value)
          setCatalog(currentCatalog)
          setPlan((current) => ({
            ...current,
            schemaVersion: currentCatalog.schemaVersion,
            catalogVersion: currentCatalog.catalogVersion,
            subject: currentCatalog.subject,
          }))
          setCatalogLoading(false)
        }
      })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setCatalogError(getErrorMessage(loadError))
          setCatalogLoading(false)
        }
      })
    return () => controller.abort()
  }, [catalogAttempt])

  useEffect(() => () => activeController.current?.abort(), [])

  const categories = catalog?.categories ?? []
  const measures = catalog?.measures ?? []
  const dimensions = catalog?.dimensions ?? []
  const recordColumns = catalog?.recordColumns ?? []
  const outcomeFilterFields = (catalog?.filterFields ?? [])
    .filter((field) => !field.isBaseFilter)
    .map((field) => ({
      id: field.id,
      label: field.label,
      values: field.allowedValues ?? [],
      allowUnknown: field.operators?.includes('IsNull') ?? false,
    }))
  const percentageSelected = measures.some(
    (measure) =>
      plan.measures.includes(measure.id) && measure.unit === 'percent',
  )
  const matchingFiltersConflict =
    percentageSelected && plan.matchingFilters.length > 0
  const selectedMeasureDefinitions = measures.filter((measure) =>
    plan.measures.includes(measure.id),
  )
  const cancelPending = () => {
    requestId.current += 1
    activeController.current?.abort()
    activeController.current = null
    setPending(false)
    setPendingLabel('')
  }

  const updatePlan = (next: ReportPlan) => {
    cancelPending()
    setPlan(next)
    setPreview(null)
    setResponse(null)
    setError(null)
    setInterpretation(null)
    setDrilldownNotice(null)
  }

  const handleMeasureSources = (
    groupValues: Record<string, unknown> | null,
    measureId: string,
    cyclesByCategory: Record<string, string[]>,
  ) => {
    const sourcePlan = createMeasureSourcePlan(
      plan,
      groupValues,
      measureId,
      cyclesByCategory,
    )
    if (!sourcePlan) {
      setError('This group has no compatible source-record view.')
      return
    }

    const measureLabel =
      measures.find((measure) => measure.id === measureId)?.label ?? measureId
    setQuestion('')
    updatePlan(sourcePlan)
    setDrilldownNotice(
      `A records plan for ${measureLabel} is ready. Preview it to review the contributing source records.`,
    )
  }

  const beginRequest = (label: string) => {
    activeController.current?.abort()
    const controller = new AbortController()
    activeController.current = controller
    requestId.current += 1
    const activeId = requestId.current
    setPending(true)
    setPendingLabel(label)
    setError(null)
    return {
      controller,
      activeId,
      isCurrent: () =>
        requestId.current === activeId && !controller.signal.aborted,
    }
  }

  const handleInterpret = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const cleanQuestion = question.trim()
    if (!cleanQuestion) return
    setPreview(null)
    setResponse(null)
    setInterpretation(null)
    const request = beginRequest('Interpreting the question')
    try {
      const result = await interpretPmAnalyticsV3Question(
        { question: cleanQuestion },
        request.controller.signal,
      )
      if (!request.isCurrent()) return
      setInterpretation(result)
      if (result.plan) setPlan(normalizePlan(result.plan))
    } catch (requestError) {
      if (request.isCurrent()) setError(getErrorMessage(requestError))
    } finally {
      if (request.isCurrent()) {
        activeController.current = null
        setPending(false)
        setPendingLabel('')
      }
    }
  }

  const handlePreview = async () => {
    const request = beginRequest('Preparing the preview')
    setPreview(null)
    setResponse(null)
    try {
      const result = await previewPmAnalyticsV3Report(
        { plan },
        request.controller.signal,
      )
      if (request.isCurrent()) setPreview(result)
    } catch (requestError) {
      if (request.isCurrent()) setError(getErrorMessage(requestError))
    } finally {
      if (request.isCurrent()) {
        activeController.current = null
        setPending(false)
        setPendingLabel('')
      }
    }
  }

  const executePage = async (page: number) => {
    if (!preview) return
    const request = beginRequest('Running the confirmed report')
    try {
      const result = await executePmAnalyticsV3Report(
        { previewId: preview.previewId, page, pageSize: 25 },
        request.controller.signal,
      )
      if (request.isCurrent()) setResponse(result)
    } catch (requestError) {
      if (request.isCurrent()) {
        const code =
          requestError instanceof ApiError
            ? requestError.problem?.extensions?.code
            : undefined
        if (code === 'PreviewUnavailable' || code === 'PreviewStale') {
          setPreview(null)
          setResponse(null)
        }
        setError(getErrorMessage(requestError))
      }
    } finally {
      if (request.isCurrent()) {
        activeController.current = null
        setPending(false)
        setPendingLabel('')
      }
    }
  }

  const toggleCategory = (category: string) => {
    const next = plan.categories.includes(category)
      ? plan.categories.filter((value) => value !== category)
      : [...plan.categories, category]
    updatePlan({ ...plan, categories: next })
  }

  const setPeriodKind = (kind: PeriodKind) => {
    const { year, month } = manilaParts()
    const nextPeriod: Period = {
      basis: 'pmCycle',
      kind,
      year: kind === 'Range' ? null : (plan.period.year ?? year),
      month: kind === 'Month' ? (plan.period.month ?? month) : null,
      quarter: kind === 'Quarter' ? (plan.period.quarter ?? 1) : null,
      startCycle:
        kind === 'Range' ? (plan.period.startCycle ?? `${year}-01`) : null,
      endCycle:
        kind === 'Range' ? (plan.period.endCycle ?? `${year}-12`) : null,
    }
    updatePlan({ ...plan, period: nextPeriod })
  }

  const updateBaseFilterValues = (
    field: string,
    values: string[],
    isNull: boolean,
  ) => {
    const existing = plan.baseFilters.filter((filter) => filter.field !== field)
    if (isNull) {
      existing.push({ field, operator: 'IsNull', values: [] })
    } else if (values.length > 0) {
      existing.push({
        field,
        operator: values.length > 1 ? 'In' : 'Equal',
        values,
      })
    }
    updatePlan({ ...plan, baseFilters: existing })
  }

  const toggleMeasure = (measure: string) => {
    const selected = plan.measures.includes(measure)
    const nextMeasures = selected
      ? plan.measures.filter((value) => value !== measure)
      : [...plan.measures, measure]
    const nextColumns = selected
      ? plan.columns.filter((value) => value !== measure)
      : [...plan.columns, measure]
    updatePlan({
      ...plan,
      measures: nextMeasures,
      columns: nextColumns,
      aggregateFilters: selected
        ? plan.aggregateFilters.filter((filter) => filter.measure !== measure)
        : plan.aggregateFilters,
      top: plan.top?.measure === measure ? null : plan.top,
      orderBy: plan.orderBy.filter((order) =>
        nextColumns.includes(order.field),
      ),
    })
  }

  const toggleDimension = (dimension: string) => {
    const selected = plan.groupBy.includes(dimension)
    const nextGroups = selected
      ? plan.groupBy.filter((value) => value !== dimension)
      : [...plan.groupBy, dimension]
    const nextColumns = selected
      ? plan.columns.filter((value) => value !== dimension)
      : [...plan.columns, dimension]
    updatePlan({
      ...plan,
      groupBy: nextGroups,
      columns: nextColumns,
      presentation: nextGroups.length > 0 ? 'Table' : plan.presentation,
      orderBy: plan.orderBy.filter((order) =>
        nextColumns.includes(order.field),
      ),
    })
  }

  const changeMode = (mode: Mode) => {
    const next: ReportPlan =
      mode === 'Records'
        ? {
            ...plan,
            mode,
            measures: [],
            groupBy: [],
            aggregateFilters: [],
            orderBy: [],
            top: null,
            presentation: 'Table',
            columns: defaultRecordColumns,
          }
        : {
            ...plan,
            mode,
            measures: [],
            groupBy: [],
            matchingFilters: [],
            aggregateFilters: [],
            orderBy: [],
            top: null,
            presentation: 'Summary',
            columns: [],
          }
    updatePlan(next)
  }

  const setMatchingValues = (field: string, values: string[]) => {
    const fieldDefinition = outcomeFilterFields.find(
      (item) => item.id === field,
    )
    const existing = plan.matchingFilters.filter(
      (filter) => filter.field !== field,
    )
    if (
      values.includes('__unknown__') &&
      fieldDefinition?.allowUnknown &&
      values.length === 1
    ) {
      existing.push({ field, operator: 'IsNull', values: [] })
    } else if (values.length > 0 && !values.includes('__unknown__')) {
      existing.push({
        field,
        operator: values.length > 1 ? 'In' : 'Equal',
        values,
      })
    }
    updatePlan({ ...plan, matchingFilters: existing })
  }

  const setAggregateFilter = (index: number, nextFilter: AggregateFilter) => {
    const next = [...plan.aggregateFilters]
    next[index] = nextFilter
    updatePlan({ ...plan, aggregateFilters: next })
  }

  const addAggregateFilter = () => {
    const firstMeasure = plan.measures[0]
    if (!firstMeasure || plan.aggregateFilters.length >= 4) return
    updatePlan({
      ...plan,
      aggregateFilters: [
        ...plan.aggregateFilters,
        { measure: firstMeasure, operator: 'GreaterThanOrEqual', value: 0 },
      ],
    })
  }

  const setTopEnabled = (enabled: boolean) => {
    if (!enabled) {
      updatePlan({ ...plan, top: null })
      return
    }
    const measure = plan.measures[0]
    if (!measure) return
    updatePlan({
      ...plan,
      top: { measure, limit: 5, direction: 'Descending' },
    })
  }

  const planReady =
    catalog !== null &&
    plan.categories.length > 0 &&
    plan.measures.length <= 4 &&
    plan.groupBy.length <= 2 &&
    !matchingFiltersConflict &&
    (plan.mode === 'Records' || plan.measures.length > 0) &&
    plan.columns.length > 0 &&
    (plan.mode === 'Records' ||
      plan.columns.some((column) => plan.measures.includes(column))) &&
    isPeriodReady(plan.period) &&
    (interpretation === null || interpretation.status === 'Valid') &&
    plan.aggregateFilters.every((filter) => {
      const measure = measures.find((item) => item.id === filter.measure)
      return (
        Number.isFinite(filter.value) &&
        filter.value >= 0 &&
        (measure?.unit !== 'percent' || filter.value <= 100) &&
        (measure?.valueType !== 'integer' || Number.isInteger(filter.value))
      )
    }) &&
    (plan.top === null ||
      (plan.groupBy.length > 0 &&
        plan.top.limit >= 1 &&
        plan.top.limit <= 20 &&
        plan.measures.includes(plan.top.measure))) &&
    (plan.presentation !== 'Summary' ||
      (plan.mode === 'Aggregate' && plan.groupBy.length === 0))

  const interpretationDescription = interpretation?.clarificationFields.length
    ? interpretation.clarificationFields
        .map((field) => {
          const prompts: Record<string, string> = {
            Categories: 'asset category',
            Period: 'report period',
            Measures: 'measure',
            Mode: 'report type',
            GroupBy: 'grouping',
            Presentation: 'presentation',
            Department: 'department',
            Building: 'building',
            Location: 'location',
            AssetCode: 'asset code',
            AssetId: 'asset',
            Filters: 'record filters',
            Thresholds: 'threshold measure',
            Top: 'ranking measure and limit',
            Columns: 'output columns',
          }
          return prompts[field] ?? field.replace(/([a-z])([A-Z])/g, '$1 $2')
        })
        .join(', ')
    : null

  return (
    <div className="space-y-4">
      <Card className="space-y-4 p-4 shadow-none sm:p-5">
        <div>
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            Build a PM report
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
            Describe a question to fill the report plan, or use the controls
            below. Review the scope before running it.
          </p>
        </div>

        <form
          className="max-w-4xl space-y-2"
          onSubmit={(event) => void handleInterpret(event)}
        >
          <Label htmlFor="pm-analytics-v3-question">Describe the report</Label>
          <textarea
            id="pm-analytics-v3-question"
            className={`${inputClass} min-h-20 resize-y`}
            value={question}
            maxLength={512}
            onChange={(event) => {
              cancelPending()
              setQuestion(event.target.value)
              setInterpretation(null)
              setPreview(null)
              setResponse(null)
              setError(null)
              setDrilldownNotice(null)
            }}
            placeholder="Which departments had 100% fire-extinguisher compliance in 2025?"
          />
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-xs text-[var(--text-neutral)]">
              Interpretation prepares a plan only. It does not run a report.
            </p>
            <Button
              type="submit"
              variant="secondary"
              disabled={pending || catalog === null || !question.trim()}
            >
              {pending && pendingLabel === 'Interpreting the question'
                ? 'Interpreting…'
                : 'Fill plan from question'}
            </Button>
          </div>
        </form>

        {catalogError && (
          <div
            role="alert"
            className="flex flex-wrap items-center gap-3 text-sm text-[var(--error)]"
          >
            <p>The report catalog could not be loaded: {catalogError}</p>
            <Button
              type="button"
              variant="secondary"
              disabled={catalogLoading}
              onClick={() => {
                setCatalogError(null)
                setCatalogLoading(true)
                setCatalogAttempt((attempt) => attempt + 1)
              }}
            >
              Retry catalog
            </Button>
          </div>
        )}
        {catalogLoading && !catalog && (
          <p role="status" className="text-sm text-[var(--text-secondary)]">
            Loading the report catalog…
          </p>
        )}
        {drilldownNotice && (
          <p
            role="status"
            className="rounded-md bg-[var(--surface-muted)] p-3 text-sm text-[var(--text-secondary)]"
          >
            {drilldownNotice}
          </p>
        )}
        {interpretation && (
          <div
            role="status"
            className="rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3 text-sm"
          >
            <p className="font-semibold text-[var(--text-primary)]">
              {interpretation.status === 'Valid'
                ? 'A report plan is ready to review.'
                : interpretation.status === 'NeedsClarification'
                  ? 'The question needs more detail.'
                  : 'The question is outside the supported report scope.'}
            </p>
            {interpretationDescription && (
              <p className="mt-1 text-[var(--text-secondary)]">
                Please clarify: {interpretationDescription}.
              </p>
            )}
            {interpretation.detail && (
              <p className="mt-1 text-[var(--text-secondary)]">
                {interpretation.detail}
              </p>
            )}
            {interpretation.status !== 'Valid' && (
              <p className="mt-1 text-xs text-[var(--text-neutral)]">
                Complete or adjust the controls below, then preview the plan.
              </p>
            )}
          </div>
        )}
      </Card>

      <Card className="space-y-6 p-4 shadow-none sm:p-5">
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <Label htmlFor="pm-v3-mode">Report type</Label>
            <select
              id="pm-v3-mode"
              className={inputClass}
              value={plan.mode}
              onChange={(event) => changeMode(event.target.value as Mode)}
            >
              <option value="Aggregate">Measures and groups</option>
              <option value="Records">Matching records</option>
            </select>
          </div>
          <div>
            <Label htmlFor="pm-v3-period-kind">Scheduled PM cycle period</Label>
            <select
              id="pm-v3-period-kind"
              className={inputClass}
              value={plan.period.kind}
              onChange={(event) =>
                setPeriodKind(event.target.value as PeriodKind)
              }
            >
              <option value="Month">Month</option>
              <option value="Quarter">Quarter</option>
              <option value="Year">Year</option>
              <option value="Range">Cycle range</option>
            </select>
          </div>
        </div>

        {plan.period.kind !== 'Range' ? (
          <div className="grid gap-4 md:grid-cols-3">
            <div>
              <Label htmlFor="pm-v3-period-year">Year</Label>
              <input
                id="pm-v3-period-year"
                className={inputClass}
                type="number"
                min={2000}
                max={9998}
                value={plan.period.year ?? ''}
                onChange={(event) =>
                  updatePlan({
                    ...plan,
                    period: {
                      ...plan.period,
                      year: Number(event.target.value) || null,
                    },
                  })
                }
              />
            </div>
            {plan.period.kind === 'Month' && (
              <div>
                <Label htmlFor="pm-v3-period-month">Month</Label>
                <select
                  id="pm-v3-period-month"
                  className={inputClass}
                  value={plan.period.month ?? ''}
                  onChange={(event) =>
                    updatePlan({
                      ...plan,
                      period: {
                        ...plan.period,
                        month: Number(event.target.value) || null,
                      },
                    })
                  }
                >
                  <option value="">Choose a month</option>
                  {Array.from({ length: 12 }, (_, index) => index + 1).map(
                    (month) => (
                      <option key={month} value={month}>
                        {new Intl.DateTimeFormat('en-PH', {
                          month: 'long',
                        }).format(new Date(Date.UTC(2020, month - 1, 1)))}
                      </option>
                    ),
                  )}
                </select>
              </div>
            )}
            {plan.period.kind === 'Quarter' && (
              <div>
                <Label htmlFor="pm-v3-period-quarter">Quarter</Label>
                <select
                  id="pm-v3-period-quarter"
                  className={inputClass}
                  value={plan.period.quarter ?? ''}
                  onChange={(event) =>
                    updatePlan({
                      ...plan,
                      period: {
                        ...plan.period,
                        quarter: Number(event.target.value) || null,
                      },
                    })
                  }
                >
                  <option value="">Choose a quarter</option>
                  {[1, 2, 3, 4].map((quarter) => (
                    <option key={quarter} value={quarter}>
                      Q{quarter}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <Label htmlFor="pm-v3-range-start">First cycle (YYYY-MM)</Label>
              <input
                id="pm-v3-range-start"
                className={inputClass}
                type="month"
                min="2000-01"
                max="9998-12"
                value={plan.period.startCycle ?? ''}
                onChange={(event) =>
                  updatePlan({
                    ...plan,
                    period: {
                      ...plan.period,
                      startCycle: event.target.value || null,
                    },
                  })
                }
              />
            </div>
            <div>
              <Label htmlFor="pm-v3-range-end">Last cycle (YYYY-MM)</Label>
              <input
                id="pm-v3-range-end"
                className={inputClass}
                type="month"
                min="2000-01"
                max="9998-12"
                value={plan.period.endCycle ?? ''}
                onChange={(event) =>
                  updatePlan({
                    ...plan,
                    period: {
                      ...plan.period,
                      endCycle: event.target.value || null,
                    },
                  })
                }
              />
            </div>
          </div>
        )}

        <fieldset className="space-y-2">
          <legend className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase">
            Asset categories
          </legend>
          <p className="text-xs text-[var(--text-neutral)]">
            Choose every category to include. Each category has its own
            scheduled PM cycles.
          </p>
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            {categories.map((category) => (
              <label
                key={category.id}
                className="flex min-h-10 items-center gap-2 rounded-md border border-[var(--border-soft)] px-3 text-sm"
              >
                <input
                  className={checkboxClass}
                  type="checkbox"
                  checked={plan.categories.includes(category.id)}
                  onChange={() => toggleCategory(category.id)}
                />
                <span>{category.label}</span>
              </label>
            ))}
          </div>
        </fieldset>

        {plan.mode === 'Aggregate' && (
          <>
            <fieldset className="space-y-2">
              <legend className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase">
                Measures
              </legend>
              <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                {measures.map((measure) => (
                  <label
                    key={measure.id}
                    className="flex min-h-10 items-start gap-2 rounded-md border border-[var(--border-soft)] px-3 py-2 text-sm"
                  >
                    <input
                      className={`${checkboxClass} mt-0.5`}
                      type="checkbox"
                      checked={plan.measures.includes(measure.id)}
                      disabled={
                        plan.measures.length >= 4 &&
                        !plan.measures.includes(measure.id)
                      }
                      onChange={() => toggleMeasure(measure.id)}
                    />
                    <span>
                      <span className="block font-medium">{measure.label}</span>
                      <span className="text-xs text-[var(--text-neutral)]">
                        {measure.definition}
                      </span>
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>

            <fieldset className="space-y-2">
              <legend className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase">
                Group results by
              </legend>
              <p className="text-xs text-[var(--text-neutral)]">
                Choose up to two dimensions. Current department and building
                values reflect current asset records.
              </p>
              <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                {dimensions.map((dimension) => (
                  <label
                    key={dimension.id}
                    className="flex min-h-10 items-center gap-2 rounded-md border border-[var(--border-soft)] px-3 text-sm"
                  >
                    <input
                      className={checkboxClass}
                      type="checkbox"
                      checked={plan.groupBy.includes(dimension.id)}
                      disabled={
                        plan.groupBy.length >= 2 &&
                        !plan.groupBy.includes(dimension.id)
                      }
                      onChange={() => toggleDimension(dimension.id)}
                    />
                    <span>{dimension.label}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          </>
        )}

        <section
          className="space-y-3"
          aria-labelledby="pm-v3-population-filters-title"
        >
          <div>
            <h3
              id="pm-v3-population-filters-title"
              className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
            >
              Population filters
            </h3>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              These filters narrow the eligible population before measures are
              calculated.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            {baseFilterFields.map((field) => (
              <BaseFilterPicker
                key={field.id}
                field={field.id}
                label={field.label}
                categories={plan.categories}
                values={findFilter(plan, field.id)?.values ?? []}
                isNull={findFilter(plan, field.id)?.operator === 'IsNull'}
                maximumLength={
                  catalog?.filterFields.find((item) => item.id === field.id)
                    ?.maximumLength ?? 256
                }
                allowNull={
                  catalog?.filterFields
                    .find((item) => item.id === field.id)
                    ?.operators?.includes('IsNull') ?? false
                }
                searchable={field.searchable !== false}
                disabled={!catalog || plan.categories.length === 0}
                onChange={(values, isNull) =>
                  updateBaseFilterValues(field.id, values, isNull)
                }
              />
            ))}
          </div>
        </section>

        <section
          className="space-y-3"
          aria-labelledby="pm-v3-matching-filters-title"
        >
          <div>
            <h3
              id="pm-v3-matching-filters-title"
              className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
            >
              Matching record filters
            </h3>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              These filters define matching rows and count numerators. They
              cannot be combined with percentage measures.
            </p>
          </div>
          {matchingFiltersConflict && (
            <p
              role="alert"
              className="rounded-md border border-[var(--error)]/30 bg-[var(--error)]/5 p-3 text-sm text-[var(--error)]"
            >
              Clear the matching record filters or remove percentage measures
              before previewing.
            </p>
          )}
          <div className="grid gap-3 sm:grid-cols-2">
            {outcomeFilterFields.map((field) => {
              const active = findMatchingFilter(plan, field.id)
              const values =
                active?.operator === 'IsNull' ? [] : (active?.values ?? [])
              return (
                <div key={field.id}>
                  <Label htmlFor={`pm-v3-match-${field.id}`}>
                    {field.label}
                  </Label>
                  <select
                    id={`pm-v3-match-${field.id}`}
                    className={inputClass}
                    multiple
                    size={Math.min(4, field.values.length)}
                    value={values}
                    onChange={(event) =>
                      setMatchingValues(
                        field.id,
                        Array.from(
                          event.target.selectedOptions,
                          (option) => option.value,
                        ),
                      )
                    }
                  >
                    {field.values.map((option) => (
                      <option key={option} value={option}>
                        {formatLabel(option)}
                      </option>
                    ))}
                  </select>
                  <p className="mt-1 text-xs text-[var(--text-neutral)]">
                    Select multiple values with Ctrl or Command. No selection
                    means any.
                  </p>
                  {field.allowUnknown && (
                    <label className="mt-1 flex items-center gap-2 text-xs text-[var(--text-secondary)]">
                      <input
                        className={checkboxClass}
                        type="checkbox"
                        checked={active?.operator === 'IsNull'}
                        disabled={values.length > 0}
                        onChange={(event) =>
                          setMatchingValues(
                            field.id,
                            event.target.checked ? ['__unknown__'] : [],
                          )
                        }
                      />
                      No form recorded
                    </label>
                  )}
                </div>
              )
            })}
          </div>
        </section>

        {plan.mode === 'Aggregate' && (
          <>
            <section
              className="space-y-3"
              aria-labelledby="pm-v3-threshold-title"
            >
              <div className="flex flex-wrap items-end justify-between gap-2">
                <div>
                  <h3
                    id="pm-v3-threshold-title"
                    className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
                  >
                    Calculated measure thresholds
                  </h3>
                  <p className="mt-1 text-xs text-[var(--text-neutral)]">
                    Thresholds apply after aggregation. An unmeasurable group
                    stops the report with an explanation.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={
                    plan.aggregateFilters.length >= 4 ||
                    plan.measures.length === 0
                  }
                  onClick={addAggregateFilter}
                >
                  Add threshold
                </Button>
              </div>
              {plan.aggregateFilters.map((filter, index) => {
                const definition = measures.find(
                  (measure) => measure.id === filter.measure,
                )
                return (
                  <div
                    key={`${index}-${filter.measure}`}
                    className="grid items-end gap-2 rounded-md border border-[var(--border-soft)] p-3 sm:grid-cols-[1.4fr_1fr_1fr_auto]"
                  >
                    <div>
                      <Label htmlFor={`pm-v3-threshold-measure-${index}`}>
                        Measure
                      </Label>
                      <select
                        id={`pm-v3-threshold-measure-${index}`}
                        className={inputClass}
                        value={filter.measure}
                        onChange={(event) =>
                          setAggregateFilter(index, {
                            ...filter,
                            measure: event.target.value,
                          })
                        }
                      >
                        {selectedMeasureDefinitions.map((measure) => (
                          <option key={measure.id} value={measure.id}>
                            {measure.label}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <Label htmlFor={`pm-v3-threshold-operator-${index}`}>
                        Comparison
                      </Label>
                      <select
                        id={`pm-v3-threshold-operator-${index}`}
                        className={inputClass}
                        value={filter.operator}
                        onChange={(event) =>
                          setAggregateFilter(index, {
                            ...filter,
                            operator: event.target.value as NumericOperator,
                          })
                        }
                      >
                        <option value="Equal">equals</option>
                        <option value="NotEqual">does not equal</option>
                        <option value="GreaterThan">greater than</option>
                        <option value="GreaterThanOrEqual">at least</option>
                        <option value="LessThan">less than</option>
                        <option value="LessThanOrEqual">at most</option>
                      </select>
                    </div>
                    <div>
                      <Label htmlFor={`pm-v3-threshold-value-${index}`}>
                        Value {definition?.unit === 'percent' ? '(%)' : ''}
                      </Label>
                      <input
                        id={`pm-v3-threshold-value-${index}`}
                        className={inputClass}
                        type="number"
                        min="0"
                        max={definition?.unit === 'percent' ? 100 : undefined}
                        step={definition?.valueType === 'integer' ? 1 : 0.01}
                        value={filter.value}
                        onChange={(event) =>
                          setAggregateFilter(index, {
                            ...filter,
                            value: Number(event.target.value),
                          })
                        }
                      />
                    </div>
                    <Button
                      type="button"
                      variant="secondary"
                      aria-label="Remove threshold"
                      onClick={() =>
                        updatePlan({
                          ...plan,
                          aggregateFilters: plan.aggregateFilters.filter(
                            (_, itemIndex) => itemIndex !== index,
                          ),
                        })
                      }
                    >
                      Remove
                    </Button>
                  </div>
                )
              })}
            </section>

            <section
              className="grid gap-4 md:grid-cols-2"
              aria-labelledby="pm-v3-ranking-title"
            >
              <div className="space-y-2">
                <h3
                  id="pm-v3-ranking-title"
                  className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
                >
                  Top or bottom groups
                </h3>
                <label className="flex min-h-10 items-center gap-2 text-sm">
                  <input
                    className={checkboxClass}
                    type="checkbox"
                    checked={plan.top !== null}
                    disabled={plan.measures.length === 0}
                    onChange={(event) => setTopEnabled(event.target.checked)}
                  />
                  <span>Limit results to ranked groups</span>
                </label>
                {plan.top && (
                  <div className="grid gap-3 sm:grid-cols-3">
                    <div>
                      <Label htmlFor="pm-v3-top-direction">Selection</Label>
                      <select
                        id="pm-v3-top-direction"
                        className={inputClass}
                        value={plan.top.direction}
                        onChange={(event) =>
                          updatePlan({
                            ...plan,
                            top: {
                              ...plan.top!,
                              direction: event.target.value as Direction,
                            },
                          })
                        }
                      >
                        <option value="Descending">Top</option>
                        <option value="Ascending">Bottom</option>
                      </select>
                    </div>
                    <div>
                      <Label htmlFor="pm-v3-top-limit">Number of groups</Label>
                      <input
                        id="pm-v3-top-limit"
                        className={inputClass}
                        type="number"
                        min="1"
                        max="20"
                        value={plan.top.limit}
                        onChange={(event) =>
                          updatePlan({
                            ...plan,
                            top: {
                              ...plan.top!,
                              limit: Number(event.target.value),
                            },
                          })
                        }
                      />
                    </div>
                    <div>
                      <Label htmlFor="pm-v3-top-measure">Rank by</Label>
                      <select
                        id="pm-v3-top-measure"
                        className={inputClass}
                        value={plan.top.measure}
                        onChange={(event) =>
                          updatePlan({
                            ...plan,
                            top: { ...plan.top!, measure: event.target.value },
                          })
                        }
                      >
                        {selectedMeasureDefinitions.map((measure) => (
                          <option key={measure.id} value={measure.id}>
                            {measure.label}
                          </option>
                        ))}
                      </select>
                    </div>
                    {plan.groupBy.length === 0 && (
                      <p className="text-xs text-[var(--error)] sm:col-span-3">
                        Choose at least one grouping dimension to rank groups.
                      </p>
                    )}
                  </div>
                )}
              </div>
              <div>
                <Label htmlFor="pm-v3-presentation">Presentation</Label>
                <select
                  id="pm-v3-presentation"
                  className={inputClass}
                  value={plan.presentation}
                  disabled={plan.groupBy.length > 0}
                  onChange={(event) =>
                    updatePlan({
                      ...plan,
                      presentation: event.target.value as Presentation,
                    })
                  }
                >
                  <option value="Summary">Summary</option>
                  <option value="Table">Table</option>
                </select>
                {plan.groupBy.length > 0 && (
                  <p className="mt-1 text-xs text-[var(--text-neutral)]">
                    Grouped reports use table presentation.
                  </p>
                )}
              </div>
            </section>
          </>
        )}

        <section className="space-y-2" aria-labelledby="pm-v3-columns-title">
          <div>
            <h3
              id="pm-v3-columns-title"
              className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
            >
              Output columns
            </h3>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              Select the fields shown in the result. Grouping fields stay
              included.
            </p>
          </div>
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {(plan.mode === 'Aggregate'
              ? [...dimensions, ...measures]
              : recordColumns
            ).map((column) => {
              const selected = plan.columns.includes(column.id)
              const required =
                plan.mode === 'Aggregate' && plan.groupBy.includes(column.id)
              const allowed =
                plan.mode === 'Aggregate'
                  ? plan.groupBy.includes(column.id) ||
                    plan.measures.includes(column.id)
                  : true
              return (
                <label
                  key={column.id}
                  className={`flex min-h-10 items-center gap-2 rounded-md border border-[var(--border-soft)] px-3 text-sm ${!allowed ? 'opacity-50' : ''}`}
                >
                  <input
                    className={checkboxClass}
                    type="checkbox"
                    checked={selected}
                    disabled={!allowed || required}
                    onChange={() => {
                      const nextColumns = selected
                        ? plan.columns.filter((value) => value !== column.id)
                        : [...plan.columns, column.id]
                      updatePlan({
                        ...plan,
                        columns: nextColumns,
                        orderBy: plan.orderBy.filter((order) =>
                          nextColumns.includes(order.field),
                        ),
                      })
                    }}
                  />
                  <span>{column.label}</span>
                </label>
              )
            })}
          </div>
          {catalog &&
            plan.mode === 'Aggregate' &&
            !plan.columns.some((column) => plan.measures.includes(column)) && (
              <p role="status" className="text-xs text-[var(--text-neutral)]">
                Choose at least one measure to include in the result.
              </p>
            )}
        </section>

        <section className="space-y-3" aria-labelledby="pm-v3-order-title">
          <div className="flex flex-wrap items-end justify-between gap-2">
            <div>
              <h3
                id="pm-v3-order-title"
                className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase"
              >
                Sort results
              </h3>
              <p className="mt-1 text-xs text-[var(--text-neutral)]">
                A stable tie-break is applied automatically.
              </p>
            </div>
            <Button
              type="button"
              variant="secondary"
              disabled={
                plan.orderBy.length >= 2 ||
                plan.orderBy.length >= plan.columns.length ||
                plan.columns.length === 0
              }
              onClick={() => {
                const field =
                  plan.columns.find(
                    (column) =>
                      !plan.orderBy.some((order) => order.field === column),
                  ) ?? plan.columns[0]
                if (!field) return
                updatePlan({
                  ...plan,
                  orderBy: [...plan.orderBy, { field, direction: 'Ascending' }],
                })
              }}
            >
              Add sort field
            </Button>
          </div>
          {plan.orderBy.map((order, index) => (
            <div
              key={`${index}-${order.field}`}
              className="grid items-end gap-2 sm:grid-cols-[1fr_1fr_auto]"
            >
              <div>
                <Label htmlFor={`pm-v3-order-field-${index}`}>Field</Label>
                <select
                  id={`pm-v3-order-field-${index}`}
                  className={inputClass}
                  value={order.field}
                  onChange={(event) => {
                    const nextOrder = [...plan.orderBy]
                    nextOrder[index] = { ...order, field: event.target.value }
                    updatePlan({ ...plan, orderBy: nextOrder })
                  }}
                >
                  {plan.columns.map((column) => (
                    <option
                      key={column}
                      value={column}
                      disabled={plan.orderBy.some(
                        (item, itemIndex) =>
                          itemIndex !== index && item.field === column,
                      )}
                    >
                      {formatLabel(column, catalog ?? undefined)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label htmlFor={`pm-v3-order-direction-${index}`}>
                  Direction
                </Label>
                <select
                  id={`pm-v3-order-direction-${index}`}
                  className={inputClass}
                  value={order.direction}
                  onChange={(event) => {
                    const nextOrder = [...plan.orderBy]
                    nextOrder[index] = {
                      ...order,
                      direction: event.target.value as Direction,
                    }
                    updatePlan({ ...plan, orderBy: nextOrder })
                  }}
                >
                  <option value="Ascending">Ascending</option>
                  <option value="Descending">Descending</option>
                </select>
              </div>
              <Button
                type="button"
                variant="secondary"
                aria-label="Remove sort field"
                onClick={() =>
                  updatePlan({
                    ...plan,
                    orderBy: plan.orderBy.filter(
                      (_, itemIndex) => itemIndex !== index,
                    ),
                  })
                }
              >
                Remove
              </Button>
            </div>
          ))}
        </section>

        {plan.mode === 'Aggregate' && percentageSelected && (
          <p className="rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3 text-sm text-[var(--text-secondary)]">
            Percentages use the eligible scheduled population as their
            denominator. A selected cycle that is active or future makes on-time
            compliance not measurable.
          </p>
        )}

        {error && (
          <p role="alert" className="text-sm text-[var(--error)]">
            {error}
          </p>
        )}
        {pending && (
          <p role="status" className="text-sm text-[var(--text-secondary)]">
            {pendingLabel}…
          </p>
        )}
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-4">
          <p className="max-w-2xl text-xs text-[var(--text-neutral)]">
            Period: {planPeriodDescription(plan.period)} ·{' '}
            {plan.categories.length} categories ·{' '}
            {plan.mode === 'Records'
              ? 'record rows'
              : `${plan.measures.length} measures`}
          </p>
          <Button
            type="button"
            disabled={!planReady || pending}
            onClick={() => void handlePreview()}
          >
            {pending && pendingLabel === 'Preparing the preview'
              ? 'Preparing…'
              : 'Preview report'}
          </Button>
        </div>
      </Card>

      {preview && (
        <Card
          className="space-y-4 border-[color-mix(in_srgb,var(--primary)_30%,var(--border-soft))] p-4 shadow-none sm:p-5"
          aria-labelledby="pm-v3-preview-title"
        >
          <div>
            <p className="text-xs font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
              Review before running
            </p>
            <h2
              id="pm-v3-preview-title"
              className="mt-1 text-lg font-semibold text-[var(--text-primary)]"
            >
              Report preview
            </h2>
            <p className="mt-2 text-sm text-[var(--text-primary)]">
              {preview.summary}
            </p>
            <p className="mt-2 text-xs text-[var(--text-neutral)]">
              This preview expires{' '}
              {new Intl.DateTimeFormat('en-PH', {
                dateStyle: 'medium',
                timeStyle: 'short',
                timeZone: 'Asia/Manila',
              }).format(new Date(preview.expiresAtUtc))}{' '}
              Manila time.
            </p>
          </div>
          <section
            aria-label="Scheduled PM cycles by category"
            className="rounded-md bg-[var(--surface-muted)] p-3"
          >
            <h3 className="text-sm font-semibold text-[var(--text-primary)]">
              Included PM cycles by category
            </h3>
            <ul className="mt-2 space-y-1 text-sm text-[var(--text-secondary)]">
              {Object.entries(preview.cyclesByCategory).map(
                ([category, cycles]) => (
                  <li key={category}>
                    <span className="font-medium">
                      {formatLabel(category, catalog ?? undefined)}:
                    </span>{' '}
                    {cycles.join(', ')}
                  </li>
                ),
              )}
            </ul>
          </section>
          {selectedMeasureDefinitions.length > 0 && (
            <details>
              <summary className="cursor-pointer text-sm font-medium text-[var(--primary)]">
                Measure definitions
              </summary>
              <dl className="mt-2 space-y-2 text-sm">
                {selectedMeasureDefinitions.map((measure) => (
                  <div key={measure.id}>
                    <dt className="font-semibold text-[var(--text-primary)]">
                      {measure.label}
                      {measure.unit ? ` (${measure.unit})` : ''}
                    </dt>
                    <dd className="text-[var(--text-secondary)]">
                      {measure.definition} Population: {measure.population}.
                    </dd>
                  </div>
                ))}
              </dl>
            </details>
          )}
          <details>
            <summary className="cursor-pointer text-sm font-medium text-[var(--primary)]">
              Counting and data notes
            </summary>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-[var(--text-secondary)]">
              {preview.scopeNotes.map((note) => (
                <li key={note}>{note}</li>
              ))}
            </ul>
          </details>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3">
            <p className="max-w-2xl text-xs text-[var(--text-neutral)]">
              Running this report reads current persisted PM schedules and
              inspections. Execution does not change maintenance records.
            </p>
            <Button
              type="button"
              disabled={pending}
              onClick={() => void executePage(1)}
            >
              {pending && pendingLabel === 'Running the confirmed report'
                ? 'Running…'
                : 'Confirm and run report'}
            </Button>
          </div>
        </Card>
      )}

      {response && catalog && (
        <Card
          className="space-y-5 p-4 shadow-none sm:p-5"
          aria-live="polite"
          aria-labelledby="pm-v3-results-title"
        >
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <p className="text-xs font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
                Report result
              </p>
              <h2
                id="pm-v3-results-title"
                className="mt-1 text-lg font-semibold text-[var(--text-primary)]"
              >
                PM execution results
              </h2>
              <p className="mt-1 text-xs text-[var(--text-neutral)]">
                Calculated{' '}
                {new Intl.DateTimeFormat('en-PH', {
                  dateStyle: 'medium',
                  timeStyle: 'short',
                  timeZone: 'Asia/Manila',
                }).format(new Date(response.asOfUtc))}{' '}
                Manila time.
              </p>
            </div>
            <Button
              type="button"
              variant="secondary"
              className="print:hidden"
              onClick={() => window.print()}
            >
              Print result
            </Button>
          </div>

          <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {[
              [
                'Eligible asset cycles',
                response.population.eligibleAssetCycleCount,
              ],
              [
                'Eligible distinct assets',
                response.population.eligibleDistinctAssetCount,
              ],
              [
                'Matching asset cycles',
                response.population.matchingAssetCycleCount,
              ],
              [
                'Matching distinct assets',
                response.population.matchingDistinctAssetCount,
              ],
            ].map(([label, value]) => (
              <div
                key={String(label)}
                className="rounded-md border border-[var(--border-soft)] p-3"
              >
                <dt className="text-xs font-semibold tracking-[0.06em] text-[var(--text-neutral)] uppercase">
                  {label}
                </dt>
                <dd className="mt-1 text-xl font-bold text-[var(--text-primary)]">
                  {formatNumber(Number(value))}
                </dd>
              </div>
            ))}
          </dl>

          {response.mode === 'Aggregate' &&
            plan.presentation === 'Summary' &&
            response.aggregateRows[0] && (
              <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                {selectedMeasureDefinitions
                  .filter((measure) => plan.columns.includes(measure.id))
                  .map((measure) => {
                    const result =
                      response.aggregateRows[0]?.measures[measure.id]
                    return (
                      <div
                        key={measure.id}
                        className="rounded-md border border-[var(--border-soft)] p-4"
                      >
                        <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
                          {measure.label}
                        </dt>
                        <dd className="mt-2 text-2xl font-bold text-[var(--text-primary)]">
                          {result?.isMeasurable
                            ? formatNumber(result.value) +
                              (result.unit === 'percent' ? '%' : '')
                            : 'Not measurable'}
                        </dd>
                        <p className="mt-1 text-xs text-[var(--text-secondary)]">
                          {formatNumber(result?.numerator)} of{' '}
                          {formatNumber(result?.denominator)}{' '}
                          {result?.unit === 'percent'
                            ? 'eligible asset cycles'
                            : (result?.unit ?? '')}
                        </p>
                        {result?.unmeasurableReason && (
                          <p className="mt-1 text-xs text-[var(--text-neutral)]">
                            {result.unmeasurableReason}
                          </p>
                        )}
                        <Button
                          type="button"
                          variant="secondary"
                          className="mt-3 min-h-10"
                          disabled={
                            !result?.isMeasurable ||
                            !measuresWithSourceRows.has(measure.id) ||
                            pending
                          }
                          onClick={() =>
                            handleMeasureSources(
                              response.aggregateRows[0]?.values ?? null,
                              measure.id,
                              response.cyclesByCategory,
                            )
                          }
                        >
                          View contributing records
                        </Button>
                      </div>
                    )
                  })}
              </dl>
            )}

          {response.mode === 'Aggregate' &&
            response.aggregateRows.length === 0 && (
              <p
                role="status"
                className="rounded-md bg-[var(--surface-muted)] p-4 text-sm text-[var(--text-secondary)]"
              >
                No groups met the selected filters.
              </p>
            )}
          {response.mode === 'Aggregate' &&
            plan.presentation === 'Table' &&
            response.aggregateRows.length > 0 && (
              <ResultTable
                columns={response.columns}
                rows={response.aggregateRows}
                mode="Aggregate"
                catalog={catalog}
                groupBy={plan.groupBy}
                onMeasureSource={(values, measure) =>
                  handleMeasureSources(
                    values,
                    measure,
                    response.cyclesByCategory,
                  )
                }
              />
            )}
          {response.mode === 'Records' &&
            (response.records.length > 0 ? (
              <ResultTable
                columns={response.columns}
                rows={response.records}
                mode="Records"
                catalog={catalog}
              />
            ) : (
              <p
                role="status"
                className="rounded-md bg-[var(--surface-muted)] p-4 text-sm text-[var(--text-secondary)]"
              >
                No records matched the selected filters.
              </p>
            ))}

          {response.mode === 'Aggregate' && (
            <details>
              <summary className="cursor-pointer text-sm font-medium text-[var(--primary)]">
                Calculation definitions and provenance
              </summary>
              <dl className="mt-3 grid gap-3 sm:grid-cols-2">
                {selectedMeasureDefinitions.map((measure) => {
                  const result = response.aggregateRows[0]?.measures[measure.id]
                  return (
                    <div
                      key={measure.id}
                      className="rounded-md border border-[var(--border-soft)] p-3"
                    >
                      <dt className="font-semibold text-[var(--text-primary)]">
                        {measure.label} ({measure.unit})
                      </dt>
                      <dd className="mt-1 text-sm text-[var(--text-secondary)]">
                        {measure.definition} Population: {measure.population}.
                      </dd>
                      {result && (
                        <dd className="mt-2 text-xs text-[var(--text-neutral)]">
                          Numerator {formatNumber(result.numerator)} of
                          denominator {formatNumber(result.denominator)};{' '}
                          {formatNumber(result.eligibleRowCount)} eligible rows
                          and {formatNumber(result.matchingRowCount)} matching
                          rows.
                          {result.unmeasurableReason
                            ? ` ${result.unmeasurableReason}`
                            : ''}
                        </dd>
                      )}
                    </div>
                  )
                })}
              </dl>
              <p className="mt-3 text-sm text-[var(--text-secondary)]">
                Source-record actions open a separate records plan filtered to
                the rows contributing to each measure numerator. Review its
                preview before running it.
              </p>
            </details>
          )}

          {Number(response.totalPages) > 1 && (
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3 print:hidden">
              <p className="text-sm text-[var(--text-secondary)]">
                Page {response.page} of {response.totalPages} ·{' '}
                {formatNumber(response.population.sourceTotalCount)} matching
                rows
              </p>
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="secondary"
                  disabled={pending || Number(response.page) <= 1}
                  onClick={() => void executePage(Number(response.page) - 1)}
                >
                  Previous
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={
                    pending ||
                    Number(response.page) >= Number(response.totalPages)
                  }
                  onClick={() => void executePage(Number(response.page) + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
          <details>
            <summary className="cursor-pointer text-sm font-medium text-[var(--primary)]">
              Scope notes
            </summary>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-[var(--text-secondary)]">
              {response.scopeNotes.map((note) => (
                <li key={note}>{note}</li>
              ))}
            </ul>
          </details>
        </Card>
      )}
    </div>
  )
}

function ResultTable({
  columns,
  rows,
  mode,
  catalog,
  groupBy = [],
  onMeasureSource,
}: {
  columns: Column[]
  rows: Array<{
    scheduleId?: string
    assetId?: string
    inspectionId?: string | null
    values: Record<string, unknown>
    measures?: Record<string, MeasureResult>
  }>
  mode: Mode
  catalog: Catalog
  groupBy?: string[]
  onMeasureSource?: (values: Record<string, unknown>, measure: string) => void
}) {
  return (
    <div className="overflow-x-auto rounded-md border border-[var(--border-soft)]">
      <table className="min-w-full border-collapse text-left text-sm">
        <caption className="sr-only">
          {mode === 'Aggregate'
            ? 'Aggregated PM report results'
            : 'Matching PM execution records'}
        </caption>
        <thead className="bg-[var(--surface-muted)] text-xs font-semibold text-[var(--text-secondary)] uppercase">
          <tr>
            {columns.map((column) => (
              <th
                key={column.id}
                scope="col"
                className="px-3 py-2 whitespace-nowrap"
                title={column.nullSemantics ?? undefined}
              >
                {column.label}
                {column.unit && (
                  <span className="ml-1 font-normal normal-case">
                    ({column.unit})
                  </span>
                )}
              </th>
            ))}
            {mode === 'Records' && (
              <th scope="col" className="px-3 py-2 whitespace-nowrap">
                Source record
              </th>
            )}
            {mode === 'Aggregate' && onMeasureSource && (
              <th scope="col" className="px-3 py-2 whitespace-nowrap">
                Calculation sources
              </th>
            )}
          </tr>
        </thead>
        <tbody className="divide-y divide-[var(--border-soft)]">
          {rows.map((row, index) => (
            <tr
              key={String(row.scheduleId ?? row.assetId ?? index)}
              className="align-top"
            >
              {columns.map((column) => {
                const measure = row.measures?.[column.id]
                const value = measure ? measure.value : row.values[column.id]
                return (
                  <td
                    key={column.id}
                    className="max-w-sm px-3 py-2 text-[var(--text-primary)]"
                  >
                    {measure && !measure.isMeasurable ? (
                      <span
                        title={
                          measure.unmeasurableReason ??
                          'This value cannot be measured for the selected scope.'
                        }
                      >
                        Not measurable
                      </span>
                    ) : (
                      formatValue(value, column, catalog)
                    )}
                    {measure && (
                      <span
                        className="mt-1 block text-xs text-[var(--text-neutral)]"
                        title="Numerator and denominator for this measure"
                      >
                        {formatNumber(measure.numerator)} of{' '}
                        {formatNumber(measure.denominator)}
                      </span>
                    )}
                  </td>
                )
              })}
              {mode === 'Records' && (
                <td className="px-3 py-2">
                  {row.inspectionId ? (
                    <Link
                      to="/app/inspections/$inspectionId"
                      params={{ inspectionId: row.inspectionId }}
                      className="font-medium text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
                    >
                      Review inspection
                    </Link>
                  ) : row.assetId ? (
                    <Link
                      to="/app/assets/$assetId"
                      params={{ assetId: row.assetId }}
                      className="font-medium text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
                    >
                      View asset
                    </Link>
                  ) : (
                    'No source link available'
                  )}
                </td>
              )}
              {mode === 'Aggregate' && onMeasureSource && (
                <td className="px-3 py-2">
                  <div className="flex min-w-44 flex-col items-start gap-2">
                    {Object.entries(row.measures ?? {}).map(
                      ([measureId, result]) => {
                        const groupLabel = groupBy
                          .map((dimension) => {
                            const value = row.values[dimension]
                            return `${formatLabel(dimension, catalog)} ${formatValue(
                              value,
                              columns.find(
                                (column) => column.id === dimension,
                              ) ?? {
                                id: dimension,
                                label: dimension,
                                valueType: 'string',
                                unit: '',
                                nullSemantics: null,
                              },
                              catalog,
                            )}`
                          })
                          .join(', ')
                        return (
                          <Button
                            key={measureId}
                            type="button"
                            variant="secondary"
                            className="min-h-10"
                            disabled={
                              !result.isMeasurable ||
                              !measuresWithSourceRows.has(measureId)
                            }
                            onClick={() =>
                              onMeasureSource(row.values, measureId)
                            }
                          >
                            View {formatLabel(measureId, catalog)} records
                            {groupLabel ? ` · ${groupLabel}` : ''}
                          </Button>
                        )
                      },
                    )}
                  </div>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
