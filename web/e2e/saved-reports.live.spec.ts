import { expect, test } from '@playwright/test'

test.use({ screenshot: 'off', trace: 'off' })

const apiBaseUrl = (
  process.env.UNIPM_API_BASE_URL ?? 'http://localhost:5200'
).replace(/\/$/, '')
const appApiBaseUrl = (process.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const password = process.env.UNIPM_DEV_USER_PASSWORD
const liveOptIn = process.env.UNIPM_SAVED_REPORTS_LIVE === 'true'

type LoginResponse = {
  accessToken: string
  user: { roles: string[] }
}

type ExecutionResponse = {
  aggregateRows: Array<{
    measures: Record<string, { value: number | null }>
  }>
}

function registeredAssetCount(result: ExecutionResponse) {
  return result.aggregateRows.reduce(
    (total, row) =>
      total + Number(row.measures.registeredAssetCount?.value ?? 0),
    0,
  )
}

test('saved report reuse reads new SQL Server asset data after confirmation', async ({
  page,
  request,
}) => {
  test.skip(
    !liveOptIn,
    'Set UNIPM_SAVED_REPORTS_LIVE=true only for the owned disposable SQL Server check.',
  )
  test.skip(
    apiBaseUrl !== 'http://localhost:5200',
    'The live saved-report check is restricted to the owned loopback API on localhost:5200.',
  )
  test.skip(
    appApiBaseUrl !== apiBaseUrl,
    'Set VITE_API_BASE_URL to the same isolated loopback API before opting into the live check.',
  )
  test.skip(
    !password,
    'Set UNIPM_DEV_USER_PASSWORD for the isolated Development users.',
  )
  if (!password) return

  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const reportName = `Saved inventory ${suffix}`
  let accessToken = ''
  let interpretationCalls = 0
  page.on('response', (response) => {
    if (response.url().endsWith('/api/v1/analytics/pm/v3/interpret')) {
      interpretationCalls += 1
    }
  })

  await page.goto('/login')
  await page.getByLabel('Institutional email').fill('gsd@unipm.local')
  await page.getByLabel('Password').fill(password)
  const loginResponsePromise = page.waitForResponse(
    (response) =>
      response.url() === `${apiBaseUrl}/api/v1/auth/login` &&
      response.request().method() === 'POST',
  )
  await page.getByRole('button', { name: 'Sign in' }).click()
  const loginResponse = await loginResponsePromise
  expect(loginResponse.status()).toBe(200)
  const session = (await loginResponse.json()) as LoginResponse
  expect(session.user.roles).toContain('GSD')
  accessToken = session.accessToken
  await expect(page).toHaveURL(/\/app\/dashboard$/)

  await page.goto('/app/analytics')
  await page
    .getByRole('textbox', { name: 'Question' })
    .fill('How many fire extinguishers are registered by status?')
  await page
    .getByRole('button', { name: 'Generate report', exact: true })
    .click()
  await page
    .getByRole('button', { name: 'Preview report', exact: true })
    .click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  await page.getByLabel('Report name', { exact: true }).fill(reportName)
  await page.getByRole('button', { name: 'Save as new report' }).click()
  await expect(
    page.getByRole('status').filter({ hasText: 'Report settings saved.' }),
  ).toBeVisible()

  await page.reload()
  await page.getByText(/^My reports/).click()
  const savedRow = page.locator('li').filter({ hasText: reportName })
  await expect(savedRow).toBeVisible()
  await savedRow.getByRole('button', { name: 'Open preview' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  const firstExecutionPromise = page.waitForResponse(
    (response) =>
      response.url() === `${apiBaseUrl}/api/v1/analytics/pm/v3/execute` &&
      response.request().method() === 'POST',
  )
  await page.getByRole('button', { name: 'Confirm and generate' }).click()
  const firstExecution = await firstExecutionPromise
  expect(firstExecution.status()).toBe(200)
  const beforeCount = registeredAssetCount(
    (await firstExecution.json()) as ExecutionResponse,
  )

  const assetResponse = await request.post(`${apiBaseUrl}/api/v1/assets`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: {
      assetCode: `SAVED-REPORT-${suffix}`,
      assetCategory: 'fire-extinguisher',
      department: `Saved report ${suffix}`,
      building: 'Synthetic saved-report verification',
      location: 'Synthetic test location',
    },
  })
  expect(assetResponse.status()).toBe(201)

  await savedRow.getByRole('button', { name: 'Open preview' }).click()
  await expect(
    page.getByRole('heading', { name: 'Report preview', exact: true }),
  ).toBeVisible()
  const secondExecutionPromise = page.waitForResponse(
    (response) =>
      response.url() === `${apiBaseUrl}/api/v1/analytics/pm/v3/execute` &&
      response.request().method() === 'POST',
  )
  await page.getByRole('button', { name: 'Confirm and generate' }).click()
  const secondExecution = await secondExecutionPromise
  expect(secondExecution.status()).toBe(200)
  const afterCount = registeredAssetCount(
    (await secondExecution.json()) as ExecutionResponse,
  )

  expect(afterCount).toBe(beforeCount + 1)
  expect(interpretationCalls).toBe(1)
})
