import { expect, test, type Page } from '@playwright/test'

const session = {
  accessToken: 'synthetic-shell-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '22222222-2222-4222-8222-222222222222',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
  },
}

async function mockSession(page: Page) {
  await page.route('**/api/v1/**', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Service unavailable', status: 503 }),
    }),
  )
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
}

test('desktop shell keeps its header and rail still while the workspace scrolls', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 720 })
  await mockSession(page)
  await page.route('**/api/v1/pm-period-dashboard**', async (route) => {
    const url = new URL(route.request().url())
    const body = url.pathname.endsWith('/cycles')
      ? [
          {
            assetCategory: 'fire-extinguisher',
            year: 2026,
            cycles: [{ pmCycle: '2026-08', scheduled: 4 }],
          },
        ]
      : {
          pmCycle: '2026-08',
          assetCategory: 'fire-extinguisher',
          department: null,
          deadline: '2026-08-31T15:59:59.999Z',
          periodState: 'Closed',
          complianceMeasurable: true,
          inspectionResultsAvailable: true,
          scheduled: 4,
          inspected: 2,
          completedOnTime: 1,
          completedLate: 1,
          notCompleted: 2,
          remaining: 0,
          operational: 1,
          nonOperational: 1,
          onTimeCompliancePercent: 25,
          progressPercent: 50,
          batches: [],
          assets: [],
        }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(body),
    })
  })
  await page.route('**/api/v1/schedules/enrollment-deferrals**', (route) =>
    route.fulfill({
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
    }),
  )
  await page.goto(
    '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08',
  )

  const workspace = page.locator('.app-workspace')
  const sidebar = page.locator('.app-sidebar')
  const heading = workspace.getByRole('heading', {
    name: 'Preventive Maintenance Dashboard',
  })
  await expect(heading).toBeVisible()
  await expect(
    page.getByText('Preventive Maintenance Portal', { exact: true }),
  ).toBeVisible()
  await expect(sidebar.getByText('UniPM', { exact: true }).last()).toBeVisible()
  await expect(
    sidebar.getByText('Institutional operations').last(),
  ).toBeVisible()
  await expect(sidebar).toHaveCSS('width', '248px')
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible()
  await expect(heading).toHaveCount(1)
  await expect(
    workspace.getByRole('heading', {
      name: 'Preventive maintenance overview',
    }),
  ).toBeVisible()
  await expect(
    workspace.getByText(
      /Supporting rates: inspection progress 50%.*completed within the scheduled month 25%/,
    ),
  ).toBeVisible()
  await expect(page.getByRole('link', { name: 'Assets' })).not.toHaveAttribute(
    'title',
  )
  await expect(page.getByRole('link', { name: 'Ask a question' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'PM Analytics' })).toHaveCount(0)
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-expanded-default-desktop.png',
  })

  await page.getByLabel('Account menu').click()
  await expect(sidebar.getByText('fictional.gsd@example.test')).toBeVisible()
  await expect(sidebar.getByRole('button', { name: 'Sign out' })).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-account-menu-expanded.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar.locator('details')).not.toHaveAttribute('open', '')

  const before = await page.evaluate(() => ({
    headerTop: document
      .querySelector('.app-page-header')!
      .getBoundingClientRect().top,
    sidebarTop: document.querySelector('.app-sidebar')!.getBoundingClientRect()
      .top,
  }))
  await workspace.evaluate((element) => {
    const spacer = document.createElement('div')
    spacer.style.height = '1400px'
    element.append(spacer)
    element.scrollTop = 400
  })
  const after = await page.evaluate(() => ({
    headerTop: document
      .querySelector('.app-page-header')!
      .getBoundingClientRect().top,
    sidebarTop: document.querySelector('.app-sidebar')!.getBoundingClientRect()
      .top,
    workspaceScroll: document.querySelector('.app-workspace')!.scrollTop,
    documentScroll: document.scrollingElement!.scrollTop,
    documentHeight: document.documentElement.scrollHeight,
    viewportHeight: window.innerHeight,
  }))
  expect(after.workspaceScroll).toBeGreaterThan(200)
  expect(after.headerTop).toBeCloseTo(before.headerTop, 0)
  expect(after.sidebarTop).toBeCloseTo(before.sidebarTop, 0)
  expect(after.documentScroll).toBe(0)
  expect(after.documentHeight).toBeLessThanOrEqual(after.viewportHeight + 1)
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
  expect(
    await workspace.evaluate((element) => getComputedStyle(element).overflowY),
  ).toBe('auto')

  await page.getByRole('button', { name: 'Collapse navigation' }).click()
  await expect(
    page.getByRole('button', { name: 'Expand navigation' }),
  ).toBeVisible()
  await expect(sidebar).toHaveCSS('width', '76px')
  expect(
    await page.evaluate(() => localStorage.getItem('unipm.sidebar.collapsed')),
  ).toBe('true')
  await page.reload()
  await expect(
    page.getByRole('button', { name: 'Expand navigation' }),
  ).toBeVisible()
  await expect(sidebar).toHaveCSS('width', '76px')
  const collapsedAlignment = await page.evaluate(() => {
    const sidebar = document.querySelector('.app-sidebar')!
    const nav = sidebar.querySelector('nav')!
    return [
      sidebar.querySelector('img[alt="UniPM"]')!,
      sidebar.querySelector('[aria-label="Expand navigation"]')!,
      ...nav.querySelectorAll('a'),
      sidebar.querySelector('details summary')!,
    ].map((element) => {
      const bounds = element.getBoundingClientRect()
      return {
        centerX: bounds.left + bounds.width / 2,
        width: bounds.width,
        height: bounds.height,
      }
    })
  })
  expect(
    Math.max(...collapsedAlignment.map((box) => box.centerX)) -
      Math.min(...collapsedAlignment.map((box) => box.centerX)),
  ).toBeLessThan(1)
  expect(
    collapsedAlignment
      .slice(2, -1)
      .every((box) => box.width === 44 && box.height === 44),
  ).toBe(true)

  await page.getByRole('button', { name: 'Expand navigation' }).click()
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible()
  await expect(sidebar).toHaveCSS('width', '248px')
  await expect(
    sidebar.getByText('Institutional operations').last(),
  ).toBeVisible()
  await workspace.evaluate((element) => {
    element.scrollTop = 0
  })
  await expect
    .poll(() => workspace.evaluate((element) => element.scrollTop))
    .toBe(0)
  await expect(sidebar.locator('[aria-current="page"]')).toHaveClass(
    'sidebar-nav-link flex min-h-10 items-center gap-3 rounded-lg bg-[var(--primary-active)] px-4 py-3 text-sm font-semibold text-white shadow-sm',
  )
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-expanded-desktop.png',
  })
  expect(
    await page.evaluate(() => localStorage.getItem('unipm.sidebar.collapsed')),
  ).toBe('false')
  await page.reload()
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible()

  await page.getByLabel('Account menu').click()
  await expect(page.getByText('fictional.gsd@example.test')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-account-menu-expanded.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar.locator('details')).not.toHaveAttribute('open', '')
  await page.setViewportSize({ width: 1024, height: 768 })
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
  await page.screenshot({
    path: 'test-results/web-ui-polish-small-laptop.png',
  })
})

