import { describe, expect, it } from 'vitest'
import { ZodError } from 'zod'
import {
  createScheduleSchema,
  parseSchedule,
  parseSchedulePeriodTypes,
  parseScheduleQuarters,
  parseScheduleStatuses,
  toCreateScheduleDto,
} from '@/features/schedules/schedule-contract'

const schedule = {
  id: '11111111-1111-4111-8111-111111111111',
  assetId: '22222222-2222-4222-8222-222222222222',
  scheduleDate: '2026-08-01T00:00:00+08:00',
  periodType: 'Quarter',
  status: 'Due',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: null,
  assignedToUserId: null,
  assignedSupervisorUserId: null,
  completedAt: null,
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
  asset: {
    id: '22222222-2222-4222-8222-222222222222',
    assetCode: 'FE-001',
    assetCategory: 'fire-extinguisher',
    building: 'Main',
    department: 'GSD',
    location: 'Lobby',
  },
}

describe('schedule contracts', () => {
  it('parses the public schedule response and rejects private or unknown fields', () => {
    expect(parseSchedule(schedule).asset?.assetCode).toBe('FE-001')
    expect(() =>
      parseSchedule({ ...schedule, privateNote: 'nope' } as never),
    ).toThrow(ZodError)
  })

  it('builds a PM cycle from the selected year and allowed month', () => {
    expect(
      createScheduleSchema.safeParse({
        assetId: schedule.assetId,
        year: 2026,
        month: 8,
        allowedMonths: [2, 5, 8, 11],
      }).success,
    ).toBe(true)
    expect(
      toCreateScheduleDto({
        assetId: schedule.assetId,
        year: 2027,
        month: 1,
        allowedMonths: [1, 4, 7, 10],
      }),
    ).toMatchObject({
      pmCycle: '2027-01',
      periodType: 'Quarter',
      year: 2027,
      quarter: 'Q1',
    })
  })

  it('rejects months that are absent from the category reference data', () => {
    expect(
      createScheduleSchema.safeParse({
        assetId: schedule.assetId,
        year: 2026,
        month: 9,
        allowedMonths: [2, 5, 8, 11],
      }).success,
    ).toBe(false)

    expect(
      createScheduleSchema.safeParse({
        assetId: schedule.assetId,
        year: 2026,
        month: 8,
        allowedMonths: [],
      }).success,
    ).toBe(false)
  })

  it('rejects an out-of-range selected year', () => {
    const unsupportedYear = new Date().getUTCFullYear() + 6
    const result = createScheduleSchema.safeParse({
      assetId: schedule.assetId,
      year: unsupportedYear,
      month: 8,
      allowedMonths: [2, 5, 8, 11],
    })

    expect(result.success).toBe(false)
    if (!result.success) {
      expect(
        result.error.issues.some((issue) => issue.path[0] === 'year'),
      ).toBe(true)
    }
  })

  it.each([
    [
      'statuses',
      parseScheduleStatuses,
      [{ code: 'Unknown', displayName: 'Unknown' }],
    ],
    [
      'period types',
      parseSchedulePeriodTypes,
      [
        { code: 'Quarter', displayName: 'Quarterly' },
        { code: 'Quarter', displayName: 'Duplicate quarterly' },
      ],
    ],
    [
      'quarters',
      parseScheduleQuarters,
      [{ code: 'Q5', displayName: 'Quarter five' }],
    ],
  ])(
    'rejects unsupported or duplicate %s reference entries',
    (_kind, parse, value) => {
      expect(() => parse(value as never)).toThrow(ZodError)
    },
  )
})
