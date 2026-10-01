import { describe, expect, it } from 'vitest'
import {
  formatPmCycle,
  formatPmCycleDueDate,
  getPmCycleDueDate,
} from '@/features/schedules/schedule-presentation'

describe('PM cycle presentation', () => {
  it.each([
    ['2026-02', '2026-02-28'],
    ['2028-02', '2028-02-29'],
    ['2026-06', '2026-06-30'],
    ['2026-11', '2026-11-30'],
    ['2026-12', '2026-12-31'],
  ])('derives the civil month-end for %s', (cycle, expected) => {
    expect(getPmCycleDueDate(cycle)?.toISOString().slice(0, 10)).toBe(expected)
  })

  it('formats the month from the cycle and fails closed without one', () => {
    expect(formatPmCycle('2026-11')).toBe('November 2026')
    expect(formatPmCycleDueDate('invalid')).toBe('Not recorded')
    expect(getPmCycleDueDate(undefined)).toBeNull()
  })
})
