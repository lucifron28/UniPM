import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from '@tanstack/react-router'
import {
  interpretPmAnalyticsV2Question,
  queryPmAnalyticsV2,
} from '@/api/generated/endpoints'
import type {
  PmAnalyticsV2InterpretationResponse,
  PmAnalyticsV2MeasureResponse,
  PmAnalyticsV2Period,
  PmAnalyticsV2Plan,
  PmAnalyticsV2QueryResponse,
  PmAnalyticsV2SourceResponse,
  PmAnalyticsV2SourceScope,
} from '@/api/generated/models'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useCurrentUser } from '@/features/auth/current-user'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'

const pageSize = 25

const clarificationPrompts: Record<string, string> = {
  Metric: 'Which PM measure should be used?',
  AssetCategory: 'Which supported asset category should be included?',
  Period: 'Which month, quarter, year, or cycle range should be used?',
  PeriodKind: 'Should the period be a month, quarter, year, or cycle range?',
  Year: 'Which year should be used?',
  Month: 'Which month should be used?',
  Quarter: 'Which quarter should be used?',
  StartCycle: 'What is the first PM cycle in the range?',
  EndCycle: 'What is the last PM cycle in the range?',
  Comparison: 'Which two PM cycles should be compared?',
  ComparisonCycle: 'Which two PM cycles should be compared?',
  EarlierCycle: 'Which earlier PM cycle should be compared?',
  LaterCycle: 'Which later PM cycle should be compared?',
  Department: 'Which department should be included?',
  Building: 'Which building should be included?',
  AssetCode: 'Which specific asset should be included?',
  GroupBy:
    'Should the results be grouped by department, building, or PM cycle?',
  Condition: 'Which inspection condition should be included?',
  Timeliness: 'Which completion timeliness should be included?',
  Presentation:
    'Should the result be shown as a count, percentage, or records?',
  CountBasis: 'Should counts represent asset cycles or distinct assets?',
  Ranking: 'How many departments should be ranked?',
  Direction: 'Should departments be ranked highest or lowest?',
}

function toNumber(value: number | string | null | undefined) {
  if (value === null || value === undefined || value === '') return null
  const normalized = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(normalized) ? normalized : null
}

function formatNumber(value: number | string | null | undefined) {
  const normalized = toNumber(value)
  return normalized === null
    ? 'Not recorded'
    : new Intl.NumberFormat('en-PH', {
        maximumFractionDigits: 2,
      }).format(normalized)
}

