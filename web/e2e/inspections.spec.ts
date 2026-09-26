import { expect, test, type Page } from '@playwright/test'

const inspectionId = '11111111-1111-4111-8111-111111111111'
const scheduleId = '22222222-2222-4222-8222-222222222222'
const assetId = '33333333-3333-4333-8333-333333333333'
const session = {
  accessToken: 'fictional-inspection-token',
  expiresAtUtc: '2026-08-01T12:00:00Z',
  user: {
    id: '44444444-4444-4444-8444-444444444444',
    email: 'fictional.inspector@example.test',
    displayName: 'Fictional Inspector',
    roles: ['Inspector'],
  },
}
const asset = {
  id: assetId,
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Lobby',
  qrCodeValue: 'UNIPM-FE-001',
  status: 'Active',
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
  hasVerificationLocation: false,
}
const schedule = {
  id: scheduleId,
  assetId,
  scheduleDate: '2026-08-01T00:00:00+08:00',
  periodType: 'Quarter',
  status: 'Completed',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: null,
  assignedToUserId: null,
  assignedSupervisorUserId: null,
  completedAt: '2026-07-22T01:00:00Z',
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T01:00:00Z',
  asset: {
    id: assetId,
    assetCode: 'FE-001',
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    location: 'Lobby',
  },
}
const inspection = {
  id: inspectionId,
  scheduleId,
  assetId,
  inspectorUserId: session.user.id,
  dateInspected: '2026-07-22T01:00:00Z',
  isOperational: false,
  remarks: 'Low pressure recorded during inspection.',
  actionsRecommendations: 'Arrange a pressure check.',
  createdAt: '2026-07-22T01:00:00Z',
  updatedAt: '2026-07-22T01:00:00Z',
}
const history = [
  {
    id: inspectionId,
    dateInspected: inspection.dateInspected,
    isOperational: inspection.isOperational,
    remarks: inspection.remarks,
    actionsRecommendations: inspection.actionsRecommendations,
  },
]

async function mockInspectionApi(page: Page) {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(session),
    }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(session.user),
    }),
  )
  await page.route('**/api/v1/assets**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([asset]),
    }),
  )
  await page.route('**/api/v1/schedules**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([schedule]),
    }),
  )
  await page.route('**/api/v1/inspections**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([inspection]),
    }),
  )
  await page.route('**/api/v1/assets/**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(asset),
    }),
  )
  await page.route('**/api/v1/schedules/**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(schedule),
    }),
  )
  await page.route(`**/api/v1/inspections/history/${assetId}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(history),
    }),
  )
  await page.route(`**/api/v1/inspections/${inspectionId}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(inspection),
    }),
  )
}

async function registryLayout(page: Page) {
  return page.getByRole('table').evaluate((table) => {
    const viewport = table.parentElement
    const pagination = document.querySelector<HTMLElement>(
      'nav[aria-label="Inspections pagination"]',
    )
    if (!viewport || !pagination) throw new Error('Registry layout not found')

    return {
      viewportHeight: viewport.getBoundingClientRect().height,
      paginationTop: pagination.getBoundingClientRect().top + window.scrollY,
    }
  })
}

function expectStableRegistryLayout(
  before: Awaited<ReturnType<typeof registryLayout>>,
  after: Awaited<ReturnType<typeof registryLayout>>,
) {
  expect(
    before.viewportHeight,
    `Inspections result viewport exceeds 780px: ${before.viewportHeight}px`,
  ).toBeLessThanOrEqual(780)
  expect(
    after.viewportHeight,
    `Inspections result viewport exceeds 780px: ${after.viewportHeight}px`,
  ).toBeLessThanOrEqual(780)
  expect(
    Math.abs(after.viewportHeight - before.viewportHeight),
    `Result viewport heights changed: ${before.viewportHeight}px to ${after.viewportHeight}px`,
  ).toBeLessThanOrEqual(8)
  expect(
    Math.abs(after.paginationTop - before.paginationTop),
    `Pagination top moved: ${before.paginationTop}px to ${after.paginationTop}px`,
  ).toBeLessThanOrEqual(8)
}

