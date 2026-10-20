import { useEffect, useMemo, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { ApiError } from '@/api/problem-details'
import type {
  ListPreventiveMaintenanceFormsParams,
  ScheduleAssigneeOption,
} from '@/api/generated/models'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { Badge } from '@/components/ui/badge'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  SearchableOptionInput,
  type SearchableOption,
} from '@/components/ui/searchable-option-input'
import { Skeleton } from '@/components/ui/skeleton'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  canReviewPreventiveMaintenanceForms,
  isGsdRole,
  preventiveMaintenanceFormStatusCodes,
  type PreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-contract'
import { usePreventiveMaintenanceForms } from '@/features/preventive-maintenance-forms/form-queries'
import { RegistryResultsPanel } from '@/features/shared/registry-results-panel'
import { RegistryZoomControls } from '@/features/shared/registry-zoom'
import { useRegistryZoom } from '@/features/shared/registry-zoom-state'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'
import { useScheduleSupervisorAssignmentOptions } from '@/features/schedules/schedule-queries'
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
  supervisor?: string | undefined
  page?: number | undefined
}

const emptyForms: PreventiveMaintenanceForm[] = []

type FormRegistryProps = {
  search: FormSearch
  onSearchChange: (
    next: FormSearch,
    options?: { replace?: boolean; preserveScroll?: boolean },
  ) => void
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

type SupervisorOption = { id: string; label: string }

function toSupervisorOptions(
  options: ScheduleAssigneeOption[],
  selected?: ScheduleAssigneeOption | null,
): SupervisorOption[] {
  const visibleOptions =
    selected && !options.some(({ id }) => id === selected.id)
      ? [...options, selected]
      : options
  const nameCounts = new Map<string, number>()
  for (const { displayName } of visibleOptions) {
    nameCounts.set(displayName, (nameCounts.get(displayName) ?? 0) + 1)
  }

  return visibleOptions
    .map(({ id, displayName }) => ({
      id,
      label:
        nameCounts.get(displayName) === 1
          ? displayName
          : `${displayName} · …${id.slice(-4)}`,
    }))
    .sort((left, right) => left.label.localeCompare(right.label))
}

function currentScheduleAssignmentSummary(
  form: PreventiveMaintenanceForm,
  role: 'Inspector' | 'Supervisor',
) {
  const assignments = new Map<string, string>()
  let unavailableCount = 0

  for (const row of form.inspections) {
    const id =
      role === 'Supervisor'
        ? row.currentScheduleSupervisorUserId
        : row.currentScheduleInspectorUserId
    const identity =
      role === 'Supervisor'
        ? row.currentScheduleSupervisorIdentity
        : row.currentScheduleInspectorIdentity

    if (id === undefined) {
      unavailableCount += 1
    } else if (id === null) {
      assignments.set('unassigned', 'Unassigned')
    } else {
      assignments.set(id, identity?.trim() || `Assigned ${role}`)
    }
  }

  const nameCounts = new Map<string, number>()
  for (const name of assignments.values()) {
    nameCounts.set(name, (nameCounts.get(name) ?? 0) + 1)
  }
  const labels = [...assignments.entries()].map(([id, name]) =>
    nameCounts.get(name) === 1 || id === 'unassigned'
      ? name
      : `${name} · …${id.slice(-4)}`,
  )

  if (unavailableCount > 0) {
    labels.push(`${unavailableCount} not recorded`)
  }

  return labels.length > 0 ? labels.join('; ') : 'Not recorded'
}

function FormSummary({
  form,
  search,
}: {
  form: PreventiveMaintenanceForm
  search: FormSearch
}) {
  const cycleLabel = form.pmCycle
    ? formatPmCycle(form.pmCycle)
    : formatFormPeriod(form) || 'Cycle not recorded'
  const draftTitle = [
    formatAssetCategory(form.assetCategory),
    form.department || 'Department not recorded',
    cycleLabel,
  ].join(' · ')
  const detailSearch = {
    returnContext: Object.values(search).some((value) => value !== undefined)
      ? { kind: 'formRegistry' as const, search }
      : { kind: 'formRegistry' as const },
  }

  return (
    <article className="content-card rounded-lg border border-[var(--border-soft)] bg-white p-3 sm:px-4">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <Link
            to="/app/preventive-maintenance-forms/$formId"
            params={{ formId: form.id }}
            search={detailSearch}
            className="inline-block font-semibold break-words text-[var(--primary)] hover:underline focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
          >
            {form.fileNumber ??
              (form.status === 'Draft' ? draftTitle : 'Unsubmitted form')}
          </Link>
        </div>
        <FormStatus status={form.status} />
      </div>
      <dl className="mt-3 grid gap-x-4 gap-y-2 border-t border-[var(--border-soft)] pt-3 text-sm sm:grid-cols-2 lg:grid-cols-5">
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Asset category
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {formatAssetCategory(form.assetCategory)}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Building / department
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {[form.building, form.department].filter(Boolean).join(' / ') ||
              'Not recorded'}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Period
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">{cycleLabel}</dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Inspection rows
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {form.inspections.length}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Submitted
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {formatFormDate(form.submittedAt)}
          </dd>
        </div>
      </dl>
      <dl className="mt-3 grid gap-x-4 gap-y-2 border-t border-[var(--border-soft)] pt-3 text-sm sm:grid-cols-2">
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Current supervisor assignment
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {currentScheduleAssignmentSummary(form, 'Supervisor')}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Current inspector assignment
          </dt>
          <dd className="mt-1 text-[var(--text-primary)]">
            {currentScheduleAssignmentSummary(form, 'Inspector')}
          </dd>
        </div>
      </dl>
      <p className="mt-2 text-xs text-[var(--text-secondary)]">
        These are current schedule assignments. They can differ from the person
        who completed an inspection.
      </p>
      <div className="mt-2 flex justify-end border-t border-[var(--border-soft)] pt-2">
        <Link
          to="/app/preventive-maintenance-forms/$formId"
          params={{ formId: form.id }}
          search={detailSearch}
          className="inline-flex min-h-8 items-center text-sm font-semibold text-[var(--primary)] hover:underline focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
        >
          View form details{' '}
          <span aria-hidden="true" className="ml-1">
            →
          </span>
        </Link>
      </div>
    </article>
  )
}

function FormRegistryFilters({
  search,
  categories,
  departments,
  cycles,
  isGsd,
  optionsPending,
  optionsError,
  formsError,
  onSearchChange,
  onRefreshForms,
  onRetryOptions,
}: {
  search: FormSearch
  categories: string[]
  departments: string[]
  cycles: string[]
  isGsd: boolean
  optionsPending: boolean
  optionsError: boolean
  formsError: boolean
  onSearchChange: FormRegistryProps['onSearchChange']
  onRefreshForms: () => void
  onRetryOptions: () => void
}) {
  const [draft, setDraft] = useState<FormSearch>(() => ({
    status: search.status,
    assetCategory: search.assetCategory,
    department: search.department,
    pmCycle: search.pmCycle,
    search: search.search,
    supervisor: search.supervisor,
  }))
  const [supervisorSearch, setSupervisorSearch] = useState<string | null>(null)
  const selectedSupervisorId =
    draft.supervisor && draft.supervisor !== 'unassigned'
      ? draft.supervisor
      : undefined
  const supervisorOptionsQuery = useScheduleSupervisorAssignmentOptions(
    isGsd,
    supervisorSearch ?? '',
    selectedSupervisorId,
  )
  const supervisors = useMemo(
    () =>
      toSupervisorOptions(
        supervisorOptionsQuery.data?.supervisors ?? [],
        supervisorOptionsQuery.data?.selected,
      ),
    [
      supervisorOptionsQuery.data?.selected,
      supervisorOptionsQuery.data?.supervisors,
    ],
  )
  const selectedSupervisorLabel =
    draft.supervisor === 'unassigned'
      ? 'Unassigned'
      : supervisors.find(({ id }) => id === draft.supervisor)?.label
  const supervisorInputValue = supervisorSearch ?? selectedSupervisorLabel ?? ''

  const supervisorPickerOptions: SearchableOption[] = [
    { value: '', label: 'All supervisors' },
    { value: 'unassigned', label: 'Unassigned' },
    ...supervisors.map(({ id, label }) => ({ value: id, label })),
  ]
  const apply = () =>
    onSearchChange(
      {
        status: draft.status || undefined,
        assetCategory: draft.assetCategory || undefined,
        department: draft.department?.trim() || undefined,
        pmCycle: draft.pmCycle || undefined,
        search: draft.search?.trim() || undefined,
        supervisor: draft.supervisor || undefined,
        page: 1,
      },
      { preserveScroll: true },
    )
  const clear = () => {
    setDraft({})
    setSupervisorSearch(null)
    onSearchChange({ page: 1 }, { preserveScroll: true })
  }

  return (
    <section aria-labelledby="find-forms-title" className="space-y-3">
      <h2
        id="find-forms-title"
        className="text-xl font-semibold text-[var(--text-primary)]"
      >
        Find forms
      </h2>
      <Card className="content-card p-3">
        <form
          className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)] md:col-span-2 xl:col-span-4">
            Search forms
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
              className="min-h-10 text-sm"
            />
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Form status
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
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Asset category
            <select
              aria-label="Asset category"
              value={draft.assetCategory ?? ''}
              disabled={optionsPending && categories.length === 0}
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
                    {formatAssetCategory(category)}
                  </option>
                ))}
            </select>
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            Department
            <select
              aria-label="Department"
              value={draft.department ?? ''}
              disabled={optionsPending && departments.length === 0}
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
          </label>
          <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
            PM cycle
            <select
              aria-label="PM cycle"
              value={draft.pmCycle ?? ''}
              disabled={optionsPending && cycles.length === 0}
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
          </label>
          {isGsd && (
            <SearchableOptionInput
              id="supervisor-filter"
              label="Supervisor filter"
              value={supervisorInputValue}
              options={supervisorPickerOptions}
              placeholder="Type a supervisor name"
              helperText="Choose a suggestion to filter by account. Use All supervisors or Unassigned when needed."
              onChange={(value) => {
                setSupervisorSearch(value)
                const option = supervisorPickerOptions.find(
                  (candidate) =>
                    candidate.label.toLocaleLowerCase() ===
                    value.trim().toLocaleLowerCase(),
                )
                setDraft((current) => ({
                  ...current,
                  supervisor: option?.value || undefined,
                }))
              }}
            />
          )}
          {isGsd && supervisorOptionsQuery.isError && (
            <Button
              type="button"
              variant="secondary"
              onClick={() => void supervisorOptionsQuery.refetch()}
            >
              Retry supervisor suggestions
            </Button>
          )}
          <div className="flex flex-wrap gap-2 md:col-span-2 xl:col-span-4">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
            <Button type="button" variant="secondary" onClick={onRefreshForms}>
              Refresh forms
            </Button>
          </div>
        </form>
        {(search.status ||
          search.assetCategory ||
          search.department ||
          search.pmCycle ||
          search.search ||
          search.supervisor) && (
          <p
            className="mt-4 text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            Active filters:{' '}
            {[
              search.status
                ? formStatusLabel(
                    search.status as PreventiveMaintenanceForm['status'],
                  )
                : undefined,
              search.assetCategory
                ? formatAssetCategory(search.assetCategory)
                : undefined,
              search.department,
              search.pmCycle ? formatPmCycle(search.pmCycle) : undefined,
              search.search,
              search.supervisor === 'unassigned'
                ? 'Unassigned supervisor'
                : (supervisors.find(({ id }) => id === search.supervisor)
                    ?.label ??
                  (search.supervisor ? 'Supervisor assignment' : undefined)),
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
        )}
        {formsError && (
          <div
            className="mt-3 flex flex-wrap items-center gap-3 text-sm text-[var(--error)]"
            role="alert"
          >
            <span>Preventive-maintenance forms could not be loaded.</span>
            <Button type="button" onClick={onRefreshForms}>
              Retry
            </Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
          </div>
        )}
        {optionsError && (
          <div
            className="mt-3 flex items-center gap-3 text-sm text-[var(--error)]"
            role="alert"
          >
            <span>Filter options could not be loaded.</span>
            <Button type="button" onClick={onRetryOptions}>
              Retry options
            </Button>
          </div>
        )}
      </Card>
    </section>
  )
}

