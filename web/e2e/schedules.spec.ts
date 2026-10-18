import { expect, test, type Page } from '@playwright/test'

const scheduleRegistryRoute = /\/api\/v1\/schedules\/registry(?:\?.*)?$/
const responsiveScheduleWidths = [320, 375, 390, 768]

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
      requestUrl.pathname !== '/api/v1/assets'
    ) {
      return route.fallback()
    }
    const search = parameters.get('search')?.trim().toLocaleLowerCase()
    const matchesAsset =
      (!parameters.has('status') ||
        parameters.get('status') === asset.status) &&
      (parameters.get('hasDepartment') !== 'true' ||
        Boolean(asset.department?.trim())) &&
      (!search ||
        [
          asset.assetCode,
          asset.assetCategory,
          asset.building,
          asset.department,
          asset.location,
        ].some((value) => value.toLocaleLowerCase().includes(search)))
    const limit = Number(parameters.get('limit') ?? 1)
    const results = matchesAsset ? [asset].slice(0, limit) : []
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(results),
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

async function horizontalOverflowGeometry(page: Page) {
  return page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth
    const offenders = Array.from(
      document.body.querySelectorAll<HTMLElement>('*'),
    )
      .map((element) => {
        const rect = element.getBoundingClientRect()
        if (
          rect.width < 1 ||
          rect.height < 1 ||
          rect.right <= viewportWidth + 1
        ) {
          return null
        }
        const style = window.getComputedStyle(element)
        const ancestors: Array<Record<string, string | number>> = []
        let ancestor = element.parentElement
        while (ancestor && ancestors.length < 4) {
          const ancestorRect = ancestor.getBoundingClientRect()
          const ancestorStyle = window.getComputedStyle(ancestor)
          ancestors.push({
            tag: ancestor.tagName.toLowerCase(),
            id: ancestor.id,
            className: String(ancestor.className).slice(0, 120),
            left: Math.round(ancestorRect.left),
            right: Math.round(ancestorRect.right),
            width: Math.round(ancestorRect.width),
            minWidth: ancestorStyle.minWidth,
            overflowX: ancestorStyle.overflowX,
          })
          ancestor = ancestor.parentElement
        }
        return {
          tag: element.tagName.toLowerCase(),
          id: element.id,
          className: String(element.className).slice(0, 180),
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          width: Math.round(rect.width),
          scrollWidth: element.scrollWidth,
          clientWidth: element.clientWidth,
          overflowX: style.overflowX,
          text: (element.innerText || '').replace(/\s+/g, ' ').slice(0, 70),
          ancestors,
        }
      })
      .filter((item): item is NonNullable<typeof item> => item !== null)
      .sort((left, right) => right.right - left.right)
      .slice(0, 12)

    return {
      viewportWidth: window.innerWidth,
      clientWidth: viewportWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      offenders,
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
  }, testInfo) => {
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
    await page.goto(
      '/app/schedules?view=history&status=Due&quarter=Q3&year=2026',
    )
    await expect(page).toHaveURL(/view=history/)
    // Longer CPMP cells must preserve the viewport even with enlarged text.
    await page.addStyleTag({
      content: 'tbody td { font-size: 18px; line-height: 28px; }',
    })
    const rows = page.getByRole('table').locator('tbody tr')
    await expect(rows).toHaveCount(25)
    const next = page.getByRole('button', { name: 'Next' })
    const workspace = page.locator('.app-workspace')
    await next.scrollIntoViewIfNeeded()
    const scrollTop = await workspace.evaluate((element) => element.scrollTop)
    expect(scrollTop).toBeGreaterThan(0)
    const firstPageLayout = await registryLayout(page)
    await next.click()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByRole('cell', { name: 'FE-026' })).toBeVisible()
    await expect(rows).toHaveCount(1)
    await expect
      .poll(() => workspace.evaluate((element) => element.scrollTop))
      .toBe(scrollTop)
    const lastPageLayout = await registryLayout(page)
    expectStableRegistryLayout(firstPageLayout, lastPageLayout)
    await page.getByRole('button', { name: 'Previous' }).click()
    await expect(page).not.toHaveURL(/page=2/)
    await expect(rows).toHaveCount(25)
    await expect
      .poll(() => workspace.evaluate((element) => element.scrollTop))
      .toBe(scrollTop)
    expectStableRegistryLayout(firstPageLayout, await registryLayout(page))

    await page.goto(
      '/app/schedules?view=history&status=Due&quarter=Q3&year=2026&page=2',
    )
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)
    await page.reload()
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)
    await expect(rows).toHaveCount(1)

    await page.goto(
      '/app/schedules?view=history&status=Due&quarter=Q3&year=2026&page=99',
    )
    await expect(page).toHaveURL(/status=Due/)
    await expect(page).toHaveURL(/quarter=Q3/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/page=2/)

    const overflowStates: unknown[] = []
    for (const width of responsiveScheduleWidths) {
      await page.setViewportSize({ width, height: 667 })
      await page.goto(
        '/app/schedules?view=history&status=Due&quarter=Q3&year=2026&page=2',
      )
      await page
        .locator('.app-workspace')
        .evaluate((workspace) => (workspace.scrollTop = 0))
      const pagination = page.getByRole('navigation', {
        name: 'Schedules pagination',
      })

      if (width < 768) {
        const mobileSchedule = page
          .getByRole('heading', { name: 'FE-026' })
          .locator('xpath=../../..')
        await expect(mobileSchedule).toBeVisible()
      } else {
        await expect(page.getByRole('cell', { name: 'FE-026' })).toBeVisible()
      }

      const geometry = await horizontalOverflowGeometry(page)
      console.log(
        `[schedule-overflow:pagination:${width}] ${JSON.stringify(geometry)}`,
      )
      const excessWidth = geometry.documentScrollWidth - geometry.clientWidth
      if (excessWidth > 1) {
        overflowStates.push({ width, excessWidth, geometry })
      }
      await page.screenshot({
        path: testInfo.outputPath(`schedule-pagination-${width}.png`),
      })

      if (width < 768) {
        const mobileSchedule = page
          .getByRole('heading', { name: 'FE-026' })
          .locator('xpath=../../..')
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
      }
    }
    expect(overflowStates, JSON.stringify(overflowStates)).toEqual([])
  })

  test('browses URL-owned filters and restores a direct schedule detail', async ({
    page,
  }) => {
    await mockScheduleApi(page)
    await page.goto(
      '/app/schedules?view=history&status=Due&quarter=Q3&year=2026',
    )
    await expect(
      page.getByRole('heading', { name: 'Schedules', exact: true }),
    ).toBeVisible()
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
    await expect(page).toHaveURL(/view=history/)
  })

  test('keeps the re-laid-out sections and management deep links usable', async ({
    page,
  }, testInfo) => {
    await mockScheduleApi(page)
    let generationCalls = 0
    await page.route('**/api/v1/schedules/generate', async (route) => {
      if (route.request().method() !== 'POST') return route.fallback()
      generationCalls += 1
      const body = route.request().postDataJSON() as { year: number }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          year: body.year,
          eligibleAssets: 1,
          existingSchedules: 1,
          createdSchedules: 2,
          deferredSchedules: 0,
          cyclesRequiringGsdCoverageReview: 0,
        }),
      })
    })
    await page.route('**/api/v1/schedules/enrollment-deferrals**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          page: 1,
          pageSize: 10,
          total: 1,
          pendingCount: 1,
          reviewedCount: 0,
          items: [
            {
              assetId,
              assetCode: 'FE-001',
              department: 'GSD',
              assetCategory: 'fire-extinguisher',
              deferredPmCycle: '2026-08',
              nextEligiblePmCycle: '2026-11',
              reasonCode: 'BatchAssigned',
              reason:
                'The batch already had an assigned Supervisor or Inspector.',
              status: 'NeedsReview',
              deferredAt: '2026-08-15T04:00:00Z',
              reviewedAt: null,
              reviewedByUserId: null,
              reviewedByDisplayName: null,
              reviewNote: null,
            },
          ],
        }),
      }),
    )
    await page.route('**/api/v1/schedules/assignment-batches**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalScheduleCount: 1,
          batches: [
            {
              batchKey: 'GSD|fire-extinguisher|2026-08',
              department: 'GSD',
              assetCategory: 'fire-extinguisher',
              pmCycle: '2026-08',
              scheduleCount: 1,
              completedCount: 0,
              ongoingCount: 0,
              overdueCount: 0,
              supervisorState: 'Unassigned',
              supervisorUserId: null,
              supervisorDisplayName: null,
              inspectorState: 'Unassigned',
              inspectorUserId: null,
              inspectorDisplayName: null,
              assignmentLocked: false,
              supervisorOwnershipIsConsistent: true,
              representativeSchedule: schedule,
            },
          ],
        }),
      }),
    )

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto('/app/schedules')
    await expect(
      page.getByRole('heading', { name: 'Schedules', exact: true }),
    ).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'PM schedule batches' }),
    ).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'GSD · Fire extinguishers' }),
    ).toBeVisible()
    const viewBatchLink = page.getByRole('link', {
      name: 'View schedules in batch',
    })
    await expect(viewBatchLink).toHaveAttribute('href', /view=history/)
    await page.screenshot({
      path: 'test-results/web-ui-polish-schedules-planning-desktop.png',
      fullPage: true,
      animations: 'disabled',
    })
    await viewBatchLink.click()
    await expect(page).toHaveURL(/view=history/)
    await expect(page).toHaveURL(/year=2026/)
    await expect(page).toHaveURL(/department=GSD/)
    await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
    await expect(page).toHaveURL(/pmCycle=2026-08/)
    const sectionHeadings = await page
      .locator('main h1, main h2')
      .allTextContents()
    expect(sectionHeadings.slice(0, 3)).toEqual([
      'Find schedules',
      'Schedule summary',
      'Schedule records',
    ])
    await expect(
      page.getByText(
        'Overall schedule totals, independent of registry filters.',
      ),
    ).toBeVisible()
    await expect(
      page.getByRole('link', { name: 'Manage schedules' }),
    ).toHaveAttribute('href', '#schedule-management')
    await page.screenshot({
      path: 'test-results/web-ui-polish-schedules-desktop.png',
      fullPage: true,
    })

    await page.getByRole('link', { name: 'Manage schedules' }).click()
    await expect(page).toHaveURL(/#schedule-management$/)
    await expect(
      page.getByRole('heading', { name: 'Generate missing schedules' }),
    ).toBeVisible()
    await page
      .getByRole('button', { name: 'Generate missing schedules' })
      .click()
    await expect(
      page.getByRole('status').filter({ hasText: 'Year 2026' }),
    ).toBeVisible()
    expect(generationCalls).toBe(1)

    await page.goto('/app/schedules?view=history#schedule-enrollment-deferrals')
    await expect(page.locator('#schedule-enrollment-deferrals')).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Deferred asset enrollment' }),
    ).toBeVisible()

    const overflowStates: unknown[] = []
    for (const width of responsiveScheduleWidths) {
      await page.setViewportSize({ width, height: 667 })
      await page.goto('/app/schedules?view=history#schedule-management')
      await expect(
        page.getByRole('heading', { name: 'Schedule management' }),
      ).toBeVisible()
      await expect(page.getByLabel('Department')).toBeVisible()
      await expect(page.getByLabel('Specific asset')).toBeHidden()
      let geometry = await horizontalOverflowGeometry(page)
      console.log(
        `[schedule-overflow:management-collapsed:${width}] ${JSON.stringify(geometry)}`,
      )
      let excessWidth = geometry.documentScrollWidth - geometry.clientWidth
      if (excessWidth > 1) {
        overflowStates.push({
          state: 'management-collapsed',
          width,
          excessWidth,
          geometry,
        })
      }
      await page.screenshot({
        path: testInfo.outputPath(`schedule-management-collapsed-${width}.png`),
      })

      await page.getByText('More filters', { exact: true }).click()
      await expect(page.getByLabel('Department')).toBeVisible()
      await expect(page.getByLabel('Specific asset')).toBeVisible()
      geometry = await horizontalOverflowGeometry(page)
      console.log(
        `[schedule-overflow:management-expanded:${width}] ${JSON.stringify(geometry)}`,
      )
      excessWidth = geometry.documentScrollWidth - geometry.clientWidth
      if (excessWidth > 1) {
        overflowStates.push({
          state: 'management-expanded',
          width,
          excessWidth,
          geometry,
        })
      }
      await page.screenshot({
        path: testInfo.outputPath(`schedule-management-expanded-${width}.png`),
      })

      await page.getByText('More filters', { exact: true }).click()
      await expect(page.getByLabel('Department')).toBeVisible()
      await expect(page.getByLabel('Specific asset')).toBeHidden()
      if (width === 375) {
        await page.screenshot({
          path: testInfo.outputPath(
            'schedule-management-mobile-full-page-375.png',
          ),
          fullPage: true,
        })
      }
    }
    expect(overflowStates, JSON.stringify(overflowStates)).toEqual([])
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
    await page.setViewportSize({ width: 1280, height: 720 })
    await page.goto('/app/schedules/new')
    const assetPicker = page.getByRole('combobox', { name: 'Asset' })
    await assetPicker.fill('FE-001')
    const expectPickerToFit = async () => {
      const geometry = await page.evaluate(() => {
        const menu = document.querySelector('[role="listbox"]')?.parentElement
        const workspace = document.querySelector('.app-workspace')
        if (!menu || !workspace) return null
        const menuBounds = menu.getBoundingClientRect()
        const workspaceBounds = workspace.getBoundingClientRect()
        return {
          left: menuBounds.left,
          right: menuBounds.right,
          top: menuBounds.top,
          bottom: menuBounds.bottom,
          workspaceTop: workspaceBounds.top,
          workspaceBottom: Math.min(workspaceBounds.bottom, window.innerHeight),
          viewportWidth: window.innerWidth,
        }
      })
      expect(geometry).not.toBeNull()
      if (!geometry) throw new Error('Asset picker menu is not mounted')
      expect(geometry.left).toBeGreaterThanOrEqual(0)
      expect(geometry.right).toBeLessThanOrEqual(geometry.viewportWidth)
      expect(geometry.top).toBeGreaterThanOrEqual(geometry.workspaceTop)
      expect(geometry.bottom).toBeLessThanOrEqual(geometry.workspaceBottom)
    }
    await expect(page.getByRole('option', { name: /FE-001/ })).toBeVisible()
    await expectPickerToFit()
    await page.setViewportSize({ width: 375, height: 667 })
    await expectPickerToFit()
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
