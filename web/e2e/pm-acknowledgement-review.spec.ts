import { expect, test } from '@playwright/test'

const formId = '22222222-2222-4222-8222-222222222222'
const inspectionId = '33333333-3333-4333-8333-333333333333'
const scheduleId = '44444444-4444-4444-8444-444444444444'
const assetId = '55555555-5555-4555-8555-555555555555'
const reviewerId = '66666666-6666-4666-8666-666666666666'

const session = {
  accessToken: 'fictional-pm-review-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: reviewerId,
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD Reviewer',
    roles: ['GSD'],
  },
}

const form = {
  id: formId,
  fileNumber: 'PMF-2026-0001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  pmCycle: '2026-08',
  periodType: 'Quarter',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: '2026-2027',
  status: 'Submitted',
  createdByUserId: reviewerId,
  submittedByUserId: reviewerId,
  submittedAt: '2026-08-29T01:00:00Z',
  fieldWorkCompletedAt: '2026-08-28T03:00:00Z',
  createdAt: '2026-08-28T00:00:00Z',
  updatedAt: '2026-08-29T01:00:00Z',
  inspections: [
    {
      id: inspectionId,
      scheduleId,
      assetId,
      inspectorUserId: reviewerId,
      dateInspected: '2026-08-28T02:00:00Z',
      startedAt: '2026-08-28T02:00:00Z',
      completedAt: '2026-08-28T03:00:00Z',
      isOperational: false,
      remarks: 'Pressure is low.',
      actionsRecommendations: 'Inspect and recharge the unit.',
      dateAccomplished: null,
      waterReplaceCarbonFilter: null,
      waterReplaceSedimentFilter: null,
      waterCheckUvLight: null,
      assetCode: 'FE-TEST-001',
      location: 'Main hallway',
      skilledWorkerIdentity: 'Synthetic Inspector',
      createdAt: '2026-08-28T02:00:00Z',
      updatedAt: '2026-08-28T03:00:00Z',
    },
  ],
}

const batch = {
  department: 'GSD',
  assetCategory: 'fire-extinguisher',
  pmCycle: '2026-08',
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
  submittedAt: '2026-08-29T01:00:00Z',
  isAcknowledged: false,
  acknowledgedAt: null,
}

const assetRow = {
  scheduleId,
  assetId,
  inspectionId,
  assetCode: 'FE-TEST-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  location: 'Main hallway',
  department: 'GSD',
  pmCycle: '2026-08',
  scheduleDate: '2026-08-31T15:59:59.999Z',
  deadline: '2026-08-31T15:59:59.999Z',
  scheduleStatus: 'Completed',
  executionStatus: 'Completed',
  isInspected: true,
  inspectionCompletedAt: '2026-08-28T03:00:00Z',
  timeliness: 'OnTime',
  condition: 'NonOperational',
  remarks: 'Pressure is low.',
  actionsRecommendations: 'Inspect and recharge the unit.',
  formId,
  formStatus: 'Submitted',
  isAcknowledged: false,
  acknowledgedAt: null,
}

const dashboard = {
  pmCycle: '2026-08',
  assetCategory: 'fire-extinguisher',
  department: 'GSD',
  deadline: '2026-08-31T15:59:59.999Z',
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
  assets: [assetRow],
}

function jsonResponse(body: unknown) {
  return {
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  }
}

