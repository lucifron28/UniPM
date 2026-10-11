import { expect, test, type Page } from '@playwright/test'

const scheduleRegistryRoute = /\/api\/v1\/schedules\/registry(?:\?.*)?$/

const assetId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '11111111-1111-4111-8111-111111111111'
const gsdSession = {
  accessToken: 'fictional-gsd-schedule-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
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
  hasVerificationLocation: false,
}
const schedule = {
  id: scheduleId,
  assetId,
  scheduleDate: '2026-08-01T00:00:00+08:00',
  pmCycle: '2026-08',
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
const scheduleRegistryItem = {
  id: schedule.id,
  assetId: schedule.assetId,
  scheduleDate: schedule.scheduleDate,
  pmCycle: schedule.pmCycle,
  periodType: schedule.periodType,
  status: schedule.status,
  quarter: schedule.quarter,
  semester: schedule.semester,
  year: schedule.year,
  academicYear: schedule.academicYear,
  completedAt: schedule.completedAt,
  createdAt: schedule.createdAt,
  updatedAt: schedule.updatedAt,
  asset: schedule.asset,
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
  await page.route(/\/api\/v1\/assets(?:\?.*)?$/, async (route) => {
    const requestUrl = new URL(route.request().url())
    const parameters = requestUrl.searchParams
    if (
      route.request().method() !== 'GET' ||
      requestUrl.pathname !== '/api/v1/assets' ||
      parameters.get('limit') !== '20' ||
      parameters.get('status') !== 'Active' ||
      parameters.get('hasDepartment') !== 'true'
    ) {
      return route.fallback()
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([asset]),
    })
  })
  await page.route(new RegExp(`/api/v1/assets/${assetId}$`), async (route) => {
    const requestUrl = new URL(route.request().url())
    if (
      route.request().method() !== 'GET' ||
      requestUrl.pathname !== `/api/v1/assets/${assetId}`
    ) {
      return route.fallback()
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(asset),
    })
  })
  await page.route('**/api/v1/reference-data/asset-categories', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          code: 'fire-extinguisher',
          displayName: 'Fire extinguishers',
          scheduledMonths: [2, 5, 8, 11],
        },
      ]),
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
    const pathname = new URL(request.url()).pathname
    if (request.method() === 'POST') {
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(schedule),
      })
    }
    if (pathname === '/api/v1/schedules/supervisor-assignment-options') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ supervisors: [] }),
      })
    }
    if (pathname === '/api/v1/schedules/assignment-options') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ workers: [] }),
      })
    }
    if (pathname === '/api/v1/schedules/enrollment-deferrals') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          page: 1,
          pageSize: 10,
          total: 0,
          pendingCount: 0,
          reviewedCount: 0,
          items: [],
        }),
      })
    }
    if (pathname === '/api/v1/schedules/coverage-review') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          year: 2026,
          page: 1,
          pageSize: 10,
          total: 0,
          items: [],
        }),
      })
    }
    if (pathname === '/api/v1/schedules/registry') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [scheduleRegistryItem],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          overallCount: 1,
        }),
      })
    }
    if (pathname === '/api/v1/schedules/registry/options') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          categories: [
            {
              code: 'fire-extinguisher',
              displayName: 'Fire extinguishers',
              scheduledMonths: [2, 5, 8, 11],
            },
          ],
          quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
          departments: ['GSD'],
          years: ['2026'],
        }),
      })
    }
    if (pathname === '/api/v1/schedules/registry/summary') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalCount: 1,
          dueCount: schedule.status === 'Due' ? 1 : 0,
          ongoingCount: 0,
          overdueCount: 0,
          completedCount: 0,
          cancelledCount: 0,
        }),
      })
    }
    if (pathname.endsWith(`/${scheduleId}`)) {
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

async function registryLayout(page: Page) {
  return page.getByRole('table').evaluate((table) => {
    const viewport = table.parentElement
    const pagination = document.querySelector<HTMLElement>(
      'nav[aria-label="Schedules pagination"]',
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
    `Schedules result viewport exceeds 780px: ${before.viewportHeight}px`,
  ).toBeLessThanOrEqual(780)
  expect(
    after.viewportHeight,
    `Schedules result viewport exceeds 780px: ${after.viewportHeight}px`,
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

test.describe('Schedule workflows', () => {
  test('keeps window scroll position on both pagination directions', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    const records = Array.from({ length: 26 }, (_, index) => ({
      ...schedule,
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      asset: {
        ...schedule.asset,
        assetCode: `FE-${String(index + 1).padStart(3, '0')}`,
      },
    }))
    await page.route(scheduleRegistryRoute, (route) => {
      const url = new URL(route.request().url())
      const pageNumber = Number(url.searchParams.get('page') ?? 1)
      const pageSize = Number(url.searchParams.get('pageSize') ?? 25)
      const start = (pageNumber - 1) * pageSize
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: records.slice(start, start + pageSize).map((item) => ({
            ...scheduleRegistryItem,
            id: item.id,
            asset: {
              ...scheduleRegistryItem.asset,
              assetCode: item.asset.assetCode,
            },
          })),
          page: pageNumber,
          pageSize,
          totalCount: records.length,
          overallCount: records.length,
        }),
      })
    })
    await page.route('**/api/v1/schedules/registry/summary', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalCount: records.length,
          dueCount: records.filter((item) => item.status === 'Due').length,
          ongoingCount: records.filter((item) => item.status === 'Ongoing')
            .length,
          overdueCount: records.filter((item) => item.status === 'Overdue')
            .length,
          completedCount: records.filter((item) => item.status === 'Completed')
            .length,
          cancelledCount: records.filter((item) => item.status === 'Cancelled')
            .length,
        }),
      }),
    )
    await page.setViewportSize({ width: 1280, height: 600 })
    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026')
    // Longer CPMP cells must preserve the viewport even with enlarged text.
    await page.addStyleTag({
      content: 'tbody td { font-size: 18px; line-height: 28px; }',
    })
    const rows = page.getByRole('table').locator('tbody tr')
    await expect(rows).toHaveCount(25)
    const next = page.getByRole('button', { name: 'Next' })
    await next.scrollIntoViewIfNeeded()
    const scrollY = await page.evaluate(() => window.scrollY)
    expect(scrollY).toBeGreaterThan(0)
    const firstPageLayout = await registryLayout(page)
    await next.click()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByRole('cell', { name: 'FE-026' })).toBeVisible()
    await expect(rows).toHaveCount(1)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
    const lastPageLayout = await registryLayout(page)
    expectStableRegistryLayout(firstPageLayout, lastPageLayout)
    await page.getByRole('button', { name: 'Previous' }).click()
    await expect(page).not.toHaveURL(/page=2/)
    await expect(rows).toHaveCount(25)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollY)
    expectStableRegistryLayout(firstPageLayout, await registryLayout(page))

    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026&page=2')
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)
    await page.reload()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)

    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026&page=99')
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)

    await page.setViewportSize({ width: 375, height: 667 })
    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026&page=2')
    const mobileSchedule = page
      .getByRole('heading', { name: 'FE-026' })
      .locator('xpath=../../..')
    await expect(mobileSchedule).toBeVisible()
    const pagination = page.getByRole('navigation', {
      name: 'Schedules pagination',
    })
    await pagination.scrollIntoViewIfNeeded()
    const [scheduleBounds, paginationBounds] = await Promise.all([
      mobileSchedule.boundingBox(),
      pagination.boundingBox(),
    ])
    if (!scheduleBounds || !paginationBounds) {
      throw new Error('Mobile registry geometry could not be measured')
    }
    const mobileGap =
      paginationBounds.y - (scheduleBounds.y + scheduleBounds.height)
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

  test('browses URL-owned filters and restores a direct schedule detail', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    await page.goto('/app/schedules?status=Due&quarter=Q3&year=2026')
    await expect(page.getByRole('heading', { name: 'Schedules' })).toBeVisible()
    await expect(page.getByLabel('Schedule status')).toHaveValue('Due')
    await expect(
      page.getByLabel('Calendar quarter', { exact: true }),
    ).toHaveValue('Q3')
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
    const assetPicker = page.getByRole('combobox', { name: 'Asset' })
    await assetPicker.fill('FE-001')
    await page.getByRole('option', { name: /FE-001/ }).click()
    await page.getByLabel('Scheduled year').fill('2026')
    await page.getByLabel('Scheduled month').selectOption('8')
    await expect(page.getByLabel('Due date')).toHaveValue('Aug 31, 2026')
    await expect(page.getByLabel('Schedule date')).toHaveCount(0)
    await expect(page.locator('input[type="date"]')).toHaveCount(0)
    await page.getByRole('button', { name: 'Create schedule' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/schedules/${scheduleId}`))
    expect(Object.keys(payload ?? {}).sort()).toEqual(
      ['assetId', 'periodType', 'quarter', 'pmCycle', 'year'].sort(),
    )
    expect(payload).toMatchObject({
      assetId,
      periodType: 'Quarter',
      quarter: 'Q3',
      pmCycle: '2026-08',
      year: 2026,
    })
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
      page.getByRole('heading', { name: 'Access denied' }),
    ).toBeVisible()
    await expect(
      page.getByText('This page requires one of these roles: GSD, Supervisor.'),
    ).toBeVisible()
    expect(postCount).toBe(0)
  })
})
