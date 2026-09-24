import { expect, test } from '@playwright/test'

const apiBaseUrl = (
  process.env.UNIPM_API_BASE_URL ?? 'http://localhost:5254'
).replace(/\/$/, '')
const password = process.env.UNIPM_DEV_USER_PASSWORD

type Schedule = {
  id: string
  pmCycle: string
  status: string
  assignedToUserId: string | null
  assignedSupervisorUserId: string | null
  asset: {
    assetCode: string
    assetCategory: string
    department: string | null
  } | null
}

type AssignmentOptions = {
  workers: { id: string; displayName: string }[]
  supervisors: { id: string; displayName: string }[]
}

type LoginResponse = {
  accessToken: string
  user: { email: string; roles: string[] }
}

test('GSD assigns the seeded batch and Inspector can only review it', async ({
  page,
  request,
}, testInfo) => {
  test.skip(
    !password,
    'Set UNIPM_DEV_USER_PASSWORD for the local demo database.',
  )
  if (!password) return

  const gsdLogin = await request.post(`${apiBaseUrl}/api/v1/auth/login`, {
    data: { email: 'gsd@unipm.local', password },
  })
  expect(gsdLogin.status()).toBe(200)
  const gsdSession = (await gsdLogin.json()) as LoginResponse
  expect(gsdSession.user.roles).toContain('GSD')
  const gsdHeaders = { Authorization: `Bearer ${gsdSession.accessToken}` }

  const scheduleResponse = await request.get(`${apiBaseUrl}/api/v1/schedules`, {
    headers: gsdHeaders,
  })
  expect(scheduleResponse.status()).toBe(200)
  const schedules = (await scheduleResponse.json()) as Schedule[]
  const batch = schedules.filter(
    (schedule) =>
      schedule.pmCycle === '2026-09' &&
      schedule.asset?.department?.toUpperCase() === 'CCMS' &&
      schedule.asset.assetCategory === 'fire-extinguisher' &&
      ['Due', 'Ongoing', 'Overdue'].includes(schedule.status),
  )
  expect(batch).toHaveLength(3)
  const selectedSchedule = batch.find(
    (schedule) => schedule.asset?.assetCode === 'DEMO-FE-001',
  )
  expect(selectedSchedule).toBeDefined()

  const optionsResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules/assignment-options`,
    { headers: gsdHeaders },
  )
  expect(optionsResponse.status()).toBe(200)
  const options = (await optionsResponse.json()) as AssignmentOptions
  const worker = options.workers.find(
    (option) => option.displayName === 'Maintenance Inspector',
  )
  const supervisor = options.supervisors.find(
    (option) => option.displayName === 'Maintenance Supervisor',
  )
  expect(worker).toBeDefined()
  expect(supervisor).toBeDefined()

  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.goto('/login')
  await page.getByLabel('Institutional email').fill('gsd@unipm.local')
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)

  await page.goto(`/app/schedules/${selectedSchedule!.id}`)
  await expect(
    page.getByRole('heading', { name: 'Batch assignment' }),
  ).toBeVisible()
  await page.getByLabel('Skilled worker (Inspector)').selectOption(worker!.id)
  await page.getByLabel('Supervisor (oversight)').selectOption(supervisor!.id)

  const assignmentResponsePromise = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname ===
        `/api/v1/schedules/${selectedSchedule!.id}/assignment`,
  )
  await page.getByRole('button', { name: /entire batch assignment/ }).click()
  const assignmentResponse = await assignmentResponsePromise
  expect(assignmentResponse.status()).toBe(200)
  const assignment = (await assignmentResponse.json()) as {
    department: string
    assetCategory: string
    pmCycle: string
    workerUserId: string
    workerDisplayName: string
    supervisorUserId: string
    supervisorDisplayName: string
    scheduleIds: string[]
  }
  expect(assignment.department).toBe('CCMS')
  expect(assignment.assetCategory).toBe('fire-extinguisher')
  expect(assignment.pmCycle).toBe('2026-09')
  expect(assignment.scheduleIds).toHaveLength(3)
  expect(assignment.workerUserId).toBe(worker!.id)
  expect(assignment.supervisorUserId).toBe(supervisor!.id)
  await expect(
    page.getByRole('status').filter({ hasText: 'Assigned 3 schedule(s)' }),
  ).toBeVisible()
  await expect(
    page.getByRole('definition').filter({ hasText: 'Maintenance Inspector' }),
  ).toBeVisible()
  await expect(
    page.getByRole('definition').filter({ hasText: 'Maintenance Supervisor' }),
  ).toBeVisible()

  for (const scheduleId of assignment.scheduleIds) {
    const response = await request.get(
      `${apiBaseUrl}/api/v1/schedules/${scheduleId}`,
      { headers: gsdHeaders },
    )
    expect(response.status()).toBe(200)
    const updated = (await response.json()) as Schedule
    expect(updated.assignedToUserId).toBe(worker!.id)
    expect(updated.assignedSupervisorUserId).toBe(supervisor!.id)
  }

  await page
    .getByRole('heading', { name: 'Batch assignment' })
    .scrollIntoViewIfNeeded()
  await page.screenshot({
    path: testInfo.outputPath('gsd-batch-assignment.png'),
    fullPage: true,
  })
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.getByLabel('Skilled worker (Inspector)')).toBeVisible()
  await expect(page.getByLabel('Supervisor (oversight)')).toBeVisible()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true)
  await page.screenshot({
    path: testInfo.outputPath('gsd-batch-assignment-mobile.png'),
    fullPage: true,
  })
  await page.setViewportSize({ width: 1440, height: 1000 })

  const inspectorLogin = await request.post(`${apiBaseUrl}/api/v1/auth/login`, {
    data: { email: 'inspector@unipm.local', password },
  })
  expect(inspectorLogin.status()).toBe(200)
  const inspectorSession = (await inspectorLogin.json()) as LoginResponse
  expect(inspectorSession.user.roles).toContain('Inspector')

  await page.getByRole('button', { name: 'Sign out' }).first().click()
  await expect(page).toHaveURL(/\/login$/)
  await page.getByLabel('Institutional email').fill('inspector@unipm.local')
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)
  await page.goto(`/app/schedules/${selectedSchedule!.id}`)

  await expect(
    page.getByRole('heading', { name: 'Batch assignment' }),
  ).toBeVisible()
  await expect(page.getByLabel('Skilled worker (Inspector)')).toHaveCount(0)
  await expect(page.getByLabel('Supervisor (oversight)')).toHaveCount(0)
  await expect(
    page.getByText('Inspector assigned', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByText('Supervisor assigned', { exact: true }),
  ).toBeVisible()
  await page
    .getByRole('heading', { name: 'Batch assignment' })
    .scrollIntoViewIfNeeded()
  await page.screenshot({
    path: testInfo.outputPath('inspector-batch-assignment-read-only.png'),
    fullPage: true,
  })

  const forbiddenAssignment = await request.put(
    `${apiBaseUrl}/api/v1/schedules/${selectedSchedule!.id}/assignment`,
    {
      headers: { Authorization: `Bearer ${inspectorSession.accessToken}` },
      data: { workerUserId: worker!.id, supervisorUserId: supervisor!.id },
    },
  )
  expect(forbiddenAssignment.status()).toBe(403)
})
