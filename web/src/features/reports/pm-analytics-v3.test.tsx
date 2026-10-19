import type { ReactNode } from 'react'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  PmAnalyticsV3CatalogResponse,
  PmAnalyticsV3ExecutionResponse,
  PmAnalyticsV3InterpretationResponse,
  PmAnalyticsV3PreviewResponse,
  PmAnalyticsV3ReportPlan,
} from '@/api/generated/models'
import { ApiError } from '@/api/problem-details'
import { PmAnalyticsV3Panel } from './pm-analytics-v3'

const api = vi.hoisted(() => ({
  catalog: vi.fn(),
  filterValues: vi.fn(),
  interpret: vi.fn(),
  preview: vi.fn(),
  execute: vi.fn(),
}))

vi.mock('@/api/generated/endpoints', () => ({
  getPmAnalyticsV3Catalog: api.catalog,
  getPmAnalyticsV3FilterValues: api.filterValues,
  interpretPmAnalyticsV3Question: api.interpret,
  previewPmAnalyticsV3Report: api.preview,
  executePmAnalyticsV3Report: api.execute,
}))

vi.mock('@tanstack/react-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-router')>()
  return {
    ...actual,
    Link: ({
      children,
      to,
      params,
    }: {
      children: ReactNode
      to: string
      params?: Record<string, string>
    }) => {
      const href = Object.entries(params ?? {}).reduce(
        (path, [key, value]) => path.replace(`$${key}`, value),
        to,
      )
      return <a href={href}>{children}</a>
    },
  }
})

const assetId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '11111111-1111-4111-8111-111111111111'
const inspectionId = '33333333-3333-4333-8333-333333333333'

function makePlan(
  overrides: Partial<PmAnalyticsV3ReportPlan> = {},
): PmAnalyticsV3ReportPlan {
  return {
    schemaVersion: 3,
    catalogVersion: 1,
    subject: 'pmExecution',
    mode: 'Aggregate',
    period: {
      basis: 'pmCycle',
      kind: 'Year',
      year: 2024,
      month: null,
      quarter: null,
      startCycle: null,
      endCycle: null,
    },
    categories: ['fire-extinguisher'],
    measures: ['progressPercent'],
    groupBy: [],
    baseFilters: [],
    matchingFilters: [],
    aggregateFilters: [],
    orderBy: [],
    top: null,
    presentation: 'Summary',
    columns: ['progressPercent'],
    ...overrides,
  }
}