function formatLabel(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  const labels: Record<string, string> = {
    OnTime: 'Completed on time',
    Late: 'Completed late',
    NonOperational: 'Non-operational',
    Operational: 'Operational',
    NotInspected: 'Not inspected',
    NotCompleted: 'Not completed',
    Remaining: 'Remaining',
    Missed: 'Missed',
    Future: 'Future',
    Draft: 'Draft',
    Submitted: 'Submitted',
    Acknowledged: 'Acknowledged',
    Active: 'Active',
    Closed: 'Closed',
    Count: 'Count',
    Percent: 'Percent',
    Records: 'Records',
    AssetCycles: 'Asset cycles',
    DistinctAssets: 'Distinct assets',
    None: 'Overall',
    Department: 'Department',
    Building: 'Building',
    PmCycle: 'PM cycle',
  }
  return labels[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function formatDate(value: string | null | undefined) {
  if (!value) return 'Not recorded'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Not recorded'

  return new Intl.DateTimeFormat('en-PH', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'Asia/Manila',
    timeZoneName: 'short',
  }).format(date)
}

function formatMetric(metric: string, countBasis: string) {
  const labels: Record<string, string> = {
    Progress: countBasis === 'DistinctAssets' ? 'Inspected assets' : 'Progress',
    OnTimeCompliance:
      countBasis === 'DistinctAssets'
        ? 'Assets completed on time'
        : 'On-time compliance',
    CompletedLate:
      countBasis === 'DistinctAssets'
        ? 'Assets with late inspections'
        : 'Late inspections',
    NonOperational:
      countBasis === 'DistinctAssets'
        ? 'Non-operational assets'
        : 'Non-operational inspections',
    Remaining:
      countBasis === 'DistinctAssets'
        ? 'Assets with remaining schedules'
        : 'Remaining schedules',
    Missed:
      countBasis === 'DistinctAssets'
        ? 'Assets with missed schedules'
        : 'Missed schedules',
  }
  return labels[metric] ?? formatLabel(metric)
}

function describeMetric(plan: PmAnalyticsV2Plan) {
  if (plan.metric === 'Progress') {
    if (plan.presentation === 'Percent') {
      return 'Completed schedules divided by the eligible scheduled population.'
    }
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with at least one completed inspection in the selected scope.'
      : 'Completed inspections counted by asset and PM cycle.'
  }
  if (plan.metric === 'OnTimeCompliance') {
    if (plan.presentation === 'Percent') {
      return 'Inspections completed by the PM deadline divided by the eligible scheduled population. Included cycles must be closed.'
    }
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with an inspection completed by the PM deadline.'
      : 'Inspections completed by the PM deadline, counted by asset and PM cycle.'
  }
  if (plan.metric === 'CompletedLate') {
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with an inspection completed after the PM deadline.'
      : 'Inspections completed after the PM deadline, counted by asset and PM cycle.'
  }
  if (plan.metric === 'NonOperational') {
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with a non-operational inspection.'
      : 'Inspections recorded with a non-operational condition.'
  }
  if (plan.metric === 'Remaining') {
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with unfinished schedules in active PM cycles only. Future cycles are excluded.'
      : 'Unfinished schedules in active PM cycles only. Future cycles are excluded.'
  }
  if (plan.metric === 'Missed') {
    return plan.countBasis === 'DistinctAssets'
      ? 'Distinct assets with unfinished schedules in closed PM cycles only. Missing schedule rows are not inferred.'
      : 'Unfinished schedules in closed PM cycles only. Missing schedules are not inferred.'
  }
  return 'PM records for the selected scope.'
}

function formatDeltaUnit(value: string) {
  const labels: Record<string, string> = {
    PercentagePoints: 'percentage points',
    Count: 'count',
    AssetCycles: 'asset cycles',
    DistinctAssets: 'distinct assets',
  }
  return labels[value] ?? formatLabel(value).toLowerCase()
}

function formatPeriod(period: PmAnalyticsV2Period | null | undefined) {
  if (!period) return 'the selected PM cycles'
  const year = toNumber(period.year)
  const month = toNumber(period.month)
  const quarter = toNumber(period.quarter)

  switch (period.kind) {
    case 'Month':
      return month !== null && year !== null
        ? formatPmCycle(String(year) + '-' + String(month).padStart(2, '0'))
        : 'the selected month'
    case 'Quarter':
      return quarter !== null && year !== null
        ? 'Q' + quarter + ' ' + year
        : 'the selected quarter'
    case 'Year':
      return year === null ? 'the selected year' : String(year)
    case 'Range':
      return (
        (period.startCycle ? formatPmCycle(period.startCycle) : 'Start cycle') +
        ' to ' +
        (period.endCycle ? formatPmCycle(period.endCycle) : 'end cycle')
      )
    default:
      return 'the selected PM cycles'
  }
}

function formatMeasureValue(
  measure: PmAnalyticsV2MeasureResponse,
  presentation: string,
) {
  if (!measure.isMeasurable || measure.value === null) {
    const denominator = toNumber(measure.denominator)
    if (denominator === 0) return 'No eligible schedules'
    if (measure.unmeasurableReason) {
      const reason = measure.unmeasurableReason.toLowerCase()
      if (reason.includes('open') || reason.includes('close')) {
        return 'Not measurable until the included cycles close'
      }
      if (reason.includes('population') || reason.includes('eligible')) {
        return 'No eligible schedules'
      }
    }
    return 'Not measurable for this scope'
  }

  const value = formatNumber(measure.value)
  const unit = measure.unit.toLowerCase()
  if (unit === 'percent' || unit === 'percentage') return value + '%'
  if (unit === 'count' || presentation === 'Count') return value
  return value + ' ' + formatLabel(measure.unit).toLowerCase()
}

function MeasureCard({
  label,
  measure,
  presentation,
}: {
  label: string
  measure: PmAnalyticsV2MeasureResponse
  presentation: string
}) {
  return (
    <div className="rounded-lg border border-[var(--border-soft)] p-4">
      <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </dt>
      <dd className="mt-2 text-xl font-bold text-[var(--text-primary)]">
        {formatMeasureValue(measure, presentation)}
      </dd>
      <p className="mt-1 text-xs text-[var(--text-secondary)]">
        {formatNumber(measure.numerator)} of {formatNumber(measure.denominator)}
      </p>
    </div>
  )
}

function groupLabel(value: string | null, groupBy: string) {
  if (value === null) return formatLabel(groupBy) + ' not recorded'
  return groupBy === 'PmCycle' ? formatPmCycle(value) : value
}

function SourceRecord({
  source,
  plan,
}: {
  source: PmAnalyticsV2SourceResponse
  plan: PmAnalyticsV2Plan
}) {
  const cycleYear = Number(source.pmCycle.slice(0, 4))
  const search = {
    assetCategory: plan.assetCategory,
    year: Number.isFinite(cycleYear) ? cycleYear : undefined,
    pmCycle: source.pmCycle,
    department: plan.department ?? undefined,
  }
  const details = [
    ['PM cycle', formatPmCycle(source.pmCycle)],
    ['Scheduled deadline', formatDate(source.deadline)],
    ['Inspection completed', formatDate(source.completedAt)],
    ['Schedule status', formatLabel(source.scheduleStatus)],
    ['Timeliness', formatLabel(source.timeliness)],
    ['Condition', formatLabel(source.condition)],
    ...(source.formStatus
      ? [['Form status', formatLabel(source.formStatus)]]
      : []),
    ...(source.usedLegacyDateFallback
      ? [['Date basis', 'Legacy date fallback']]
      : []),
  ]

  return (
    <li className="rounded-lg border border-[var(--border-soft)] p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <Link
          to="/app/assets/$assetId"
          params={{ assetId: source.assetId }}
          search={{
            returnContext: { kind: 'dashboard', search },
          }}
          className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
        >
          {source.assetCode}
        </Link>
        <span className="text-sm text-[var(--text-secondary)]">
          Current department: {source.currentDepartment ?? 'Not recorded'}
          {' · '}
          Current building: {source.currentBuilding ?? 'Not recorded'}
        </span>
      </div>
      <dl className="mt-2 grid gap-x-4 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
        {details.map(([label, value]) => (
          <div key={label}>
            <dt className="text-xs text-[var(--text-neutral)]">{label}</dt>
            <dd className="text-[var(--text-primary)]">{value}</dd>
          </div>
        ))}
      </dl>
    </li>
  )
}

function RankingList({ response }: { response: PmAnalyticsV2QueryResponse }) {
  const rankingLabel =
    response.plan.ranking?.direction === 'Lowest' ? 'Lowest' : 'Highest'
  return (
    <section aria-labelledby="pm-analytics-ranking-title" className="space-y-2">
      <h4
        id="pm-analytics-ranking-title"
        className="font-semibold text-[var(--text-primary)]"
      >
        {rankingLabel} ranked departments
      </h4>
      <p className="text-xs text-[var(--text-neutral)]">
        Ranking uses department results. The source list remains scoped to the
        full report and can include departments outside this top list.
      </p>
      <ol className="list-decimal space-y-2 pl-5">
        {response.groups.map((group, index) => (
          <li key={String(group.value) + '-' + index} className="pl-1">
            <span className="font-medium">
              {groupLabel(group.value, 'Department')}
            </span>
            {' — '}
            {formatMeasureValue(group.result, response.plan.presentation)}
          </li>
        ))}
      </ol>
      {response.rankingExclusions.length > 0 && (
        <div>
          <h5 className="text-sm font-medium">Not ranked</h5>
          <ul className="list-disc pl-5 text-sm text-[var(--text-secondary)]">
            {response.rankingExclusions.map((item, index) => (
              <li key={String(item.department) + '-' + index}>
                {item.department ?? 'Department not recorded'}: {item.reason}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

function CycleTable({ response }: { response: PmAnalyticsV2QueryResponse }) {
  if (response.cycles.length === 0) return null
  return (
    <section aria-labelledby="pm-analytics-cycles-title">
      <h4
        id="pm-analytics-cycles-title"
        className="mb-2 font-semibold text-[var(--text-primary)]"
      >
        PM cycle detail
      </h4>
      <div className="overflow-x-auto">
        <table className="min-w-full border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-[var(--border-soft)] text-xs text-[var(--text-neutral)]">
              <th className="px-2 py-2">PM cycle</th>
              <th className="px-2 py-2">State</th>
              <th className="px-2 py-2">Eligible asset cycles</th>
              <th className="px-2 py-2">Distinct assets</th>
              <th className="px-2 py-2">Inspected</th>
              <th className="px-2 py-2">On time</th>
              <th className="px-2 py-2">Late</th>
              <th className="px-2 py-2">Non-operational</th>
              <th className="px-2 py-2">Remaining</th>
              <th className="px-2 py-2">Missed</th>
            </tr>
          </thead>
          <tbody>
            {response.cycles.map((cycle) => (
              <tr
                key={cycle.pmCycle}
                className="border-b border-[var(--border-soft)]"
              >
                <td className="px-2 py-2">{formatPmCycle(cycle.pmCycle)}</td>
                <td className="px-2 py-2">{formatLabel(cycle.state)}</td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.eligibleAssetCycleCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.eligibleDistinctAssetCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.inspectedCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.completedOnTimeCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.completedLateCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.nonOperationalCount)}
                </td>
                <td className="px-2 py-2">
                  {formatNumber(cycle.remainingCount)}
                </td>
                <td className="px-2 py-2">{formatNumber(cycle.missedCount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-1 text-xs text-[var(--text-neutral)]">
        Distinct asset counts are calculated within each cycle; they are not
        additive across cycles.
      </p>
    </section>
  )
}

export function PmAnalyticsPanel() {
  const currentUser = useCurrentUser()
  const currentUserId = currentUser.data?.id ?? null
  const roles = currentUser.data?.roles ?? []

  if (!roles.includes('GSD') || !currentUserId) return null

  return (
    <PmAnalyticsQuestionPanel key={currentUserId + ':' + roles.join(',')} />
  )
}

function PmAnalyticsQuestionPanel() {
  const [question, setQuestion] = useState('')
  const [interpretation, setInterpretation] =
    useState<PmAnalyticsV2InterpretationResponse | null>(null)
  const [response, setResponse] = useState<PmAnalyticsV2QueryResponse | null>(
    null,
  )
  const [isPending, setIsPending] = useState(false)
  const [isInterpreting, setIsInterpreting] = useState(false)
  const [hasError, setHasError] = useState(false)
  const requestId = useRef(0)
  const controller = useRef<AbortController | null>(null)

  useEffect(() => {
    return () => {
      requestId.current += 1
      controller.current?.abort()
      controller.current = null
    }
  }, [])

  const beginRequest = () => {
    controller.current?.abort()
    const nextController = new AbortController()
    controller.current = nextController
    requestId.current += 1
    return { nextController, nextRequestId: requestId.current }
  }

  const isCurrentRequest = (
    activeController: AbortController,
    activeRequestId: number,
  ) => requestId.current === activeRequestId && !activeController.signal.aborted

  const executePlan = async (
    plan: PmAnalyticsV2Plan,
    scope: PmAnalyticsV2SourceScope,
    page: number,
    activeController: AbortController,
    activeRequestId: number,
  ) => {
    try {
      const result = await queryPmAnalyticsV2(
        { plan, sourceScope: scope, page, pageSize },
        activeController.signal,
      )
      if (!isCurrentRequest(activeController, activeRequestId)) return
      setResponse(result)
    } catch {
      if (isCurrentRequest(activeController, activeRequestId)) {
        setHasError(true)
      }
    } finally {
      if (isCurrentRequest(activeController, activeRequestId)) {
        controller.current = null
        setIsPending(false)
        setIsInterpreting(false)
      }
    }
  }

  const handleQuestionChange = (value: string) => {
    requestId.current += 1
    controller.current?.abort()
    controller.current = null
    setQuestion(value)
    setResponse(null)
    setInterpretation(null)
    setHasError(false)
    setIsPending(false)
    setIsInterpreting(false)
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const submittedQuestion = question.trim()
    if (!submittedQuestion) return

    const { nextController, nextRequestId } = beginRequest()
    setResponse(null)
    setInterpretation(null)
    setHasError(false)
    setIsPending(true)
    setIsInterpreting(true)

    try {
      const interpreted = await interpretPmAnalyticsV2Question(
        { question: submittedQuestion },
        nextController.signal,
      )
      if (!isCurrentRequest(nextController, nextRequestId)) return
      setInterpretation(interpreted)
      setIsInterpreting(false)

      if (interpreted.status !== 'Valid') return
      if (!interpreted.plan) {
        setHasError(true)
        return
      }

      await executePlan(
        interpreted.plan,
        'Matching',
        1,
        nextController,
        nextRequestId,
      )
    } catch {
      if (isCurrentRequest(nextController, nextRequestId)) {
        setHasError(true)
      }
    } finally {
      if (isCurrentRequest(nextController, nextRequestId)) {
        controller.current = null
        setIsPending(false)
        setIsInterpreting(false)
      }
    }
  }

  const reloadSources = async (
    plan: PmAnalyticsV2Plan,
    sourceScope: PmAnalyticsV2SourceScope,
    page: number,
  ) => {
    const { nextController, nextRequestId } = beginRequest()
    setHasError(false)
    setIsPending(true)
    setIsInterpreting(false)
    await executePlan(plan, sourceScope, page, nextController, nextRequestId)
  }

  const plan = response?.plan ?? interpretation?.plan ?? null
  const population = response?.population
  const currentPage = toNumber(response?.page) ?? 1
  const totalPages = toNumber(population?.totalPages) ?? 0

  return (
    <Card className="p-4 shadow-none sm:p-5">
      <div className="max-w-3xl print:hidden">
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          Ask about PM results
        </h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Ask bounded questions about progress, on-time compliance, late,
          non-operational, remaining, or missed schedules. Use a supported asset
          category with an explicit month, quarter, year, range, or two-cycle
          comparison.
        </p>
        <form
          className="mt-4 space-y-3"
          onSubmit={(event) => void handleSubmit(event)}
        >
          <div>
            <Label htmlFor="pm-analytics-question">Question</Label>
            <Input
              id="pm-analytics-question"
              className="mt-2"
              value={question}
              maxLength={512}
              onChange={(event) => handleQuestionChange(event.target.value)}
              placeholder="Show on-time compliance for fire extinguishers during 2025 by department"
              aria-describedby="pm-analytics-help"
            />
            <p
              id="pm-analytics-help"
              className="mt-1 text-xs text-[var(--text-neutral)]"
            >
              Remaining includes unfinished schedules in active cycles only;
              missed includes unfinished schedules in closed cycles only. Future
              cycles and unscheduled assets are not counted as missed. Current
              department and building labels reflect current asset metadata, not
              a historical snapshot.
            </p>
          </div>
          <Button type="submit" disabled={isPending || !question.trim()}>
            {isPending
              ? isInterpreting
                ? 'Checking question'
                : 'Reading PM results'
              : 'Show result'}
          </Button>
        </form>
      </div>

      {isPending && (
        <p
          role="status"
          className="mt-4 text-sm text-[var(--text-secondary)] print:hidden"
        >
          {isInterpreting
            ? 'Checking the question scope.'
            : 'Reading the selected PM records.'}
        </p>
      )}
      {hasError && (
        <p role="alert" className="mt-4 text-sm text-[var(--error)]">
          PM analytics could not be loaded. Please try again.
        </p>
      )}
      {interpretation?.status === 'NeedsClarification' && (
        <section
          aria-labelledby="pm-analytics-clarification-title"
          className="mt-4"
        >
          <h3
            id="pm-analytics-clarification-title"
            className="font-semibold text-[var(--text-primary)]"
          >
            Please clarify the question
          </h3>
          <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-[var(--text-secondary)]">
            {interpretation.clarificationFields
              .map((field) => clarificationPrompts[field])
              .filter((prompt): prompt is string => Boolean(prompt))
              .map((prompt) => (
                <li key={prompt}>{prompt}</li>
              ))}
          </ul>
        </section>
      )}
      {interpretation?.status === 'Unsupported' && (
        <p role="status" className="mt-4 text-sm text-[var(--text-secondary)]">
          This question is outside the supported PM measures or time scopes. Try
          one asset category and a specific supported period.
        </p>
      )}
      {interpretation &&
        interpretation.status !== 'Valid' &&
        interpretation.status !== 'NeedsClarification' &&
        interpretation.status !== 'Unsupported' && (
          <p
            role="status"
            className="mt-4 text-sm text-[var(--text-secondary)]"
          >
            The question could not be converted into a supported PM plan.
          </p>
        )}

      {response && plan && (
        <section
          aria-labelledby="pm-analytics-result-title"
          className="mt-5 space-y-5"
          aria-live="polite"
        >
          <div>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h3
                id="pm-analytics-result-title"
                className="text-base font-semibold text-[var(--text-primary)]"
              >
                PM analytics result
              </h3>
              <Button
                type="button"
                variant="secondary"
                className="print:hidden"
                onClick={() => window.print()}
              >
                Print result
              </Button>
            </div>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {formatMetric(plan.metric, plan.countBasis)} for{' '}
              {formatAssetCategory(plan.assetCategory)} during{' '}
              {plan.comparison
                ? formatPmCycle(plan.comparison.earlierCycle) +
                  ' compared with ' +
                  formatPmCycle(plan.comparison.laterCycle)
                : formatPeriod(plan.period)}
              {plan.department ? ', department ' + plan.department : ''}
              {plan.building ? ', building ' + plan.building : ''}
              {plan.assetCode ? ', asset ' + plan.assetCode : ''}.
              {plan.condition
                ? ' Condition: ' + formatLabel(plan.condition) + '.'
                : ''}
              {plan.timeliness
                ? ' Timeliness: ' + formatLabel(plan.timeliness) + '.'
                : ''}
              {plan.condition && plan.timeliness
                ? ' Both record filters are combined with AND.'
                : ''}
              {plan.ranking
                ? ' Ranking: top ' +
                  formatNumber(plan.ranking.limit) +
                  ' by ' +
                  formatLabel(plan.ranking.direction).toLowerCase() +
                  '.'
                : ''}
            </p>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {describeMetric(plan)}
            </p>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              As of {formatDate(response.asOfUtc)}. Count basis:{' '}
              {formatLabel(plan.countBasis)}. Grouping:{' '}
              {formatLabel(plan.groupBy)}.
            </p>
            <p className="mt-2 text-xs text-[var(--text-neutral)]">
              {response.scopeNote}
            </p>
            {plan.presentation === 'Percent' && (
              <p className="mt-2 text-xs text-[var(--text-neutral)]">
                Percentages use the eligible scheduled population. Matching
                source filters do not change the measure denominator.
              </p>
            )}
            {plan.groupBy !== 'None' &&
              plan.countBasis === 'DistinctAssets' && (
                <p className="mt-2 text-xs text-[var(--text-neutral)]">
                  Distinct asset counts in separate groups or cycles can
                  overlap; do not add them to calculate an overall
                  distinct-asset total.
                </p>
              )}
          </div>

          <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-lg border border-[var(--border-soft)] p-4">
              <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
                Eligible asset cycles
              </dt>
              <dd className="mt-2 text-xl font-bold text-[var(--text-primary)]">
                {formatNumber(population?.eligibleAssetCycleCount)}
              </dd>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">
                Eligible distinct assets:{' '}
                {formatNumber(population?.eligibleDistinctAssetCount)}
              </p>
            </div>
            <div className="rounded-lg border border-[var(--border-soft)] p-4">
              <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
                Matching asset cycles
              </dt>
              <dd className="mt-2 text-xl font-bold text-[var(--text-primary)]">
                {formatNumber(population?.matchingAssetCycleCount)}
              </dd>
              <p className="mt-1 text-xs text-[var(--text-secondary)]">
                Matching distinct assets:{' '}
                {formatNumber(population?.matchingDistinctAssetCount)}
              </p>
            </div>
            {response.result && (
              <MeasureCard
                label={formatMetric(plan.metric, plan.countBasis)}
                measure={response.result}
                presentation={plan.presentation}
              />
            )}
            {response.comparison && (
              <div className="rounded-lg border border-[var(--border-soft)] p-4">
                <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
                  Comparison change
                </dt>
                <dd className="mt-2 text-xl font-bold text-[var(--text-primary)]">
                  {response.comparison.delta === null
                    ? 'Not measurable'
                    : formatNumber(response.comparison.delta) +
                      ' ' +
                      formatDeltaUnit(response.comparison.deltaUnit)}
                </dd>
                <p className="mt-1 text-xs text-[var(--text-secondary)]">
                  {formatPmCycle(response.comparison.earlierCycle)}:{' '}
                  {formatMeasureValue(
                    response.comparison.earlier,
                    plan.presentation,
                  )}
                  {' · '}
                  {formatPmCycle(response.comparison.laterCycle)}:{' '}
                  {formatMeasureValue(
                    response.comparison.later,
                    plan.presentation,
                  )}
                </p>
              </div>
            )}
          </dl>

          {response.rankingExclusions.length > 0 && (
            <p className="text-xs text-[var(--text-secondary)]">
              {response.rankingExclusions.length.toLocaleString()} departments
              were excluded because they did not meet this ranking plan's
              qualification rules.
            </p>
          )}
          {plan.ranking ? (
            <RankingList response={response} />
          ) : (
            response.groups.length > 0 && (
              <section aria-labelledby="pm-analytics-groups-title">
                <h4
                  id="pm-analytics-groups-title"
                  className="mb-2 font-semibold text-[var(--text-primary)]"
                >
                  Results by {formatLabel(plan.groupBy).toLowerCase()}
                </h4>
                <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                  {response.groups.map((group, index) => (
                    <MeasureCard
                      key={String(group.value) + '-' + index}
                      label={groupLabel(group.value, group.groupBy)}
                      measure={group.result}
                      presentation={plan.presentation}
                    />
                  ))}
                </dl>
              </section>
            )
          )}

          <CycleTable response={response} />

          <section aria-labelledby="pm-analytics-sources-title">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h4
                id="pm-analytics-sources-title"
                className="font-semibold text-[var(--text-primary)]"
              >
                {response.sourceScope === 'Matching'
                  ? 'Matching source records'
                  : 'Calculation source records'}
              </h4>
              <p className="text-xs text-[var(--text-secondary)]">
                {response.sources.length.toLocaleString()} shown on this page of{' '}
                {formatNumber(population?.sourceTotalCount)}
              </p>
            </div>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              {response.sourceScope === 'Matching'
                ? 'These source rows match the requested record filters.'
                : 'Calculation sources support the measure and may include records that do not match every requested record filter.'}{' '}
              Current department/building values come from current asset
              metadata. They are not historical snapshots.
            </p>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              Matching counts are independent of this page and source-list
              scope. Asset-cycle and distinct-asset totals are shown separately.
            </p>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              Printing includes the source rows on this page only; change pages
              to print another page.
            </p>
            <div className="mt-3 flex flex-wrap items-center gap-2 print:hidden">
              <Button
                type="button"
                variant={
                  response.sourceScope === 'Matching' ? 'primary' : 'secondary'
                }
                aria-pressed={response.sourceScope === 'Matching'}
                disabled={isPending || response.sourceScope === 'Matching'}
                onClick={() => void reloadSources(response.plan, 'Matching', 1)}
              >
                Matching only
              </Button>
              <Button
                type="button"
                variant={
                  response.sourceScope === 'Calculation'
                    ? 'primary'
                    : 'secondary'
                }
                aria-pressed={response.sourceScope === 'Calculation'}
                disabled={isPending || response.sourceScope === 'Calculation'}
                onClick={() =>
                  void reloadSources(response.plan, 'Calculation', 1)
                }
              >
                Calculation sources
              </Button>
            </div>

            {response.plan.ranking && (
              <p className="mt-2 text-xs text-[var(--text-neutral)]">
                The report sources cover the full requested scope; they can
                include departments outside the ranked top list.
              </p>
            )}

            {response.sources.length === 0 ? (
              <p className="mt-2 text-sm text-[var(--text-secondary)]">
                No source records are available on this page.
              </p>
            ) : (
              <ul className="mt-3 space-y-2">
                {response.sources.map((source) => (
                  <SourceRecord
                    key={
                      source.scheduleId + '-' + (source.inspectionId ?? 'none')
                    }
                    source={source}
                    plan={response.plan}
                  />
                ))}
              </ul>
            )}

            <div className="mt-3 flex flex-wrap items-center justify-between gap-2 print:hidden">
              <p className="text-sm text-[var(--text-secondary)]">
                Page {formatNumber(response.page)} of{' '}
                {formatNumber(population?.totalPages)}
              </p>
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="secondary"
                  disabled={isPending || currentPage <= 1}
                  onClick={() =>
                    void reloadSources(
                      response.plan,
                      response.sourceScope,
                      Math.max(1, currentPage - 1),
                    )
                  }
                >
                  Previous page
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={
                    isPending || totalPages === 0 || currentPage >= totalPages
                  }
                  onClick={() =>
                    void reloadSources(
                      response.plan,
                      response.sourceScope,
                      Math.min(totalPages, currentPage + 1),
                    )
                  }
                >
                  Next page
                </Button>
              </div>
            </div>
          </section>
        </section>
      )}
    </Card>
  )
}
