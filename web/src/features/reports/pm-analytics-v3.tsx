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
import { DetailBackLink } from '@/features/shared/detail-back-link'
import type { DetailReturnContext } from '@/features/shared/detail-navigation'

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
type AnalyticsStage = 'question' | 'plan' | 'preview' | 'results'
type ClarificationChoices = {
  scopeRecovered: boolean
  requiredFields: string[]
  answeredFields: string[]
  periodKindChosen: boolean
  periodBasis: 'pmCycle' | 'inspectionDate' | null
  filterTarget: string | 'none' | null
}

type AnalyticsViewSnapshot = {
  catalog: Catalog | null
  selectedSubject: string
  plan: ReportPlan
  question: string
  submittedQuestion: string
  interpretation: Interpretation | null
  clarificationChoices: ClarificationChoices
  preview: PreviewResponse | null
  response: ExecutionResponse | null
  stage: AnalyticsStage
  originContext: DetailReturnContext
  manualEditorOpen: boolean
  error: string | null
  interpretationFailureCode: string | null
  drilldownNotice: string | null
  scrollY: number
}

type AnalyticsPanelSnapshot = AnalyticsViewSnapshot & {
  parentReport: AnalyticsViewSnapshot | null
}

const analyticsReturnSnapshots = new Map<string, AnalyticsPanelSnapshot>()

function createAnalyticsSessionId() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (marker) => {
    const value = Math.floor(Math.random() * 16)
    return (marker === 'x' ? value : (value & 0x3) | 0x8).toString(16)
  })
}

function saveAnalyticsReturnSnapshot(
  sessionId: string,
  snapshot: AnalyticsPanelSnapshot,
) {
  analyticsReturnSnapshots.set(sessionId, snapshot)
  while (analyticsReturnSnapshots.size > 8) {
    const oldestSession = analyticsReturnSnapshots.keys().next().value
    if (oldestSession === undefined) break
    analyticsReturnSnapshots.delete(oldestSession)
  }
}

const inputClass =
  'mt-1 block min-h-10 w-full rounded-md border border-[var(--border-soft)] bg-white px-3 py-2 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)] disabled:cursor-not-allowed disabled:bg-[var(--surface-muted)]'
const checkboxClass =
  'h-4 w-4 rounded border-[var(--border-soft)] accent-[var(--primary)]'
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

  if (plan.subject !== 'pmExecution' || period === null) return null

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

