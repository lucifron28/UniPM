import { useState, type ReactNode } from 'react'
import type { PmAnalyticsV3ExecutionResponse } from '@/api/generated/models'
import { Button } from '@/components/ui/button'
import { ReportChart, type ReportChartRow } from './report-charts'

function numeric(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function groupLabel(value: unknown) {
  return value === null || value === undefined || value === ''
    ? 'Not recorded'
    : String(value)
}

function temporalPeriodValues(dimension: string, cycles: string[]) {
  const indexes = cycles.flatMap((cycle) => {
    const match = /^(\d{4})-(0[1-9]|1[0-2])$/.exec(cycle)
    if (!match) return []

    const year = Number(match[1])
    const month = Number(match[2])
    if (dimension === 'pmCycle') return [year * 12 + month - 1]
    if (dimension === 'calendarQuarter')
      return [year * 4 + Math.floor((month - 1) / 3)]
    if (dimension === 'calendarYear') return [year]
    return []
  })
  if (indexes.length === 0) return []

  const first = Math.min(...indexes)
  const last = Math.max(...indexes)
  return Array.from({ length: last - first + 1 }, (_, offset) => {
    const index = first + offset
    if (dimension === 'pmCycle') {
      const year = Math.floor(index / 12)
      const month = (index % 12) + 1
      return `${year}-${String(month).padStart(2, '0')}`
    }
    if (dimension === 'calendarQuarter') {
      const year = Math.floor(index / 4)
      const quarter = (index % 4) + 1
      return `${year}-Q${quarter}`
    }
    return String(index)
  })
}

export function AnalyticsChartView({
  response,
  children,
  onMeasureSource,
}: {
  response: PmAnalyticsV3ExecutionResponse
  children: ReactNode
  onMeasureSource?: (values: Record<string, unknown>, measure: string) => void
}) {
  const [view, setView] = useState<'table' | 'chart'>('table')
  const groupBy = response.plan.groupBy ?? []
  const rows = response.chartRows ?? []
  const canChart = rows.length > 0
  const columns = new Map(response.columns.map((column) => [column.id, column]))
  const scope = response.plan.top
    ? `${response.plan.top.direction === 'Descending' ? 'Top' : 'Bottom'} ${response.plan.top.limit} groups after filters and ranking. This is a selected subset.`
    : `All ${rows.length} groups after the selected filters.`

  if (groupBy.length === 0) return children

  return (
    <div className="space-y-4">
      <div
        className="flex flex-wrap gap-2 print:hidden"
        role="group"
        aria-label="Report view"
      >
        <Button
          type="button"
          variant={view === 'table' ? 'primary' : 'secondary'}
          aria-pressed={view === 'table'}
          onClick={() => setView('table')}
        >
          Table
        </Button>
        <Button
          type="button"
          variant={view === 'chart' ? 'primary' : 'secondary'}
          aria-pressed={view === 'chart'}
          disabled={!canChart}
          onClick={() => setView('chart')}
        >
          Chart
        </Button>
      </div>
      {!canChart && (
        <p role="status" className="text-sm text-[var(--text-secondary)]">
          {response.chartUnavailableReason ??
            'No complete grouped chart data is available for this result. The table remains available.'}
        </p>
      )}
      {view === 'table' || !canChart ? (
        children
      ) : (
        <div className="space-y-6">
          <p className="text-sm text-[var(--text-secondary)]">
            {scope} Calculated{' '}
            {new Date(response.asOfUtc).toLocaleString(undefined, {
              timeZone: 'Asia/Manila',
            })}{' '}
            Asia/Manila. Department and building describe current asset
            metadata.
          </p>
          {response.plan.measures.map((measure) => {
            const chartRows: ReportChartRow[] = rows.map((row, index) => {
              const result = row.measures[measure]
              return {
                id: String(index),
                label: groupBy
                  .map(
                    (field) =>
                      `${columns.get(field)?.label ?? field}: ${groupLabel(row.values[field])}`,
                  )
                  .join(' / '),
                values: [
                  {
                    seriesId: measure,
                    seriesLabel: columns.get(measure)?.label ?? measure,
                    value: result?.isMeasurable ? numeric(result.value) : null,
                    unit: result?.unit ?? columns.get(measure)?.unit ?? '',
                    ...(result && result.unit !== 'assets'
                      ? {
                          numerator: Number(result.numerator),
                          denominator: Number(result.denominator),
                        }
                      : {}),
                    ...(result?.unmeasurableReason
                      ? { unavailableReason: result.unmeasurableReason }
                      : {}),
                  },
                ],
              }
            })
            const temporal =
              groupBy.length === 1 &&
              ['pmCycle', 'calendarQuarter', 'calendarYear'].includes(
                groupBy[0]!,
              )
            const completeTimeline =
              temporal &&
              !response.plan.top &&
              response.plan.aggregateFilters.length === 0
            if (completeTimeline) {
              const dimension = groupBy[0]!
              const periods = temporalPeriodValues(
                dimension,
                Object.values(response.cyclesByCategory).flat(),
              )
              const present = new Set(
                rows.map((row) => String(row.values[dimension])),
              )
              for (const period of periods) {
                if (present.has(period)) continue
                chartRows.push({
                  id: `missing-${dimension}-${period}`,
                  label: `${columns.get(dimension)?.label ?? dimension}: ${period}`,
                  values: [
                    {
                      seriesId: measure,
                      seriesLabel: columns.get(measure)?.label ?? measure,
                      value: null,
                      unit: columns.get(measure)?.unit ?? '',
                      unavailableReason:
                        'No group was recorded for this period in the selected scope. This is unavailable, not zero.',
                    },
                  ],
                })
              }
            }
            if (temporal && !response.plan.top)
              chartRows.sort((left, right) =>
                left.label.localeCompare(right.label, undefined, {
                  numeric: true,
                }),
              )
            const canDrilldown =
              onMeasureSource && measure !== 'distinctAssetCount'
            return (
              <ReportChart
                key={measure}
                title={columns.get(measure)?.label ?? measure}
                description={
                  temporal
                    ? `${scope} ${completeTimeline ? 'Points represent scheduled groups; missing or unavailable values do not mean zero.' : 'Temporal groups are shown as bars so this selected subset does not imply continuity across omitted periods.'}`
                    : scope
                }
                variant={completeTimeline ? 'line' : 'bar'}
                rows={chartRows}
                {...(canDrilldown
                  ? {
                      onDrilldown: (chartRow: ReportChartRow) => {
                        const row = rows[Number(chartRow.id)]
                        if (row) onMeasureSource(row.values, measure)
                      },
                    }
                  : {})}
              />
            )
          })}
          {groupBy.length === 2 &&
            response.plan.measures.map((measure) => {
              const first = groupBy[0]!
              const second = groupBy[1]!
              const labels = Array.from(
                new Map(
                  rows.map((row) => [
                    JSON.stringify(row.values[second] ?? null),
                    groupLabel(row.values[second]),
                  ]),
                ),
              )
              const rowLabels = Array.from(
                new Map(
                  rows.map((row) => [
                    JSON.stringify(row.values[first] ?? null),
                    groupLabel(row.values[first]),
                  ]),
                ),
              )
              const cells = new Map(
                rows.map((row) => [
                  JSON.stringify([
                    JSON.stringify(row.values[first] ?? null),
                    JSON.stringify(row.values[second] ?? null),
                  ]),
                  row,
                ]),
              )
              const max = Math.max(
                1,
                ...rows.map((row) =>
                  row.measures[measure]?.isMeasurable
                    ? (numeric(row.measures[measure]?.value) ?? 0)
                    : 0,
                ),
              )
              return (
                <div
                  key={`matrix-${measure}`}
                  className="overflow-x-auto rounded-lg border border-[var(--border-soft)] p-4"
                >
                  <h3 className="font-semibold">
                    {columns.get(measure)?.label ?? measure} by{' '}
                    {columns.get(first)?.label ?? first} and{' '}
                    {columns.get(second)?.label ?? second}
                  </h3>
                  <p className="my-2 text-sm">
                    Darker cells indicate larger values. Missing combinations
                    are unavailable, not zero. Units:{' '}
                    {columns.get(measure)?.unit}.
                  </p>
                  <table className="w-full border-collapse text-sm">
                    <caption className="sr-only">
                      Exact values for the two-dimensional heatmap
                    </caption>
                    <thead>
                      <tr>
                        <th scope="col" className="p-2 text-left">
                          {columns.get(first)?.label ?? first}
                        </th>
                        {labels.map(([key, label]) => (
                          <th scope="col" className="p-2" key={key}>
                            {label}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {rowLabels.map(([rowKey, label]) => (
                        <tr key={rowKey}>
                          <th scope="row" className="p-2 text-left">
                            {label}
                          </th>
                          {labels.map(([columnKey]) => {
                            const row = cells.get(
                              JSON.stringify([rowKey, columnKey]),
                            )
                            const result = row?.measures[measure]
                            const value = result?.isMeasurable
                              ? numeric(result.value)
                              : null
                            return (
                              <td
                                key={columnKey}
                                className="border border-[var(--border-soft)] p-2 text-center"
                                style={{
                                  background:
                                    value === null
                                      ? undefined
                                      : `color-mix(in srgb, var(--primary) ${Math.round((value / max) * 25)}%, white)`,
                                }}
                              >
                                {value === null ? (
                                  <span
                                    title={
                                      result?.unmeasurableReason ??
                                      'No group recorded'
                                    }
                                  >
                                    Unavailable
                                  </span>
                                ) : (
                                  <>
                                    <span>
                                      {value.toLocaleString()}
                                      {result?.unit === 'percent' ? '%' : ''}
                                    </span>
                                    {result?.unit === 'percent' && (
                                      <span className="block text-xs">
                                        {result.numerator} /{' '}
                                        {result.denominator}
                                      </span>
                                    )}
                                    {onMeasureSource &&
                                      row &&
                                      measure !== 'distinctAssetCount' && (
                                        <button
                                          type="button"
                                          className="block min-h-11 w-full text-xs font-semibold underline print:hidden"
                                          onClick={() =>
                                            onMeasureSource(row.values, measure)
                                          }
                                        >
                                          View source records
                                        </button>
                                      )}
                                  </>
                                )}
                              </td>
                            )
                          })}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )
            })}
        </div>
      )}
    </div>
  )
}
