import { expect, test, type Page } from '@playwright/test'

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
  correctiveFollowUpStatus = null,
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
  correctiveFollowUpStatus?: string | null
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
    correctiveFollowUpStatus,
    externalPmNumber: null,
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
      formStatus: 'Acknowledged',
      isAcknowledged: true,
      correctiveFollowUpStatus: 'CorrectiveFollowUpPending',
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
    const assetId = url.pathname.split('/').at(-1)
    if (assetId === assetIds.onTime || assetId === assetIds.late) {
      return route.fulfill(
        jsonResponse({
          id: assetId,
          assetCode: assetId === assetIds.onTime ? 'FE-001' : 'FE-002',
          assetCategory: 'fire-extinguisher',
          building: 'Main Building',
          department: assetId === assetIds.onTime ? 'GSD' : 'OPS',
          location: 'Lobby',
          qrCodeValue: `UNIPM-${assetId === assetIds.onTime ? 'FE-001' : 'FE-002'}`,
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
  await page.route(`**/api/v1/inspections/${assetIds.late}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: assetIds.late,
        scheduleId: assetIds.late,
        assetId: assetIds.late,
        inspectorUserId: session.user.id,
        dateInspected: dateAfterCycle('2026-08', 2),
        isOperational: false,
        hasPhotoEvidence: false,
        remarks: 'Pressure is low.',
        actionsRecommendations: 'Arrange a pressure check.',
        externalPmNumber: null,
        wmsReferralRevision: 0,
        correctiveFollowUpStatus: 'CorrectiveFollowUpPending',
        createdAt: dateAfterCycle('2026-08', 2),
        updatedAt: dateAfterCycle('2026-08', 2),
      }),
    ),
  )
  await page.route(`**/api/v1/schedules/${assetIds.late}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: assetIds.late,
        assetId: assetIds.late,
        scheduleDate: deadlineFor('2026-08'),
        pmCycle: '2026-08',
        periodType: 'Quarter',
        status: 'Completed',
        quarter: 'Q3',
        semester: null,
        year: 2026,
        academicYear: '2026-2027',
        assignedToUserId: session.user.id,
        completedAt: dateAfterCycle('2026-08', 2),
        createdAt: dateAfterCycle('2026-08', 1),
        updatedAt: dateAfterCycle('2026-08', 2),
        asset: {
          id: assetIds.late,
          assetCode: 'FE-002',
          assetCategory: 'fire-extinguisher',
          building: 'Main Building',
          department: 'OPS',
          location: 'Lobby',
          status: 'Active',
        },
      }),
    ),
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

async function selectDashboardPeriod(page: Page, month: string) {
  const category = page.getByRole('combobox', { name: 'Asset category' })
  await category.fill('fire')
  await page.getByRole('option', { name: 'Fire extinguishers' }).click()
  await page.getByLabel('Year').selectOption('2026')
  const monthNumber = { May: '05', August: '08', November: '11' }[month]
  if (!monthNumber) throw new Error(`Unsupported fixture month: ${month}`)
  await page.getByLabel('Scheduled month').selectOption(`2026-${monthNumber}`)
}

async function generateDashboard(page: Page) {
  await page.getByRole('button', { name: 'Generate dashboard' }).click()
}

test.describe('PM period dashboard', () => {
  test('filters and exports the generated period report', async ({ page }) => {
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
    await expect(report).toBeVisible()
    await expect(
      metricCard(page, 'Inspected / scheduled').locator('p').nth(1),
    ).toHaveText('2 / 3')
    await expect(metricCard(page, 'Progress').locator('p').nth(1)).toHaveText(
      '67%',
    )
    await expect(
      metricCard(page, 'On-time compliance').locator('p').nth(1),
    ).toHaveText('Not measurable yet')
    const outcomes = page.getByRole('region', {
      name: 'Completion against the PM deadline',
    })
    await expect(outcomes.getByRole('listitem')).toHaveText([
      'On time',
      'Late',
      'Still due',
    ])
    await expect(
      report.getByText(
        'No acknowledged non-operational findings are recorded for this PM period.',
      ),
    ).toBeVisible()

    await page.screenshot({
      path: 'test-results/web-ui-refinement-dashboard-desktop.png',
    })
    await page.setViewportSize({ width: 375, height: 812 })
    await page.screenshot({
      path: 'test-results/web-ui-refinement-dashboard-mobile.png',
      fullPage: true,
    })
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      ),
    ).toBeLessThanOrEqual(1)
    await page.setViewportSize({ width: 1280, height: 900 })

    await page.getByRole('button', { name: 'Export dashboard' }).click()
    await expect(page.locator('html')).toHaveAttribute(
      'data-print-invoked',
      'true',
    )
    await page.emulateMedia({ media: 'print' })
    await expect(
      page.getByRole('button', { name: 'Export dashboard' }),
    ).toBeHidden()
    await page.emulateMedia({ media: 'screen' })

    await page
      .getByRole('combobox', { name: 'Department', exact: true })
      .selectOption('OPS')
    await expectRequest(requests, { ...mayScope, department: 'OPS' })
    await expect(page).toHaveURL(/department=OPS/)
    await expect(
      metricCard(page, 'Inspected / scheduled').locator('p').nth(1),
    ).toHaveText('1 / 2')
    await expect(metricCard(page, 'Progress').locator('p').nth(1)).toHaveText(
      '50%',
    )
  })

  test('loads a valid scope URL and keeps corrective inspection navigation filtered', async ({
    page,
  }) => {
    const requests = await mockDashboardApi(page)
    const search = {
      assetCategory: 'fire-extinguisher',
      year: 2026,
      pmCycle: '2026-08',
      department: 'OPS',
    }
    const params = new URLSearchParams({
      assetCategory: search.assetCategory,
      year: String(search.year),
      pmCycle: search.pmCycle,
      department: search.department,
    })
    await page.goto('/app/dashboard?' + params.toString())
    await expectRequest(requests, {
      assetCategory: search.assetCategory,
      pmCycle: search.pmCycle,
      department: search.department,
    })
    await expect(page.locator('.pm-dashboard-report')).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Corrective follow-up' }),
    ).toBeVisible()
    await expect(page.getByText('FE-002', { exact: true })).toBeVisible()

    const inspectionLink = page.getByRole('link', {
      name: 'View inspection',
    })
    const inspectionUrl = new URL(
      (await inspectionLink.getAttribute('href'))!,
      'http://localhost',
    )
    expect(inspectionUrl.pathname).toBe('/app/inspections/' + assetIds.late)
    expect(
      JSON.parse(inspectionUrl.searchParams.get('returnContext') ?? 'null'),
    ).toEqual({ kind: 'dashboard', search })
    await inspectionLink.click()
    await expect(page).toHaveURL(
      new RegExp('/app/inspections/' + assetIds.late),
    )
    await expect(page.getByRole('heading', { name: 'FE-002' })).toBeVisible()
    await page.getByRole('link', { name: 'Back to PM dashboard' }).click()
    await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/pmCycle=2026-08/)
    await expect(page).toHaveURL(/department=OPS/)
  })

  test('presents Future, Active, and Closed states and contains follow-up on mobile', async ({
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
      page.getByText('Upcoming', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      metricCard(page, 'Inspected / scheduled').locator('p').nth(1),
    ).toHaveText('0 / 1')
    await expect(metricCard(page, 'Progress').locator('p').nth(1)).toHaveText(
      '0%',
    )
    const outcomes = () =>
      page.getByRole('region', { name: 'Completion against the PM deadline' })
    await expect(outcomes().getByRole('listitem')).toHaveText([
      'On time',
      'Late',
      'Still due',
    ])

    const futureRequestCount = requests.length
    await page.getByRole('button', { name: 'Change period' }).click()
    await selectDashboardPeriod(page, 'May')
    expect(requests).toHaveLength(futureRequestCount)
    await generateDashboard(page)
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-05',
    })
    await expect(
      page.getByText('In progress', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      metricCard(page, 'Inspected / scheduled').locator('p').nth(1),
    ).toHaveText('2 / 3')
    await expect(metricCard(page, 'Progress').locator('p').nth(1)).toHaveText(
      '67%',
    )
    await expect(outcomes().getByRole('listitem')).toHaveText([
      'On time',
      'Late',
      'Still due',
    ])

    const activeRequestCount = requests.length
    await page.getByRole('button', { name: 'Change period' }).click()
    await selectDashboardPeriod(page, 'August')
    expect(requests).toHaveLength(activeRequestCount)
    await generateDashboard(page)
    await expectRequest(requests, {
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-08',
    })
    await expect(
      page.getByText('Past deadline', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      metricCard(page, 'Inspected / scheduled').locator('p').nth(1),
    ).toHaveText('2 / 4')
    await expect(metricCard(page, 'Progress').locator('p').nth(1)).toHaveText(
      '50%',
    )
    await expect(
      metricCard(page, 'On-time compliance').locator('p').nth(1),
    ).toHaveText('50%')
    await expect(outcomes().getByRole('listitem')).toHaveText([
      'On time',
      'Late',
      'Unfinished',
    ])

    const correctiveTable = page.getByRole('table', {
      name: 'Corrective follow-up for acknowledged non-operational findings',
    })
    await expect(correctiveTable).toBeVisible()
    await page.setViewportSize({ width: 768, height: 1024 })
    let dimensions = await page.evaluate(() => ({
      viewportWidth: window.innerWidth,
      documentWidth: Math.max(
        document.documentElement.scrollWidth,
        document.body.scrollWidth,
      ),
    }))
    expect(dimensions.documentWidth).toBeLessThanOrEqual(
      dimensions.viewportWidth,
    )

    await page.setViewportSize({ width: 375, height: 667 })
    const container = correctiveTable.locator('..')
    await expect(container).toHaveCSS('overflow-x', 'auto')
    dimensions = await page.evaluate(() => ({
      viewportWidth: window.innerWidth,
      documentWidth: Math.max(
        document.documentElement.scrollWidth,
        document.body.scrollWidth,
      ),
    }))
    expect(dimensions.documentWidth).toBeLessThanOrEqual(
      dimensions.viewportWidth,
    )
    const scrollDimensions = await container.evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }))
    expect(scrollDimensions.scrollWidth).toBeGreaterThan(
      scrollDimensions.clientWidth,
    )
  })
})