test('dashboard questions open the V3 report builder and legacy links redirect', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  await mockSession(page)
  await page.route('**/api/v1/pm-period-dashboard/cycles**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: '[]',
    }),
  )
  const dashboardSearch = {
    assetCategory: 'fire-extinguisher',
    year: 2026,
    pmCycle: '2026-08',
    department: 'GSD',
    condition: 'NonOperational',
    timeliness: 'OnTime',
    search: 'FE-001',
  }
  const dashboardParams = new URLSearchParams({
    assetCategory: dashboardSearch.assetCategory,
    year: String(dashboardSearch.year),
    pmCycle: dashboardSearch.pmCycle,
    department: dashboardSearch.department,
    condition: dashboardSearch.condition,
    timeliness: dashboardSearch.timeliness,
    search: dashboardSearch.search,
  })
  await page.goto('/app/dashboard?' + dashboardParams.toString())

  await page.getByRole('link', { name: 'Ask a question' }).click()
  await expect(page).toHaveURL(/\/app\/analytics(?:\?.*)?$/)
  await expect(
    page.getByRole('heading', { name: 'Natural Language Analytics' }),
  ).toBeVisible()
  let analyticsUrl = new URL(page.url())
  expect(analyticsUrl.pathname).toBe('/app/analytics')
  expect(
    JSON.parse(analyticsUrl.searchParams.get('returnContext') ?? 'null'),
  ).toEqual({ kind: 'dashboard', search: dashboardSearch })
  await expect(
    page.getByRole('heading', { name: 'Ask about PM results' }),
  ).toHaveCount(0)

  const legacyPage = await page.context().newPage()
  await mockSession(legacyPage)
  await legacyPage.goto(
    '/app/dashboard?view=question&' + dashboardParams.toString(),
  )
  await expect(legacyPage).toHaveURL(/\/app\/analytics(?:\?.*)?$/)
  await expect(
    legacyPage.getByRole('heading', { name: 'Natural Language Analytics' }),
  ).toBeVisible()
  analyticsUrl = new URL(legacyPage.url())
  expect(analyticsUrl.pathname).toBe('/app/analytics')
  const legacyReturnLink = new URL(
    (await legacyPage
      .getByRole('link', { name: 'Back to PM dashboard' })
      .getAttribute('href'))!,
    'http://localhost',
  )
  expect(
    JSON.parse(analyticsUrl.searchParams.get('returnContext') ?? 'null'),
  ).toEqual({ kind: 'dashboard', search: dashboardSearch })
  expect(legacyReturnLink.pathname).toBe('/app/dashboard')
  for (const [key, value] of dashboardParams) {
    expect(legacyReturnLink.searchParams.get(key)).toBe(value)
  }
  await legacyPage.close()
})

