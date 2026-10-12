import { useQuery } from '@tanstack/react-query'
import { httpClient } from '@/api/http-client'
import {
  getGetInspectionHistoryQueryKey,
  getGetInspectionRegistryOptionsQueryKey,
  getGetInspectionRegistrySummaryQueryKey,
  getGetInspectionQueryKey,
  getInspection,
  getInspectionHistory,
  getListInspectionsQueryKey,
  getListInspectionRegistryQueryKey,
  getInspectionRegistryOptions,
  getInspectionRegistrySummary,
  listInspectionRegistry,
  listInspections,
} from '@/api/generated/endpoints'
import type {
  GetInspectionRegistryOptionsParams,
  ListInspectionsParams,
  ListInspectionRegistryParams,
} from '@/api/generated/models'
import {
  parseInspection,
  parseInspectionHistory,
  parseInspectionRegistryOptions,
  parseInspectionRegistryPage,
  parseInspectionRegistrySummary,
  parseInspections,
} from '@/features/inspections/inspection-contract'

export type InspectionServerFilters = ListInspectionsParams
export type InspectionRegistryFilters = ListInspectionRegistryParams

export function useInspections(filters: InspectionServerFilters = {}) {
  return useQuery({
    queryKey: getListInspectionsQueryKey(filters),
    queryFn: ({ signal }) =>
      listInspections(filters, signal).then(parseInspections),
    placeholderData: (previousData) => previousData,
  })
}

export function useInspectionRegistry(filters: InspectionRegistryFilters = {}) {
  return useQuery({
    queryKey: getListInspectionRegistryQueryKey(filters),
    queryFn: ({ signal }) =>
      listInspectionRegistry(filters, signal).then(parseInspectionRegistryPage),
    placeholderData: (previousData) => previousData,
  })
}

export function useInspectionRegistryOptions(
  filters: GetInspectionRegistryOptionsParams = {},
) {
  return useQuery({
    queryKey: getGetInspectionRegistryOptionsQueryKey(filters),
    queryFn: ({ signal }) =>
      getInspectionRegistryOptions(filters, signal).then(
        parseInspectionRegistryOptions,
      ),
  })
}

export function useInspectionRegistrySummary() {
  return useQuery({
    queryKey: getGetInspectionRegistrySummaryQueryKey(),
    queryFn: ({ signal }) =>
      getInspectionRegistrySummary(signal).then(parseInspectionRegistrySummary),
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

export function useInspectionPhotoEvidence(
  inspectionId: string,
  enabled = true,
) {
  return useQuery({
    queryKey: ['inspection-photo-evidence', inspectionId],
    queryFn: async ({ signal }) => {
      const response = await httpClient.get<Blob>(
        `/api/v1/inspections/${inspectionId}/photo`,
        { responseType: 'blob', signal },
      )
      return response.data
    },
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
