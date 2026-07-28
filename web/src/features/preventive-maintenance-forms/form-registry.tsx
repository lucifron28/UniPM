import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  type PreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-contract'
import { usePreventiveMaintenanceForms } from '@/features/preventive-maintenance-forms/form-queries'
import {
  formStatusClass,
  formStatusLabel,
  formatFormDate,
  formatFormPeriod,
} from '@/features/preventive-maintenance-forms/form-presentation'

function AccessState({ title, message }: { title: string; message: string }) {
  return (
    <Card role="alert" className="border-[var(--warning)] shadow-none">
      <h1 className="text-xl font-bold text-[var(--text-primary)]">{title}</h1>
      <p className="mt-2 text-sm text-[var(--text-secondary)]">{message}</p>
    </Card>
  )
}

function FormStatus({
  status,
}: {
  status: PreventiveMaintenanceForm['status']
}) {
  return (
    <Badge className={formStatusClass(status)}>{formStatusLabel(status)}</Badge>
  )
}

function FormSummary({ form }: { form: PreventiveMaintenanceForm }) {
  return (
    <article className="rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-sm">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div>
          <Link
            to="/app/preventive-maintenance-forms/$formId"
            params={{ formId: form.id }}
            className="font-semibold text-[var(--primary)] hover:underline"
          >
            {form.fileNumber ?? 'Unsubmitted form'}
          </Link>
          <p className="mt-1 text-xs break-all text-[var(--text-neutral)]">
            {form.id}
          </p>
        </div>
        <FormStatus status={form.status} />
      </div>
      <dl className="mt-5 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Asset category
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {form.assetCategory}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Building / department
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {[form.building, form.department].filter(Boolean).join(' / ') ||
              'Not recorded'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Period
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {formatFormPeriod(form)}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Inspection rows
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {form.inspections.length}
          </dd>
        </div>
      </dl>
      <p className="mt-4 text-xs text-[var(--text-neutral)]">
        Submitted: {formatFormDate(form.submittedAt)}
      </p>
    </article>
  )
}

export function FormRegistry() {
  const currentUser = useCurrentUser()
  const canReview = canReviewPreventiveMaintenanceForms(currentUser.data?.roles)
  const forms = usePreventiveMaintenanceForms(canReview)

  if (currentUser.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading user access">
        <span className="sr-only">
          Loading preventive-maintenance access...
        </span>
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-28 w-full" />
      </div>
    )
  }

  if (currentUser.isError || !currentUser.data) {
    return (
      <AccessState
        title="Form access unavailable"
        message="Your signed-in user details could not be loaded. Please try again from the authenticated portal."
      />
    )
  }

  if (!canReview) {
    return (
      <AccessState
        title="Access restricted"
        message="Preventive-maintenance form review is available to GSD and Inspector users."
      />
    )
  }

  if (forms.isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Loading forms">
        <span className="sr-only">Loading preventive-maintenance forms...</span>
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-28 w-full" />
      </div>
    )
  }

  if (forms.isError) {
    const forbidden =
      forms.error instanceof ApiError && forms.error.status === 403
    return (
      <AccessState
        title={forbidden ? 'Access restricted' : 'Forms unavailable'}
        message={
          forbidden
            ? 'Your account is not permitted to review preventive-maintenance forms.'
            : 'Preventive-maintenance forms could not be loaded.'
        }
      />
    )
  }

  return (
    <section aria-labelledby="forms-title" className="max-w-6xl space-y-6">
      <div>
        <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
          Preventive maintenance
        </p>
        <h1
          id="forms-title"
          className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
        >
          Form review
        </h1>
        <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
          Review submitted preventive-maintenance forms and their inspection
          source rows. Field workflow actions remain outside this web module.
        </p>
      </div>
      {forms.data.length === 0 ? (
        <Card className="text-center shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            No preventive-maintenance forms found.
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            Forms will appear here after they are created through the field
            workflow.
          </p>
        </Card>
      ) : (
        <div
          className="space-y-3"
          role="list"
          aria-label="Preventive-maintenance forms"
        >
          {forms.data.map((form) => (
            <FormSummary key={form.id} form={form} />
          ))}
        </div>
      )}
      {forms.isRefetching && (
        <p className="text-xs text-[var(--text-neutral)]" role="status">
          Refreshing forms...
        </p>
      )}
      <Button type="button" onClick={() => void forms.refetch()}>
        Refresh forms
      </Button>
    </section>
  )
}