test('active navigation styling follows browser history', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 })
  await mockSession(page)
  await page.goto('/app/dashboard')

  const assetsLink = page.getByRole('link', { name: 'Assets' })
  const dashboardLink = page.getByRole('link', { name: 'Dashboard' })
  await expect(dashboardLink).toHaveAttribute('aria-current', 'page')
  await expect(dashboardLink).toHaveClass(/bg-\[var\(--primary-active\)\]/)
  await assetsLink.click()
  await expect(assetsLink).toHaveAttribute('aria-current', 'page')
  await expect(assetsLink).toHaveClass(/bg-\[var\(--primary-active\)\]/)

  await page.goBack()
  await expect(dashboardLink).toHaveAttribute('aria-current', 'page')
  await expect(dashboardLink).toHaveClass(/bg-\[var\(--primary-active\)\]/)
  await page.goForward()
  await expect(assetsLink).toHaveAttribute('aria-current', 'page')
  await expect(assetsLink).toHaveClass(/bg-\[var\(--primary-active\)\]/)

  await page.emulateMedia({ reducedMotion: 'reduce' })
  const reducedMotionDuration = await assetsLink.evaluate((element) =>
    Number.parseFloat(getComputedStyle(element).transitionDuration),
  )
  expect(reducedMotionDuration).toBeLessThan(0.001)
})

test('mobile navigation opens, closes, and leaves the page usable', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 667 })
  await mockSession(page)
  await page.goto('/app/dashboard')
  const sidebar = page.locator('.app-sidebar')
  const open = page.getByRole('button', { name: 'Open navigation' })
  await expect(open).toBeVisible()
  await expect(sidebar).toBeHidden()
  await open.click()
  await expect(sidebar).toBeVisible()
  await expect(sidebar.getByRole('link', { name: 'Schedules' })).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-mobile-navigation.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar).toBeHidden()
  await open.click()
  await sidebar.getByRole('link', { name: 'Assets' }).click()
  await expect(page).toHaveURL(/\/app\/assets/)
  await expect(sidebar).toBeHidden()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
})

