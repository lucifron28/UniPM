import { useQuery } from '@tanstack/react-query'
import {
  getGetScheduleQueryKey,
  getListSchedulePeriodTypesQueryKey,
  getListScheduleQuartersQueryKey,
  getListScheduleStatusesQueryKey,
  getListSchedulesQueryKey,
  getSchedule,
  listSchedulePeriodTypes,
  listScheduleQuarters,
  listScheduleStatuses,
  listSchedules,
} from '@/api/generated/endpoints'
import type { ListSchedulesParams } from '@/api/generated/models'
import {
  parseSchedule,
  parseScheduleReferences,
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

function useReferences(
  queryKey: readonly string[],
  load: (signal?: AbortSignal) => Promise<unknown>,
) {
  return useQuery({
    queryKey,
    queryFn: ({ signal }) =>
      load(signal).then((value) =>
        parseScheduleReferences(
          value as Parameters<typeof parseScheduleReferences>[0],
        ),
      ),
  })
}

export function useScheduleStatuses() {
  return useReferences(getListScheduleStatusesQueryKey(), listScheduleStatuses)
}

export function useSchedulePeriodTypes() {
  return useReferences(
    getListSchedulePeriodTypesQueryKey(),
    listSchedulePeriodTypes,
  )
}

export function useScheduleQuarters() {
  return useReferences(getListScheduleQuartersQueryKey(), listScheduleQuarters)
}
