import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  assignScheduleBatchSupervisor,
  assignScheduleBatchWorker,
  generatePreventiveMaintenanceSchedules,
  getGetScheduleRegistryOptionsQueryKey,
  getGetScheduleRegistrySummaryQueryKey,
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
  getListScheduleRegistryQueryKey,
  getListSchedulesQueryKey,
  getScheduleRegistryOptions,
  getScheduleRegistrySummary,
  getSchedule,
  listScheduleSupervisorAssignmentOptions,
  listScheduleWorkerAssignmentOptions,
  listScheduleCoverageReview,
  listSchedulePeriodTypes,
  listScheduleQuarters,
  listScheduleEnrollmentDeferrals,
  reviewScheduleEnrollmentDeferral,
  listScheduleStatuses,
  listScheduleRegistry,
  listSchedules,
} from '@/api/generated/endpoints'
import type {
  GetScheduleRegistryOptionsParams,
  ListScheduleSupervisorAssignmentOptionsParams,
  ListScheduleWorkerAssignmentOptionsParams,
  ListScheduleRegistryParams,
  ListSchedulesParams,
  ListScheduleCoverageReviewParams,
  ListScheduleEnrollmentDeferralsParams,
  ScheduleEnrollmentDeferralReviewRequest,
  ScheduleReferenceResponse,
} from '@/api/generated/models'
import {
  parseSchedulePeriodTypes,
  parseScheduleRegistryOptions,
  parseScheduleRegistryPage,
  parseScheduleRegistrySummary,
  parseScheduleQuarters,
  parseScheduleStatuses,
  parseSchedule,
  parseSchedules,
} from '@/features/schedules/schedule-contract'

export type ScheduleServerFilters = ListSchedulesParams
export type ScheduleRegistryFilters = ListScheduleRegistryParams

export function useSchedules(filters: ScheduleServerFilters = {}) {
  return useQuery({
    queryKey: getListSchedulesQueryKey(filters),
    queryFn: ({ signal }) =>
      listSchedules(filters, signal).then(parseSchedules),
    placeholderData: (previousData) => previousData,
  })
}

export function useScheduleRegistry(filters: ScheduleRegistryFilters = {}) {
  return useQuery({
    queryKey: getListScheduleRegistryQueryKey(filters),
    queryFn: ({ signal }) =>
      listScheduleRegistry(filters, signal).then(parseScheduleRegistryPage),
    placeholderData: (previousData) => previousData,
  })
}

export function useScheduleRegistryOptions(
  filters: GetScheduleRegistryOptionsParams = {},
) {
  return useQuery({
    queryKey: getGetScheduleRegistryOptionsQueryKey(filters),
    queryFn: ({ signal }) =>
      getScheduleRegistryOptions(filters, signal).then(
        parseScheduleRegistryOptions,
      ),
  })
}

export function useScheduleRegistrySummary() {
  return useQuery({
    queryKey: getGetScheduleRegistrySummaryQueryKey(),
    queryFn: ({ signal }) =>
      getScheduleRegistrySummary(signal).then(parseScheduleRegistrySummary),
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

export function useScheduleSupervisorAssignmentOptions(
  enabled: boolean,
  filters: Pick<
    ListScheduleSupervisorAssignmentOptionsParams,
    'search' | 'selectedUserId'
  > = {},
) {
  const params = { ...filters, pageSize: 20 }
  return useInfiniteQuery({
    queryKey: getListScheduleSupervisorAssignmentOptionsQueryKey(params),
    queryFn: ({ signal, pageParam }) =>
      listScheduleSupervisorAssignmentOptions(
        { ...params, page: pageParam },
        signal,
      ),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const page = Number(lastPage.page)
      return page * Number(lastPage.pageSize) < Number(lastPage.totalCount)
        ? page + 1
        : undefined
    },
    enabled,
  })
}

export function useScheduleWorkerAssignmentOptions(
  enabled: boolean,
  filters: Pick<
    ListScheduleWorkerAssignmentOptionsParams,
    'search' | 'selectedUserId'
  > = {},
) {
  const params = { ...filters, pageSize: 20 }
  return useInfiniteQuery({
    queryKey: getListScheduleWorkerAssignmentOptionsQueryKey(params),
    queryFn: ({ signal, pageParam }) =>
      listScheduleWorkerAssignmentOptions(
        { ...params, page: pageParam },
        signal,
      ),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const page = Number(lastPage.page)
      return page * Number(lastPage.pageSize) < Number(lastPage.totalCount)
        ? page + 1
        : undefined
    },
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
          queryKey: getListScheduleRegistryQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistryOptionsQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistrySummaryQueryKey(),
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
        queryClient.invalidateQueries({
          queryKey: getListScheduleRegistryQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistryOptionsQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistrySummaryQueryKey(),
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
          queryKey: getListScheduleRegistryQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistryOptionsQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetScheduleRegistrySummaryQueryKey(),
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
