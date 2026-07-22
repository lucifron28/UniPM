import { describe, expect, it } from 'vitest'
import { ZodError } from 'zod'
import {
  parseInspection,
  parseInspectionHistory,
} from '@/features/inspections/inspection-contract'

const inspection = {
  id: '11111111-1111-4111-8111-111111111111',
  scheduleId: '22222222-2222-4222-8222-222222222222',
  assetId: '33333333-3333-4333-8333-333333333333',
  inspectorUserId: '44444444-4444-4444-8444-444444444444',
  dateInspected: '2026-07-22T01:00:00Z',
  isOperational: false,
  remarks: 'Low pressure recorded.',
  actionsRecommendations: 'Arrange a pressure check.',
  createdAt: '2026-07-22T01:00:00Z',
  updatedAt: '2026-07-22T01:00:00Z',
}

describe('inspection contracts', () => {
  it('parses public inspection and history responses while rejecting private fields', () => {
    expect(parseInspection(inspection).scheduleId).toBe(inspection.scheduleId)
    expect(() =>
      parseInspection({ ...inspection, remarksEmbedding: [0.1] } as never),
    ).toThrow(ZodError)
    expect(
      parseInspectionHistory([
        {
          id: inspection.id,
          dateInspected: inspection.dateInspected,
          isOperational: inspection.isOperational,
          remarks: inspection.remarks,
          actionsRecommendations: inspection.actionsRecommendations,
        },
      ])[0]?.id,
    ).toBe(inspection.id)
  })
})
