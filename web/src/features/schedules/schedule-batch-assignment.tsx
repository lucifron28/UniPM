import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  SearchableOptionInput,
  type SearchableOption,
} from '@/components/ui/searchable-option-input'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useAssignScheduleBatchSupervisor,
  useAssignScheduleBatchWorker,
  useScheduleRegistry,
  useScheduleSupervisorAssignmentOptions,
  useScheduleWorkerAssignmentOptions,
} from '@/features/schedules/schedule-queries'

const assignableStatuses = new Set(['Due', 'Ongoing', 'Overdue'])
type AssigneeOption = { id: string; displayName: string }

function includeSelectedOption(
  options: AssigneeOption[],
  selected?: AssigneeOption | null,
) {
  return selected && !options.some((option) => option.id === selected.id)
    ? [selected, ...options]
    : options
}

export function ScheduleBatchAssignment({
  schedule,
  canAssignSupervisor,
  canAssignWorker,
  hasSupervisorRole,
  supervisorAssignmentIsMixed = false,
  inspectorAssignmentIsMixed = false,
  knownBatchScheduleCount,
}: {
  schedule: Schedule
  canAssignSupervisor: boolean
  canAssignWorker: boolean
  hasSupervisorRole: boolean
  supervisorAssignmentIsMixed?: boolean
  inspectorAssignmentIsMixed?: boolean
  knownBatchScheduleCount?: number
}) {
  const canEdit =
    (canAssignSupervisor || canAssignWorker) &&
    assignableStatuses.has(schedule.status) &&
    schedule.completedAt === null
  const [workerSearch, setWorkerSearch] = useState('')
  const [supervisorSearch, setSupervisorSearch] = useState('')
  const [workerUserId, setWorkerUserId] = useState(
    schedule.assignedToUserId ?? '',
  )
  const [supervisorUserId, setSupervisorUserId] = useState(
    schedule.assignedSupervisorUserId ?? '',
  )
  const supervisors = useScheduleSupervisorAssignmentOptions(
    canEdit && canAssignSupervisor,
    supervisorSearch,
    supervisorUserId || undefined,
  )
  const workers = useScheduleWorkerAssignmentOptions(
    canEdit && canAssignWorker,
    workerSearch,
    workerUserId || undefined,
  )
  const supervisorAssignment = useAssignScheduleBatchSupervisor()
  const workerAssignment = useAssignScheduleBatchWorker()
  const assignmentBatchSummary = useScheduleRegistry(
    {
      ...(schedule.asset?.assetCategory
        ? { assetCategory: schedule.asset.assetCategory }
        : {}),
      ...(schedule.asset?.department
        ? { department: schedule.asset.department }
        : {}),
      ...(schedule.pmCycle ? { pmCycle: schedule.pmCycle } : {}),
      page: 1,
      pageSize: 1,
    },
    knownBatchScheduleCount === undefined &&
      canEdit &&
      (canAssignSupervisor || canAssignWorker) &&
      Boolean(
        schedule.asset?.assetCategory &&
        schedule.asset.department &&
        schedule.pmCycle,
      ),
  )
  const workerName =
    workers.data?.workers.find(
      (worker) => worker.id === schedule.assignedToUserId,
    )?.displayName ?? workers.data?.selected?.displayName
  const supervisorName =
    supervisors.data?.supervisors.find(
      (supervisor) => supervisor.id === schedule.assignedSupervisorUserId,
    )?.displayName ?? supervisors.data?.selected?.displayName

  function submitSupervisor(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!supervisorUserId) return

    supervisorAssignment.mutate({
      scheduleId: schedule.id,
      supervisorUserId,
    })
  }

  function submitWorker(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!workerUserId) return

    workerAssignment.mutate({
      scheduleId: schedule.id,
      workerUserId,
    })
  }

  const showsSupervisorStage = canEdit && canAssignSupervisor
  const showsWorkerStage = canEdit && canAssignWorker
  const supervisorList = includeSelectedOption(
    supervisors.data?.supervisors ?? [],
    supervisors.data?.selected,
  )
  const workerList = includeSelectedOption(
    workers.data?.workers ?? [],
    workers.data?.selected,
  )
  const batchScheduleCount =
    knownBatchScheduleCount ??
    (assignmentBatchSummary.isPlaceholderData
      ? null
      : (assignmentBatchSummary.data?.totalCount ?? null))
  const isBatchCountPending =
    knownBatchScheduleCount === undefined &&
    (assignmentBatchSummary.isPending || assignmentBatchSummary.isFetching)
  const batchCountError =
    knownBatchScheduleCount === undefined && assignmentBatchSummary.isError
  const assignmentScope = {
    department: schedule.asset?.department ?? 'Not recorded',
    assetCategory: schedule.asset
      ? formatAssetCategory(schedule.asset.assetCategory)
      : 'Not recorded',
    pmCycle: schedule.pmCycle ?? 'Not recorded',
  }

  return (
    <Card className="space-y-5 shadow-none">
      <div>
        <h2 className="font-semibold">Batch assignment</h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          GSD assigns a Supervisor for the department, category, and PM cycle.
          The Supervisor then assigns an Inspector and provides oversight; this
          is not an approval step.
        </p>
      </div>

      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            Department
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.asset?.department ?? 'Not recorded'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            Asset category
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.asset
              ? formatAssetCategory(schedule.asset.assetCategory)
              : 'Not recorded'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            PM cycle
          </dt>
          <dd className="mt-1 text-sm">{schedule.pmCycle ?? 'Not recorded'}</dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            Inspector
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.assignedToUserId
              ? (workerName ?? 'Inspector assigned')
              : 'Not assigned'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            Supervisor
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.assignedSupervisorUserId
              ? (supervisorName ?? 'Supervisor assigned')
              : 'Not assigned'}
          </dd>
        </div>
      </dl>

      {(showsSupervisorStage || showsWorkerStage) && (
        <div className="space-y-5 border-t border-[var(--border)] pt-5">
          {showsSupervisorStage && (
            <section aria-labelledby="supervisor-assignment-title">
              <h3 id="supervisor-assignment-title" className="font-medium">
                Supervisor assignment
              </h3>
              <AssignmentForm
                role="Supervisor"
                label="Supervisor (oversight)"
                options={supervisorList}
                optionsIsPending={supervisors.isPending}
                optionsIsFetching={supervisors.isFetching}
                optionsIsError={supervisors.isError}
                onRetryOptions={() => void supervisors.refetch()}
                search={supervisorSearch}
                onSearchChange={setSupervisorSearch}
                selectedId={supervisorUserId}
                onSelectedIdChange={setSupervisorUserId}
                currentAssigneeId={schedule.assignedSupervisorUserId}
                scope={assignmentScope}
                isSaving={supervisorAssignment.isPending}
                error={
                  supervisorAssignment.isError
                    ? supervisorAssignment.error
                    : null
                }
                existingAssignment={Boolean(
                  schedule.assignedSupervisorUserId ||
                  supervisorAssignmentIsMixed,
                )}
                assignmentIsMixed={supervisorAssignmentIsMixed}
                batchScheduleCount={batchScheduleCount}
                isBatchCountPending={isBatchCountPending}
                batchCountError={batchCountError}
                onRetryBatchCount={() => void assignmentBatchSummary.refetch()}
                onSubmit={submitSupervisor}
              />
            </section>
          )}

          {showsWorkerStage && (
            <section aria-labelledby="worker-assignment-title">
              <h3 id="worker-assignment-title" className="font-medium">
                Inspector assignment
              </h3>
              <AssignmentForm
                role="Inspector"
                label="Inspector"
                options={workerList}
                optionsIsPending={workers.isPending}
                optionsIsFetching={workers.isFetching}
                optionsIsError={workers.isError}
                onRetryOptions={() => void workers.refetch()}
                search={workerSearch}
                onSearchChange={setWorkerSearch}
                selectedId={workerUserId}
                onSelectedIdChange={setWorkerUserId}
                currentAssigneeId={schedule.assignedToUserId}
                scope={assignmentScope}
                isSaving={workerAssignment.isPending}
                error={workerAssignment.isError ? workerAssignment.error : null}
                existingAssignment={Boolean(schedule.assignedToUserId)}
                assignmentIsMixed={inspectorAssignmentIsMixed}
                batchScheduleCount={batchScheduleCount}
                isBatchCountPending={isBatchCountPending}
                batchCountError={batchCountError}
                onRetryBatchCount={() => void assignmentBatchSummary.refetch()}
                onSubmit={submitWorker}
              />
            </section>
          )}
        </div>
      )}

      {hasSupervisorRole &&
        !canAssignWorker &&
        assignableStatuses.has(schedule.status) &&
        schedule.completedAt === null && (
          <p
            role="status"
            className="border-t border-[var(--border)] pt-4 text-sm text-[var(--text-secondary)]"
          >
            {schedule.assignedSupervisorUserId
              ? 'Only the Supervisor assigned to this PM batch can assign or update its Inspector.'
              : 'GSD must assign a Supervisor to this PM batch before an Inspector can be assigned.'}
          </p>
        )}
    </Card>
  )
}

