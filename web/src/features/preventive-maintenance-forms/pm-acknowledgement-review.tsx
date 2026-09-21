import { useState } from 'react'
import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import type {
  PmPeriodDashboardAssetRowResponse,
  PmPeriodDashboardBatchResponse,
} from '@/api/generated/models'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  type PreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-contract'
import {
  AcknowledgeForm,
} from '@/features/preventive-maintenance-forms/form-detail'
import {
  usePreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-queries'
import {
  formStatusClass,
  formStatusLabel,
  formatFormDate,
  formatFormPeriod,
} from '@/features/preventive-maintenance-forms/form-presentation'
import { usePmPeriodDashboard } from '@/features/reports/pm-period-dashboard-queries'

const uuidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export type PmAcknowledgementReviewSearch = {
  department?: string | undefined
  assetCategory?: string | undefined
  pmCycle?: string | undefined
}

export type PmAcknowledgementReviewContext = {
  reviewFormId: string
  department?: string | undefined
  assetCategory: string
  pmCycle: string
}

function formatCategory(value: string) {
  return value
    .split('-')
    .map((part) => `${part.slice(0, 1).toUpperCase()}${part.slice(1)}`)
    .join(' ')
}

function formatCycle(value: string) {
  const [yearText, monthText] = value.split('-')
  const year = Number(yearText)
  const month = Number(monthText)
  if (!Number.isInteger(year) || !Number.isInteger(month)) return value

  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, 1)))
}

function formatNumber(value: number | string) {
  return typeof value === 'number' ? value.toLocaleString() : value
}

function formatPercent(value: number | string | null) {
  return value === null ? 'Not measurable yet' : `${value}%`
}

function normalizeDepartment(value: string | null | undefined) {
  return value?.trim().toUpperCase() ?? ''
}

function formatCondition(value: string) {
  switch (value) {
    case 'Operational':
      return 'Operational'
    case 'NonOperational':
      return 'Not operational'
    case 'NotInspected':
      return 'Not inspected'
    default:
      return value || 'Not recorded'
  }
}

function detailValue(value: string | null | undefined) {
  return value || 'Not recorded'
}

function locationValue(asset: Pick<PmPeriodDashboardAssetRowResponse, 'building' | 'location'>) {
  return (
    [asset.building, asset.location].filter(Boolean).join(' · ') ||
    'Not recorded'
  )
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </dt>
      <dd className="mt-1 text-sm break-words text-[var(--text-primary)]">
        {detailValue(value)}
      </dd>
    </div>
  )
}

function ReviewError({
  title,
  message,
  retry,
}: {
  title: string
  message: string
  retry?: (() => void) | undefined
}) {
  return (
    <Card role="alert" className="border-[var(--warning)] p-6 shadow-none">
      <h1 className="text-xl font-bold text-[var(--text-primary)]">{title}</h1>
      <p className="mt-2 text-sm text-[var(--text-secondary)]">{message}</p>
      <div className="mt-5 flex flex-wrap gap-3">
        {retry && (
          <Button type="button" onClick={retry}>
            Retry
          </Button>
        )}
        <Button
          asChild
          className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
        >
          <Link to="/app/preventive-maintenance-forms">
            Return to form review
          </Link>
        </Button>
      </div>
    </Card>
  )
}

function Summary({
  batch,
  form,
}: {
  batch: PmPeriodDashboardBatchResponse
  form: PreventiveMaintenanceForm
}) {
  const status = form.status === 'Submitted' ? form.status : batch.formStatus

  return (
    <Card
      aria-label="Submitted batch review summary"
      className="space-y-4 p-4 shadow-none sm:p-5"
    >
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Submitted batch review
          </p>
          <h1
            id="pm-acknowledgement-review-title"
            className="mt-1 text-2xl font-bold tracking-tight text-[var(--text-primary)] sm:text-3xl"
          >
            Review before acknowledgement
          </h1>
          <p className="mt-2 max-w-3xl text-sm text-[var(--text-secondary)]">
            Acknowledgement records receipt/noting of the completed PM form. It
            is not personal witnessing. It does not approve corrective work,
            funding, or an RMRF.
          </p>
        </div>
        <Badge className={formStatusClass(form.status)}>
          {status ? formStatusLabel(status as PreventiveMaintenanceForm['status']) : 'No form status'}
        </Badge>
      </div>
      <dl className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <DetailItem label="Department" value={batch.department ?? form.department ?? ''} />
        <DetailItem label="Asset category" value={formatCategory(batch.assetCategory)} />
        <DetailItem label="PM cycle" value={formatCycle(batch.pmCycle)} />
        <DetailItem label="Form period" value={formatFormPeriod(form)} />
        <DetailItem label="Scheduled" value={formatNumber(batch.scheduled)} />
        <DetailItem label="Inspected" value={formatNumber(batch.inspected)} />
        <DetailItem
          label="Completed on time"
          value={formatNumber(batch.completedOnTime)}
        />
        <DetailItem
          label="On-time compliance"
          value={formatPercent(batch.onTimeCompliancePercent)}
        />
        <DetailItem
          label="Field-work completion"
          value={formatFormDate(batch.fieldWorkCompletedAt)}
        />
        <DetailItem
          label="Submitted timestamp"
          value={formatFormDate(batch.submittedAt ?? form.submittedAt)}
        />
        <DetailItem label="Inspection rows" value={String(form.inspections.length)} />
        <DetailItem label="File number" value={form.fileNumber ?? ''} />
      </dl>
    </Card>
  )
}

