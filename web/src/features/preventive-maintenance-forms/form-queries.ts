import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '@/api/problem-details'
import {
  getCorrectiveMaintenanceHandoff,
  getGetCorrectiveMaintenanceHandoffQueryKey,
  getGetPmPeriodDashboardQueryKey,
  getGetInspectionQueryKey,
  getGetInspectionWmsReferralQueryKey,
  getGetPreventiveMaintenanceFormQueryKey,
  getListInspectionsQueryKey,
  getListPreventiveMaintenanceFormsQueryKey,
  getInspectionWmsReferral,
  listPreventiveMaintenanceForms,
  getPreventiveMaintenanceForm,
  useAcknowledgePreventiveMaintenanceForm,
  useUpdateInspectionWmsReferral,
} from '@/api/generated/endpoints'
import type { ListPreventiveMaintenanceFormsParams } from '@/api/generated/models'
import {
  parseCorrectiveMaintenanceHandoff,
  parseInspectionWmsReferralDetail,
  parsePreventiveMaintenanceForm,
  parsePreventiveMaintenanceForms,
} from '@/features/preventive-maintenance-forms/form-contract'

export function usePreventiveMaintenanceForms(
  filters: ListPreventiveMaintenanceFormsParams = {},
  enabled = true,
) {
  return useQuery({
    queryKey: getListPreventiveMaintenanceFormsQueryKey(filters),
    queryFn: ({ signal }) =>
      listPreventiveMaintenanceForms(filters, signal).then(
        parsePreventiveMaintenanceForms,
      ),
    placeholderData: (previousData) => previousData,
    enabled,
  })
}

export function usePreventiveMaintenanceForm(formId: string, enabled = true) {
  return useQuery({
    queryKey: getGetPreventiveMaintenanceFormQueryKey(formId),
    queryFn: ({ signal }) =>
      getPreventiveMaintenanceForm(formId, signal).then(
        parsePreventiveMaintenanceForm,
      ),
    enabled,
  })
}

export function useCorrectiveMaintenanceHandoff(
  formId: string,
  enabled = true,
) {
  return useQuery({
    queryKey: getGetCorrectiveMaintenanceHandoffQueryKey(formId),
    queryFn: ({ signal }) =>
      getCorrectiveMaintenanceHandoff(formId, signal).then(
        parseCorrectiveMaintenanceHandoff,
      ),
    enabled,
  })
}

export function useInspectionWmsReferral(inspectionId: string, enabled = true) {
  return useQuery({
    queryKey: getGetInspectionWmsReferralQueryKey(inspectionId),
    queryFn: ({ signal }) =>
      getInspectionWmsReferral(inspectionId, signal).then(
        parseInspectionWmsReferralDetail,
      ),
    enabled,
  })
}

export function useUpdateInspectionWmsReferralMutation(formId: string) {
  const queryClient = useQueryClient()

  return useUpdateInspectionWmsReferral({
    mutation: {
      onSuccess: (_response, variables) => {
        void queryClient.invalidateQueries({
          queryKey: getGetInspectionWmsReferralQueryKey(variables.id),
        })
        void queryClient.invalidateQueries({
          queryKey: getGetInspectionQueryKey(variables.id),
        })
        void queryClient.invalidateQueries({
          queryKey: getGetCorrectiveMaintenanceHandoffQueryKey(formId),
        })
        void queryClient.invalidateQueries({
          queryKey: getListInspectionsQueryKey(),
        })
      },
      onError: (error, variables) => {
        if (error instanceof ApiError && error.status === 409) {
          void queryClient.invalidateQueries({
            queryKey: getGetInspectionWmsReferralQueryKey(variables.id),
          })
          void queryClient.invalidateQueries({
            queryKey: getGetCorrectiveMaintenanceHandoffQueryKey(formId),
          })
        }
      },
    },
  })
}

export function useAcknowledgePreventiveMaintenanceFormMutation() {
  const queryClient = useQueryClient()

  return useAcknowledgePreventiveMaintenanceForm({
    mutation: {
      onSuccess: (_response, variables) => {
        void queryClient.invalidateQueries({
          queryKey: getGetPreventiveMaintenanceFormQueryKey(variables.id),
        })
        void queryClient.invalidateQueries({
          queryKey: getListPreventiveMaintenanceFormsQueryKey(),
        })
        void queryClient.invalidateQueries({
          queryKey: getGetCorrectiveMaintenanceHandoffQueryKey(variables.id),
        })
        void queryClient.invalidateQueries({
          queryKey: getGetPmPeriodDashboardQueryKey(),
        })
      },
    },
  })
}
