import { describe, expect, it } from 'vitest'
import { parseInspectionSearch } from '@/routes/app/inspections/'

describe('inspection route search', () => {
  it.each([
    [true, true],
    [false, false],
    ['true', true],
    ['false', false],
    ['unknown', undefined],
    ['1', undefined],
  ])(
    'accepts only explicit operational-result values: %s',
    (value, expected) => {
      expect(parseInspectionSearch({ isOperational: value })).toEqual(
        expected === undefined ? {} : { isOperational: expected },
      )
    },
  )
})
