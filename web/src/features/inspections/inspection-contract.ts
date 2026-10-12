import { z } from 'zod'
import type {
  InspectionHistoryResponse,
  InspectionResponse,
} from '@/api/generated/models'

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

export function parseInspection(value: InspectionResponse): Inspection {
  return inspectionSchema.parse(value)
}

export function parseInspections(values: InspectionResponse[]): Inspection[] {
  return z.array(inspectionSchema).parse(values)
}

export function parseInspectionHistory(
  values: InspectionHistoryResponse[],
): InspectionHistory[] {
  return z.array(inspectionHistorySchema).parse(values)
}
