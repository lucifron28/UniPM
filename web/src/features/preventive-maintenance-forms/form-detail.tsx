import {
  useRef,
  useState,
  type FormEvent,
  type PointerEvent,
  type RefObject,
} from 'react'
import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import type { PreventiveMaintenanceAcknowledgementResponse } from '@/api/generated/models/preventiveMaintenanceAcknowledgementResponse'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  isGsdRole,
  type CorrectiveMaintenanceHandoff,
  type PreventiveMaintenanceInspectionRow,
} from '@/features/preventive-maintenance-forms/form-contract'
import {
  useCorrectiveMaintenanceHandoff,
  useAcknowledgePreventiveMaintenanceFormMutation,
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

function SignaturePad({
  canvasRef,
  disabled,
  onDrawnChange,
}: {
  canvasRef: RefObject<HTMLCanvasElement | null>
  disabled: boolean
  onDrawnChange: (drawn: boolean) => void
}) {
  const [isDrawing, setIsDrawing] = useState(false)
  const lastPoint = useRef<{ x: number; y: number } | null>(null)

  function pointFor(event: PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current
    if (!canvas) return null
    const bounds = canvas.getBoundingClientRect()
    return {
      x: ((event.clientX - bounds.left) / bounds.width) * canvas.width,
      y: ((event.clientY - bounds.top) / bounds.height) * canvas.height,
    }
  }

  function begin(event: PointerEvent<HTMLCanvasElement>) {
    if (disabled) return
    const point = pointFor(event)
    if (!point) return
    if (event.currentTarget.setPointerCapture) {
      event.currentTarget.setPointerCapture(event.pointerId)
    }
    lastPoint.current = point
    setIsDrawing(true)
    onDrawnChange(true)
  }

  function move(event: PointerEvent<HTMLCanvasElement>) {
    if (!isDrawing) return
    const canvas = canvasRef.current
    const previous = lastPoint.current
    const point = pointFor(event)
    if (!canvas || !previous || !point) return
    const context = canvas.getContext('2d')
    if (!context) return
    context.strokeStyle = '#17212b'
    context.lineWidth = 2
    context.lineCap = 'round'
    context.beginPath()
    context.moveTo(previous.x, previous.y)
    context.lineTo(point.x, point.y)
    context.stroke()
    lastPoint.current = point
  }

  function end(event: PointerEvent<HTMLCanvasElement>) {
    if (!isDrawing) return
    setIsDrawing(false)
    lastPoint.current = null
    if (
      event.currentTarget.hasPointerCapture &&
      event.currentTarget.hasPointerCapture(event.pointerId)
    ) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }
  }

  function clear() {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height)
    onDrawnChange(false)
  }

  return (
    <div>
      <canvas
        ref={canvasRef}
        aria-label="Signature"
        width={640}
        height={180}
        className="h-36 w-full rounded-lg border border-[var(--border-soft)] bg-white"
        style={{ touchAction: 'none' }}
        onPointerDown={begin}
        onPointerMove={move}
        onPointerUp={end}
        onPointerCancel={end}
      />
      <div className="mt-2 flex items-center justify-between gap-3">
        <p className="text-xs text-[var(--text-neutral)]">
          Draw the department-head signature in the box.
        </p>
        <Button
          type="button"
          className="bg-[var(--surface-muted)] text-[var(--text-primary)] hover:bg-[var(--border-soft)]"
          onClick={clear}
          disabled={disabled}
        >
          Clear
        </Button>
      </div>
    </div>
  )
}

function acknowledgementError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.classification === 'network') {
      return 'The service could not be reached. Try again when the connection is available.'
    }
    if (error.status === 400) {
      return 'The acknowledgement details were rejected. Check the signatory fields and signature.'
    }
    if (error.status === 403) {
      return 'Your account is not permitted to acknowledge this form.'
    }
    if (error.status === 409) {
      return 'This form is no longer available for acknowledgement.'
    }
  }
  return 'The form could not be acknowledged. Please try again.'
}

function AcknowledgementSummary({
  acknowledgement,
}: {
  acknowledgement: PreventiveMaintenanceAcknowledgementResponse
}) {
  return (
    <Card className="shadow-none" role="status">
      <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
        Acknowledgement recorded
      </p>
      <dl className="mt-3 grid gap-4 text-sm sm:grid-cols-3">
        <DetailItem
          label="Signatory name"
          value={acknowledgement.signatoryName}
        />
        <DetailItem
          label="Signatory position"
          value={acknowledgement.signatoryPosition}
        />
        <DetailItem
          label="Acknowledged"
          value={formatFormDate(acknowledgement.acknowledgedAt)}
        />
      </dl>
    </Card>
  )
}

