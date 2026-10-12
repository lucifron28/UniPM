import { expect, test, type Locator, type Page } from '@playwright/test'

const session = {
  accessToken: 'fictional-pm-dashboard-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '22222222-2222-4222-8222-222222222222',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
  },
}

const assetIds = {
  onTime: '11111111-1111-4111-8111-111111111111',
  late: '22222222-2222-4222-8222-222222222222',
  pending: '33333333-3333-4333-8333-333333333333',
  notCompleted: '44444444-4444-4444-8444-444444444444',
  future: '55555555-5555-4555-8555-555555555555',
}

const cycles = [
  {
    assetCategory: 'fire-extinguisher',
    year: 2026,
    cycles: [
      { pmCycle: '2026-05', scheduled: 3 },
      { pmCycle: '2026-08', scheduled: 4 },
      { pmCycle: '2026-11', scheduled: 1 },
    ],
  },
  {
    assetCategory: 'fire-extinguisher',
    year: 2025,
    cycles: [{ pmCycle: '2025-11', scheduled: 2 }],
  },
  {
    assetCategory: 'fire-alarm',
    year: 2026,
    cycles: [{ pmCycle: '2026-06', scheduled: 2 }],
  },
]

function jsonResponse(body: unknown) {
  return {
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  }
}

function deadlineFor(pmCycle: string) {
  const [year, month] = pmCycle.split('-').map(Number)
  return new Date(Date.UTC(year, month, 0, 15, 59, 59, 999)).toISOString()
}

function dateInCycle(pmCycle: string, day: number) {
  const [year, month] = pmCycle.split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day, 8)).toISOString()
}

function dateAfterCycle(pmCycle: string, day: number) {
  const [year, month] = pmCycle.split('-').map(Number)
  return new Date(Date.UTC(year, month, day, 8)).toISOString()
}

function makeAssetRow({
  id,
  assetCode,
  department,
  pmCycle,
  timeliness,
  condition,
  executionStatus,
  isInspected,
  inspectionCompletedAt = null,
  formStatus = null,
  isAcknowledged = false,
}: {
  id: string
  assetCode: string
  department: string
  pmCycle: string
  timeliness: string
  condition: string
  executionStatus: string
  isInspected: boolean
  inspectionCompletedAt?: string | null
  formStatus?: string | null
  isAcknowledged?: boolean
}) {
  return {
    scheduleId: id,
    assetId: id,
    inspectionId: isInspected ? id : null,
    assetCode,
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    location: assetCode === 'FE-003' ? 'Second floor' : 'Lobby',
    department,
    pmCycle,
    scheduleDate: deadlineFor(pmCycle),
    deadline: deadlineFor(pmCycle),
    scheduleStatus: 'Scheduled',
    executionStatus,
    isInspected,
    inspectionCompletedAt,
    timeliness,
    condition,
    formId: isInspected ? id : null,
    formStatus,
    isAcknowledged,
    acknowledgedAt: isAcknowledged ? dateAfterCycle(pmCycle, 30) : null,
  }
}

function fixtureAssets(pmCycle: string) {
  if (pmCycle === '2026-11') {
    return [
      makeAssetRow({
        id: assetIds.future,
        assetCode: 'FE-005',
        department: 'GSD',
        pmCycle,
        timeliness: 'Scheduled',
        condition: 'NotInspected',
        executionStatus: 'Scheduled',
        isInspected: false,
      }),
    ]
  }

  if (pmCycle === '2026-05') {
    return [
      makeAssetRow({
        id: assetIds.onTime,
        assetCode: 'FE-001',
        department: 'GSD',
        pmCycle,
        timeliness: 'OnTime',
        condition: 'Operational',
        executionStatus: 'Completed',
        isInspected: true,
        inspectionCompletedAt: dateInCycle(pmCycle, 20),
        formStatus: 'Acknowledged',
        isAcknowledged: true,
      }),
      makeAssetRow({
        id: assetIds.late,
        assetCode: 'FE-002',
        department: 'OPS',
        pmCycle,
        timeliness: 'Late',
        condition: 'NonOperational',
        executionStatus: 'Completed',
        isInspected: true,
        inspectionCompletedAt: dateAfterCycle(pmCycle, 2),
        formStatus: 'Submitted',
      }),
      makeAssetRow({
        id: assetIds.pending,
        assetCode: 'FE-003',
        department: 'OPS',
        pmCycle,
        timeliness: 'Pending',
        condition: 'Operational',
        executionStatus: 'Scheduled',
        isInspected: false,
      }),
    ]
  }

  return [
    makeAssetRow({
      id: assetIds.onTime,
      assetCode: 'FE-001',
      department: 'GSD',
      pmCycle,
      timeliness: 'OnTime',
      condition: 'Operational',
      executionStatus: 'Completed',
      isInspected: true,
      inspectionCompletedAt: dateInCycle(pmCycle, 20),
      formStatus: 'Acknowledged',
      isAcknowledged: true,
    }),
    makeAssetRow({
      id: assetIds.late,
      assetCode: 'FE-002',
      department: 'OPS',
      pmCycle,
      timeliness: 'Late',
      condition: 'NonOperational',
      executionStatus: 'Completed',
      isInspected: true,
      inspectionCompletedAt: dateAfterCycle(pmCycle, 2),
      formStatus: 'Submitted',
    }),
    makeAssetRow({
      id: assetIds.notCompleted,
      assetCode: 'FE-004',
      department: 'GSD',
      pmCycle,
      timeliness: 'NotCompleted',
      condition: 'NotInspected',
      executionStatus: 'NotCompleted',
      isInspected: false,
    }),
  ]
}

