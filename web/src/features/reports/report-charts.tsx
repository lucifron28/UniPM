import { useId } from 'react'
import './report-charts.css'

const SERIES_COLORS = [
  'var(--primary)',
  'var(--information)',
  'var(--success)',
  'var(--warning)',
  'var(--error)',
  'var(--text-neutral)',
] as const

export type ReportChartValue = {
  seriesId: string
  seriesLabel: string
  value: number | null
  unit: string
  numerator?: number
  denominator?: number
  unavailableReason?: string
}

export type ReportChartRow = {
  id: string
  label: string
  values: ReportChartValue[]
}

export type ReportChartVariant = 'bar' | 'grouped' | 'stacked' | 'line'

export type ReportChartProps = {
  title: string
  description?: string
  variant: ReportChartVariant
  rows: ReportChartRow[]
  compact?: boolean
  onDrilldown?: (row: ReportChartRow, value: ReportChartValue) => void
}

function formatNumber(value: number) {
  return value.toLocaleString(undefined, { maximumFractionDigits: 20 })
}

function formatValue(value: ReportChartValue) {
  if (value.value === null || !Number.isFinite(value.value)) {
    return value.unavailableReason ?? 'Not available'
  }

  const unit = value.unit.trim()
  const unitLabel =
    unit === '%' || unit.toLowerCase() === 'percent'
      ? '%'
      : unit
        ? ` ${unit}`
        : ''
  const displayedValue = `${formatNumber(value.value)}${unitLabel}`

  if (
    value.numerator === undefined ||
    value.denominator === undefined ||
    !Number.isFinite(value.numerator) ||
    !Number.isFinite(value.denominator)
  ) {
    return displayedValue
  }

  return `${displayedValue} (${formatNumber(value.numerator)} / ${formatNumber(value.denominator)})`
}

function isPercentage(value: ReportChartValue) {
  return value.unit === '%' || value.unit.toLowerCase() === 'percent'
}

function compactValue(value: ReportChartValue) {
  if (value.value === null || !Number.isFinite(value.value))
    return 'Unavailable'
  const number = value.value.toLocaleString(undefined, {
    maximumFractionDigits: 2,
  })
  const ratio =
    value.numerator !== undefined && value.denominator !== undefined
      ? ` · ${value.numerator.toLocaleString()} of ${value.denominator.toLocaleString()}`
      : ''
  return isPercentage(value) ? `${number}%${ratio}` : number
}

function visualValue(value: ReportChartValue) {
  return value.value !== null && Number.isFinite(value.value)
    ? Math.max(0, value.value)
    : 0
}

function ReportLineChart({
  rows,
  series,
}: {
  rows: ReportChartRow[]
  series: Array<{ id: string; label: string }>
}) {
  const pointSpacing = 72
  const width = Math.max(360, rows.length * pointSpacing)
  const height = 240
  const left = 36
  const right = 36
  const top = 22
  const bottom = 62
  const plotWidth = width - left - right
  const plotHeight = height - top - bottom
  const lineValues = rows.flatMap((row) =>
    row.values
      .map((value) => value.value)
      .filter(
        (value): value is number => value !== null && Number.isFinite(value),
      ),
  )
  const minValue = lineValues.length === 0 ? 0 : Math.min(...lineValues)
  const maxValue = lineValues.length === 0 ? 1 : Math.max(...lineValues)
  const range =
    maxValue === minValue
      ? Math.max(1, Math.abs(maxValue))
      : maxValue - minValue
  const percentage = rows.flatMap((row) => row.values).every(isPercentage)
  const domainMin = percentage
    ? 0
    : maxValue === minValue
      ? minValue - range / 2
      : minValue
  const domainMax = percentage
    ? 100
    : maxValue === minValue
      ? maxValue + range / 2
      : maxValue
  const pointsFor = (seriesId: string) =>
    rows.map((row, index) => {
      const value = row.values.find(
        (candidate) => candidate.seriesId === seriesId,
      )
      if (!value || value.value === null || !Number.isFinite(value.value)) {
        return null
      }

      const x =
        rows.length === 1
          ? left + plotWidth / 2
          : left + (index / (rows.length - 1)) * plotWidth
      const y =
        top + ((domainMax - value.value) / (domainMax - domainMin)) * plotHeight
      return { row, value: value.value, x, y }
    })
  const lines = series.map((item) => {
    const points = pointsFor(item.id)
    const paths: string[] = []
    let currentPath: string[] = []

    for (const point of points) {
      if (point === null) {
        if (currentPath.length > 0) paths.push(currentPath.join(' '))
        currentPath = []
        continue
      }

      currentPath.push(
        `${currentPath.length === 0 ? 'M' : 'L'} ${point.x} ${point.y}`,
      )
    }

    if (currentPath.length > 0) paths.push(currentPath.join(' '))
    return { ...item, points, paths }
  })

  return (
    <div className="mt-4 overflow-x-auto" aria-hidden="true">
      <svg
        className="report-chart__line-svg block h-60 w-full"
        style={{ minWidth: `${width}px` }}
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="xMinYMin meet"
      >
        {[0, 0.5, 1].map((fraction) => {
          const y = top + fraction * plotHeight
          return (
            <line
              key={fraction}
              x1={left}
              x2={width - right}
              y1={y}
              y2={y}
              stroke="var(--border-soft)"
              strokeWidth="1"
            />
          )
        })}
        {lines.map((line, seriesIndex) =>
          line.paths.map((path, pathIndex) => (
            <path
              key={`${line.id}:${pathIndex}`}
              d={path}
              fill="none"
              stroke={SERIES_COLORS[seriesIndex % SERIES_COLORS.length]}
              strokeWidth="2.5"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          )),
        )}
        {lines.flatMap((line, seriesIndex) =>
          line.points.map((point) =>
            point ? (
              <g key={`${line.id}:${point.row.id}`}>
                <circle
                  cx={point.x}
                  cy={point.y}
                  r="4"
                  fill="white"
                  stroke={SERIES_COLORS[seriesIndex % SERIES_COLORS.length]}
                  strokeWidth="2"
                />
                <text
                  x={point.x}
                  y={point.y - 8}
                  textAnchor="middle"
                  fontSize="10"
                  fill="var(--text-primary)"
                >
                  {formatNumber(point.value)}
                </text>
              </g>
            ) : null,
          ),
        )}
        {rows.map((row, index) => {
          const x =
            rows.length === 1
              ? left + plotWidth / 2
              : left + (index / (rows.length - 1)) * plotWidth
          const separator = row.label.indexOf(':')
          const firstLine =
            separator >= 0 ? row.label.slice(0, separator + 1) : row.label
          const secondLine =
            separator >= 0 ? row.label.slice(separator + 1).trim() : ''
          return (
            <text
              key={row.id}
              x={x}
              y={height - 39}
              textAnchor="middle"
              fontSize="10"
              fill="var(--text-secondary)"
            >
              <title>{row.label}</title>
              <tspan x={x}>{firstLine}</tspan>
              {secondLine && (
                <tspan x={x} dy="12">
                  {secondLine}
                </tspan>
              )}
            </text>
          )
        })}
      </svg>
    </div>
  )
}

