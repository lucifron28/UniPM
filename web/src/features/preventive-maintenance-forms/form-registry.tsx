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
  canAcknowledgePreventiveMaintenanceForms,
  canReviewPreventiveMaintenanceForms,
  isGsdRole,
  preventiveMaintenanceFormStatusCodes,
  type PreventiveMaintenanceForm,
} from '@/features/preventive-maintenance-forms/form-contract'
import {
  usePreventiveMaintenanceFormOptions,
  usePreventiveMaintenanceForms,
} from '@/features/preventive-maintenance-forms/form-queries'
import { formatPmCycle } from '@/features/schedules/schedule-presentation'
import { useScheduleSupervisorAssignmentOptions } from '@/features/schedules/schedule-queries'
import {
  formStatusClass,
  formStatusLabel,
  formatFormPeriod,
} from '@/features/preventive-maintenance-forms/form-presentation'

export type FormSearch = {
  attention?: 'awaitingAcknowledgement' | undefined
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

function formIdentifier(form: PreventiveMaintenanceForm) {
  return form.fileNumber?.trim() || formatAssetCategory(form.assetCategory)
}

function formDetailSearch(search: FormSearch) {
  return {
    returnContext: Object.values(search).some((value) => value !== undefined)
      ? { kind: 'formRegistry' as const, search }
      : { kind: 'formRegistry' as const },
  }
}

function FormTableRow({
  form,
  search,
}: {
  form: PreventiveMaintenanceForm
  search: FormSearch
}) {
  const cycleLabel = form.pmCycle
    ? formatPmCycle(form.pmCycle)
    : formatFormPeriod(form) || 'Not recorded'
  const identifier = formIdentifier(form)
  const detailSearch = formDetailSearch(search)

  return (
    <tr
      tabIndex={0}
      aria-label={'Open form ' + identifier}
      className="h-16 cursor-pointer border-b border-[var(--border-soft)] text-sm text-[var(--text-primary)] transition-colors last:border-b-0 hover:bg-[var(--surface-subtle)] focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[var(--primary)]"
      onClick={(event) => {
        if (
          event.target instanceof Element &&
          event.target.closest('a, button, input, select, textarea')
        ) {
          return
        }
        event.currentTarget
          .querySelector<HTMLAnchorElement>('[data-form-identifier]')
          ?.click()
      }}
      onKeyDown={(event) => {
        if (event.target !== event.currentTarget) return
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          event.currentTarget
            .querySelector<HTMLAnchorElement>('[data-form-identifier]')
            ?.click()
        }
      }}
    >
      <th scope="row" className="max-w-0 px-4 py-3 text-left font-semibold">
        <Link
          data-form-identifier
          to="/app/preventive-maintenance-forms/$formId"
          params={{ formId: form.id }}
          search={detailSearch}
          className="block truncate text-[var(--primary)] hover:underline focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
          title={identifier}
        >
          {identifier}
        </Link>
      </th>
      <td className="max-w-0 px-4 py-3">
        <span
          className="block truncate"
          title={formatAssetCategory(form.assetCategory)}
        >
          {formatAssetCategory(form.assetCategory)}
        </span>
      </td>
      <td className="max-w-0 px-4 py-3">
        <span
          className="block truncate"
          title={form.department || 'Not recorded'}
        >
          {form.department || 'Not recorded'}
        </span>
      </td>
      <td className="max-w-0 px-4 py-3">
        <span className="block truncate" title={cycleLabel}>
          {cycleLabel}
        </span>
      </td>
      <td className="px-4 py-3 text-center">{form.inspections.length}</td>
      <td className="px-4 py-3">
        <FormStatus status={form.status} />
      </td>
      <td className="px-4 py-3 text-right">
        <Link
          to="/app/preventive-maintenance-forms/$formId"
          params={{ formId: form.id }}
          search={detailSearch}
          className="inline-flex min-h-8 items-center font-semibold text-[var(--primary)] hover:underline focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]"
        >
          View details
        </Link>
      </td>
    </tr>
  )
}

