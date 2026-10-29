import {
  RecordField as DetailItem,
  RecordHeader,
  RecordMetadata,
  RecordSection,
} from '@/features/shared/record-layout'
import { Link } from '@tanstack/react-router'
import { ZodError } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { ScheduleBatchAssignment } from '@/features/schedules/schedule-batch-assignment'
import { useSchedule } from '@/features/schedules/schedule-queries'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import {
  formatAssignmentPeriodDateTime,
  formatAssignmentPeriodStatus,
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
    <section aria-labelledby="schedule-detail-title" className="record-page">
      <DetailBackLink context={returnContext} fallback={returnFallback} />
      <RecordHeader
        id="schedule-detail-title"
        eyebrow="Preventive maintenance schedule"
        title={record.asset?.assetCode ?? 'Schedule record'}
        description={
          <>
            {record.asset
              ? formatAssetCategory(record.asset.assetCategory)
              : 'Asset category unavailable'}{' '}
            · {record.asset?.department ?? 'Department unavailable'} ·{' '}
            {formatPmCycle(record.pmCycle)}
          </>
        }
        status={
          <Badge
            variant={
              record.status === 'Completed'
                ? 'success'
                : record.status === 'Overdue'
                  ? 'danger'
                  : record.status === 'Cancelled'
                    ? 'neutral'
                    : 'warning'
            }
            className="px-3 text-sm"
          >
            {record.status}
          </Badge>
        }
      />
      <div className="record-columns">
        <div className="record-stack">
          <RecordSection title="Deadlines and progress">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="record-deadline">
                <p className="text-xs font-semibold text-[var(--primary)]">
                  Original PM deadline
                </p>
                <strong>{formatPmCycleDueDate(record.pmCycle)}</strong>
                <p className="record-note">
                  Scheduled month: {formatPmCycle(record.pmCycle)}. Assignment
                  does not change this date.
                </p>
              </div>
              <div
                className="record-deadline"
                data-attention={record.assignmentPeriodStatus === 'Overdue'}
              >
                <p className="text-xs font-semibold text-[var(--primary)]">
                  Inspector assignment due
                </p>
                <strong>
                  {record.inspectorAssignmentDueAt
                    ? formatAssignmentPeriodDateTime(
                        record.inspectorAssignmentDueAt,
                      )
                    : record.assignedToUserId
                      ? 'Timing unavailable'
                      : 'Not assigned'}
                </strong>
                <p className="record-note">
                  {formatAssignmentPeriodStatus(record.assignmentPeriodStatus)}
                </p>
              </div>
            </div>
            <p className="record-note mt-4">
              The Inspector has 30 calendar days from the first saved
              assignment. Reassignment does not restart that period.
            </p>
            <dl className="mt-4 grid gap-4 sm:grid-cols-2">
              <DetailItem
                label="Completed"
                value={
                  record.completedAt
                    ? formatScheduleDateTime(record.completedAt)
                    : 'Not completed'
                }
              />
              <DetailItem label="Recorded status" value={record.status} />
            </dl>
          </RecordSection>

          <ScheduleBatchAssignment
            key={`${record.id}-${record.assignedSupervisorUserId ?? ''}-${record.assignedToUserId ?? ''}`}
            schedule={record}
            canAssignSupervisor={canAssignSupervisor}
            canAssignWorker={canAssignWorker}
            hasSupervisorRole={hasSupervisorRole}
          />
        </div>
        <div className="record-stack">
          <RecordSection title="Asset and location">
            <dl className="grid gap-4 sm:grid-cols-2 xl:grid-cols-1">
              <DetailItem
                label="Asset code"
                value={
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
                    className="font-semibold text-[var(--primary)] underline-offset-4 hover:underline"
                  >
                    {record.asset?.assetCode ?? record.assetId}
                  </Link>
                }
              />
              <DetailItem
                label="Asset category"
                value={
                  record.asset
                    ? formatAssetCategory(record.asset.assetCategory)
                    : 'Not recorded'
                }
              />
              <DetailItem label="Department" value={record.asset?.department} />
              <DetailItem label="Building" value={record.asset?.building} />
              <DetailItem label="Location" value={record.asset?.location} />
            </dl>
          </RecordSection>
          <RecordMetadata title="Assignment timing details">
            <dl className="grid gap-4">
              <DetailItem
                label="First Supervisor assignment"
                value={
                  record.firstSupervisorAssignedAt
                    ? formatAssignmentPeriodDateTime(
                        record.firstSupervisorAssignedAt,
                      )
                    : 'Timing unavailable'
                }
              />
              <DetailItem
                label="First Inspector assignment"
                value={
                  record.firstInspectorAssignedAt
                    ? formatAssignmentPeriodDateTime(
                        record.firstInspectorAssignedAt,
                      )
                    : 'Timing unavailable'
                }
              />
            </dl>
          </RecordMetadata>
          <RecordMetadata>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailItem
                label="Due date"
                value={formatPmCycleDueDate(record.pmCycle)}
              />
              <DetailItem label="Period type" value={record.periodType} />
              <DetailItem label="Quarter" value={record.quarter} />
              <DetailItem label="Semester" value={record.semester} />
              <DetailItem label="Year" value={record.year?.toString()} />
              <DetailItem label="Academic year" value={record.academicYear} />
              <DetailItem
                label="Created"
                value={formatScheduleDateTime(record.createdAt)}
              />
              <DetailItem
                label="Last updated"
                value={formatScheduleDateTime(record.updatedAt)}
              />
            </dl>
          </RecordMetadata>
        </div>
      </div>
    </section>
  )
}
