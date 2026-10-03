import type { ReactNode } from 'react'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { PmAnalyticsResponse } from '@/api/generated/models'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PmAnalyticsPanel } from './pm-analytics'

const testState = vi.hoisted(() => ({
  roles: ['GSD'] as string[],
  queryPmAnalytics: vi.fn(),
}))

vi.mock('@/features/auth/current-user', () => ({
  useCurrentUser: () => ({ data: { roles: testState.roles } }),
}))

vi.mock('@/api/generated/endpoints', () => ({
  queryPmAnalytics: testState.queryPmAnalytics,
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
    }) => <a href={`/app/assets/${params.assetId}`}>{children}</a>,
  }
})

const source = {
  scheduleId: '11111111-1111-4111-8111-111111111111',
  assetId: '22222222-2222-4222-8222-222222222222',
  inspectionId: '33333333-3333-4333-8333-333333333333',
  assetCode: 'FE-001',
  department: 'GSD',
  pmCycle: '2026-11',
  deadline: '2026-11-30T15:59:59.999Z',
  inspectionCompletedAt: '2026-12-01T04:00:00Z',
  timeliness: 'Late',
  condition: 'NonOperational',
  formStatus: 'Draft',
}

function makeResponse(
  overrides: Partial<PmAnalyticsResponse> = {},
): PmAnalyticsResponse {
  return {
    plan: {
      metric: 'NonOperational',
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-11',
      department: null,
      groupBy: 'None',
    },
    deadline: '2026-11-30T15:59:59.999Z',
    periodState: 'Closed',
    result: {
      department: null,
      numerator: 1,
      denominator: 3,
      value: 1,
      unit: 'Count',
      isMeasurable: true,
    },
    groups: [],
    sources: [
      source,
      {
        ...source,
        scheduleId: '44444444-4444-4444-8444-444444444444',
        assetId: '55555555-5555-4555-8555-555555555555',
        inspectionId: null,
        assetCode: 'FE-002',
        inspectionCompletedAt: null,
        timeliness: 'NotCompleted',
        condition: 'NotInspected',
        formStatus: null,
      },
      {
        ...source,
        scheduleId: '66666666-6666-4666-8666-666666666666',
        assetId: '77777777-7777-4777-8777-777777777777',
        inspectionId: null,
        assetCode: 'FE-003',
        inspectionCompletedAt: null,
        timeliness: 'NotCompleted',
        condition: 'NotInspected',
        formStatus: null,
      },
    ],
    totalSourceCount: 3,
    sourcesTruncated: false,
    scopeNote: 'These are live PM results for the selected scope.',
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
    testState.queryPmAnalytics.mockReset()
  })

  it('keeps the analytics panel hidden from non-GSD roles', () => {
    testState.roles = ['Inspector']

    render(<PmAnalyticsPanel />)

    expect(
      screen.queryByRole('heading', { name: 'Ask about PM results' }),
    ).toBeNull()
    expect(testState.queryPmAnalytics).not.toHaveBeenCalled()
  })

  it('shows the scoped result and human-readable source details', async () => {
    testState.queryPmAnalytics.mockResolvedValue(makeResponse())
    render(<PmAnalyticsPanel />)

    submitQuestion()

    expect(
      await screen.findByRole('heading', { name: 'PM result' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Non-operational assets for Fire Extinguisher in November 2026.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('3 shown of 3')).toBeInTheDocument()
    expect(
      screen.getByText(/Deadline: Nov 30, 2026.*11:59 PM.*GMT\+8/),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /A measure count can therefore be lower than the source count/,
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'FE-001' })).toHaveAttribute(
      'href',
      '/app/assets/22222222-2222-4222-8222-222222222222',
    )
    expect(
      screen.getByText('Non-operational', { exact: true }),
    ).toBeInTheDocument()
    expect(screen.getByText('Form status')).toBeInTheDocument()
    expect(screen.getByText('Draft', { exact: true })).toBeInTheDocument()
    expect(screen.getAllByText('Not inspected', { exact: true })).toHaveLength(
      2,
    )
    expect(screen.getByText('1', { exact: true })).toBeInTheDocument()
  })

  it('explains why active-cycle compliance is not yet measurable', async () => {
    testState.queryPmAnalytics.mockResolvedValue(
      makeResponse({
        plan: {
          metric: 'OnTimeCompliance',
          assetCategory: 'fire-extinguisher',
          pmCycle: '2026-11',
          department: null,
          groupBy: 'None',
        },
        periodState: 'Active',
        result: {
          department: null,
          numerator: 1,
          denominator: 3,
          value: null,
          unit: 'Percent',
          isMeasurable: false,
        },
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion(
      'Show on-time compliance for fire extinguishers in November 2026',
    )

    expect(
      await screen.findByText('Not measurable until the cycle closes'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Inspections completed by the deadline/),
    ).toBeInTheDocument()
  })

  it.each([
    {
      metric: 'OnTimeCompliance',
      unit: 'Percent',
      question:
        'Show on-time compliance for fire extinguishers in November 2026',
    },
    {
      metric: 'CompletedLate',
      unit: 'Count',
      question: 'Show late inspections for fire extinguishers in November 2026',
    },
    {
      metric: 'NonOperational',
      unit: 'Count',
      question:
        'Show non-operational assets for fire extinguishers in November 2026',
    },
  ] as const)(
    'uses an empty-scope message for $metric when there are no eligible schedules',
    async ({ metric, unit, question }) => {
      testState.queryPmAnalytics.mockResolvedValue(
        makeResponse({
          plan: {
            metric,
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-11',
            department: null,
            groupBy: 'None',
          },
          result: {
            department: null,
            numerator: 0,
            denominator: 0,
            value: null,
            unit,
            isMeasurable: false,
          },
          sources: [],
          totalSourceCount: 0,
        }),
      )
      render(<PmAnalyticsPanel />)

      submitQuestion(question)

      expect(
        await screen.findByText('No eligible schedules'),
      ).toBeInTheDocument()
    },
  )

  it('shows a safe error and clears results when the question changes', async () => {
    testState.queryPmAnalytics
      .mockResolvedValueOnce(makeResponse())
      .mockRejectedValueOnce(new Error('Unsupported question'))
    render(<PmAnalyticsPanel />)

    submitQuestion()
    expect(
      await screen.findByRole('heading', { name: 'PM result' }),
    ).toBeInTheDocument()

    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show something unsupported' },
    })
    expect(screen.queryByRole('heading', { name: 'PM result' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Show result' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This question could not be answered.',
    )
    expect(screen.queryByText('Unsupported question')).toBeNull()
  })

  it('ignores an old response after the question is edited', async () => {
    let resolveFirst: ((response: PmAnalyticsResponse) => void) | undefined
    testState.queryPmAnalytics.mockReturnValueOnce(
      new Promise<PmAnalyticsResponse>((resolve) => {
        resolveFirst = resolve
      }),
    )
    render(<PmAnalyticsPanel />)

    submitQuestion()
    expect(await screen.findByRole('status')).toBeInTheDocument()
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: {
        value: 'Show progress for fire extinguishers in November 2026',
      },
    })

    await act(async () => {
      resolveFirst?.(makeResponse())
    })

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: 'PM result' })).toBeNull()
      expect(screen.queryByRole('status')).toBeNull()
    })
  })
})
