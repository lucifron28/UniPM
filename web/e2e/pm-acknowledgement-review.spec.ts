import { expect, test } from '@playwright/test'

const formId = '22222222-2222-4222-8222-222222222222'
const inspectionId = '33333333-3333-4333-8333-333333333333'
const scheduleId = '44444444-4444-4444-8444-444444444444'
const assetId = '55555555-5555-4555-8555-555555555555'
const reviewerId = '66666666-6666-4666-8666-666666666666'

const session = {
  accessToken: 'fictional-pm-review-token',
  expiresAtUtc: '2026-08-01T12:00:00Z',
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
  pmCycle: '2026-07',
  periodType: 'Quarter',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: '2026-2027',
  status: 'Submitted',
  createdByUserId: reviewerId,
  submittedByUserId: reviewerId,
  submittedAt: '2026-07-29T01:00:00Z',
  fieldWorkCompletedAt: '2026-07-28T03:00:00Z',
  createdAt: '2026-07-28T00:00:00Z',
  updatedAt: '2026-07-29T01:00:00Z',
  inspections: [
    {
      id: inspectionId,
      scheduleId,
      assetId,
      inspectorUserId: reviewerId,
      dateInspected: '2026-07-28T02:00:00Z',
      startedAt: '2026-07-28T02:00:00Z',
      completedAt: '2026-07-28T03:00:00Z',
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
      createdAt: '2026-07-28T02:00:00Z',
      updatedAt: '2026-07-28T03:00:00Z',
    },
  ],
}

const batch = {
  department: 'GSD',
  assetCategory: 'fire-extinguisher',
  pmCycle: '2026-07',
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
  fieldWorkCompletedAt: '2026-07-28T03:00:00Z',
  submittedAt: '2026-07-29T01:00:00Z',
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
  pmCycle: '2026-07',
  scheduleDate: '2026-07-01T00:00:00Z',
  deadline: '2026-07-31T16:00:00Z',
  scheduleStatus: 'Completed',
  executionStatus: 'Completed',
  isInspected: true,
  inspectionCompletedAt: '2026-07-28T03:00:00Z',
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
  pmCycle: '2026-07',
  assetCategory: 'fire-extinguisher',
  department: 'GSD',
  deadline: '2026-07-31T16:00:00Z',
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
          cycles: [{ pmCycle: '2026-07', scheduled: 1 }],
        },
      ]),
    ),
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
        dateInspected: '2026-07-28T02:00:00Z',
        isOperational: false,
        remarks: 'Pressure is low.',
        actionsRecommendations: 'Inspect and recharge the unit.',
        createdAt: '2026-07-28T02:00:00Z',
        updatedAt: '2026-07-28T03:00:00Z',
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
        createdAt: '2026-07-01T00:00:00Z',
        updatedAt: '2026-07-28T03:00:00Z',
      }),
    ),
  )
  await page.route(`**/api/v1/schedules/${scheduleId}`, (route) =>
    route.fulfill(
      jsonResponse({
        id: scheduleId,
        assetId,
        scheduleDate: '2026-07-01T00:00:00Z',
        periodType: 'Quarter',
        status: 'Completed',
        quarter: 'Q3',
        semester: null,
        year: 2026,
        academicYear: '2026-2027',
        assignedToUserId: reviewerId,
        completedAt: '2026-07-28T03:00:00Z',
        createdAt: '2026-07-01T00:00:00Z',
        updatedAt: '2026-07-28T03:00:00Z',
        asset: {
          id: assetId,
          assetCode: 'FE-TEST-001',
          assetCategory: 'fire-extinguisher',
          building: 'Main Building',
          department: 'GSD',
          location: 'Main hallway',
        },
      }),
    ),
  )
}

