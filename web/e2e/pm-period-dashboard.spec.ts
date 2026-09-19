import { expect, test, type Page } from '@playwright/test'

const session = {
  accessToken: 'fictional-pm-dashboard-token',
  expiresAtUtc: '2026-08-01T12:00:00Z',
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
      { pmCycle: '2026-01', scheduled: 1 },
      { pmCycle: '2026-06', scheduled: 3 },
      { pmCycle: '2026-07', scheduled: 4 },
    ],
  },
  {
    assetCategory: 'fire-extinguisher',
    year: 2025,
    cycles: [{ pmCycle: '2025-12', scheduled: 2 }],
  },
  {
    assetCategory: 'fire-alarm',
    year: 2026,
    cycles: [{ pmCycle: '2026-03', scheduled: 2 }],
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
  return new Date(Date.UTC(year, month, 0, 16)).toISOString()
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
    scheduleDate: `${pmCycle}-01T00:00:00+00:00`,
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
    acknowledgedAt: isAcknowledged ? '2026-07-30T08:00:00Z' : null,
  }
}

function fixtureAssets(pmCycle: string) {
  if (pmCycle === '2026-01') {
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

  if (pmCycle === '2026-06') {
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
        inspectionCompletedAt: '2026-06-20T08:00:00Z',
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
        inspectionCompletedAt: '2026-06-30T08:00:00Z',
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
      inspectionCompletedAt: '2026-07-20T08:00:00Z',
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
      inspectionCompletedAt: '2026-07-30T08:00:00Z',
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
  const pmCycle = url.searchParams.get('pmCycle') ?? '2026-07'
  const assetCategory =
    url.searchParams.get('assetCategory') ?? 'fire-extinguisher'
  const department = url.searchParams.get('department')
  const condition = url.searchParams.get('condition')
  const timeliness = url.searchParams.get('timeliness')
  const search = url.searchParams.get('search')?.toLowerCase()
  const periodState =
    pmCycle === '2026-01'
      ? 'Future'
      : pmCycle === '2026-06'
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
        submittedAt: '2026-07-30T08:00:00Z',
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

test.describe('PM period dashboard', () => {
  test('navigates category, year, period, and asset detail links', async ({
    page,
  }) => {
    await mockDashboardApi(page)
    await page.goto('/app/dashboard')

    await expect(
      page.getByRole('heading', { name: /Preventive maintenance compliance/ }),
    ).toBeVisible()
    await expect(page.locator('#pm-dashboard-cycle')).toHaveValue('2026-07')
    await expect(page.locator('#pm-dashboard-cycle')).toContainText('July 2026')

    await page.locator('#pm-dashboard-category').selectOption('fire-alarm')
    await expect(page).toHaveURL(/assetCategory=fire-alarm/)
    await expect(page.locator('#pm-dashboard-cycle')).toHaveValue('2026-03')

    await page
      .locator('#pm-dashboard-category')
      .selectOption('fire-extinguisher')
    await page.locator('#pm-dashboard-year').selectOption('2025')
    await expect(page.locator('#pm-dashboard-cycle')).toHaveValue('2025-12')
    await page.locator('#pm-dashboard-year').selectOption('2026')
    await page.locator('#pm-dashboard-cycle').selectOption('2026-07')

    const assetLink = page.getByRole('link', { name: 'FE-001' }).first()
    await expect(assetLink).toHaveAttribute(
      'href',
      `/app/assets/${assetIds.onTime}`,
    )
    await assetLink.click()
    await expect(page).toHaveURL(new RegExp(`/app/assets/${assetIds.onTime}$`))
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()
  })

  test('keeps official metrics scoped while table filters change', async ({
    page,
  }) => {
    const requests = await mockDashboardApi(page)
    await page.goto('/app/dashboard')
    await page.locator('#pm-dashboard-cycle').selectOption('2026-06')
    await expect(
      page.getByText('Active', { exact: true }).first(),
    ).toBeVisible()

    await page.locator('#pm-dashboard-department').fill('OPS')
    await page.getByRole('button', { name: 'Apply filters' }).click()
    await expectRequest(requests, {
      pmCycle: '2026-06',
      department: 'OPS',
    })
    await expect(metricCard(page, 'Scheduled')).toContainText('2')

    await page.locator('#pm-dashboard-condition').selectOption('Operational')
    await expectRequest(requests, {
      pmCycle: '2026-06',
      department: 'OPS',
      condition: 'Operational',
    })
    await expect(metricCard(page, 'Scheduled')).toContainText('2')

    await page.locator('#pm-dashboard-timeliness').selectOption('Pending')
    await expectRequest(requests, {
      pmCycle: '2026-06',
      department: 'OPS',
      condition: 'Operational',
      timeliness: 'Pending',
    })
    await expect(metricCard(page, 'Scheduled')).toContainText('2')

    await page.locator('#pm-dashboard-search').fill('FE-003')
    await page.getByRole('button', { name: 'Apply filters' }).click()
    await expectRequest(requests, {
      pmCycle: '2026-06',
      department: 'OPS',
      condition: 'Operational',
      timeliness: 'Pending',
      search: 'FE-003',
    })
    await expect(metricCard(page, 'Scheduled')).toContainText('2')
    await expect(assetTable(page).locator('tbody tr')).toHaveCount(1)
    await expect(assetTable(page).getByText('FE-003')).toBeVisible()

    await page.getByRole('button', { name: 'Clear filters' }).click()
    await expect(page.locator('#pm-dashboard-department')).toHaveValue('')
    await expect(page.locator('#pm-dashboard-condition')).toHaveValue('')
    await expect(page.locator('#pm-dashboard-timeliness')).toHaveValue('')
    await expect(page.locator('#pm-dashboard-search')).toHaveValue('')
    await expect(metricCard(page, 'Scheduled')).toContainText('3')
    await expect(assetTable(page).locator('tbody tr')).toHaveCount(3)
  })

  test('presents Future, Active, and Closed states and contains tables on mobile', async ({
    page,
  }) => {
    await mockDashboardApi(page)
    await page.goto('/app/dashboard')

    await page.locator('#pm-dashboard-cycle').selectOption('2026-01')
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

    await page.locator('#pm-dashboard-cycle').selectOption('2026-06')
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

    await page.locator('#pm-dashboard-cycle').selectOption('2026-07')
    await expect(
      page.getByText('Closed', { exact: true }).first(),
    ).toBeVisible()
    await expect(
      assetTable(page).getByText('Not completed', { exact: true }),
    ).toBeVisible()
    await expect(metricCard(page, 'Completed on time')).toContainText('1')
    await expect(metricCard(page, 'Completed late')).toContainText('1')
    await expect(metricCard(page, 'Not completed')).toContainText('1')
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Not completed' }),
    ).toBeVisible()
    await expect(
      batchTable(page).getByRole('columnheader', { name: 'Remaining' }),
    ).toHaveCount(0)
    await expect(metricCard(page, 'On-time compliance')).toContainText('50%')

    await page.setViewportSize({ width: 375, height: 667 })
    const tableContainer = assetTable(page).locator('..')
    await expect(tableContainer).toHaveCSS('overflow-x', 'auto')
    const dimensions = await tableContainer.evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }))
    expect(dimensions.scrollWidth).toBeGreaterThan(dimensions.clientWidth)
  })
})
