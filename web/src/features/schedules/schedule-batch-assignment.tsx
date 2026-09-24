import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useAssignScheduleBatch,
  useScheduleAssignmentOptions,
} from '@/features/schedules/schedule-queries'

const assignableStatuses = new Set(['Due', 'Ongoing', 'Overdue'])

export function ScheduleBatchAssignment({
  schedule,
  canAssign,
}: {
  schedule: Schedule
  canAssign: boolean
}) {
  const options = useScheduleAssignmentOptions(canAssign)
  const assignment = useAssignScheduleBatch()
  const [workerUserId, setWorkerUserId] = useState(
    schedule.assignedToUserId ?? '',
  )
  const [supervisorUserId, setSupervisorUserId] = useState(
    schedule.assignedSupervisorUserId ?? '',
  )
  const canEdit = canAssign && assignableStatuses.has(schedule.status)
  const workers = options.data?.workers ?? []
  const supervisors = options.data?.supervisors ?? []
  const workerName = workers.find(
    (worker) => worker.id === schedule.assignedToUserId,
  )?.displayName
  const supervisorName = supervisors.find(
    (supervisor) => supervisor.id === schedule.assignedSupervisorUserId,
  )?.displayName

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!workerUserId || !supervisorUserId) return

    assignment.mutate({
      scheduleId: schedule.id,
      workerUserId,
      supervisorUserId,
    })
  }

  return (
    <Card className="space-y-5 shadow-none">
      <div>
        <h2 className="font-semibold">Batch assignment</h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          GSD assigns one Inspector and one Supervisor for the department,
          category, and PM cycle. The Supervisor provides oversight and is not
          an approval step.
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
            {schedule.asset?.assetCategory ?? 'Not recorded'}
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
            Skilled worker
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

      {canEdit && (
        <div className="border-t border-[var(--border)] pt-5">
          {options.isPending ? (
            <p role="status" className="text-sm text-[var(--text-secondary)]">
              Loading assignment options...
            </p>
          ) : options.isError ? (
            <div role="alert" className="space-y-3">
              <p className="text-sm text-[var(--error)]">
                Assignment options could not be loaded.
              </p>
              <Button
                type="button"
                disabled={options.isFetching}
                onClick={() => void options.refetch()}
              >
                Retry
              </Button>
            </div>
          ) : workers.length === 0 || supervisors.length === 0 ? (
            <p role="status" className="text-sm text-[var(--text-secondary)]">
              Add an active Inspector and Supervisor account before assigning
              this batch.
            </p>
          ) : (
            <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
              <div>
                <label
                  htmlFor="batch-worker"
                  className="mb-1 block text-sm font-medium"
                >
                  Skilled worker (Inspector)
                </label>
                <select
                  id="batch-worker"
                  required
                  value={workerUserId}
                  onChange={(event) => setWorkerUserId(event.target.value)}
                  disabled={assignment.isPending}
                  className="min-h-10 w-full rounded-lg border border-[var(--border)] bg-white px-3 text-sm text-[var(--text-primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
                >
                  <option value="">Choose an Inspector</option>
                  {workers.map((worker) => (
                    <option key={worker.id} value={worker.id}>
                      {worker.displayName}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label
                  htmlFor="batch-supervisor"
                  className="mb-1 block text-sm font-medium"
                >
                  Supervisor (oversight)
                </label>
                <select
                  id="batch-supervisor"
                  required
                  value={supervisorUserId}
                  onChange={(event) => setSupervisorUserId(event.target.value)}
                  disabled={assignment.isPending}
                  className="min-h-10 w-full rounded-lg border border-[var(--border)] bg-white px-3 text-sm text-[var(--text-primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
                >
                  <option value="">Choose a Supervisor</option>
                  {supervisors.map((supervisor) => (
                    <option key={supervisor.id} value={supervisor.id}>
                      {supervisor.displayName}
                    </option>
                  ))}
                </select>
              </div>
              {assignment.isError && (
                <p
                  role="alert"
                  className="text-sm text-[var(--error)] sm:col-span-2"
                >
                  {assignment.error instanceof ApiError &&
                  assignment.error.status === 409
                    ? 'This batch can no longer be assigned. Refresh it and review its current status.'
                    : 'The batch assignment could not be saved. Check the selections and try again.'}
                </p>
              )}
              {assignment.data && (
                <p
                  role="status"
                  className="text-sm text-[var(--success)] sm:col-span-2"
                >
                  Assigned {assignment.data.scheduleIds.length} schedule(s) for{' '}
                  {assignment.data.department}, {assignment.data.assetCategory},{' '}
                  {assignment.data.pmCycle} to{' '}
                  {assignment.data.workerDisplayName};{' '}
                  {assignment.data.supervisorDisplayName} is the oversight
                  Supervisor.
                </p>
              )}
              <div className="sm:col-span-2">
                <Button
                  type="submit"
                  disabled={
                    assignment.isPending || !workerUserId || !supervisorUserId
                  }
                >
                  {assignment.isPending
                    ? 'Saving assignment...'
                    : schedule.assignedToUserId ||
                        schedule.assignedSupervisorUserId
                      ? 'Update entire batch assignment'
                      : 'Assign entire batch'}
                </Button>
              </div>
            </form>
          )}
        </div>
      )}
    </Card>
  )
}
