import type { ReactNode } from 'react'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import type {
  PmAnalyticsV2InterpretationResponse,
  PmAnalyticsV2Plan,
  PmAnalyticsV2QueryResponse,
  PmAnalyticsV2SourceResponse,
} from '@/api/generated/models'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PmAnalyticsPanel } from './pm-analytics'

const testState = vi.hoisted(() => ({
  roles: ['GSD'] as string[],
  userId: '88888888-8888-4888-8888-888888888888',
  interpret: vi.fn(),
  query: vi.fn(),
}))

vi.mock('@/features/auth/current-user', () => ({
  useCurrentUser: () => ({
    data: { id: testState.userId, roles: testState.roles },
  }),
}))

vi.mock('@/api/generated/endpoints', () => ({
  interpretPmAnalyticsV2Question: testState.interpret,
  queryPmAnalyticsV2: testState.query,
}))

vi.mock('@tanstack/react-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-router')>()
  return {
    ...actual,
    Link: ({
      children,
      params,
    }: {
      children: ReactNode
      params: { assetId: string }
    }) => <a href={'/app/assets/' + params.assetId}>{children}</a>,
  }
})

function makePlan(
  overrides: Partial<PmAnalyticsV2Plan> = {},
): PmAnalyticsV2Plan {
  return {
    schemaVersion: 2,
    metric: 'NonOperational',
    assetCategory: 'fire-extinguisher',
    period: {
      kind: 'Month',
      year: 2026,
      month: 11,
      quarter: null,
      startCycle: null,
      endCycle: null,
    },
    comparison: null,
    presentation: 'Count',
    countBasis: 'AssetCycles',
    department: null,
    building: null,
    assetCode: null,
    condition: null,
    timeliness: null,
    groupBy: 'None',
    ranking: null,
    ...overrides,
  }
}

function makeInterpretation(
  overrides: Partial<PmAnalyticsV2InterpretationResponse> = {},
): PmAnalyticsV2InterpretationResponse {
  return {
    status: 'Valid',
    plan: makePlan(),
    clarificationFields: [],
    code: null,
    detail: null,
    ...overrides,
  }
}

const firstSource: PmAnalyticsV2SourceResponse = {
  scheduleId: '11111111-1111-4111-8111-111111111111',
  assetId: '22222222-2222-4222-8222-222222222222',
  inspectionId: '33333333-3333-4333-8333-333333333333',
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  currentDepartment: 'Demo CCMS',
  currentBuilding: 'Library',
  pmCycle: '2026-11',
  deadline: '2026-11-30T15:59:59.999Z',
  completedAt: '2026-12-01T04:00:00Z',
  scheduleStatus: 'Completed',
  formStatus: 'Draft',
  condition: 'NonOperational',
  timeliness: 'Late',
  usedLegacyDateFallback: false,
}

function makeResponse(
  overrides: Partial<PmAnalyticsV2QueryResponse> = {},
): PmAnalyticsV2QueryResponse {
  return {
    schemaVersion: 2,
    asOfUtc: '2026-12-01T05:00:00Z',
    plan: makePlan(),
    sourceScope: 'Matching',
    page: 1,
    pageSize: 25,
    population: {
      eligibleAssetCycleCount: 3,
      eligibleDistinctAssetCount: 3,
      matchingAssetCycleCount: 1,
      matchingDistinctAssetCount: 1,
      sourceTotalCount: 1,
      totalPages: 1,
    },
    result: {
      numerator: 1,
      denominator: 3,
      value: 1,
      unit: 'Count',
      isMeasurable: true,
      unmeasurableReason: null,
    },
    cycles: [],
    groups: [],
    rankingExclusions: [],
    comparison: null,
    sources: [firstSource],
    scopeNote: 'Results use the validated PM schedule scope.',
    ...overrides,
  }
}

function submitQuestion(
  question = 'Show non-operational assets for fire extinguishers in November 2026',
) {
  fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
    target: { value: question },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Show result' }))
}

