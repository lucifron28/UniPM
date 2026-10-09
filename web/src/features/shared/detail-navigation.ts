import { z } from 'zod'
import {
  assetCategoryCodes,
  assetStatusCodes,
} from '@/features/assets/asset-contract'
import type { AssetSearch } from '@/features/assets/asset-registry'
import type { InspectionSearch } from '@/features/inspections/inspection-registry'
import { inspectionFollowUpStatusCodes } from '@/features/inspections/inspection-contract'
import type { FormSearch } from '@/features/preventive-maintenance-forms/form-registry'
import { preventiveMaintenanceFormStatusCodes } from '@/features/preventive-maintenance-forms/form-contract'
import {
  scheduleQuarterCodes,
  scheduleStatusCodes,
} from '@/features/schedules/schedule-contract'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import type { PmPeriodDashboardSearch } from '@/features/reports/pm-period-dashboard'

export type DetailReturnContext =
  | { kind: 'dashboard'; search?: PmPeriodDashboardSearch | undefined }
  | { kind: 'assetRegistry'; search?: AssetSearch | undefined }
  | { kind: 'inspectionRegistry'; search?: InspectionSearch | undefined }
  | { kind: 'scheduleRegistry'; search?: ScheduleSearch | undefined }
  | { kind: 'formRegistry'; search?: FormSearch | undefined }
  | {
      kind: 'batchReview'
      formId: string
      search?: PmPeriodDashboardSearch | undefined
    }
  | {
      kind: 'assetDetail'
      assetId: string
      assetCode: string
      parent?: DetailReturnContext | undefined
    }
  | {
      kind: 'inspectionDetail'
      inspectionId: string
      parent?: DetailReturnContext | undefined
    }
  | {
      kind: 'scheduleDetail'
      scheduleId: string
      parent?: DetailReturnContext | undefined
    }

export type DetailReturnFallback = Extract<
  DetailReturnContext,
  {
    kind:
      | 'dashboard'
      | 'assetRegistry'
      | 'inspectionRegistry'
      | 'scheduleRegistry'
      | 'formRegistry'
      | 'batchReview'
  }
>

export type DetailReturnTarget =
  | { kind: 'dashboard'; search: PmPeriodDashboardSearch; label: string }
  | { kind: 'assetRegistry'; search: AssetSearch; label: string }
  | { kind: 'inspectionRegistry'; search: InspectionSearch; label: string }
  | { kind: 'scheduleRegistry'; search: ScheduleSearch; label: string }
  | { kind: 'formRegistry'; search: FormSearch; label: string }
  | {
      kind: 'batchReview'
      formId: string
      search: PmPeriodDashboardSearch
      returnContext: DetailReturnContext
      label: string
    }
  | {
      kind: 'assetDetail'
      assetId: string
      assetCode: string
      returnContext?: DetailReturnContext | undefined
      label: string
    }
  | {
      kind: 'inspectionDetail'
      inspectionId: string
      returnContext?: DetailReturnContext | undefined
      label: string
    }
  | {
      kind: 'scheduleDetail'
      scheduleId: string
      returnContext?: DetailReturnContext | undefined
      label: string
    }