export function FormRegistry({ search, onSearchChange }: FormRegistryProps) {
  const currentUser = useCurrentUser()
  const recordZoom = useRegistryZoom()
  const canReview = canReviewPreventiveMaintenanceForms(currentUser.data?.roles)
  const canFilterSupervisor = isGsdRole(currentUser.data?.roles)
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
  const responseRows = forms.data ?? emptyForms
  const optionRows = allForms.data ?? responseRows
  const rows = useMemo(() => {
    if (!search.supervisor) return responseRows
    return responseRows.filter((form) =>
      form.inspections.some((row) =>
        search.supervisor === 'unassigned'
          ? row.currentScheduleSupervisorUserId === null
          : row.currentScheduleSupervisorUserId === search.supervisor,
      ),
    )
  }, [responseRows, search.supervisor])
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
    <section aria-labelledby="forms-title" className="space-y-4">
      <PageHeading
        id="forms-title"
        eyebrow="Preventive maintenance"
        title="Form review"
        description="Review submitted forms and open their inspection source rows."
      />
      <FormRegistryFilters
        key={JSON.stringify([
          search.status,
          search.assetCategory,
          search.department,
          search.pmCycle,
          search.search,
          search.supervisor,
        ])}
        search={search}
        categories={categories}
        departments={departments}
        cycles={cycles}
        isGsd={canFilterSupervisor}
        optionsPending={allForms.isPending}
        optionsError={allForms.isError}
        formsError={forms.isError}
        onSearchChange={onSearchChange}
        onRefreshForms={() => void forms.refetch()}
        onRetryOptions={() => void allForms.refetch()}
      />
      <section aria-labelledby="form-records-title" className="space-y-4">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2
              id="form-records-title"
              className="text-xl font-semibold text-[var(--text-primary)]"
            >
              Form records
            </h2>
            <p
              className="mt-1 text-sm text-[var(--text-secondary)]"
              aria-live="polite"
            >
              Filtered results: {rows.length.toLocaleString()}
            </p>
          </div>
          <RegistryZoomControls
            label="Form records"
            zoom={recordZoom.zoom}
            onDecrease={recordZoom.decrease}
            onIncrease={recordZoom.increase}
            onReset={recordZoom.reset}
          />
        </header>
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
            desktopLabel="Preventive-maintenance form records"
            desktopFrameClassName="border-0 bg-transparent"
            breakpoint="md"
            viewportSize="standard"
            isUpdating={forms.isFetching}
            recordsZoom={recordZoom.zoom}
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
    </section>
  )
}
