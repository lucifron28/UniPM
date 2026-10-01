import { expect, test, type Page } from '@playwright/test'

const assetId = '11111111-1111-4111-8111-111111111111'
const inspectionId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '33333333-3333-4333-8333-333333333333'
const formId = '44444444-4444-4444-8444-444444444444'
const userId = '55555555-5555-4555-8555-555555555555'
const pmCycle = '2026-08'

const session = {
  accessToken: 'fictional-ux-context-token',
  expiresAtUtc: '2030-01-01T12:00:00Z',
  user: {
    id: userId,
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
  createdAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-28T03:00:00Z',
  hasVerificationLocation: false,
}

const schedule = {
  id: scheduleId,
  assetId,
  scheduleDate: '2026-08-31T15:59:59.9999999Z',
  pmCycle,
  periodType: 'Quarter',
  status: 'Completed',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: '2026-2027',
  assignedToUserId: userId,
  assignedSupervisorUserId: null,
  completedAt: '2026-08-28T03:00:00Z',
  createdAt: '2026-08-01T00:00:00Z',
  updatedAt: '2026-08-28T03:00:00Z',
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
  inspectorUserId: userId,
  dateInspected: '2026-08-28T02:00:00Z',
  isOperational: false,
  remarks: 'Pressure is low.',
  actionsRecommendations: 'Arrange a pressure check.',
  createdAt: '2026-08-28T02:00:00Z',
  updatedAt: '2026-08-28T03:00:00Z',
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

const inspectionRow = {
  ...inspection,
  startedAt: '2026-08-28T02:00:00Z',
  completedAt: '2026-08-28T03:00:00Z',
  assetCode: 'FE-001',
  location: 'Lobby',
  skilledWorkerIdentity: 'Synthetic Inspector',
}

const form = {
  id: formId,
  fileNumber: 'PMF-2026-0001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  pmCycle,
  periodType: 'Quarter',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: '2026-2027',
  status: 'Submitted',
  createdByUserId: userId,
  submittedByUserId: userId,
  submittedAt: '2026-08-30T01:00:00Z',
  fieldWorkCompletedAt: '2026-08-28T03:00:00Z',
  createdAt: '2026-08-28T00:00:00Z',
  updatedAt: '2026-08-30T01:00:00Z',
  inspections: [inspectionRow],
}

const batch = {
  department: 'GSD',
  assetCategory: 'fire-extinguisher',
  pmCycle,
  scheduled: 1,
  inspected: 1,
  completedOnTime: 1,
  onTimeCompliancePercent: 100,
  completedLate: 0,
  notCompleted: 0,
  remaining: 0,
  formId,
  formStatus: 'Submitted',
  fileNumber: 'PMF-2026-0001',
  fieldWorkCompletedAt: '2026-08-28T03:00:00Z',
  submittedAt: '2026-08-30T01:00:00Z',
  isAcknowledged: false,
  acknowledgedAt: null,
}

const dashboardAsset = {
  scheduleId,
  assetId,
  inspectionId,
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  location: 'Lobby',
  department: 'GSD',
  pmCycle,
  scheduleDate: '2026-08-31T15:59:59.9999999Z',
  deadline: '2026-08-31T15:59:59.9999999Z',
  scheduleStatus: 'Completed',
  executionStatus: 'Completed',
  isInspected: true,
  inspectionCompletedAt: '2026-08-28T03:00:00Z',
  timeliness: 'OnTime',
  condition: 'NonOperational',
  remarks: 'Pressure is low.',
  actionsRecommendations: 'Arrange a pressure check.',
  formId,
  formStatus: 'Submitted',
  isAcknowledged: false,
  acknowledgedAt: null,
}

const dashboard = {
  pmCycle,
  assetCategory: 'fire-extinguisher',
  department: 'GSD',
  deadline: '2026-08-31T15:59:59.9999999Z',
  periodState: 'Closed',
  complianceMeasurable: true,
  inspectionResultsAvailable: true,
  scheduled: 1,
  inspected: 1,
  completedOnTime: 1,
  completedLate: 0,
  notCompleted: 0,
  remaining: 0,
  operational: 0,
  nonOperational: 1,
  onTimeCompliancePercent: 100,
  progressPercent: 100,
  batches: [batch],
  assets: [dashboardAsset],
}

const cycleGroups = [
  {
    assetCategory: 'fire-extinguisher',
    year: 2026,
    cycles: [{ pmCycle, scheduled: 1 }],
  },
]

function jsonResponse(body: unknown) {
  return {
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  }
}

const assetCategories = [
  {
    code: 'fire-extinguisher',
    displayName: 'Fire extinguishers',
    scheduledMonths: [2, 5, 8, 11],
  },
  {
    code: 'fire-alarm',
    displayName: 'Fire alarm systems',
    scheduledMonths: [6, 12],
  },
  {
    code: 'emergency-light',
    displayName: 'Emergency lights',
    scheduledMonths: [6, 12],
  },
  {
    code: 'water-drinking-station',
    displayName: 'Water drinking stations',
    scheduledMonths: [2, 5, 8, 11],
  },
]

async function installApi(page: Page, assetList = [asset]) {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill(jsonResponse(session)),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill(jsonResponse(session.user)),
  )
  await page.route('**/api/v1/reference-data/asset-categories', (route) =>
    route.fulfill(jsonResponse(assetCategories)),
  )
  await page.route('**/api/v1/reference-data/schedule-statuses', (route) =>
    route.fulfill(
      jsonResponse([
        { code: 'Due', displayName: 'Due' },
        { code: 'Completed', displayName: 'Completed' },
      ]),
    ),
  )
  await page.route('**/api/v1/reference-data/schedule-period-types', (route) =>
    route.fulfill(
      jsonResponse([
        { code: 'Quarter', displayName: 'Quarter' },
        { code: 'Semester', displayName: 'Semester' },
        { code: 'Annual', displayName: 'Annual' },
      ]),
    ),
  )
  await page.route('**/api/v1/reference-data/schedule-quarters', (route) =>
    route.fulfill(
      jsonResponse([
        { code: 'Q1', displayName: 'Q1' },
        { code: 'Q2', displayName: 'Q2' },
        { code: 'Q3', displayName: 'Q3' },
        { code: 'Q4', displayName: 'Q4' },
      ]),
    ),
  )
  await page.route('**/api/v1/pm-period-dashboard**', (route) => {
    const pathname = new URL(route.request().url()).pathname
    return route.fulfill(
      jsonResponse(pathname.endsWith('/cycles') ? cycleGroups : dashboard),
    )
  })
  await page.route('**/api/v1/assets**', (route) => {
    const pathname = new URL(route.request().url()).pathname
    if (pathname === '/api/v1/assets') {
      return route.fulfill(jsonResponse(assetList))
    }
    if (pathname.endsWith('/verification-location')) {
      return route.fulfill(
        jsonResponse({
          verificationLatitude: null,
          verificationLongitude: null,
          verificationRadiusMeters: null,
        }),
      )
    }
    const id = pathname.slice('/api/v1/assets/'.length)
    const record = assetList.find((candidate) => candidate.id === id)
    return record
      ? route.fulfill(jsonResponse(record))
      : route.fulfill({ status: 404, body: '' })
  })
  await page.route('**/api/v1/inspections**', (route) => {
    const pathname = new URL(route.request().url()).pathname
    if (pathname === '/api/v1/inspections') {
      return route.fulfill(jsonResponse([inspection]))
    }
    if (pathname === '/api/v1/inspections/history/' + assetId) {
      return route.fulfill(jsonResponse(history))
    }
    if (pathname === '/api/v1/inspections/history/' + assetList[0]?.id) {
      return route.fulfill(jsonResponse(history))
    }
    if (pathname === '/api/v1/inspections/' + inspectionId) {
      return route.fulfill(jsonResponse(inspection))
    }
    return route.fulfill(jsonResponse([]))
  })
  await page.route('**/api/v1/schedules**', (route) => {
    const pathname = new URL(route.request().url()).pathname
    if (pathname === '/api/v1/schedules/assignment-options') {
      return route.fulfill(jsonResponse({ workers: [], supervisors: [] }))
    }
    if (pathname === '/api/v1/schedules') {
      return route.fulfill(jsonResponse([schedule]))
    }
    if (pathname === '/api/v1/schedules/' + scheduleId) {
      return route.fulfill(jsonResponse(schedule))
    }
    return route.fulfill(jsonResponse([]))
  })
  await page.route('**/api/v1/preventive-maintenance-forms**', (route) => {
    const pathname = new URL(route.request().url()).pathname
    if (pathname === '/api/v1/preventive-maintenance-forms/' + formId) {
      return route.fulfill(jsonResponse(form))
    }
    return route.fulfill(jsonResponse([]))
  })
}

function expectNoHorizontalOverflow(page: Page) {
  return expect
    .poll(() =>
      page.evaluate(
        () =>
          Math.max(
            document.documentElement.scrollWidth,
            document.body.scrollWidth,
          ) - document.documentElement.clientWidth,
      ),
    )
    .toBeLessThanOrEqual(1)
}

async function tabToLink(page: Page, linkName: string) {
  const link = page.getByRole('link', { name: linkName })
  for (let attempt = 0; attempt < 40; attempt += 1) {
    await page.keyboard.press('Tab')
    if (await link.evaluate((element) => element === document.activeElement)) {
      return link
    }
  }
  throw new Error('The back link was not reachable by keyboard Tab navigation.')
}

const dashboardViewports = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 375, height: 667 },
]