const pageSchema = z.coerce.number().int().positive().max(10000).optional()
const dashboardSearchSchema = z.object({
  assetCategory: z.string().trim().max(128).optional(),
  year: z.coerce.number().int().positive().optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
  department: z.string().trim().max(256).optional(),
  condition: z
    .enum(['Operational', 'NonOperational', 'NotInspected'])
    .optional(),
  timeliness: z
    .enum(['OnTime', 'Late', 'Scheduled', 'Pending', 'NotCompleted'])
    .optional(),
  search: z.string().trim().max(256).optional(),
})
const assetSearchSchema = z.object({
  assetCategory: z.enum(assetCategoryCodes).optional(),
  status: z.enum(assetStatusCodes).optional(),
  building: z.string().trim().max(256).optional(),
  department: z.string().trim().max(256).optional(),
  text: z.string().trim().max(256).optional(),
  page: pageSchema,
})
const inspectionSearchSchema = z.object({
  assetId: z.string().uuid().optional(),
  scheduleId: z.string().uuid().optional(),
  assetCategory: z.enum(assetCategoryCodes).optional(),
  department: z.string().trim().max(256).optional(),
  search: z.string().trim().max(256).optional(),
  isOperational: z.boolean().optional(),
  wmsReferralStatus: z.enum(inspectionFollowUpStatusCodes).optional(),
  dateFrom: z.string().datetime({ offset: true }).optional(),
  dateTo: z.string().datetime({ offset: true }).optional(),
  page: pageSchema,
})
const scheduleSearchSchema = z.object({
  assetId: z.string().uuid().optional(),
  assetCategory: z.enum(assetCategoryCodes).optional(),
  department: z.string().trim().max(256).optional(),
  search: z.string().trim().max(256).optional(),
  status: z.enum(scheduleStatusCodes).optional(),
  from: z.string().datetime({ offset: true }).optional(),
  to: z.string().datetime({ offset: true }).optional(),
  quarter: z.enum(scheduleQuarterCodes).optional(),
  year: z.coerce
    .number()
    .int()
    .min(2000)
    .max(new Date().getUTCFullYear() + 5)
    .optional(),
  page: pageSchema,
})
const formSearchSchema = z.object({
  status: z.enum(preventiveMaintenanceFormStatusCodes).optional(),
  assetCategory: z.enum(assetCategoryCodes).optional(),
  department: z.string().trim().max(256).optional(),
  pmCycle: z
    .string()
    .regex(/^(?!0000)\d{4}-(0[1-9]|1[0-2])$/)
    .optional(),
  search: z.string().trim().max(256).optional(),
  page: pageSchema,
})

const entityKindSchema = z.enum([
  'assetDetail',
  'inspectionDetail',
  'scheduleDetail',
])
const detailReturnContextSchema: z.ZodType<DetailReturnContext> = z.lazy(
  () =>
    z.union([
      z.object({
        kind: z.literal('dashboard'),
        search: dashboardSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('assetRegistry'),
        search: assetSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('inspectionRegistry'),
        search: inspectionSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('scheduleRegistry'),
        search: scheduleSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('formRegistry'),
        search: formSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('batchReview'),
        formId: z.string().uuid(),
        search: dashboardSearchSchema.optional(),
      }),
      z.object({
        kind: z.literal('assetDetail'),
        assetId: z.string().uuid(),
        assetCode: z.string().trim().min(1).max(128),
        parent: detailReturnContextSchema.optional(),
      }),
      z.object({
        kind: z.literal('inspectionDetail'),
        inspectionId: z.string().uuid(),
        parent: detailReturnContextSchema.optional(),
      }),
      z.object({
        kind: z.literal('scheduleDetail'),
        scheduleId: z.string().uuid(),
        parent: detailReturnContextSchema.optional(),
      }),
    ]) as z.ZodType<DetailReturnContext>,
)

const maxEntityHops = 4

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function hasBoundedEntityDepth(value: unknown) {
  let current = value
  let hops = 0
  while (
    isRecord(current) &&
    entityKindSchema.safeParse(current.kind).success
  ) {
    hops += 1
    if (hops > maxEntityHops) return false
    current = current.parent
  }
  return true
}

export function parseDetailReturnContext(
  value: unknown,
): DetailReturnContext | undefined {
  if (!hasBoundedEntityDepth(value)) return undefined
  const parsed = detailReturnContextSchema.safeParse(value)
  return parsed.success ? parsed.data : undefined
}

function isEntityContext(
  context: DetailReturnContext,
): context is Extract<
  DetailReturnContext,
  { kind: z.infer<typeof entityKindSchema> }
> {
  return entityKindSchema.safeParse(context.kind).success
}

function entityDepth(context: DetailReturnContext | undefined): number {
  if (!context || !isEntityContext(context)) return 0
  return 1 + entityDepth(context.parent)
}

function terminalOrigin(
  context: DetailReturnContext | undefined,
): DetailReturnFallback | undefined {
  if (!context) return undefined
  if (!isEntityContext(context)) return context
  return terminalOrigin(context.parent)
}

