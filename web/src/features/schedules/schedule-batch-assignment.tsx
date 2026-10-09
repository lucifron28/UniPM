import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useAssignScheduleBatchSupervisor,
  useAssignScheduleBatchWorker,
  useScheduleSupervisorAssignmentOptions,
  useScheduleWorkerAssignmentOptions,
} from '@/features/schedules/schedule-queries'

const assignableStatuses = new Set(['Due', 'Ongoing', 'Overdue'])

export function ScheduleBatchAssignment({
  schedule,
  canAssignSupervisor,
  canAssignWorker,
  hasSupervisorRole,
}: {
  schedule: Schedule
  canAssignSupervisor: boolean
  canAssignWorker: boolean
  hasSupervisorRole: boolean
}) {
  const canEdit =
    (canAssignSupervisor || canAssignWorker) &&
    assignableStatuses.has(schedule.status) &&
    schedule.completedAt === null
  const supervisors = useScheduleSupervisorAssignmentOptions(
    canEdit && canAssignSupervisor,
  )
  const workers = useScheduleWorkerAssignmentOptions(canEdit && canAssignWorker)
  const supervisorAssignment = useAssignScheduleBatchSupervisor()
  const workerAssignment = useAssignScheduleBatchWorker()
  const [workerUserId, setWorkerUserId] = useState(
    schedule.assignedToUserId ?? '',
  )
  const [supervisorUserId, setSupervisorUserId] = useState(
    schedule.assignedSupervisorUserId ?? '',
  )

  const workerName = workers.data?.workers.find(
    (worker) => worker.id === schedule.assignedToUserId,
  )?.displayName
  const supervisorName = supervisors.data?.supervisors.find(
    (supervisor) => supervisor.id === schedule.assignedSupervisorUserId,
  )?.displayName

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
  const supervisorList = supervisors.data?.supervisors ?? []
  const workerList = workers.data?.workers ?? []

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

      {(showsSupervisorStage || showsWorkerStage) && (
        <div className="space-y-5 border-t border-[var(--border)] pt-5">
          {showsSupervisorStage && (
            <section aria-labelledby="supervisor-assignment-title">
              <h3 id="supervisor-assignment-title" className="font-medium">
                Supervisor assignment
              </h3>
              {supervisors.isPending ? (
                <p
                  role="status"
                  className="mt-2 text-sm text-[var(--text-secondary)]"
                >
                  Loading Supervisor options...
                </p>
              ) : supervisors.isError ? (
                <div role="alert" className="mt-3 space-y-3">
                  <p className="text-sm text-[var(--error)]">
                    Supervisor options could not be loaded.
                  </p>
                  <Button
                    type="button"
                    disabled={supervisors.isFetching}
                    onClick={() => void supervisors.refetch()}
                  >
                    Retry
                  </Button>
                </div>
              ) : supervisorList.length === 0 ? (
                <p
                  role="status"
                  className="mt-2 text-sm text-[var(--text-secondary)]"
                >
                  Add an active Supervisor account before assigning this batch.
                </p>
              ) : (
                <form onSubmit={submitSupervisor} className="mt-3 space-y-3">
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
                      onChange={(event) =>
                        setSupervisorUserId(event.target.value)
                      }
                      disabled={supervisorAssignment.isPending}
                      className="min-h-10 w-full rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm text-[var(--text-primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
                    >
                      <option value="">Choose a Supervisor</option>
                      {supervisorList.map((supervisor) => (
                        <option key={supervisor.id} value={supervisor.id}>
                          {supervisor.displayName}
                        </option>
                      ))}
                    </select>
                  </div>
                  {supervisorAssignment.isError && (
                    <p role="alert" className="text-sm text-[var(--error)]">
                      {supervisorAssignment.error instanceof ApiError &&
                      supervisorAssignment.error.status === 409
                        ? 'This batch can no longer be assigned. Refresh it and review its current status.'
                        : 'The Supervisor assignment could not be saved. Check the selection and try again.'}
                    </p>
                  )}
                  <Button
                    type="submit"
                    disabled={
                      supervisorAssignment.isPending || !supervisorUserId
                    }
                  >
                    {supervisorAssignment.isPending
                      ? 'Saving Supervisor assignment...'
                      : schedule.assignedSupervisorUserId
                        ? 'Update Supervisor for entire batch'
                        : 'Assign Supervisor to entire batch'}
                  </Button>
                </form>
              )}
            </section>
          )}

          {showsWorkerStage && (
            <section aria-labelledby="worker-assignment-title">
              <h3 id="worker-assignment-title" className="font-medium">
                Inspector assignment
              </h3>
              {workers.isPending ? (
                <p
                  role="status"
                  className="mt-2 text-sm text-[var(--text-secondary)]"
                >
                  Loading Inspector options...
                </p>
              ) : workers.isError ? (
                <div role="alert" className="mt-3 space-y-3">
                  <p className="text-sm text-[var(--error)]">
                    Inspector options could not be loaded.
                  </p>
                  <Button
                    type="button"
                    disabled={workers.isFetching}
                    onClick={() => void workers.refetch()}
                  >
                    Retry
                  </Button>
                </div>
              ) : workerList.length === 0 ? (
                <p
                  role="status"
                  className="mt-2 text-sm text-[var(--text-secondary)]"
                >
                  Add an active Inspector account before assigning this batch.
                </p>
              ) : (
                <form onSubmit={submitWorker} className="mt-3 space-y-3">
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
                      disabled={workerAssignment.isPending}
                      className="min-h-10 w-full rounded-lg border border-[var(--border-control)] bg-white px-3 text-sm text-[var(--text-primary)] focus-visible:ring-2 focus-visible:ring-[var(--primary-active)]"
                    >
                      <option value="">Choose an Inspector</option>
                      {workerList.map((worker) => (
                        <option key={worker.id} value={worker.id}>
                          {worker.displayName}
                        </option>
                      ))}
                    </select>
                  </div>
                  {workerAssignment.isError && (
                    <p role="alert" className="text-sm text-[var(--error)]">
                      {workerAssignment.error instanceof ApiError &&
                      workerAssignment.error.status === 409
                        ? 'This batch can no longer be assigned. Refresh it and review its current status.'
                        : 'The Inspector assignment could not be saved. Check the selection and try again.'}
                    </p>
                  )}
                  <Button
                    type="submit"
                    disabled={workerAssignment.isPending || !workerUserId}
                  >
                    {workerAssignment.isPending
                      ? 'Saving Inspector assignment...'
                      : schedule.assignedToUserId
                        ? 'Update Inspector for entire batch'
                        : 'Assign Inspector to entire batch'}
                  </Button>
                </form>
              )}
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
