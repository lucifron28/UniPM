import { z } from 'zod'
import type {
  CreateScheduleDto,
  ScheduleReferenceResponse,
  ScheduleResponse,
} from '@/api/generated/models'

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
export const scheduleSemesterCodes = ['First', 'Second', 'Summer'] as const

const optionalText = z.string().max(256).nullable()
const scheduleAssetSchema = z
  .object({
    id: z.string().uuid(),
    assetCode: z.string().trim().min(1).max(64),
    assetCategory: z.string().trim().min(1).max(64),
    building: optionalText,
    department: optionalText,
    location: optionalText,
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
    completedAt: z.string().datetime({ offset: true }).nullable(),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    asset: scheduleAssetSchema.nullable(),
  })
  .strict()

export type Schedule = z.infer<typeof scheduleSchema>

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
    const maxPlanningYear = new Date().getUTCFullYear() + 5

    if (value.year === undefined) {
      context.addIssue({
        code: 'custom',
        path: ['year'],
        message: 'Choose a scheduled year.',
      })
    } else if (value.year < 2000 || value.year > maxPlanningYear) {
      context.addIssue({
        code: 'custom',
        path: ['year'],
        message: `Year must be between 2000 and ${maxPlanningYear}.`,
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