function dashboardFixture(url: URL) {
  const pmCycle = url.searchParams.get('pmCycle') ?? '2026-08'
  const assetCategory =
    url.searchParams.get('assetCategory') ?? 'fire-extinguisher'
  const department = url.searchParams.get('department')
  const condition = url.searchParams.get('condition')
  const timeliness = url.searchParams.get('timeliness')
  const search = url.searchParams.get('search')?.toLowerCase()
  const periodState =
    pmCycle === '2026-11'
      ? 'Future'
      : pmCycle === '2026-05'
        ? 'Active'
        : 'Closed'

  const allAssets = fixtureAssets(pmCycle)
  const assets = allAssets.filter((asset) => {
    if (department && asset.department !== department) return false
    if (condition && asset.condition !== condition) return false
    if (timeliness && asset.timeliness !== timeliness) return false
    if (
      search &&
      ![asset.assetCode, asset.building, asset.location, asset.department]
        .filter(Boolean)
        .some((value) => value.toLowerCase().includes(search))
    ) {
      return false
    }
    return true
  })

  const baseMetrics =
    periodState === 'Future'
      ? {
          scheduled: 1,
          inspected: 0,
          completedOnTime: 0,
          completedLate: 0,
          notCompleted: 0,
          remaining: 1,
          operational: 0,
          nonOperational: 0,
          inspectionResultsAvailable: false,
          complianceMeasurable: false,
          onTimeCompliancePercent: null,
          progressPercent: 0,
        }
      : periodState === 'Active'
        ? {
            scheduled: department ? 2 : 3,
            inspected: department ? 1 : 2,
            completedOnTime: department ? 0 : 1,
            completedLate: department ? 1 : 1,
            notCompleted: 0,
            remaining: 1,
            operational: department ? 1 : 1,
            nonOperational: department ? 1 : 1,
            inspectionResultsAvailable: true,
            complianceMeasurable: false,
            onTimeCompliancePercent: null,
            progressPercent: department ? 50 : 67,
          }
        : {
            scheduled: department ? 2 : 4,
            inspected: department ? 1 : 2,
            completedOnTime: department ? 0 : 1,
            completedLate: department ? 1 : 1,
            notCompleted: department ? 0 : 1,
            remaining: department ? 1 : 1,
            operational: department ? 0 : 1,
            nonOperational: department ? 1 : 1,
            inspectionResultsAvailable: true,
            complianceMeasurable: true,
            onTimeCompliancePercent: department ? 0 : 50,
            progressPercent: department ? 50 : 50,
          }

  return {
    pmCycle,
    assetCategory,
    department,
    deadline: deadlineFor(pmCycle),
    periodState,
    ...baseMetrics,
    batches: [
      {
        department: department ?? 'GSD',
        assetCategory,
        pmCycle,
        scheduled: baseMetrics.scheduled,
        inspected: baseMetrics.inspected,
        completedOnTime: baseMetrics.completedOnTime,
        completedLate: baseMetrics.completedLate,
        notCompleted: baseMetrics.notCompleted,
        remaining: baseMetrics.remaining,
        formId: null,
        formStatus: 'Submitted',
        fileNumber: 'PM-2026-001',
        submittedAt: dateAfterCycle(pmCycle, 30),
        isAcknowledged: false,
        acknowledgedAt: null,
      },
    ],
    assets,
  }
}

