import { describe, expect, it } from 'vitest'
import {
  assetDetailReturnContext,
  inspectionDetailReturnContext,
  parseDetailReturnContext,
  resolveDetailReturn,
  scheduleDetailReturnContext,
  type DetailReturnContext,
  type DetailReturnFallback,
} from '@/features/shared/detail-navigation'

const assetId = '11111111-1111-4111-8111-111111111111'
const inspectionId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '33333333-3333-4333-8333-333333333333'
const formId = '44444444-4444-4444-8444-444444444444'
const dashboardSearch = {
  department: 'GSD',
  assetCategory: 'fire-extinguisher',
  year: 2026,
  pmCycle: '2026-08',
  condition: 'Operational' as const,
  timeliness: 'Late' as const,
  search: 'FE-01',
}

describe('detail return navigation', () => {
  it('uses a safe canonical fallback when the URL has no valid origin', () => {
    const fallback: DetailReturnFallback = {
      kind: 'assetRegistry',
      search: { assetCategory: 'fire-extinguisher', page: 3, text: 'FE' },
    }

    expect(resolveDetailReturn(undefined, fallback)).toEqual({
      kind: 'assetRegistry',
      search: { assetCategory: 'fire-extinguisher', page: 3, text: 'FE' },
      label: 'Back to assets',
    })
    expect(
      parseDetailReturnContext({
        kind: 'external',
        url: 'https://example.com',
      }),
    ).toBeUndefined()
  })

  it('preserves each registry search and page in its contextual return link', () => {
    expect(
      resolveDetailReturn(undefined, {
        kind: 'inspectionRegistry',
        search: { assetId, isOperational: false, page: 4 },
      }),
    ).toMatchObject({
      kind: 'inspectionRegistry',
      search: { assetId, isOperational: false, page: 4 },
      label: 'Back to inspections',
    })
    expect(
      resolveDetailReturn(undefined, {
        kind: 'scheduleRegistry',
        search: { status: 'Due', quarter: 'Q3', year: 2026, page: 2 },
      }),
    ).toMatchObject({
      kind: 'scheduleRegistry',
      search: { status: 'Due', quarter: 'Q3', year: 2026, page: 2 },
      label: 'Back to schedules',
    })
  })

  it('returns from linked details through the asset and back to the dashboard', () => {
    const fallback: DetailReturnFallback = {
      kind: 'inspectionRegistry',
      search: { page: 2 },
    }
    const fromDashboard: DetailReturnContext = {
      kind: 'dashboard',
      search: dashboardSearch,
    }
    const assetOrigin = assetDetailReturnContext(
      assetId,
      'FE-01',
      fromDashboard,
      fallback,
    )
    const inspectionOrigin = inspectionDetailReturnContext(
      inspectionId,
      assetOrigin,
      fallback,
    )
    const scheduleOrigin = scheduleDetailReturnContext(
      scheduleId,
      inspectionOrigin,
      fallback,
    )

    expect(resolveDetailReturn(inspectionOrigin, fallback)).toMatchObject({
      kind: 'inspectionDetail',
      inspectionId,
      returnContext: assetOrigin,
      label: 'Back to inspection',
    })
    expect(resolveDetailReturn(assetOrigin, fallback)).toMatchObject({
      kind: 'assetDetail',
      assetId,
      assetCode: 'FE-01',
      returnContext: fromDashboard,
      label: 'Back to FE-01',
    })
    expect(resolveDetailReturn(fromDashboard, fallback)).toMatchObject({
      kind: 'dashboard',
      search: dashboardSearch,
      label: 'Back to PM dashboard',
    })
    expect(resolveDetailReturn(scheduleOrigin, fallback)).toMatchObject({
      kind: 'scheduleDetail',
      scheduleId,
      returnContext: inspectionOrigin,
      label: 'Back to schedule',
    })
  })

  it('returns from source inspection and full-form links to the correct batch', () => {
    const batchReview: DetailReturnContext = {
      kind: 'batchReview',
      formId,
      search: dashboardSearch,
    }
    const inspectionOrigin = inspectionDetailReturnContext(
      inspectionId,
      batchReview,
      { kind: 'inspectionRegistry', search: { page: 1 } },
    )

    expect(
      resolveDetailReturn(inspectionOrigin, { kind: 'formRegistry' }),
    ).toMatchObject({
      kind: 'inspectionDetail',
      inspectionId,
      returnContext: batchReview,
      label: 'Back to inspection',
    })
    expect(
      resolveDetailReturn(batchReview, { kind: 'formRegistry' }),
    ).toMatchObject({
      kind: 'batchReview',
      formId,
      search: dashboardSearch,
      returnContext: { kind: 'dashboard', search: dashboardSearch },
      label: 'Back to batch review',
    })
    expect(
      resolveDetailReturn({ kind: 'formRegistry' }, { kind: 'dashboard' }),
    ).toEqual({
      kind: 'formRegistry',
      label: 'Back to form review',
    })
  })

  it('rejects malformed origins and caps repeated detail ancestry', () => {
    const malformed = parseDetailReturnContext({
      kind: 'assetDetail',
      assetId,
      assetCode: 'FE-01',
      parent: { kind: 'assetRegistry', search: { page: 'many' } },
    })
    expect(malformed).toBeUndefined()

    const fallback: DetailReturnFallback = {
      kind: 'dashboard',
      search: dashboardSearch,
    }
    let parent: DetailReturnContext = {
      kind: 'dashboard',
      search: dashboardSearch,
    }
    parent = assetDetailReturnContext(assetId, 'FE-01', parent, fallback)
    parent = inspectionDetailReturnContext(inspectionId, parent, fallback)
    parent = scheduleDetailReturnContext(scheduleId, parent, fallback)
    parent = assetDetailReturnContext(assetId, 'FE-01', parent, fallback)
    parent = inspectionDetailReturnContext(inspectionId, parent, fallback)

    expect(parseDetailReturnContext(parent)).toBeDefined()
    expect(resolveDetailReturn(parent, fallback)).toMatchObject({
      kind: 'inspectionDetail',
      inspectionId,
    })
  })
})
