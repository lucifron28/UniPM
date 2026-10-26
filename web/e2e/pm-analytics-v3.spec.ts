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

test('saved reports reuse a fresh preview and guard stale deletes', async ({ page }) => {
  test.setTimeout(60_000)
  await page.setViewportSize({ width: 390, height: 844 })
  const pageErrors: string[] = []
  page.on('pageerror', (error) => pageErrors.push(error.message))
  const unexpectedAnalytics: string[] = []
  await page.route('**/api/v1/**', (route) => {
    if (route.request().url().includes('/analytics/')) {
      unexpectedAnalytics.push(route.request().url())
    }
    return route.fulfill({ status: 503, json: { title: 'Unmocked API request' } })
  })
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({ json: session }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({ json: session.user }),
  )
  await page.route('**/api/v1/analytics/pm/v3/catalog**', (route) =>
    route.fulfill({ json: catalog }),
  )

  let interpretationCalls = 0
  let executionCalls = 0
  let savedPreviewCalls = 0
  let currentProgress = 50
  let activePlan = structuredClone(plan)
  let revision = 0
  let rejectNextSave = false
  type Saved = {
    id: string
    name: string
    plan: typeof plan
    periodMode: string
    revision: string
    updatedAtUtc: string
  }
  let saved: Saved | null = null
  const deleteRevisions: Array<string | null> = []
  const writes: Array<{
    name: string
    plan: typeof plan
    periodMode: string
    revision?: string
  }> = []

  await page.route('**/api/v1/analytics/pm/v3/interpret', (route) => {
    interpretationCalls += 1
    return route.fulfill({
      json: {
        status: 'Valid',
        plan,
        clarificationFields: [],
        code: null,
        detail: null,
        interpreter: 'DeterministicParser',
      },
    })
  })
  const makeCurrentPreview = () => ({
    ...preview,
    plan: activePlan,
    expiresAtUtc: new Date(Date.now() + 600_000).toISOString(),
    summary: `Reviewed PM report for ${activePlan.period.year}.`,
  })
  await page.route('**/api/v1/analytics/pm/v3/preview', (route) => {
    activePlan = route.request().postDataJSON().plan
    return route.fulfill({ json: makeCurrentPreview() })
  })
  await page.route('**/api/v1/analytics/pm/v3/saved-reports**', (route) => {
    const request = route.request()
    if (request.method() === 'GET') {
      return route.fulfill({ json: saved ? [saved] : [] })
    }
    if (request.url().endsWith('/preview')) {
      savedPreviewCalls += 1
      if (!saved) return route.fulfill({ status: 404 })
      activePlan = structuredClone(saved.plan)
      if (saved.periodMode === 'ThisYear') activePlan.period.year = 2026
      return route.fulfill({ json: makeCurrentPreview() })
    }
    if (request.method() === 'DELETE') {
      const deletionRevision = new URL(request.url()).searchParams.get('revision')
      deleteRevisions.push(deletionRevision)
      if (!saved || deletionRevision !== saved.revision) {
        return route.fulfill({
          status: 409,
          json: {
            title:
              'This saved report changed. Reload My reports before editing it again.',
          },
        })
      }
      saved = null
      return route.fulfill({ status: 204 })
    }
    const input = request.postDataJSON() as (typeof writes)[number]
    if (rejectNextSave) {
      rejectNextSave = false
      return route.fulfill({
        status: 400,
        json: {
          title: 'Validation failed',
          errors: { name: ['Choose another report name.'] },
        },
      })
    }
    writes.push(input)
    revision += 1
    saved = {
      id: '99999999-9999-4999-8999-999999999999',
      name: input.name,
      plan: input.plan,
      periodMode: input.periodMode,
      revision: `00000000-0000-4000-8000-${String(revision).padStart(12, '0')}`,
      updatedAtUtc: new Date().toISOString(),
    }
    return route.fulfill({
      status: request.method() === 'POST' ? 201 : 200,
      json: saved,
    })
  })
  await page.route('**/api/v1/analytics/pm/v3/execute', (route) => {
    executionCalls += 1
    expect(route.request().postDataJSON().previewId).toBe(preview.previewId)
    return route.fulfill({
      json: {
        schemaVersion: 3,
        catalogVersion: 2,
        asOfUtc: new Date().toISOString(),
        plan: activePlan,
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
            values: {},
            measures: {
              progressPercent: {
                value: currentProgress,
                numerator: currentProgress === 50 ? 1 : 2,
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
        scopeNotes: ['Controlled browser fixture.'],
      },
    })
  })

  await page.goto('/app/analytics')
  await page
    .getByRole('textbox', { name: 'Question' })
    .fill('Show progress for fire extinguishers during 2024')
  await page.getByRole('button', { name: 'Generate report', exact: true }).click()
  await page.getByRole('button', { name: 'Preview report', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  expect(executionCalls).toBe(0)
  await page.getByLabel('Report name', { exact: true }).fill('Routine progress')
  await page.getByLabel('Dates on future runs').selectOption('ThisYear')
  rejectNextSave = true
  await page.getByRole('button', { name: 'Save as new report' }).click()
  await expect(page.getByRole('alert')).toContainText('Choose another report name.')
  await page.getByRole('button', { name: 'Save as new report' }).click()
  await expect(
    page.getByRole('status').filter({ hasText: 'Report settings saved.' }),
  ).toBeVisible()
  expect(writes[0]).toMatchObject({
    name: 'Routine progress',
    plan,
    periodMode: 'ThisYear',
  })

  await page.reload()
  await page.getByText('My reports (1)', { exact: true }).click()
  await page.getByRole('button', { name: 'Open preview', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  await expect(
    page.getByText('Reviewed PM report for 2026.', { exact: true }),
  ).toBeVisible()
  expect(interpretationCalls).toBe(1)
  expect(executionCalls).toBe(0)
  await page.getByRole('button', { name: 'Confirm and generate', exact: true }).click()
  await expect(page.getByText('50%', { exact: true })).toBeVisible()

  currentProgress = 100
  await page.getByRole('button', { name: 'Open preview', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  expect(executionCalls).toBe(1)
  await page.getByRole('button', { name: 'Confirm and generate', exact: true }).click()
  await expect(page.getByText('100%', { exact: true })).toBeVisible()
  expect(executionCalls).toBe(2)
  expect(savedPreviewCalls).toBe(2)
  expect(interpretationCalls).toBe(1)

  await page.getByRole('button', { name: 'Edit settings', exact: true }).click()
  await page.getByLabel('Year', { exact: true }).fill('2025')
  await page.getByRole('button', { name: 'Preview report', exact: true }).click()
  await expect(
    page.getByText('Reviewed PM report for 2025.', { exact: true }),
  ).toBeVisible()
  await page.getByLabel('Dates on future runs').selectOption('Fixed')
  await page
    .getByRole('button', { name: 'Replace settings for Routine progress' })
    .click()
  await expect.poll(() => writes.length).toBe(2)
  expect(writes[1]).toMatchObject({
    periodMode: 'Fixed',
    plan: { period: { year: 2025 } },
    revision: '00000000-0000-4000-8000-000000000001',
  })
  await page.getByRole('button', { name: 'Rename', exact: true }).click()
  await page
    .getByRole('textbox', { name: 'New name for Routine progress' })
    .fill('Annual progress')
  await page.getByRole('button', { name: 'Save name', exact: true }).click()
  await expect(page.getByText('Annual progress', { exact: true })).toBeVisible()
  expect(writes[2]).toMatchObject({ name: 'Annual progress', periodMode: 'Fixed' })

  const staleRevision = saved?.revision
  saved = {
    ...saved!,
    name: 'Updated outside this tab',
    revision: '00000000-0000-4000-8000-000000000004',
  }
  await page.getByRole('button', { name: 'Delete', exact: true }).click()
  await page.getByRole('button', { name: 'Confirm delete', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('changed')
  expect(saved).not.toBeNull()
  expect(deleteRevisions).toEqual([staleRevision])

  await page.getByRole('button', { name: 'Reload my reports' }).click()
  await expect(
    page.getByText('Updated outside this tab', { exact: true }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Confirm delete', exact: true }).click()
  await expect(
    page.getByText('No saved reports yet. Preview a report to save its settings.'),
  ).toBeVisible()
  expect(saved).toBeNull()
  expect(deleteRevisions).toEqual([
    staleRevision,
    '00000000-0000-4000-8000-000000000004',
  ])
  expect(interpretationCalls).toBe(1)
  expect(executionCalls).toBe(2)
  expect(unexpectedAnalytics).toEqual([])
  expect(pageErrors).toEqual([])
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
})

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
  await expect(
    page.getByRole('heading', { name: 'Review your report' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Preview report' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview' }),
  ).toBeVisible()
  await page.getByText('Report details', { exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Counting definitions and population' }),
  ).toBeVisible()
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
  const reviewInspectionLink = page.getByRole('link', {
    name: 'Review inspection',
  })
  const inspectionUrl = new URL(
    (await reviewInspectionLink.getAttribute('href'))!,
    'http://localhost',
  )
  expect(inspectionUrl.pathname).toBe(
    '/app/inspections/33333333-3333-4333-8333-333333333333',
  )
  const inspectionReturnContext = JSON.parse(
    inspectionUrl.searchParams.get('returnContext') ?? 'null',
  )
  expect(inspectionReturnContext.kind).toBe('analytics')
  expect(inspectionReturnContext.sessionId).toBeTruthy()

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
  await page.getByText('Report details', { exact: true }).click()
  await expect(page.getByText('Registered asset inventory')).toBeVisible()
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
