import { useQuery } from '@tanstack/react-query'
import {
  getGetInspectionHistoryQueryKey,
  getGetInspectionQueryKey,
  getInspection,
  getInspectionHistory,
  getListInspectionsQueryKey,
  listInspections,
} from '@/api/generated/endpoints'
import type { ListInspectionsParams } from '@/api/generated/models'
import {
  parseInspection,
  parseInspectionHistory,
  parseInspections,
} from '@/features/inspections/inspection-contract'

export type InspectionServerFilters = ListInspectionsParams

export function useInspections(filters: InspectionServerFilters = {}) {
  return useQuery({
    queryKey: getListInspectionsQueryKey(filters),
    queryFn: ({ signal }) =>
      listInspections(filters, signal).then(parseInspections),
  })
}

export function useInspection(inspectionId: string, enabled = true) {
  return useQuery({
    queryKey: getGetInspectionQueryKey(inspectionId),
    queryFn: ({ signal }) =>
      getInspection(inspectionId, signal).then(parseInspection),
    enabled,
  })
}

export function useInspectionHistory(assetId: string, enabled = true) {
  return useQuery({
    queryKey: getGetInspectionHistoryQueryKey(assetId),
    queryFn: ({ signal }) =>
      getInspectionHistory(assetId, signal).then(parseInspectionHistory),
    enabled,
  })
}