function AssignmentForm({
  role,
  label,
  options,
  optionsIsPending,
  optionsIsFetching,
  optionsIsError,
  onRetryOptions,
  search,
  onSearchChange,
  selectedId,
  onSelectedIdChange,
  currentAssigneeId,
  scope,
  isSaving,
  error,
  existingAssignment,
  assignmentIsMixed,
  batchScheduleCount,
  isBatchCountPending,
  batchCountError,
  onRetryBatchCount,
  onSubmit,
}: {
  role: 'Supervisor' | 'Inspector'
  label: string
  options: { id: string; displayName: string }[]
  optionsIsPending: boolean
  optionsIsFetching: boolean
  optionsIsError: boolean
  onRetryOptions: () => void
  search: string
  onSearchChange: (value: string) => void
  selectedId: string
  onSelectedIdChange: (id: string) => void
  currentAssigneeId: string | null
  scope: { department: string; assetCategory: string; pmCycle: string }
  isSaving: boolean
  error: unknown
  existingAssignment: boolean
  assignmentIsMixed: boolean
  batchScheduleCount: number | null
  isBatchCountPending: boolean
  batchCountError: boolean
  onRetryBatchCount: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  const pickerOptions = getAssigneePickerOptions(options)
  const selectedOption = pickerOptions.find(
    (option) => option.value === selectedId,
  )

  return (
    <form onSubmit={onSubmit} className="mt-3 space-y-3">
      <div>
        <p className="mb-1 text-xs font-semibold text-[var(--text-neutral)]">
          Step 1 of 3 · Choose a {role}
        </p>
        <SearchableOptionInput
          id={`batch-${role.toLowerCase()}-picker`}
          label={label}
          value={search || selectedOption?.label || ''}
          options={pickerOptions}
          disabled={isSaving}
          placeholder={`Type a ${role.toLowerCase()}'s name`}
          helperText={`Type a name to search active ${role.toLowerCase()} accounts, then choose a suggestion.`}
          onChange={(value) => {
            onSearchChange(value)
            const option = pickerOptions.find(
              (candidate) =>
                candidate.label.toLocaleLowerCase() ===
                value.trim().toLocaleLowerCase(),
            )
            onSelectedIdChange(option?.value ?? '')
          }}
        />
        {optionsIsPending ? (
          <p
            role="status"
            className="mt-2 text-xs text-[var(--text-secondary)]"
          >
            Loading eligible {role} accounts…
          </p>
        ) : optionsIsError ? (
          <div
            role="alert"
            className="mt-2 flex flex-wrap items-center gap-2 text-sm text-[var(--error)]"
          >
            <span>{role} accounts could not be loaded.</span>
            <button
              type="button"
              disabled={optionsIsFetching}
              onClick={onRetryOptions}
              className="font-semibold underline underline-offset-2"
            >
              Retry
            </button>
          </div>
        ) : optionsIsFetching ? (
          <p
            role="status"
            className="mt-2 text-xs text-[var(--text-secondary)]"
          >
            Searching eligible {role} accounts…
          </p>
        ) : options.length === 0 ? (
          <p
            role="status"
            className="mt-2 text-xs text-[var(--text-secondary)]"
          >
            No active {role} accounts match. Try another name or ask an
            administrator to check the account.
          </p>
        ) : null}
      </div>
      <div
        aria-live="polite"
        className="rounded-lg border border-[var(--border-soft)] bg-[var(--surface-muted)] p-3 text-sm"
      >
        <p className="font-medium">Step 2 of 3 · Review assignment</p>
        <p className="mt-1 text-[var(--text-secondary)]">
          {selectedOption
            ? `${selectedOption.label} will be ${existingAssignment ? 'updated as' : 'assigned as'} the ${role.toLowerCase()} for this batch.`
            : `Choose a ${role.toLowerCase()} to review this assignment.`}
        </p>
        {assignmentIsMixed && selectedId && (
          <p
            role="status"
            className="mt-2 text-sm text-[var(--text-secondary)]"
          >
            This batch has different existing {role.toLowerCase()} assignments.
            The selected {role.toLowerCase()} will be assigned to every eligible
            schedule in the batch.
          </p>
        )}
        {isBatchCountPending ? (
          <p
            role="status"
            className="mt-2 text-xs text-[var(--text-secondary)]"
          >
            Checking the number of schedules in this batch…
          </p>
        ) : batchCountError ? (
          <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-[var(--error)]">
            <span>
              Batch size could not be verified. Retry before assigning.
            </span>
            <button
              type="button"
              disabled={isBatchCountPending}
              onClick={onRetryBatchCount}
              className="font-semibold underline underline-offset-2"
            >
              Retry batch check
            </button>
          </div>
        ) : batchScheduleCount === null ? (
          <p role="status" className="mt-2 text-xs text-[var(--error)]">
            Batch details are incomplete. Assignment is unavailable until the
            scope can be verified.
          </p>
        ) : (
          <p className="mt-2 text-xs font-medium text-[var(--text-primary)]">
            This assignment affects {batchScheduleCount}{' '}
            {batchScheduleCount === 1 ? 'schedule' : 'schedules'} in this batch.
          </p>
        )}
        <p className="mt-2 text-xs text-[var(--text-secondary)]">
          {scope.department} · {scope.assetCategory} · {scope.pmCycle}. The
          assignment applies to every eligible schedule in this batch.
        </p>
        {role === 'Supervisor' &&
          selectedId &&
          (assignmentIsMixed ||
            (currentAssigneeId !== null &&
              selectedId !== currentAssigneeId)) && (
            <p className="mt-2 text-sm text-[var(--error)]">
              Changing the Supervisor clears Inspector assignments for this
              batch. The new Supervisor will need to assign an Inspector again.
            </p>
          )}
      </div>
      {error != null && (
        <p role="alert" className="text-sm text-[var(--error)]">
          {error instanceof ApiError && error.status === 409
            ? 'This batch can no longer be assigned. Refresh it and review its current status.'
            : `The ${role} assignment could not be saved. Check the selection and try again.`}
        </p>
      )}
      <p className="text-xs font-semibold text-[var(--text-neutral)]">
        Step 3 of 3 · Confirm assignment
      </p>
      <Button
        type="submit"
        disabled={
          isSaving ||
          !selectedId ||
          !selectedOption ||
          batchScheduleCount === null ||
          batchScheduleCount < 1 ||
          batchCountError
        }
      >
        {isSaving
          ? `Saving ${role} assignment...`
          : existingAssignment
            ? `Update ${role} for entire batch`
            : `Assign ${role} to entire batch`}
      </Button>
    </form>
  )
}

function getAssigneePickerOptions(
  options: { id: string; displayName: string }[],
): SearchableOption[] {
  const nameCounts = new Map<string, number>()
  for (const option of options) {
    nameCounts.set(
      option.displayName,
      (nameCounts.get(option.displayName) ?? 0) + 1,
    )
  }

  return options.map((option) => ({
    value: option.id,
    label:
      nameCounts.get(option.displayName) === 1
        ? option.displayName
        : `${option.displayName} · …${option.id.slice(-4)}`,
  }))
}