async function mockDashboardApi(page: Page) {
  const requests: URL[] = []
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill(jsonResponse(session)),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill(jsonResponse(session.user)),
  )
  await page.route('**/api/v1/pm-period-dashboard**', async (route) => {
    const url = new URL(route.request().url())
    if (!url.pathname.endsWith('/pm-period-dashboard')) {
      await route.continue()
      return
    }
    requests.push(url)
    await route.fulfill(jsonResponse(dashboardFixture(url)))
  })
  await page.route('**/api/v1/pm-period-dashboard/cycles**', (route) =>
    route.fulfill(jsonResponse(cycles)),
  )
  await page.route('**/api/v1/schedules/enrollment-deferrals**', (route) =>
    route.fulfill(
      jsonResponse({
        page: 1,
        pageSize: 10,
        total: 0,
        pendingCount: 0,
        reviewedCount: 0,
        items: [],
      }),
    ),
  )
  await page.route('**/api/v1/reference-data/asset-categories', (route) =>
    route.fulfill(
      jsonResponse([
        { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
        { code: 'fire-alarm', displayName: 'Fire alarm systems' },
      ]),
    ),
  )
  await page.route('**/api/v1/assets/**', (route) => {
    const url = new URL(route.request().url())
    if (url.pathname.endsWith('/verification-location')) {
      return route.fulfill(
        jsonResponse({
          verificationLatitude: null,
          verificationLongitude: null,
          verificationRadiusMeters: null,
        }),
      )
    }
    if (url.pathname.endsWith(`/${assetIds.onTime}`)) {
      return route.fulfill(
        jsonResponse({
          id: assetIds.onTime,
          assetCode: 'FE-001',
          assetCategory: 'fire-extinguisher',
          building: 'Main Building',
          department: 'GSD',
          location: 'Lobby',
          qrCodeValue: 'UNIPM-FE-001',
          status: 'Active',
          createdAt: '2026-07-01T00:00:00Z',
          updatedAt: '2026-07-30T00:00:00Z',
          hasVerificationLocation: false,
        }),
      )
    }
    return route.continue()
  })
  await page.route('**/api/v1/inspections/history/**', (route) =>
    route.fulfill(jsonResponse([])),
  )

  return requests
}

function metricCard(page: Page, label: string) {
  return page
    .locator('p')
    .filter({ hasText: new RegExp(`^${label}$`) })
    .first()
    .locator('..')
}

function assetTable(page: Page) {
  return page.getByRole('table', {
    name: 'Scheduled PM assets and inspection status',
  })
}

const assetColumnIndexes = {
  scheduledMonth: 3,
  inspection: 4,
  timeliness: 5,
  condition: 6,
  form: 7,
} as const

const officialMetricLabels = [
  'Scheduled',
  'Inspected',
  'Completed on time',
  'Completed late',
  'Remaining',
  'Operational',
  'Non-operational',
  'Progress',
  'Compliance rate',
] as const

function assetRow(page: Page, assetCode: string) {
  const table = assetTable(page)
  return table
    .getByRole('link', { name: assetCode, exact: true })
    .locator('xpath=ancestor::tr')
}

function assetCell(row: Locator, column: keyof typeof assetColumnIndexes) {
  return row.getByRole('cell').nth(assetColumnIndexes[column])
}

function batchTable(page: Page) {
  return page.getByRole('table', {
    name: 'Department batch acknowledgement overview',
  })
}

async function expectRequest(
  requests: URL[],
  expected: Record<string, string>,
) {
  await expect
    .poll(() =>
      requests.some((url) =>
        Object.entries(expected).every(
          ([key, value]) => url.searchParams.get(key) === value,
        ),
      ),
    )
    .toBe(true)
}

async function readOfficialMetrics(page: Page) {
  return Promise.all(
    officialMetricLabels.map((label) =>
      metricCard(page, label).locator('p').nth(1).innerText(),
    ),
  )
}

