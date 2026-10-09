import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useScheduleEnrollmentDeferrals } from '@/features/schedules/schedule-queries'
import {
  formatPmCycle,
  formatScheduleDateTime,
} from '@/features/schedules/schedule-presentation'

export function ScheduleEnrollmentDeferralReview() {
  const [page, setPage] = useState(1)
  const deferrals = useScheduleEnrollmentDeferrals(page)

  if (deferrals.isPending) {
    return (
      <p className="text-sm text-[var(--text-neutral)]" role="status">
        Checking for asset cycles that need GSD review…
      </p>
    )
  }

  if (deferrals.isError) {
    return (
      <Card role="alert" className="border-[var(--error)] p-5 shadow-none">
        <h2 className="font-bold text-[var(--error)]">
          Deferred enrollment review is unavailable
        </h2>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The schedule registry is available, but GSD could not load cycles that
          need review.
        </p>
        <Button
          type="button"
          className="mt-4"
          onClick={() => void deferrals.refetch()}
        >
          Retry review list
        </Button>
      </Card>
    )
  }

  const records = deferrals.data.items
  const pageCount = Math.max(
    1,
    Math.ceil(deferrals.data.total / deferrals.data.pageSize),
  )
  if (deferrals.data.total === 0) return null

  return (
    <Card
      aria-labelledby="schedule-enrollment-review-title"
      className="space-y-4 border-[color-mix(in_srgb,var(--warning)_35%,white)] p-5 shadow-none"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2
            id="schedule-enrollment-review-title"
            className="text-lg font-bold text-[var(--text-primary)]"
          >
            Asset enrollment needs GSD review
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
            These assets could not join a PM batch after work began. Review the
            deferred cycle and next eligible CPMP cycle below.
          </p>
        </div>
        <Badge variant="warning">
          {deferrals.data.total} cycle
          {deferrals.data.total === 1 ? '' : 's'}
        </Badge>
      </div>

      <ol className="grid gap-3">
        {records.map((record) => (
          <li
            key={`${record.assetId}-${record.deferredPmCycle}`}
            className="rounded-lg border border-[var(--border-soft)] bg-white p-4 transition-colors duration-150 hover:bg-[var(--page-background)]"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="font-semibold text-[var(--text-primary)]">
                  {record.assetCode}
                </p>
                <p className="mt-1 text-xs text-[var(--text-neutral)]">
                  {record.department} · {record.assetCategory}
                </p>
              </div>
              <Badge variant="warning">{record.status}</Badge>
            </div>

            <dl className="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2 xl:grid-cols-4">
              <div>
                <dt className="text-xs text-[var(--text-neutral)]">
                  Deferred PM cycle
                </dt>
                <dd className="font-medium text-[var(--text-primary)]">
                  {formatPmCycle(record.deferredPmCycle)}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-[var(--text-neutral)]">
                  Next eligible PM cycle
                </dt>
                <dd className="font-medium text-[var(--text-primary)]">
                  {formatPmCycle(record.nextEligiblePmCycle)}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-[var(--text-neutral)]">Reason</dt>
                <dd className="text-[var(--text-secondary)]">
                  {record.reason}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-[var(--text-neutral)]">Recorded</dt>
                <dd className="text-[var(--text-secondary)]">
                  {formatScheduleDateTime(record.deferredAt)}
                </dd>
              </div>
            </dl>
          </li>
        ))}
      </ol>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-[var(--text-secondary)]" aria-live="polite">
          Page {page} of {pageCount} · {deferrals.data.total} deferred cycles
        </p>
        {pageCount > 1 && (
          <nav
            aria-label="Deferred enrollment pagination"
            className="flex gap-2"
          >
            <Button
              type="button"
              variant="secondary"
              disabled={page <= 1 || deferrals.isFetching}
              onClick={() => setPage((current) => current - 1)}
            >
              Previous
            </Button>
            <Button
              type="button"
              variant="secondary"
              disabled={page >= pageCount || deferrals.isFetching}
              onClick={() => setPage((current) => current + 1)}
            >
              Next
            </Button>
          </nav>
        )}
      </div>

      {deferrals.isFetching && (
        <p className="text-sm text-[var(--text-neutral)]" role="status">
          Updating deferred cycles…
        </p>
      )}
    </Card>
  )
}