function removeRepeatedEntity(
  context: DetailReturnContext | undefined,
  kind: Extract<
    DetailReturnContext,
    { kind: z.infer<typeof entityKindSchema> }
  >['kind'],
  id: string,
): DetailReturnContext | undefined {
  if (!context) return undefined
  if (context.kind === kind) {
    const contextId =
      context.kind === 'assetDetail'
        ? context.assetId
        : context.kind === 'inspectionDetail'
          ? context.inspectionId
          : context.scheduleId
    if (contextId === id) return context.parent
  }
  if (!isEntityContext(context)) return context
  const parent = removeRepeatedEntity(context.parent, kind, id)
  return { ...context, parent }
}

function normalizeParent(
  kind: Extract<
    DetailReturnContext,
    { kind: z.infer<typeof entityKindSchema> }
  >['kind'],
  id: string,
  parent: DetailReturnContext | undefined,
  fallback: DetailReturnFallback,
): DetailReturnContext {
  let normalized = removeRepeatedEntity(parent, kind, id) ?? fallback
  if (entityDepth(normalized) >= maxEntityHops) {
    const origin = terminalOrigin(normalized) ?? fallback
    if (isEntityContext(normalized)) {
      normalized = { ...normalized, parent: origin }
    } else {
      normalized = origin
    }
  }
  return normalized
}

export function assetDetailReturnContext(
  assetId: string,
  assetCode: string,
  parent: DetailReturnContext | undefined,
  fallback: DetailReturnFallback,
): DetailReturnContext {
  return {
    kind: 'assetDetail',
    assetId,
    assetCode,
    parent: normalizeParent('assetDetail', assetId, parent, fallback),
  }
}

export function inspectionDetailReturnContext(
  inspectionId: string,
  parent: DetailReturnContext | undefined,
  fallback: DetailReturnFallback,
): DetailReturnContext {
  return {
    kind: 'inspectionDetail',
    inspectionId,
    parent: normalizeParent('inspectionDetail', inspectionId, parent, fallback),
  }
}

export function scheduleDetailReturnContext(
  scheduleId: string,
  parent: DetailReturnContext | undefined,
  fallback: DetailReturnFallback,
): DetailReturnContext {
  return {
    kind: 'scheduleDetail',
    scheduleId,
    parent: normalizeParent('scheduleDetail', scheduleId, parent, fallback),
  }
}

export function resolveDetailReturn(
  context: DetailReturnContext | undefined,
  fallback: DetailReturnFallback,
): DetailReturnTarget {
  const origin = context ?? fallback
  switch (origin.kind) {
    case 'dashboard':
      return {
        kind: origin.kind,
        search: origin.search ?? {},
        label: 'Back to PM dashboard',
      }
    case 'assetRegistry':
      return {
        kind: origin.kind,
        search: origin.search ?? {},
        label: 'Back to assets',
      }
    case 'inspectionRegistry':
      return {
        kind: origin.kind,
        search: origin.search ?? {},
        label: 'Back to inspections',
      }
    case 'scheduleRegistry':
      return {
        kind: origin.kind,
        search: origin.search ?? {},
        label: 'Back to schedules',
      }
    case 'formRegistry':
      return {
        kind: origin.kind,
        search: origin.search ?? {},
        label: 'Back to form review',
      }
    case 'batchReview':
      return {
        kind: origin.kind,
        formId: origin.formId,
        search: origin.search ?? {},
        returnContext: {
          kind: 'dashboard',
          search: origin.search ?? {},
        },
        label: 'Back to batch review',
      }
    case 'assetDetail':
      return {
        kind: origin.kind,
        assetId: origin.assetId,
        assetCode: origin.assetCode,
        returnContext: origin.parent,
        label: `Back to ${origin.assetCode}`,
      }
    case 'inspectionDetail':
      return {
        kind: origin.kind,
        inspectionId: origin.inspectionId,
        returnContext: origin.parent,
        label: 'Back to inspection',
      }
    case 'scheduleDetail':
      return {
        kind: origin.kind,
        scheduleId: origin.scheduleId,
        returnContext: origin.parent,
        label: 'Back to schedule',
      }
  }
}
