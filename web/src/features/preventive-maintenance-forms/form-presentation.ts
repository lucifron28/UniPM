import type { PreventiveMaintenanceForm } from '@/features/preventive-maintenance-forms/form-contract'

export function formatFormDate(value: string | null | undefined) {
  if (!value) return 'Not recorded'

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function formatFormPeriod(
  form: Pick<
    PreventiveMaintenanceForm,
    'periodType' | 'quarter' | 'semester' | 'year' | 'academicYear'
  >,
) {
  const detail = form.quarter ?? form.semester ?? form.academicYear
  const year = form.year === null ? undefined : String(form.year)
  return [form.periodType, detail, year].filter(Boolean).join(' / ')
}

export function formStatusLabel(status: PreventiveMaintenanceForm['status']) {
  return status
}

export function formStatusClass(status: PreventiveMaintenanceForm['status']) {
  switch (status) {
    case 'Draft':
      return 'bg-slate-100 text-slate-700'
    case 'Submitted':
      return 'bg-amber-100 text-amber-800'
    case 'Acknowledged':
      return 'bg-emerald-100 text-emerald-800'
  }
}

export function inspectionConditionLabel(isOperational: boolean) {
  return isOperational ? 'Operational' : 'Not operational'
}