function AcknowledgeForm({
  formId,
  onAcknowledged,
}: {
  formId: string
  onAcknowledged: (
    acknowledgement: PreventiveMaintenanceAcknowledgementResponse,
  ) => void
}) {
  const mutation = useAcknowledgePreventiveMaintenanceFormMutation()
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const [signatoryName, setSignatoryName] = useState('')
  const [signatoryPosition, setSignatoryPosition] = useState('')
  const [signatureDrawn, setSignatureDrawn] = useState(false)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [isConfirming, setIsConfirming] = useState(false)

  async function acknowledge(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    if (!signatoryName.trim() || !signatoryPosition.trim()) {
      setValidationError('Signatory name and position are required.')
      return
    }
    if (!signatureDrawn) {
      setValidationError(
        'Capture the department-head signature before continuing.',
      )
      return
    }

    setIsConfirming(true)
  }

  async function submitAcknowledgement() {
    setIsConfirming(false)

    const dataUrl = canvasRef.current?.toDataURL('image/png') ?? ''
    const comma = dataUrl.indexOf(',')
    const signatureData = comma >= 0 ? dataUrl.slice(comma + 1) : dataUrl
    if (!signatureData) {
      setValidationError('The signature could not be captured. Try again.')
      return
    }

    try {
      const acknowledgement = await mutation.mutateAsync({
        id: formId,
        data: {
          signatoryName: signatoryName.trim(),
          signatoryPosition: signatoryPosition.trim(),
          signatureData,
          signatureContentType: 'image/png',
        },
      })
      onAcknowledged(acknowledgement)
    } catch {
      // The bounded mutation state below provides the user-facing message.
    }
  }

  return (
    <Card className="space-y-5 shadow-none">
      <div>
        <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
          Department-head acknowledgement
        </p>
        <h2 className="mt-1 text-2xl font-bold text-[var(--text-primary)]">
          Acknowledge submitted form
        </h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Acknowledgement locks the form, completes its linked schedules, and
          publishes the inspection rows as official history. It records receipt
          and noting of the findings; it does not approve corrective work or
          budget.
        </p>
      </div>
      <form className="space-y-4" onSubmit={acknowledge}>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm font-semibold text-[var(--text-primary)]">
            Signatory name
            <input
              className="mt-2 min-h-10 w-full rounded-lg border border-[var(--border-soft)] px-3 font-normal outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
              value={signatoryName}
              maxLength={160}
              onChange={(event) => setSignatoryName(event.target.value)}
            />
          </label>
          <label className="text-sm font-semibold text-[var(--text-primary)]">
            Signatory position
            <input
              className="mt-2 min-h-10 w-full rounded-lg border border-[var(--border-soft)] px-3 font-normal outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
              value={signatoryPosition}
              maxLength={160}
              onChange={(event) => setSignatoryPosition(event.target.value)}
            />
          </label>
        </div>
        <div>
          <p className="text-sm font-semibold text-[var(--text-primary)]">
            Signature
          </p>
          <div className="mt-2">
            <SignaturePad
              canvasRef={canvasRef}
              disabled={mutation.isPending}
              onDrawnChange={setSignatureDrawn}
            />
          </div>
        </div>
        {validationError && (
          <p role="alert" className="text-sm text-[var(--danger)]">
            {validationError}
          </p>
        )}
        {mutation.isError && (
          <p role="alert" className="text-sm text-[var(--danger)]">
            {acknowledgementError(mutation.error)}
          </p>
        )}
        {mutation.isSuccess && (
          <p role="status" className="text-sm text-[var(--success)]">
            Acknowledgement recorded. The form is now locked.
          </p>
        )}
        {isConfirming && (
          <div
            role="dialog"
            aria-labelledby="acknowledgement-confirmation-title"
            className="rounded-lg border border-[var(--border-soft)] bg-[var(--surface-muted)] p-4"
          >
            <h3
              id="acknowledgement-confirmation-title"
              className="font-semibold text-[var(--text-primary)]"
            >
              Confirm department-head acknowledgement
            </h3>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              This records receipt/noting of the preventive-maintenance
              findings, locks the form, completes linked schedules, and does not
              approve corrective work or budget.
            </p>
            <div className="mt-4 flex flex-wrap justify-end gap-3">
              <Button
                type="button"
                className="bg-[var(--surface-muted)] text-[var(--text-primary)] hover:bg-[var(--border-soft)]"
                onClick={() => setIsConfirming(false)}
              >
                Cancel
              </Button>
              <Button
                type="button"
                onClick={() => void submitAcknowledgement()}
              >
                Confirm acknowledgement
              </Button>
            </div>
          </div>
        )}
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'Acknowledging...'
            : isConfirming
              ? 'Review acknowledgement'
              : 'Acknowledge form'}
        </Button>
      </form>
    </Card>
  )
}

export function FormDetail({ formId }: { formId: string }) {
  const [acknowledgement, setAcknowledgement] =
    useState<PreventiveMaintenanceAcknowledgementResponse | null>(null)
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
      {acknowledgement && (
        <AcknowledgementSummary acknowledgement={acknowledgement} />
      )}
      {record.status === 'Submitted' && !acknowledgement && (
        <AcknowledgeForm
          formId={record.id}
          onAcknowledged={setAcknowledgement}
        />
      )}
      {isGsd && record.status === 'Acknowledged' && (
        <CorrectiveHandoff query={handoff} />
      )}
    </section>
  )
}
