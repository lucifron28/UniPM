import { expect, test, type Page } from '@playwright/test'

const inspectionId = '11111111-1111-4111-8111-111111111111'
const scheduleId = '22222222-2222-4222-8222-222222222222'
const assetId = '33333333-3333-4333-8333-333333333333'
const inspectionRegistryRoute = /\/api\/v1\/inspections\/registry(?:\?.*)?$/
const inspectionRegistryOptionsRoute =
  /\/api\/v1\/inspections\/registry\/options(?:\?.*)?$/
const inspectionRegistrySummaryRoute =
  /\/api\/v1\/inspections\/registry\/summary(?:\?.*)?$/
const session = {
  accessToken: 'fictional-inspection-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
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
    status: 'Active',
  },
}
const inspection = {
  id: inspectionId,
  scheduleId,
  assetId,
  inspectorUserId: session.user.id,
  dateInspected: '2026-07-22T01:00:00Z',
  isOperational: false,
  hasPhotoEvidence: false,
  remarks: 'Low pressure recorded during inspection.',
  actionsRecommendations: 'Arrange a pressure check.',
  externalPmNumber: null,
  wmsReferralRevision: 0,
  correctiveFollowUpStatus: 'CorrectiveFollowUpPending',
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
const registryInspection = {
  id: inspection.id,
  scheduleId: inspection.scheduleId,
  assetId: inspection.assetId,
  dateInspected: inspection.dateInspected,
  isOperational: inspection.isOperational,
  remarks: inspection.remarks,
  actionsRecommendations: inspection.actionsRecommendations,
  createdAt: inspection.createdAt,
  updatedAt: inspection.updatedAt,
  dateAccomplished: null,
  waterReplaceCarbonFilter: null,
  waterReplaceSedimentFilter: null,
  waterCheckUvLight: null,
  externalPmNumber: inspection.externalPmNumber,
  wmsReferralRevision: inspection.wmsReferralRevision,
  correctiveFollowUpStatus: inspection.correctiveFollowUpStatus,
  hasPhotoEvidence: inspection.hasPhotoEvidence,
  asset: {
    id: assetId,
    assetCode: asset.assetCode,
    assetCategory: asset.assetCategory,
    department: asset.department,
    building: asset.building,
    location: asset.location,
  },
  schedule: { id: scheduleId, pmCycle: '2026-08' },
}
const registryOptions = {
  categories: [
    {
      code: 'fire-extinguisher',
      displayName: 'Fire extinguishers',
      scheduledMonths: [2, 5, 8, 11],
    },
  ],
  quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
  wmsReferralStatuses: [
    'NoReferralRequired',
    'CorrectiveFollowUpPending',
    'ReferredToWms',
  ],
  departments: ['GSD'],
  years: ['2026'],
}
const registrySummary = {
  totalCount: 1,
  operationalCount: 0,
  nonOperationalCount: 1,
  noReferralCount: 0,
  pendingFollowUpCount: 1,
  referredCount: 0,
}

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
  await page.route('**/api/v1/assets/registry**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [asset],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      }),
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
  await page.route(inspectionRegistryRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [registryInspection],
        page: 1,
        pageSize: 25,
        totalCount: 1,
        overallCount: 1,
      }),
    }),
  )
  await page.route(inspectionRegistryOptionsRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(registryOptions),
    }),
  )
  await page.route(inspectionRegistrySummaryRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(registrySummary),
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
  test('groups advanced filters, preserves their indicators, and contains zoom across breakpoints', async ({
    page,
  }) => {
    await mockInspectionApi(page)
    await page.route(inspectionRegistryRoute, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              ...registryInspection,
              externalPmNumber: 'WMS-PM-007',
              correctiveFollowUpStatus: 'ReferredToWms',
            },
          ],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          overallCount: 1,
        }),
      }),
    )
    await page.setViewportSize({ width: 1280, height: 900 })
    await page.goto(
      '/app/inspections?assetId=' +
        `${assetId}&department=GSD&year=2026&quarter=Q3&pmCycle=2026-08&dateFrom=2026-07-01T00%3A00%3A00.000Z&dateTo=2026-07-31T23%3A59%3A59.000Z`,
    )

    const registry = page.locator(
      'section[aria-labelledby="inspections-title"]',
    )
    await expect(
      page.locator('.app-workspace').getByRole('heading', {
        name: 'Inspections',
        exact: true,
      }),
    ).toBeVisible()
    await expect(
      registry.getByRole('heading', { name: 'Find inspections' }),
    ).toBeVisible()
    await expect(
      registry.getByRole('heading', { name: 'Inspection summary' }),
    ).toBeVisible()
    await expect(
      registry.getByRole('heading', { name: 'Inspection records' }),
    ).toBeVisible()
    expect(await registry.locator('h1, h2').allTextContents()).toEqual([
      'Inspections',
      'Find inspections',
      'Inspection summary',
      'Inspection records',
    ])
    await expect(page.getByLabel('Search inspections')).toBeVisible()
    await expect(page.getByLabel('Asset category')).toBeVisible()
    await expect(
      page.getByLabel('PM calendar year', { exact: true }),
    ).toBeVisible()
    await expect(page.getByLabel('Recorded operational result')).toBeVisible()
    await expect(page.getByLabel('Corrective follow-up status')).toBeVisible()
    await expect(page.getByText('WMS-PM-007').first()).toBeVisible()
    const filterLayout = await registry.locator('form').evaluate((form) => {
      const columns = getComputedStyle(form).gridTemplateColumns
      const repeatedColumns = columns.match(/^repeat\((\d+),/)
      return {
        className: form.className,
        columns,
        count: repeatedColumns
          ? Number(repeatedColumns[1])
          : columns.trim().split(/\s+/).length,
        viewport: innerWidth,
        md: matchMedia('(min-width: 48rem)').matches,
        xl: matchMedia('(min-width: 80rem)').matches,
        twoXl: matchMedia('(min-width: 96rem)').matches,
      }
    })
    expect(filterLayout.count).toBe(3)
    await page.mouse.move(1270, 5)
    await page.screenshot({
      path: 'test-results/web-ui-polish-inspections-desktop.png',
      animations: 'disabled',
      fullPage: true,
    })

    await page.getByText('More filters').click()
    await expect(page.getByLabel('Department', { exact: true })).toBeVisible()
    await expect(
      page.getByLabel('Specific asset', { exact: true }),
    ).toBeHidden()
    for (const filter of [
      'Specific asset',
      'Department',
      'PM calendar quarter',
      'Exact PM month',
      'Inspected from',
      'Inspected to',
    ]) {
      await expect(
        page.getByRole('button', { name: `Remove ${filter} filter` }),
      ).toBeVisible()
    }

    const zoomGroup = page.getByRole('group', {
      name: 'Inspection records zoom controls',
    })
    const increase = zoomGroup.getByRole('button', {
      name: 'Increase Inspection records zoom',
    })
    await increase.click()
    await increase.click()
    await increase.click()
    await expect(zoomGroup).toContainText('130%')
    await expect(increase).toBeDisabled()

    await page.getByRole('link', { name: 'View details' }).click()
    await expect(page).toHaveURL(new RegExp(`/app/inspections/${inspectionId}`))
    await page.getByRole('link', { name: 'Back to inspections' }).click()
    await expect(page).toHaveURL(/department=GSD/)
    await expect(page).toHaveURL(/pmCycle=2026-08/)
    await expect(
      page.getByRole('button', { name: 'Remove Exact PM month filter' }),
    ).toBeVisible()

    await page.setViewportSize({ width: 375, height: 812 })
    await expect(
      page.getByRole('link', { name: 'FE-001' }).first(),
    ).toBeVisible()
    await expect
      .poll(() =>
        page.evaluate(
          () =>
            document.documentElement.scrollWidth -
            document.documentElement.clientWidth,
        ),
      )
      .toBeLessThanOrEqual(1)
    await zoomGroup
      .getByRole('button', {
        name: 'Reset Inspection records zoom to 100 percent',
      })
      .click()
    await page.screenshot({
      path: 'test-results/web-ui-polish-inspections-mobile.png',
      fullPage: true,
    })
  })

  test('keeps window scroll position on both pagination directions', async ({
    page,
  }) => {
    await mockInspectionApi(page)
    const records = Array.from({ length: 26 }, (_, index) => ({
      ...registryInspection,
      id: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
      remarks: `Finding ${index + 1}`,
    }))
    await page.route(inspectionRegistryRoute, (route) => {
      const params = new URL(route.request().url()).searchParams
      const pageNumber = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      const start = (pageNumber - 1) * pageSize
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: records.slice(start, start + pageSize),
          page: pageNumber,
          pageSize,
          totalCount: records.length,
          overallCount: records.length,
        }),
      })
    })
    await page.route(inspectionRegistrySummaryRoute, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalCount: records.length,
          operationalCount: records.filter((item) => item.isOperational).length,
          nonOperationalCount: records.filter((item) => !item.isOperational)
            .length,
          noReferralCount: 0,
          pendingFollowUpCount: records.length,
          referredCount: 0,
        }),
      }),
    )
    await page.setViewportSize({ width: 1280, height: 600 })
    await page.goto('/app/inspections?isOperational=false')
    const rows = page.getByRole('table').locator('tbody tr')
    await expect(rows).toHaveCount(25)
    const next = page.getByRole('button', { name: 'Next' })
    const workspace = page.locator('.app-workspace')
    await next.scrollIntoViewIfNeeded()
    const scrollTop = await workspace.evaluate((element) => element.scrollTop)
    expect(scrollTop).toBeGreaterThan(0)
    const firstPageLayout = await registryLayout(page)
    await next.click()
    await expect(page).toHaveURL(/isOperational=false/)
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByRole('cell', { name: 'Finding 26' })).toBeVisible()
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
      .getByText('Finding 26', { exact: true })
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
      page.getByRole('heading', { name: 'Inspections', exact: true }),
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
    await expect(
      page.getByRole('heading', { name: 'Recommendation', exact: true }),
    ).toBeVisible()
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

  for (const viewport of [
    { name: 'desktop', width: 1440, height: 900 },
    { name: 'mobile', width: 375, height: 667 },
  ]) {
    test(`keeps photo preview keyboard-accessible on ${viewport.name}`, async ({
      page,
    }) => {
      await page.setViewportSize({
        width: viewport.width,
        height: viewport.height,
      })
      await mockInspectionApi(page)
      const photoDataUrl = await page.evaluate(() => {
        const canvas = document.createElement('canvas')
        canvas.width = 2
        canvas.height = 2
        const context = canvas.getContext('2d')
        if (!context) throw new Error('Canvas context is unavailable.')
        context.fillStyle = '#8f1d1d'
        context.fillRect(0, 0, canvas.width, canvas.height)
        return canvas.toDataURL('image/jpeg')
      })
      const validJpeg = Buffer.from(photoDataUrl.split(',')[1]!, 'base64')
      await page.route(`**/api/v1/inspections/${inspectionId}`, (route) =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ ...inspection, hasPhotoEvidence: true }),
        }),
      )
      await page.route(`**/api/v1/inspections/${inspectionId}/photo`, (route) =>
        route.fulfill({
          status: 200,
          contentType: 'image/jpeg',
          body: validJpeg,
        }),
      )

      await page.goto(`/app/inspections/${inspectionId}`)
      const thumbnail = page.getByRole('button', {
        name: 'Open inspection photo preview',
      })
      await expect(thumbnail).toBeVisible()
      const thumbnailImage = page.getByAltText('Inspection photo thumbnail')
      await expect
        .poll(() => thumbnailImage.evaluate((image) => image.naturalWidth))
        .toBeGreaterThan(0)
      await thumbnail.focus()
      await page.keyboard.press('Enter')

      const dialog = page.getByRole('dialog', {
        name: 'Inspection photo evidence',
      })
      const closeButton = dialog.getByRole('button', {
        name: 'Close photo preview',
      })
      await expect(dialog).toBeVisible()
      const previewImage = page.getByAltText(
        'Photo captured for this inspection',
      )
      await expect
        .poll(() => previewImage.evaluate((image) => image.naturalWidth))
        .toBeGreaterThan(0)
      await expect(closeButton).toBeFocused()
      await expect
        .poll(() => dialog.evaluate((element) => element.matches(':modal')))
        .toBe(true)
      await page.keyboard.press('Tab')
      await expect(closeButton).toBeFocused()
      await page.keyboard.press('Shift+Tab')
      await expect(closeButton).toBeFocused()
      await page.keyboard.press('Escape')
      await expect(dialog).toHaveCount(0)
      await expect(thumbnail).toBeFocused()
    })
  }
})
