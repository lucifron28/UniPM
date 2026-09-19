import { z } from 'zod'
import type {
  InspectionHistoryResponse,
  InspectionResponse,
} from '@/api/generated/models'

const sourceText = z.string().max(2_000).nullable()

export const inspectionSchema = z
  .object({
    id: z.string().uuid(),
    scheduleId: z.string().uuid(),
    assetId: z.string().uuid(),
    inspectorUserId: z.string().uuid(),
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
