import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'
import { useScheduleCoverageReview } from '@/features/schedules/schedule-queries'

const pageSize = 10

export function ScheduleCoverageReview() {
  const [page, setPage] = useState(1)
  const coverage = useScheduleCoverageReview(page)

  if (coverage.isPending) {
    return (
      <p className="text-sm text-[var(--text-neutral)]" role="status">
        Checking for past PM cycles without a schedule or deferral…
      </p>
    )
  }

  if (coverage.isError) {
    return (
      <Card role="alert" className="border-[var(--error)] p-5 shadow-none">
        <h2 className="font-bold text-[var(--error)]">
          PM coverage review is unavailable
        </h2>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Schedule planning remains available, but GSD could not load the
          unconfirmed past cycles.
        </p>
        <Button
          type="button"
          className="mt-4"
          onClick={() => void coverage.refetch()}
        >
          Retry coverage review
        </Button>
      </Card>
    )
  }

  const records = coverage.data.items
  const total = Number(coverage.data.total)
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  if (total === 0) return null

  return (
    <Card
      aria-labelledby="schedule-coverage-review-title"
      className="space-y-4 border-[color-mix(in_srgb,var(--warning)_35%,white)] p-5 shadow-none"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2
            id="schedule-coverage-review-title"
            className="text-lg font-bold text-[var(--text-primary)]"
          >
            Unconfirmed past-cycle coverage
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
            These active assets have past CPMP cycles with no schedule or
            enrollment deferral. The approved coverage start date is not
            configured, so GSD must confirm whether these cycles are in scope.
            This read-only list does not create, waive, or complete PM work.
          </p>
        </div>
        <Badge variant="warning">
          {total} {total === 1 ? 'cycle' : 'cycles'} to review
        </Badge>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[48rem] text-left text-sm">
          <thead>
            <tr className="border-b border-[var(--border-soft)] text-[var(--text-neutral)]">
              <th className="px-3 py-2 font-semibold" scope="col">
                Asset
              </th>
              <th className="px-3 py-2 font-semibold" scope="col">
                Department
              </th>
              <th className="px-3 py-2 font-semibold" scope="col">
                Category
              </th>
              <th className="px-3 py-2 font-semibold" scope="col">
                Missing PM cycle
              </th>
              <th className="px-3 py-2 font-semibold" scope="col">
                Reason
              </th>
            </tr>
          </thead>
          <tbody>
            {records.map((record) => (
              <tr
                key={`${record.assetId}-${record.pmCycle}`}
                className="border-b border-[var(--border-soft)] last:border-0"
              >
                <th
                  className="px-3 py-3 font-semibold text-[var(--text-primary)]"
                  scope="row"
                >
                  {record.assetCode}
                </th>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {record.department}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {record.assetCategory}
                </td>
                <td className="px-3 py-3 text-[var(--text-secondary)]">
                  {formatPmCycle(record.pmCycle)}
                </td>
                <td className="max-w-md px-3 py-3 text-[var(--text-secondary)]">
                  {record.reason}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-[var(--text-secondary)]" aria-live="polite">
          Page {page} of {pageCount}
        </p>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={page <= 1 || coverage.isFetching}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
          >
            Previous
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={page >= pageCount || coverage.isFetching}
            onClick={() =>
              setPage((current) => Math.min(pageCount, current + 1))
            }
          >
            Next
          </Button>
        </div>
      </div>
    </Card>
  )
}