function makeCatalog(): PmAnalyticsV3CatalogResponse {
  return {
    schemaVersion: 3,
    catalogVersion: 1,
    subject: 'pmExecution',
    categories: [
      {
        id: 'fire-extinguisher',
        label: 'Fire Extinguisher',
        scheduledMonths: [2, 5, 8, 11],
      },
    ],
    measures: [
      {
        id: 'progressPercent',
        label: 'Progress',
        valueType: 'decimal',
        unit: 'percent',
        grain:
          'completed eligible asset-cycles divided by scheduled eligible asset-cycles',
        population: 'eligible population',
        definition:
          'Completed count divided by eligible scheduled count, multiplied by 100.',
      },
      {
        id: 'scheduledCount',
        label: 'Scheduled',
        valueType: 'integer',
        unit: 'asset-cycles',
        grain: 'one eligible asset-cycle',
        population: 'eligible population',
        definition: 'Count eligible persisted asset-cycle rows.',
      },
      {
        id: 'distinctAssetCount',
        label: 'Distinct assets',
        valueType: 'integer',
        unit: 'assets',
        grain: 'unique assets in the selected population',
        population: 'eligible population',
        definition: 'Count unique asset IDs in the selected population.',
      },
    ],
    dimensions: [
      {
        id: 'department',
        label: 'Department',
        valueType: 'string',
        nullSemantics: 'Unknown values form a group.',
        grain: 'current asset',
        definition: 'Current department.',
      },
    ],
    filterFields: [],
    recordColumns: [
      {
        id: 'scheduleId',
        label: 'Schedule ID',
        valueType: 'guid',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'assetId',
        label: 'Asset ID',
        valueType: 'guid',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'assetCode',
        label: 'Asset code',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'assetCategory',
        label: 'Asset category',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'department',
        label: 'Current department',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'building',
        label: 'Current building',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'pmCycle',
        label: 'PM cycle',
        valueType: 'string',
        unit: 'yyyy-MM',
        nullSemantics: null,
      },
      {
        id: 'deadline',
        label: 'Deadline',
        valueType: 'date-time',
        unit: 'Asia/Manila',
        nullSemantics: null,
      },
      {
        id: 'completedAt',
        label: 'Completed at',
        valueType: 'date-time',
        unit: 'UTC',
        nullSemantics: null,
      },
      {
        id: 'condition',
        label: 'Inspection condition',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
      {
        id: 'timeliness',
        label: 'Timeliness',
        valueType: 'string',
        unit: '',
        nullSemantics: null,
      },
    ],
  }
}

function makeInterpretation(
  plan = makePlan(),
  overrides: Partial<PmAnalyticsV3InterpretationResponse> = {},
): PmAnalyticsV3InterpretationResponse {
  return {
    status: 'Valid',
    plan,
    clarificationFields: [],
    code: null,
    detail: null,
    ...overrides,
  }
}

function makePreview(
  plan: PmAnalyticsV3ReportPlan,
): PmAnalyticsV3PreviewResponse {
  return {
    previewId: 'signed-preview-token',
    expiresAtUtc: '2025-01-01T01:10:00Z',
    plan,
    cyclesByCategory: { 'fire-extinguisher': ['2024-02', '2024-05'] },
    summary:
      'Calculate progress on scheduled PM cycles for Fire Extinguisher: 2024.',
    scopeNotes: ['Cancelled schedules are excluded.'],
  }
}

function makeExecution(
  plan = makePlan(),
  mode: 'Aggregate' | 'Records' = 'Aggregate',
): PmAnalyticsV3ExecutionResponse {
  const recordColumns = makeCatalog().recordColumns
  const aggregate = mode === 'Aggregate'
  const measureDefinitions = makeCatalog().measures
  const aggregateValues = Object.fromEntries([
    ...(plan.groupBy ?? []).map((dimension) => [dimension, 'TEST DEPARTMENT']),
    ...plan.measures.map((measureId) => [
      measureId,
      measureId === 'distinctAssetCount'
        ? 1
        : measureId === 'progressPercent'
          ? 100
          : 2,
    ]),
  ])
  const aggregateMeasures = Object.fromEntries(
    plan.measures.map((measureId) => {
      const definition = measureDefinitions.find(
        (item) => item.id === measureId,
      )
      const value = aggregateValues[measureId] as number
      return [
        measureId,
        {
          value,
          numerator: definition?.unit === 'percent' ? 2 : value,
          denominator: definition?.unit === 'percent' ? 2 : value,
          unit: definition?.unit ?? '',
          isMeasurable: true,
          unmeasurableReason: null,
          eligibleRowCount: 2,
          matchingRowCount: 2,
        },
      ]
    }),
  )
  return {
    schemaVersion: 3,
    catalogVersion: 1,
    asOfUtc: '2025-01-01T01:00:00Z',
    plan,
    cyclesByCategory: { 'fire-extinguisher': ['2024-02', '2024-05'] },
    mode,
    columns: aggregate
      ? [
          {
            id: 'progressPercent',
            label: 'Progress',
            valueType: 'decimal',
            unit: 'percent',
            nullSemantics: null,
          },
        ]
      : recordColumns.map((column) => ({
          id: column.id,
          label: column.label,
          valueType: column.valueType,
          unit: column.unit,
          nullSemantics: column.nullSemantics,
        })),
    population: {
      eligibleAssetCycleCount: 2,
      eligibleDistinctAssetCount: 1,
      matchingAssetCycleCount: 2,
      matchingDistinctAssetCount: 1,
      sourceTotalCount: 2,
      groupsBeforeAggregateFilters: aggregate ? 1 : 0,
      groupsAfterAggregateFilters: aggregate ? 1 : 0,
      groupsReturnedAfterTop: aggregate ? 1 : 0,
    },
    page: 1,
    pageSize: 25,
    totalPages: 1,
    aggregateRows: aggregate
      ? [
          {
            values: aggregateValues,
            measures: aggregateMeasures,
          },
        ]
      : [],
    records: aggregate
      ? []
      : [
          {
            scheduleId,
            assetId,
            inspectionId,
            values: {
              scheduleId,
              assetId,
              assetCode: 'FE-001',
              assetCategory: 'fire-extinguisher',
              department: 'TEST DEPARTMENT',
              building: 'TEST BUILDING',
              pmCycle: '2024-02',
              deadline: '2024-02-29T15:59:59.999Z',
              completedAt: '2024-02-20T04:00:00Z',
              condition: 'Operational',
              timeliness: 'OnTime',
            },
          },
        ],
    scopeNotes: ['The report uses eligible persisted schedules.'],
  }
}

async function interpretAndPreview(
  plan = makePlan(),
  question = 'Show progress for fire extinguishers during 2024',
) {
  fireEvent.change(
    screen.getByRole('textbox', { name: 'Describe the report' }),
    {
      target: { value: question },
    },
  )
  await waitFor(() =>
    expect(
      screen.getByRole('button', { name: 'Fill plan from question' }),
    ).toBeEnabled(),
  )
  fireEvent.click(
    screen.getByRole('button', { name: 'Fill plan from question' }),
  )
  expect(
    await screen.findByText('A report plan is ready to review.'),
  ).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
  expect(
    await screen.findByRole('heading', { name: 'Report preview' }),
  ).toBeInTheDocument()
  expect(api.interpret).toHaveBeenCalledWith(
    { question },
    expect.any(AbortSignal),
  )
  expect(api.preview).toHaveBeenCalledWith({ plan }, expect.any(AbortSignal))
}

describe('PM analytics V3 workflow', () => {
  beforeEach(() => {
    api.catalog.mockReset().mockResolvedValue(makeCatalog())
    api.filterValues.mockReset()
    api.interpret.mockReset().mockResolvedValue(makeInterpretation())
    api.preview
      .mockReset()
      .mockImplementation(async ({ plan }: { plan: PmAnalyticsV3ReportPlan }) =>
        makePreview(plan),
      )
    api.execute.mockReset()
  })

  it('loads the catalog, carries an interpreted plan through confirmation, and drills into numerator sources', async () => {
    const plan = makePlan()
    api.interpret.mockResolvedValueOnce(makeInterpretation(plan))
    api.execute
      .mockResolvedValueOnce(makeExecution(plan))
      .mockImplementationOnce(async () => {
        const recordPlan = api.preview.mock.calls.at(-1)?.[0]
          .plan as PmAnalyticsV3ReportPlan
        return makeExecution(recordPlan, 'Records')
      })

    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview(plan)
    expect(screen.getByText(/Measure definitions/)).toBeInTheDocument()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and run report' }),
    )
    expect(await screen.findByText('100%')).toBeInTheDocument()
    expect(screen.getByText(/2 of 2 eligible asset cycles/)).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'View contributing records' }),
    )
    expect(
      await screen.findByText(/records plan for Progress is ready/),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'PM execution results' }),
    ).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    await waitFor(() => expect(api.preview).toHaveBeenCalledTimes(2))
    const sourcePlan = api.preview.mock.calls.at(1)?.[0]
      .plan as PmAnalyticsV3ReportPlan
    expect(sourcePlan.mode).toBe('Records')
    expect(sourcePlan.matchingFilters).toEqual([
      {
        field: 'condition',
        operator: 'In',
        values: ['Operational', 'NonOperational'],
      },
    ])
    fireEvent.click(
      await screen.findByRole('button', { name: 'Confirm and run report' }),
    )
    expect(
      await screen.findByRole('link', { name: 'Review inspection' }),
    ).toHaveAttribute('href', `/app/inspections/${inspectionId}`)
  })

  it('clears a previous preview and result when the question changes', async () => {
    api.execute.mockResolvedValueOnce(makeExecution())
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and run report' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'PM execution results' }),
    ).toBeInTheDocument()

    fireEvent.change(
      screen.getByRole('textbox', { name: 'Describe the report' }),
      {
        target: { value: 'Show something else' },
      },
    )

    expect(
      screen.queryByRole('heading', { name: 'Report preview' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'PM execution results' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('100%', { exact: true })).not.toBeInTheDocument()
  })

  it('does not offer an inaccurate source drill-down for distinct asset counts', async () => {
    const plan = makePlan({
      measures: ['distinctAssetCount'],
      groupBy: ['department'],
      presentation: 'Table',
      columns: ['department', 'distinctAssetCount'],
    })
    api.interpret.mockResolvedValueOnce(makeInterpretation(plan))
    api.execute.mockResolvedValueOnce(makeExecution(plan))
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview(
      plan,
      'Show distinct assets for fire extinguishers by department in 2024',
    )
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and run report' }),
    )

    expect(
      await screen.findByRole('button', {
        name: /View Distinct assets records/,
      }),
    ).toBeDisabled()
  })

  it('shows an empty state when an aggregate summary returns no groups', async () => {
    const execution = makeExecution()
    execution.aggregateRows = []
    execution.population.groupsBeforeAggregateFilters = 0
    execution.population.groupsAfterAggregateFilters = 0
    execution.population.groupsReturnedAfterTop = 0
    api.execute.mockResolvedValueOnce(execution)
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and run report' }),
    )

    expect(
      await screen.findByText('No groups met the selected filters.'),
    ).toHaveAttribute('role', 'status')
  })

  it('clears expired previews and offers a new preview action', async () => {
    api.execute.mockRejectedValueOnce(
      new ApiError('Preview expired', 409, {
        extensions: { code: 'PreviewUnavailable' },
      }),
    )
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and run report' }),
    )

    expect(
      await screen.findByText(/This preview expired or is no longer available/),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Report preview' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Preview report' })).toBeEnabled()
    expect(
      screen.queryByRole('heading', { name: 'PM execution results' }),
    ).not.toBeInTheDocument()
  })

  it('announces catalog loading and keeps interpretation disabled until metadata is ready', async () => {
    let resolveCatalog!: (value: PmAnalyticsV3CatalogResponse) => void
    api.catalog.mockReturnValueOnce(
      new Promise<PmAnalyticsV3CatalogResponse>((resolve) => {
        resolveCatalog = resolve
      }),
    )
    render(<PmAnalyticsV3Panel />)
    expect(screen.getByText('Loading the report catalog…')).toHaveAttribute(
      'role',
      'status',
    )
    fireEvent.change(
      screen.getByRole('textbox', { name: 'Describe the report' }),
      {
        target: { value: 'Show scheduled fire extinguishers in 2024' },
      },
    )
    const submit = screen.getByRole('button', {
      name: 'Fill plan from question',
    })
    expect(submit).toBeDisabled()

    await act(async () => resolveCatalog(makeCatalog()))
    await waitFor(() => expect(submit).toBeEnabled())
  })

  it('shows a recoverable catalog error and exposes clarification before preview', async () => {
    api.catalog.mockRejectedValueOnce(new ApiError('Catalog offline'))
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(makePlan(), {
        status: 'NeedsClarification',
        plan: null,
        clarificationFields: ['Top'],
        code: 'RankingNeedsClarification',
        detail: 'Choose a ranking measure and group limit.',
      }),
    )
    render(<PmAnalyticsV3Panel />)
    expect(
      await screen.findByText(/The report catalog could not be loaded/),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Retry catalog' }))
    await waitFor(() => expect(api.catalog).toHaveBeenCalledTimes(2))
    await waitFor(() =>
      expect(
        screen.queryByText(/The report catalog could not be loaded/),
      ).not.toBeInTheDocument(),
    )

    fireEvent.change(
      screen.getByRole('textbox', { name: 'Describe the report' }),
      {
        target: { value: 'Show top department results' },
      },
    )
    fireEvent.click(
      screen.getByRole('button', { name: 'Fill plan from question' }),
    )
    expect(
      await screen.findByText('The question needs more detail.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Please clarify: ranking measure and limit/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Preview report' }),
    ).toBeDisabled()
  })
})