function FormMobileRow({
  form,
  search,
}: {
  form: PreventiveMaintenanceForm
  search: FormSearch
}) {
  const cycleLabel = form.pmCycle
    ? formatPmCycle(form.pmCycle)
    : formatFormPeriod(form) || 'Not recorded'
  const identifier = formIdentifier(form)
  const category = formatAssetCategory(form.assetCategory)
  const department = form.department || 'Department not recorded'
  const hasFileNumber = Boolean(form.fileNumber?.trim())

  return (
    <Link
      to="/app/preventive-maintenance-forms/$formId"
      params={{ formId: form.id }}
      search={formDetailSearch(search)}
      className="block border-b border-[var(--border-soft)] px-3 py-3 text-sm last:border-b-0 hover:bg-[var(--surface-subtle)] focus-visible:relative focus-visible:z-10 focus-visible:outline-2 focus-visible:outline-[var(--primary)]"
    >
      <div className="flex items-center justify-between gap-3">
        <span className="min-w-0 truncate font-semibold text-[var(--primary)]">
          {identifier}
        </span>
        <FormStatus status={form.status} />
      </div>
      <p className="mt-1 truncate text-[var(--text-secondary)]">
        {hasFileNumber ? category + ' · ' + department : department}
      </p>
      <p className="mt-1 flex items-center justify-between gap-3 text-xs text-[var(--text-neutral)]">
        <span>{cycleLabel}</span>
        <span>
          {form.inspections.length}{' '}
          {form.inspections.length === 1 ? 'inspection' : 'inspections'}
        </span>
      </p>
    </Link>
  )
}