test('quick search resets its active destination when results are filtered', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  await mockSession(page)
  await page.route('**/api/v1/pm-period-dashboard**', async (route) => {
    const url = new URL(route.request().url())
    const body = url.pathname.endsWith('/cycles')
      ? [
          {
            assetCategory: 'fire-extinguisher',
            year: 2026,
            cycles: [{ pmCycle: '2026-08', scheduled: 4 }],
          },
        ]
      : {
          pmCycle: '2026-08',
          assetCategory: 'fire-extinguisher',
          department: null,
          deadline: '2026-08-31T15:59:59.999Z',
          periodState: 'Closed',
          complianceMeasurable: true,
          inspectionResultsAvailable: true,
          scheduled: 4,
          inspected: 2,
          completedOnTime: 1,
          completedLate: 1,
          notCompleted: 2,
          remaining: 0,
          operational: 1,
          nonOperational: 1,
          onTimeCompliancePercent: 25,
          progressPercent: 50,
          batches: [],
          assets: [],
        }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(body),
    })
  })
  await page.route('**/api/v1/schedules/enrollment-deferrals**', (route) =>
    route.fulfill({
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
    }),
  )
  await page.goto(
    '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08',
  )
  await expect(
    page.getByRole('heading', { name: 'Preventive Maintenance Dashboard' }),
  ).toBeVisible()
  await expect(page.getByText('Checking deferred enrollment…')).toHaveCount(0)

  await page.getByRole('button', { name: 'Quick search' }).click()
  const search = page.getByRole('textbox', { name: 'Search pages and tasks' })
  await expect(search).toBeFocused()
  await page.keyboard.press('ArrowDown')
  await page.keyboard.press('ArrowDown')
  await search.fill('supervisor')

  const destination = page.getByRole('option', { name: /Assign supervisor/ })
  await expect(destination).toHaveAttribute('aria-selected', 'true')
  await expect(search).toHaveAttribute(
    'aria-activedescendant',
    'quick-navigation-option-assign-supervisor',
  )
  await page.screenshot({
    path: 'test-results/web-ui-polish-quick-search-desktop.png',
    animations: 'disabled',
  })

  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/app\/schedules\?view=assignments/)
  await expect(page).toHaveURL(/assignmentFilter=needs-supervisor/)
  await expect(
    page.getByRole('heading', { name: 'Supervisor Assignments' }),
  ).toBeVisible()
})