describe('PM analytics panel', () => {
  beforeEach(() => {
    testState.roles = ['GSD']
    testState.userId = '88888888-8888-4888-8888-888888888888'
    testState.interpret.mockReset()
    testState.interpret.mockResolvedValue(makeInterpretation())
    testState.query.mockReset()
  })

  it('keeps the analytics panel hidden from non-GSD roles', () => {
    testState.roles = ['Inspector']

    render(<PmAnalyticsPanel />)

    expect(
      screen.queryByRole('heading', { name: 'Ask about PM results' }),
    ).toBeNull()
    expect(testState.interpret).not.toHaveBeenCalled()
    expect(testState.query).not.toHaveBeenCalled()
  })

  it('passes the interpreted typed plan directly to the V2 query and labels source context', async () => {
    const typedPlan = makePlan({
      metric: 'Remaining',
      period: {
        kind: 'Quarter',
        year: 2026,
        quarter: 3,
        month: null,
        startCycle: null,
        endCycle: null,
      },
      countBasis: 'DistinctAssets',
    })
    testState.interpret.mockResolvedValueOnce(
      makeInterpretation({ plan: typedPlan }),
    )
    testState.query.mockResolvedValueOnce(
      makeResponse({
        plan: typedPlan,
        population: {
          eligibleAssetCycleCount: '12',
          eligibleDistinctAssetCount: '10',
          matchingAssetCycleCount: '4',
          matchingDistinctAssetCount: '3',
          sourceTotalCount: '4',
          totalPages: '1',
        },
        result: {
          numerator: '4',
          denominator: '12',
          value: '4',
          unit: 'Count',
          isMeasurable: true,
          unmeasurableReason: null,
        },
        cycles: [
          {
            pmCycle: '2026-07',
            deadline: '2026-07-31T15:59:59.999Z',
            state: 'Active',
            eligibleAssetCycleCount: 4,
            eligibleDistinctAssetCount: 4,
            inspectedCount: 2,
            completedOnTimeCount: 2,
            completedLateCount: 0,
            nonOperationalCount: 0,
            remainingCount: 2,
            missedCount: 0,
            legacyDateFallbackCount: 0,
          },
        ],
        sources: [firstSource],
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion('Show remaining fire extinguishers for Q3 2026')

    expect(
      await screen.findByRole('heading', { name: 'PM analytics result' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Assets with remaining schedules for Fire extinguishers during Q3 2026.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Eligible distinct assets: 10')).toBeInTheDocument()
    expect(screen.getByText('Matching distinct assets: 3')).toBeInTheDocument()
    expect(
      screen.getByText(
        'Current department: Demo CCMS · Current building: Library',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Inspection completed')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Print result' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(firstSource.assetId)).toBeNull()

    expect(testState.interpret).toHaveBeenCalledWith(
      { question: 'Show remaining fire extinguishers for Q3 2026' },
      expect.any(AbortSignal),
    )
    expect(testState.query).toHaveBeenCalledWith(
      {
        plan: typedPlan,
        sourceScope: 'Matching',
        page: 1,
        pageSize: 25,
      },
      expect.any(AbortSignal),
    )
  })

  it('uses server paging and calculation scope without filtering returned rows in the browser', async () => {
    const plan = makePlan()
    const nextSource = {
      ...firstSource,
      scheduleId: '44444444-4444-4444-8444-444444444444',
      assetId: '55555555-5555-4555-8555-555555555555',
      assetCode: 'FE-002',
      condition: 'Operational',
      timeliness: 'OnTime',
    }
    const calculationSource = {
      ...firstSource,
      scheduleId: '66666666-6666-4666-8666-666666666666',
      assetId: '77777777-7777-4777-8777-777777777777',
      inspectionId: null,
      assetCode: 'FE-003',
      condition: 'NotInspected',
      timeliness: 'Future',
      formStatus: null,
    }
    testState.interpret.mockResolvedValueOnce(makeInterpretation({ plan }))
    testState.query
      .mockResolvedValueOnce(
        makeResponse({
          plan,
          population: {
            eligibleAssetCycleCount: '120',
            eligibleDistinctAssetCount: '100',
            matchingAssetCycleCount: '30',
            matchingDistinctAssetCount: '28',
            sourceTotalCount: '30',
            totalPages: '2',
          },
          sources: [firstSource],
        }),
      )
      .mockResolvedValueOnce(
        makeResponse({
          plan,
          page: '2',
          population: {
            eligibleAssetCycleCount: '120',
            eligibleDistinctAssetCount: '100',
            matchingAssetCycleCount: '30',
            matchingDistinctAssetCount: '28',
            sourceTotalCount: '30',
            totalPages: '2',
          },
          sources: [nextSource],
        }),
      )
      .mockResolvedValueOnce(
        makeResponse({
          plan,
          sourceScope: 'Calculation',
          population: {
            eligibleAssetCycleCount: '120',
            eligibleDistinctAssetCount: '100',
            matchingAssetCycleCount: '30',
            matchingDistinctAssetCount: '28',
            sourceTotalCount: '120',
            totalPages: '5',
          },
          sources: [calculationSource],
        }),
      )
    render(<PmAnalyticsPanel />)

    submitQuestion()
    expect(await screen.findByText('FE-001')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
    expect(await screen.findByText('FE-002')).toBeInTheDocument()
    expect(screen.getByText(/Page 2 of 2/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Calculation sources' }))
    expect(await screen.findByText('FE-003')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Calculation source records' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /may include records that do not match every requested record filter/,
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Matching counts are independent of this page and source-list scope. Asset-cycle and distinct-asset totals are shown separately.',
      ),
    ).toBeInTheDocument()

    expect(testState.query).toHaveBeenNthCalledWith(
      2,
      { plan, sourceScope: 'Matching', page: 2, pageSize: 25 },
      expect.any(AbortSignal),
    )
    expect(testState.query).toHaveBeenNthCalledWith(
      3,
      { plan, sourceScope: 'Calculation', page: 1, pageSize: 25 },
      expect.any(AbortSignal),
    )
  })

  it('shows rankings and marks percentage denominators and exclusions', async () => {
    const plan = makePlan({
      metric: 'OnTimeCompliance',
      period: {
        kind: 'Year',
        year: 2025,
        month: null,
        quarter: null,
        startCycle: null,
        endCycle: null,
      },
      presentation: 'Percent',
      groupBy: 'Department',
      ranking: { limit: 2, direction: 'Highest' },
    })
    testState.interpret.mockResolvedValueOnce(makeInterpretation({ plan }))
    testState.query.mockResolvedValueOnce(
      makeResponse({
        plan,
        result: {
          numerator: '9',
          denominator: '10',
          value: '90.00',
          unit: 'Percent',
          isMeasurable: true,
          unmeasurableReason: null,
        },
        groups: [
          {
            groupBy: 'Department',
            value: 'Demo CCMS',
            result: {
              numerator: '9',
              denominator: '10',
              value: '90.00',
              unit: 'Percent',
              isMeasurable: true,
              unmeasurableReason: null,
            },
            eligibleAssetCycleCount: '10',
            eligibleDistinctAssetCount: '10',
            matchingAssetCycleCount: '9',
            matchingDistinctAssetCount: '9',
          },
        ],
        rankingExclusions: [
          { department: 'Facilities', reason: 'No closed-cycle denominator.' },
        ],
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion(
      'Show on-time compliance for fire extinguishers during 2025 by department',
    )

    expect(await screen.findByText('90%')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Highest ranked departments' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Demo CCMS')).toBeInTheDocument()
    expect(
      screen.getByText(/Percentages use the eligible scheduled population/),
    ).toBeInTheDocument()
    expect(screen.getByText(/outside this top list/)).toBeInTheDocument()
    expect(
      screen.getByText('Facilities: No closed-cycle denominator.'),
    ).toBeInTheDocument()
  })

  it('shows late inspections with both record conditions and distinct-asset grain', async () => {
    const plan = makePlan({
      metric: 'CompletedLate',
      period: {
        kind: 'Year',
        year: 2025,
        month: null,
        quarter: null,
        startCycle: null,
        endCycle: null,
      },
      countBasis: 'DistinctAssets',
      condition: 'NonOperational',
      timeliness: 'Late',
    })
    testState.interpret.mockResolvedValueOnce(makeInterpretation({ plan }))
    testState.query.mockResolvedValueOnce(makeResponse({ plan }))
    render(<PmAnalyticsPanel />)

    submitQuestion(
      'Show late and non-operational fire extinguisher inspections during 2025',
    )

    expect(
      await screen.findByText(
        'Assets with late inspections for Fire extinguishers during 2025. Condition: Non-operational. Timeliness: Completed late. Both record filters are combined with AND.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Distinct assets with an inspection completed after the PM deadline.',
      ),
    ).toBeInTheDocument()
    expect(testState.query).toHaveBeenCalledWith(
      { plan, sourceScope: 'Matching', page: 1, pageSize: 25 },
      expect.any(AbortSignal),
    )
  })

  it('shows comparison values and the backend percentage-point delta', async () => {
    const plan = makePlan({
      metric: 'OnTimeCompliance',
      period: null,
      comparison: {
        earlierCycle: '2025-08',
        laterCycle: '2025-11',
      },
      presentation: 'Percent',
    })
    const earlier = {
      numerator: 1,
      denominator: 3,
      value: '33.33',
      unit: 'Percent',
      isMeasurable: true,
      unmeasurableReason: null,
    }
    const later = {
      numerator: 2,
      denominator: 3,
      value: '66.67',
      unit: 'Percent',
      isMeasurable: true,
      unmeasurableReason: null,
    }
    testState.interpret.mockResolvedValueOnce(makeInterpretation({ plan }))
    testState.query.mockResolvedValueOnce(
      makeResponse({
        plan,
        result: null,
        comparison: {
          earlierCycle: '2025-08',
          laterCycle: '2025-11',
          earlier,
          later,
          delta: '33.33',
          deltaUnit: 'percentage points',
        },
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion(
      'Compare on-time compliance for fire extinguishers in August 2025 and November 2025',
    )

    expect(await screen.findByText('Comparison change')).toBeInTheDocument()
    expect(screen.getByText('33.33 percentage points')).toBeInTheDocument()
    expect(screen.getByText(/August 2025:/)).toBeInTheDocument()
    expect(screen.getByText(/November 2025:/)).toBeInTheDocument()
  })

  it('explains why compliance is unavailable while a cycle is open', async () => {
    const plan = makePlan({
      metric: 'OnTimeCompliance',
      presentation: 'Percent',
      period: {
        kind: 'Month',
        year: 2026,
        month: 11,
        quarter: null,
        startCycle: null,
        endCycle: null,
      },
    })
    testState.interpret.mockResolvedValueOnce(makeInterpretation({ plan }))
    testState.query.mockResolvedValueOnce(
      makeResponse({
        plan,
        result: {
          numerator: 1,
          denominator: 3,
          value: null,
          unit: 'Percent',
          isMeasurable: false,
          unmeasurableReason: 'IncludedCyclesOpen',
        },
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion(
      'Show on-time compliance for fire extinguishers in November 2026',
    )

    expect(
      await screen.findByText('Not measurable until the included cycles close'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Included cycles must be closed/),
    ).toBeInTheDocument()
  })

  it('does not query records when interpretation needs clarification', async () => {
    testState.interpret.mockResolvedValueOnce(
      makeInterpretation({
        status: 'NeedsClarification',
        plan: null,
        clarificationFields: ['Year', 'Month'],
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion('Show progress for fire extinguishers in November')

    expect(
      await screen.findByRole('heading', {
        name: 'Please clarify the question',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Which year should be used?')).toBeInTheDocument()
    expect(screen.getByText('Which month should be used?')).toBeInTheDocument()
    expect(testState.query).not.toHaveBeenCalled()
  })

  it('does not query records for unsupported questions', async () => {
    testState.interpret.mockResolvedValueOnce(
      makeInterpretation({
        status: 'Unsupported',
        plan: null,
        detail: 'A safe unsupported response.',
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion('Predict maintenance costs for next year')

    expect(
      await screen.findByText(
        'This question is outside the supported PM measures or time scopes. Try one asset category and a specific supported period.',
      ),
    ).toBeInTheDocument()
    expect(testState.query).not.toHaveBeenCalled()
  })

  it('rejects a valid interpretation without an executable plan', async () => {
    testState.interpret.mockResolvedValueOnce(
      makeInterpretation({ plan: null }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'PM analytics could not be loaded. Please try again.',
    )
    expect(testState.query).not.toHaveBeenCalled()
  })

  it('clears an old result after an edited question and shows a safe query error', async () => {
    testState.query
      .mockResolvedValueOnce(makeResponse())
      .mockRejectedValueOnce(new Error('private API detail'))
    render(<PmAnalyticsPanel />)

    submitQuestion()
    expect(
      await screen.findByRole('heading', { name: 'PM analytics result' }),
    ).toBeInTheDocument()

    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show another supported PM result' },
    })
    expect(
      screen.queryByRole('heading', { name: 'PM analytics result' }),
    ).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Show result' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'PM analytics could not be loaded. Please try again.',
    )
    expect(screen.queryByText('private API detail')).toBeNull()
  })

  it('ignores an interpretation from a previous authenticated identity', async () => {
    let resolveInterpretation:
      ((response: PmAnalyticsV2InterpretationResponse) => void) | undefined
    testState.interpret.mockReturnValueOnce(
      new Promise<PmAnalyticsV2InterpretationResponse>((resolve) => {
        resolveInterpretation = resolve
      }),
    )
    const { rerender } = render(<PmAnalyticsPanel />)

    submitQuestion()
    expect(await screen.findByRole('status')).toBeInTheDocument()
    testState.userId = '99999999-9999-4999-8999-999999999999'
    rerender(<PmAnalyticsPanel />)

    await act(async () => {
      resolveInterpretation?.(makeInterpretation())
    })

    await waitFor(() => {
      expect(
        screen.queryByRole('heading', { name: 'PM analytics result' }),
      ).toBeNull()
      expect(testState.query).not.toHaveBeenCalled()
    })
  })
})