async function selectDashboardPeriod(page: Page, month: string) {
  await page.getByRole('button', { name: /fire extinguishers?/i }).click()
  await page.getByRole('button', { name: /^2026\b/ }).click()
  await page
    .getByRole('button', { name: new RegExp(`^${month}\\b`, 'i') })
    .click()
}

async function generateDashboard(page: Page) {
  await page.getByRole('button', { name: 'Generate dashboard' }).click()
}

test.describe('PM period dashboard', () => {
  test('filters and exports the generated May report', async ({ page }) => {
    const requests = await mockDashboardApi(page)
    const mayScope = {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-05',
    }
    await page.addInitScript(() => {
      window.print = () => {
        document.documentElement.dataset.printInvoked = 'true'
      }
    })
    await page.goto('/app/dashboard')
    await expect(page.locator('.pm-dashboard-report')).toHaveCount(0)
    await expect(
      page.getByRole('button', { name: 'Generate dashboard' }),
    ).toBeDisabled()
    expect(requests).toHaveLength(0)
    await selectDashboardPeriod(page, 'May')
    expect(requests).toHaveLength(0)
    await generateDashboard(page)
    await expectRequest(requests, mayScope)

    const report = page.locator('.pm-dashboard-report')
    const assets = assetTable(page).getByRole('link')
    await expect(report).toBeVisible()
    await expect(assets).toHaveText(['FE-001', 'FE-002', 'FE-003'])
    await expect(
      assetTable(page).getByRole('columnheader', {
        name: 'Scheduled month / due date',
        exact: true,
      }),
    ).toBeVisible()
    const onTimeRow = assetRow(page, 'FE-001')
    await expect(assetCell(onTimeRow, 'scheduledMonth')).toContainText(
      'May 2026',
    )
    await expect(assetCell(onTimeRow, 'scheduledMonth')).toContainText(
      'Due date: May 31, 2026',
    )
    await expect(assetCell(onTimeRow, 'inspection')).toContainText(
      'Actual inspection date: May 20, 2026',
    )
    const metrics = await readOfficialMetrics(page)
    expect(metrics).toEqual([
      '3',
      '2',
      '1',
      '1',
      '1',
      '1',
      '1',
      '67%',
      'Not measurable yet',
    ])

    await page.getByLabel('Condition').selectOption('NonOperational')
    await expectRequest(requests, {
      ...mayScope,
      condition: 'NonOperational',
    })
    await expect(page).toHaveURL(/condition=NonOperational/)
    await expect(assets).toHaveText(['FE-002'])
    await expect(assetCell(assetRow(page, 'FE-002'), 'condition')).toHaveText(
      'Non-operational',
    )
    await expect.poll(() => readOfficialMetrics(page)).toEqual(metrics)

    await page.getByLabel('Condition').selectOption('')
    await expect
      .poll(() => new URL(page.url()).searchParams.has('condition'))
      .toBe(false)
    await page.getByLabel('Timeliness / status').selectOption('Late')
    await expectRequest(requests, {
      ...mayScope,
      timeliness: 'Late',
    })
    await expect(page).toHaveURL(/timeliness=Late/)
    await expect(assets).toHaveText(['FE-002'])
    await expect(assetCell(assetRow(page, 'FE-002'), 'timeliness')).toHaveText(
      'Completed late',
    )
    await expect.poll(() => readOfficialMetrics(page)).toEqual(metrics)

    await page.getByLabel('Timeliness / status').selectOption('')
    await expect
      .poll(() => new URL(page.url()).searchParams.has('timeliness'))
      .toBe(false)
    await page.getByLabel('Search assets').fill('FE-001')
    await page.getByRole('button', { name: 'Apply search' }).click()
    await expectRequest(requests, {
      ...mayScope,
      search: 'FE-001',
    })
    await expect(page).toHaveURL(/search=FE-001/)
    await expect(assets).toHaveText(['FE-001'])
    await expect.poll(() => readOfficialMetrics(page)).toEqual(metrics)

    await page.getByLabel('Condition').selectOption('Operational')
    await expect
      .poll(() => new URL(page.url()).searchParams.get('condition'))
      .toBe('Operational')
    await page.getByLabel('Timeliness / status').selectOption('OnTime')
    await expectRequest(requests, {
      ...mayScope,
      condition: 'Operational',
      timeliness: 'OnTime',
      search: 'FE-001',
    })
    const activeParams = new URL(page.url()).searchParams
    expect(
      ['condition', 'timeliness', 'search'].map((key) => activeParams.get(key)),
    ).toEqual(['Operational', 'OnTime', 'FE-001'])
    await expect.poll(() => readOfficialMetrics(page)).toEqual(metrics)

    await page.getByRole('button', { name: 'Export dashboard' }).click()
    await expect(page.locator('html')).toHaveAttribute(
      'data-print-invoked',
      'true',
    )
    await page.emulateMedia({ media: 'print' })
    const filterSummary = report
      .getByRole('heading', { name: 'Asset-list filters' })
      .locator('..')
    await expect(filterSummary).toBeVisible()
    await expect(filterSummary.locator('dd')).toHaveText([
      'Operational',
      'Completed on time',
      'FE-001',
    ])
    await expect(filterSummary).toContainText('narrow asset rows only')
    await expect(report).toContainText('UniPM')
    await expect(report).toContainText('May 2026')
    await expect(report).toContainText('All departments')
    await expect(page.locator('form:visible')).toHaveCount(0)
    await expect(
      page.getByRole('button', {
        name: 'Export dashboard',
        includeHidden: true,
      }),
    ).toBeHidden()

    const batches = batchTable(page)
    await expect(
      batches.getByRole('columnheader', { name: 'Department' }),
    ).toBeVisible()
    await expect(
      batches.getByRole('columnheader', { name: 'Scheduled' }),
    ).toBeVisible()
    await expect(batches.locator('thead th').last()).toBeHidden()
    await expect(
      batches.locator('tbody tr').first().locator('td').last(),
    ).toBeHidden()

    await page.emulateMedia({ media: 'screen' })
    await page.getByRole('button', { name: 'Clear filters' }).click()
    const clearKeys = ['department', 'condition', 'timeliness', 'search']
    await expect
      .poll(() =>
        clearKeys.map((key) => new URL(page.url()).searchParams.has(key)),
      )
      .toEqual([false, false, false, false])
    await expect(assets).toHaveText(['FE-001', 'FE-002', 'FE-003'])
    await expect.poll(() => readOfficialMetrics(page)).toEqual(metrics)

    await page.getByLabel('Department').selectOption('OPS')
    await expectRequest(requests, {
      ...mayScope,
      department: 'OPS',
    })
    await expect(metricCard(page, 'Scheduled')).toContainText('2')
    await expect(metricCard(page, 'Inspected')).toContainText('1')
  })

  test('loads a valid scope URL directly and keeps asset detail links navigable', async ({
    page,
  }) => {
    const requests = await mockDashboardApi(page)
    await page.goto(
      '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08',
    )
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-08',
    })
    await expect(page.locator('.pm-dashboard-report')).toBeVisible()
    await expect(metricCard(page, 'Scheduled')).toContainText('4')

    const assetLink = page.getByRole('link', { name: 'FE-001' }).first()
    const returnContext = {
      kind: 'dashboard',
      search: {
        assetCategory: 'fire-extinguisher',
        year: 2026,
        pmCycle: '2026-08',
      },
    }
    await expect(assetLink).toHaveAttribute(
      'href',
      `/app/assets/${assetIds.onTime}?returnContext=${encodeURIComponent(JSON.stringify(returnContext))}`,
    )
    await assetLink.click()
    await expect(page).toHaveURL(
      new RegExp(`/app/assets/${assetIds.onTime}\\?`),
    )
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()
    await page.getByRole('link', { name: 'Back to PM dashboard' }).click()
    await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/pmCycle=2026-08/)
  })

  test('presents Future, Active, and Closed states and contains tables on mobile', async ({
    page,
  }) => {
    const requests = await mockDashboardApi(page)
    await page.goto(
      '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-11',
    )
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-11',
    })
    await expect(page.locator('.pm-dashboard-report')).toBeVisible()
    await expect(
      page.getByText('Future', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      page.getByText('Scheduled', { exact: true }).last(),
    ).toBeVisible()
    await expect(
      page.getByText('Not measurable yet', { exact: true }),
    ).toBeVisible()
    await expect(
      page.getByText('No completed inspection results yet', { exact: true }),
    ).toBeVisible()
    await expect(metricCard(page, 'Remaining')).toContainText('1')
    await expect(metricCard(page, 'Not completed')).toHaveCount(0)
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Remaining' }),
    ).toBeVisible()
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Not completed' }),
    ).toHaveCount(0)
    await expect(
      page.locator('p').filter({ hasText: /^Operational$/ }),
    ).toHaveCount(0)

    const futureRequestCount = requests.length
    await page.getByRole('button', { name: 'Change selection' }).click()
    await selectDashboardPeriod(page, 'May')
    expect(requests).toHaveLength(futureRequestCount)
    await generateDashboard(page)
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-05',
    })
    await expect(
      page.getByText('Active', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      assetTable(page).getByText('Pending', { exact: true }),
    ).toBeVisible()
    await expect(
      assetTable(page).getByText('Completed on time', { exact: true }),
    ).toBeVisible()
    await expect(
      assetTable(page).getByText('Completed late', { exact: true }),
    ).toBeVisible()
    await expect(metricCard(page, 'Progress')).toContainText('67%')
    await expect(metricCard(page, 'Remaining')).toContainText('1')
    await expect(metricCard(page, 'Not completed')).toHaveCount(0)
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Remaining' }),
    ).toBeVisible()
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Not completed' }),
    ).toHaveCount(0)
    await expect(
      page.getByText('No completed inspection results yet', { exact: true }),
    ).toHaveCount(0)

    const activeRequestCount = requests.length
    await page.getByRole('button', { name: 'Change selection' }).click()
    await selectDashboardPeriod(page, 'August')
    expect(requests).toHaveLength(activeRequestCount)
    await generateDashboard(page)
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-08',
    })
    await expect(
      page.getByText('Closed', { exact: true }).first(),
    ).toBeVisible()
    const onTimeRow = assetRow(page, 'FE-001')
    const lateRow = assetRow(page, 'FE-002')
    const notCompletedRow = assetRow(page, 'FE-004')
    await expect(onTimeRow).toHaveCount(1)
    await expect(lateRow).toHaveCount(1)
    await expect(notCompletedRow).toHaveCount(1)
    await expect(assetCell(onTimeRow, 'timeliness')).toHaveText(
      /^Completed on time$/,
    )
    await expect(assetCell(onTimeRow, 'form')).toContainText('Acknowledgement:')
    await expect(assetCell(onTimeRow, 'form')).toContainText('Acknowledged')
    await expect(assetCell(onTimeRow, 'form')).not.toContainText(
      assetIds.onTime,
    )
    await expect(assetCell(lateRow, 'timeliness')).toHaveText(
      /^Completed late$/,
    )
    await expect(assetCell(notCompletedRow, 'timeliness')).toHaveText(
      /^Not completed$/,
    )
    await expect(metricCard(page, 'Completed on time')).toContainText('1')
    await expect(metricCard(page, 'Completed late')).toContainText('1')
    await expect(metricCard(page, 'Not completed')).toHaveText(/1/)
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Not completed' }),
    ).toBeVisible()
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Remaining' }),
    ).toHaveCount(0)
    await expect(
      metricCard(page, 'Compliance rate').locator('p').nth(1),
    ).toHaveText('50%')

    const scheduledAssetsTable = assetTable(page)
    await page.setViewportSize({ width: 768, height: 1024 })
    await expect(scheduledAssetsTable).toBeVisible()
    const tabletPageDimensions = await page.evaluate(() => ({
      viewportWidth: window.innerWidth,
      documentWidth: Math.max(
        document.documentElement.scrollWidth,
        document.body.scrollWidth,
      ),
    }))
    expect(tabletPageDimensions.documentWidth).toBeLessThanOrEqual(
      tabletPageDimensions.viewportWidth,
    )

    await page.setViewportSize({ width: 375, height: 667 })
    await expect(scheduledAssetsTable).toHaveCount(1)
    await expect(scheduledAssetsTable).toBeVisible()
    await expect(scheduledAssetsTable).toHaveAccessibleName(
      'Scheduled PM assets and inspection status',
    )
    const tableContainer = scheduledAssetsTable.locator('..')
    await expect(tableContainer).toHaveCSS('overflow-x', 'auto')
    const dimensions = await tableContainer.evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }))
    expect(dimensions.scrollWidth).toBeGreaterThan(dimensions.clientWidth)
    const pageDimensions = await page.evaluate(() => ({
      viewportWidth: window.innerWidth,
      documentWidth: Math.max(
        document.documentElement.scrollWidth,
        document.body.scrollWidth,
      ),
    }))
    expect(pageDimensions.documentWidth).toBeLessThanOrEqual(
      pageDimensions.viewportWidth,
    )
  })

  test('shows a GSD-scoped PM analytics result with source navigation', async ({
    page,
  }) => {
    await mockDashboardApi(page)
    const analyticsRequests: unknown[] = []
    const interpretationRequests: unknown[] = []
    await page.route('**/api/v1/analytics/pm/interpret', async (route) => {
      interpretationRequests.push(route.request().postDataJSON())
      await route.fulfill(
        jsonResponse({
          status: 'Valid',
          plan: {
            metric: 'NonOperational',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-11',
            department: null,
            groupBy: 'None',
          },
          clarificationFields: [],
          presentation: 'Count',
          code: null,
          canonicalQuestion:
            'Show non-operational assets for fire-extinguisher in 2026-11',
        }),
      )
    })
    await page.route('**/api/v1/analytics/pm/query', async (route) => {
      analyticsRequests.push(route.request().postDataJSON())
      await route.fulfill(
        jsonResponse({
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
            {
              scheduleId: assetIds.onTime,
              assetId: assetIds.onTime,
              inspectionId: assetIds.onTime,
              assetCode: 'FE-001',
              department: 'GSD',
              pmCycle: '2026-11',
              deadline: '2026-11-30T15:59:59.999Z',
              inspectionCompletedAt: '2026-12-01T04:00:00Z',
              timeliness: 'Late',
              condition: 'NonOperational',
              formStatus: 'Draft',
            },
            {
              scheduleId: assetIds.pending,
              assetId: assetIds.pending,
              inspectionId: null,
              assetCode: 'FE-002',
              department: 'GSD',
              pmCycle: '2026-11',
              deadline: '2026-11-30T15:59:59.999Z',
              inspectionCompletedAt: null,
              timeliness: 'NotCompleted',
              condition: 'NotInspected',
              formStatus: null,
            },
            {
              scheduleId: assetIds.notCompleted,
              assetId: assetIds.notCompleted,
              inspectionId: null,
              assetCode: 'FE-003',
              department: 'GSD',
              pmCycle: '2026-11',
              deadline: '2026-11-30T15:59:59.999Z',
              inspectionCompletedAt: null,
              timeliness: 'NotCompleted',
              condition: 'NotInspected',
              formStatus: null,
            },
          ],
          totalSourceCount: 3,
          sourcesTruncated: false,
          scopeNote:
            'These are live PM results for the selected scope, not acknowledged-only official history.',
        }),
      )
    })

    // This legacy dashboard cycle checks that the question sets its own scope.
    await page.goto(
      '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08',
    )
    await expect(
      page.getByRole('heading', { name: 'Ask about PM results' }),
    ).toBeVisible()
    await page
      .getByRole('textbox', { name: 'Question' })
      .fill(
        'Show non-operational assets for fire extinguishers in November 2026',
      )
    await page.getByRole('button', { name: 'Show result' }).click()

    const results = page.getByRole('region', { name: 'PM result' })
    await expect(
      results.getByText('Non-operational assets', { exact: true }),
    ).toBeVisible()
    await expect(results.getByText('3 shown of 3')).toBeVisible()
    await expect(
      results.getByText(/Deadline: Nov 30, 2026.*11:59 PM.*GMT\+8/),
    ).toBeVisible()
    await expect(results.getByText('Form status')).toBeVisible()
    await expect(results.getByText('Draft', { exact: true })).toBeVisible()
    await expect(
      results.getByText('Not inspected', { exact: true }),
    ).toHaveCount(2)
    await expect
      .poll(() => interpretationRequests)
      .toEqual([
        {
          question:
            'Show non-operational assets for fire extinguishers in November 2026',
        },
      ])
    await expect
      .poll(() => analyticsRequests)
      .toEqual([
        {
          question:
            'Show non-operational assets for fire-extinguisher in 2026-11',
        },
      ])

    const sourceLink = results.getByRole('link', {
      name: 'FE-001',
      exact: true,
    })
    await expect(sourceLink).toHaveAttribute(
      'href',
      `/app/assets/${assetIds.onTime}?returnContext=${encodeURIComponent(
        JSON.stringify({
          kind: 'dashboard',
          search: {
            assetCategory: 'fire-extinguisher',
            year: 2026,
            pmCycle: '2026-11',
          },
        }),
      )}`,
    )
  })
})