test('demonstrates the submitted PM batch acknowledgement review workflow', async ({
  page,
}) => {
  await mockApi(page)

  // 1. Start at the PM dashboard and 2. confirm the submitted batch action.
  await page.goto('/app/dashboard')
  await expect(
    page.getByRole('heading', { name: 'Preventive maintenance compliance' }),
  ).toBeVisible()
  await expect(page.getByRole('link', { name: 'Review batch' })).toBeVisible()

  // 3. Open the exact Department + Category + PM cycle batch review.
  await page.getByRole('link', { name: 'Review batch' }).click()
  await expect(page).toHaveURL(
    new RegExp(`/app/preventive-maintenance-forms/${formId}/review`),
  )

  // 4. Confirm the whole-batch context and submitted lifecycle status.
  await expect(
    page.getByRole('heading', { name: 'Review before acknowledgement' }),
  ).toBeVisible()
  const summary = page.getByLabel('Submitted batch review summary')
  const summaryValue = (label: string) =>
    summary.locator('dt').filter({ hasText: label }).locator('..').locator('dd')

  await expect(summary.getByText('Department', { exact: true })).toBeVisible()
  await expect(summary.getByText('GSD', { exact: true })).toBeVisible()
  await expect(
    summary.getByText('Fire Extinguisher', { exact: true }),
  ).toBeVisible()
  await expect(summary.getByText('July 2026', { exact: true })).toBeVisible()
  await expect(
    summary.getByText('Awaiting acknowledgement', { exact: true }),
  ).toBeVisible()

  // 5. Check backend-reported metrics and 6. the full asset row evidence.
  await expect(summaryValue('Scheduled')).toHaveText('1')
  await expect(summaryValue('Inspected')).toHaveText('1')
  await expect(summaryValue('Completed on time')).toHaveText('1')
  await expect(summaryValue('On-time compliance')).toHaveText('100%')
  await expect(summaryValue('Field-work completion')).toHaveText(/Jul 28, 2026/)
  await expect(summaryValue('Submitted timestamp')).toHaveText(/Jul 29, 2026/)
  const acknowledgementCard = page
    .getByRole('heading', { name: 'Acknowledge whole PM batch' })
    .locator('..')
    .locator('..')
  await expect(
    acknowledgementCard.getByText(
      /For the whole PM batch, acknowledgement records receipt\/noting/,
    ),
  ).toBeVisible()
  await expect(
    acknowledgementCard.getByText(/It is not personal witnessing\./),
  ).toBeVisible()
  await expect(
    acknowledgementCard.getByText(
      /It does not approve corrective work, funding, or an RMRF\./,
    ),
  ).toBeVisible()
  await expect(page.getByText('FE-TEST-001', { exact: true })).toBeVisible()
  await expect(
    page.getByText('Main Building · Main hallway', { exact: true }),
  ).toBeVisible()
  await expect(page.getByText('Not operational', { exact: true })).toBeVisible()
  await expect(
    page.getByText('Pressure is low.', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByText('Inspect and recharge the unit.', { exact: true }),
  ).toBeVisible()

  // 7. Follow the row inspection ID and 8. return with the stable review query.
  await page.getByRole('link', { name: 'View inspection detail' }).click()
  await expect(
    page.getByRole('heading', { name: `Inspection ${inspectionId}` }),
  ).toBeVisible()
  await expect(page.getByText('Arrange', { exact: false })).toHaveCount(0)
  await page.getByRole('link', { name: 'Back to batch review' }).click()
  await expect(page).toHaveURL(
    new RegExp(`/app/preventive-maintenance-forms/${formId}/review`),
  )
  await expect(page).toHaveURL(/assetCategory=fire-extinguisher/)
  await expect(page).toHaveURL(/pmCycle=2026-07/)
  await expect(page).toHaveURL(/department=GSD/)

  // 9. Open the complete PM form in read-only mode.
  await page.getByRole('link', { name: 'View full PM form' }).click()
  await expect(
    page.getByText('Read-only submitted form', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByRole('heading', { name: 'Inspection rows' }),
  ).toBeVisible()
  await expect(
    page.getByRole('heading', { name: 'Acknowledge whole PM batch' }),
  ).toHaveCount(0)

  // 10. Confirm the review table remains contained and scrollable on mobile.
  await page.goto(
    `/app/preventive-maintenance-forms/${formId}/review?department=GSD&assetCategory=fire-extinguisher&pmCycle=2026-07`,
  )
  await page.setViewportSize({ width: 375, height: 667 })
  const table = page.getByRole('table', {
    name: 'Submitted batch asset review',
  })
  await expect(table).toBeVisible()
  const container = table.locator('..')
  await expect(container).toHaveCSS('overflow-x', 'auto')
  const dimensions = await container.evaluate((element) => ({
    clientWidth: element.clientWidth,
    scrollWidth: element.scrollWidth,
  }))
  expect(dimensions.scrollWidth).toBeGreaterThan(dimensions.clientWidth)
  const pageDimensions = await page.evaluate(() => ({
    viewportWidth: window.innerWidth,
    documentWidth: Math.max(
      document.documentElement.scrollWidth,
      document.body.scrollWidth,
    ),
  }))
  expect(pageDimensions.documentWidth).toBeLessThanOrEqual(
    pageDimensions.viewportWidth,
  )
})
