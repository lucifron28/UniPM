import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  isGsdRole,
  type CorrectiveMaintenanceHandoff,
  type PreventiveMaintenanceForm,
  type PreventiveMaintenanceInspectionRow,
} from '@/features/preventive-maintenance-forms/form-contract'
import {
  useCorrectiveMaintenanceHandoff,
  usePreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-queries'
import {
  formStatusClass,
  formatFormDate,
  formatFormPeriod,
  inspectionConditionLabel,
} from '@/features/preventive-maintenance-forms/form-presentation'

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

function HandoffRow({
  row,
}: {
  row: CorrectiveMaintenanceHandoff['rows'][number]
}) {
  return (
    <article className="rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-sm">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold text-[var(--text-primary)]">
            Inspection {row.inspectionId}
          </p>
          <p className="mt-1 text-xs text-[var(--text-neutral)]">
            {formatFormDate(row.inspectionDate)}
          </p>
        </div>
        <Badge
          className={
            row.isOperational
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-amber-100 text-amber-800'
          }
        >
          {inspectionConditionLabel(row.isOperational)}
        </Badge>
      </div>
      <dl className="mt-5 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <DetailItem
          label="Asset/device number"
          value={row.assetDeviceNumber ?? 'Unresolved'}
        />
        <DetailItem label="Asset code" value={row.assetCode} />
        <DetailItem label="Location" value={row.location ?? ''} />
        <DetailItem
          label="Skilled worker"
          value={row.skilledWorkerIdentity ?? ''}
        />
        <DetailItem
          label="Skilled worker user ID"
          value={row.skilledWorkerUserId}
        />
      </dl>
      <div className="mt-5 grid gap-4 border-t border-[var(--border-soft)] pt-4 sm:grid-cols-2">
        <div>
          <h3 className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Finding or remarks
          </h3>
          <p className="mt-1 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {row.findingOrRemarks || 'Not recorded'}
          </p>
        </div>
        <div>
          <h3 className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Recommended corrective action
          </h3>
          <p className="mt-1 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {row.recommendedCorrectiveAction}
          </p>
        </div>
      </div>
    </article>
  )
}

function CorrectiveHandoff({
  query,
}: {
  query: ReturnType<typeof useCorrectiveMaintenanceHandoff>
}) {
  if (query.isPending) {
    return (
      <Card
        role="status"
        aria-label="Loading corrective handoff"
        className="shadow-none"
      >
        <span className="sr-only">Loading corrective-action handoff...</span>
        <Skeleton className="h-6 w-64" />
        <Skeleton className="mt-4 h-24 w-full" />
      </Card>
    )
  }

  if (query.isError) {
    return (
      <Card role="alert" className="border-[var(--warning)] shadow-none">
        <h2 className="font-semibold text-[var(--text-primary)]">
          Corrective handoff unavailable
        </h2>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The acknowledged form could not provide its corrective-action read
          model.
        </p>
        <Button
          type="button"
          className="mt-4"
          onClick={() => void query.refetch()}
        >
          Retry handoff
        </Button>
      </Card>
    )
  }

  if (!query.data) return null
  const handoff = query.data

  return (
    <section aria-labelledby="corrective-handoff-title" className="space-y-4">
      <div>
        <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
          GSD preparation view
        </p>
        <h2
          id="corrective-handoff-title"
          className="mt-1 text-2xl font-bold text-[var(--text-primary)]"
        >
          Corrective-action findings
        </h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          This read model prepares acknowledged findings for later human-led
          follow-up. It does not create or track an RMRF or WMS handoff.
        </p>
      </div>
      <Card className="grid gap-4 shadow-none sm:grid-cols-2 lg:grid-cols-4">
        <DetailItem
          label="UniPM file number"
          value={handoff.fileNumber ?? ''}
        />
        <DetailItem
          label="Acknowledged"
          value={formatFormDate(handoff.acknowledgedAt)}
        />
        <DetailItem label="Department" value={handoff.department ?? ''} />
        <DetailItem label="Building" value={handoff.building ?? ''} />
        <DetailItem label="Asset category" value={handoff.assetCategory} />
        <DetailItem
          label="Corrective-action rows"
          value={String(handoff.rows.length)}
        />
      </Card>
      {!handoff.hasCorrectiveActionRows || handoff.rows.length === 0 ? (
        <Card className="shadow-none">
          <h3 className="font-semibold text-[var(--text-primary)]">
            No corrective-action rows
          </h3>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            This acknowledged form has no rows with a recommended corrective
            action.
          </p>
        </Card>
      ) : (
        <div
          className="space-y-3"
          role="list"
          aria-label="Corrective-action findings"
        >
          {handoff.rows.map((row) => (
            <HandoffRow key={row.inspectionId} row={row} />
          ))}
        </div>
      )}
    </section>
  )
}

function InspectionRow({ row }: { row: PreventiveMaintenanceInspectionRow }) {
  return (
    <article className="rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-sm">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold text-[var(--text-primary)]">
            Inspection {row.id}
          </p>
          <p className="mt-1 text-xs text-[var(--text-neutral)]">
            {formatFormDate(row.dateInspected)}
          </p>
        </div>
        <Badge
          className={
            row.isOperational
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-amber-100 text-amber-800'
          }
        >
          {inspectionConditionLabel(row.isOperational)}
        </Badge>
      </div>
      <dl className="mt-5 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <DetailItem label="Asset ID" value={row.assetId} />
        <DetailItem label="Schedule ID" value={row.scheduleId} />
        <DetailItem
          label="Skilled worker user ID"
          value={row.inspectorUserId}
        />
        <DetailItem
          label="Inspection date"
          value={formatFormDate(row.dateInspected)}
        />
      </dl>
      <div className="mt-5 grid gap-4 border-t border-[var(--border-soft)] pt-4 sm:grid-cols-2">
        <div>
          <h3 className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Remarks
          </h3>
          <p className="mt-1 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {row.remarks || 'No remarks were recorded.'}
          </p>
        </div>
        <div>
          <h3 className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Recommended corrective action
          </h3>
          <p className="mt-1 text-sm leading-6 whitespace-pre-wrap text-[var(--text-secondary)]">
            {row.actionsRecommendations || 'No corrective action was recorded.'}
          </p>
        </div>
      </div>
    </article>
  )
}

export function FormDetail({ formId }: { formId: string }) {
  const currentUser = useCurrentUser()
  const canReview = canReviewPreventiveMaintenanceForms(currentUser.data?.roles)
  const isGsd = isGsdRole(currentUser.data?.roles)
  const validId = uuidPattern.test(formId)
  const form = usePreventiveMaintenanceForm(formId, canReview && validId)
  const handoff = useCorrectiveMaintenanceHandoff(
    formId,
    isGsd && form.data?.status === 'Acknowledged',
  )

  if (currentUser.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading user access">
        <span className="sr-only">
          Loading preventive-maintenance access...
        </span>
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (currentUser.isError || !currentUser.data) {
    return (
      <Card role="alert" className="border-[var(--warning)] shadow-none">
        Form access is temporarily unavailable.
      </Card>
    )
  }

  if (!canReview) {
    return (
      <Card role="alert" className="border-[var(--warning)] shadow-none">
        <h1 className="text-xl font-bold text-[var(--text-primary)]">
          Access restricted
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Preventive-maintenance form review is available to GSD and Inspector
          users.
        </p>
      </Card>
    )
  }

  if (!validId) {
    return (
      <Card role="alert" className="shadow-none">
        <h1 className="text-xl font-bold text-[var(--text-primary)]">
          Form not found
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The form link is invalid. No form request was made.
        </p>
      </Card>
    )
  }

  if (form.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading form">
        <span className="sr-only">Loading preventive-maintenance form...</span>
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-72 w-full" />
      </div>
    )
  }

  if (form.isError || !form.data) {
    const notFound = form.error instanceof ApiError && form.error.status === 404
    const forbidden =
      form.error instanceof ApiError && form.error.status === 403
    return (
      <Card role="alert" className="border-[var(--warning)] shadow-none">
        <h1 className="text-xl font-bold text-[var(--text-primary)]">
          {forbidden
            ? 'Access restricted'
            : notFound
              ? 'Form not found'
              : 'Form unavailable'}
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          {forbidden
            ? 'Your account is not permitted to review this form.'
            : notFound
              ? 'This preventive-maintenance form may no longer be available.'
              : 'The preventive-maintenance form could not be loaded.'}
        </p>
        {!notFound && (
          <Button
            type="button"
            className="mt-4"
            onClick={() => void form.refetch()}
          >
            Retry
          </Button>
        )}
      </Card>
    )
  }

  const record = form.data

  return (
    <section
      aria-labelledby="form-detail-title"
      className="max-w-6xl space-y-6"
    >
      <Link
        to="/app/preventive-maintenance-forms"
        className="text-sm font-semibold text-[var(--primary)] hover:underline"
      >
        Back to form review
      </Link>
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Preventive-maintenance form
          </p>
          <h1
            id="form-detail-title"
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
          >
            {record.fileNumber ?? 'Unsubmitted form'}
          </h1>
          <p className="mt-2 text-sm break-all text-[var(--text-secondary)]">
            {record.id}
          </p>
        </div>
        <Badge className={formStatusClass(record.status)}>
          {record.status}
        </Badge>
      </div>
      <Card className="grid gap-5 shadow-none sm:grid-cols-2 lg:grid-cols-4">
        <DetailItem label="Asset category" value={record.assetCategory} />
        <DetailItem label="Building" value={record.building ?? ''} />
        <DetailItem label="Department" value={record.department ?? ''} />
        <DetailItem label="Period" value={formatFormPeriod(record)} />
        <DetailItem label="Created by user ID" value={record.createdByUserId} />
        <DetailItem
          label="Submitted by user ID"
          value={record.submittedByUserId ?? ''}
        />
        <DetailItem
          label="Submitted"
          value={formatFormDate(record.submittedAt)}
        />
        <DetailItem
          label="Inspection rows"
          value={String(record.inspections.length)}
        />
      </Card>
      <section aria-labelledby="form-inspections-title" className="space-y-4">
        <div>
          <h2
            id="form-inspections-title"
            className="text-2xl font-bold text-[var(--text-primary)]"
          >
            Inspection rows
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Source rows are shown for review. Condition labels describe the
            recorded operational result and do not represent workflow
            completion.
          </p>
        </div>
        {record.inspections.length === 0 ? (
          <Card className="shadow-none">
            No inspection rows are attached to this form.
          </Card>
        ) : (
          <div className="space-y-3" role="list" aria-label="Inspection rows">
            {record.inspections.map((row) => (
              <InspectionRow key={row.id} row={row} />
            ))}
          </div>
        )}
      </section>
      {isGsd && record.status === 'Acknowledged' && (
        <CorrectiveHandoff query={handoff} />
      )}
    </section>
  )
}
