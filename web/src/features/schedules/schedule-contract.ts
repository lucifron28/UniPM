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
    completedAt: z.string().datetime({ offset: true }).nullable(),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
    asset: scheduleAssetSchema.nullable(),
  })
  .strict()

export type Schedule = z.infer<typeof scheduleSchema>

const scheduleReferenceSchema = z
  .object({
    code: z.string().trim().min(1).max(32),
    displayName: z.string().trim().min(1).max(128),
  })
  .strict()

export type ScheduleReference = z.infer<typeof scheduleReferenceSchema>

export const createScheduleSchema = z
  .object({
    assetId: z.string().uuid('Choose an asset.'),
    scheduleDate: z.string().date('Choose a schedule date.'),
    periodType: z.enum(schedulePeriodTypeCodes, {
      message: 'Choose a maintenance period.',
    }),
    quarter: z.enum(scheduleQuarterCodes).optional(),
    year: z.preprocess(
      (value) => (value === '' || value === null ? undefined : value),
      z.coerce
        .number()
        .int('Year must be a whole number.')
        .min(2000, 'Year must be 2000 or later.')
        .max(
          new Date().getUTCFullYear() + 5,
          `Year must not be later than ${new Date().getUTCFullYear() + 5}.`,
        )
        .optional(),
    ),
  })
  .superRefine((value, context) => {
    if (value.periodType === 'Quarter' && !value.quarter) {
      context.addIssue({
        code: 'custom',
        path: ['quarter'],
        message: 'Choose a quarter for a quarterly schedule.',
      })
    }
  })

export type CreateScheduleValues = {
  assetId: string
  scheduleDate: string
  periodType: (typeof schedulePeriodTypeCodes)[number]
  quarter?: (typeof scheduleQuarterCodes)[number] | undefined
  year?: number | string | undefined
}

export function parseSchedule(value: ScheduleResponse): Schedule {
  return scheduleSchema.parse(value)
}

export function parseSchedules(values: ScheduleResponse[]): Schedule[] {
  return z.array(scheduleSchema).parse(values)
}

export function parseScheduleReferences(
  values: ScheduleReferenceResponse[],
): ScheduleReference[] {
  return z.array(scheduleReferenceSchema).parse(values)
}

export function toCreateScheduleDto(
  values: CreateScheduleValues,
): CreateScheduleDto {
  const parsed = createScheduleSchema.parse(values)
  return {
    assetId: parsed.assetId,
    scheduleDate: new Date(`${parsed.scheduleDate}T00:00:00`).toISOString(),
    periodType: parsed.periodType,
    quarter: parsed.periodType === 'Quarter' ? (parsed.quarter ?? null) : null,
    year: parsed.year ?? null,
  }
}
