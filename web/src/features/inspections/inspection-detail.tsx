import { Link } from '@tanstack/react-router'
import { ZodError } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAsset } from '@/features/assets/asset-queries'
import { useInspection } from '@/features/inspections/inspection-queries'
import {
  formatInspectionDate,
  inspectionOutcome,
} from '@/features/inspections/inspection-presentation'
import { useSchedule } from '@/features/schedules/schedule-queries'
import { formatScheduleDate } from '@/features/schedules/schedule-presentation'

const uuidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

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

function DetailError({
  title,
  message,
  retry,
}: {
  title: string
  message: string
  retry?: (() => void) | undefined
}) {
  return (
    <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
      <h1 className="text-xl font-bold text-[var(--text-primary)]">{title}</h1>
      <p className="mt-2 text-sm text-[var(--text-secondary)]">{message}</p>
      <div className="mt-5 flex gap-3">
        {retry && (
          <Button type="button" onClick={retry}>
            Retry
          </Button>
        )}
        <Button
          asChild
          className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
        >
          <Link to="/app/inspections">Return to inspections</Link>
        </Button>
      </div>
    </Card>
  )
}

export function InspectionDetail({ inspectionId }: { inspectionId: string }) {
  const isValidId = uuidPattern.test(inspectionId)
  const inspection = useInspection(inspectionId, isValidId)
  const asset = useAsset(
    inspection.data?.assetId ?? '',
    Boolean(inspection.data),
  )
  const schedule = useSchedule(
    inspection.data?.scheduleId ?? '',
    Boolean(inspection.data),
  )

  if (!isValidId) {
    return (
      <DetailError
        title="Inspection not found"
        message="The inspection link is invalid. No inspection request was made."
      />
    )
  }

  if (inspection.isPending) {
    return (
      <div className="space-y-4" role="status">
        <span className="sr-only">Loading inspection detail...</span>
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-72 w-full" />
      </div>
    )
  }

  if (inspection.isError || !inspection.data) {
    const error = inspection.error
    const notFound = error instanceof ApiError && error.status === 404
    const network =
      error instanceof ApiError && error.classification === 'network'
    const malformed = error instanceof ZodError
    return (
      <DetailError
        title={
          notFound
            ? 'Inspection not found'
            : network
              ? 'Service unavailable'
              : malformed
                ? 'Inspection record error'
                : 'Inspection details unavailable'
        }
        message={
          notFound
            ? 'This inspection record may no longer be available.'
            : network
              ? 'The service could not be reached. Check your connection and try again.'
              : malformed
                ? 'The inspection response did not match the public API contract.'
                : 'The inspection record could not be loaded.'
        }
        retry={notFound ? undefined : () => void inspection.refetch()}
      />
    )
  }

  const record = inspection.data
  const assetLabel = asset.data?.assetCode ?? record.assetId
  const scheduleLabel = schedule.data
    ? formatScheduleDate(schedule.data.scheduleDate)
    : record.scheduleId

  return (
    <section
      aria-labelledby="inspection-detail-title"
      className="max-w-5xl space-y-6"
    >
      <Link
        to="/app/inspections"
        className="text-sm font-semibold text-[var(--primary)] hover:underline"
      >
        Back to inspections
      </Link>
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Inspection source record
          </p>
          <h1
            id="inspection-detail-title"
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
          >
            Inspection {record.id}
          </h1>
          <p className="mt-2 text-[var(--text-secondary)]">
            Recorded {formatInspectionDate(record.dateInspected)}
          </p>
        </div>
        <span className="inline-flex rounded-full bg-[var(--page-background)] px-3 py-1 text-sm font-semibold">
          {inspectionOutcome(record.isOperational)}
        </span>
      </div>

      <Card className="grid gap-6 shadow-none md:grid-cols-2 lg:grid-cols-3">
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Asset
          </dt>
          <dd className="mt-1 text-sm">
            <Link
              to="/app/assets/$assetId"
              params={{ assetId: record.assetId }}
              className="font-semibold text-[var(--primary)] hover:underline"
            >
              {assetLabel}
            </Link>
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Schedule
          </dt>
          <dd className="mt-1 text-sm">
            <Link
              to="/app/schedules/$scheduleId"
              params={{ scheduleId: record.scheduleId }}
              className="font-semibold text-[var(--primary)] hover:underline"
            >
              {scheduleLabel}
            </Link>
          </dd>
        </div>
        <DetailItem label="Inspector user ID" value={record.inspectorUserId} />
        <DetailItem
          label="Date inspected"
          value={formatInspectionDate(record.dateInspected)}
        />
        <DetailItem
          label="Recorded operational result"
          value={inspectionOutcome(record.isOperational)}
        />
        <DetailItem
          label="Created"
          value={formatInspectionDate(record.createdAt)}
        />
        <DetailItem
          label="Last updated"
          value={formatInspectionDate(record.updatedAt)}
        />
      </Card>

      {(asset.isError || schedule.isError) && (
        <Card role="alert" className="border-[var(--warning)] p-4 shadow-none">
          <p className="font-semibold text-[var(--text-primary)]">
            Some linked context is unavailable.
          </p>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            The source inspection remains available. Asset and schedule IDs are
            shown above while their additional context is retried.
          </p>
          <div className="mt-3 flex flex-wrap gap-3">
            {asset.isError && (
              <Button type="button" onClick={() => void asset.refetch()}>
                Retry asset context
              </Button>
            )}
            {schedule.isError && (
              <Button type="button" onClick={() => void schedule.refetch()}>
                Retry schedule context
              </Button>
            )}
          </div>
        </Card>
      )}

      <Card className="space-y-5 shadow-none">
        <div>
          <h2 className="font-semibold">Remarks</h2>
          <p className="mt-2 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {record.remarks || 'No remarks were recorded.'}
          </p>
        </div>
        <div>
          <h2 className="font-semibold">Actions and recommendations</h2>
          <p className="mt-2 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {record.actionsRecommendations ||
              'No actions or recommendations were recorded.'}
          </p>
        </div>
      </Card>
    </section>
  )
}
