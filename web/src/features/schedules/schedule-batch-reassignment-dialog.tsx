import { useEffect, useState } from 'react'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { SearchableOptionInput } from '@/components/ui/searchable-option-input'
import { useCurrentUser } from '@/features/auth/current-user'
import type { ScheduleRegistryItem } from '@/features/schedules/schedule-contract'
import {
  useReassignScheduleBatchInspector,
  useReassignScheduleBatchSupervisor,
  useScheduleAssignmentBatches,
  useScheduleSupervisorAssignmentOptions,
  useScheduleWorkerAssignmentOptions,
} from '@/features/schedules/schedule-queries'

type ReassignmentRole = 'Supervisor' | 'Inspector'

export function ScheduleBatchReassignmentDialog({
  schedule,
  onClose,
}: {
  schedule: ScheduleRegistryItem
  onClose: () => void
}) {
  const currentUser = useCurrentUser()
  const [reassignmentRole, setReassignmentRole] =
    useState<ReassignmentRole | null>(null)
  const [search, setSearch] = useState('')
  const [assigneeUserId, setAssigneeUserId] = useState('')
  const [reason, setReason] = useState('')
  const canLoadBatch = Boolean(
    schedule.pmCycle &&
    schedule.asset?.department &&
    schedule.asset.assetCategory,
  )
  const batches = useScheduleAssignmentBatches(
    {
      year: Number(schedule.pmCycle.slice(0, 4)),
      pmCycle: schedule.pmCycle,
      ...(schedule.asset?.department
        ? { department: schedule.asset.department }
        : {}),
      ...(schedule.asset?.assetCategory
        ? { assetCategory: schedule.asset.assetCategory }
        : {}),
    },
    canLoadBatch,
  )
  const batch = batches.data?.batches.find(
    (candidate) =>
      candidate.pmCycle === schedule.pmCycle &&
      candidate.assetCategory === schedule.asset?.assetCategory &&
      candidate.department.toLocaleLowerCase() ===
        schedule.asset?.department?.toLocaleLowerCase(),
  )
  const roles = currentUser.data?.roles ?? []
  const canReassignSupervisor = roles.includes('GSD')
  const canReassignInspector =
    roles.includes('Supervisor') &&
    batch?.supervisorOwnershipIsConsistent === true &&
    batch.supervisorUserId === currentUser.data?.id
  const supervisorOptions = useScheduleSupervisorAssignmentOptions(
    reassignmentRole === 'Supervisor' && canReassignSupervisor,
    search,
  )
  const inspectorOptions = useScheduleWorkerAssignmentOptions(
    reassignmentRole === 'Inspector' && canReassignInspector,
    search,
  )
  const supervisorReassignment = useReassignScheduleBatchSupervisor()
  const inspectorReassignment = useReassignScheduleBatchInspector()
  const isSaving =
    supervisorReassignment.isPending || inspectorReassignment.isPending
  const options =
    reassignmentRole === 'Supervisor'
      ? (supervisorOptions.data?.supervisors ?? [])
      : (inspectorOptions.data?.workers ?? [])
  const selectedOption = options.find((option) => option.id === assigneeUserId)
  const assignmentState =
    reassignmentRole === 'Supervisor'
      ? batch?.supervisorState
      : batch?.inspectorState
  const currentAssignee =
    reassignmentRole === 'Supervisor'
      ? (batch?.supervisorDisplayName ?? assignmentState)
      : (batch?.inspectorDisplayName ?? assignmentState)

  useEffect(() => {
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isSaving) onClose()
    }
    window.addEventListener('keydown', handleEscape)
    return () => window.removeEventListener('keydown', handleEscape)
  }, [isSaving, onClose])

  function startReassignment(role: ReassignmentRole) {
    setReassignmentRole(role)
    setSearch('')
    setAssigneeUserId('')
    setReason('')
  }

  async function submitReassignment() {
    if (!reassignmentRole || !batch || !assigneeUserId || !reason.trim()) return
    const variables = {
      scheduleId: schedule.id,
      request: {
        assigneeUserId,
        reason: reason.trim(),
      },
    }
    try {
      if (reassignmentRole === 'Supervisor') {
        await supervisorReassignment.mutateAsync(variables)
      } else {
        await inspectorReassignment.mutateAsync(variables)
      }
      onClose()
    } catch {
      // Mutation errors are shown below without exposing response payloads.
    }
  }

  const error = supervisorReassignment.error ?? inspectorReassignment.error

  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center bg-black/45 p-4"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isSaving) onClose()
      }}
    >
      <section
        aria-labelledby="schedule-reassignment-title"
        aria-modal="true"
        className="max-h-[min(90vh,44rem)] w-full max-w-xl overflow-y-auto rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-2xl"
        role="dialog"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 id="schedule-reassignment-title" className="text-xl font-bold">
              Schedule batch assignment
            </h2>
            <p className="mt-1 text-sm text-[var(--text-secondary)]">
              {schedule.asset?.department ?? 'Department unavailable'} ·{' '}
              {schedule.asset?.assetCategory ?? 'Category unavailable'} ·{' '}
              {schedule.pmCycle}
            </p>
          </div>
          <Button variant="secondary" disabled={isSaving} onClick={onClose}>
            Close
          </Button>
        </div>

        {batches.isPending ? (
          <p
            role="status"
            className="mt-5 text-sm text-[var(--text-secondary)]"
          >
            Loading the current batch assignments…
          </p>
        ) : batches.isError ? (
          <div role="alert" className="mt-5 space-y-2 text-sm">
            <p>The current assignments could not be loaded.</p>
            <Button
              variant="secondary"
              disabled={batches.isFetching}
              onClick={() => void batches.refetch()}
            >
              Retry
            </Button>
          </div>
        ) : !batch ? (
          <p
            role="status"
            className="mt-5 text-sm text-[var(--text-secondary)]"
          >
            This batch is not available for assignment review under your
            account.
          </p>
        ) : (
          <>
            <dl className="mt-5 grid gap-3 rounded-lg border-l-4 border-[var(--primary)] bg-[#f7f2ef] p-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                  Current Supervisor
                </dt>
                <dd className="mt-1">
                  {batch.supervisorDisplayName ?? batch.supervisorState}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                  Current Inspector
                </dt>
                <dd className="mt-1">
                  {batch.inspectorDisplayName ?? batch.inspectorState}
                </dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-xs font-semibold text-[var(--text-neutral)]">
                  Unfinished schedules
                </dt>
                <dd className="mt-1">{batch.unfinishedScheduleCount}</dd>
              </div>
            </dl>

            {batch.unfinishedScheduleCount === 0 ? (
              <p
                role="status"
                className="mt-4 text-sm text-[var(--text-secondary)]"
              >
                No unfinished schedules remain. This batch is view-only.
              </p>
            ) : reassignmentRole === null ? (
              <div className="mt-4 flex flex-wrap gap-2">
                {canReassignSupervisor &&
                  batch.supervisorState !== 'Unassigned' && (
                    <Button onClick={() => startReassignment('Supervisor')}>
                      Reassign Supervisor
                    </Button>
                  )}
                {canReassignInspector &&
                  batch.inspectorState !== 'Unassigned' && (
                    <Button onClick={() => startReassignment('Inspector')}>
                      Reassign Inspector
                    </Button>
                  )}
                {!canReassignSupervisor && !canReassignInspector && (
                  <p
                    role="status"
                    className="text-sm text-[var(--text-secondary)]"
                  >
                    Reassignment is available only to GSD or the Supervisor
                    currently responsible for this batch.
                  </p>
                )}
              </div>
            ) : (
              <div className="mt-4 space-y-3">
                <p className="text-sm text-[var(--text-secondary)]">
                  Replacing {currentAssignee ?? 'the current assignee'} for{' '}
                  {batch.unfinishedScheduleCount} unfinished schedule
                  {batch.unfinishedScheduleCount === 1 ? '' : 's'}. Completed
                  schedules and inspection history stay unchanged.
                </p>
                <SearchableOptionInput
                  id="schedule-reassignment-assignee"
                  label={`Replacement ${reassignmentRole}`}
                  value={search || selectedOption?.displayName || ''}
                  options={options.map((option) => ({
                    value: option.id,
                    label: option.displayName,
                  }))}
                  placeholder={`Type a ${reassignmentRole.toLowerCase()}'s name`}
                  helperText={`Search active ${reassignmentRole.toLowerCase()} accounts.`}
                  disabled={isSaving}
                  onChange={(value) => {
                    setSearch(value)
                    setAssigneeUserId(
                      options.find(
                        (option) =>
                          option.displayName.toLocaleLowerCase() ===
                          value.trim().toLocaleLowerCase(),
                      )?.id ?? '',
                    )
                  }}
                />
                <label className="grid gap-1 text-sm font-medium">
                  Reason for reassignment
                  <textarea
                    value={reason}
                    maxLength={500}
                    rows={2}
                    disabled={isSaving}
                    onChange={(event) => setReason(event.target.value)}
                    className="rounded-lg border border-[var(--border-control)] bg-white px-3 py-2"
                  />
                </label>
                {(supervisorOptions.isPending ||
                  inspectorOptions.isPending) && (
                  <p
                    role="status"
                    className="text-sm text-[var(--text-secondary)]"
                  >
                    Loading active replacement accounts…
                  </p>
                )}
                {error && (
                  <p role="alert" className="text-sm text-[var(--error)]">
                    {error instanceof ApiError && error.status === 409
                      ? 'The batch changed before reassignment. Refresh and review its current assignments.'
                      : `The ${reassignmentRole.toLowerCase()} could not be reassigned. Check the selection and try again.`}
                  </p>
                )}
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    disabled={
                      isSaving ||
                      !selectedOption ||
                      !reason.trim() ||
                      reason.trim().length > 500
                    }
                    onClick={() => void submitReassignment()}
                  >
                    {isSaving ? 'Saving reassignment…' : 'Confirm reassignment'}
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={isSaving}
                    onClick={() => setReassignmentRole(null)}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  )
}
