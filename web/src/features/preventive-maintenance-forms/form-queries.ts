import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getCorrectiveMaintenanceHandoff,
  getGetCorrectiveMaintenanceHandoffQueryKey,
  getGetPmPeriodDashboardQueryKey,
  getGetPreventiveMaintenanceFormQueryKey,
  getListPreventiveMaintenanceFormsQueryKey,
  listPreventiveMaintenanceForms,
  getPreventiveMaintenanceForm,
  useAcknowledgePreventiveMaintenanceForm,
} from '@/api/generated/endpoints'
import type { ListPreventiveMaintenanceFormsParams } from '@/api/generated/models'
import {
  parseCorrectiveMaintenanceHandoff,
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
