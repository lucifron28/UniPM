import { z } from 'zod'
import type {
  CreateScheduleDto,
  ScheduleRegistryItemResponse,
  ScheduleRegistryOptionsResponse,
  ScheduleRegistryPageResponse,
  ScheduleRegistrySummaryResponse,
  ScheduleAssignmentQueueResponse,
  ScheduleReferenceResponse,
  ScheduleResponse,
} from '@/api/generated/models'
import {
  assetCategoryCodes,
  assetCategorySchema,
} from '@/features/assets/asset-contract'
import { getCurrentManilaYear } from '@/features/schedules/schedule-presentation'

export const scheduleStatusCodes = [
  'Due',
  'Ongoing',
  'Completed',
  'Overdue',
  'Cancelled',
] as const
export const schedulePeriodTypeCodes = [
  'Quarter',
  'Semester',
  'Annual',
  'Custom',
] as const
export const scheduleQuarterCodes = ['Q1', 'Q2', 'Q3', 'Q4'] as const
export type ScheduleQuarter = (typeof scheduleQuarterCodes)[number]
export const scheduleSemesterCodes = ['First', 'Second', 'Summer'] as const

export function quartersForScheduledMonths(
  months: readonly number[],
): Set<ScheduleQuarter> {
  return new Set(
    months.map(
      (month) => `Q${Math.floor((month - 1) / 3) + 1}` as ScheduleQuarter,
    ),
  )
}

const optionalText = z.string().max(256).nullable()
const optionalAssignmentTimestamp = z
  .string()
  .datetime({ offset: true })
  .nullable()
  .optional()
const optionalAssignmentPeriodStatus = z
  .string()
  .trim()
  .min(1)
  .max(32)
  .optional()
const scheduleAssetSchema = z
  .object({
    id: z.string().uuid(),
    assetCode: z.string().trim().min(1).max(64),
    assetCategory: z.string().trim().min(1).max(64),
    building: optionalText,
    department: optionalText,
    location: optionalText,
    status: z.string().trim().min(1).max(32),
    // Older cached schedules do not include the backend-issued QR value.
    qrCodeValue: optionalText.optional(),
  })
  .strict()

export const scheduleSchema = z
  .object({
    id: z.string().uuid(),
    assetId: z.string().uuid(),
    scheduleDate: z.string().datetime({ offset: true }),
    // Keep parsing older cached fixtures that predate the canonical PM cycle field.
    pmCycle: z
      .string()
      .regex(/^\d{4}-(0[1-9]|1[0-2])$/)
      .optional(),
    periodType: z.enum(schedulePeriodTypeCodes),
    status: z.enum(scheduleStatusCodes),
    quarter: z.enum(scheduleQuarterCodes).nullable(),
    semester: z.enum(scheduleSemesterCodes).nullable(),
    year: z
      .union([z.number().int(), z.string().regex(/^-?\d+$/)])
      .nullable()
      .transform((value) => (value === null ? null : Number(value))),
    academicYear: z.string().max(32).nullable(),
    assignedToUserId: z.string().uuid().nullable(),
    assignedSupervisorUserId: z.string().uuid().nullable(),
    firstSupervisorAssignedAt: optionalAssignmentTimestamp,
    firstInspectorAssignedAt: optionalAssignmentTimestamp,
    inspectorAssignmentDueAt: optionalAssignmentTimestamp,
    assignmentPeriodStatus: optionalAssignmentPeriodStatus,
    completedAt: z.string().datetime({ offset: true }).nullable(),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    asset: scheduleAssetSchema.nullable(),
  })
  .strict()

export type Schedule = z.infer<typeof scheduleSchema>

const scheduleAssignmentStateSchema = z.enum([
  'Unassigned',
  'Assigned',
  'Partially assigned',
  'Mixed',
])

