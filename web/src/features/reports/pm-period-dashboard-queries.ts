import { useQuery } from '@tanstack/react-query'
import {
  getGetPmPeriodDashboardQueryKey,
  getPmPeriodDashboard,
  getListPmPeriodDashboardCyclesQueryKey,
  listPmPeriodDashboardCycles,
} from '@/api/generated/endpoints'
import type {
  GetPmPeriodDashboardParams,
  ListPmPeriodDashboardCyclesParams,
} from '@/api/generated/models'

export type PmPeriodDashboardCycleFilters = ListPmPeriodDashboardCyclesParams

export type PmPeriodDashboardFilters = GetPmPeriodDashboardParams

export function usePmPeriodDashboardCycles(
  filters?: PmPeriodDashboardCycleFilters,
) {
  return useQuery({
    queryKey: getListPmPeriodDashboardCyclesQueryKey(filters),
    queryFn: ({ signal }) => listPmPeriodDashboardCycles(filters, signal),
  })
}

export function usePmPeriodDashboard(
  filters: PmPeriodDashboardFilters | undefined,
) {
  const enabled = Boolean(filters?.pmCycle && filters?.assetCategory)

  return useQuery({
    queryKey: getGetPmPeriodDashboardQueryKey(filters),
    queryFn: ({ signal }) => getPmPeriodDashboard(filters, signal),
    enabled,
  })
}
