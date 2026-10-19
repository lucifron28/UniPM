import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import type { SearchablePickerOption } from '@/components/ui/searchable-picker'
import type { ScheduleAssigneeOption } from '@/api/generated/models'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import type { Schedule } from '@/features/schedules/schedule-contract'
import {
  useAssignScheduleBatchSupervisor,
  useAssignScheduleBatchWorker,
  useScheduleSupervisorAssignmentOptions,
  useScheduleWorkerAssignmentOptions,
} from '@/features/schedules/schedule-queries'

const assignableStatuses = new Set(['Due', 'Ongoing', 'Overdue'])

function assigneePickerOption(
  option?: ScheduleAssigneeOption | null,
): SearchablePickerOption<ScheduleAssigneeOption> | undefined {
  return option
    ? { value: option.id, label: option.displayName, item: option }
    : undefined
}

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
  const [workerUserId, setWorkerUserId] = useState(
    schedule.assignedToUserId ?? '',
  )
  const [supervisorUserId, setSupervisorUserId] = useState(
    schedule.assignedSupervisorUserId ?? '',
  )
  const [workerSearch, setWorkerSearch] = useState('')
  const [supervisorSearch, setSupervisorSearch] = useState('')
  const [workerSelectedOption, setWorkerSelectedOption] = useState<
    SearchablePickerOption<ScheduleAssigneeOption> | undefined
  >()
  const [supervisorSelectedOption, setSupervisorSelectedOption] = useState<
    SearchablePickerOption<ScheduleAssigneeOption> | undefined
  >()
  const supervisors = useScheduleSupervisorAssignmentOptions(
    canEdit && canAssignSupervisor,
    {
      search: supervisorSearch,
      ...(schedule.assignedSupervisorUserId
        ? { selectedUserId: schedule.assignedSupervisorUserId }
        : {}),
    },
  )
  const workers = useScheduleWorkerAssignmentOptions(
    canEdit && canAssignWorker,
    {
      search: workerSearch,
      ...(schedule.assignedToUserId
        ? { selectedUserId: schedule.assignedToUserId }
        : {}),
    },
  )
  const supervisorAssignment = useAssignScheduleBatchSupervisor()
  const workerAssignment = useAssignScheduleBatchWorker()

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
  const supervisorList =
    supervisors.data?.pages.flatMap((page) => page.supervisors) ?? []
  const workerList = workers.data?.pages.flatMap((page) => page.workers) ?? []
  const selectedSupervisor =
    supervisorSelectedOption ??
    assigneePickerOption(supervisors.data?.pages[0]?.selected) ??
    assigneePickerOption(
      supervisorList.find((option) => option.id === supervisorUserId),
    )
  const selectedWorker =
    workerSelectedOption ??
    assigneePickerOption(workers.data?.pages[0]?.selected) ??
    assigneePickerOption(
      workerList.find((option) => option.id === workerUserId),
    )
  const savedSupervisorName =
    supervisors.data?.pages[0]?.selected?.id ===
    schedule.assignedSupervisorUserId
      ? supervisors.data.pages[0].selected.displayName
      : supervisorList.find(
          (option) => option.id === schedule.assignedSupervisorUserId,
        )?.displayName
  const savedWorkerName =
    workers.data?.pages[0]?.selected?.id === schedule.assignedToUserId
      ? workers.data.pages[0].selected.displayName
      : workerList.find((option) => option.id === schedule.assignedToUserId)
          ?.displayName

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
            Skilled worker
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.assignedToUserId
              ? (savedWorkerName ?? 'Inspector assigned')
              : 'Not assigned'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-[var(--text-neutral)]">
            Supervisor
          </dt>
          <dd className="mt-1 text-sm">
            {schedule.assignedSupervisorUserId
              ? (savedSupervisorName ?? 'Supervisor assigned')
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
              <form onSubmit={submitSupervisor} className="mt-3 space-y-3">
                <SearchablePicker
                  id="batch-supervisor"
                  label="Supervisor (oversight)"
                  value={supervisorUserId || undefined}
                  selectedOption={selectedSupervisor}
                  options={supervisorList.map((supervisor) => ({
                    value: supervisor.id,
                    label: supervisor.displayName,
                    item: supervisor,
                  }))}
                  placeholder="Choose a Supervisor"
                  emptyMessage="Add an active Supervisor account before assigning this batch."
                  loading={
                    supervisors.isPending ||
                    (supervisors.isFetching && supervisorList.length === 0)
                  }
                  loadingMore={supervisors.isFetchingNextPage}
                  errorMessage={
                    supervisors.isError
                      ? 'Supervisor options could not be loaded.'
                      : undefined
                  }
                  hasMore={Boolean(supervisors.hasNextPage)}
                  onLoadMore={() => void supervisors.fetchNextPage()}
                  onRetry={() => void supervisors.refetch()}
                  onSearchChange={setSupervisorSearch}
                  disabled={supervisorAssignment.isPending}
                  onChange={(value, option) => {
                    setSupervisorUserId(value ?? '')
                    setSupervisorSelectedOption(option)
                  }}
                />
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
                  disabled={supervisorAssignment.isPending || !supervisorUserId}
                >
                  {supervisorAssignment.isPending
                    ? 'Saving Supervisor assignment...'
                    : schedule.assignedSupervisorUserId
                      ? 'Update Supervisor for entire batch'
                      : 'Assign Supervisor to entire batch'}
                </Button>
              </form>
            </section>
          )}

          {showsWorkerStage && (
            <section aria-labelledby="worker-assignment-title">
              <h3 id="worker-assignment-title" className="font-medium">
                Inspector assignment
              </h3>
              <form onSubmit={submitWorker} className="mt-3 space-y-3">
                <SearchablePicker
                  id="batch-worker"
                  label="Skilled worker (Inspector)"
                  value={workerUserId || undefined}
                  selectedOption={selectedWorker}
                  options={workerList.map((worker) => ({
                    value: worker.id,
                    label: worker.displayName,
                    item: worker,
                  }))}
                  placeholder="Choose an Inspector"
                  emptyMessage="Add an active Inspector account before assigning this batch."
                  loading={
                    workers.isPending ||
                    (workers.isFetching && workerList.length === 0)
                  }
                  loadingMore={workers.isFetchingNextPage}
                  errorMessage={
                    workers.isError
                      ? 'Inspector options could not be loaded.'
                      : undefined
                  }
                  hasMore={Boolean(workers.hasNextPage)}
                  onLoadMore={() => void workers.fetchNextPage()}
                  onRetry={() => void workers.refetch()}
                  onSearchChange={setWorkerSearch}
                  disabled={workerAssignment.isPending}
                  onChange={(value, option) => {
                    setWorkerUserId(value ?? '')
                    setWorkerSelectedOption(option)
                  }}
                />
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