const scheduleAssignmentQueueBatchSchema = z
  .object({
    batchKey: z.string().trim().min(1).max(128),
    department: z.string().trim().min(1).max(256),
    assetCategory: z.enum(assetCategoryCodes),
    pmCycle: z.string().regex(/^\d{4}-(0[1-9]|1[0-2])$/),
    scheduleCount: z.number().int().nonnegative(),
    unfinishedScheduleCount: z.number().int().nonnegative(),
    activeUnfinishedScheduleCount: z.number().int().nonnegative().optional(),
    completedCount: z.number().int().nonnegative(),
    ongoingCount: z.number().int().nonnegative(),
    overdueCount: z.number().int().nonnegative(),
    cancelledCount: z.number().int().nonnegative().optional(),
    activeScheduleCount: z.number().int().nonnegative().optional(),
    supervisorState: scheduleAssignmentStateSchema,
    supervisorUserId: z.string().uuid().nullable(),
    supervisorDisplayName: z.string().nullable(),
    inspectorState: scheduleAssignmentStateSchema,
    inspectorUserId: z.string().uuid().nullable(),
    inspectorDisplayName: z.string().nullable(),
    attention: z
      .object({
        state: z.enum(['Pending', 'Needed', 'Soon', 'Urgent', 'MonthEnded']),
        priority: z.number().int().min(0).max(2),
        daysUntilMonthEnd: z.number().int(),
      })
      .strict()
      .nullable()
      .optional(),
    assignmentLocked: z.boolean(),
    supervisorOwnershipIsConsistent: z.boolean(),
    buildings: z.array(z.string().trim().min(1).max(256)),
    representativeSchedule: scheduleSchema,
  })
  .strict()
  .transform((batch) => ({
    ...batch,
    activeUnfinishedScheduleCount:
      batch.activeUnfinishedScheduleCount ?? batch.unfinishedScheduleCount,
    cancelledCount: batch.cancelledCount ?? 0,
    activeScheduleCount: batch.activeScheduleCount ?? batch.scheduleCount,
  }))

const scheduleAssignmentQueueSchema = z
  .object({
    totalScheduleCount: z.number().int().nonnegative(),
    totalBatchCount: z.number().int().nonnegative().optional(),
    page: z.number().int().positive().optional(),
    pageSize: z.number().int().positive().optional(),
    summary: z
      .object({
        notStartedBatchCount: z.number().int().nonnegative(),
        inProgressBatchCount: z.number().int().nonnegative(),
        ongoingScheduleCount: z.number().int().nonnegative(),
        overdueScheduleCount: z.number().int().nonnegative(),
        pendingAssignmentBatchCount: z.number().int().nonnegative().optional(),
        urgentAssignmentBatchCount: z.number().int().nonnegative().optional(),
        attentionDate: z.string().optional(),
        attentionRole: z.string().optional(),
        attentionYear: z.number().int().optional(),
        attentionAssetCategory: z.string().nullable().optional(),
        attentionDepartment: z.string().nullable().optional(),
        attentionPmCycle: z.string().nullable().optional(),
      })
      .strict()
      .optional(),
    buildingOptions: z.array(z.string().trim().min(1).max(256)).optional(),
    batches: z.array(scheduleAssignmentQueueBatchSchema),
  })
  .strict()
  .transform((queue) => ({
    ...queue,
    totalBatchCount: queue.totalBatchCount ?? queue.batches.length,
    page: queue.page ?? 1,
    pageSize: queue.pageSize ?? Math.max(1, queue.batches.length),
    summary: queue.summary ?? {
      notStartedBatchCount: 0,
      inProgressBatchCount: 0,
      ongoingScheduleCount: 0,
      overdueScheduleCount: 0,
      pendingAssignmentBatchCount: undefined,
      urgentAssignmentBatchCount: undefined,
      attentionDate: undefined,
      attentionRole: undefined,
      attentionYear: undefined,
      attentionAssetCategory: undefined,
      attentionDepartment: undefined,
      attentionPmCycle: undefined,
    },
    buildingOptions: queue.buildingOptions ?? [],
  }))

export type ScheduleAssignmentQueueBatch = z.infer<
  typeof scheduleAssignmentQueueBatchSchema
>

const scheduleRegistryAssetSchema = z
  .object({
    id: z.string().uuid(),
    assetCode: z.string().trim().min(1).max(64),
    assetCategory: z.string().trim().min(1).max(64),
    building: optionalText,
    department: optionalText,
    location: optionalText,
  })
  .strict()

