import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from '@playwright/test'

const apiBaseUrl = (
  process.env.UNIPM_API_BASE_URL ?? 'http://localhost:5262'
).replace(/\/$/, '')
const apiOrigin = new URL(apiBaseUrl)
const password = process.env.UNIPM_DEV_USER_PASSWORD
const pngSignatureBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII='

type LoginResponse = {
  accessToken: string
  user: { id: string; email: string; roles: string[] }
}

type Schedule = {
  id: string
  pmCycle: string
  status: string
  completedAt: string | null
  updatedAt: string
  assignedToUserId: string | null
  assignedSupervisorUserId: string | null
  asset: {
    assetCode: string
    assetCategory: string
    department: string | null
  } | null
}

type BatchAssignment = {
  department: string
  assetCategory: string
  pmCycle: string
  workerUserId: string | null
  supervisorUserId: string
  scheduleIds: string[]
}

type DraftInspection = {
  id: string
  scheduleId: string
  dateInspected: string
  completedAt: string | null
  isOperational: boolean
  createdAt: string
  updatedAt: string
}

type Form = {
  id: string
  status: string
  fileNumber: string | null
  fieldWorkCompletedAt: string | null
  submittedAt: string | null
  createdAt: string
  updatedAt: string
}

type WmsReferralDetail = {
  inspectionId: string
  followUpStatus: string
  externalPmNumber: string | null
  revision: number
  canRecordReferral: boolean
  eligibilityMessage: string | null
  audit: {
    previousExternalPmNumber: string | null
    newExternalPmNumber: string
    revision: number
    changedByUserId: string
  }[]
}

async function login(
  request: APIRequestContext,
  email: string,
  userPassword: string,
) {
  const response = await request.post(`${apiBaseUrl}/api/v1/auth/login`, {
    data: { email, password: userPassword },
  })
  expect(response.status(), `Login failed for ${email}`).toBe(200)
  return (await response.json()) as LoginResponse
}

async function signIn(page: Page, email: string, userPassword: string) {
  await page.goto('/login')
  await page.getByLabel('Institutional email').fill(email)
  await page.getByLabel('Password').fill(userPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)
}

async function signOut(page: Page) {
  await page.getByRole('button', { name: 'Sign out' }).first().click()
  await expect(page).toHaveURL(/\/login$/)
}

function authHeaders(session: LoginResponse) {
  return { Authorization: `Bearer ${session.accessToken}` }
}

