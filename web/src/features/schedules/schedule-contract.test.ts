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

  it('requires quarter only for quarterly schedules', () => {
    expect(
      createScheduleSchema.safeParse({
        assetId: schedule.assetId,
        scheduleDate: '2026-08-01',
        periodType: 'Quarter',
        quarter: undefined,
        year: 2026,
      }).success,
    ).toBe(false)
    expect(
      createScheduleSchema.safeParse({
        assetId: schedule.assetId,
        scheduleDate: '2026-08-01',
        periodType: 'Annual',
        quarter: undefined,
        year: 2026,
      }).success,
    ).toBe(true)
  })

  it('clears quarter from non-quarter create payloads', () => {
    const dto = toCreateScheduleDto({
      assetId: schedule.assetId,
      scheduleDate: '2026-08-01',
      periodType: 'Annual',
      quarter: 'Q3',
      year: 2026,
    })
    expect(dto.quarter).toBeNull()
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
