import { z } from 'zod'
import type {
  CorrectiveMaintenanceHandoffResponse,
  PreventiveMaintenanceFormResponse,
} from '@/api/generated/models'

export const preventiveMaintenanceFormStatusCodes = [
  'Draft',
  'Submitted',
  'Acknowledged',
] as const

const optionalText = z.string().nullable()

const inspectionRowSchema = z
  .object({
    id: z.string().uuid(),
    scheduleId: z.string().uuid(),
    assetId: z.string().uuid(),
    inspectorUserId: z.string().uuid(),
    dateInspected: z.string().datetime({ offset: true }),
    isOperational: z.boolean(),
    remarks: optionalText,
    actionsRecommendations: optionalText,
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
  })
  .strict()

export type PreventiveMaintenanceInspectionRow = z.infer<
  typeof inspectionRowSchema
>

const formSchema = z
  .object({
    id: z.string().uuid(),
    fileNumber: optionalText,
    assetCategory: z.string().trim().min(1),
    building: optionalText,
    department: optionalText,
    periodType: z.string().trim().min(1),
    quarter: optionalText,
    semester: optionalText,
    year: z
      .union([z.number().int(), z.string().regex(/^-?\d+$/)])
      .nullable()
      .transform((value) => (value === null ? null : Number(value))),
    academicYear: optionalText,
    status: z.enum(preventiveMaintenanceFormStatusCodes),
    createdByUserId: z.string().uuid(),
    submittedByUserId: z.string().uuid().nullable(),
    submittedAt: z.string().datetime({ offset: true }).nullable(),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    inspections: z.array(inspectionRowSchema),
  })
  .strict()

export type PreventiveMaintenanceForm = z.infer<typeof formSchema>

const handoffRowSchema = z
  .object({
    inspectionId: z.string().uuid(),
    inspectionDate: z.string().datetime({ offset: true }),
    assetDeviceNumber: z.string().nullable(),
    assetCode: z.string().trim().min(1),
    location: optionalText,
    findingOrRemarks: optionalText,
    isOperational: z.boolean(),
    recommendedCorrectiveAction: z.string().trim().min(1),
    skilledWorkerUserId: z.string().uuid(),
    skilledWorkerIdentity: optionalText,
  })
  .strict()

const handoffSchema = z
  .object({
    formId: z.string().uuid(),
    fileNumber: optionalText,
    acknowledgedAt: z.string().datetime({ offset: true }),
    department: optionalText,
    building: optionalText,
    assetCategory: z.string().trim().min(1),
    hasCorrectiveActionRows: z.boolean(),
    rows: z.array(handoffRowSchema),
  })
  .strict()

export type CorrectiveMaintenanceHandoff = z.infer<typeof handoffSchema>

export function parsePreventiveMaintenanceForms(
  values: PreventiveMaintenanceFormResponse[],
) {
  return z.array(formSchema).parse(values)
}

export function parsePreventiveMaintenanceForm(
  value: PreventiveMaintenanceFormResponse,
) {
  return formSchema.parse(value)
}

export function parseCorrectiveMaintenanceHandoff(
  value: CorrectiveMaintenanceHandoffResponse,
) {
  return handoffSchema.parse(value)
}

export function canReviewPreventiveMaintenanceForms(
  roles: readonly string[] | undefined,
) {
  return roles?.some((role) => role === 'GSD' || role === 'Inspector') ?? false
}

export function isGsdRole(roles: readonly string[] | undefined) {
  return roles?.includes('GSD') ?? false
}
