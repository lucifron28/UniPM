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
  createSaved: vi.fn(),
  deleteSaved: vi.fn(),
  listSaved: vi.fn(),
  previewSaved: vi.fn(),
  updateSaved: vi.fn(),
}))

vi.mock('@/api/generated/endpoints', () => ({
  getPmAnalyticsV3Catalog: api.catalog,
  getPmAnalyticsV3FilterValues: api.filterValues,
  interpretPmAnalyticsV3Question: api.interpret,
  previewPmAnalyticsV3Report: api.preview,
  executePmAnalyticsV3Report: api.execute,
  createSavedAnalyticsReport: api.createSaved,
  deleteSavedAnalyticsReport: api.deleteSaved,
  listSavedAnalyticsReports: api.listSaved,
  previewSavedAnalyticsReport: api.previewSaved,
  updateSavedAnalyticsReport: api.updateSaved,
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
        id: 'completedLateCount',
        label: 'Completed late',
        valueType: 'integer',
        unit: 'asset-cycles',
        grain: 'one eligible persisted schedule for one asset and PM cycle',
        population: 'eligible population',
        definition:
          'Count completed eligible rows whose completion is after the PM-cycle deadline.',
      },
      {
        id: 'missedCount',
        label: 'Missed',
        valueType: 'integer',
        unit: 'asset-cycles',
        grain: 'one eligible persisted schedule for one asset and PM cycle',
        population: 'eligible population',
        definition:
          'Count incomplete eligible rows whose PM-cycle deadline has passed.',
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
  plan: PmAnalyticsV3ReportPlan = makePlan(),
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
  expect(await screen.findByText('Review your report')).toBeInTheDocument()
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
    api.createSaved.mockReset()
    api.deleteSaved.mockReset()
    api.listSaved.mockReset().mockResolvedValue([])
    api.previewSaved.mockReset()
    api.updateSaved.mockReset()
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

  it('reopens a saved report with a fresh preview and executes only after confirmation', async () => {
    const savedPlan = makePlan()
    api.listSaved.mockResolvedValueOnce([
      {
        id: '99999999-9999-4999-8999-999999999999',
        name: 'Routine progress',
        plan: savedPlan,
        periodMode: 'Fixed',
        revision: '00000000-0000-4000-8000-000000000001',
        updatedAtUtc: '2026-10-26T00:00:00Z',
      },
    ])
    api.previewSaved.mockResolvedValueOnce(makePreview(savedPlan))
    api.execute.mockResolvedValueOnce(makeExecution(savedPlan))
    render(<PmAnalyticsV3Panel />)

    fireEvent.click(await screen.findByText('My reports (1)'))
    fireEvent.click(
      await screen.findByRole('button', { name: 'Open preview' }),
    )
    expect(api.previewSaved).toHaveBeenCalledWith(
      '99999999-9999-4999-8999-999999999999',
      expect.any(AbortSignal),
    )
    expect(api.interpret).not.toHaveBeenCalled()
    expect(api.execute).not.toHaveBeenCalled()
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and generate' }),
    )
    expect(await screen.findByText('100%', { exact: true })).toBeInTheDocument()
    expect(api.execute).toHaveBeenCalledWith(
      { previewId: 'signed-preview-token', page: 1, pageSize: 25 },
      expect.any(AbortSignal),
    )
    expect(api.interpret).not.toHaveBeenCalled()
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
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Ask UniPM' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Clarify the report scope' }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByText('Period based on scheduled PM cycles: 2024'),
    ).toBeInTheDocument()
    expect(screen.getByText('Fire Extinguisher')).toBeInTheDocument()
    expect(screen.getByText('Completion progress')).toBeInTheDocument()
    expect(screen.getByText('Department')).toBeInTheDocument()
    fireEvent.click(screen.getByText('Report details'))
    expect(screen.getByText('Interpreted by DeepSeek')).toBeInTheDocument()
    expect(screen.getByText('Preventive-maintenance work')).toBeInTheDocument()
    expect(api.preview).not.toHaveBeenCalled()
    expect(api.execute).not.toHaveBeenCalled()

    expect(
      screen.queryByRole('button', { name: 'Edit report parameters' }),
    ).not.toBeInTheDocument()
  })

  it('keeps provider failures visible, leaves the question editable, and offers deterministic parser', async () => {
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

    const parserButton = screen.getByRole('button', {
      name: 'Use deterministic parser',
    })
    expect(parserButton).toBeVisible()
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(makePlan(), { interpreter: 'DeterministicParser' }),
    )
    fireEvent.click(parserButton)
    await waitFor(() =>
      expect(api.interpret).toHaveBeenLastCalledWith(
        expect.objectContaining({
          question: 'Show progress for fire extinguishers during 2024',
          interpreter: 'DeterministicParser',
        }),
        expect.anything(),
      ),
    )
    expect(
      await screen.findByText(/Interpreted by the deterministic parser/),
    ).toBeInTheDocument()
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
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByText('Report details'))
    expect(
      await screen.findByText('Registered asset inventory'),
    ).toBeInTheDocument()
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
    fireEvent.click(screen.getByText('Report details'))
    expect(
      screen.getByText(/Counting definitions and population/),
    ).toBeInTheDocument()
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
    expect(await screen.findByText('Review your report')).toBeInTheDocument()
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
      await screen.findByRole('heading', { name: 'Clarify the report scope' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'UniPM did not return a safe partial plan, so there is no report scope to preview. Edit the question to make the missing scope clear, or try interpretation again.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Edit question' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Choose a ranking measure and group limit.'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Preview report' }),
    ).not.toBeInTheDocument()
  })

  it('keeps a generic clarification filter in the preview plan', async () => {
    const partialPlan = makePlan()
    api.catalog.mockResolvedValueOnce({
      ...makeCatalog(),
      filterFields: makeInventoryCatalog().filterFields.filter(
        (field) => field.id === 'department',
      ),
    })
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(partialPlan, {
        status: 'NeedsClarification',
        clarificationFields: ['Filters'],
        detail: 'Choose a filter to narrow the results.',
      }),
    )

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Which extinguisher checks need attention?' },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByRole('heading', { name: 'Clarify the report scope' }),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('option', { name: 'Department' }),
    ).toBeInTheDocument()
    fireEvent.change(
      screen.getByRole('combobox', {
        name: 'Which filter should narrow the results?',
      }),
      { target: { value: 'department' } },
    )
    const departmentInput = screen.getByPlaceholderText(
      'Search current asset values',
    )
    fireEvent.change(departmentInput, { target: { value: 'A' } })
    fireEvent.keyDown(departmentInput, { key: 'Enter' })
    fireEvent.click(
      await screen.findByRole('button', { name: 'Use selected filter' }),
    )
    fireEvent.click(
      await screen.findByRole('button', { name: 'Preview report' }),
    )

    await waitFor(() =>
      expect(api.preview).toHaveBeenCalledWith(
        {
          plan: expect.objectContaining({
            baseFilters: [
              {
                field: 'department',
                operator: PmAnalyticsV3FilterOperator.Equal,
                values: ['A'],
              },
            ],
          }),
        },
        expect.any(AbortSignal),
      ),
    )
  })

  it('guides clarification one question at a time and preserves answers when navigating back or changing them', async () => {
    const partialPlan = makePlan({
      categories: ['fire-extinguisher'],
      measures: ['progressPercent'],
      groupBy: ['department'],
      columns: ['department', 'progressPercent'],
      aggregateFilters: [
        {
          measure: 'progressPercent',
          operator: 'GreaterThanOrEqual',
          value: 70,
        },
      ],
      orderBy: [{ field: 'progressPercent', direction: 'Descending' }],
      top: { measure: 'progressPercent', limit: 5, direction: 'Descending' },
      presentation: 'Table',
      period: null,
    })
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(partialPlan, {
        status: 'NeedsClarification',
        clarificationFields: ['Period', 'Measures'],
        detail:
          'Choose whether falling behind means overdue unfinished work or late completed work, and specify the period.',
      }),
    )

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: {
        value: 'Which departments are falling behind on extinguisher checks?',
      },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByRole('heading', { name: 'Clarify the report scope' }),
    ).toBeInTheDocument()

    const overdueChip = screen.getByRole('button', {
      name: 'Overdue and unfinished',
    })
    expect(screen.getByText('Question 1 of 2')).toBeInTheDocument()
    expect(
      screen.getByText('What should the report measure?'),
    ).toBeInTheDocument()
    expect(overdueChip).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Overdue and unfinished' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Which scheduled PM period should be included?'),
    ).not.toBeInTheDocument()

    // Answer the measure question locally. The period is the next step.
    fireEvent.click(overdueChip)
    expect(
      await screen.findByText(
        /Using Overdue and unfinished removed threshold for Progress, ranking by Progress, sorting by Progress/,
      ),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('Which scheduled PM period should be included?'),
    ).toBeInTheDocument()
    expect(screen.getByText('Question 2 of 2')).toBeInTheDocument()
    expect(
      screen.queryByText('What should the report measure?'),
    ).not.toBeInTheDocument()
    expect(api.interpret).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: '2026' })).toBeInTheDocument()

    // Back returns to the measure question and keeps its chosen value.
    fireEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByText('Question 1 of 2')).toBeInTheDocument()
    const preservedMeasureChip = screen.getByRole('button', {
      name: 'Overdue and unfinished',
    })
    expect(preservedMeasureChip).toHaveAttribute('aria-pressed', 'true')
    expect(api.interpret).toHaveBeenCalledTimes(1)

    // Reconfirm the measure and complete the period question.
    fireEvent.click(preservedMeasureChip)
    const periodChip = await screen.findByRole('button', { name: '2026' })
    fireEvent.click(periodChip)
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    expect(
      screen.getAllByText(
        'Which departments are falling behind on extinguisher checks?',
      ),
    ).toHaveLength(1)
    expect(
      screen.getByText(
        'Only departments with at least one overdue, unfinished PM cycle.',
      ),
    ).toBeInTheDocument()

    // Change navigates to an answered step without losing either value.
    fireEvent.click(screen.getByRole('button', { name: 'Change measure' }))
    expect(screen.getByText('Question 1 of 2')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Overdue and unfinished' }),
    ).toHaveAttribute('aria-pressed', 'true')
    fireEvent.click(
      screen.getByRole('button', { name: 'Overdue and unfinished' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Change period' }))
    expect(screen.getByText('Question 2 of 2')).toBeInTheDocument()
    const preservedPeriodChip = screen.getByRole('button', { name: '2026' })
    expect(preservedPeriodChip).toHaveAttribute('aria-pressed', 'true')
    expect(api.interpret).toHaveBeenCalledTimes(1)

    // Returning to the report review is also local; no second interpretation runs.
    fireEvent.click(preservedPeriodChip)
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    const previewButton = screen.getByRole('button', { name: 'Preview report' })
    expect(previewButton).toBeEnabled()

    api.preview.mockResolvedValueOnce(makePreview())
    fireEvent.click(previewButton)

    await waitFor(() =>
      expect(api.preview).toHaveBeenCalledWith(
        {
          plan: expect.objectContaining({
            measures: ['missedCount'],
            categories: ['fire-extinguisher'],
            groupBy: ['department'],
            columns: ['department', 'missedCount'],
            aggregateFilters: [
              {
                measure: 'missedCount',
                operator: 'GreaterThan',
                value: 0,
              },
            ],
            orderBy: [{ field: 'missedCount', direction: 'Descending' }],
            top: null,
            period: expect.objectContaining({
              year: 2026,
            }),
          }),
        },
        expect.anything(),
      ),
    )
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(api.interpret).toHaveBeenCalledTimes(1)
    expect(
      screen.getByText(
        /Using Overdue and unfinished removed threshold for Progress, ranking by Progress, sorting by Progress/,
      ),
    ).toBeVisible()
  })

  it.each([
    {
      label: 'an explicit sort',
      orderBy: [{ field: 'department', direction: 'Ascending' }],
      top: null,
    },
    {
      label: 'an explicit ranking',
      orderBy: [],
      top: {
        measure: 'progressPercent',
        limit: 5,
        direction: 'Descending',
      },
    },
  ] satisfies Array<{
    label: string
    orderBy: PmAnalyticsV3ReportPlan['orderBy']
    top: PmAnalyticsV3ReportPlan['top']
  }>)(
    'preserves $label for departments falling behind',
    async ({ orderBy, top }) => {
      const plan = makePlan({
        measures: ['missedCount', 'progressPercent'],
        groupBy: ['department'],
        columns: ['department', 'missedCount', 'progressPercent'],
        orderBy,
        top,
        presentation: 'Table',
      })
      const question =
        'Which departments are falling behind on extinguisher checks?'
      api.interpret.mockResolvedValueOnce(makeInterpretation(plan))

      render(<PmAnalyticsV3Panel />)
      fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
        target: { value: question },
      })
      await waitFor(() =>
        expect(
          screen.getByRole('button', { name: 'Generate report' }),
        ).toBeEnabled(),
      )
      fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
      expect(
        await screen.findByRole('heading', { name: 'Review your report' }),
      ).toBeInTheDocument()

      fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
      expect(
        await screen.findByRole('heading', { name: 'Report preview' }),
      ).toBeInTheDocument()
      expect(api.preview).toHaveBeenCalledWith(
        {
          plan: expect.objectContaining({
            orderBy,
            top,
            aggregateFilters: [
              {
                measure: 'missedCount',
                operator: 'GreaterThan',
                value: 0,
              },
            ],
          }),
        },
        expect.anything(),
      )
    },
  )

  it('shows every aggregate threshold in review and preview', async () => {
    const plan = makePlan({
      measures: ['missedCount', 'progressPercent'],
      groupBy: ['department'],
      columns: ['department', 'missedCount', 'progressPercent'],
      aggregateFilters: [
        {
          measure: 'missedCount',
          operator: 'GreaterThan',
          value: 0,
        },
        {
          measure: 'progressPercent',
          operator: 'GreaterThanOrEqual',
          value: 70,
        },
      ],
      presentation: 'Table',
    })
    const inclusionCriteria =
      'Only results where Overdue and unfinished PM work is greater than 0 and Completion progress is at least 70.'
    api.interpret.mockResolvedValueOnce(makeInterpretation(plan))

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: {
        value:
          'Which departments are falling behind on extinguisher checks with at least 70% completion progress?',
      },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    expect(screen.getByText(inclusionCriteria)).toBeInTheDocument()
    expect(
      screen.queryByText(
        'Only departments with at least one overdue, unfinished PM cycle.',
      ),
    ).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(screen.getByText(inclusionCriteria)).toBeInTheDocument()
  })

  it('requires an explicit scheduled-cycle choice for unsupported inspection-date requests', async () => {
    const currentYear = new Date().getFullYear()
    const partialPlan = makePlan({ period: null })
    const completedPlan = makePlan({
      period: {
        basis: 'pmCycle',
        kind: 'Year',
        year: currentYear,
        month: null,
        quarter: null,
        startCycle: null,
        endCycle: null,
      },
    })
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(partialPlan, {
        status: 'NeedsClarification',
        clarificationFields: ['Period'],
        code: 'PeriodBasisNeedsClarification',
        detail:
          'Does the requested month mean a scheduled PM cycle or the date the inspection happened?',
      }),
    )
    api.preview.mockResolvedValueOnce(makePreview(completedPlan))

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: {
        value: 'Show non-operational emergency lights inspected in June 2026.',
      },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByText(
        'UniPM currently supports reporting by scheduled PM cycle. Filtering by the date an inspection happened is unavailable.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Use scheduled PM cycles instead' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('Which scheduled PM period should be included?'),
    ).not.toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Use scheduled PM cycles instead' }),
    )
    expect(
      await screen.findByText('Which scheduled PM period should be included?'),
    ).toBeInTheDocument()
    expect(screen.getByText('Question 1 of 1')).toBeInTheDocument()
    expect(api.interpret).toHaveBeenCalledTimes(1)
    expect(api.preview).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: String(currentYear) }))
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(`Period based on scheduled PM cycles: ${currentYear}`),
    ).toBeInTheDocument()
    expect(api.preview).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(api.preview).toHaveBeenCalledWith(
      {
        plan: expect.objectContaining({
          period: expect.objectContaining({ year: currentYear }),
        }),
      },
      expect.anything(),
    )
  })

  it('reinterprets an explicitly accepted scheduled-cycle alternative when no partial plan is available', async () => {
    const originalQuestion =
      'Show non-operational emergency lights inspected in June 2026.'
    api.interpret
      .mockResolvedValueOnce(
        makeInterpretation(makePlan(), {
          status: 'NeedsClarification',
          plan: null,
          clarificationFields: ['Period'],
          code: 'PeriodBasisNeedsClarification',
        }),
      )
      .mockResolvedValueOnce(makeInterpretation())

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: originalQuestion },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(
      await screen.findByText(
        'UniPM currently supports reporting by scheduled PM cycle. Filtering by the date an inspection happened is unavailable.',
      ),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Use scheduled PM cycles instead' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'Review your report' }),
    ).toBeInTheDocument()
    expect(api.interpret).toHaveBeenCalledTimes(2)
    expect(api.interpret.mock.calls[1]?.[0].question).toBe(
      'Show non-operational emergency lights for the scheduled PM cycle in June 2026.',
    )
    expect(api.preview).not.toHaveBeenCalled()
  })

  it('does not show PM-specific measure or period choices for asset inventory', async () => {
    const partialInventoryPlan = makePlan({
      subject: 'assetInventory',
      period: null,
      categories: ['fire-extinguisher'],
      measures: [],
      groupBy: [],
      columns: [],
      presentation: 'Summary',
    })
    api.catalog.mockImplementation(async (params?: { subject?: string }) =>
      params?.subject === 'assetInventory'
        ? makeInventoryCatalog()
        : makeCatalog(),
    )
    api.interpret.mockResolvedValueOnce(
      makeInterpretation(partialInventoryPlan, {
        status: 'NeedsClarification',
        clarificationFields: ['Measures'],
      }),
    )

    render(<PmAnalyticsV3Panel />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'How many fire extinguishers are registered?' },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))

    expect(
      await screen.findByRole('heading', { name: 'Clarify the report scope' }),
    ).toBeInTheDocument()
    await waitFor(() => {
      expect(
        api.catalog.mock.calls.some(
          ([params]) =>
            (params as { subject?: string } | undefined)?.subject ===
            'assetInventory',
        ),
      ).toBe(true)
      expect(
        screen.getByRole('button', { name: 'More report options' }),
      ).toBeEnabled()
    })
    const measureOptionsButton = screen.getByRole('button', {
      name: 'More report options',
    })
    fireEvent.click(measureOptionsButton)
    expect(measureOptionsButton).toHaveAttribute('aria-expanded', 'true')
    expect(await screen.findByText('Registered assets')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Completed late' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Overdue and unfinished' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Completion progress' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText(/Overdue work is unfinished past its due date/),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText('Which scheduled PM period should be included?'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: '2026' }),
    ).not.toBeInTheDocument()
  })

  it('invalidates stale preview when question text is edited, preventing confirmation of old preview', async () => {
    api.execute.mockResolvedValue(makeExecution())
    render(<PmAnalyticsV3Panel />)
    await interpretAndPreview()

    expect(
      screen.getByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Confirm and generate' }),
    ).toBeInTheDocument()

    // From preview, clicking "Edit question" returns to question stage
    fireEvent.click(screen.getByRole('button', { name: 'Edit question' }))
    expect(
      screen.getByRole('textbox', { name: 'Question' }),
    ).toBeInTheDocument()

    // Editing question text immediately clears existing preview
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show progress for fire extinguishers during 2025' },
    })

    expect(
      screen.queryByRole('heading', { name: 'Report preview' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Confirm and generate' }),
    ).not.toBeInTheDocument()
  })

  it('supports back navigation from results to preview, and from plan to question retaining question text', async () => {
    api.execute.mockResolvedValue(makeExecution())
    render(<PmAnalyticsV3Panel />)

    // Stage: question -> plan
    fireEvent.change(screen.getByRole('textbox', { name: 'Question' }), {
      target: { value: 'Show progress for fire extinguishers during 2024' },
    })
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Generate report' }),
      ).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(await screen.findByText('Review your report')).toBeInTheDocument()

    // Back navigation: From plan, "Edit question" returns to question stage with question text retained
    fireEvent.click(screen.getByRole('button', { name: 'Edit question' }))
    expect(screen.getByRole('textbox', { name: 'Question' })).toHaveValue(
      'Show progress for fire extinguishers during 2024',
    )
    expect(screen.queryByText('Review your report')).not.toBeInTheDocument()

    // Move to preview and results
    fireEvent.click(screen.getByRole('button', { name: 'Generate report' }))
    expect(await screen.findByText('Review your report')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Preview report' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm and generate' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'PM execution results' }),
    ).toBeInTheDocument()

    // Back navigation: From results, "Back to preview" returns to preview stage
    fireEvent.click(screen.getByRole('button', { name: 'Back to preview' }))
    expect(
      await screen.findByRole('heading', { name: 'Report preview' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Return to results' }),
    ).toBeInTheDocument()
  })
})