for (const viewport of dashboardViewports) {
  test(
    'keeps PM dashboard context through the detail chain on ' + viewport.name,
    async ({ page }) => {
      await installApi(page)
      await page.setViewportSize({
        width: viewport.width,
        height: viewport.height,
      })

      await page.goto(
        '/app/dashboard?assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08&department=GSD&condition=NonOperational&timeliness=OnTime&search=FE-001',
      )
      await expect(
        page.getByRole('heading', { name: 'Preventive Maintenance Dashboard' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)

      await page.getByRole('link', { name: 'FE-001', exact: true }).click()
      await expect(
        page.getByRole('heading', { name: 'FE-001', level: 1 }),
      ).toBeVisible()
      await expect(
        page.getByRole('link', { name: 'Back to PM dashboard' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)

      await page.getByRole('link', { name: 'View source' }).click()
      await expect(
        page.getByRole('heading', { name: 'FE-001', level: 1 }),
      ).toBeVisible()
      await expect(
        page.getByRole('heading', { name: 'Record information' }),
      ).toBeVisible()
      await expect(
        page.getByRole('link', { name: 'Back to FE-001' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)

      await page.getByRole('link', { name: 'August 2026', exact: true }).click()
      await expect(
        page.getByRole('heading', { name: 'FE-001', level: 1 }),
      ).toBeVisible()
      await expect(
        page.getByRole('link', { name: 'Back to inspection' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)

      await page.reload()
      await expect(
        page.getByRole('link', { name: 'Back to inspection' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)
      await page.getByRole('link', { name: 'Back to inspection' }).click()
      await expect(page).toHaveURL(
        new RegExp('/app/inspections/' + inspectionId),
      )
      await page.getByRole('link', { name: 'Back to FE-001' }).click()
      await expect(page).toHaveURL(new RegExp('/app/assets/' + assetId))
      await page.getByRole('link', { name: 'Back to PM dashboard' }).click()
      await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
      await expect(page).toHaveURL(/year=2026/)
      await expect(page).toHaveURL(/pmCycle=2026-08/)
      await expect(page).toHaveURL(/department=GSD/)
      await expect(page).toHaveURL(/condition=NonOperational/)
      await expect(page).toHaveURL(/timeliness=OnTime/)
      await expect(page).toHaveURL(/search=FE-001/)

      await page.goto('/app/assets/' + assetId)
      await expect(
        page.getByRole('link', { name: 'Back to assets' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)
      await page.getByRole('link', { name: 'View source' }).click()
      await expect(
        page.getByRole('link', { name: 'Back to FE-001' }),
      ).toBeVisible()
      await page.getByRole('link', { name: 'FE-001', exact: true }).click()
      await expect(
        page.getByRole('link', { name: 'Back to inspection' }),
      ).toBeVisible()
      if (viewport.width === 375) await expectNoHorizontalOverflow(page)
      await page.getByRole('link', { name: 'Back to inspection' }).click()
      await expect(
        page.getByRole('heading', { name: 'FE-001', level: 1 }),
      ).toBeVisible()

      await page.goto('/app/inspections/' + inspectionId)
      await expect(
        page.getByRole('link', { name: 'Back to inspections' }),
      ).toBeVisible()
      await page.goto('/app/schedules/' + scheduleId)
      await expect(
        page.getByRole('link', { name: 'Back to schedules' }),
      ).toBeVisible()
    },
  )
}
test('returns to filtered, paged assets and keeps the back link keyboard reachable on mobile', async ({
  page,
}) => {
  const pagedAssets = Array.from({ length: 11 }, (_, index) => ({
    ...asset,
    id: '00000000-0000-4000-8000-' + String(index + 1).padStart(12, '0'),
    assetCode: 'FE-' + String(index + 1).padStart(3, '0'),
    location: 'Floor ' + String(index + 1),
  }))
  await installApi(page, pagedAssets)
  await page.setViewportSize({ width: 1280, height: 800 })

  await page.goto(
    '/app/assets?assetCategory=fire-extinguisher&status=Active&page=2',
  )
  const finalAssetRow = page.getByRole('row').filter({ hasText: 'FE-011' })
  await expect(finalAssetRow).toBeVisible()
  await finalAssetRow.getByRole('link', { name: 'View details' }).click()
  await expect(
    page.getByRole('heading', { name: 'FE-011', level: 1 }),
  ).toBeVisible()

  await page.setViewportSize({ width: 375, height: 667 })
  await page.reload()
  await expect(page.getByRole('link', { name: 'Back to assets' })).toBeVisible()
  await expectNoHorizontalOverflow(page)

  const backLink = await tabToLink(page, 'Back to assets')
  await expect(backLink).toBeVisible()
  const focusRing = await backLink.evaluate(
    (element) => window.getComputedStyle(element).boxShadow,
  )
  expect(focusRing).not.toBe('none')

  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
  await expect(page).toHaveURL(/status=Active/)
  await expect(page).toHaveURL(/page=2/)
  await expectNoHorizontalOverflow(page)

  await page.setViewportSize({ width: 1280, height: 800 })
  await expect(
    page.getByRole('row').filter({ hasText: 'FE-011' }),
  ).toBeVisible()
})
test('returns from submitted batch inspection and form detail to the exact review context', async ({
  page,
}) => {
  await installApi(page)
  await page.setViewportSize({ width: 375, height: 667 })
  await page.goto(
    '/app/preventive-maintenance-forms/' +
      formId +
      '/review?department=GSD&assetCategory=fire-extinguisher&year=2026&pmCycle=2026-08&condition=NonOperational&timeliness=OnTime&search=FE-001',
  )
  await expect(
    page.getByRole('heading', { name: 'Review before acknowledgement' }),
  ).toBeVisible()
  await page.getByRole('link', { name: 'View inspection detail' }).click()
  await expect(
    page.getByRole('link', { name: 'Back to batch review' }),
  ).toBeVisible()
  await page.reload()
  await expectNoHorizontalOverflow(page)
  await page.getByRole('link', { name: 'Back to batch review' }).click()
  await expect(page).toHaveURL(
    new RegExp('/app/preventive-maintenance-forms/' + formId + '/review'),
  )
  await expect(page).toHaveURL(/pmCycle=2026-08/)

  await page.getByRole('link', { name: 'View full PM form' }).click()
  await expect(
    page.getByRole('heading', { name: 'PMF-2026-0001', level: 1 }),
  ).toBeVisible()
  await expect(
    page.getByRole('heading', { name: 'Record information' }),
  ).toBeVisible()
  await expect(
    page.getByRole('link', { name: 'Back to batch review' }),
  ).toBeVisible()
  await page.reload()
  await expectNoHorizontalOverflow(page)
  await page.getByRole('link', { name: 'Back to batch review' }).click()
  await expect(page).toHaveURL(
    new RegExp('/app/preventive-maintenance-forms/' + formId + '/review'),
  )
  await expect(page).toHaveURL(/pmCycle=2026-08/)
})