const scheduleRegistryItemSchema = z
  .object({
    id: z.string().uuid(),
    assetId: z.string().uuid(),
    scheduleDate: z.string().datetime({ offset: true }),
    pmCycle: z.string().regex(/^\d{4}-(0[1-9]|1[0-2])$/),
    periodType: z.enum(schedulePeriodTypeCodes),
    status: z.enum(scheduleStatusCodes),
    quarter: z.enum(scheduleQuarterCodes).nullable(),
    semester: z.enum(scheduleSemesterCodes).nullable(),
    year: z.preprocess(
      (value) => (value == null ? null : Number(value)),
      z.number().int().nullable(),
    ),
    academicYear: z.string().max(32).nullable(),
    firstSupervisorAssignedAt: optionalAssignmentTimestamp,
    firstInspectorAssignedAt: optionalAssignmentTimestamp,
    inspectorAssignmentDueAt: optionalAssignmentTimestamp,
    assignmentPeriodStatus: optionalAssignmentPeriodStatus,
    completedAt: z.string().datetime({ offset: true }).nullable(),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    asset: scheduleRegistryAssetSchema.nullable(),
  })
  .strict()

const scheduleRegistryPageSchema = z
  .object({
    items: z.array(scheduleRegistryItemSchema),
    page: z.coerce.number().int().positive(),
    pageSize: z.coerce.number().int().positive(),
    totalCount: z.coerce.number().int().nonnegative(),
    overallCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

const scheduleRegistryOptionsSchema = z
  .object({
    categories: z.array(assetCategorySchema),
    quarters: z.array(z.enum(scheduleQuarterCodes)),
    departments: z.array(z.string().trim().min(1).max(256)),
    years: z.array(z.string().regex(/^\d{4}$/)),
  })
  .strict()

const scheduleRegistrySummarySchema = z
  .object({
    totalCount: z.coerce.number().int().nonnegative(),
    dueCount: z.coerce.number().int().nonnegative(),
    ongoingCount: z.coerce.number().int().nonnegative(),
    overdueCount: z.coerce.number().int().nonnegative(),
    completedCount: z.coerce.number().int().nonnegative(),
    cancelledCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

export type ScheduleRegistryItem = z.infer<typeof scheduleRegistryItemSchema>
export type ScheduleRegistryPage = z.infer<typeof scheduleRegistryPageSchema>
export type ScheduleRegistryOptions = z.infer<
  typeof scheduleRegistryOptionsSchema
>
export type ScheduleRegistrySummary = z.infer<
  typeof scheduleRegistrySummarySchema
>

function rejectDuplicateReferenceCodes(
  references: ReadonlyArray<{ code: string }>,
  context: z.RefinementCtx,
) {
  const seenCodes = new Set<string>()

  references.forEach((reference, index) => {
    if (seenCodes.has(reference.code)) {
      context.addIssue({
        code: 'custom',
        path: [index, 'code'],
        message: 'Reference codes must be unique.',
      })
      return
    }

    seenCodes.add(reference.code)
  })
}

const scheduleStatusReferenceSchema = z
  .object({
    code: z.enum(scheduleStatusCodes),
    displayName: z.string().trim().min(1).max(128),
  })
  .strict()
const schedulePeriodTypeReferenceSchema = z
  .object({
    code: z.enum(schedulePeriodTypeCodes),
    displayName: z.string().trim().min(1).max(128),
  })
  .strict()
const scheduleQuarterReferenceSchema = z
  .object({
    code: z.enum(scheduleQuarterCodes),
    displayName: z.string().trim().min(1).max(128),
  })
  .strict()

const scheduleStatusReferencesSchema = z
  .array(scheduleStatusReferenceSchema)
  .superRefine(rejectDuplicateReferenceCodes)
const schedulePeriodTypeReferencesSchema = z
  .array(schedulePeriodTypeReferenceSchema)
  .superRefine(rejectDuplicateReferenceCodes)
const scheduleQuarterReferencesSchema = z
  .array(scheduleQuarterReferenceSchema)
  .superRefine(rejectDuplicateReferenceCodes)

export type ScheduleStatusReference = z.infer<
  typeof scheduleStatusReferenceSchema
>
export type SchedulePeriodTypeReference = z.infer<
  typeof schedulePeriodTypeReferenceSchema
>
export type ScheduleQuarterReference = z.infer<
  typeof scheduleQuarterReferenceSchema
>

export const createScheduleSchema = z
  .object({
    assetId: z.string().uuid('Choose an asset.'),
    year: z.preprocess(
      (value) => (value === '' || value === null ? undefined : value),
      z.coerce.number().int('Year must be a whole number.').optional(),
    ),
    month: z.preprocess(
      (value) => (value === '' || value === null ? undefined : value),
      z.coerce.number().int('Choose a scheduled month.').optional(),
    ),
    allowedMonths: z.array(z.number().int().min(1).max(12)).default([]),
  })
  .superRefine((value, context) => {
    const currentInstitutionalYear = getCurrentManilaYear()

    if (value.year === undefined) {
      context.addIssue({
        code: 'custom',
        path: ['year'],
        message: 'Choose a scheduled year.',
      })
    } else if (value.year !== currentInstitutionalYear) {
      context.addIssue({
        code: 'custom',
        path: ['year'],
        message: `Schedule creation is limited to the current institutional year (${currentInstitutionalYear}).`,
      })
    }

    if (value.month === undefined || value.month < 1 || value.month > 12) {
      context.addIssue({
        code: 'custom',
        path: ['month'],
        message: 'Choose a scheduled month.',
      })
      return
    }

    if (value.allowedMonths.length === 0) {
      context.addIssue({
        code: 'custom',
        path: ['month'],
        message: 'Allowed PM months are unavailable for this asset category.',
      })
    } else if (!value.allowedMonths.includes(value.month)) {
      context.addIssue({
        code: 'custom',
        path: ['month'],
        message: 'Choose a PM month allowed for this asset category.',
      })
    }
  })

export type CreateScheduleValues = {
  assetId: string
  year?: number | string | undefined
  month?: number | string | undefined
  allowedMonths: number[]
}

type ScheduleResponseCompat = Omit<ScheduleResponse, 'pmCycle'> & {
  pmCycle?: string
}

export function parseSchedule(value: ScheduleResponseCompat): Schedule {
  return scheduleSchema.parse(value)
}

export function parseSchedules(values: ScheduleResponseCompat[]): Schedule[] {
  return z.array(scheduleSchema).parse(values)
}

export function parseScheduleAssignmentQueue(
  value: ScheduleAssignmentQueueResponse,
) {
  return scheduleAssignmentQueueSchema.parse(value)
}

export function parseScheduleRegistryItem(
  value: ScheduleRegistryItemResponse,
): ScheduleRegistryItem {
  return scheduleRegistryItemSchema.parse(value)
}

export function parseScheduleRegistryPage(
  value: ScheduleRegistryPageResponse,
): ScheduleRegistryPage {
  return scheduleRegistryPageSchema.parse(value)
}

export function parseScheduleRegistryOptions(
  value: ScheduleRegistryOptionsResponse,
): ScheduleRegistryOptions {
  return scheduleRegistryOptionsSchema.parse(value)
}

export function parseScheduleRegistrySummary(
  value: ScheduleRegistrySummaryResponse,
): ScheduleRegistrySummary {
  return scheduleRegistrySummarySchema.parse(value)
}

export function parseScheduleStatuses(
  values: ScheduleReferenceResponse[],
): ScheduleStatusReference[] {
  return scheduleStatusReferencesSchema.parse(values)
}

export function parseSchedulePeriodTypes(
  values: ScheduleReferenceResponse[],
): SchedulePeriodTypeReference[] {
  return schedulePeriodTypeReferencesSchema.parse(values)
}

export function parseScheduleQuarters(
  values: ScheduleReferenceResponse[],
): ScheduleQuarterReference[] {
  return scheduleQuarterReferencesSchema.parse(values)
}

export function toCreateScheduleDto(
  values: CreateScheduleValues,
): CreateScheduleDto {
  const parsed = createScheduleSchema.parse(values)
  const month = Number(parsed.month)
  const pmCycle = `${parsed.year}-${String(month).padStart(2, '0')}`
  return {
    assetId: parsed.assetId,
    pmCycle,
    periodType: 'Quarter',
    quarter: `Q${Math.floor((month - 1) / 3) + 1}`,
    year: Number(parsed.year),
  }
}