function AssetReviewList({
  assets,
  reviewContext,
}: {
  assets: PmPeriodDashboardAssetRowResponse[]
  reviewContext: PmAcknowledgementReviewContext
}) {
  return (
    <Card className="p-4 shadow-none sm:p-5">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-bold text-[var(--text-primary)]">
            Batch asset review
          </h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Review each submitted asset row before recording receipt/noting of
            the completed PM form.
          </p>
        </div>
        <p className="text-xs text-[var(--text-neutral)]">
          {assets.length} {assets.length === 1 ? 'asset' : 'assets'}
        </p>
      </div>
      {assets.length === 0 ? (
        <p className="mt-4 rounded-lg bg-[var(--surface-muted)] p-4 text-sm text-[var(--text-secondary)]">
          No submitted asset rows were returned for this batch.
        </p>
      ) : (
        <div className="mt-4 overflow-x-auto rounded-lg border border-[var(--border-soft)]">
          <table className="w-full min-w-[780px] text-left text-sm">
            <caption className="sr-only">Submitted batch asset review</caption>
            <thead className="border-b border-[var(--border-soft)] bg-[var(--page-background)]">
              <tr>
                {['Asset', 'Location', 'Condition', 'Finding', 'Recommendation', 'Action'].map(
                  (heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="px-3 py-3 font-semibold text-[var(--text-primary)]"
                    >
                      {heading}
                    </th>
                  ),
                )}
              </tr>
            </thead>
            <tbody>
              {assets.map((asset) => (
                <tr
                  key={asset.scheduleId}
                  className="border-b border-[var(--border-soft)] align-top last:border-0"
                >
                  <td className="px-3 py-3">
                    <p className="font-semibold text-[var(--text-primary)]">
                      {asset.assetCode}
                    </p>
                    <p className="mt-1 text-xs text-[var(--text-neutral)]">
                      {formatCategory(asset.assetCategory)}
                    </p>
                  </td>
                  <td className="px-3 py-3 text-[var(--text-secondary)]">
                    {locationValue(asset)}
                  </td>
                  <td className="px-3 py-3 text-[var(--text-secondary)]">
                    {formatCondition(asset.condition)}
                  </td>
                  <td className="max-w-[18rem] px-3 py-3 whitespace-pre-wrap text-[var(--text-secondary)]">
                    {detailValue(asset.remarks)}
                  </td>
                  <td className="max-w-[18rem] px-3 py-3 whitespace-pre-wrap text-[var(--text-secondary)]">
                    {detailValue(asset.actionsRecommendations)}
                  </td>
                  <td className="px-3 py-3">
                    {asset.inspectionId ? (
                      <Link
                        to="/app/inspections/$inspectionId"
                        params={{ inspectionId: asset.inspectionId }}
                        search={{
                          reviewFormId: reviewContext.reviewFormId,
                          department: reviewContext.department,
                          assetCategory: reviewContext.assetCategory,
                          pmCycle: reviewContext.pmCycle,
                        }}
                        className="font-semibold text-[var(--primary)] underline-offset-2 hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
                      >
                        View inspection detail
                      </Link>
                    ) : (
                      <span className="text-xs text-[var(--text-neutral)]">
                        No inspection detail
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}

export function PmAcknowledgementReview({
  formId,
  search,
}: {
  formId: string
  search: PmAcknowledgementReviewSearch
}) {
  const [acknowledgement, setAcknowledgement] = useState(false)
  const currentUser = useCurrentUser()
  const canReview = canReviewPreventiveMaintenanceForms(currentUser.data?.roles)
  const validId = uuidPattern.test(formId)
  const formQuery = usePreventiveMaintenanceForm(formId, canReview && validId)
  const assetCategory = search.assetCategory || formQuery.data?.assetCategory
  const pmCycle = search.pmCycle || formQuery.data?.pmCycle || undefined
  const department = search.department || formQuery.data?.department || undefined
  const dashboardFilters =
    assetCategory && pmCycle
      ? {
          assetCategory,
          pmCycle,
          ...(department ? { department } : {}),
        }
      : undefined
  const dashboardQuery = usePmPeriodDashboard(dashboardFilters)

  if (currentUser.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading review access">
        <span className="sr-only">Loading preventive-maintenance review access...</span>
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-72 w-full" />
      </div>
    )
  }

  if (currentUser.isError || !currentUser.data) {
    return (
      <ReviewError
        title="Review access unavailable"
        message="The signed-in reviewer details could not be loaded."
      />
    )
  }

  if (!canReview) {
    return (
      <ReviewError
        title="Access restricted"
        message="Preventive-maintenance acknowledgement review is available to GSD and Inspector users."
      />
    )
  }

  if (!validId) {
    return (
      <ReviewError
        title="Review not found"
        message="The submitted form link is invalid. No form request was made."
      />
    )
  }

  if (formQuery.isPending || dashboardQuery.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading batch review">
        <span className="sr-only">Loading submitted PM batch review...</span>
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-72 w-full" />
      </div>
    )
  }

  if (formQuery.isError || !formQuery.data) {
    const notFound = formQuery.error instanceof ApiError && formQuery.error.status === 404
    return (
      <ReviewError
        title={notFound ? 'Form not found' : 'Form unavailable'}
        message={
          notFound
            ? 'This submitted preventive-maintenance form may no longer be available.'
            : 'The submitted preventive-maintenance form could not be loaded.'
        }
        retry={notFound ? undefined : () => void formQuery.refetch()}
      />
    )
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <ReviewError
        title="Batch summary unavailable"
        message="The backend batch summary could not be loaded for this form."
        retry={() => void dashboardQuery.refetch()}
      />
    )
  }

  const form = formQuery.data
  const batch = dashboardQuery.data.batches.find(
    (candidate) =>
      candidate.formId === form.id &&
      candidate.assetCategory === assetCategory &&
      candidate.pmCycle === pmCycle &&
      normalizeDepartment(candidate.department) === normalizeDepartment(department),
  )

  if (!batch) {
    return (
      <ReviewError
        title="Batch review unavailable"
        message="The backend did not return a batch linked to this submitted form."
      />
    )
  }

  const isSubmitted = form.status === 'Submitted'
  const reviewContext: PmAcknowledgementReviewContext = {
    reviewFormId: form.id,
    department: batch.department ?? undefined,
    assetCategory: batch.assetCategory,
    pmCycle: batch.pmCycle,
  }

  return (
    <section
      aria-labelledby="pm-acknowledgement-review-title"
      className="max-w-7xl space-y-5"
    >
      <Link
        to="/app/dashboard"
        search={{
          assetCategory: batch.assetCategory,
          pmCycle: batch.pmCycle,
          ...(batch.department ? { department: batch.department } : {}),
        }}
        className="text-sm font-semibold text-[var(--primary)] hover:underline"
      >
        Back to PM dashboard
      </Link>
      <Summary batch={batch} form={form} />
      <div className="flex flex-wrap gap-3">
        <Link
          to="/app/preventive-maintenance-forms/$formId"
          params={{ formId: form.id }}
          search={{
            readonly: true,
            reviewFormId: reviewContext.reviewFormId,
            department: reviewContext.department,
            assetCategory: reviewContext.assetCategory,
            pmCycle: reviewContext.pmCycle,
          }}
          className="inline-flex min-h-10 items-center justify-center rounded-lg bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white hover:bg-[var(--primary-active)] focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
        >
          View full PM form
        </Link>
      </div>
      <AssetReviewList
        assets={dashboardQuery.data.assets}
        reviewContext={reviewContext}
      />
      {isSubmitted && !acknowledgement ? (
        <AcknowledgeForm
          formId={form.id}
          title="Acknowledge whole PM batch"
          onAcknowledged={() => setAcknowledgement(true)}
        />
      ) : acknowledgement ? (
        <Card className="shadow-none" role="status">
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Acknowledgement recorded
          </p>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            Receipt/noting of this completed PM form is recorded. This does not
            approve corrective work, funding, or an RMRF.
          </p>
        </Card>
      ) : (
        <Card className="shadow-none" role="status">
          <p className="font-semibold text-[var(--text-primary)]">
            {formStatusLabel(form.status)}
          </p>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            This review is read-only because the form is no longer awaiting
            acknowledgement.
          </p>
        </Card>
      )}
    </section>
  )
}
