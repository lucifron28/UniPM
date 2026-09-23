import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  assignScheduleBatch,
  getGetScheduleQueryKey,
  getListScheduleAssignmentOptionsQueryKey,
  getListSchedulePeriodTypesQueryKey,
  getListScheduleQuartersQueryKey,
  getListScheduleStatusesQueryKey,
  getListSchedulesQueryKey,
  getSchedule,
  listScheduleAssignmentOptions,
  listSchedulePeriodTypes,
  listScheduleQuarters,
  listScheduleStatuses,
  listSchedules,
} from '@/api/generated/endpoints'
import type {
  ListSchedulesParams,
  ScheduleReferenceResponse,
} from '@/api/generated/models'
import {
  parseSchedulePeriodTypes,
  parseScheduleQuarters,
  parseScheduleStatuses,
  parseSchedule,
  parseSchedules,
} from '@/features/schedules/schedule-contract'

export type ScheduleServerFilters = ListSchedulesParams

export function useSchedules(filters: ScheduleServerFilters = {}) {
  return useQuery({
    queryKey: getListSchedulesQueryKey(filters),
    queryFn: ({ signal }) =>
      listSchedules(filters, signal).then(parseSchedules),
  })
}

export function useSchedule(scheduleId: string, enabled = true) {
  return useQuery({
    queryKey: getGetScheduleQueryKey(scheduleId),
    queryFn: ({ signal }) =>
      getSchedule(scheduleId, signal).then(parseSchedule),
    enabled,
  })
}

export function useScheduleAssignmentOptions(enabled: boolean) {
  return useQuery({
    queryKey: getListScheduleAssignmentOptionsQueryKey(),
    queryFn: ({ signal }) => listScheduleAssignmentOptions(signal),
    enabled,
  })
}

export function useAssignScheduleBatch() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      scheduleId,
      workerUserId,
      supervisorUserId,
    }: {
      scheduleId: string
      workerUserId: string
      supervisorUserId: string
    }) => assignScheduleBatch(scheduleId, { workerUserId, supervisorUserId }),
    onSuccess: async (_result, { scheduleId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getGetScheduleQueryKey(scheduleId),
        }),
        queryClient.invalidateQueries({
          queryKey: getListSchedulesQueryKey(),
        }),
      ])
    },
  })
}

function useReferences<T>(
  queryKey: readonly string[],
  load: (signal?: AbortSignal) => Promise<unknown>,
  parse: (value: ScheduleReferenceResponse[]) => T,
) {
  return useQuery<T>({
    queryKey,
    queryFn: ({ signal }) =>
      load(signal).then((value) => parse(value as ScheduleReferenceResponse[])),
  })
}

export function useScheduleStatuses() {
  return useReferences(
    getListScheduleStatusesQueryKey(),
    listScheduleStatuses,
    parseScheduleStatuses,
  )
}

export function useSchedulePeriodTypes() {
  return useReferences(
    getListSchedulePeriodTypesQueryKey(),
    listSchedulePeriodTypes,
    parseSchedulePeriodTypes,
  )
}

export function useScheduleQuarters() {
  return useReferences(
    getListScheduleQuartersQueryKey(),
    listScheduleQuarters,
    parseScheduleQuarters,
  )
}
