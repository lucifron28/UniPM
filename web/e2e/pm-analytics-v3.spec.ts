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
}

const catalog = {
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
  await page.route('**/api/v1/analytics/pm/v3/catalog', (route) =>
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
          catalogVersion: 1,
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
        catalogVersion: 1,
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
    .getByRole('textbox', { name: 'Describe the report' })
    .fill('Show progress for fire extinguishers during 2024')
  await page.getByRole('button', { name: 'Fill plan from question' }).click()
  await expect(
    page.getByText('A report plan is ready to review.'),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Preview report' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview' }),
  ).toBeVisible()
  await expect(page.getByText(/Measure definitions/)).toBeVisible()
  await page.getByRole('button', { name: 'Confirm and run report' }).click()
  await expect(page.getByText('100%', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'View contributing records' }).click()
  await expect(
    page.getByText(/records plan for Progress is ready/),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Preview report' }).click()
  await expect.poll(() => previewPlans).toHaveLength(2)
  expect(previewPlans[1].mode).toBe('Records')
  expect(previewPlans[1].matchingFilters).toEqual([
    {
      field: 'condition',
      operator: 'In',
      values: ['Operational', 'NonOperational'],
    },
  ])
  await page.getByRole('button', { name: 'Confirm and run report' }).click()
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
