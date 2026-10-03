import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from '@tanstack/react-router'
import { queryPmAnalytics } from '@/api/generated/endpoints'
import type {
  PmAnalyticsMeasureResponse,
  PmAnalyticsResponse,
  PmAnalyticsSourceResponse,
} from '@/api/generated/models'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useCurrentUser } from '@/features/auth/current-user'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'

const metricDescriptions: Record<string, string> = {
  Progress: 'Completed inspections divided by scheduled assets.',
  OnTimeCompliance:
    'Inspections completed by the deadline divided by scheduled assets. This measure is available after the cycle closes.',
  CompletedLate: 'Completed inspections recorded after the cycle deadline.',
  NonOperational:
    'Completed inspections recorded with a non-operational condition.',
}

function formatCategory(value: string) {
  return value
    .split('-')
    .map((part) => `${part.slice(0, 1).toUpperCase()}${part.slice(1)}`)
    .join(' ')
}

function formatMetric(value: string) {
  switch (value) {
    case 'Progress':
      return 'Progress'
    case 'OnTimeCompliance':
      return 'On-time compliance'
    case 'CompletedLate':
      return 'Late inspections'
    case 'NonOperational':
      return 'Non-operational assets'
    default:
      return value
  }
}

function formatLabel(value: string) {
  switch (value) {
    case 'OnTime':
      return 'Completed on time'
    case 'NonOperational':
      return 'Non-operational'
    case 'NotInspected':
      return 'Not inspected'
    default:
      return value.replace(/([a-z])([A-Z])/g, '$1 $2')
  }
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

function formatValue(measure: PmAnalyticsMeasureResponse, periodState: string) {
  if (!measure.isMeasurable || measure.value == null) {
    if (measure.denominator === 0) return 'No eligible schedules'
    if (measure.unit.toLowerCase() === 'percent' && periodState !== 'Closed') {
      return 'Not measurable until the cycle closes'
    }
    return 'Not measurable yet'
  }

  const value =
    typeof measure.value === 'number'
      ? measure.value.toLocaleString()
      : measure.value
  const unit = measure.unit.toLowerCase()
  if (unit === 'count') return value
  return unit === 'percent' || unit === 'percentage'
    ? `${value}%`
    : `${value} ${measure.unit}`
}

function MeasureResult({
  label,
  measure,
  periodState,
}: {
  label: string
  measure: PmAnalyticsMeasureResponse
  periodState: string
}) {
  return (
    <div className="rounded-lg border border-[var(--border-soft)] p-4">
      <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </dt>
      <dd className="mt-2 text-xl font-bold text-[var(--text-primary)]">
        {formatValue(measure, periodState)}
      </dd>
      <p className="mt-1 text-xs text-[var(--text-secondary)]">
        {measure.numerator.toLocaleString()} of{' '}
        {measure.denominator.toLocaleString()}
      </p>
    </div>
  )
}

function SourceRecord({
  source,
  plan,
}: {
  source: PmAnalyticsSourceResponse
  plan: PmAnalyticsResponse['plan']
}) {
  const returnSearch = {
    assetCategory: plan.assetCategory,
    year: Number(plan.pmCycle.slice(0, 4)),
    pmCycle: plan.pmCycle,
    department: plan.department ?? undefined,
  }
  const details = [
    ['PM cycle', formatPmCycle(source.pmCycle)],
    ['Deadline', formatDate(source.deadline)],
    ['Inspection completed', formatDate(source.inspectionCompletedAt)],
    ['Timeliness', formatLabel(source.timeliness)],
    ['Condition', formatLabel(source.condition)],
    ...(source.formStatus
      ? [['Form status', formatLabel(source.formStatus)]]
      : []),
  ]

  return (
    <li className="rounded-lg border border-[var(--border-soft)] p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <Link
          to="/app/assets/$assetId"
          params={{ assetId: source.assetId }}
          search={{
            returnContext: { kind: 'dashboard', search: returnSearch },
          }}
          className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
        >
          {source.assetCode}
        </Link>
        <span className="text-sm text-[var(--text-secondary)]">
          {source.department ?? 'Department not recorded'}
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

export function PmAnalyticsPanel() {
  const currentUser = useCurrentUser()
  const [question, setQuestion] = useState('')
  const [response, setResponse] = useState<PmAnalyticsResponse | null>(null)
  const [isPending, setIsPending] = useState(false)
  const [hasError, setHasError] = useState(false)
  const requestId = useRef(0)

  useEffect(
    () => () => {
      requestId.current += 1
    },
    [],
  )

  if (!currentUser.data?.roles.includes('GSD')) return null

  const handleQuestionChange = (value: string) => {
    requestId.current += 1
    setQuestion(value)
    setResponse(null)
    setHasError(false)
    setIsPending(false)
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const submittedQuestion = question.trim()
    if (!submittedQuestion) return

    requestId.current += 1
    const currentRequestId = requestId.current
    setResponse(null)
    setHasError(false)
    setIsPending(true)

    try {
      const result = await queryPmAnalytics({ question: submittedQuestion })
      if (requestId.current === currentRequestId) setResponse(result)
    } catch {
      if (requestId.current === currentRequestId) setHasError(true)
    } finally {
      if (requestId.current === currentRequestId) setIsPending(false)
    }
  }

  return (
    <Card className="p-4 shadow-none sm:p-5 print:hidden">
      <div className="max-w-3xl">
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          Ask about PM results
        </h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Ask about progress, on-time compliance, late inspections, or
          non-operational assets. Use one asset category and an explicit month.
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
              placeholder="Show progress for fire extinguishers in November 2026"
              aria-describedby="pm-analytics-help"
            />
            <p
              id="pm-analytics-help"
              className="mt-1 text-xs text-[var(--text-neutral)]"
            >
              You may add department "Name" or grouped by department. Progress
              is completed over scheduled. On-time compliance is completed by
              the deadline over scheduled and appears only after the cycle
              closes. Late inspections count completions after the deadline.
              Non-operational assets count inspections recorded as
              non-operational.
            </p>
          </div>
          <Button type="submit" disabled={isPending || !question.trim()}>
            {isPending ? 'Reading PM results' : 'Show result'}
          </Button>
        </form>
      </div>

      {isPending && (
        <p role="status" className="mt-4 text-sm text-[var(--text-secondary)]">
          Reading the selected PM records.
        </p>
      )}
      {hasError && (
        <p role="alert" className="mt-4 text-sm text-[var(--error)]">
          This question could not be answered. Check that it uses one supported
          measure, category, and month.
        </p>
      )}
      {response && (
        <section
          aria-labelledby="pm-analytics-result-title"
          className="mt-5 space-y-4"
          aria-live="polite"
        >
          <div>
            <h3
              id="pm-analytics-result-title"
              className="text-base font-semibold text-[var(--text-primary)]"
            >
              PM result
            </h3>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {formatMetric(response.plan.metric)} for{' '}
              {formatCategory(response.plan.assetCategory)} in{' '}
              {formatPmCycle(response.plan.pmCycle)}
              {response.plan.department ? `, ${response.plan.department}` : ''}
              {response.plan.groupBy === 'Department'
                ? ', grouped by department'
                : ''}
              .
            </p>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              {response.periodState} period. Deadline:{' '}
              {formatDate(response.deadline)}
            </p>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              {metricDescriptions[response.plan.metric]}
            </p>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              {response.scopeNote}
            </p>
          </div>

          <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <MeasureResult
              label={formatMetric(response.plan.metric)}
              measure={response.result}
              periodState={response.periodState}
            />
            {response.groups.map((group, index) => (
              <MeasureResult
                key={`${group.department ?? 'group'}-${index}`}
                label={group.department ?? 'Department not recorded'}
                measure={group}
                periodState={response.periodState}
              />
            ))}
          </dl>

          <div>
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h4 className="font-semibold text-[var(--text-primary)]">
                Source records
              </h4>
              <p className="text-xs text-[var(--text-secondary)]">
                {response.sources.length.toLocaleString()} shown of{' '}
                {response.totalSourceCount.toLocaleString()}
              </p>
            </div>
            <p className="mt-1 text-xs text-[var(--text-neutral)]">
              Sources include all eligible scheduled assets, even when an
              inspection has not been recorded. A measure count can therefore be
              lower than the source count.
            </p>
            {response.sources.length === 0 ? (
              <p className="mt-2 text-sm text-[var(--text-secondary)]">
                No source records match this scope.
              </p>
            ) : (
              <ul className="mt-3 space-y-2">
                {response.sources.map((source) => (
                  <SourceRecord
                    key={`${source.scheduleId}-${source.inspectionId ?? 'none'}`}
                    source={source}
                    plan={response.plan}
                  />
                ))}
              </ul>
            )}
            {response.sourcesTruncated && (
              <p className="mt-2 text-xs text-[var(--text-neutral)]">
                The list is limited to the first 100 source records.
              </p>
            )}
          </div>
        </section>
      )}
    </Card>
  )
}