test('keeps Form Review content aligned and contained across desktop and mobile', async ({
  page,
}) => {
  await mockApi(page)
  await page.route('**/api/v1/preventive-maintenance-forms**', (route) =>
    route.fulfill(jsonResponse([form])),
  )
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.goto(
    '/app/preventive-maintenance-forms?status=Submitted&assetCategory=fire-extinguisher&department=GSD&pmCycle=2026-08',
  )

  const registry = page.locator('section[aria-labelledby="forms-title"]')
  await expect(
    page.locator('.app-workspace').getByRole('heading', {
      name: 'Form review',
      exact: true,
    }),
  ).toBeVisible()
  await expect(
    registry.getByRole('heading', { name: 'Find forms' }),
  ).toBeVisible()
  await expect(
    registry.getByRole('heading', { name: 'Form records' }),
  ).toBeVisible()
  expect(await registry.locator('h1, h2').allTextContents()).toEqual([
    'Form review',
    'Find forms',
    'Form records',
  ])
  await expect(page.getByLabel('Search forms')).toBeVisible()
  await expect(page.getByLabel('Form status')).toBeVisible()
  await page.getByText('More filters', { exact: true }).click()
  await expect(page.getByLabel('Asset category')).toBeVisible()
  await expect(page.getByLabel('Department')).toBeVisible()
  await expect(page.getByLabel('PM cycle')).toBeVisible()
  await expect(page.getByLabel('Form status')).toHaveValue('Submitted')
  await expect(
    page.getByRole('link', { name: 'PMF-2026-0001' }).first(),
  ).toBeVisible()
  await expect(
    page
      .getByRole('row')
      .filter({ hasText: 'PMF-2026-0001' })
      .getByText('Awaiting acknowledgement', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByRole('link', { name: /PMF-2026-0001/ }).first(),
  ).toBeVisible()
  await expect(page).toHaveURL(/department=GSD/)
  await expect(page).toHaveURL(/pmCycle=2026-08/)
  const formReviewAlignment = await page.evaluate(() => {
    const registry = document.querySelector(
      'section[aria-labelledby="forms-title"]',
    )!
    const findHeading = registry.querySelector('#find-forms-title')!
    const filterCard = findHeading
      .closest('section')!
      .querySelector('.content-card')!
    const recordsHeading = registry.querySelector('#form-records-title')!
    const recordsSection = recordsHeading.closest('section')!
    const tableWrapper = registry
      .querySelector('table')!
      .closest('.overflow-x-auto')!
    const rect = (element: Element) => element.getBoundingClientRect()
    return {
      registry: rect(registry),
      filterCard: rect(filterCard),
      recordsHeading: rect(recordsHeading),
      recordsSection: rect(recordsSection),
      tableWrapper: rect(tableWrapper),
    }
  })
  expect(
    Math.abs(
      formReviewAlignment.filterCard.left - formReviewAlignment.registry.left,
    ),
  ).toBeLessThan(1)
  expect(
    Math.abs(
      formReviewAlignment.filterCard.width - formReviewAlignment.registry.width,
    ),
  ).toBeLessThan(1)
  expect(
    Math.abs(
      formReviewAlignment.recordsHeading.left -
        formReviewAlignment.registry.left,
    ),
  ).toBeLessThan(1)
  expect(
    Math.abs(
      formReviewAlignment.recordsSection.width -
        formReviewAlignment.registry.width,
    ),
  ).toBeLessThan(1)
  expect(
    Math.abs(
      formReviewAlignment.tableWrapper.width -
        formReviewAlignment.recordsSection.width,
    ),
  ).toBeLessThan(1)
  await page.screenshot({
    path: 'test-results/web-ui-polish-form-review-desktop.png',
    fullPage: true,
  })

  await page.setViewportSize({ width: 375, height: 812 })
  await expect(
    page.getByRole('link', { name: /PMF-2026-0001/ }).first(),
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
  await expect(
    page.getByRole('link', { name: /PMF-2026-0001/ }).last(),
  ).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-form-review-mobile.png',
    fullPage: true,
  })
})