for (const width of [1440, 1536]) {
  test(`assignment attention keeps scope and print layout at ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 })
    await mockSession(page)
    await page.route('**/api/v1/pm-period-dashboard**', (route) => {
      const url = new URL(route.request().url())
      const body = url.pathname.endsWith('/cycles')
        ? [
            {
              assetCategory: 'fire-extinguisher',
              year: 2026,
              cycles: [{ pmCycle: '2026-08', scheduled: 2 }],
            },
          ]
        : {
            pmCycle: '2026-08',
            assetCategory: 'fire-extinguisher',
            department: 'CCMS',
            deadline: '2026-08-31T15:59:59.999Z',
            periodState: 'Active',
            complianceMeasurable: false,
            inspectionResultsAvailable: false,
            scheduled: 2,
            inspected: 0,
            completedOnTime: 0,
            completedLate: 0,
            notCompleted: 0,
            remaining: 2,
            operational: 0,
            nonOperational: 0,
            onTimeCompliancePercent: null,
            progressPercent: 0,
            batches: [],
            assets: [],
          }
      return route.fulfill({ json: body })
    })
    await page.route('**/api/v1/schedules/registry/options**', (route) =>
      route.fulfill({
        json: {
          categories: [
            {
              code: 'fire-extinguisher',
              displayName: 'Fire extinguishers',
              scheduledMonths: [8],
            },
          ],
          quarters: ['Q3'],
          departments: ['CCMS'],
          years: ['2026'],
        },
      }),
    )
    const asset = {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      assetCode: 'FE-001',
      assetCategory: 'fire-extinguisher',
      building: 'Main',
      department: 'CCMS',
      location: 'Main',
      status: 'Active',
      qrCodeValue: null,
    }
    const schedule = {
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      assetId: asset.id,
      scheduleDate: '2026-08-31T15:59:59Z',
      pmCycle: '2026-08',
      periodType: 'Custom',
      status: 'Due',
      quarter: null,
      semester: null,
      year: 2026,
      academicYear: null,
      assignedToUserId: null,
      assignedSupervisorUserId: null,
      completedAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      asset,
    }
    const batch = {
      batchKey: 'CCMS|fire-extinguisher|2026-08',
      department: 'CCMS',
      assetCategory: 'fire-extinguisher',
      pmCycle: '2026-08',
      scheduleCount: 2,
      unfinishedScheduleCount: 2,
      activeUnfinishedScheduleCount: 2,
      completedCount: 0,
      ongoingCount: 0,
      overdueCount: 0,
      cancelledCount: 0,
      activeScheduleCount: 2,
      supervisorState: 'Unassigned',
      supervisorUserId: null,
      supervisorDisplayName: null,
      inspectorState: 'Unassigned',
      inspectorUserId: null,
      inspectorDisplayName: null,
      assignmentLocked: false,
      supervisorOwnershipIsConsistent: false,
      buildings: ['Main'],
      representativeSchedule: schedule,
      attention: { state: 'Urgent', priority: 2, daysUntilMonthEnd: 5 },
    }
    await page.route('**/api/v1/schedules/assignment-batches**', (route) =>
      route.fulfill({
        json: {
          totalScheduleCount: 2,
          totalBatchCount: 1,
          page: 1,
          pageSize: 1,
          summary: {
            notStartedBatchCount: 1,
            inProgressBatchCount: 0,
            ongoingScheduleCount: 0,
            overdueScheduleCount: 0,
            pendingAssignmentBatchCount: 1,
            urgentAssignmentBatchCount: 1,
            attentionDate: '2026-08-26',
            attentionRole: 'Supervisor',
            attentionYear: 2026,
            attentionAssetCategory: 'fire-extinguisher',
            attentionDepartment: 'CCMS',
            attentionPmCycle: '2026-08',
          },
          buildingOptions: ['Main'],
          batches: [batch],
        },
      }),
    )
    await page.goto(
      '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08&department=CCMS',
    )
    const action = page.getByRole('link', { name: /Review assignments/ })
    await expect(
      page.getByText('1 batch needs a Supervisor. 1 is urgent.'),
    ).toBeVisible()
    await expect(action).toBeVisible()
    await action.focus()
    await expect(action).toBeFocused()
    await page.emulateMedia({ media: 'print' })
    await expect(action).toBeHidden()
    await expect(
      page.getByText('Scheduled', { exact: true }).first(),
    ).toBeVisible()
    await page.emulateMedia({ media: 'screen' })
    await action.focus()
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/app\/schedules/)
    const url = new URL(page.url())
    expect(url.searchParams.get('assetCategory')).toBe('fire-extinguisher')
    expect(url.searchParams.get('pmCycle')).toBe('2026-08')
    expect(url.searchParams.get('department')).toBe('CCMS')
    await expect(
      page.getByText(
        /Supervisor needed · Assignment urgent · 5 days until month-end/,
      ),
    ).toBeVisible()
    await page.goBack()
    await expect(action).toBeVisible()
    let unavailable = false
    await page.route('**/api/v1/schedules/assignment-batches**', (route) =>
      unavailable
        ? route.fulfill({ status: 503 })
        : route.fulfill({
            json: {
              totalScheduleCount: 0,
              summary: {
                notStartedBatchCount: 0,
                inProgressBatchCount: 0,
                ongoingScheduleCount: 0,
                overdueScheduleCount: 0,
                pendingAssignmentBatchCount: 0,
                urgentAssignmentBatchCount: 0,
              },
              batches: [],
            },
          }),
    )
    await page.reload()
    await expect(
      page.getByText('Scheduled', { exact: true }).first(),
    ).toBeVisible()
    await expect(action).toHaveCount(0)
    unavailable = true
    await page.reload()
    await expect(
      page.getByText('Assignment attention is unavailable.', { exact: false }),
    ).toBeVisible()
    await expect(action).toHaveCount(0)
    let roles = ['Supervisor']
    await page.route('**/api/v1/auth/me', (route) =>
      route.fulfill({ json: { ...session.user, roles } }),
    )
    await page.route('**/api/v1/auth/refresh', (route) =>
      route.fulfill({ json: { ...session, user: { ...session.user, roles } } }),
    )
    await page.route('**/api/v1/schedules/assignment-batches**', (route) =>
      route.fulfill({
        json: {
          totalScheduleCount: 2,
          summary: {
            notStartedBatchCount: 1,
            inProgressBatchCount: 0,
            ongoingScheduleCount: 0,
            overdueScheduleCount: 0,
            pendingAssignmentBatchCount: 1,
            urgentAssignmentBatchCount: 1,
          },
          batches: [
            {
              ...batch,
              supervisorState: 'Assigned',
              supervisorUserId: session.user.id,
              supervisorOwnershipIsConsistent: true,
            },
          ],
        },
      }),
    )
    await page.reload()
    await expect(
      page.getByText('1 batch needs an Inspector. 1 is urgent.'),
    ).toBeVisible()
    await action.click()
    await expect(
      page.getByRole('heading', { name: 'Inspector Assignments' }),
    ).toBeVisible()
    await expect(
      page.getByText(/Inspector needed · Assignment urgent/),
    ).toBeVisible()
    roles = ['GSD', 'Supervisor']
    await page.goto(
      '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08&department=CCMS',
    )
    await page.reload()
    await expect(
      page.getByText('1 batch needs a Supervisor. 1 is urgent.'),
    ).toBeVisible()
    await action.click()
    await expect(
      page.getByRole('heading', { name: 'Supervisor Assignments' }),
    ).toBeVisible()
  })
}
