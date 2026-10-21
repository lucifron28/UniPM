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
import { PmAnalyticsV3FilterOperator } from '@/api/generated/models'
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
    catalogVersion: 2,
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
    catalogVersion: 2,
    subject: 'pmExecution',
    subjects: [
      {
        id: 'pmExecution',
        label: 'PM execution',
        description: 'Eligible PM schedule rows.',
        requiresPeriod: true,
      },
      {
        id: 'assetInventory',
        label: 'Asset inventory',
        description: 'One registered asset per row.',
        requiresPeriod: false,
      },
    ],
    categories: [
      {
        id: 'fire-extinguisher',
        label: 'Fire Extinguisher',
        scheduledMonths: [2, 5, 8, 11],
      },
      {
        id: 'water-drinking-station',
        label: 'Water Drinking Station',
        scheduledMonths: [2, 5, 8, 11],
      },
      {
        id: 'fire-alarm',
        label: 'Fire Alarm',
        scheduledMonths: [6, 12],
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
      {
        id: 'calendarQuarter',
        label: 'Calendar quarter',
        valueType: 'string',
        nullSemantics: 'Unknown values form a group.',
        grain: 'PM cycle',
        definition: 'Calendar quarter derived from the PM cycle.',
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

function makeInventoryCatalog(): PmAnalyticsV3CatalogResponse {
  const catalog = makeCatalog()
  return {
    ...catalog,
    subject: 'assetInventory',
    measures: [
      {
        id: 'registeredAssetCount',
        label: 'Registered assets',
        valueType: 'integer',
        unit: 'assets',
        grain: 'one registered Asset row per unique asset ID',
        population: 'filtered registered-asset population',
        definition:
          'Count unique registered asset IDs after category and population filters.',
      },
    ],
    dimensions: [
      {
        id: 'assetCategory',
        label: 'Asset category',
        valueType: 'string',
        nullSemantics: 'Not null.',
        grain: 'asset',
        definition: 'Canonical asset category.',
      },
      {
        id: 'department',
        label: 'Department',
        valueType: 'string',
        nullSemantics: 'Unknown values form a group.',
        grain: 'current asset',
        definition: 'Current department.',
      },
      {
        id: 'building',
        label: 'Building',
        valueType: 'string',
        nullSemantics: 'Unknown values form a group.',
        grain: 'current asset',
        definition: 'Current building.',
      },
      {
        id: 'location',
        label: 'General location',
        valueType: 'string',
        nullSemantics: 'Unknown values form a group.',
        grain: 'current asset',
        definition: 'Current location.',
      },
      {
        id: 'registrationStatus',
        label: 'Registration status',
        valueType: 'string',
        nullSemantics: 'Stored catalog status.',
        grain: 'current asset',
        definition:
          'Stored registration status, separate from inspection condition.',
      },
    ],
    filterFields: [
      ...['department', 'building', 'location'].map((id) => ({
        id,
        label:
          id === 'location'
            ? 'General location'
            : id.charAt(0).toUpperCase() + id.slice(1),
        isBaseFilter: true,
        valueType: 'string',
        maximumLength: 256,
        allowedValues: [],
        operators: [
          PmAnalyticsV3FilterOperator.Equal,
          PmAnalyticsV3FilterOperator.In,
          PmAnalyticsV3FilterOperator.IsNull,
        ],
      })),
      {
        id: 'registrationStatus',
        label: 'Registration status',
        isBaseFilter: true,
        valueType: 'registrationStatus',
        maximumLength: 16,
        allowedValues: ['Active', 'Inactive', 'Retired'],
        operators: [
          PmAnalyticsV3FilterOperator.Equal,
          PmAnalyticsV3FilterOperator.In,
        ],
      },
    ],
    recordColumns: [
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
        nullSemantics: 'Current Asset value.',
      },
      {
        id: 'building',
        label: 'Current building',
        valueType: 'string',
        unit: '',
        nullSemantics: 'Current Asset value.',
      },
      {
        id: 'location',
        label: 'General location',
        valueType: 'string',
        unit: '',
        nullSemantics: 'Current Asset value.',
      },
      {
        id: 'registrationStatus',
        label: 'Registration status',
        valueType: 'string',
        unit: '',
        nullSemantics: 'Stored status.',
      },
    ],
  }
}

function makeInventoryExecution(
  plan: PmAnalyticsV3ReportPlan,
): PmAnalyticsV3ExecutionResponse {
  return {
    schemaVersion: 3,
    catalogVersion: 2,
    asOfUtc: '2025-01-01T01:00:00Z',
    plan,
    cyclesByCategory: {},
    mode: 'Aggregate',
    columns: [
      {
        id: 'registrationStatus',
        label: 'Registration status',
        valueType: 'string',
        unit: '',
        nullSemantics: 'Stored status.',
      },
      {
        id: 'registeredAssetCount',
        label: 'Registered assets',
        valueType: 'integer',
        unit: 'assets',
        nullSemantics: null,
      },
    ],
    population: {
      eligibleAssetCycleCount: 0,
      eligibleDistinctAssetCount: 2,
      matchingAssetCycleCount: 0,
      matchingDistinctAssetCount: 2,
      sourceTotalCount: 2,
      groupsBeforeAggregateFilters: 2,
      groupsAfterAggregateFilters: 2,
      groupsReturnedAfterTop: 2,
    },
    page: 1,
    pageSize: 25,
    totalPages: 1,
    aggregateRows: [
      {
        values: { registrationStatus: 'Active' },
        measures: {
          registeredAssetCount: {
            value: 1,
            numerator: 1,
            denominator: 0,
            unit: 'assets',
            isMeasurable: true,
            unmeasurableReason: null,
            eligibleRowCount: 1,
            matchingRowCount: 1,
          },
        },
      },
      {
        values: { registrationStatus: 'Retired' },
        measures: {
          registeredAssetCount: {
            value: 1,
            numerator: 1,
            denominator: 0,
            unit: 'assets',
            isMeasurable: true,
            unmeasurableReason: null,
            eligibleRowCount: 1,
            matchingRowCount: 1,
          },
        },
      },
    ],
    records: [],
    scopeNotes: ['One Asset row per ID.'],
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
    interpreter: 'DeterministicParser',
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
    cyclesByCategory: {
      'fire-extinguisher': ['2024-02', '2024-05', '2024-08', '2024-11'],
      'water-drinking-station': ['2024-02', '2024-05', '2024-08', '2024-11'],
      'fire-alarm': ['2024-06', '2024-12'],
    },
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
    catalogVersion: 2,
    asOfUtc: '2025-01-01T01:00:00Z',
    plan,
    cyclesByCategory: {
      'fire-extinguisher': ['2024-02', '2024-05', '2024-08', '2024-11'],
      'water-drinking-station': ['2024-02', '2024-05', '2024-08', '2024-11'],
      'fire-alarm': ['2024-06', '2024-12'],
    },
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
  fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
    target: { value: question },
  })
  await waitFor(() =>
    expect(
      screen.getByRole('button', { name: 'Generate report' }),
    ).toBeEnabled(),
  )
  fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
  expect(await screen.findByText('Report ready for review')).toBeInTheDocument()
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

  it('starts with one question input and no subject or manual report controls', async () => {
    render(<PmAnalyticsV3Panel />)

    expect(
      await screen.findByRole('heading', {
        name: 'Ask UniPM',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Report subject')).not.toBeInTheDocument()
    expect(
      screen.getByRole('textbox', { name: 'Question' }),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Report type')).not.toBeInTheDocument()
    expect(
      screen.queryByLabelText('Scheduled PM cycle period'),
    ).not.toBeInTheDocument()

    const example = screen.getByRole('button', {
      name: 'How many fire extinguishers are registered in the CCMS building?',
    })
    fireEvent.click(example)
    expect(screen.getByRole('textbox', { name: 'Question' })).toHaveValue(
      'How many fire extinguishers are registered in the CCMS building?',
    )
    expect(api.interpret).not.toHaveBeenCalled()

    expect(
      screen.queryByRole('button', { name: 'Build report manually' }),
    ).not.toBeInTheDocument()
  })

  it('summarizes the returned plan without exposing the manual builder', async () => {
    const plan = makePlan({
      groupBy: ['department'],
      presentation: 'Table',
      columns: ['department', 'progressPercent'],
    })
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(plan, { interpreter: 'DeepSeek' }),
    )
    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show progress by department in 2024' },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByRole('heading', { name: 'Report ready for review' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Interpreted by DeepSeek')).toBeInTheDocument()
    expect(screen.getByText('PM execution')).toBeInTheDocument()
    expect(screen.getByText('2024')).toBeInTheDocument()
    expect(screen.getByText('Fire Extinguisher')).toBeInTheDocument()
    expect(screen.getByText('Progress')).toBeInTheDocument()
    expect(screen.getByText('Department')).toBeInTheDocument()
    expect(api.preview).not.toHaveBeenCalled()
    expect(api.execute).not.toHaveBeenCalled()

    expect(
      screen.queryByRole('button', { name: 'Edit report parameters' }),
    ).not.toBeInTheDocument()
  })

  it('keeps provider failures visible and leaves the question editable', async () => {
    api.interpret.mockRejectedValueOnce(
      new ApiError('DeepSeek unavailable', 503, {
        extensions: { code: 'ProviderUnavailable' },
      }),
    )
    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show progress for fire extinguishers during 2024' },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'choose the deterministic parser explicitly',
    )
    expect(screen.getByRole('textbox', { name: 'Question' })).toHaveValue(
      'Show progress for fire extinguishers during 2024',
    )
    expect(
      screen.queryByRole('button', { name: 'Build report manually' }),
    ).not.toBeInTheDocument()
  })

  it('builds, previews, and runs a schedule-independent inventory report', async () => {
    const plan: PmAnalyticsV3ReportPlan = {
      ...makePlan(),
      subject: 'assetInventory',
      mode: 'Aggregate',
      period: null,
      categories: ['fire-extinguisher'],
      measures: ['registeredAssetCount'],
      groupBy: ['registrationStatus'],
      baseFilters: [
        {
          field: 'building',
          operator: PmAnalyticsV3FilterOperator.Equal,
          values: ['CCMS'],
        },
      ],
      matchingFilters: [],
      aggregateFilters: [],
      orderBy: [],
      top: null,
      presentation: 'Table',
      columns: ['registrationStatus', 'registeredAssetCount'],
    }
    api.interpret.mockResolvedValueOnce(makeInterpretation(plan))
    api.catalog.mockImplementation(async (params?: { subject?: string }) =>
      params?.subject === 'assetInventory'
        ? makeInventoryCatalog()
        : makeCatalog(),
    )
    api.preview.mockImplementation(
      async ({ plan }: { plan: PmAnalyticsV3ReportPlan }) => ({
        ...makePreview(plan),
        cyclesByCategory: {},
        summary: 'Count registered assets grouped by registration status.',
        scopeNotes: ['One Asset row per ID; no PM schedule joins.'],
      }),
    )
    api.execute.mockImplementation(async () => {
      const plan = api.preview.mock.calls.at(-1)?.[0]
        .plan as PmAnalyticsV3ReportPlan
      return makeInventoryExecution(plan)
    })

    render(<PmAnalyticsV3Panel />)
    await screen.findByRole('heading', {
      name: 'Ask UniPM',
    })
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: {
        value:
          'How many fire extinguishers are registered in the CCMS building by status?',
      },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByRole('heading', { name: 'Report ready for review' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Asset inventory')).toBeInTheDocument()
    expect(screen.getByText('Building is CCMS')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(api.interpret).toHaveBeenCalledWith(
      {
        question:
          'How many fire extinguishers are registered in the CCMS building by status?',
      },
      expect.any(AbortSignal),
    )
    const previewPlan = api.preview.mock.calls.at(-1)?.[0]
      .plan as PmAnalyticsV3ReportPlan
    expect(previewPlan.subject).toBe('assetInventory')
    expect(previewPlan.period).toBeNull()
    expect(previewPlan.baseFilters).toEqual(plan.baseFilters)
    expect(
      screen.queryByLabelText('Scheduled PM cycle period'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText('Matching record filters'),
    ).not.toBeInTheDocument()

    expect(
      screen.queryByText('Included PM cycles by category'),
    ).not.toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and generate' }),
    )
    expect(
      await screen.findByRole('heading', {
        name: 'Registered asset inventory results',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getAllByText(/no denominator applies/i).length,
    ).toBeGreaterThan(0)
    expect(api.interpret).toHaveBeenCalledTimes(1)
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
      screen.getByRole('button', { name: 'Confirm and generate' }),
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
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
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
      await screen.findByRole('button', { name: 'Confirm and generate' }),
    )
    expect(
      await screen.findByRole('link', { name: 'Review inspection' }),
    ).toHaveAttribute('href', `/app/inspections/${inspectionId}`)
  })

  it.each([
    {
      quarter: '2024-Q1',
      quarterNumber: 1,
      expectedCategories: ['fire-extinguisher', 'water-drinking-station'],
      sourceCycle: '2024-02',
    },
    {
      quarter: '2024-Q4',
      quarterNumber: 4,
      expectedCategories: [
        'fire-extinguisher',
        'water-drinking-station',
        'fire-alarm',
      ],
      sourceCycle: '2024-11',
    },
  ])(
    'keeps CPMP categories, period, filters, and source records for $sourceCycle quarter drill-down',
    async ({ quarter, quarterNumber, expectedCategories, sourceCycle }) => {
      const plan = makePlan({
        categories: [
          'fire-extinguisher',
          'water-drinking-station',
          'fire-alarm',
        ],
        groupBy: ['calendarQuarter'],
        baseFilters: [
          {
            field: 'building',
            operator: 'Equal',
            values: ['Engineering Hall'],
          },
        ],
        columns: ['calendarQuarter', 'progressPercent'],
        presentation: 'Table',
      })
      const aggregateExecution = makeExecution(plan)
      aggregateExecution.columns = [
        {
          id: 'calendarQuarter',
          label: 'Calendar quarter',
          valueType: 'string',
          unit: '',
          nullSemantics: null,
        },
        {
          id: 'progressPercent',
          label: 'Progress',
          valueType: 'decimal',
          unit: 'percent',
          nullSemantics: null,
        },
      ]
      const aggregateRow = aggregateExecution.aggregateRows[0]
      if (!aggregateRow) throw new Error('Expected one aggregate row')
      aggregateRow.values.calendarQuarter = quarter

      api.interpret.mockResolvedValueOnce(makeInterpretation(plan))
      api.execute
        .mockResolvedValueOnce(aggregateExecution)
        .mockImplementationOnce(async () => {
          const recordPlan = api.preview.mock.calls.at(-1)?.[0]
            .plan as PmAnalyticsV3ReportPlan
          const recordsExecution = makeExecution(recordPlan, 'Records')
          const [record] = recordsExecution.records
          if (!record) throw new Error('Expected one source record')
          record.values.assetCategory = 'water-drinking-station'
          record.values.assetCode = 'WDS-001'
          record.values.pmCycle = sourceCycle
          recordsExecution.population.sourceTotalCount = 1
          return recordsExecution
        })

      render(<PmAnalyticsV3Panel />)
      await interpretAndPreview(
        plan,
        'Show progress by calendar quarter for fire extinguishers, water stations, and fire alarms during 2024',
      )
      fireEvent.click(
        screen.getByRole('button', { name: 'Confirm and generate' }),
      )
      fireEvent.click(
        await screen.findByRole('button', {
          name: /View Progress records · Calendar quarter/,
        }),
      )

      expect(
        await screen.findByText(/records plan for Progress is ready/),
      ).toBeInTheDocument()
      await waitFor(() => expect(api.preview).toHaveBeenCalledTimes(2))
      const sourcePlan = api.preview.mock.calls.at(1)?.[0]
        .plan as PmAnalyticsV3ReportPlan
      expect(sourcePlan).toMatchObject({
        mode: 'Records',
        categories: expectedCategories,
        period: {
          basis: 'pmCycle',
          kind: 'Quarter',
          year: 2024,
          month: null,
          quarter: quarterNumber,
          startCycle: null,
          endCycle: null,
        },
        baseFilters: [
          {
            field: 'building',
            operator: 'Equal',
            values: ['Engineering Hall'],
          },
        ],
        matchingFilters: [
          {
            field: 'condition',
            operator: 'In',
            values: ['Operational', 'NonOperational'],
          },
        ],
      })

      fireEvent.click(
        await screen.findByRole('button', { name: 'Confirm and generate' }),
      )
      expect(await screen.findByText('WDS-001')).toBeInTheDocument()
      expect(await screen.findByText(sourceCycle)).toBeInTheDocument()
      expect(
        await screen.findByRole('link', { name: 'Review inspection' }),
      ).toHaveAttribute('href', `/app/inspections/${inspectionId}`)
    },
  )

  it('clears a previous preview and result when starting another question', async () => {
    api.execute.mockResolvedValueOnce(makeExecution())
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and generate' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'PM execution results' }),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Ask another question' }),
    )

    expect(screen.getByRole('textbox', { name: 'Question' })).toHaveValue('')

    expect(
      screen.queryByRole('heading', { name: 'Report preview' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'PM execution results' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('100%', { exact: true })).not.toBeInTheDocument()
  })

  it('clears a preview when starting a new question and previews its new scope', async () => {
    const updatedPlan = makePlan({
      period: {
        basis: 'pmCycle',
        kind: 'Year',
        year: 2023,
        month: null,
        quarter: null,
        startCycle: null,
        endCycle: null,
      },
    })
    api.interpret
      .mockResolvedValueOnce(makeInterpretation())
      .mockResolvedValueOnce(makeInterpretation(updatedPlan))
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()

    fireEvent.click(
      screen.getByRole('button', { name: 'Ask another question' }),
    )
    expect(screen.getByRole('textbox', { name: 'Question' })).toHaveValue('')
    expect(
      screen.queryByRole('heading', { name: 'Report preview' }),
    ).not.toBeInTheDocument()
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show progress for fire extinguishers during 2023' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(
      await screen.findByText('Report ready for review'),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    await waitFor(() => expect(api.preview).toHaveBeenCalledTimes(2))
    expect(api.preview.mock.calls[1]?.[0].plan.period?.year).toBe(2023)
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
      screen.getByRole('button', { name: 'Confirm and generate' }),
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
      screen.getByRole('button', { name: 'Confirm and generate' }),
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
      screen.getByRole('button', { name: 'Confirm and generate' }),
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
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show scheduled fire extinguishers in 2024' },
    })
    const submit = screen.getByRole('button', {
      name: 'Generate report',
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

    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show top department results' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(
      await screen.findByText('The question needs more detail.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Please clarify: ranking measure and limit/),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Preview report' }),
    ).not.toBeInTheDocument()
  })
})
