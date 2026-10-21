import { expect, test } from '@playwright/test'

const session = {
  accessToken: 'fictional-nla-v3-browser-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '88888888-8888-4888-8888-888888888888',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
  },
}

const plan = {
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
}

const catalog = {
  schemaVersion: 3,
  catalogVersion: 2,
  subject: 'pmExecution',
  subjects: [
    {
      id: 'pmExecution',
      label: 'PM execution',
      description:
        'Eligible persisted asset-cycle schedules and their inspection outcomes.',
      requiresPeriod: true,
    },
    {
      id: 'assetInventory',
      label: 'Asset inventory',
      description:
        'One current registered asset per row, independent of PM schedules and inspections.',
      requiresPeriod: false,
    },
  ],
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
  ],
  dimensions: [],
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

const preview = {
  previewId: 'signed-browser-preview-token',
  expiresAtUtc: '2026-10-20T08:00:00Z',
  plan,
  cyclesByCategory: { 'fire-extinguisher': ['2024-02', '2024-05'] },
  summary:
    'Calculate progress on scheduled PM cycles for Fire Extinguisher: 2024.',
  scopeNotes: ['Cancelled schedules are excluded.'],
}

const population = {
  eligibleAssetCycleCount: 2,
  eligibleDistinctAssetCount: 1,
  matchingAssetCycleCount: 2,
  matchingDistinctAssetCount: 1,
  sourceTotalCount: 2,
  groupsBeforeAggregateFilters: 1,
  groupsAfterAggregateFilters: 1,
  groupsReturnedAfterTop: 1,
}

