import { Link } from '@tanstack/react-router'
import { ZodError } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useSchedule } from '@/features/schedules/schedule-queries'
import {
  formatScheduleDate,
  formatScheduleDateTime,
} from '@/features/schedules/schedule-presentation'

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </dt>
      <dd className="mt-1 text-sm break-words text-[var(--text-primary)]">
        {value || 'Not recorded'}
      </dd>
    </div>
  )
}

export function ScheduleDetail({ scheduleId }: { scheduleId: string }) {
  const isValidId =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      scheduleId,
    )
  const schedule = useSchedule(scheduleId, isValidId)

  if (!isValidId) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <h1 className="text-xl font-bold">Schedule not found</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The schedule link is invalid. No schedule request was made.
        </p>
        <Button asChild className="mt-5">
          <Link to="/app/schedules">Return to schedules</Link>
        </Button>
      </Card>
    )
  }

  if (schedule.isPending) {
    return (
      <div className="space-y-4" role="status">
        <span className="sr-only">Loading schedule detail...</span>
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (schedule.isError || !schedule.data) {
    const error = schedule.error
    const isNotFound = error instanceof ApiError && error.status === 404
    const isNetwork =
      error instanceof ApiError && error.classification === 'network'
    const malformed = error instanceof ZodError

    return (
      <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
        <h1 className="text-xl font-bold">
          {isNotFound
            ? 'Schedule not found'
            : isNetwork
              ? 'Service unavailable'
              : malformed
                ? 'Schedule record error'
                : 'Schedule details unavailable'}
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          {isNotFound
            ? 'This schedule may no longer be available.'
            : isNetwork
              ? 'The service could not be reached. Check your connection and try again.'
              : malformed
                ? 'The schedule response did not match the public API contract.'
                : 'The schedule record could not be loaded.'}
        </p>
        <div className="mt-5 flex gap-3">
          {!isNotFound && (
            <Button type="button" onClick={() => void schedule.refetch()}>
              Retry
            </Button>
          )}
          <Button
            asChild
            className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
          >
            <Link to="/app/schedules">Return to schedules</Link>
          </Button>
        </div>
      </Card>
    )
  }

  const record = schedule.data
  return (
    <section
      aria-labelledby="schedule-detail-title"
      className="max-w-5xl space-y-6"
    >
      <Link
        to="/app/schedules"
        className="text-sm font-semibold text-[var(--primary)] hover:underline"
      >
        Back to schedules
      </Link>
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Preventive maintenance schedule
          </p>
          <h1
            id="schedule-detail-title"
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
          >
            {record.asset?.assetCode ?? 'Schedule record'}
          </h1>
          <p className="mt-2 text-[var(--text-secondary)]">
            Scheduled for {formatScheduleDate(record.scheduleDate)}
          </p>
        </div>
        <span className="inline-flex rounded-full bg-[var(--page-background)] px-3 py-1 text-sm font-semibold">
          {record.status}
        </span>
      </div>

      <Card className="grid gap-6 shadow-none md:grid-cols-2 lg:grid-cols-3">
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Asset code
          </dt>
          <dd className="mt-1 text-sm">
            <Link
              to="/app/assets/$assetId"
              params={{ assetId: record.assetId }}
              className="font-semibold text-[var(--primary)] hover:underline"
            >
              {record.asset?.assetCode ?? record.assetId}
            </Link>
          </dd>
        </div>
        <DetailItem
          label="Asset category"
          value={record.asset?.assetCategory ?? 'Not recorded'}
        />
        <DetailItem
          label="Location"
          value={record.asset?.location ?? 'Not recorded'}
        />
        <DetailItem
          label="Schedule date"
          value={formatScheduleDate(record.scheduleDate)}
        />
        <DetailItem label="Period type" value={record.periodType} />
        <DetailItem label="Quarter" value={record.quarter ?? 'Not recorded'} />
        <DetailItem
          label="Semester"
          value={record.semester ?? 'Not recorded'}
        />
        <DetailItem
          label="Year"
          value={record.year?.toString() ?? 'Not recorded'}
        />
        <DetailItem label="Recorded status" value={record.status} />
        <DetailItem
          label="Academic year"
          value={record.academicYear ?? 'Not recorded'}
        />
        {record.assignedToUserId && (
          <DetailItem
            label="Assigned user ID"
            value={record.assignedToUserId}
          />
        )}
        <DetailItem
          label="Completed"
          value={formatScheduleDateTime(record.completedAt)}
        />
        <DetailItem
          label="Created"
          value={formatScheduleDateTime(record.createdAt)}
        />
        <DetailItem
          label="Last updated"
          value={formatScheduleDateTime(record.updatedAt)}
        />
      </Card>

      <Card className="border-dashed shadow-none">
        <h2 className="font-semibold">Recorded contract only</h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Recurrence generation, status transitions, assignment, editing, and
          deletion are not part of the current schedule workflow.
        </p>
      </Card>
    </section>
  )
}
