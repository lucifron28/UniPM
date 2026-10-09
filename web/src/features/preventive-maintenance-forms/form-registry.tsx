import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import type { ListPreventiveMaintenanceFormsParams } from '@/api/generated/models'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  preventiveMaintenanceFormStatusCodes,
  type PreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-contract'
import { usePreventiveMaintenanceForms } from '@/features/preventive-maintenance-forms/form-queries'
import { RegistryResultsPanel } from '@/features/shared/registry-results-panel'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'
import {
  formStatusClass,
  formStatusLabel,
  formatFormDate,
  formatFormPeriod,
} from '@/features/preventive-maintenance-forms/form-presentation'

export type FormSearch = {
  status?: ListPreventiveMaintenanceFormsParams['status'] | undefined
  assetCategory?:
    ListPreventiveMaintenanceFormsParams['assetCategory'] | undefined
  department?: ListPreventiveMaintenanceFormsParams['department'] | undefined
  pmCycle?: ListPreventiveMaintenanceFormsParams['pmCycle'] | undefined
  search?: ListPreventiveMaintenanceFormsParams['search'] | undefined
  page?: number | undefined
}

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

function FormSummary({
  form,
  search,
}: {
  form: PreventiveMaintenanceForm
  search: FormSearch
}) {
  return (
    <article className="rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-sm">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
        <div>
          <Link
            to="/app/preventive-maintenance-forms/$formId"
            params={{ formId: form.id }}
            search={{
              returnContext: Object.values(search).some(
                (value) => value !== undefined,
              )
                ? { kind: 'formRegistry', search }
                : { kind: 'formRegistry' },
            }}
            className="font-semibold text-[var(--primary)] hover:underline"
          >
            {form.fileNumber ?? 'Unsubmitted form'}
          </Link>
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

export function FormRegistry({
  search,
  onSearchChange,
}: {
  search: FormSearch
  onSearchChange: (
    next: FormSearch,
    options?: { replace?: boolean; preserveScroll?: boolean },
  ) => void
}) {
  const currentUser = useCurrentUser()
  const canReview = canReviewPreventiveMaintenanceForms(currentUser.data?.roles)
  const filters = useMemo(
    () => ({
      ...(search.status ? { status: search.status } : {}),
      ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
      ...(search.department ? { department: search.department } : {}),
      ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
      ...(search.search ? { search: search.search } : {}),
    }),
    [
      search.status,
      search.assetCategory,
      search.department,
      search.pmCycle,
      search.search,
    ],
  )
  const allForms = usePreventiveMaintenanceForms({}, canReview)
  const forms = usePreventiveMaintenanceForms(filters, canReview)
  const [draft, setDraft] = useState<FormSearch>(() => ({
    status: search.status,
    assetCategory: search.assetCategory,
    department: search.department,
    pmCycle: search.pmCycle,
    search: search.search,
  }))
  const rows = forms.data ?? []
  const optionRows = allForms.data ?? rows
  const categories = useMemo(
    () => [...new Set(optionRows.map((form) => form.assetCategory))].sort(),
    [optionRows],
  )
  const departments = useMemo(
    () =>
      [
        ...new Set(
          optionRows
            .map((form) => form.department)
            .filter((value): value is string => Boolean(value)),
        ),
      ].sort(),
    [optionRows],
  )
  const cycles = useMemo(
    () =>
      [
        ...new Set(
          optionRows
            .map((form) => form.pmCycle)
            .filter((value): value is string => Boolean(value)),
        ),
      ].sort((left, right) => right.localeCompare(left)),
    [optionRows],
  )
  const pageSize = 10
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(Math.max(requestedPage, 1), pageCount)
  const pageData = useMemo(
    () => rows.slice((page - 1) * pageSize, page * pageSize),
    [page, rows],
  )

  useEffect(() => {
    setDraft({
      status: search.status,
      assetCategory: search.assetCategory,
      department: search.department,
      pmCycle: search.pmCycle,
      search: search.search,
    })
  }, [
    search.status,
    search.assetCategory,
    search.department,
    search.pmCycle,
    search.search,
  ])

  useEffect(() => {
    if (
      forms.isSuccess &&
      !forms.isPlaceholderData &&
      search.page &&
      search.page > pageCount
    ) {
      onSearchChange(
        { ...search, page: pageCount > 1 ? pageCount : undefined },
        { replace: true, preserveScroll: true },
      )
    }
  }, [
    forms.isSuccess,
    forms.isPlaceholderData,
    onSearchChange,
    pageCount,
    search,
  ])

  const apply = () =>
    onSearchChange(
      {
        status: draft.status || undefined,
        assetCategory: draft.assetCategory || undefined,
        department: draft.department?.trim() || undefined,
        pmCycle: draft.pmCycle || undefined,
        search: draft.search?.trim() || undefined,
        page: 1,
      },
      { preserveScroll: true },
    )
  const clear = () => {
    setDraft({})
    onSearchChange({ page: 1 }, { preserveScroll: true })
  }
  const changePage = (nextPage: number) => {
    onSearchChange(
      { ...search, page: nextPage > 1 ? nextPage : undefined },
      { preserveScroll: true },
    )
  }

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
        title="Access denied"
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

  const formsForbidden =
    forms.isError &&
    forms.error instanceof ApiError &&
    forms.error.status === 403
  if (formsForbidden) {
    return (
      <AccessState
        title="Access denied"
        message="Your account is not permitted to review preventive-maintenance forms."
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
      <Card className="p-4 shadow-none">
        <form
          className="grid gap-3 md:grid-cols-2 xl:grid-cols-5"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <Input
            aria-label="Search forms"
            placeholder="File number, building, or asset details"
            value={draft.search ?? ''}
            maxLength={256}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                search: event.target.value,
              }))
            }
            className="xl:col-span-2"
          />
          <select
            aria-label="Form status"
            value={draft.status ?? ''}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                status: (event.target.value || undefined) as
                  PreventiveMaintenanceForm['status'] | undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All statuses</option>
            {preventiveMaintenanceFormStatusCodes.map((status) => (
              <option key={status} value={status}>
                {formStatusLabel(status)}
              </option>
            ))}
          </select>
          <select
            aria-label="Asset category"
            value={draft.assetCategory ?? ''}
            disabled={allForms.isPending && categories.length === 0}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                assetCategory: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All asset categories</option>
            {assetCategoryCodes
              .filter((category) => categories.includes(category))
              .map((category) => (
                <option key={category} value={category}>
                  {category.replaceAll('-', ' ')}
                </option>
              ))}
          </select>
          <select
            aria-label="Department"
            value={draft.department ?? ''}
            disabled={allForms.isPending && departments.length === 0}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                department: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All departments</option>
            {departments.map((department) => (
              <option key={department} value={department}>
                {department}
              </option>
            ))}
          </select>
          <select
            aria-label="PM cycle"
            value={draft.pmCycle ?? ''}
            disabled={allForms.isPending && cycles.length === 0}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                pmCycle: event.target.value || undefined,
              }))
            }
            className="min-h-10 rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm"
          >
            <option value="">All PM cycles</option>
            {cycles.map((cycle) => (
              <option key={cycle} value={cycle}>
                {formatPmCycle(cycle)}
              </option>
            ))}
          </select>
          <div className="flex flex-wrap gap-2 xl:col-span-5">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => void forms.refetch()}
            >
              Refresh forms
            </Button>
          </div>
        </form>
        {(search.status ||
          search.assetCategory ||
          search.department ||
          search.pmCycle ||
          search.search) && (
          <p
            className="mt-4 text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            Active filters:{' '}
            {[
              search.status,
              search.assetCategory,
              search.department,
              search.pmCycle,
              search.search,
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
        )}
        {forms.isError && (
          <div
            className="mt-3 flex flex-wrap items-center gap-3 text-sm text-[var(--error)]"
            role="alert"
          >
            <span>Preventive-maintenance forms could not be loaded.</span>
            <Button type="button" onClick={() => void forms.refetch()}>
              Retry
            </Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
          </div>
        )}
        {allForms.isError && (
          <div
            className="mt-3 flex items-center gap-3 text-sm text-[var(--error)]"
            role="alert"
          >
            <span>Filter options could not be loaded.</span>
            <Button type="button" onClick={() => void allForms.refetch()}>
              Retry options
            </Button>
          </div>
        )}
      </Card>
      {rows.length === 0 && (
        <p
          className="min-h-5 text-sm text-[var(--text-neutral)]"
          role={forms.isFetching ? 'status' : undefined}
          aria-live="polite"
        >
          {forms.isFetching ? 'Updating results...' : null}
        </p>
      )}
      {forms.isSuccess && rows.length === 0 ? (
        <Card className="text-center shadow-none">
          <h2 className="text-lg font-semibold text-[var(--text-primary)]">
            {allForms.isSuccess && allForms.data.length === 0
              ? 'No preventive-maintenance forms found.'
              : 'No forms match these filters.'}
          </h2>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            {allForms.isSuccess && allForms.data.length === 0
              ? 'Forms will appear here after they are created through the field workflow.'
              : 'Clear the filters to return to all recorded forms.'}
          </p>
        </Card>
      ) : rows.length > 0 ? (
        <RegistryResultsPanel
          label="Preventive-maintenance forms"
          breakpoint="md"
          viewportSize="standard"
          isUpdating={forms.isFetching}
          desktopContent={
            <div className="space-y-3 p-1" role="list">
              {pageData.map((form) => (
                <FormSummary key={form.id} form={form} search={search} />
              ))}
            </div>
          }
          mobileContent={pageData.map((form) => (
            <FormSummary key={form.id} form={form} search={search} />
          ))}
          pagination={{
            page,
            pageSize,
            total: rows.length,
            onPageChange: changePage,
          }}
        />
      ) : null}
    </section>
  )
}