function FormPagination({
  page,
  pageSize,
  total,
  onPageChange,
}: {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const first = (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, total)

  return (
    <nav
      aria-label="Form review pagination"
      className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border-soft)] pt-3 text-sm"
    >
      <p className="text-[var(--text-secondary)]">
        Showing {first}–{last} of {total}
      </p>
      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="secondary"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>
        <span aria-live="polite" className="text-[var(--text-secondary)]">
          Page {page} of {pageCount}
        </span>
        <Button
          type="button"
          variant="secondary"
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </nav>
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
  onRetryForms,
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
  onRetryForms: () => void
  onRetryOptions: () => void
}) {
  const [draft, setDraft] = useState<FormSearch>(() => ({
    attention: search.attention,
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
        attention: search.attention,
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
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            apply()
          }}
        >
          <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_16rem]">
            <label className="grid gap-1 text-sm font-semibold text-[var(--text-primary)]">
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
          </div>
          <details className="rounded-lg border border-[var(--border-soft)] px-3">
            <summary className="cursor-pointer py-2 text-sm font-semibold text-[var(--text-primary)] focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]">
              More filters
            </summary>
            <div className="grid gap-3 border-t border-[var(--border-soft)] py-3 md:grid-cols-2 xl:grid-cols-4">
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
            </div>
          </details>
          <div className="flex flex-wrap gap-2">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={clear}>
              Clear filters
            </Button>
          </div>
        </form>
        {(search.attention ||
          search.status ||
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
              search.attention
                ? 'Unacknowledged non-operational findings'
                : undefined,
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
            <Button type="button" onClick={onRetryForms}>
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
  const currentRoles = currentUser.data?.roles
  const canReview = canReviewPreventiveMaintenanceForms(currentRoles)
  const isReadOnlySupervisor =
    currentRoles?.includes('Supervisor') === true &&
    !canAcknowledgePreventiveMaintenanceForms(currentRoles)
  const canFilterSupervisor = isGsdRole(currentRoles)
  const pageSize = 10
  const filters = useMemo(
    () => ({
      ...(search.attention ? { attention: search.attention } : {}),
      ...(search.status ? { status: search.status } : {}),
      ...(search.assetCategory ? { assetCategory: search.assetCategory } : {}),
      ...(search.department ? { department: search.department } : {}),
      ...(search.pmCycle ? { pmCycle: search.pmCycle } : {}),
      ...(search.search ? { search: search.search } : {}),
      ...(search.supervisor ? { supervisor: search.supervisor } : {}),
      page: search.page ?? 1,
      pageSize,
    }),
    [
      search.attention,
      search.status,
      search.assetCategory,
      search.department,
      search.pmCycle,
      search.search,
      search.supervisor,
      search.page,
    ],
  )
  const options = usePreventiveMaintenanceFormOptions(canReview)
  const forms = usePreventiveMaintenanceForms(filters, canReview)
  const rows = forms.data?.items ?? emptyForms
  const categories = options.data?.categories ?? []
  const departments = options.data?.departments ?? []
  const cycles = useMemo(
    () =>
      [...(options.data?.pmCycles ?? [])].sort((left, right) =>
        right.localeCompare(left),
      ),
    [options.data?.pmCycles],
  )
  const totalCount = forms.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const requestedPage = search.page ?? 1
  const page = Math.min(
    Math.max(forms.data?.page ?? requestedPage, 1),
    pageCount,
  )
  const pageData = rows

  useEffect(() => {
    if (
      forms.isSuccess &&
      !forms.isPlaceholderData &&
      requestedPage > pageCount
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
    requestedPage,
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
        message="Preventive-maintenance form review is available to GSD, Inspector, and Supervisor users."
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
    <section aria-labelledby="forms-title" className="max-w-6xl space-y-4">
      {search.attention && (
        <div className="space-y-2">
          <Link
            to="/app/dashboard"
            search={{}}
            className="inline-flex cursor-pointer items-center rounded-md border border-[var(--border-soft)] bg-white px-4 py-2 font-semibold text-[var(--primary)] hover:bg-[var(--surface-muted)]"
          >
            ← Back to dashboard
          </Link>
          <p className="text-sm font-semibold">
            Needs attention · Unacknowledged forms containing non-operational
            findings · All periods
          </p>
        </div>
      )}
      <PageHeading
        id="forms-title"
        eyebrow="Preventive maintenance"
        title="Form review"
        description={
          isReadOnlySupervisor
            ? 'Review forms available for your assigned schedules. Form details are read-only.'
            : 'Review submitted forms and open their inspection source rows.'
        }
      />
      <FormRegistryFilters
        key={JSON.stringify([
          search.attention,
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
        optionsPending={options.isPending}
        optionsError={options.isError}
        formsError={forms.isError}
        onSearchChange={onSearchChange}
        onRetryForms={() => void forms.refetch()}
        onRetryOptions={() => void options.refetch()}
      />
      <section
        aria-labelledby="form-records-title"
        className="w-full space-y-3"
      >
        <header className="flex items-center justify-between gap-3">
          <h2
            id="form-records-title"
            className="text-lg font-semibold text-[var(--text-primary)]"
          >
            Form records
          </h2>
          <p
            className="text-sm text-[var(--text-secondary)]"
            aria-live="polite"
          >
            {totalCount.toLocaleString()} forms
          </p>
        </header>
        {forms.isFetching && (
          <p className="text-sm text-[var(--text-neutral)]" role="status">
            Updating results...
          </p>
        )}
        {forms.isSuccess && rows.length === 0 && options.isSuccess ? (
          <Card className="text-center shadow-none">
            <h2 className="text-lg font-semibold text-[var(--text-primary)]">
              {options.data.totalCount === 0
                ? 'No preventive-maintenance forms found.'
                : 'No forms match these filters.'}
            </h2>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              {options.data.totalCount === 0
                ? 'Forms will appear here after they are created through the field workflow.'
                : 'Clear the filters to return to all recorded forms.'}
            </p>
          </Card>
        ) : rows.length > 0 ? (
          <>
            <div className="hidden overflow-x-auto rounded-lg border border-[var(--border-soft)] bg-white lg:block">
              <table className="w-full min-w-[880px] table-fixed text-left text-sm">
                <caption className="sr-only">
                  Preventive-maintenance forms
                </caption>
                <colgroup>
                  <col className="w-[22%]" />
                  <col className="w-[16%]" />
                  <col className="w-[15%]" />
                  <col className="w-[14%]" />
                  <col className="w-[10%]" />
                  <col className="w-[14%]" />
                  <col className="w-[9%]" />
                </colgroup>
                <thead className="bg-[var(--primary)] text-xs text-white">
                  <tr>
                    <th scope="col" className="px-4 py-3 font-semibold">
                      Form
                    </th>
                    <th scope="col" className="px-4 py-3 font-semibold">
                      Asset category
                    </th>
                    <th scope="col" className="px-4 py-3 font-semibold">
                      Department
                    </th>
                    <th scope="col" className="px-4 py-3 font-semibold">
                      PM cycle
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-center font-semibold"
                    >
                      Inspections
                    </th>
                    <th scope="col" className="px-4 py-3 font-semibold">
                      Status
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-right font-semibold"
                    >
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {pageData.map((form) => (
                    <FormTableRow key={form.id} form={form} search={search} />
                  ))}
                </tbody>
              </table>
            </div>
            <div className="overflow-hidden rounded-lg border border-[var(--border-soft)] bg-white lg:hidden">
              {pageData.map((form) => (
                <FormMobileRow key={form.id} form={form} search={search} />
              ))}
            </div>
            <FormPagination
              page={page}
              pageSize={pageSize}
              total={totalCount}
              onPageChange={changePage}
            />
          </>
        ) : null}
      </section>
    </section>
  )
}