function initialPlan(): ReportPlan {
  return {
    schemaVersion: 3,
    catalogVersion: 2,
    subject: 'pmExecution',
    mode: 'Aggregate',
    period: {
      basis: 'pmCycle',
      kind: 'Year',
      year: null,
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

function emptyPeriod(): Period {
  return {
    basis: 'pmCycle',
    kind: 'Year',
    year: null,
    month: null,
    quarter: null,
    startCycle: null,
    endCycle: null,
  }
}

function emptyClarificationChoices(): ClarificationChoices {
  return {
    scopeRecovered: false,
    requiredFields: [],
    answeredFields: [],
    periodKindChosen: false,
    periodBasis: null,
    filterTarget: null,
  }
}

function toApiPlan(plan: ReportPlan): PmAnalyticsV3ReportPlan {
  return {
    ...plan,
    period: plan.subject === 'assetInventory' ? null : plan.period,
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
  const period = value.period ?? emptyPeriod()
  return {
    ...value,
    schemaVersion: Number(value.schemaVersion),
    catalogVersion: Number(value.catalogVersion),
    period: {
      ...period,
      basis: 'pmCycle',
      year: period.year == null ? null : Number(period.year),
      month: period.month == null ? null : Number(period.month),
      quarter: period.quarter == null ? null : Number(period.quarter),
      startCycle: period.startCycle ?? null,
      endCycle: period.endCycle ?? null,
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
    if (code === 'RequestTooLarge') {
      return 'The interpreter request exceeds the server limit. This is a server configuration issue; changing your question will not fix it.'
    }
    if (code === 'PreviewUnavailable') {
      return 'This preview expired or is no longer available. Create and review a new preview before running the report.'
    }
    if (code === 'PreviewStale') {
      return 'The selected report values changed. Update the plan and create a new preview before running it.'
    }
    if (code === 'ProviderUnavailable' || code === 'Timeout') {
      return 'The configured interpreter is unavailable. Your question is unchanged. Retry, or choose the deterministic parser explicitly.'
    }
    if (code === 'InvalidJson') {
      return 'DeepSeek replied, but its response was not valid JSON. Your question is unchanged. Retry or choose the deterministic parser explicitly.'
    }
    if (code === 'InvalidEnvelope' || code === 'InvalidOutput') {
      return 'The selected interpreter returned incomplete or malformed report details. Your question is unchanged. Retry or choose the deterministic parser explicitly.'
    }
    if (code === 'InvalidPlan') {
      const fields = Array.isArray(error.problem?.extensions?.fields)
        ? error.problem.extensions.fields.filter(
            (field): field is string => typeof field === 'string',
          )
        : []
      const labels: Record<string, string> = {
        Subject: 'report type',
        Mode: 'report view',
        Period: 'PM cycle period',
        Categories: 'asset category',
        Measures: 'measure',
        GroupBy: 'grouping',
        BaseFilters: 'asset filters',
        MatchingFilters: 'inspection filters',
        AggregateFilters: 'thresholds',
        Columns: 'output columns',
        Presentation: 'presentation',
        OrderBy: 'sorting',
        Top: 'ranking',
        'top.measure': 'ranking',
        'top.limit': 'ranking',
        'top.direction': 'ranking',
        SchemaVersion: 'report format',
        CatalogVersion: 'report catalog',
        plan: 'report structure',
      }
      const affected = [
        ...new Set(fields.map((field) => labels[field]).filter(Boolean)),
      ].join(', ')
      return affected
        ? `The selected interpreter suggested a report that UniPM could not validate (${affected}). No report ran. Retry, choose the deterministic parser, or revise the question.`
        : 'The selected interpreter suggested a report that UniPM could not validate. No report ran. Retry, choose the deterministic parser, or revise the question.'
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

function interpretationSourceLabel(interpreter: string) {
  if (interpreter === 'DeepSeek') return 'Interpreted by DeepSeek'
  if (interpreter === 'DeterministicParser')
    return 'Interpreted by the deterministic parser'
  if (interpreter === 'ApprovedInterpretation')
    return 'Matched an approved interpretation'
  return 'Handled by UniPM safety rules'
}

function getProblemCode(error: unknown) {
  if (!(error instanceof ApiError)) return null
  const code = error.problem?.extensions?.code
  return typeof code === 'string' ? code : null
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
  subject,
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
  subject: string
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
        { field, subject, categories, search: trimmedSearch },
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
  }, [
    categories,
    categoryKey,
    field,
    queryKey,
    shouldSearch,
    subject,
    trimmedSearch,
  ])

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

export function PmAnalyticsV3Panel({
  returnContext,
}: {
  returnContext?: DetailReturnContext | undefined
}) {
  const [analyticsSessionId] = useState(() =>
    returnContext?.kind === 'analytics'
      ? returnContext.sessionId
      : createAnalyticsSessionId(),
  )
  const [restoredSnapshot] = useState<AnalyticsPanelSnapshot | null>(() =>
    returnContext?.kind === 'analytics'
      ? (analyticsReturnSnapshots.get(returnContext.sessionId) ?? null)
      : null,
  )
  const [originContext] = useState<DetailReturnContext>(
    () =>
      restoredSnapshot?.originContext ??
      (returnContext?.kind === 'dashboard'
        ? returnContext
        : { kind: 'dashboard' }),
  )
  const [catalog, setCatalog] = useState<Catalog | null>(
    () => restoredSnapshot?.catalog ?? null,
  )
  const [selectedSubject, setSelectedSubject] = useState(
    () => restoredSnapshot?.selectedSubject ?? 'pmExecution',
  )
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const [catalogLoading, setCatalogLoading] = useState(
    () => !restoredSnapshot?.catalog,
  )
  const [catalogAttempt, setCatalogAttempt] = useState(0)
  const [plan, setPlan] = useState<ReportPlan>(
    () => restoredSnapshot?.plan ?? initialPlan(),
  )
  const [question, setQuestion] = useState(
    () => restoredSnapshot?.question ?? '',
  )
  const [submittedQuestion, setSubmittedQuestion] = useState(
    () => restoredSnapshot?.submittedQuestion ?? '',
  )
  const [stage, setStage] = useState<AnalyticsStage>(
    () => restoredSnapshot?.stage ?? 'question',
  )
  const [manualEditorOpen, setManualEditorOpen] = useState(
    () => restoredSnapshot?.manualEditorOpen ?? false,
  )
  const [interpretation, setInterpretation] = useState<Interpretation | null>(
    () => restoredSnapshot?.interpretation ?? null,
  )
  const [clarificationChoices, setClarificationChoices] =
    useState<ClarificationChoices>(
      () =>
        restoredSnapshot?.clarificationChoices ?? emptyClarificationChoices(),
    )
  const [preview, setPreview] = useState<PreviewResponse | null>(
    () => restoredSnapshot?.preview ?? null,
  )
  const [response, setResponse] = useState<ExecutionResponse | null>(
    () => restoredSnapshot?.response ?? null,
  )
  const [error, setError] = useState<string | null>(null)
  const [interpretationFailureCode, setInterpretationFailureCode] = useState<
    string | null
  >(null)
  const [pending, setPending] = useState(false)
  const [pendingLabel, setPendingLabel] = useState('')
  const [drilldownNotice, setDrilldownNotice] = useState<string | null>(null)
  const [parentReport, setParentReport] =
    useState<AnalyticsViewSnapshot | null>(
      () => restoredSnapshot?.parentReport ?? null,
    )
  const [isEditingQuestion, setIsEditingQuestion] = useState(false)
  const [editingSnapshot, setEditingSnapshot] =
    useState<AnalyticsPanelSnapshot | null>(null)
  const activeController = useRef<AbortController | null>(null)
  const requestId = useRef(0)
  const returnSessionId =
    returnContext?.kind === 'analytics' ? returnContext.sessionId : undefined

  useEffect(() => {
    if (!returnSessionId || !restoredSnapshot) return
    const frame = window.requestAnimationFrame(() =>
      window.scrollTo(0, restoredSnapshot.scrollY),
    )
    return () => window.cancelAnimationFrame(frame)
  }, [returnSessionId, restoredSnapshot])

  const captureView = (): AnalyticsViewSnapshot => ({
    catalog,
    selectedSubject,
    plan,
    question,
    submittedQuestion,
    interpretation,
    clarificationChoices,
    preview,
    response,
    stage,
    originContext,
    manualEditorOpen,
    error,
    interpretationFailureCode,
    drilldownNotice,
    scrollY: window.scrollY,
  })
  const capturePanel = (): AnalyticsPanelSnapshot => ({
    ...captureView(),
    parentReport,
  })
  const restoreView = (snapshot: AnalyticsViewSnapshot) => {
    setCatalog(snapshot.catalog)
    setSelectedSubject(snapshot.selectedSubject)
    setPlan(snapshot.plan)
    setQuestion(snapshot.question)
    setSubmittedQuestion(snapshot.submittedQuestion)
    setInterpretation(snapshot.interpretation)
    setClarificationChoices(
      snapshot.clarificationChoices ?? emptyClarificationChoices(),
    )
    setPreview(snapshot.preview)
    setResponse(snapshot.response)
    setStage(snapshot.stage)
    setManualEditorOpen(snapshot.manualEditorOpen)
    setError(snapshot.error)
    setInterpretationFailureCode(snapshot.interpretationFailureCode)
    setDrilldownNotice(snapshot.drilldownNotice)
    setIsEditingQuestion(false)
    setEditingSnapshot(null)
    window.requestAnimationFrame(() => window.scrollTo(0, snapshot.scrollY))
  }
  const restorePanel = (snapshot: AnalyticsPanelSnapshot) => {
    restoreView(snapshot)
    setParentReport(snapshot.parentReport)
  }
  const startEditingQuestion = () => {
    setEditingSnapshot(capturePanel())
    setIsEditingQuestion(true)
    setQuestion(submittedQuestion)
    setInterpretation(null)
    setError(null)
    setInterpretationFailureCode(null)
    setStage('question')
  }
  const cancelQuestionEdit = () => {
    if (editingSnapshot) restorePanel(editingSnapshot)
    else setStage('question')
  }
  const returnToParentReport = () => {
    if (!parentReport) return
    restoreView(parentReport)
    setParentReport(null)
  }
  const saveBeforeSourceNavigation = () => {
    saveAnalyticsReturnSnapshot(analyticsSessionId, capturePanel())
  }

  useEffect(() => {
    const controller = new AbortController()
    void getPmAnalyticsV3Catalog(
      { subject: selectedSubject },
      controller.signal,
    )
      .then((value) => {
        if (!controller.signal.aborted) {
          const currentCatalog = normalizeCatalog(value)
          setCatalog(currentCatalog)
          setPlan((current) => {
            if (current.subject === currentCatalog.subject) {
              return {
                ...current,
                schemaVersion: currentCatalog.schemaVersion,
                catalogVersion: currentCatalog.catalogVersion,
              }
            }

            const initial = initialPlan()
            const isInventory = currentCatalog.subject === 'assetInventory'
            const registeredCount = currentCatalog.measures.find(
              (measure) => measure.id === 'registeredAssetCount',
            )
            return {
              ...initial,
              schemaVersion: currentCatalog.schemaVersion,
              catalogVersion: currentCatalog.catalogVersion,
              subject: currentCatalog.subject,
              categories: isInventory
                ? currentCatalog.categories.map((category) => category.id)
                : [],
              measures:
                isInventory && registeredCount ? [registeredCount.id] : [],
              columns:
                isInventory && registeredCount ? [registeredCount.id] : [],
            }
          })
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
  }, [catalogAttempt, selectedSubject])

  useEffect(() => () => activeController.current?.abort(), [])

  const categories = catalog?.categories ?? []
  const measures = catalog?.measures ?? []
  const dimensions = catalog?.dimensions ?? []
  const recordColumns = catalog?.recordColumns ?? []
  const subject = catalog?.subjects.find((item) => item.id === selectedSubject)
  const isInventory = plan.subject === 'assetInventory'
  const baseFilterFields = (catalog?.filterFields ?? []).filter(
    (field) => field.isBaseFilter,
  )
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
    setInterpretationFailureCode(null)
    setInterpretation(null)
    setDrilldownNotice(null)
  }

  const clearClarificationResults = () => {
    cancelPending()
    setPreview(null)
    setResponse(null)
    setError(null)
    setInterpretationFailureCode(null)
    setDrilldownNotice(null)
  }

  const updateClarificationPlan = (next: ReportPlan) => {
    clearClarificationResults()
    setPlan(next)
  }

  const answerClarification = (field: string) => {
    clearClarificationResults()
    setClarificationChoices((current) => ({
      ...current,
      answeredFields: current.answeredFields.includes(field)
        ? current.answeredFields
        : [...current.answeredFields, field],
    }))
  }

  const reopenClarification = (field: string) => {
    clearClarificationResults()
    setClarificationChoices((current) => ({
      ...current,
      answeredFields: current.answeredFields.filter((item) => item !== field),
    }))
  }

  const startNewQuestion = () => {
    cancelPending()
    analyticsReturnSnapshots.delete(analyticsSessionId)
    setQuestion('')
    setSubmittedQuestion('')
    setPlan(initialPlan())
    setInterpretation(null)
    setClarificationChoices(emptyClarificationChoices())
    setPreview(null)
    setResponse(null)
    setError(null)
    setInterpretationFailureCode(null)
    setDrilldownNotice(null)
    setManualEditorOpen(false)
    setParentReport(null)
    setIsEditingQuestion(false)
    setEditingSnapshot(null)
    setStage('question')
    if (selectedSubject !== 'pmExecution') {
      setSelectedSubject('pmExecution')
      setCatalog(null)
      setCatalogLoading(true)
      setCatalogError(null)
    }
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
    setParentReport(captureView())
    setQuestion('')
    setSubmittedQuestion('')
    setIsEditingQuestion(false)
    setEditingSnapshot(null)
    updatePlan(sourcePlan)
    setStage('preview')
    setDrilldownNotice(
      `A records plan for ${measureLabel} is ready. Preview it to review the contributing source records.`,
    )
    void handlePreview(sourcePlan)
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

  const runInterpretation = async (
    requestedInterpreter?: 'DeepSeek' | 'DeterministicParser',
  ) => {
    const cleanQuestion = question.trim()
    if (!cleanQuestion) return
    setInterpretation(null)
    setInterpretationFailureCode(null)
    const request = beginRequest('Interpreting the question')
    try {
      const result = await interpretPmAnalyticsV3Question(
        {
          question: cleanQuestion,
          ...(requestedInterpreter
            ? { interpreter: requestedInterpreter }
            : {}),
        },
        request.controller.signal,
      )
      if (!request.isCurrent()) return
      setInterpretation(result)
      setSubmittedQuestion(cleanQuestion)
      setPreview(null)
      setResponse(null)
      setParentReport(null)
      setIsEditingQuestion(false)
      setEditingSnapshot(null)
      setManualEditorOpen(false)
      if (result.status === 'Valid' && result.plan) {
        const nextPlan = normalizePlan(result.plan)
        setPlan(nextPlan)
        setClarificationChoices({
          scopeRecovered: true,
          requiredFields: [],
          answeredFields: [],
          periodKindChosen: result.plan.period !== null,
          periodBasis:
            result.plan.period?.basis === 'pmCycle' ? 'pmCycle' : null,
          filterTarget: null,
        })
        setStage('plan')
        if (nextPlan.subject !== selectedSubject) {
          setSelectedSubject(nextPlan.subject)
          setCatalog(null)
          setCatalogLoading(true)
          setCatalogError(null)
        }
      } else if (result.status === 'NeedsClarification') {
        setStage('question')
        if (result.plan) {
          const partialPlan = normalizePlan(result.plan)
          const hasBasisClarification =
            result.code === 'PeriodBasisNeedsClarification' ||
            result.clarificationFields.includes('PeriodBasis')
          setPlan(partialPlan)
          setClarificationChoices({
            scopeRecovered: true,
            requiredFields: [
              ...new Set([
                ...result.clarificationFields,
                ...(partialPlan.categories.length === 0 ? ['Categories'] : []),
                ...(partialPlan.mode === 'Aggregate' &&
                partialPlan.measures.length === 0
                  ? ['Measures']
                  : []),
              ]),
            ],
            answeredFields: [],
            periodKindChosen: result.plan.period !== null,
            periodBasis:
              !hasBasisClarification && result.plan.period?.basis === 'pmCycle'
                ? 'pmCycle'
                : null,
            filterTarget: null,
          })
          if (partialPlan.subject !== selectedSubject) {
            setSelectedSubject(partialPlan.subject)
            setCatalog(null)
            setCatalogLoading(true)
            setCatalogError(null)
          }
        } else {
          setPlan({
            ...initialPlan(),
            period: {
              basis: 'pmCycle',
              kind: 'Year',
              year: null,
              month: null,
              quarter: null,
              startCycle: null,
              endCycle: null,
            },
          })
          setClarificationChoices(emptyClarificationChoices())
        }
      } else {
        setStage('question')
        setClarificationChoices(emptyClarificationChoices())
      }
    } catch (requestError) {
      if (request.isCurrent()) {
        setInterpretationFailureCode(getProblemCode(requestError))
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

  const handleInterpret = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void runInterpretation()
  }

  const handlePreview = async (targetPlan: ReportPlan = plan) => {
    const request = beginRequest('Preparing the preview')
    try {
      const result = await previewPmAnalyticsV3Report(
        { plan: toApiPlan(targetPlan) },
        request.controller.signal,
      )
      if (request.isCurrent()) {
        setPreview(result)
        setResponse(null)
        setManualEditorOpen(false)
        setStage('preview')
      }
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
      if (request.isCurrent()) {
        setResponse(result)
        setStage('results')
      }
    } catch (requestError) {
      if (request.isCurrent()) {
        const code =
          requestError instanceof ApiError
            ? requestError.problem?.extensions?.code
            : undefined
        if (code === 'PreviewUnavailable' || code === 'PreviewStale') {
          setPreview(null)
          setResponse(null)
          setStage('plan')
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
    const nextPeriod: Period = {
      basis: 'pmCycle',
      kind,
      year: kind === 'Range' ? null : plan.period.year,
      month: null,
      quarter: null,
      startCycle: kind === 'Range' ? plan.period.startCycle : null,
      endCycle: kind === 'Range' ? plan.period.endCycle : null,
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

  const updateClarificationFilterValues = (
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
    updateClarificationPlan({ ...plan, baseFilters: existing })
  }

  const chooseClarificationSubject = (subjectId: string) => {
    if (plan.subject === subjectId) {
      answerClarification('Subject')
      return
    }

    const nextPlan: ReportPlan = {
      ...initialPlan(),
      subject: subjectId,
      schemaVersion: catalog?.schemaVersion ?? plan.schemaVersion,
      catalogVersion: catalog?.catalogVersion ?? plan.catalogVersion,
      period: emptyPeriod(),
    }
    updateClarificationPlan(nextPlan)
    setClarificationChoices({
      scopeRecovered: true,
      requiredFields: [
        'Mode',
        'Categories',
        'Measures',
        'Period',
        'PeriodBasis',
      ],
      answeredFields: ['Subject'],
      periodKindChosen: false,
      periodBasis: null,
      filterTarget: null,
    })
    setSelectedSubject(subjectId)
    setCatalog(null)
    setCatalogLoading(true)
    setCatalogError(null)
  }

  const chooseClarificationMode = (mode: Mode) => {
    if (mode === plan.mode) {
      answerClarification('Mode')
      return
    }
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
            columns:
              plan.subject === 'assetInventory'
                ? recordColumns.map((column) => column.id)
                : defaultRecordColumns,
          }
        : {
            ...plan,
            mode,
            measures: [],
            groupBy: [],
            aggregateFilters: [],
            orderBy: [],
            top: null,
            presentation: 'Summary',
            columns: [],
          }
    updateClarificationPlan(next)
    answerClarification('Mode')
  }

  const chooseClarificationPeriodKind = (kind: PeriodKind) => {
    const nextPeriod: Period = {
      basis: 'pmCycle',
      kind,
      year: kind === 'Range' ? null : plan.period.year,
      month: null,
      quarter: null,
      startCycle: kind === 'Range' ? plan.period.startCycle : null,
      endCycle: kind === 'Range' ? plan.period.endCycle : null,
    }
    updateClarificationPlan({ ...plan, period: nextPeriod })
    setClarificationChoices((current) => ({
      ...current,
      periodKindChosen: true,
      answeredFields: isPeriodReady(nextPeriod)
        ? [...new Set([...current.answeredFields, 'Period'])]
        : current.answeredFields.filter((field) => field !== 'Period'),
    }))
  }

  const updateClarificationPeriod = (period: Period) => {
    updateClarificationPlan({ ...plan, period })
    setClarificationChoices((current) => ({
      ...current,
      answeredFields: isPeriodReady(period)
        ? [...new Set([...current.answeredFields, 'Period'])]
        : current.answeredFields.filter((field) => field !== 'Period'),
    }))
  }

  const chooseClarificationPeriodBasis = (
    periodBasis: 'pmCycle' | 'inspectionDate',
  ) => {
    clearClarificationResults()
    setClarificationChoices((current) => ({
      ...current,
      periodBasis,
      answeredFields: current.answeredFields.includes('PeriodBasis')
        ? current.answeredFields
        : [...current.answeredFields, 'PeriodBasis'],
    }))
  }

  const chooseClarificationFilterTarget = (filterTarget: string | 'none') => {
    clearClarificationResults()
    setClarificationChoices((current) => ({
      ...current,
      filterTarget,
      answeredFields:
        filterTarget === 'none'
          ? [...new Set([...current.answeredFields, 'Filters'])]
          : current.answeredFields.filter((field) => field !== 'Filters'),
    }))
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
            columns:
              plan.subject === 'assetInventory'
                ? recordColumns.map((column) => column.id)
                : defaultRecordColumns,
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

  const clarificationInterpretation =
    interpretation?.status === 'NeedsClarification' ? interpretation : null
  const isClarification = clarificationInterpretation !== null
  const canGuideClarification =
    clarificationInterpretation !== null &&
    clarificationInterpretation.plan !== null &&
    clarificationChoices.scopeRecovered
  const clarificationFieldSet = new Set(
    clarificationInterpretation?.clarificationFields ?? [],
  )
  const clarificationRequiredFieldSet = new Set(
    clarificationChoices.requiredFields,
  )
  const clarificationWasAnswered = (field: string) =>
    clarificationChoices.answeredFields.includes(field)
  const needsSubjectAnswer =
    canGuideClarification &&
    clarificationFieldSet.has('Subject') &&
    !clarificationWasAnswered('Subject')
  const needsCategoriesAnswer =
    canGuideClarification &&
    clarificationRequiredFieldSet.has('Categories') &&
    !clarificationWasAnswered('Categories')
  const needsMeasuresAnswer =
    canGuideClarification &&
    plan.mode === 'Aggregate' &&
    clarificationRequiredFieldSet.has('Measures') &&
    !clarificationWasAnswered('Measures')
  const needsModeAnswer =
    canGuideClarification &&
    clarificationFieldSet.has('Mode') &&
    !clarificationWasAnswered('Mode')
  const needsGroupingAnswer =
    canGuideClarification &&
    clarificationFieldSet.has('GroupBy') &&
    !clarificationWasAnswered('GroupBy')
  const periodBasisWasRequested =
    clarificationFieldSet.has('PeriodBasis') ||
    clarificationInterpretation?.code === 'PeriodBasisNeedsClarification'
  const needsPeriodBasisAnswer =
    canGuideClarification &&
    subject?.requiresPeriod === true &&
    (clarificationChoices.periodBasis !== 'pmCycle' ||
      (periodBasisWasRequested && !clarificationWasAnswered('PeriodBasis')))
  const needsPeriodAnswer =
    canGuideClarification &&
    subject?.requiresPeriod === true &&
    (!clarificationChoices.periodKindChosen ||
      !isPeriodReady(plan.period) ||
      (clarificationFieldSet.has('Period') &&
        !clarificationWasAnswered('Period')))
  const needsGenericFilterAnswer =
    canGuideClarification &&
    clarificationFieldSet.has('Filters') &&
    !clarificationWasAnswered('Filters')
  const requestedNamedFilterFields = [
    'Department',
    'Building',
    'Location',
  ].filter((field) => clarificationFieldSet.has(field))
  const findBaseFilterForClarification = (name: string) =>
    baseFilterFields.find((field) =>
      `${field.id} ${field.label}`.toLowerCase().includes(name.toLowerCase()),
    )
  const missingNamedFilterDefinitions = requestedNamedFilterFields.filter(
    (field) => !findBaseFilterForClarification(field),
  )
  const needsNamedFilterAnswers = requestedNamedFilterFields.filter(
    (field) => !clarificationWasAnswered(field),
  )
  const supportedClarificationFields = new Set([
    'Subject',
    'Categories',
    'Measures',
    'Period',
    'PeriodBasis',
    'Mode',
    'GroupBy',
    'Filters',
    'Department',
    'Building',
    'Location',
  ])
  const unresolvedUnsupportedClarificationFields = clarificationInterpretation
    ? clarificationInterpretation.clarificationFields.filter(
        (field) =>
          !supportedClarificationFields.has(field) ||
          missingNamedFilterDefinitions.includes(field),
      )
    : []
  const guidedClarificationReady =
    canGuideClarification &&
    !needsSubjectAnswer &&
    !needsCategoriesAnswer &&
    !needsMeasuresAnswer &&
    !needsModeAnswer &&
    !needsGroupingAnswer &&
    !needsPeriodBasisAnswer &&
    !needsPeriodAnswer &&
    !needsGenericFilterAnswer &&
    needsNamedFilterAnswers.length === 0 &&
    unresolvedUnsupportedClarificationFields.length === 0
  const planReady =
    catalog !== null &&
    catalog.subject === selectedSubject &&
    plan.subject === selectedSubject &&
    plan.categories.length > 0 &&
    plan.measures.length <= 4 &&
    plan.groupBy.length <= 2 &&
    !matchingFiltersConflict &&
    (plan.mode === 'Records' || plan.measures.length > 0) &&
    plan.columns.length > 0 &&
    (plan.mode === 'Records' ||
      plan.columns.some((column) => plan.measures.includes(column))) &&
    (!subject?.requiresPeriod ||
      (isPeriodReady(plan.period) &&
        (isClarification
          ? clarificationChoices.periodBasis === 'pmCycle'
          : plan.period.basis === 'pmCycle'))) &&
    (interpretation === null ||
      interpretation.status === 'Valid' ||
      (isClarification && guidedClarificationReady)) &&
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
            Subject: 'report type',
            Categories: 'asset category',
            Period: 'report period',
            Measures: 'measure',
            Mode: 'count or record list',
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

  const interpretationSummary = [
    { label: 'Question', value: submittedQuestion || question },
    { label: 'Report type', value: subject?.label ?? 'Report' },
    ...(subject?.requiresPeriod
      ? [{ label: 'Period', value: planPeriodDescription(plan.period) }]
      : []),
    {
      label: 'Asset categories',
      value:
        plan.categories
          .map((category) => formatLabel(category, catalog ?? undefined))
          .join(', ') || 'None selected',
    },
    {
      label: plan.mode === 'Records' ? 'Report view' : 'Measures',
      value:
        plan.mode === 'Records'
          ? 'Matching records'
          : plan.measures
              .map((measure) => formatLabel(measure, catalog ?? undefined))
              .join(', ') || 'None selected',
    },
    {
      label: 'Grouping',
      value:
        plan.groupBy
          .map((dimension) => formatLabel(dimension, catalog ?? undefined))
          .join(', ') || 'None',
    },
    ...([...plan.baseFilters, ...plan.matchingFilters].length > 0
      ? [
          {
            label: 'Filters',
            value: [...plan.baseFilters, ...plan.matchingFilters]
              .map((filter) => {
                const field = formatLabel(filter.field, catalog ?? undefined)
                const operation = filter.operator === 'In' ? 'is one of' : 'is'
                const values = (filter.values ?? [])
                  .map((value) => formatLabel(value, catalog ?? undefined))
                  .join(', ')
                return `${field} ${operation} ${values}`
              })
              .join('; '),
          },
        ]
      : []),
  ]
  const showClarificationPanel =
    clarificationInterpretation !== null &&
    stage === 'question' &&
    !manualEditorOpen &&
    !isEditingQuestion
  const showQuestionForm =
    stage === 'question' &&
    !manualEditorOpen &&
    (!showClarificationPanel || isEditingQuestion)
  const showQuestionExamples = showQuestionForm && interpretation === null
  const showPlanSummary =
    stage === 'plan' &&
    interpretation?.status === 'Valid' &&
    !manualEditorOpen &&
    catalog?.subject === plan.subject &&
    !catalogLoading

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <DetailBackLink
          context={originContext}
          fallback={{ kind: 'dashboard' }}
        />
        {parentReport && (
          <Button
            type="button"
            variant="secondary"
            disabled={pending}
            onClick={returnToParentReport}
          >
            Back to report
          </Button>
        )}
      </div>
      <Card className="space-y-4 p-4 shadow-none sm:p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-[var(--text-primary)]">
              {stage === 'results'
                ? 'Report results'
                : stage === 'preview'
                  ? 'Review your report'
                  : stage === 'plan'
                    ? 'Review the report'
                    : 'Ask UniPM'}
            </h2>
            <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
              {stage === 'results'
                ? 'The report has finished. Results and source records are shown below.'
                : stage === 'preview'
                  ? 'Review the scope before confirming the report.'
                  : stage === 'plan'
                    ? 'Check what the report will show. It has not run yet.'
                    : 'Ask questions about university assets and preventive maintenance.'}
            </p>
            {stage !== 'question' && submittedQuestion && (
              <p className="mt-2 rounded-md bg-[var(--surface-muted)] px-3 py-2 text-sm text-[var(--text-secondary)]">
                <span className="font-medium text-[var(--text-primary)]">
                  Question:
                </span>{' '}
                {submittedQuestion}
              </p>
            )}
          </div>
        </div>

        {showQuestionForm && (
          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(15rem,0.65fr)]">
            <form
              className="space-y-3"
              onSubmit={(event) => void handleInterpret(event)}
            >
              <Label htmlFor="pm-analytics-v3-question">Question</Label>
              <textarea
                id="pm-analytics-v3-question"
                className={`${inputClass} min-h-28 resize-y`}
                value={question}
                maxLength={512}
                onChange={(event) => {
                  cancelPending()
                  setQuestion(event.target.value)
                  setInterpretation(null)
                  setError(null)
                  setInterpretationFailureCode(null)
                  setDrilldownNotice(null)
                }}
                placeholder="How many fire extinguishers are registered in the CCMS building?"
              />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-xs text-[var(--text-neutral)]">
                  You’ll review the report scope before it runs.
                </p>
                <div className="flex flex-wrap gap-2">
                  {isEditingQuestion && (
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={pending}
                      onClick={cancelQuestionEdit}
                    >
                      Cancel
                    </Button>
                  )}
                  <Button
                    type="submit"
                    disabled={pending || catalog === null || !question.trim()}
                  >
                    {pending && pendingLabel === 'Interpreting the question'
                      ? 'Interpreting…'
                      : 'Generate report'}
                  </Button>
                </div>
              </div>
            </form>

            {showQuestionExamples && (
              <section aria-labelledby="pm-v3-question-examples-title">
                <h3
                  id="pm-v3-question-examples-title"
                  className="text-sm font-semibold text-[var(--text-primary)]"
                >
                  Example questions
                </h3>
                <div className="mt-2 space-y-2">
                  {[
                    'Show missed fire-extinguisher inspections by department in 2026.',
                    'How many fire extinguishers are registered in the CCMS building?',
                    'Show non-operational emergency lights for the June 2026 PM cycle.',
                    'Which departments achieved 100% fire-extinguisher PM compliance in 2025?',
                  ].map((example) => (
                    <button
                      key={example}
                      type="button"
                      className="w-full rounded-md border border-[var(--border-soft)] px-3 py-2 text-left text-sm text-[var(--text-secondary)] hover:bg-[var(--surface-muted)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                      onClick={() => {
                        cancelPending()
                        setQuestion(example)
                        setInterpretation(null)
                        setError(null)
                        setInterpretationFailureCode(null)
                        setDrilldownNotice(null)
                      }}
                    >
                      {example}
                    </button>
                  ))}
                </div>
              </section>
            )}
          </div>
        )}

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
        {catalogLoading && catalog?.subject !== selectedSubject && (
          <p role="status" className="text-sm text-[var(--text-secondary)]">
            Loading the selected report subject…
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
        {showPlanSummary && (
          <section aria-labelledby="pm-v3-interpretation-title">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <h3
                  id="pm-v3-interpretation-title"
                  className="font-semibold text-[var(--text-primary)]"
                >
                  Report ready for review
                </h3>
                <p className="mt-1 text-xs text-[var(--text-neutral)]">
                  Check the scope before previewing. The report has not run.
                </p>
              </div>
              <p className="text-xs text-[var(--text-neutral)]">
                {interpretationSourceLabel(interpretation.interpreter)}
              </p>
            </div>
            <dl className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {interpretationSummary.map((item) => (
                <div key={item.label}>
                  <dt className="text-xs font-medium text-[var(--text-neutral)]">
                    {item.label}
                  </dt>
                  <dd className="mt-1 text-sm text-[var(--text-primary)]">
                    {item.value}
                  </dd>
                </div>
              ))}
            </dl>
            <div className="mt-4 flex flex-wrap gap-2 border-t border-[var(--border-soft)] pt-3">
              <Button
                type="button"
                variant="secondary"
                disabled={pending}
                onClick={startEditingQuestion}
              >
                Edit question
              </Button>
              <Button
                type="button"
                disabled={!planReady || pending}
                onClick={() => void handlePreview()}
              >
                {pending && pendingLabel === 'Preparing the preview'
                  ? 'Preparing…'
                  : 'Preview report'}
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={startNewQuestion}
              >
                {selectedSubject === 'pmExecution'
                  ? 'Ask another question'
                  : 'Start another report'}
              </Button>
            </div>
          </section>
        )}

        {showClarificationPanel && clarificationInterpretation && (
          <section
            aria-labelledby="pm-v3-clarification-title"
            className="space-y-4 rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3"
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <h3
                  id="pm-v3-clarification-title"
                  className="font-semibold text-[var(--text-primary)]"
                >
                  Clarify the report scope
                </h3>
                <p className="mt-1 text-sm text-[var(--text-secondary)]">
                  Question: {submittedQuestion || question}
                </p>
              </div>
              <p className="text-xs text-[var(--text-neutral)]">
                {interpretationSourceLabel(
                  clarificationInterpretation.interpreter,
                )}
              </p>
            </div>
            {clarificationInterpretation.detail && (
              <p className="text-sm text-[var(--text-secondary)]">
                {clarificationInterpretation.detail}
              </p>
            )}
            {clarificationInterpretation.plan &&
              clarificationInterpretation.plan.subject !== plan.subject && (
                <p
                  role="status"
                  className="rounded-md border border-[var(--border-soft)] bg-white p-3 text-sm text-[var(--text-secondary)]"
                >
                  Changing the report type cleared the previous categories,
                  measures, period, grouping, and filters. Choose the new scope
                  below; the original question is still shown above.
                </p>
              )}

            {!canGuideClarification ? (
              <div className="space-y-3 rounded-md border border-[var(--border-soft)] bg-white p-3">
                <p className="text-sm text-[var(--text-secondary)]">
                  {clarificationInterpretation.plan === null
                    ? 'UniPM did not return a safe partial plan, so there is no report scope to preview. Edit the question to make the missing scope clear, or try interpretation again.'
                    : 'Changing the report type cleared the prior scope. Edit the question and retry to recover a safe draft for this report type.'}
                </p>
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={pending}
                    onClick={startEditingQuestion}
                  >
                    Edit question
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={pending || !question.trim()}
                    onClick={() => void runInterpretation()}
                  >
                    Retry
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={pending || !question.trim()}
                    onClick={() =>
                      void runInterpretation('DeterministicParser')
                    }
                  >
                    Use deterministic parser
                  </Button>
                </div>
              </div>
            ) : (
              <>
                <div className="space-y-4 rounded-md border border-[var(--border-soft)] bg-white p-3">
                  {needsSubjectAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        Which report type do you mean?
                      </legend>
                      <div className="flex flex-wrap gap-2">
                        {(catalog?.subjects ?? []).map((option) => (
                          <Button
                            key={option.id}
                            type="button"
                            variant={
                              plan.subject === option.id
                                ? 'primary'
                                : 'secondary'
                            }
                            disabled={pending || catalogLoading}
                            onClick={() =>
                              chooseClarificationSubject(option.id)
                            }
                          >
                            {option.label}
                          </Button>
                        ))}
                      </div>
                    </fieldset>
                  )}

                  {needsCategoriesAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        Which asset categories should be included?
                      </legend>
                      <div className="flex flex-wrap gap-2">
                        {categories.map((category) => (
                          <label
                            key={category.id}
                            className="flex min-h-10 items-center gap-2 rounded-md border border-[var(--border-soft)] px-3 text-sm"
                          >
                            <input
                              className={checkboxClass}
                              type="checkbox"
                              checked={plan.categories.includes(category.id)}
                              disabled={pending || catalogLoading}
                              onChange={(event) => {
                                const next = event.target.checked
                                  ? [...plan.categories, category.id]
                                  : plan.categories.filter(
                                      (value) => value !== category.id,
                                    )
                                updateClarificationPlan({
                                  ...plan,
                                  categories: next,
                                })
                              }}
                            />
                            {category.label}
                          </label>
                        ))}
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={
                            pending || catalogLoading || categories.length === 0
                          }
                          onClick={() => {
                            updateClarificationPlan({
                              ...plan,
                              categories: categories.map((item) => item.id),
                            })
                            answerClarification('Categories')
                          }}
                        >
                          All supported categories
                        </Button>
                      </div>
                      {plan.categories.length > 0 &&
                        !clarificationWasAnswered('Categories') && (
                          <Button
                            type="button"
                            variant="secondary"
                            disabled={pending || catalogLoading}
                            onClick={() => answerClarification('Categories')}
                          >
                            Use selected categories
                          </Button>
                        )}
                    </fieldset>
                  )}

                  {needsMeasuresAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        What should the report measure?
                      </legend>
                      <p className="text-xs text-[var(--text-secondary)]">
                        Missed counts past-due cycles without completion.
                        Completed late counts work finished after its PM
                        deadline. On-time compliance counts work finished by
                        that deadline, while progress includes completed work
                        even when it was late.
                      </p>
                      <div className="grid gap-2 sm:grid-cols-2">
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
                                pending ||
                                catalogLoading ||
                                (plan.measures.length >= 4 &&
                                  !plan.measures.includes(measure.id))
                              }
                              onChange={() => {
                                const selected = plan.measures.includes(
                                  measure.id,
                                )
                                const nextMeasures = selected
                                  ? plan.measures.filter(
                                      (value) => value !== measure.id,
                                    )
                                  : [...plan.measures, measure.id]
                                const nextColumns = selected
                                  ? plan.columns.filter(
                                      (value) => value !== measure.id,
                                    )
                                  : [...plan.columns, measure.id]
                                updateClarificationPlan({
                                  ...plan,
                                  measures: nextMeasures,
                                  columns: nextColumns,
                                  aggregateFilters: selected
                                    ? plan.aggregateFilters.filter(
                                        (filter) =>
                                          filter.measure !== measure.id,
                                      )
                                    : plan.aggregateFilters,
                                  top:
                                    plan.top?.measure === measure.id
                                      ? null
                                      : plan.top,
                                  orderBy: plan.orderBy.filter((order) =>
                                    nextColumns.includes(order.field),
                                  ),
                                })
                              }}
                            />
                            <span>
                              <span className="block font-medium">
                                {measure.label}
                              </span>
                              <span className="text-xs text-[var(--text-neutral)]">
                                {measure.definition}
                              </span>
                            </span>
                          </label>
                        ))}
                      </div>
                      {plan.measures.length > 0 &&
                        !clarificationWasAnswered('Measures') && (
                          <Button
                            type="button"
                            variant="secondary"
                            disabled={pending || catalogLoading}
                            onClick={() => answerClarification('Measures')}
                          >
                            Use selected measures
                          </Button>
                        )}
                    </fieldset>
                  )}

                  {needsPeriodBasisAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        Which date defines this reporting period?
                      </legend>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          variant={
                            clarificationChoices.periodBasis === 'pmCycle'
                              ? 'primary'
                              : 'secondary'
                          }
                          disabled={pending}
                          onClick={() =>
                            chooseClarificationPeriodBasis('pmCycle')
                          }
                        >
                          Scheduled PM cycle
                        </Button>
                        <Button
                          type="button"
                          variant={
                            clarificationChoices.periodBasis ===
                            'inspectionDate'
                              ? 'primary'
                              : 'secondary'
                          }
                          disabled={pending}
                          onClick={() =>
                            chooseClarificationPeriodBasis('inspectionDate')
                          }
                        >
                          Actual inspection date
                        </Button>
                      </div>
                      {clarificationChoices.periodBasis ===
                        'inspectionDate' && (
                        <p className="text-xs text-[var(--error)]">
                          Reports cannot filter by actual inspection date.
                          Choose scheduled PM cycle or edit the question.
                        </p>
                      )}
                    </fieldset>
                  )}

                  {needsPeriodAnswer && (
                    <fieldset className="space-y-3">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        Which scheduled PM period should be included?
                      </legend>
                      <div className="grid gap-3 sm:grid-cols-2">
                        <div>
                          <Label htmlFor="pm-v3-clarification-period-kind">
                            Period type
                          </Label>
                          <select
                            id="pm-v3-clarification-period-kind"
                            className={inputClass}
                            value={
                              clarificationChoices.periodKindChosen
                                ? plan.period.kind
                                : ''
                            }
                            disabled={pending || catalogLoading}
                            onChange={(event) =>
                              chooseClarificationPeriodKind(
                                event.target.value as PeriodKind,
                              )
                            }
                          >
                            <option value="">Choose a period type</option>
                            <option value="Month">Month</option>
                            <option value="Quarter">Quarter</option>
                            <option value="Year">Year</option>
                            <option value="Range">Cycle range</option>
                          </select>
                        </div>
                      </div>
                      {clarificationChoices.periodKindChosen &&
                        (plan.period.kind === 'Range' ? (
                          <div className="grid gap-3 sm:grid-cols-2">
                            <div>
                              <Label htmlFor="pm-v3-clarification-range-start">
                                First cycle
                              </Label>
                              <input
                                id="pm-v3-clarification-range-start"
                                className={inputClass}
                                type="month"
                                min="2000-01"
                                max="9998-12"
                                value={plan.period.startCycle ?? ''}
                                disabled={pending || catalogLoading}
                                onChange={(event) =>
                                  updateClarificationPeriod({
                                    ...plan.period,
                                    startCycle: event.target.value || null,
                                  })
                                }
                              />
                            </div>
                            <div>
                              <Label htmlFor="pm-v3-clarification-range-end">
                                Last cycle
                              </Label>
                              <input
                                id="pm-v3-clarification-range-end"
                                className={inputClass}
                                type="month"
                                min="2000-01"
                                max="9998-12"
                                value={plan.period.endCycle ?? ''}
                                disabled={pending || catalogLoading}
                                onChange={(event) =>
                                  updateClarificationPeriod({
                                    ...plan.period,
                                    endCycle: event.target.value || null,
                                  })
                                }
                              />
                            </div>
                          </div>
                        ) : (
                          <div className="grid gap-3 sm:grid-cols-3">
                            <div>
                              <Label htmlFor="pm-v3-clarification-period-year">
                                Year
                              </Label>
                              <input
                                id="pm-v3-clarification-period-year"
                                className={inputClass}
                                type="number"
                                min={2000}
                                max={9998}
                                value={plan.period.year ?? ''}
                                disabled={pending || catalogLoading}
                                onChange={(event) =>
                                  updateClarificationPeriod({
                                    ...plan.period,
                                    year: Number(event.target.value) || null,
                                  })
                                }
                              />
                            </div>
                            {plan.period.kind === 'Month' && (
                              <div>
                                <Label htmlFor="pm-v3-clarification-period-month">
                                  Month
                                </Label>
                                <select
                                  id="pm-v3-clarification-period-month"
                                  className={inputClass}
                                  value={plan.period.month ?? ''}
                                  disabled={pending || catalogLoading}
                                  onChange={(event) =>
                                    updateClarificationPeriod({
                                      ...plan.period,
                                      month: Number(event.target.value) || null,
                                    })
                                  }
                                >
                                  <option value="">Choose a month</option>
                                  {Array.from(
                                    { length: 12 },
                                    (_, index) => index + 1,
                                  ).map((month) => (
                                    <option key={month} value={month}>
                                      {new Intl.DateTimeFormat('en-PH', {
                                        month: 'long',
                                      }).format(
                                        new Date(Date.UTC(2020, month - 1, 1)),
                                      )}
                                    </option>
                                  ))}
                                </select>
                              </div>
                            )}
                            {plan.period.kind === 'Quarter' && (
                              <div>
                                <Label htmlFor="pm-v3-clarification-period-quarter">
                                  Quarter
                                </Label>
                                <select
                                  id="pm-v3-clarification-period-quarter"
                                  className={inputClass}
                                  value={plan.period.quarter ?? ''}
                                  disabled={pending || catalogLoading}
                                  onChange={(event) =>
                                    updateClarificationPeriod({
                                      ...plan.period,
                                      quarter:
                                        Number(event.target.value) || null,
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
                        ))}
                      {isPeriodReady(plan.period) &&
                        clarificationFieldSet.has('Period') &&
                        !clarificationWasAnswered('Period') && (
                          <Button
                            type="button"
                            variant="secondary"
                            disabled={pending || catalogLoading}
                            onClick={() => answerClarification('Period')}
                          >
                            Use this period
                          </Button>
                        )}
                    </fieldset>
                  )}

                  {needsModeAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        Do you want measures or matching records?
                      </legend>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          variant={
                            plan.mode === 'Aggregate' ? 'primary' : 'secondary'
                          }
                          disabled={pending || catalogLoading}
                          onClick={() => chooseClarificationMode('Aggregate')}
                        >
                          Measures and groups
                        </Button>
                        <Button
                          type="button"
                          variant={
                            plan.mode === 'Records' ? 'primary' : 'secondary'
                          }
                          disabled={pending || catalogLoading}
                          onClick={() => chooseClarificationMode('Records')}
                        >
                          Matching records
                        </Button>
                      </div>
                    </fieldset>
                  )}

                  {needsGroupingAnswer && (
                    <fieldset className="space-y-2">
                      <legend className="text-sm font-medium text-[var(--text-primary)]">
                        How should results be grouped?
                      </legend>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={pending || catalogLoading}
                          onClick={() => {
                            const dimensionIds = new Set(
                              dimensions.map((item) => item.id),
                            )
                            updateClarificationPlan({
                              ...plan,
                              groupBy: [],
                              columns: plan.columns.filter(
                                (column) => !dimensionIds.has(column),
                              ),
                              presentation:
                                plan.presentation === 'Table'
                                  ? 'Summary'
                                  : plan.presentation,
                            })
                            answerClarification('GroupBy')
                          }}
                        >
                          No grouping
                        </Button>
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
                                pending ||
                                catalogLoading ||
                                (plan.groupBy.length >= 2 &&
                                  !plan.groupBy.includes(dimension.id))
                              }
                              onChange={(event) => {
                                const nextGroups = event.target.checked
                                  ? [...plan.groupBy, dimension.id]
                                  : plan.groupBy.filter(
                                      (value) => value !== dimension.id,
                                    )
                                const dimensionIds = new Set(
                                  dimensions.map((item) => item.id),
                                )
                                const nextColumns = [
                                  ...plan.columns.filter(
                                    (column) =>
                                      !dimensionIds.has(column) ||
                                      nextGroups.includes(column),
                                  ),
                                  ...nextGroups.filter(
                                    (value) => !plan.columns.includes(value),
                                  ),
                                ]
                                updateClarificationPlan({
                                  ...plan,
                                  groupBy: nextGroups,
                                  columns: nextColumns,
                                  presentation:
                                    nextGroups.length > 0
                                      ? 'Table'
                                      : plan.presentation,
                                })
                              }}
                            />
                            {dimension.label}
                          </label>
                        ))}
                      </div>
                      {plan.groupBy.length > 0 &&
                        !clarificationWasAnswered('GroupBy') && (
                          <Button
                            type="button"
                            variant="secondary"
                            disabled={pending || catalogLoading}
                            onClick={() => answerClarification('GroupBy')}
                          >
                            Use selected grouping
                          </Button>
                        )}
                    </fieldset>
                  )}

                  {needsGenericFilterAnswer && (
                    <div className="space-y-2">
                      <Label htmlFor="pm-v3-clarification-filter">
                        Which filter should narrow the results?
                      </Label>
                      <select
                        id="pm-v3-clarification-filter"
                        className={inputClass}
                        value={clarificationChoices.filterTarget ?? ''}
                        disabled={pending || catalogLoading}
                        onChange={(event) =>
                          chooseClarificationFilterTarget(
                            event.target.value as string | 'none',
                          )
                        }
                      >
                        <option value="">Choose a filter</option>
                        <option value="none">No additional filter</option>
                        {baseFilterFields.map((field) => (
                          <option key={field.id} value={field.id}>
                            {field.label}
                          </option>
                        ))}
                      </select>
                      {clarificationChoices.filterTarget === 'none' && (
                        <p className="text-xs text-[var(--text-secondary)]">
                          Existing filters from the interpreted question stay in
                          place.
                        </p>
                      )}
                    </div>
                  )}

                  {needsNamedFilterAnswers.map((name) => {
                    const field = findBaseFilterForClarification(name)
                    if (!field) return null
                    const active = findFilter(plan, field.id)
                    const noFilterSelected =
                      !active && clarificationWasAnswered(name)
                    return (
                      <div key={name} className="space-y-2">
                        <BaseFilterPicker
                          subject={selectedSubject}
                          field={field.id}
                          label={field.label}
                          categories={plan.categories}
                          values={active?.values ?? []}
                          isNull={active?.operator === 'IsNull'}
                          maximumLength={field.maximumLength}
                          allowNull={
                            field.operators?.includes('IsNull') ?? false
                          }
                          searchable={field.valueType === 'string'}
                          disabled={
                            pending ||
                            catalogLoading ||
                            plan.categories.length === 0
                          }
                          onChange={(values, isNull) =>
                            updateClarificationFilterValues(
                              field.id,
                              values,
                              isNull,
                            )
                          }
                        />
                        {active &&
                          ((active.values?.length ?? 0) > 0 ||
                            active.operator === 'IsNull') &&
                          !clarificationWasAnswered(name) && (
                            <Button
                              type="button"
                              variant="secondary"
                              disabled={pending || catalogLoading}
                              onClick={() => answerClarification(name)}
                            >
                              Use selected {name.toLowerCase()}
                            </Button>
                          )}
                        <label className="flex items-center gap-2 text-xs text-[var(--text-secondary)]">
                          <input
                            className={checkboxClass}
                            type="checkbox"
                            checked={noFilterSelected}
                            disabled={pending || catalogLoading}
                            onChange={(event) => {
                              const existing = plan.baseFilters.filter(
                                (item) => item.field !== field.id,
                              )
                              updateClarificationPlan({
                                ...plan,
                                baseFilters: existing,
                              })
                              if (event.target.checked)
                                answerClarification(name)
                              else reopenClarification(name)
                            }}
                          />
                          Include all {name.toLowerCase()} values
                        </label>
                      </div>
                    )
                  })}

                  {needsGenericFilterAnswer &&
                    clarificationChoices.filterTarget &&
                    clarificationChoices.filterTarget !== 'none' &&
                    (() => {
                      const field = baseFilterFields.find(
                        (item) => item.id === clarificationChoices.filterTarget,
                      )
                      if (!field) return null
                      const active = findFilter(plan, field.id)
                      return (
                        <div className="space-y-2">
                          <BaseFilterPicker
                            subject={selectedSubject}
                            field={field.id}
                            label={field.label}
                            categories={plan.categories}
                            values={active?.values ?? []}
                            isNull={active?.operator === 'IsNull'}
                            maximumLength={field.maximumLength}
                            allowNull={
                              field.operators?.includes('IsNull') ?? false
                            }
                            searchable={field.valueType === 'string'}
                            disabled={
                              pending ||
                              catalogLoading ||
                              plan.categories.length === 0
                            }
                            onChange={(values, isNull) =>
                              updateClarificationFilterValues(
                                field.id,
                                values,
                                isNull,
                              )
                            }
                          />
                          {active &&
                            ((active.values?.length ?? 0) > 0 ||
                              active.operator === 'IsNull') &&
                            !clarificationWasAnswered('Filters') && (
                              <Button
                                type="button"
                                variant="secondary"
                                disabled={pending || catalogLoading}
                                onClick={() => answerClarification('Filters')}
                              >
                                Use selected filter
                              </Button>
                            )}
                        </div>
                      )
                    })()}
                </div>

                {unresolvedUnsupportedClarificationFields.length > 0 && (
                  <p className="text-sm text-[var(--text-secondary)]">
                    This question also needs clarification about{' '}
                    {unresolvedUnsupportedClarificationFields.join(', ')}. Edit
                    the question to resolve that detail safely.
                  </p>
                )}

                <div>
                  <h4 className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase">
                    Scope so far
                  </h4>
                  <dl className="mt-2 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                    {interpretationSummary
                      .filter((item) => item.label !== 'Question')
                      .map((item) => (
                        <div key={item.label}>
                          <dt className="text-xs font-medium text-[var(--text-neutral)]">
                            {item.label}
                          </dt>
                          <dd className="mt-1 text-sm text-[var(--text-primary)]">
                            {item.value}
                          </dd>
                        </div>
                      ))}
                  </dl>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3">
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={pending}
                    onClick={startEditingQuestion}
                  >
                    Edit question
                  </Button>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={pending || !question.trim()}
                      onClick={() => void runInterpretation()}
                    >
                      Retry interpretation
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={pending || !question.trim()}
                      onClick={() =>
                        void runInterpretation('DeterministicParser')
                      }
                    >
                      Use deterministic parser
                    </Button>
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
                </div>
              </>
            )}
          </section>
        )}

        {interpretation &&
          interpretation.status !== 'Valid' &&
          interpretation.status !== 'NeedsClarification' &&
          !manualEditorOpen && (
            <section
              aria-labelledby="pm-v3-interpretation-title"
              className="rounded-md border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3 text-sm"
            >
              <h3
                id="pm-v3-interpretation-title"
                className="font-semibold text-[var(--text-primary)]"
              >
                The question is outside the supported report scope.
              </h3>
              <p className="mt-1 text-xs text-[var(--text-neutral)]">
                {interpretationSourceLabel(interpretation.interpreter)}.
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
              <p className="mt-3 text-xs text-[var(--text-neutral)]">
                Edit the question to continue.
              </p>
            </section>
          )}

        {selectedSubject === 'pmExecution' &&
          manualEditorOpen &&
          !preview &&
          !response && (
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3">
              <p className="text-xs text-[var(--text-neutral)]">
                Your current report values are kept while you switch views.
              </p>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setManualEditorOpen(false)}
              >
                Back to question
              </Button>
            </div>
          )}

        {error && (
          <div
            role="alert"
            className="flex flex-wrap items-center justify-between gap-3 text-sm text-[var(--error)]"
          >
            <p>{error}</p>
            {stage === 'question' && (
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  variant="secondary"
                  disabled={pending || !question.trim()}
                  onClick={() => void runInterpretation()}
                >
                  Retry
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={pending || !question.trim()}
                  onClick={() => void runInterpretation('DeterministicParser')}
                >
                  Use deterministic parser
                </Button>
              </div>
            )}
          </div>
        )}
        {pending && (
          <p role="status" className="text-sm text-[var(--text-secondary)]">
            {pendingLabel}…
          </p>
        )}
      </Card>

      {manualEditorOpen &&
        (catalog?.subject === selectedSubject ? (
          <Card className="space-y-6 p-4 shadow-none sm:p-5">
            <div className="border-b border-[var(--border-soft)] pb-3">
              <h2 className="text-lg font-semibold text-[var(--text-primary)]">
                {isInventory
                  ? 'Asset inventory parameters'
                  : 'Report parameters'}
              </h2>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                Any change clears an existing preview and result. Your selected
                values remain editable.
              </p>
            </div>
            <div
              className={`grid gap-4 ${subject?.requiresPeriod ? 'md:grid-cols-2' : 'md:grid-cols-1'}`}
            >
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
              {subject?.requiresPeriod && (
                <div>
                  <Label htmlFor="pm-v3-period-kind">
                    Scheduled PM cycle period
                  </Label>
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
              )}
            </div>

            {subject?.requiresPeriod &&
              (plan.period.kind !== 'Range' ? (
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
                        {Array.from(
                          { length: 12 },
                          (_, index) => index + 1,
                        ).map((month) => (
                          <option key={month} value={month}>
                            {new Intl.DateTimeFormat('en-PH', {
                              month: 'long',
                            }).format(new Date(Date.UTC(2020, month - 1, 1)))}
                          </option>
                        ))}
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
                    <Label htmlFor="pm-v3-range-start">
                      First cycle (YYYY-MM)
                    </Label>
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
                    <Label htmlFor="pm-v3-range-end">
                      Last cycle (YYYY-MM)
                    </Label>
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
              ))}

            <fieldset className="space-y-2">
              <legend className="text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase">
                Asset categories
              </legend>
              <p className="text-xs text-[var(--text-neutral)]">
                {selectedSubject === 'assetInventory'
                  ? 'Choose which registered asset categories to count. PM schedules and inspections do not change inventory counts.'
                  : 'Choose every category to include. Each category has its own scheduled PM cycles.'}
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
                          <span className="block font-medium">
                            {measure.label}
                          </span>
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
                  These filters narrow the eligible population before measures
                  are calculated.
                </p>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                {baseFilterFields.map((field) => {
                  const active = findFilter(plan, field.id)
                  if (field.allowedValues.length > 0) {
                    return (
                      <div key={field.id}>
                        <Label htmlFor={`pm-v3-base-${field.id}`}>
                          {field.label}
                        </Label>
                        <select
                          id={`pm-v3-base-${field.id}`}
                          className={inputClass}
                          multiple
                          size={Math.min(4, field.allowedValues.length)}
                          value={active?.values ?? []}
                          disabled={
                            catalogLoading || plan.categories.length === 0
                          }
                          onChange={(event) =>
                            updateBaseFilterValues(
                              field.id,
                              Array.from(
                                event.target.selectedOptions,
                                (option) => option.value,
                              ),
                              false,
                            )
                          }
                        >
                          {field.allowedValues.map((value) => (
                            <option key={value} value={value}>
                              {formatLabel(value, catalog ?? undefined)}
                            </option>
                          ))}
                        </select>
                        <p className="mt-1 text-xs text-[var(--text-neutral)]">
                          Stored registration status. This does not describe
                          inspection condition.
                        </p>
                      </div>
                    )
                  }

                  return (
                    <BaseFilterPicker
                      key={field.id}
                      subject={selectedSubject}
                      field={field.id}
                      label={field.label}
                      categories={plan.categories}
                      values={active?.values ?? []}
                      isNull={active?.operator === 'IsNull'}
                      maximumLength={field.maximumLength}
                      allowNull={field.operators?.includes('IsNull') ?? false}
                      searchable={field.valueType === 'string'}
                      disabled={catalogLoading || plan.categories.length === 0}
                      onChange={(values, isNull) =>
                        updateBaseFilterValues(field.id, values, isNull)
                      }
                    />
                  )
                })}
              </div>
            </section>

            {outcomeFilterFields.length > 0 && (
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
                    These filters define matching rows and count numerators.
                    They cannot be combined with percentage measures.
                  </p>
                </div>
                {matchingFiltersConflict && (
                  <p
                    role="alert"
                    className="rounded-md border border-[var(--error)]/30 bg-[var(--error)]/5 p-3 text-sm text-[var(--error)]"
                  >
                    Clear the matching record filters or remove percentage
                    measures before previewing.
                  </p>
                )}
                <div className="grid gap-3 sm:grid-cols-2">
                  {outcomeFilterFields.map((field) => {
                    const active = findMatchingFilter(plan, field.id)
                    const values =
                      active?.operator === 'IsNull'
                        ? []
                        : (active?.values ?? [])
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
                          Select multiple values with Ctrl or Command. No
                          selection means any.
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
            )}

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
                        Thresholds apply after aggregation. An unmeasurable
                        group stops the report with an explanation.
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
                            max={
                              definition?.unit === 'percent' ? 100 : undefined
                            }
                            step={
                              definition?.valueType === 'integer' ? 1 : 0.01
                            }
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
                        onChange={(event) =>
                          setTopEnabled(event.target.checked)
                        }
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
                          <Label htmlFor="pm-v3-top-limit">
                            Number of groups
                          </Label>
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
                                top: {
                                  ...plan.top!,
                                  measure: event.target.value,
                                },
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
                            Choose at least one grouping dimension to rank
                            groups.
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

            <section
              className="space-y-2"
              aria-labelledby="pm-v3-columns-title"
            >
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
                    plan.mode === 'Aggregate' &&
                    plan.groupBy.includes(column.id)
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
                            ? plan.columns.filter(
                                (value) => value !== column.id,
                              )
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
                !plan.columns.some((column) =>
                  plan.measures.includes(column),
                ) && (
                  <p
                    role="status"
                    className="text-xs text-[var(--text-neutral)]"
                  >
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
                      orderBy: [
                        ...plan.orderBy,
                        { field, direction: 'Ascending' },
                      ],
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
                        nextOrder[index] = {
                          ...order,
                          field: event.target.value,
                        }
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
                denominator. A selected cycle that is active or future makes
                on-time compliance not measurable.
              </p>
            )}

            {pending && (
              <p role="status" className="text-sm text-[var(--text-secondary)]">
                {pendingLabel}…
              </p>
            )}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-4">
              <p className="max-w-2xl text-xs text-[var(--text-neutral)]">
                {subject?.requiresPeriod && (
                  <>Period: {planPeriodDescription(plan.period)} · </>
                )}
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
        ) : (
          <Card className="p-4 shadow-none sm:p-5">
            <p role="status" className="text-sm text-[var(--text-secondary)]">
              {catalogLoading
                ? 'Loading the selected report subject…'
                : 'The selected report catalog is unavailable.'}
            </p>
          </Card>
        ))}

      {preview && stage === 'preview' && (
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
          {Object.keys(preview.cyclesByCategory).length > 0 && (
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
          )}
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
              {isInventory
                ? 'This report reads current registered asset records directly. It does not include PM schedules, inspection findings, or change stored records.'
                : 'Running this report reads current persisted PM schedules and inspections. Execution does not change maintenance records.'}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={pending}
                onClick={() => setStage(response ? 'results' : 'plan')}
              >
                {response ? 'Return to results' : 'Back to report details'}
              </Button>
              {!response && (
                <>
                  <Button
                    type="button"
                    disabled={pending}
                    onClick={() => void executePage(1)}
                  >
                    {pending && pendingLabel === 'Running the confirmed report'
                      ? 'Running…'
                      : 'Confirm and generate'}
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={pending}
                    onClick={startEditingQuestion}
                  >
                    Edit question
                  </Button>
                </>
              )}
              <Button
                type="button"
                variant="secondary"
                disabled={pending}
                onClick={startNewQuestion}
              >
                {selectedSubject === 'pmExecution'
                  ? 'Ask another question'
                  : 'Start another report'}
              </Button>
            </div>
          </div>
        </Card>
      )}

      {response && catalog && stage === 'results' && (
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
                {isInventory
                  ? 'Registered asset inventory results'
                  : 'PM execution results'}
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
            <div className="flex flex-wrap gap-2 print:hidden">
              <Button
                type="button"
                variant="secondary"
                disabled={pending}
                onClick={() => setStage('preview')}
              >
                Back to preview
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={pending}
                onClick={startEditingQuestion}
              >
                Edit question
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={() => window.print()}
              >
                Print result
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={startNewQuestion}
              >
                {selectedSubject === 'pmExecution'
                  ? 'Ask another question'
                  : 'Start another report'}
              </Button>
            </div>
          </div>

          {preview && (
            <details className="rounded-md border border-[var(--border-soft)] px-3 py-2">
              <summary className="cursor-pointer text-sm font-medium text-[var(--primary)]">
                Report scope
              </summary>
              <p className="mt-2 text-sm text-[var(--text-secondary)]">
                {preview.summary}
              </p>
            </details>
          )}

          <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {(isInventory
              ? [
                  [
                    'Registered assets in population',
                    response.population.eligibleDistinctAssetCount,
                  ],
                  [
                    'Assets returned by filters',
                    response.population.sourceTotalCount,
                  ],
                ]
              : [
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
                ]
            ).map(([label, value]) => (
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
                          {result?.unit === 'assets'
                            ? `${formatNumber(result.value)} registered assets; no denominator applies.`
                            : `${formatNumber(result?.numerator)} of ${formatNumber(result?.denominator)} ${result?.unit === 'percent' ? 'eligible asset cycles' : (result?.unit ?? '')}`}
                        </p>
                        {result?.unmeasurableReason && (
                          <p className="mt-1 text-xs text-[var(--text-neutral)]">
                            {result.unmeasurableReason}
                          </p>
                        )}
                        {!isInventory && (
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
                        )}
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
                sourceReturnContext={{
                  kind: 'analytics',
                  sessionId: analyticsSessionId,
                }}
                onSourceNavigate={saveBeforeSourceNavigation}
                {...(!isInventory && {
                  onMeasureSource: (
                    values: Record<string, unknown>,
                    measure: string,
                  ) =>
                    handleMeasureSources(
                      values,
                      measure,
                      response.cyclesByCategory,
                    ),
                })}
              />
            )}
          {response.mode === 'Records' &&
            (response.records.length > 0 ? (
              <ResultTable
                columns={response.columns}
                rows={response.records}
                mode="Records"
                catalog={catalog}
                sourceReturnContext={{
                  kind: 'analytics',
                  sessionId: analyticsSessionId,
                }}
                onSourceNavigate={saveBeforeSourceNavigation}
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
                        {measure.unit === 'assets'
                          ? ' Source: Assets table, one row per unique Asset ID. No denominator applies.'
                          : ''}
                      </dd>
                      {result && (
                        <dd className="mt-2 text-xs text-[var(--text-neutral)]">
                          {result.unit === 'assets'
                            ? `${formatNumber(result.value)} registered assets from ${formatNumber(result.eligibleRowCount)} filtered Asset rows; no denominator applies.`
                            : `Numerator ${formatNumber(result.numerator)} of denominator ${formatNumber(result.denominator)}; ${formatNumber(result.eligibleRowCount)} eligible rows and ${formatNumber(result.matchingRowCount)} matching rows.`}
                          {result.unmeasurableReason
                            ? ` ${result.unmeasurableReason}`
                            : ''}
                        </dd>
                      )}
                    </div>
                  )
                })}
              </dl>
              {!isInventory && (
                <p className="mt-3 text-sm text-[var(--text-secondary)]">
                  Source-record actions open a separate records plan filtered to
                  the rows contributing to each measure numerator. Review its
                  preview before running it.
                </p>
              )}
            </details>
          )}

          {Number(response.totalPages) > 1 && (
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3 print:hidden">
              <p className="text-sm text-[var(--text-secondary)]">
                Page {response.page} of {response.totalPages} ·{' '}
                {formatNumber(response.population.sourceTotalCount)}{' '}
                {isInventory ? 'registered assets' : 'matching rows'}
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
  sourceReturnContext,
  onSourceNavigate,
}: {
  columns: Column[]
  rows: Array<{
    scheduleId?: string | null
    assetId?: string
    inspectionId?: string | null
    values: Record<string, unknown>
    measures?: Record<string, MeasureResult>
  }>
  mode: Mode
  catalog: Catalog
  groupBy?: string[]
  onMeasureSource?: (values: Record<string, unknown>, measure: string) => void
  sourceReturnContext: DetailReturnContext
  onSourceNavigate: () => void
}) {
  const visibleColumns = columns.filter(
    (column) => column.id !== 'scheduleId' && column.id !== 'assetId',
  )

  return (
    <div className="overflow-x-auto rounded-md border border-[var(--border-soft)]">
      <table className="min-w-full border-collapse text-left text-sm">
        <caption className="sr-only">
          {mode === 'Aggregate'
            ? 'Aggregated report results'
            : catalog.subject === 'assetInventory'
              ? 'Registered asset inventory records'
              : 'Matching PM execution records'}
        </caption>
        <thead className="bg-[var(--surface-muted)] text-xs font-semibold text-[var(--text-secondary)] uppercase">
          <tr>
            {visibleColumns.map((column) => (
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
              {visibleColumns.map((column) => {
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
                        title={
                          measure.unit === 'assets'
                            ? 'Unique registered assets; no denominator applies'
                            : 'Numerator and denominator for this measure'
                        }
                      >
                        {measure.unit === 'assets'
                          ? `${formatNumber(measure.value)} assets`
                          : `${formatNumber(measure.numerator)} of ${formatNumber(measure.denominator)}`}
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
                      search={{ returnContext: sourceReturnContext }}
                      onClick={onSourceNavigate}
                      className="font-medium text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
                    >
                      Review inspection
                    </Link>
                  ) : row.assetId ? (
                    <Link
                      to="/app/assets/$assetId"
                      params={{ assetId: row.assetId }}
                      search={{ returnContext: sourceReturnContext }}
                      onClick={onSourceNavigate}
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
