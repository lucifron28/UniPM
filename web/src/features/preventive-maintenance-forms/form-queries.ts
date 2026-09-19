import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getCorrectiveMaintenanceHandoff,
  getGetCorrectiveMaintenanceHandoffQueryKey,
  getGetPreventiveMaintenanceFormQueryKey,
  getListPreventiveMaintenanceFormsQueryKey,
  getPreventiveMaintenanceForm,
  listPreventiveMaintenanceForms,
  useAcknowledgePreventiveMaintenanceForm,
} from '@/api/generated/endpoints'
import {
  parseCorrectiveMaintenanceHandoff,
  parsePreventiveMaintenanceForm,
  parsePreventiveMaintenanceForms,
} from '@/features/preventive-maintenance-forms/form-contract'

export function usePreventiveMaintenanceForms(enabled = true) {
  return useQuery({
    queryKey: getListPreventiveMaintenanceFormsQueryKey(),
    queryFn: ({ signal }) =>
      listPreventiveMaintenanceForms(signal).then(
        parsePreventiveMaintenanceForms,
      ),
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
      },
    },
  })
}
