import { z } from 'zod'
import type {
  InspectionHistoryResponse,
  InspectionResponse,
  InspectionRegistryOptionsResponse,
  InspectionRegistryPageResponse,
  InspectionRegistrySummaryResponse,
} from '@/api/generated/models'
import { assetCategorySchema } from '@/features/assets/asset-contract'
import { scheduleQuarterCodes } from '@/features/schedules/schedule-contract'

const sourceText = z.string().max(2_000).nullable()
export const inspectionFollowUpStatusCodes = [
  'NoReferralRequired',
  'CorrectiveFollowUpPending',
  'ReferredToWms',
] as const
const followUpStatus = z.enum(inspectionFollowUpStatusCodes)
const revision = z.union([
  z.number().int().nonnegative(),
  z.string().regex(/^\d+$/).transform(Number),
])

export const inspectionSchema = z
  .object({
    id: z.string().uuid(),
    scheduleId: z.string().uuid(),
    assetId: z.string().uuid(),
    inspectorUserId: z.string().uuid(),
    dateInspected: z.string().datetime({ offset: true }),
    isOperational: z.boolean(),
    hasPhotoEvidence: z.boolean(),
    remarks: sourceText,
    actionsRecommendations: sourceText,
    dateAccomplished: z
      .string()
      .datetime({ offset: true })
      .nullable()
      .optional(),
    waterReplaceCarbonFilter: z.boolean().nullable().optional(),
    waterReplaceSedimentFilter: z.boolean().nullable().optional(),
    waterCheckUvLight: z.boolean().nullable().optional(),
    externalPmNumber: z.string().max(128).nullable(),
    wmsReferralRevision: revision,
    correctiveFollowUpStatus: followUpStatus,
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
  })
  .strict()

export const inspectionHistorySchema = z
  .object({
    id: z.string().uuid(),
    dateInspected: z.string().datetime({ offset: true }),
    isOperational: z.boolean(),
    remarks: sourceText,
    actionsRecommendations: sourceText,
    dateAccomplished: z
      .string()
      .datetime({ offset: true })
      .nullable()
      .optional(),
    waterReplaceCarbonFilter: z.boolean().nullable().optional(),
    waterReplaceSedimentFilter: z.boolean().nullable().optional(),
    waterCheckUvLight: z.boolean().nullable().optional(),
  })
  .strict()

export type Inspection = z.infer<typeof inspectionSchema>
export type InspectionHistory = z.infer<typeof inspectionHistorySchema>
export type InspectionFollowUpStatus = z.infer<typeof followUpStatus>

const inspectionRegistryAssetSchema = z
  .object({
    id: z.string().uuid(),
    assetCode: z.string().trim().min(1).max(64),
    assetCategory: z.string().trim().min(1).max(64),
    department: z.string().max(256).nullable(),
    building: z.string().max(256).nullable(),
    location: z.string().max(256).nullable(),
  })
  .strict()

const inspectionRegistryScheduleSchema = z
  .object({
    id: z.string().uuid(),
    pmCycle: z.string().regex(/^\d{4}-(0[1-9]|1[0-2])$/),
  })
  .strict()

const inspectionRegistryItemSchema = z
  .object({
    id: z.string().uuid(),
    scheduleId: z.string().uuid(),
    assetId: z.string().uuid(),
    dateInspected: z.string().datetime({ offset: true }),
    isOperational: z.boolean(),
    remarks: sourceText,
    actionsRecommendations: sourceText,
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    dateAccomplished: z.string().datetime({ offset: true }).nullable(),
    waterReplaceCarbonFilter: z.boolean().nullable(),
    waterReplaceSedimentFilter: z.boolean().nullable(),
    waterCheckUvLight: z.boolean().nullable(),
    externalPmNumber: z.string().max(128).nullable(),
    wmsReferralRevision: revision,
    correctiveFollowUpStatus: followUpStatus,
    hasPhotoEvidence: z.boolean(),
    asset: inspectionRegistryAssetSchema.nullable(),
    schedule: inspectionRegistryScheduleSchema.nullable(),
  })
  .strict()

const inspectionRegistryPageSchema = z
  .object({
    items: z.array(inspectionRegistryItemSchema),
    page: z.coerce.number().int().positive(),
    pageSize: z.coerce.number().int().positive(),
    totalCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

const inspectionRegistryOptionsSchema = z
  .object({
    categories: z.array(assetCategorySchema),
    quarters: z.array(z.enum(scheduleQuarterCodes)),
    wmsReferralStatuses: z.array(followUpStatus),
    departments: z.array(z.string().trim().min(1).max(256)),
    years: z.array(z.string().regex(/^\d{4}$/)),
  })
  .strict()

const inspectionRegistrySummarySchema = z
  .object({
    totalCount: z.coerce.number().int().nonnegative(),
    operationalCount: z.coerce.number().int().nonnegative(),
    nonOperationalCount: z.coerce.number().int().nonnegative(),
    noReferralCount: z.coerce.number().int().nonnegative(),
    pendingFollowUpCount: z.coerce.number().int().nonnegative(),
    referredCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

export type InspectionRegistryItem = z.infer<
  typeof inspectionRegistryItemSchema
>
export type InspectionRegistryPage = z.infer<
  typeof inspectionRegistryPageSchema
>
export type InspectionRegistryOptions = z.infer<
  typeof inspectionRegistryOptionsSchema
>
export type InspectionRegistrySummary = z.infer<
  typeof inspectionRegistrySummarySchema
>

export function parseInspection(value: InspectionResponse): Inspection {
  return inspectionSchema.parse(value)
}

export function parseInspectionRegistryPage(
  value: InspectionRegistryPageResponse,
): InspectionRegistryPage {
  return inspectionRegistryPageSchema.parse(value)
}

export function parseInspectionRegistryOptions(
  value: InspectionRegistryOptionsResponse,
): InspectionRegistryOptions {
  return inspectionRegistryOptionsSchema.parse(value)
}

export function parseInspectionRegistrySummary(
  value: InspectionRegistrySummaryResponse,
): InspectionRegistrySummary {
  return inspectionRegistrySummarySchema.parse(value)
}

export function parseInspections(values: InspectionResponse[]): Inspection[] {
  return z.array(inspectionSchema).parse(values)
}

export function parseInspectionHistory(
  values: InspectionHistoryResponse[],
): InspectionHistory[] {
  return z.array(inspectionHistorySchema).parse(values)
}