export function ReportChart({
  title,
  description,
  variant,
  rows,
  onDrilldown,
  compact = false,
}: ReportChartProps) {
  const titleId = useId()
  const series = Array.from(
    new Map(
      rows.flatMap((row) =>
        row.values.map(
          (value) =>
            [
              value.seriesId,
              { id: value.seriesId, label: value.seriesLabel },
            ] as const,
        ),
      ),
    ).values(),
  )
  const values = rows.flatMap((row) => row.values)
  const percentage = values.length > 0 && values.every(isPercentage)
  const maxBarValue = percentage ? 100 : Math.max(0, ...values.map(visualValue))
  const maxStackedValue = percentage
    ? 100
    : Math.max(
        0,
        ...rows.map((row) =>
          row.values.reduce((total, value) => total + visualValue(value), 0),
        ),
      )
  const hasNegativeValues = values.some(
    (value) => variant !== 'line' && value.value !== null && value.value < 0,
  )

  const DataContainer = compact ? 'details' : 'div'

  return (
    <section
      aria-labelledby={titleId}
      className="report-chart content-card min-w-0 rounded-lg border border-[var(--border-soft)] bg-white p-4 shadow-none sm:p-5"
    >
      <h3
        id={titleId}
        className="text-base font-semibold text-[var(--text-primary)]"
      >
        {title}
      </h3>
      {description &&
        (compact ? (
          <details className="mt-1 text-sm text-[var(--text-secondary)] print:hidden">
            <summary className="min-h-9 cursor-pointer py-1.5 text-xs font-medium focus-visible:outline-2">
              How this is calculated
            </summary>
            <p className="pb-2">{description}</p>
          </details>
        ) : (
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            {description}
          </p>
        ))}

      {rows.length === 0 ? (
        <p
          role="status"
          className="mt-4 rounded-md border border-[var(--border-soft)] px-3 py-3 text-sm text-[var(--text-secondary)]"
        >
          No chart data is available for this scope.
        </p>
      ) : (
        <>
          {series.length > 0 && (!compact || series.length > 1) && (
            <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-2 text-xs text-[var(--text-secondary)]">
              {series.map((item, index) => (
                <li key={item.id} className="inline-flex items-center gap-2">
                  <span
                    aria-hidden="true"
                    className="h-3 w-3 shrink-0 rounded-sm"
                    style={{
                      backgroundColor:
                        SERIES_COLORS[index % SERIES_COLORS.length],
                    }}
                  />
                  {item.label}
                </li>
              ))}
            </ul>
          )}

          {variant === 'line' ? (
            <ReportLineChart rows={rows} series={series} />
          ) : (
            <div
              aria-hidden="true"
              className="mt-4 space-y-3"
              data-chart-variant={variant}
            >
              {rows.map((row) => (
                <div
                  key={row.id}
                  className="report-chart__visual-row grid gap-2 sm:grid-cols-[minmax(8rem,12rem)_minmax(0,1fr)] sm:items-center"
                >
                  <p className="text-sm font-medium break-words text-[var(--text-primary)]">
                    {row.label}
                  </p>
                  {variant === 'stacked' ? (
                    <div>
                      <div className="h-4 overflow-hidden rounded-full bg-[var(--surface-muted)]">
                        <div className="flex h-full">
                          {row.values.map((value) => {
                            const width =
                              maxStackedValue === 0
                                ? 0
                                : (visualValue(value) / maxStackedValue) * 100
                            const colorIndex = series.findIndex(
                              (item) => item.id === value.seriesId,
                            )

                            return (
                              <span
                                key={value.seriesId}
                                className="h-full"
                                style={{
                                  width: `${Math.min(100, width)}%`,
                                  backgroundColor:
                                    SERIES_COLORS[
                                      Math.max(colorIndex, 0) %
                                        SERIES_COLORS.length
                                    ],
                                }}
                              />
                            )
                          })}
                        </div>
                      </div>
                      {compact && (
                        <p className="mt-2 text-xs text-[var(--text-secondary)] tabular-nums">
                          {row.values
                            .map(
                              (value) =>
                                `${value.seriesLabel}: ${compactValue(value)}`,
                            )
                            .join(' · ')}
                        </p>
                      )}
                    </div>
                  ) : (
                    <div className="space-y-1.5">
                      {row.values.map((value) => {
                        const width =
                          maxBarValue === 0
                            ? 0
                            : (visualValue(value) / maxBarValue) * 100
                        const colorIndex = series.findIndex(
                          (item) => item.id === value.seriesId,
                        )

                        return (
                          <div
                            key={value.seriesId}
                            className={
                              compact && series.length === 1
                                ? 'grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3'
                                : 'grid grid-cols-[minmax(6rem,8rem)_minmax(0,1fr)_auto] items-center gap-2'
                            }
                          >
                            {(!compact || series.length > 1) && (
                              <span className="text-xs text-[var(--text-secondary)]">
                                {value.seriesLabel}
                              </span>
                            )}
                            <span className="block h-3 overflow-hidden rounded-full bg-[var(--surface-muted)]">
                              <span
                                className="block h-full rounded-full"
                                style={{
                                  width: `${Math.min(100, width)}%`,
                                  backgroundColor:
                                    SERIES_COLORS[
                                      Math.max(colorIndex, 0) %
                                        SERIES_COLORS.length
                                    ],
                                }}
                              />
                            </span>
                            <span className="text-xs font-semibold whitespace-nowrap text-[var(--text-primary)] tabular-nums">
                              {compactValue(value)}
                            </span>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {hasNegativeValues && (
            <p className="mt-3 text-xs text-[var(--text-secondary)]">
              Negative values are shown in the exact-value table but are not
              drawn as bars.
            </p>
          )}

          <DataContainer className="report-chart__data mt-4 border-t border-[var(--border-soft)] pt-2">
            {compact && (
              <summary className="min-h-10 cursor-pointer py-2 text-sm font-medium text-[var(--primary)] focus-visible:outline-2">
                View data
              </summary>
            )}
            <div className="report-chart__table overflow-x-auto">
              <table className="w-full text-left text-sm">
                <caption className="sr-only">Exact values for {title}</caption>
                <thead className="border-b border-[var(--border-soft)]">
                  <tr>
                    <th
                      scope="col"
                      className="px-2 py-2 font-semibold text-[var(--text-primary)]"
                    >
                      Group
                    </th>
                    {series.map((item) => (
                      <th
                        key={item.id}
                        scope="col"
                        className="px-2 py-2 font-semibold text-[var(--text-primary)]"
                      >
                        {item.label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr
                      key={row.id}
                      className="report-chart__table-row border-b border-[var(--border-soft)] last:border-0"
                    >
                      <th
                        scope="row"
                        className="px-2 py-2 font-medium break-words text-[var(--text-primary)]"
                      >
                        {row.label}
                      </th>
                      {series.map((item) => {
                        const value = row.values.find(
                          (candidate) => candidate.seriesId === item.id,
                        )
                        const displayed = value
                          ? formatValue(value)
                          : 'Not returned for this group'

                        return (
                          <td
                            key={item.id}
                            className="px-2 py-2 break-words text-[var(--text-secondary)]"
                          >
                            <span>{displayed}</span>
                            {value?.value === null &&
                              value.unavailableReason && (
                                <span className="mt-1 block text-xs">
                                  {value.unavailableReason}
                                </span>
                              )}
                            {value && onDrilldown && value.value !== null && (
                              <button
                                type="button"
                                className="ml-2 min-h-10 rounded px-1 font-semibold text-[var(--primary)] underline underline-offset-2 focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none print:hidden"
                                aria-label={`Open ${item.label} source records for ${row.label}`}
                                onClick={() => onDrilldown(row, value)}
                              >
                                View sources
                              </button>
                            )}
                          </td>
                        )
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </DataContainer>
        </>
      )}
    </section>
  )
}
