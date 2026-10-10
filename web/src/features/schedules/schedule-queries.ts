import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  assignScheduleBatchSupervisor,
  assignScheduleBatchWorker,
  generatePreventiveMaintenanceSchedules,
  getGetPmPeriodDashboardQueryKey,
  getGetScheduleQueryKey,
  getListPmPeriodDashboardCyclesQueryKey,
  getListScheduleCoverageReviewQueryKey,
  getListScheduleEnrollmentDeferralsQueryKey,
  getListScheduleSupervisorAssignmentOptionsQueryKey,
  getListScheduleWorkerAssignmentOptionsQueryKey,
  getListSchedulePeriodTypesQueryKey,
  getListScheduleQuartersQueryKey,
  getListScheduleStatusesQueryKey,
  getListSchedulesQueryKey,
  getSchedule,
  listScheduleSupervisorAssignmentOptions,
  listScheduleWorkerAssignmentOptions,
  listScheduleCoverageReview,
  listSchedulePeriodTypes,
  listScheduleQuarters,
  listScheduleEnrollmentDeferrals,
  reviewScheduleEnrollmentDeferral,
  listScheduleStatuses,
  listSchedules,
} from '@/api/generated/endpoints'
import type {
  ListSchedulesParams,
  ListScheduleCoverageReviewParams,
  ListScheduleEnrollmentDeferralsParams,
  ScheduleEnrollmentDeferralReviewRequest,
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
    placeholderData: (previousData) => previousData,
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

export function useScheduleEnrollmentDeferrals(
  page = 1,
  enabled = true,
  filters: Omit<ListScheduleEnrollmentDeferralsParams, 'page'> = {},
) {
  const params = { page, pageSize: 10, ...filters }
  return useQuery({
    queryKey: getListScheduleEnrollmentDeferralsQueryKey(params),
    queryFn: ({ signal }) => listScheduleEnrollmentDeferrals(params, signal),
    enabled,
    placeholderData: (previousData) => previousData,
  })
}

export function useScheduleCoverageReview(page = 1, enabled = true) {
  const params: ListScheduleCoverageReviewParams = { page, pageSize: 10 }
  return useQuery({
    queryKey: getListScheduleCoverageReviewQueryKey(params),
    queryFn: ({ signal }) => listScheduleCoverageReview(params, signal),
    enabled,
    placeholderData: (previousData) => previousData,
  })
}

export function useReviewScheduleEnrollmentDeferral() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      assetId,
      pmCycle,
      request,
    }: {
      assetId: string
      pmCycle: string
      request: ScheduleEnrollmentDeferralReviewRequest
    }) => reviewScheduleEnrollmentDeferral(assetId, pmCycle, request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: getListScheduleEnrollmentDeferralsQueryKey(),
      })
    },
  })
}

export function useScheduleSupervisorAssignmentOptions(enabled: boolean) {
  return useQuery({
    queryKey: getListScheduleSupervisorAssignmentOptionsQueryKey(),
    queryFn: ({ signal }) => listScheduleSupervisorAssignmentOptions(signal),
    enabled,
  })
}

export function useScheduleWorkerAssignmentOptions(enabled: boolean) {
  return useQuery({
    queryKey: getListScheduleWorkerAssignmentOptionsQueryKey(),
    queryFn: ({ signal }) => listScheduleWorkerAssignmentOptions(signal),
    enabled,
  })
}

export function useAssignScheduleBatchSupervisor() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      scheduleId,
      supervisorUserId,
    }: {
      scheduleId: string
      supervisorUserId: string
    }) => assignScheduleBatchSupervisor(scheduleId, { supervisorUserId }),
    onSuccess: async (result, { scheduleId }) => {
      const count = result.scheduleIds.length
      toast.success(
        `Supervisor assignment saved for ${count} schedule${count === 1 ? '' : 's'}.`,
      )
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getGetScheduleQueryKey(scheduleId),
        }),
        queryClient.invalidateQueries({
          queryKey: getListSchedulesQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getListScheduleEnrollmentDeferralsQueryKey(),
        }),
      ])
    },
  })
}

export function useAssignScheduleBatchWorker() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      scheduleId,
      workerUserId,
    }: {
      scheduleId: string
      workerUserId: string
    }) => assignScheduleBatchWorker(scheduleId, { workerUserId }),
    onSuccess: async (result, { scheduleId }) => {
      const count = result.scheduleIds.length
      toast.success(
        `Inspector assignment saved for ${count} schedule${count === 1 ? '' : 's'}.`,
      )
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

export function useGenerateSchedules() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (year: number) =>
      generatePreventiveMaintenanceSchedules({ year }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getListSchedulesQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getListPmPeriodDashboardCyclesQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetPmPeriodDashboardQueryKey(),
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