test.describe('Inspection review workflows', () => {
  test('keeps window scroll position on both pagination directions', async ({
    page,
  }) => {
    await mockInspectionApi(page)
    const records = Array.from({ length: 11 }, (_, index) => ({
      ...inspection,
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      remarks: `Finding ${index + 1}`,
    }))
    await page.route('**/api/v1/inspections**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(records),
      }),
    )
    await page.setViewportSize({ width: 1280, height: 600 })
    await page.goto('/app/inspections?isOperational=false')
    const rows = page.getByRole('table').locator('tbody tr')
    await expect(rows).toHaveCount(10)
    const next = page.getByRole('button', { name: 'Next' })
    await next.scrollIntoViewIfNeeded()
    const scrollY = await page.evaluate(() => window.scrollY)
    expect(scrollY).toBeGreaterThan(0)
    const firstPageLayout = await registryLayout(page)
    await next.click()
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByRole('cell', { name: 'Finding 11' })).toBeVisible()
    await expect(rows).toHaveCount(1)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
    const lastPageLayout = await registryLayout(page)
    expectStableRegistryLayout(firstPageLayout, lastPageLayout)
    await page.getByRole('button', { name: 'Previous' }).click()
    await expect(page).not.toHaveURL(/page=2/)
    await expect(rows).toHaveCount(10)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
    expectStableRegistryLayout(firstPageLayout, await registryLayout(page))

    await page.goto('/app/inspections?isOperational=false&page=2')
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)
    await page.reload()
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)

    await page.goto('/app/inspections?isOperational=false&page=99')
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page).toHaveURL(/page=2/)

    await page.setViewportSize({ width: 375, height: 667 })
    await page.goto('/app/inspections?isOperational=false&page=2')
    const mobileInspection = page
      .getByText('Finding 11', { exact: true })
      .last()
      .locator('xpath=..')
    await expect(mobileInspection).toBeVisible()
    const pagination = page.getByRole('navigation', {
      name: 'Inspections pagination',
    })
    await pagination.scrollIntoViewIfNeeded()
    const [inspectionBounds, paginationBounds] = await Promise.all([
      mobileInspection.boundingBox(),
      pagination.boundingBox(),
    ])
    if (!inspectionBounds || !paginationBounds) {
      throw new Error('Mobile registry geometry could not be measured')
    }
    const mobileGap =
      paginationBounds.y - (inspectionBounds.y + inspectionBounds.height)
    expect(mobileGap).toBeGreaterThanOrEqual(0)
    expect(mobileGap).toBeLessThanOrEqual(64)
    expect(
      await page.evaluate(
        () =>
          document.documentElement.scrollWidth -
          document.documentElement.clientWidth,
      ),
    ).toBeLessThanOrEqual(1)
  })

  test('filters inspection records and opens immutable source detail', async ({
    page,
  }) => {
    await mockInspectionApi(page)
    await page.goto('/app/inspections?isOperational=false')
    await expect(
      page.getByRole('heading', { name: 'Inspections' }),
    ).toBeVisible()
    await expect(page.getByLabel('Recorded operational result')).toHaveValue(
      'false',
    )
    await expect(
      page.getByRole('cell', {
        name: 'Low pressure recorded during inspection.',
      }),
    ).toBeVisible()
    await page.getByRole('link', { name: 'View details' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/inspections/${inspectionId}`))
    await expect(page.getByText('Recommendation')).toBeVisible()
    await expect(page.getByText('Arrange a pressure check.')).toBeVisible()
    await expect(
      page.getByRole('button', { name: /submit|record/i }),
    ).toHaveCount(0)
    await page.getByRole('link', { name: 'Back to inspections' }).click()
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page.getByLabel('Recorded operational result')).toHaveValue(
      'false',
    )
  })

  test('shows asset inspection history and opens the linked source record', async ({
    page,
  }) => {
    await mockInspectionApi(page)
    await page.goto(`/app/assets/${assetId}`)
    await expect(
      page.getByRole('heading', { name: 'Recent inspection history' }),
    ).toBeVisible()
    await page.getByRole('link', { name: 'View source' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/inspections/${inspectionId}`))
    await expect(
      page.getByText('Low pressure recorded during inspection.'),
    ).toBeVisible()
  })
})
