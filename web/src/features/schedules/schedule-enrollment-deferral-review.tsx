import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  formatPmCycle,
  formatScheduleDateTime,
  getCurrentManilaYear,
} from '@/features/schedules/schedule-presentation'
import {
  useReviewScheduleEnrollmentDeferral,
  useScheduleEnrollmentDeferrals,
} from '@/features/schedules/schedule-queries'

type DeferralStatusFilter = '' | 'NeedsReview' | 'Reviewed'

const pageSize = 10

export function ScheduleEnrollmentDeferralReview() {
  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] =
    useState<DeferralStatusFilter>('NeedsReview')
  const [reviewNotes, setReviewNotes] = useState<Record<string, string>>({})
  const [reviewMessage, setReviewMessage] = useState<string | null>(null)
  const deferrals = useScheduleEnrollmentDeferrals(page, true, {
    ...(statusFilter ? { status: statusFilter } : {}),
  })
  const reviewDeferral = useReviewScheduleEnrollmentDeferral()

  if (deferrals.isPending) {
    return (
      <p className="text-sm text-[var(--text-neutral)]" role="status">
        Checking for deferred asset cycles…
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
          The schedule registry is available, but GSD could not load deferred
          cycles.
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
  const totalMatching = Number(deferrals.data.total)
  const pendingCount = Number(deferrals.data.pendingCount)
  const reviewedCount = Number(deferrals.data.reviewedCount)
  const totalDeferrals = pendingCount + reviewedCount
  const pageCount = Math.max(1, Math.ceil(totalMatching / pageSize))
  if (totalDeferrals === 0) return null

  return (
    <Card
      id="schedule-enrollment-deferrals"
      aria-labelledby="schedule-enrollment-review-title"
      className="space-y-4 border-[color-mix(in_srgb,var(--warning)_35%,white)] p-5 shadow-none"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2
            id="schedule-enrollment-review-title"
            className="text-lg font-bold text-[var(--text-primary)]"
          >
            Deferred asset enrollment
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-[var(--text-secondary)]">
            These cycles could not join a PM batch after work began. GSD review
            records an acknowledgement only. It does not create a schedule,
            complete or waive PM work, or change asset condition.
          </p>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            If a next eligible cycle belongs to a future year, it is a planning
            reference. UniPM generates that schedule when the year begins.
          </p>
        </div>
        <div
          className="flex flex-wrap gap-2"
          aria-label="Deferred cycle totals"
        >
          <Badge variant="warning">{pendingCount} needs review</Badge>
          <Badge variant="success">{reviewedCount} reviewed</Badge>
        </div>
      </div>

      <div className="max-w-xs">
        <label
          htmlFor="schedule-enrollment-review-filter"
          className="mb-1 block text-sm font-medium text-[var(--text-primary)]"
        >
          Review status
        </label>
        <select
          id="schedule-enrollment-review-filter"
          className="min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm text-[var(--text-primary)] outline-none focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]"
          value={statusFilter}
          onChange={(event) => {
            setPage(1)
            setStatusFilter(event.target.value as DeferralStatusFilter)
          }}
        >
          <option value="NeedsReview">Needs review</option>
          <option value="Reviewed">Reviewed</option>
          <option value="">All deferred cycles</option>
        </select>
      </div>

      {reviewMessage && (
        <p className="text-sm text-[var(--success)]" role="status">
          {reviewMessage}
        </p>
      )}
      {reviewDeferral.isError && (
        <p className="text-sm text-[var(--error)]" role="alert">
          The review could not be saved. Refresh the list and try again.
        </p>
      )}

      {records.length === 0 ? (
        <p className="rounded-lg border border-[var(--border-soft)] p-4 text-sm text-[var(--text-secondary)]">
          No deferred cycles match this review status.
        </p>
      ) : (
        <ol className="grid gap-3">
          {records.map((record) => {
            const key = `${record.assetId}-${record.deferredPmCycle}`
            const nextCycleIsFuture =
              Number(record.nextEligiblePmCycle.slice(0, 4)) >
              getCurrentManilaYear()

            return (
              <li
                key={key}
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
                  <Badge
                    variant={
                      record.status === 'Reviewed' ? 'success' : 'warning'
                    }
                  >
                    {record.status}
                  </Badge>
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
                      {nextCycleIsFuture
                        ? 'Next eligible cycle, planning reference'
                        : 'Next eligible PM cycle'}
                    </dt>
                    <dd className="font-medium text-[var(--text-primary)]">
                      {formatPmCycle(record.nextEligiblePmCycle)}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs text-[var(--text-neutral)]">
                      Reason
                    </dt>
                    <dd className="text-[var(--text-secondary)]">
                      {record.reason}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs text-[var(--text-neutral)]">
                      Recorded
                    </dt>
                    <dd className="text-[var(--text-secondary)]">
                      {formatScheduleDateTime(record.deferredAt)}
                    </dd>
                  </div>
                </dl>

                {record.status === 'Reviewed' ? (
                  <div className="mt-3 border-t border-[var(--border-soft)] pt-3 text-sm text-[var(--text-secondary)]">
                    <p>
                      Reviewed {formatScheduleDateTime(record.reviewedAt)}
                      {record.reviewedByDisplayName
                        ? ` by ${record.reviewedByDisplayName}`
                        : record.reviewedByUserId
                          ? ` by account ${record.reviewedByUserId}`
                          : ''}
                    </p>
                    {record.reviewNote && (
                      <p className="mt-1">Review note: {record.reviewNote}</p>
                    )}
                  </div>
                ) : (
                  <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-end">
                    <div className="min-w-0 flex-1">
                      <label
                        htmlFor={`deferral-review-note-${key}`}
                        className="mb-1 block text-sm font-medium text-[var(--text-primary)]"
                      >
                        Optional review note
                      </label>
                      <textarea
                        id={`deferral-review-note-${key}`}
                        maxLength={1000}
                        rows={2}
                        className="w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 py-2 text-sm text-[var(--text-primary)] outline-none focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]"
                        value={reviewNotes[key] ?? ''}
                        onChange={(event) =>
                          setReviewNotes((notes) => ({
                            ...notes,
                            [key]: event.target.value,
                          }))
                        }
                      />
                    </div>
                    <Button
                      type="button"
                      disabled={reviewDeferral.isPending}
                      onClick={() =>
                        reviewDeferral.mutate(
                          {
                            assetId: record.assetId,
                            pmCycle: record.deferredPmCycle,
                            request: { note: reviewNotes[key] ?? null },
                          },
                          {
                            onSuccess: () => {
                              setReviewNotes((notes) => {
                                const next = { ...notes }
                                delete next[key]
                                return next
                              })
                              setPage(1)
                              setReviewMessage('Deferred cycle review saved.')
                            },
                          },
                        )
                      }
                    >
                      Mark reviewed
                    </Button>
                  </div>
                )}
              </li>
            )
          })}
        </ol>
      )}

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-[var(--text-secondary)]" aria-live="polite">
          Page {page} of {pageCount} · {totalMatching} matching cycle
          {totalMatching === 1 ? '' : 's'}
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