test('runs a V3 report through source-record review at a narrow viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({ json: session }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({ json: session.user }),
  )
  await page.route('**/api/v1/analytics/pm/v3/catalog**', (route) =>
    route.fulfill({ json: catalog }),
  )
  await page.route('**/api/v1/analytics/pm/v3/interpret', (route) =>
    route.fulfill({
      json: {
        status: 'Valid',
        plan,
        clarificationFields: [],
        code: null,
        detail: null,
      },
    }),
  )
  const previewPlans: (typeof plan)[] = []
  await page.route('**/api/v1/analytics/pm/v3/preview', async (route) => {
    const request = route.request().postDataJSON() as { plan: typeof plan }
    previewPlans.push(request.plan)
    await route.fulfill({ json: { ...preview, plan: request.plan } })
  })
  let executions = 0
  await page.route('**/api/v1/analytics/pm/v3/execute', async (route) => {
    executions += 1
    if (executions === 1) {
      await route.fulfill({
        json: {
          schemaVersion: 3,
          catalogVersion: 2,
          asOfUtc: '2026-10-19T08:00:00Z',
          plan,
          cyclesByCategory: preview.cyclesByCategory,
          mode: 'Aggregate',
          columns: [
            {
              id: 'progressPercent',
              label: 'Progress',
              valueType: 'decimal',
              unit: 'percent',
              nullSemantics: null,
            },
          ],
          population,
          page: 1,
          pageSize: 25,
          totalPages: 1,
          aggregateRows: [
            {
              values: { progressPercent: 100 },
              measures: {
                progressPercent: {
                  value: 100,
                  numerator: 2,
                  denominator: 2,
                  unit: 'percent',
                  isMeasurable: true,
                  unmeasurableReason: null,
                  eligibleRowCount: 2,
                  matchingRowCount: 2,
                },
              },
            },
          ],
          records: [],
          scopeNotes: ['Cancelled schedules are excluded.'],
        },
      })
      return
    }

    const recordPlan = previewPlans.at(-1)!
    await route.fulfill({
      json: {
        schemaVersion: 3,
        catalogVersion: 2,
        asOfUtc: '2026-10-19T08:00:00Z',
        plan: recordPlan,
        cyclesByCategory: preview.cyclesByCategory,
        mode: 'Records',
        columns: catalog.recordColumns.map((column) => ({
          id: column.id,
          label: column.label,
          valueType: column.valueType,
          unit: column.unit,
          nullSemantics: column.nullSemantics,
        })),
        population,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        aggregateRows: [],
        records: [
          {
            scheduleId: '11111111-1111-4111-8111-111111111111',
            assetId: '22222222-2222-4222-8222-222222222222',
            inspectionId: '33333333-3333-4333-8333-333333333333',
            values: {
              scheduleId: '11111111-1111-4111-8111-111111111111',
              assetId: '22222222-2222-4222-8222-222222222222',
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
      },
    })
  })

  await page.goto('/app/analytics')
  await page
    .getByRole('textbox', { name: 'Question' })
    .fill('Show progress for fire extinguishers during 2024')
  await page.getByRole('button', { name: 'Generate report' }).click()
  await expect(page.getByText('Report ready for review')).toBeVisible()
  await page.getByRole('button', { name: 'Preview report' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview' }),
  ).toBeVisible()
  await expect(page.getByText(/Measure definitions/)).toBeVisible()
  await page.getByRole('button', { name: 'Confirm and generate' }).click()
  await expect(page.getByText('100%', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'View contributing records' }).click()
  await expect(
    page.getByText(/records plan for Progress is ready/),
  ).toBeVisible()
  await expect(
    page.getByRole('heading', { name: 'Report preview' }),
  ).toBeVisible()
  await expect.poll(() => previewPlans).toHaveLength(2)
  expect(previewPlans[1].mode).toBe('Records')
  expect(previewPlans[1].matchingFilters).toEqual([
    {
      field: 'condition',
      operator: 'In',
      values: ['Operational', 'NonOperational'],
    },
  ])
  await page.getByRole('button', { name: 'Confirm and generate' }).click()
  await expect(
    page.getByRole('link', { name: 'Review inspection' }),
  ).toHaveAttribute(
    'href',
    '/app/inspections/33333333-3333-4333-8333-333333333333',
  )

  const pageWidth = await page.evaluate(
    () => document.documentElement.scrollWidth,
  )
  expect(pageWidth).toBeLessThanOrEqual(390)
})

test('runs a registered asset inventory report without a PM period', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({ json: session }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({ json: session.user }),
  )

  const inventoryCatalog = {
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
      {
        id: 'registrationStatus',
        label: 'Registration status',
        isBaseFilter: true,
        valueType: 'registrationStatus',
        maximumLength: 16,
        allowedValues: ['Active', 'Inactive', 'Retired'],
        operators: ['Equal', 'In'],
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
        id: 'registrationStatus',
        label: 'Registration status',
        valueType: 'string',
        unit: '',
        nullSemantics: 'Stored status.',
      },
    ],
  }

  await page.route('**/api/v1/analytics/pm/v3/catalog**', (route) => {
    const subject = new URL(route.request().url()).searchParams.get('subject')
    return route.fulfill({
      json: subject === 'assetInventory' ? inventoryCatalog : catalog,
    })
  })

  const inventoryPlan = {
    schemaVersion: 3,
    catalogVersion: 2,
    subject: 'assetInventory',
    mode: 'Aggregate',
    period: null,
    categories: ['fire-extinguisher'],
    measures: ['registeredAssetCount'],
    groupBy: ['registrationStatus'],
    baseFilters: [],
    matchingFilters: [],
    aggregateFilters: [],
    orderBy: [],
    top: null,
    presentation: 'Table',
    columns: ['registrationStatus', 'registeredAssetCount'],
  }
  await page.route('**/api/v1/analytics/pm/v3/interpret', (route) =>
    route.fulfill({
      json: {
        status: 'Valid',
        plan: inventoryPlan,
        clarificationFields: [],
        code: null,
        detail: null,
        interpreter: 'DeterministicParser',
      },
    }),
  )

  let previewPlan: Record<string, unknown> | null = null
  await page.route('**/api/v1/analytics/pm/v3/preview', async (route) => {
    const request = route.request().postDataJSON() as {
      plan: Record<string, unknown>
    }
    previewPlan = request.plan
    await route.fulfill({
      json: {
        previewId: 'signed-inventory-preview-token',
        expiresAtUtc: '2026-10-20T08:00:00Z',
        plan: request.plan,
        cyclesByCategory: {},
        summary: 'Count registered assets grouped by registration status.',
        scopeNotes: [
          'Each result represents one registered Asset row, identified by Asset ID.',
          'Inventory reads Assets directly and does not join PM schedules or inspections.',
          'Registration status is separate from inspection condition.',
        ],
      },
    })
  })
  await page.route('**/api/v1/analytics/pm/v3/execute', (route) =>
    route.fulfill({
      json: {
        schemaVersion: 3,
        catalogVersion: 2,
        asOfUtc: '2026-10-19T08:00:00Z',
        plan: previewPlan,
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
        scopeNotes: ['One registered Asset row per ID.'],
      },
    }),
  )

  await page.goto('/app/analytics')
  await expect(page.getByRole('heading', { name: 'Ask UniPM' })).toBeVisible()
  await page
    .getByRole('textbox', { name: 'Question' })
    .fill('How many fire extinguishers are registered by status?')
  await page.getByRole('button', { name: 'Generate report' }).click()
  await expect(page.getByText('Asset inventory')).toBeVisible()
  await expect(page.getByLabel('Scheduled PM cycle period')).toHaveCount(0)
  await page.getByRole('button', { name: 'Preview report' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview' }),
  ).toBeVisible()
  expect(previewPlan).toMatchObject({
    subject: 'assetInventory',
    period: null,
    groupBy: ['registrationStatus'],
  })
  expect(previewPlan).toEqual(inventoryPlan)
  await expect(page.getByText('Included PM cycles by category')).toHaveCount(0)
  await page.getByRole('button', { name: 'Confirm and generate' }).click()
  await expect(
    page.getByRole('heading', { name: 'Registered asset inventory results' }),
  ).toBeVisible()
  await page.getByText('Calculation definitions and provenance').click()
  await expect(page.getByText(/Source: Assets table/)).toBeVisible()

  const pageWidth = await page.evaluate(
    () => document.documentElement.scrollWidth,
  )
  expect(pageWidth).toBeLessThanOrEqual(390)
})
