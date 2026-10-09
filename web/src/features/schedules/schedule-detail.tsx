import { Link } from '@tanstack/react-router'
import { ZodError } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import { ScheduleBatchAssignment } from '@/features/schedules/schedule-batch-assignment'
import { useSchedule } from '@/features/schedules/schedule-queries'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import {
  formatPmCycle,
  formatPmCycleDueDate,
  formatScheduleDateTime,
} from '@/features/schedules/schedule-presentation'
import { DetailBackLink } from '@/features/shared/detail-back-link'
import {
  scheduleDetailReturnContext,
  type DetailReturnContext,
  type DetailReturnFallback,
} from '@/features/shared/detail-navigation'

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

export function ScheduleDetail({
  scheduleId,
  registrySearch = {},
  returnContext,
}: {
  scheduleId: string
  registrySearch?: ScheduleSearch
  returnContext?: DetailReturnContext | undefined
}) {
  const returnFallback: DetailReturnFallback = {
    kind: 'scheduleRegistry',
    search: registrySearch,
  }
  const isValidId =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      scheduleId,
    )
  const schedule = useSchedule(scheduleId, isValidId)
  const currentUser = useCurrentUser()
  const canAssignSupervisor = currentUser.data?.roles.includes('GSD') === true
  const hasSupervisorRole =
    currentUser.data?.roles.includes('Supervisor') === true

  if (!isValidId) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <h1 className="text-xl font-bold">Schedule not found</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The schedule link is invalid. No schedule request was made.
        </p>
        <Button asChild variant="secondary" className="mt-5">
          <DetailBackLink context={returnContext} fallback={returnFallback} />
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
          <Button asChild variant="secondary">
            <DetailBackLink context={returnContext} fallback={returnFallback} />
          </Button>
        </div>
      </Card>
    )
  }

  const record = schedule.data
  const canAssignWorker =
    hasSupervisorRole &&
    currentUser.data?.id === record.assignedSupervisorUserId
  return (
    <section
      aria-labelledby="schedule-detail-title"
      className="max-w-5xl space-y-6"
    >
      <DetailBackLink
        context={returnContext}
        fallback={returnFallback}
        className="inline-flex min-h-10 items-center text-sm font-semibold text-[var(--primary)] hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
      />
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
            Scheduled month: {formatPmCycle(record.pmCycle)}
          </p>
        </div>
        <Badge
          variant={
            record.status === 'Completed'
              ? 'success'
              : record.status === 'Overdue'
                ? 'danger'
                : record.status === 'Due' || record.status === 'Ongoing'
                  ? 'warning'
                  : 'neutral'
          }
          className="px-3 text-sm font-semibold"
        >
          {record.status}
        </Badge>
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
              search={{
                returnContext: scheduleDetailReturnContext(
                  record.id,
                  returnContext,
                  returnFallback,
                ),
              }}
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
          label="Due date"
          value={formatPmCycleDueDate(record.pmCycle)}
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

      <ScheduleBatchAssignment
        key={`${record.id}-${record.assignedSupervisorUserId ?? ''}-${record.assignedToUserId ?? ''}`}
        schedule={record}
        canAssignSupervisor={canAssignSupervisor}
        canAssignWorker={canAssignWorker}
        hasSupervisorRole={hasSupervisorRole}
      />
    </section>
  )
}