test('GSD, Supervisor, and Inspector complete an assigned PM batch through WMS referral', async ({
  page,
  request,
}, testInfo) => {
  test.skip(
    apiOrigin.hostname !== 'localhost' || apiOrigin.port !== '5262',
    'Live workflow is restricted to the owned disposable API on localhost:5262.',
  )
  test.skip(
    !password,
    'Set UNIPM_DEV_USER_PASSWORD for the isolated Development database.',
  )
  if (!password) return

  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const department = `PRE-EVAL-${suffix}`
  const building = 'Pre-evaluation synthetic building'
  const gsd = await login(request, 'gsd@unipm.local', password)
  expect(gsd.user.roles).toContain('GSD')
  const gsdHeaders = authHeaders(gsd)

  const assets = [] as {
    id: string
    assetCode: string
    assetCategory: string
  }[]
  for (const letter of ['A', 'B']) {
    const assetCode = `PRE-EVAL-FE-${suffix}-${letter}`
    const response = await request.post(`${apiBaseUrl}/api/v1/assets`, {
      headers: gsdHeaders,
      data: {
        assetCode,
        assetCategory: 'fire-extinguisher',
        building,
        department,
        location: `Synthetic workflow location ${letter}`,
      },
    })
    expect(response.status(), `Create synthetic asset ${letter}`).toBe(201)
    assets.push((await response.json()) as (typeof assets)[number])
  }

  const schedulesUrl = new URL(`${apiBaseUrl}/api/v1/schedules`)
  schedulesUrl.searchParams.set('department', department)
  schedulesUrl.searchParams.set('year', '2026')
  const initialSchedulesResponse = await request.get(schedulesUrl.toString(), {
    headers: gsdHeaders,
  })
  expect(initialSchedulesResponse.status()).toBe(200)
  const initialSchedules = (await initialSchedulesResponse.json()) as Schedule[]
  expect(initialSchedules).toHaveLength(2)
  expect(initialSchedules.map((schedule) => schedule.pmCycle)).toEqual([
    '2026-11',
    '2026-11',
  ])
  expect(
    initialSchedules.map((schedule) => schedule.asset?.assetCode).sort(),
  ).toEqual(assets.map((asset) => asset.assetCode).sort())
  const selectedSchedule = initialSchedules[0]
  expect(selectedSchedule).toBeDefined()
  const scheduleIds = initialSchedules.map((schedule) => schedule.id)

  const supervisorOptionsResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules/supervisor-assignment-options`,
    { headers: gsdHeaders },
  )
  expect(supervisorOptionsResponse.status()).toBe(200)
  const supervisorOptions = (await supervisorOptionsResponse.json()) as {
    supervisors: { id: string; displayName: string }[]
  }
  const supervisor = supervisorOptions.supervisors.find(
    (option) => option.displayName === 'Maintenance Supervisor',
  )
  expect(supervisor).toBeDefined()

  const gsdWorkerOptions = await request.get(
    `${apiBaseUrl}/api/v1/schedules/assignment-options`,
    { headers: gsdHeaders },
  )
  expect(gsdWorkerOptions.status()).toBe(403)
  const gsdWorkerAssignment = await request.put(
    `${apiBaseUrl}/api/v1/schedules/${selectedSchedule!.id}/assignment`,
    { headers: gsdHeaders, data: { workerUserId: gsd.user.id } },
  )
  expect(gsdWorkerAssignment.status()).toBe(403)

  const inspector = await login(request, 'inspector@unipm.local', password)
  expect(inspector.user.roles).toContain('Inspector')
  const inspectorHeaders = authHeaders(inspector)
  const inspectorBeforeResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules`,
    { headers: inspectorHeaders },
  )
  expect(inspectorBeforeResponse.status()).toBe(200)
  const inspectorBefore = (await inspectorBeforeResponse.json()) as Schedule[]
  expect(
    inspectorBefore.some((schedule) => scheduleIds.includes(schedule.id)),
  ).toBe(false)

  await page.setViewportSize({ width: 1440, height: 1000 })
  await signIn(page, 'gsd@unipm.local', password)
  await page.goto(`/app/schedules/${selectedSchedule!.id}`)
  await expect(
    page.getByRole('heading', { name: 'Batch assignment' }),
  ).toBeVisible()
  await expect(page.getByLabel('Supervisor (oversight)')).toBeVisible()
  await expect(page.getByLabel('Skilled worker (Inspector)')).toHaveCount(0)
  await page.getByLabel('Supervisor (oversight)').selectOption(supervisor!.id)

  const gsdAssignmentPromise = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname ===
        `/api/v1/schedules/${selectedSchedule!.id}/supervisor-assignment`,
  )
  await page
    .getByRole('button', { name: 'Assign Supervisor to entire batch' })
    .click()
  const gsdAssignmentResponse = await gsdAssignmentPromise
  expect(gsdAssignmentResponse.status()).toBe(200)
  const gsdAssignment = (await gsdAssignmentResponse.json()) as BatchAssignment
  expect(gsdAssignment.department).toBe(department.toUpperCase())
  expect(gsdAssignment.pmCycle).toBe('2026-11')
  expect(gsdAssignment.scheduleIds.sort()).toEqual(scheduleIds.sort())
  expect(gsdAssignment.supervisorUserId).toBe(supervisor!.id)
  expect(gsdAssignment.workerUserId).toBeNull()
  await expect(
    page.getByText('Supervisor assignment saved for 2 schedules.'),
  ).toBeVisible()
  await expect(page.getByLabel('Skilled worker (Inspector)')).toHaveCount(0)
  await page.screenshot({
    path: testInfo.outputPath('gsd-supervisor-stage.png'),
    fullPage: true,
  })

  const gsdUpdatedSchedules = await Promise.all(
    scheduleIds.map(async (scheduleId) => {
      const response = await request.get(
        `${apiBaseUrl}/api/v1/schedules/${scheduleId}`,
        { headers: gsdHeaders },
      )
      expect(response.status()).toBe(200)
      return (await response.json()) as Schedule
    }),
  )
  expect(gsdUpdatedSchedules).toHaveLength(2)
  for (const schedule of gsdUpdatedSchedules) {
    expect(schedule.assignedSupervisorUserId).toBe(supervisor!.id)
    expect(schedule.assignedToUserId).toBeNull()
  }

  const supervisorSession = await login(
    request,
    'supervisor@unipm.local',
    password,
  )
  expect(supervisorSession.user.roles).toContain('Supervisor')
  const supervisorHeaders = authHeaders(supervisorSession)
  const workerOptionsResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules/assignment-options`,
    { headers: supervisorHeaders },
  )
  expect(workerOptionsResponse.status()).toBe(200)
  const workerOptions = (await workerOptionsResponse.json()) as {
    workers: { id: string; displayName: string }[]
  }
  const worker = workerOptions.workers.find(
    (option) => option.id === inspector.user.id,
  )
  expect(worker).toBeDefined()
  const supervisorStageOptions = await request.get(
    `${apiBaseUrl}/api/v1/schedules/supervisor-assignment-options`,
    { headers: supervisorHeaders },
  )
  expect(supervisorStageOptions.status()).toBe(403)

  await signOut(page)
  await signIn(page, 'supervisor@unipm.local', password)
  await page.goto(`/app/schedules/${selectedSchedule!.id}`)
  await expect(
    page.getByRole('heading', { name: 'Batch assignment' }),
  ).toBeVisible()
  await expect(page.getByLabel('Skilled worker (Inspector)')).toBeVisible()
  await expect(page.getByLabel('Supervisor (oversight)')).toHaveCount(0)
  await page.getByLabel('Skilled worker (Inspector)').selectOption(worker!.id)

  const supervisorAssignmentPromise = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname ===
        `/api/v1/schedules/${selectedSchedule!.id}/assignment`,
  )
  await page
    .getByRole('button', { name: 'Assign Inspector to entire batch' })
    .click()
  const supervisorAssignmentResponse = await supervisorAssignmentPromise
  expect(supervisorAssignmentResponse.status()).toBe(200)
  const supervisorAssignment =
    (await supervisorAssignmentResponse.json()) as BatchAssignment
  expect(supervisorAssignment.scheduleIds.sort()).toEqual(scheduleIds.sort())
  expect(supervisorAssignment.supervisorUserId).toBe(supervisorSession.user.id)
  expect(supervisorAssignment.workerUserId).toBe(inspector.user.id)
  await expect(
    page.getByText('Inspector assignment saved for 2 schedules.'),
  ).toBeVisible()
  await page.screenshot({
    path: testInfo.outputPath('supervisor-inspector-stage.png'),
    fullPage: true,
  })

  const inspectorSchedulesResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules`,
    { headers: inspectorHeaders },
  )
  expect(inspectorSchedulesResponse.status()).toBe(200)
  const inspectorSchedules =
    (await inspectorSchedulesResponse.json()) as Schedule[]
  expect(
    inspectorSchedules.filter((schedule) => scheduleIds.includes(schedule.id)),
  ).toHaveLength(2)
  const inspectorWorkerAssignment = await request.put(
    `${apiBaseUrl}/api/v1/schedules/${selectedSchedule!.id}/assignment`,
    {
      headers: inspectorHeaders,
      data: { workerUserId: inspector.user.id },
    },
  )
  expect(inspectorWorkerAssignment.status()).toBe(403)
  const inspectorSupervisorOptions = await request.get(
    `${apiBaseUrl}/api/v1/schedules/supervisor-assignment-options`,
    { headers: inspectorHeaders },
  )
  expect(inspectorSupervisorOptions.status()).toBe(403)

  await signOut(page)
  await signIn(page, 'inspector@unipm.local', password)
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
  await page.screenshot({
    path: testInfo.outputPath('inspector-assigned-task.png'),
    fullPage: true,
  })

  const draftResponse = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms`,
    {
      headers: inspectorHeaders,
      data: {
        assetCategory: 'fire-extinguisher',
        building,
        department,
        periodType: 'Quarter',
        quarter: 'Q4',
        year: 2026,
      },
    },
  )
  expect(draftResponse.status()).toBe(201)
  const draft = (await draftResponse.json()) as Form

  const inspectedAt = new Date().toISOString()
  const firstInspectionResponse = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/inspections`,
    {
      headers: inspectorHeaders,
      data: {
        scheduleId: scheduleIds[0],
        inspectorUserId: inspector.user.id,
        dateInspected: inspectedAt,
        isOperational: false,
        remarks: 'Synthetic inspection found a low pressure reading.',
        actionsRecommendations:
          'Recharge the extinguisher and verify pressure.',
      },
    },
  )
  expect(firstInspectionResponse.status()).toBe(201)
  const nonOperationalInspection =
    (await firstInspectionResponse.json()) as DraftInspection
  expect(nonOperationalInspection.isOperational).toBe(false)
  expect(nonOperationalInspection.completedAt).not.toBeNull()

  const scheduleAfterFirstInspectionResponse = await request.get(
    `${apiBaseUrl}/api/v1/schedules`,
    { headers: inspectorHeaders },
  )
  expect(scheduleAfterFirstInspectionResponse.status()).toBe(200)
  const schedulesAfterFirstInspection =
    (await scheduleAfterFirstInspectionResponse.json()) as Schedule[]
  const firstCompletedSchedule = schedulesAfterFirstInspection.find(
    (schedule) => schedule.id === scheduleIds[0],
  )
  expect(firstCompletedSchedule?.status).toBe('Completed')
  expect(firstCompletedSchedule?.completedAt).not.toBeNull()

  const reassignmentAfterWork = await request.put(
    `${apiBaseUrl}/api/v1/schedules/${scheduleIds[0]}/assignment`,
    {
      headers: supervisorHeaders,
      data: { workerUserId: inspector.user.id },
    },
  )
  expect(reassignmentAfterWork.status()).toBe(409)
  const incompleteSubmit = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/submit`,
    { headers: inspectorHeaders },
  )
  expect(incompleteSubmit.status()).toBe(409)

  const secondInspectionResponse = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/inspections`,
    {
      headers: inspectorHeaders,
      data: {
        scheduleId: scheduleIds[1],
        inspectorUserId: inspector.user.id,
        dateInspected: inspectedAt,
        isOperational: true,
        remarks: 'Synthetic inspection passed the operational check.',
        actionsRecommendations: null,
      },
    },
  )
  expect(secondInspectionResponse.status()).toBe(201)
  const operationalInspection =
    (await secondInspectionResponse.json()) as DraftInspection
  expect(operationalInspection.isOperational).toBe(true)
  expect(operationalInspection.completedAt).not.toBeNull()

  const scheduleSnapshots = await Promise.all(
    scheduleIds.map(async (scheduleId) => {
      const response = await request.get(
        `${apiBaseUrl}/api/v1/schedules/${scheduleId}`,
        { headers: gsdHeaders },
      )
      expect(response.status()).toBe(200)
      const schedule = (await response.json()) as Schedule
      expect(schedule.status).toBe('Completed')
      expect(schedule.completedAt).not.toBeNull()
      return {
        id: schedule.id,
        status: schedule.status,
        completedAt: schedule.completedAt,
        updatedAt: schedule.updatedAt,
      }
    }),
  )

  const gsdEligibilityBeforeAcknowledgement = await request.get(
    `${apiBaseUrl}/api/v1/inspections/${nonOperationalInspection.id}/wms-referral`,
    { headers: gsdHeaders },
  )
  expect(gsdEligibilityBeforeAcknowledgement.status()).toBe(200)
  const pendingEligibility =
    (await gsdEligibilityBeforeAcknowledgement.json()) as WmsReferralDetail
  expect(pendingEligibility.canRecordReferral).toBe(false)

  const submitResponse = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/submit`,
    { headers: inspectorHeaders },
  )
  expect(submitResponse.status()).toBe(200)
  const submittedForm = (await submitResponse.json()) as Form
  expect(submittedForm.status).toBe('Submitted')
  expect(submittedForm.fileNumber).toBeTruthy()
  expect(submittedForm.fieldWorkCompletedAt).not.toBeNull()

  const acknowledgementResponse = await request.post(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/acknowledge`,
    {
      headers: inspectorHeaders,
      data: {
        signatoryName: 'Synthetic Department Head',
        signatoryPosition: 'Department Head',
        signatureContentType: 'image/png',
        signatureData: pngSignatureBase64,
      },
    },
  )
  expect(acknowledgementResponse.status()).toBe(200)

  const acknowledgedFormResponse = await request.get(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}`,
    { headers: inspectorHeaders },
  )
  expect(acknowledgedFormResponse.status()).toBe(200)
  const acknowledgedForm = (await acknowledgedFormResponse.json()) as Form
  expect(acknowledgedForm.status).toBe('Acknowledged')
  const formSnapshot = {
    id: acknowledgedForm.id,
    status: acknowledgedForm.status,
    fileNumber: acknowledgedForm.fileNumber,
    fieldWorkCompletedAt: acknowledgedForm.fieldWorkCompletedAt,
    submittedAt: acknowledgedForm.submittedAt,
    createdAt: acknowledgedForm.createdAt,
    updatedAt: acknowledgedForm.updatedAt,
  }

  const handoffResponse = await request.get(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}/corrective-handoff`,
    { headers: gsdHeaders },
  )
  expect(handoffResponse.status()).toBe(200)
  const handoff = (await handoffResponse.json()) as {
    rows: {
      inspectionId: string
      isOperational: boolean
      canRecordWmsReferral: boolean
      wmsPmNumber: string | null
    }[]
  }
  expect(handoff.rows).toHaveLength(1)
  expect(handoff.rows[0].inspectionId).toBe(nonOperationalInspection.id)
  expect(handoff.rows[0].isOperational).toBe(false)
  expect(handoff.rows[0].canRecordWmsReferral).toBe(true)
  expect(handoff.rows[0].wmsPmNumber).toBeNull()

  const eligibleReferralResponse = await request.get(
    `${apiBaseUrl}/api/v1/inspections/${nonOperationalInspection.id}/wms-referral`,
    { headers: gsdHeaders },
  )
  expect(eligibleReferralResponse.status()).toBe(200)
  const eligibleReferral =
    (await eligibleReferralResponse.json()) as WmsReferralDetail
  expect(eligibleReferral.canRecordReferral).toBe(true)
  expect(eligibleReferral.externalPmNumber).toBeNull()

  const operationalEligibilityResponse = await request.get(
    `${apiBaseUrl}/api/v1/inspections/${operationalInspection.id}/wms-referral`,
    { headers: gsdHeaders },
  )
  expect(operationalEligibilityResponse.status()).toBe(200)
  const operationalEligibility =
    (await operationalEligibilityResponse.json()) as WmsReferralDetail
  expect(operationalEligibility.canRecordReferral).toBe(false)
  expect(operationalEligibility.eligibilityMessage).toContain(
    'Only non-operational inspections',
  )

  const dashboardUrl = new URL(`${apiBaseUrl}/api/v1/pm-period-dashboard`)
  dashboardUrl.searchParams.set('pmCycle', '2026-11')
  dashboardUrl.searchParams.set('assetCategory', 'fire-extinguisher')
  dashboardUrl.searchParams.set('department', department)
  const dashboardResponse = await request.get(dashboardUrl.toString(), {
    headers: gsdHeaders,
  })
  expect(dashboardResponse.status()).toBe(200)
  const dashboard = (await dashboardResponse.json()) as {
    scheduled: number
    inspected: number
    nonOperational: number
    batches: { formStatus: string | null; isAcknowledged: boolean }[]
    assets: { scheduleId: string; scheduleStatus: string }[]
  }
  expect(dashboard.scheduled).toBe(2)
  expect(dashboard.inspected).toBe(2)
  expect(dashboard.nonOperational).toBe(1)
  expect(dashboard.batches).toEqual([
    expect.objectContaining({
      formStatus: 'Acknowledged',
      isAcknowledged: true,
    }),
  ])
  expect(dashboard.assets).toHaveLength(2)
  expect(dashboard.assets.map((asset) => asset.scheduleStatus)).toEqual([
    'Completed',
    'Completed',
  ])

  const externalPmNumber = `WMS-PRE-EVAL-${suffix}`
  const recordReferral = await request.put(
    `${apiBaseUrl}/api/v1/inspections/${nonOperationalInspection.id}/wms-referral`,
    {
      headers: gsdHeaders,
      data: { externalPmNumber, expectedRevision: 0 },
    },
  )
  expect(recordReferral.status()).toBe(200)
  const recordedReferral = (await recordReferral.json()) as {
    externalPmNumber: string
    revision: number
    followUpStatus: string
  }
  expect(recordedReferral.externalPmNumber).toBe(externalPmNumber)
  expect(recordedReferral.revision).toBe(1)
  expect(recordedReferral.followUpStatus).toBe('ReferredToWms')

  const referralAuditResponse = await request.get(
    `${apiBaseUrl}/api/v1/inspections/${nonOperationalInspection.id}/wms-referral`,
    { headers: gsdHeaders },
  )
  expect(referralAuditResponse.status()).toBe(200)
  const referralAudit =
    (await referralAuditResponse.json()) as WmsReferralDetail
  expect(referralAudit.audit).toHaveLength(1)
  expect(referralAudit.audit[0]).toMatchObject({
    previousExternalPmNumber: null,
    newExternalPmNumber: externalPmNumber,
    revision: 1,
    changedByUserId: gsd.user.id,
  })

  const inspectionAfterReferralResponse = await request.get(
    `${apiBaseUrl}/api/v1/inspections/${nonOperationalInspection.id}`,
    { headers: gsdHeaders },
  )
  expect(inspectionAfterReferralResponse.status()).toBe(200)
  const inspectionAfterReferral = await inspectionAfterReferralResponse.json()
  expect(inspectionAfterReferral).toMatchObject({
    id: nonOperationalInspection.id,
    scheduleId: nonOperationalInspection.scheduleId,
    isOperational: false,
    dateInspected: nonOperationalInspection.dateInspected,
    externalPmNumber,
    wmsReferralRevision: 1,
    correctiveFollowUpStatus: 'ReferredToWms',
    createdAt: nonOperationalInspection.createdAt,
    updatedAt: nonOperationalInspection.updatedAt,
  })

  const formAfterReferralResponse = await request.get(
    `${apiBaseUrl}/api/v1/preventive-maintenance-forms/${draft.id}`,
    { headers: inspectorHeaders },
  )
  expect(formAfterReferralResponse.status()).toBe(200)
  const formAfterReferral = (await formAfterReferralResponse.json()) as Form
  expect({
    id: formAfterReferral.id,
    status: formAfterReferral.status,
    fileNumber: formAfterReferral.fileNumber,
    fieldWorkCompletedAt: formAfterReferral.fieldWorkCompletedAt,
    submittedAt: formAfterReferral.submittedAt,
    createdAt: formAfterReferral.createdAt,
    updatedAt: formAfterReferral.updatedAt,
  }).toEqual(formSnapshot)

  const dashboardAfterReferralResponse = await request.get(
    dashboardUrl.toString(),
    { headers: gsdHeaders },
  )
  expect(dashboardAfterReferralResponse.status()).toBe(200)
  expect(await dashboardAfterReferralResponse.json()).toEqual(dashboard)

  const finalSchedules = await Promise.all(
    scheduleIds.map(async (scheduleId) => {
      const response = await request.get(
        `${apiBaseUrl}/api/v1/schedules/${scheduleId}`,
        { headers: gsdHeaders },
      )
      expect(response.status()).toBe(200)
      return (await response.json()) as Schedule
    }),
  )
  expect(
    finalSchedules.map((schedule) => ({
      id: schedule.id,
      status: schedule.status,
      completedAt: schedule.completedAt,
      updatedAt: schedule.updatedAt,
    })),
  ).toEqual(scheduleSnapshots)

  const filteredInspectionsUrl = new URL(`${apiBaseUrl}/api/v1/inspections`)
  filteredInspectionsUrl.searchParams.set('department', department)
  filteredInspectionsUrl.searchParams.set('search', externalPmNumber)
  filteredInspectionsUrl.searchParams.set('wmsReferralStatus', 'ReferredToWms')
  const filteredInspectionsResponse = await request.get(
    filteredInspectionsUrl.toString(),
    { headers: gsdHeaders },
  )
  expect(filteredInspectionsResponse.status()).toBe(200)
  const filteredInspections = (await filteredInspectionsResponse.json()) as {
    id: string
    externalPmNumber: string | null
    correctiveFollowUpStatus: string
  }[]
  expect(filteredInspections).toEqual([
    expect.objectContaining({
      id: nonOperationalInspection.id,
      externalPmNumber,
      correctiveFollowUpStatus: 'ReferredToWms',
    }),
  ])
})