async function mockApi(page: import('@playwright/test').Page) {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill(jsonResponse(session)),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill(jsonResponse(session.user)),
  )
  await page.route('**/api/v1/pm-period-dashboard**', (route) =>
    route.fulfill(jsonResponse(dashboard)),
  )
  await page.route('**/api/v1/pm-period-dashboard/cycles**', (route) =>
    route.fulfill(
      jsonResponse([
        {
          assetCategory: 'fire-extinguisher',
          year: 2026,
          cycles: [{ pmCycle: '2026-08', scheduled: 1 }],
        },
      ]),
    ),
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
  await page.route(
    '**/api/v1/schedules/supervisor-assignment-options',
    (route) => route.fulfill(jsonResponse({ supervisors: [] })),
  )
  await page.route(
    `**/api/v1/preventive-maintenance-forms/${formId}`,
    (route) => route.fulfill(jsonResponse(form)),
  )
  await page.route(`**/api/v1/inspections/${inspectionId}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: inspectionId,
        scheduleId,
        assetId,
        inspectorUserId: reviewerId,
        dateInspected: '2026-08-28T02:00:00Z',
        isOperational: false,
        hasPhotoEvidence: false,
        remarks: 'Pressure is low.',
        actionsRecommendations: 'Inspect and recharge the unit.',
        externalPmNumber: null,
        wmsReferralRevision: 0,
        correctiveFollowUpStatus: 'CorrectiveFollowUpPending',
        createdAt: '2026-08-28T02:00:00Z',
        updatedAt: '2026-08-28T03:00:00Z',
      }),
    ),
  )
  await page.route(`**/api/v1/assets/${assetId}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: assetId,
        assetCode: 'FE-TEST-001',
        assetCategory: 'fire-extinguisher',
        building: 'Main Building',
        department: 'GSD',
        location: 'Main hallway',
        qrCodeValue: 'UNIPM-FE-TEST-001',
        status: 'Active',
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: '2026-08-28T03:00:00Z',
        hasVerificationLocation: false,
      }),
    ),
  )
  await page.route(`**/api/v1/schedules/${scheduleId}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: scheduleId,
        assetId,
        scheduleDate: '2026-08-31T15:59:59.999Z',
        periodType: 'Quarter',
        status: 'Completed',
        quarter: 'Q3',
        semester: null,
        year: 2026,
        academicYear: '2026-2027',
        assignedToUserId: reviewerId,
        completedAt: '2026-08-28T03:00:00Z',
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: '2026-08-28T03:00:00Z',
        asset: {
          id: assetId,
          assetCode: 'FE-TEST-001',
          assetCategory: 'fire-extinguisher',
          building: 'Main Building',
          department: 'GSD',
          location: 'Main hallway',
          status: 'Active',
        },
      }),
    ),
  )
}

test('demonstrates the submitted PM batch acknowledgement review workflow', async ({
  page,
}) => {
  await mockApi(page)
  await page.route(
    /\/api\/v1\/preventive-maintenance-forms(?:\?.*)?$/,
    (route) => route.fulfill(jsonResponse([form])),
  )

  const registrySearch = {
    status: 'Submitted',
    assetCategory: 'fire-extinguisher',
    department: 'GSD',
    pmCycle: '2026-08',
  }
  await page.goto(
    '/app/preventive-maintenance-forms?status=Submitted&assetCategory=fire-extinguisher&department=GSD&pmCycle=2026-08',
  )
  await expect(page.getByRole('link', { name: 'PMF-2026-0001' })).toBeVisible()
  await page.getByRole('link', { name: /PMF-2026-0001/ }).click()
  await expect(page).toHaveURL(
    new RegExp(`/app/preventive-maintenance-forms/${formId}`),
  )
  const detailUrl = new URL(page.url())
  expect(detailUrl.pathname).toBe(`/app/preventive-maintenance-forms/${formId}`)
  expect(
    JSON.parse(detailUrl.searchParams.get('returnContext') ?? 'null'),
  ).toEqual({ kind: 'formRegistry', search: registrySearch })

  await expect(
    page.getByRole('heading', { name: 'PMF-2026-0001' }),
  ).toBeVisible()
  await expect(page.getByText('FE-TEST-001', { exact: true })).toBeVisible()
  await expect(page.getByText(/Main hallway/)).toBeVisible()
  await expect(page.getByText('Not operational', { exact: true })).toBeVisible()
  await expect(
    page.getByText('Pressure is low.', { exact: true }).first(),
  ).toBeVisible()
  await page.getByText('Row details', { exact: true }).click()
  await expect(
    page.getByText('Inspect and recharge the unit.', { exact: true }),
  ).toBeVisible()

  await expect(
    page.getByRole('heading', { name: 'Acknowledge submitted form' }),
  ).toBeVisible()
  await expect(page.getByLabel('Signatory name')).toBeVisible()
  await expect(page.getByLabel('Signatory position')).toBeVisible()
  await expect(page.getByText('Signature', { exact: true })).toBeVisible()
  await expect(
    page.getByRole('button', { name: 'Acknowledge form' }),
  ).toBeVisible()

  await page.getByRole('link', { name: 'Back to form review' }).click()
  await expect(page).toHaveURL(/status=Submitted/)
  await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
  await expect(page).toHaveURL(/department=GSD/)
  await expect(page).toHaveURL(/pmCycle=2026-08/)
  await expect(page.getByLabel('Form status')).toHaveValue('Submitted')
})
