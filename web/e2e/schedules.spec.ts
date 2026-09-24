import { expect, test, type Page } from '@playwright/test'

const assetId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '11111111-1111-4111-8111-111111111111'
const gsdSession = {
  accessToken: 'fictional-gsd-schedule-token',
  expiresAtUtc: '2026-08-01T12:00:00Z',
  user: {
    id: '33333333-3333-4333-8333-333333333333',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
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
}
const schedule = {
  id: scheduleId,
  assetId,
  scheduleDate: '2026-08-01T00:00:00+08:00',
  periodType: 'Quarter',
  status: 'Due',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: null,
  assignedToUserId: null,
  assignedSupervisorUserId: null,
  completedAt: null,
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
  asset: {
    id: asset.id,
    assetCode: asset.assetCode,
    assetCategory: asset.assetCategory,
    building: asset.building,
    department: asset.department,
    location: asset.location,
  },
}

async function mockScheduleApi(page: Page, roles = ['GSD']) {
  const session = { ...gsdSession, user: { ...gsdSession.user, roles } }
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
  await page.route('**/api/v1/reference-data/schedule-statuses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { code: 'Due', displayName: 'Due' },
        { code: 'Completed', displayName: 'Completed' },
      ]),
    }),
  )
  await page.route('**/api/v1/reference-data/schedule-period-types', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { code: 'Quarter', displayName: 'Quarter' },
        { code: 'Annual', displayName: 'Annual' },
      ]),
    }),
  )
  await page.route('**/api/v1/reference-data/schedule-quarters', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { code: 'Q1', displayName: 'Q1' },
        { code: 'Q2', displayName: 'Q2' },
        { code: 'Q3', displayName: 'Q3' },
        { code: 'Q4', displayName: 'Q4' },
      ]),
    }),
  )
  await page.route('**/api/v1/schedules**', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(schedule),
      })
    }
    if (new URL(request.url()).pathname.endsWith(`/${scheduleId}`)) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(schedule),
      })
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([schedule]),
    })
  })
}

test.describe('Schedule workflows', () => {
  test('keeps window scroll position on both pagination directions', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    const records = Array.from({ length: 11 }, (_, index) => ({
      ...schedule,
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      asset: {
        ...schedule.asset,
        assetCode: `FE-${String(index + 1).padStart(3, '0')}`,
      },
    }))
    await page.route('**/api/v1/schedules**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(records),
      }),
    )
    await page.setViewportSize({ width: 1280, height: 600 })
    await page.goto('/app/schedules?status=Due')
    const next = page.getByRole('button', { name: 'Next' })
    await next.scrollIntoViewIfNeeded()
    const scrollY = await page.evaluate(() => window.scrollY)
    expect(scrollY).toBeGreaterThan(0)
    await next.click()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByRole('cell', { name: 'FE-011' })).toBeVisible()
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
    await page.getByRole('button', { name: 'Previous' }).click()
    await expect(page).not.toHaveURL(/page=2/)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
  })

  test('browses URL-owned filters and restores a direct schedule detail', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026')
    await expect(page.getByRole('heading', { name: 'Schedules' })).toBeVisible()
    await expect(page.getByLabel('Schedule status')).toHaveValue('Due')
    await expect(page.getByLabel('Quarter')).toHaveValue('Q3')
    await page.getByRole('link', { name: 'View details' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/schedules/${scheduleId}`))
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Batch assignment' }),
    ).toBeVisible()
    await page.getByRole('link', { name: 'Back to schedules' }).click()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page.getByLabel('Schedule status')).toHaveValue('Due')
  })

  test('creates a schedule with only approved fields and opens its detail', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    let payload: Record<string, unknown> | undefined
    await page.route('**/api/v1/schedules', async (route) => {
      if (route.request().method() === 'POST') {
        payload = route.request().postDataJSON() as Record<string, unknown>
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(schedule),
        })
      }
      return route.fallback()
    })
    await page.goto('/app/schedules/new')
    await page.getByLabel('Asset', { exact: true }).selectOption(assetId)
    await page.getByLabel('Schedule date').fill('2026-08-01')
    await page.getByLabel('Quarter').selectOption('Q3')
    await page.getByRole('button', { name: 'Create schedule' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/schedules/${scheduleId}`))
    expect(Object.keys(payload ?? {}).sort()).toEqual(
      ['assetId', 'periodType', 'quarter', 'scheduleDate', 'year'].sort(),
    )
  })

  test('blocks an Admin-only user without sending a schedule POST', async ({
    page,
  }) => {
    await mockScheduleApi(page, ['Admin'])
    let postCount = 0
    page.on('request', (request) => {
      if (
        request.method() === 'POST' &&
        request.url().endsWith('/api/v1/schedules')
      ) {
        postCount += 1
      }
    })
    await page.goto('/app/schedules/new')
    await expect(
      page.getByRole('heading', { name: 'Schedule manager access required' }),
    ).toBeVisible()
    expect(postCount).toBe(0)
  })
})
