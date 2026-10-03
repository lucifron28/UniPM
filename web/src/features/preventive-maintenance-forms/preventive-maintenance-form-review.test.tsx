import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'
import { FormRegistry } from '@/features/preventive-maintenance-forms/form-registry'
import { PmAcknowledgementReview } from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'
import { useAcknowledgePreventiveMaintenanceFormMutation } from '@/features/preventive-maintenance-forms/form-queries'
import type { PmPeriodDashboardSearch } from '@/features/reports/pm-period-dashboard'
import { usePmPeriodDashboard } from '@/features/reports/pm-period-dashboard-queries'
import { AppShell } from '@/components/layout/app-shell'
import { routeTree } from '@/routeTree.gen'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const meUrl = '*/api/v1/auth/me'
const formsUrl = '*/api/v1/preventive-maintenance-forms'
const formId = '22222222-2222-4222-8222-222222222222'
const inspectionId = '33333333-3333-4333-8333-333333333333'
const scheduleId = '44444444-4444-4444-8444-444444444444'
const assetId = '55555555-5555-4555-8555-555555555555'
const inspectorId = '66666666-6666-4666-8666-666666666666'

const timestamps = {
  createdAt: '2026-07-29T00:00:00Z',
  updatedAt: '2026-07-29T00:00:00Z',
}

function form(status: 'Draft' | 'Submitted' | 'Acknowledged') {
  return {
    id: formId,
    fileNumber: status === 'Draft' ? null : `GSD-${status.toUpperCase()}-001`,
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    periodType: 'Quarter',
    quarter: 'Q3',
    semester: null,
    year: 2026,
    academicYear: '2026-2027',
    status,
    createdByUserId: inspectorId,
    submittedByUserId: status === 'Draft' ? null : inspectorId,
    submittedAt: status === 'Draft' ? null : '2026-07-29T01:00:00Z',
    ...timestamps,
    inspections:
      status === 'Draft'
        ? []
        : [
            {
              id: inspectionId,
              scheduleId,
              assetId,
              inspectorUserId: inspectorId,
              dateInspected: '2026-07-28T02:00:00Z',
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
              ...timestamps,
            },
          ],
  }
}

const allDepartmentDashboardSearch: PmPeriodDashboardSearch = {
  assetCategory: 'fire-extinguisher',
  year: 2026,
  pmCycle: '2026-07',
  condition: 'NonOperational',
  timeliness: 'Late',
  search: 'FE-TEST-001',
}
const selectedDepartmentDashboardSearch: PmPeriodDashboardSearch = {
  ...allDepartmentDashboardSearch,
  department: 'GSD',
}

function installDashboardNavigationHandlers(
  status: 'Submitted' | 'Acknowledged',
) {
  const acknowledgedAt =
    status === 'Acknowledged' ? '2026-07-30T02:00:00Z' : null
  const reviewForm = {
    ...form(status),
    pmCycle: '2026-07',
    fieldWorkCompletedAt: '2026-07-28T03:00:00Z',
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
    formStatus: status,
    fileNumber: reviewForm.fileNumber,
    fieldWorkCompletedAt: '2026-07-28T03:00:00Z',
    submittedAt: '2026-07-29T01:00:00Z',
    isAcknowledged: status === 'Acknowledged',
    acknowledgedAt,
  }

  server.use(
    http.get(meUrl, () => HttpResponse.json(currentUser(['Inspector']))),
    http.get(formsUrl, () => HttpResponse.json([reviewForm])),
    http.get(`${formsUrl}/${formId}`, () => HttpResponse.json(reviewForm)),
    http.get('*/api/v1/pm-period-dashboard/cycles', () =>
      HttpResponse.json([
        {
          assetCategory: 'fire-extinguisher',
          year: 2026,
          cycles: [{ pmCycle: '2026-07', scheduled: 1 }],
        },
      ]),
    ),
    http.get('*/api/v1/pm-period-dashboard', () =>
      HttpResponse.json({
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
        assets: [
          {
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
            formStatus: status,
            isAcknowledged: status === 'Acknowledged',
            acknowledgedAt,
          },
        ],
      }),
    ),
    http.get(`*/api/v1/inspections/${inspectionId}`, () =>
      HttpResponse.json({
        id: inspectionId,
        scheduleId,
        assetId,
        inspectorUserId: inspectorId,
        dateInspected: '2026-07-28T02:00:00Z',
        isOperational: false,
        remarks: 'Pressure is low.',
        actionsRecommendations: 'Inspect and recharge the unit.',
        createdAt: timestamps.createdAt,
        updatedAt: timestamps.updatedAt,
      }),
    ),
    http.get(`*/api/v1/assets/${assetId}`, () =>
      HttpResponse.json({
        id: assetId,
        assetCode: 'FE-TEST-001',
        assetCategory: 'fire-extinguisher',
        building: 'Main Building',
        department: 'GSD',
        location: 'Main hallway',
        hasVerificationLocation: false,
        qrCodeValue: null,
        status: 'Active',
        ...timestamps,
      }),
    ),
    http.get(`*/api/v1/schedules/${scheduleId}`, () =>
      HttpResponse.json({
        id: scheduleId,
        assetId,
        scheduleDate: '2026-07-01T00:00:00Z',
        pmCycle: '2026-07',
        periodType: 'Quarter',
        status: 'Completed',
        quarter: 'Q3',
        semester: null,
        year: 2026,
        academicYear: '2026-2027',
        assignedToUserId: null,
        assignedSupervisorUserId: null,
        completedAt: '2026-07-28T03:00:00Z',
        createdAt: timestamps.createdAt,
        updatedAt: timestamps.updatedAt,
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

function setupAuth() {
  useAuthStore.getState().establishSession('synthetic-test-token')
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
}

function renderWithProviders(ui: React.ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const rootRoute = createRootRoute({
    component: () => (
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    ),
  })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })

  return render(<RouterProvider router={router} />)
}

function renderAppRouter(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const router = createRouter({
    routeTree,
    context: {
      queryClient,
      getAccessToken: () => useAuthStore.getState().accessToken,
    },
    history: createMemoryHistory({
      initialEntries: [initialEntry],
    }),
  })

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

function renderDashboardReviewWithProviders(
  initialSearch: PmPeriodDashboardSearch,
) {
  const searchParams = new URLSearchParams()
  for (const [key, value] of Object.entries(initialSearch)) {
    if (value !== undefined) searchParams.set(key, String(value))
  }
  return renderAppRouter(`/app/dashboard?${searchParams}`)
}

function currentUser(roles: string[]) {
  return {
    id: inspectorId,
    email: 'reviewer@example.test',
    displayName: 'Synthetic Reviewer',
    roles,
  }
}

describe('preventive-maintenance form review', () => {
  beforeEach(() => {
    setupAuth()
  })

  it('renders lifecycle labels and registry metadata', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(formsUrl, () =>
        HttpResponse.json([
          form('Draft'),
          { ...form('Submitted'), id: '77777777-7777-4777-8777-777777777777' },
          {
            ...form('Acknowledged'),
            id: '88888888-8888-4888-8888-888888888888',
          },
        ]),
      ),
    )

    renderWithProviders(<FormRegistry />)

    expect(
      await screen.findByRole('heading', { name: 'Form review' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Draft')).toBeInTheDocument()
    expect(screen.getByText('Awaiting acknowledgement')).toBeInTheDocument()
    expect(screen.getByText('Acknowledged')).toBeInTheDocument()
    expect(screen.getAllByText('Main Building / GSD')).toHaveLength(3)
    expect(screen.getAllByText('Inspection rows')).toHaveLength(3)
    expect(screen.queryByText(formId)).not.toBeInTheDocument()
    expect(
      screen.queryByText('77777777-7777-4777-8777-777777777777'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText('88888888-8888-4888-8888-888888888888'),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'GSD-SUBMITTED-001' }),
    ).toHaveAttribute(
      'href',
      '/app/preventive-maintenance-forms/77777777-7777-4777-8777-777777777777?returnContext=%7B%22kind%22%3A%22formRegistry%22%7D',
    )
  })

  it('renders human-readable row context and water-station work items', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json({
          ...form('Submitted'),
          assetCategory: 'water-drinking-station',
          inspections: [
            {
              ...form('Submitted').inspections[0],
              assetCode: 'WDS-MAIN-001',
              location: 'Main Building lobby',
              skilledWorkerIdentity: 'Synthetic Inspector',
              dateAccomplished: '2026-07-28T03:00:00Z',
              waterReplaceCarbonFilter: true,
              waterReplaceSedimentFilter: false,
              waterCheckUvLight: true,
            },
          ],
        }),
      ),
    )

    renderWithProviders(<FormDetail formId={formId} />)

    expect(await screen.findAllByText('WDS-MAIN-001')).toHaveLength(2)
    expect(screen.getByText('Main Building lobby')).toBeInTheDocument()
    expect(screen.getByText('Synthetic Inspector')).toBeInTheDocument()
    expect(screen.getByText('Awaiting acknowledgement')).toBeInTheDocument()
    expect(screen.getByText('Recommendation')).toBeInTheDocument()
    expect(
      screen.getByText('Water drinking station work items'),
    ).toBeInTheDocument()
    expect(screen.getByText('Replace carbon filter')).toBeInTheDocument()
    expect(screen.getByText('Replace sediment filter')).toBeInTheDocument()
    expect(screen.getByText('Check UV light')).toBeInTheDocument()
  })

  it('provides role-aware form review navigation in the app shell', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))))

    renderWithProviders(<AppShell />)

    expect(
      await screen.findByText('Preventive Maintenance Portal'),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('link', { name: 'Form review' }),
    ).toBeInTheDocument()
  })

  it('renders acknowledged detail and GSD corrective handoff without signatures', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json(form('Acknowledged')),
      ),
      http.get(`${formsUrl}/${formId}/corrective-handoff`, () =>
        HttpResponse.json({
          formId,
          fileNumber: 'GSD-ACKNOWLEDGED-001',
          acknowledgedAt: '2026-07-29T02:00:00Z',
          department: 'GSD',
          building: 'Main Building',
          assetCategory: 'fire-extinguisher',
          hasCorrectiveActionRows: true,
          rows: [
            {
              inspectionId,
              inspectionDate: '2026-07-28T02:00:00Z',
              assetDeviceNumber: null,
              assetCode: 'FE-001',
              location: 'Room 101',
              findingOrRemarks: 'Pressure is low.',
              isOperational: false,
              recommendedCorrectiveAction: 'Inspect and recharge the unit.',
              skilledWorkerUserId: inspectorId,
              skilledWorkerIdentity: 'Synthetic Reviewer',
            },
          ],
        }),
      ),
    )

    renderWithProviders(<FormDetail formId={formId} />)

    expect(
      await screen.findByRole('heading', {
        name: 'Corrective-action findings',
      }),
    ).toBeInTheDocument()
    const handoffHeading = screen.getByRole('heading', { name: 'FE-001' })
    expect(handoffHeading).toBeInTheDocument()
    const handoffCard = handoffHeading.closest('article')
    if (!(handoffCard instanceof HTMLElement)) {
      throw new Error('Corrective-action finding card was not found')
    }
    expect(handoffCard).toHaveTextContent('Actual inspection date:')
    const handoff = within(handoffCard)
    const technicalSummary = handoff.getByText('Technical identifiers')
    const technicalDetails = technicalSummary.closest('details')
    if (!(technicalDetails instanceof HTMLDetailsElement)) {
      throw new Error('Technical identifiers details section was not found')
    }
    expect(technicalDetails).not.toHaveAttribute('open')
    fireEvent.click(technicalSummary)
    expect(within(technicalDetails).getByText(inspectionId)).toBeInTheDocument()
    expect(screen.getAllByText('Not operational')).toHaveLength(2)
    expect(screen.getByText('Unresolved')).toBeInTheDocument()
    expect(screen.getByText('FE-001')).toBeInTheDocument()
    expect(screen.getByText(scheduleId)).toBeInTheDocument()
    expect(screen.getAllByText(inspectorId)).toHaveLength(4)
    expect(screen.queryByText('Completed')).not.toBeInTheDocument()
    expect(screen.queryByText('signatureData')).not.toBeInTheDocument()
    expect(screen.queryByText('signatureChecksum')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Acknowledge submitted form' }),
    ).not.toBeInTheDocument()
  })

  it('does not offer acknowledgement for a Draft form', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/${formId}`, () => HttpResponse.json(form('Draft'))),
    )

    renderWithProviders(<FormDetail formId={formId} />)

    expect(
      await screen.findByRole('heading', { name: 'Inspection rows' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Acknowledge submitted form' }),
    ).not.toBeInTheDocument()
  })

  it('does not request corrective handoff for Inspector users', async () => {
    let handoffRequested = false
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['Inspector']))),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json(form('Acknowledged')),
      ),
      http.get(`${formsUrl}/${formId}/corrective-handoff`, () => {
        handoffRequested = true
        return HttpResponse.json({})
      }),
    )

    renderWithProviders(<FormDetail formId={formId} />)

    expect(
      await screen.findByRole('heading', { name: 'Inspection rows' }),
    ).toBeInTheDocument()
    await waitFor(() => expect(handoffRequested).toBe(false))
    expect(
      screen.queryByRole('heading', { name: 'Corrective-action findings' }),
    ).not.toBeInTheDocument()
  })

  it('acknowledges a submitted form without exposing signature payloads', async () => {
    let acknowledgementBody: Record<string, unknown> | undefined
    let acknowledged = false
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json(form(acknowledged ? 'Acknowledged' : 'Submitted')),
      ),
      http.get(`${formsUrl}/${formId}/corrective-handoff`, () =>
        HttpResponse.json({
          formId,
          fileNumber: 'GSD-ACKNOWLEDGED-001',
          acknowledgedAt: '2026-07-29T02:00:00Z',
          department: 'GSD',
          building: 'Main Building',
          assetCategory: 'fire-extinguisher',
          hasCorrectiveActionRows: false,
          rows: [],
        }),
      ),
      http.post(`${formsUrl}/${formId}/acknowledge`, async ({ request }) => {
        acknowledged = true
        acknowledgementBody = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({
          id: '99999999-9999-4999-8999-999999999999',
          formId,
          signatoryName: 'Synthetic Department Head',
          signatoryPosition: 'Department Head',
          signatureContentType: 'image/png',
          signatureChecksum: 'not-displayed',
          capturedByUserId: inspectorId,
          acknowledgedAt: '2026-07-29T02:00:00Z',
        })
      }),
    )
    const toDataUrl = vi
      .spyOn(HTMLCanvasElement.prototype, 'toDataURL')
      .mockReturnValue('data:image/png;base64,iVBORw0KGgo=')
    const canvasContext = {
      beginPath: vi.fn(),
      moveTo: vi.fn(),
      lineTo: vi.fn(),
      stroke: vi.fn(),
      clearRect: vi.fn(),
    } as unknown as CanvasRenderingContext2D
    const getContext = vi
      .spyOn(HTMLCanvasElement.prototype, 'getContext')
      .mockReturnValue(canvasContext)

    renderWithProviders(<FormDetail formId={formId} />)

    expect(
      await screen.findByRole('heading', {
        name: 'Acknowledge submitted form',
      }),
    ).toBeInTheDocument()
    const acknowledgementCopy = screen.getByText(
      /Field-work completion and acknowledgement are separate/,
    )
    expect(acknowledgementCopy).toHaveTextContent(
      /For the whole PM batch, acknowledgement records receipt\/noting, locks the form, and makes its inspection rows eligible for official history\./,
    )
    expect(acknowledgementCopy).toHaveTextContent(
      /It does not approve corrective work, funding, or an RMRF\./,
    )
    fireEvent.change(screen.getByLabelText('Signatory name'), {
      target: { value: 'Synthetic Department Head' },
    })
    fireEvent.change(screen.getByLabelText('Signatory position'), {
      target: { value: 'Department Head' },
    })
    const signatureCanvas = screen.getByLabelText('Signature')
    const bounds = {
      x: 0,
      y: 0,
      left: 0,
      top: 0,
      right: 640,
      bottom: 180,
      width: 640,
      height: 180,
      toJSON: () => undefined,
    } as DOMRect
    const getBoundingClientRect = vi
      .spyOn(signatureCanvas, 'getBoundingClientRect')
      .mockReturnValue(bounds)
    fireEvent.pointerDown(signatureCanvas, {
      clientX: 20,
      clientY: 20,
      pointerId: 1,
    })
    fireEvent.pointerUp(signatureCanvas, { pointerId: 1 })
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge form' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Capture the department-head signature before continuing.',
    )
    expect(
      screen.queryByRole('dialog', {
        name: 'Confirm department-head acknowledgement',
      }),
    ).not.toBeInTheDocument()
    expect(acknowledgementBody).toBeUndefined()

    fireEvent.pointerDown(signatureCanvas, {
      clientX: 20,
      clientY: 20,
      pointerId: 1,
    })
    fireEvent.pointerMove(signatureCanvas, {
      clientX: 100,
      clientY: 100,
      pointerId: 1,
    })
    fireEvent.pointerUp(signatureCanvas, { pointerId: 1 })
    expect(canvasContext.stroke).toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge form' }))

    expect(
      screen.getByRole('dialog', {
        name: 'Confirm department-head acknowledgement',
      }),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    fireEvent.click(screen.getByRole('button', { name: 'Clear' }))
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge form' }))
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Capture the department-head signature before continuing.',
    )
    expect(
      screen.queryByRole('dialog', {
        name: 'Confirm department-head acknowledgement',
      }),
    ).not.toBeInTheDocument()

    fireEvent.pointerDown(signatureCanvas, {
      clientX: 20,
      clientY: 20,
      pointerId: 1,
    })
    fireEvent.pointerMove(signatureCanvas, {
      clientX: 100,
      clientY: 100,
      pointerId: 1,
    })
    fireEvent.pointerUp(signatureCanvas, { pointerId: 1 })
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge form' }))
    expect(
      screen.getByRole('dialog', {
        name: 'Confirm department-head acknowledgement',
      }),
    ).toBeInTheDocument()
    expect(
      within(
        screen.getByRole('dialog', {
          name: 'Confirm department-head acknowledgement',
        }),
      ).getByText(/makes its inspection rows eligible for official history/),
    ).toBeInTheDocument()
    expect(
      within(
        screen.getByRole('dialog', {
          name: 'Confirm department-head acknowledgement',
        }),
      ).getByText(/does not approve corrective work, funding, or an RMRF/),
    ).toBeInTheDocument()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm acknowledgement' }),
    )

    await waitFor(() => expect(acknowledgementBody).toBeDefined())
    expect(toDataUrl).toHaveBeenCalledWith('image/png')
    expect(acknowledgementBody).toMatchObject({
      signatoryName: 'Synthetic Department Head',
      signatoryPosition: 'Department Head',
      signatureContentType: 'image/png',
    })
    expect(acknowledgementBody?.signatureData).toBe('iVBORw0KGgo=')
    expect(screen.getByText('Acknowledgement recorded')).toBeInTheDocument()
    expect(screen.getByText('Synthetic Department Head')).toBeInTheDocument()
    expect(screen.getByText('Department Head')).toBeInTheDocument()
    expect(screen.getAllByText('Acknowledged')).toHaveLength(2)
    expect(
      screen.queryByRole('button', { name: 'Acknowledge form' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('signatureData')).not.toBeInTheDocument()
    expect(screen.queryByText('signatureChecksum')).not.toBeInTheDocument()
    getBoundingClientRect.mockRestore()
    getContext.mockRestore()
    toDataUrl.mockRestore()
  })

  it.each([
    {
      scope: 'all departments',
      search: allDepartmentDashboardSearch,
      departmentValue: '',
    },
    {
      scope: 'a selected department',
      search: selectedDepartmentDashboardSearch,
      departmentValue: 'GSD',
    },
  ])(
    'preserves $scope filters through review, inspection, and full-form returns',
    async ({ search: dashboardSearch, departmentValue }) => {
      installDashboardNavigationHandlers('Submitted')
      const router = renderDashboardReviewWithProviders(dashboardSearch)

      fireEvent.click(await screen.findByRole('link', { name: 'Review batch' }))

      expect(
        await screen.findByRole('heading', {
          name: 'Review before acknowledgement',
        }),
      ).toBeInTheDocument()
      expect(screen.getByText('Awaiting acknowledgement')).toBeInTheDocument()
      expect(screen.getByText('Compliance rate')).toBeInTheDocument()
      expect(screen.getByText('100%')).toBeInTheDocument()
      expect(screen.getByText('Pressure is low.')).toBeInTheDocument()
      expect(
        screen.getByText('Inspect and recharge the unit.'),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('columnheader', { name: 'Finding' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('columnheader', { name: 'Recommendation' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'View full PM form' }),
      ).toHaveAttribute(
        'href',
        expect.stringContaining(`/app/preventive-maintenance-forms/${formId}?`),
      )
      expect(
        screen.getByRole('link', { name: 'View inspection detail' }),
      ).toHaveAttribute(
        'href',
        expect.stringContaining(`/app/inspections/${inspectionId}?`),
      )
      expect(
        screen.getByRole('link', { name: 'View full PM form' }),
      ).toHaveAttribute('href', expect.stringContaining('reviewFormId='))
      expect(
        screen.getByRole('link', { name: 'View inspection detail' }),
      ).toHaveAttribute('href', expect.stringContaining('pmCycle=2026-07'))

      fireEvent.click(
        screen.getByRole('link', { name: 'View inspection detail' }),
      )
      expect(
        await screen.findByRole('heading', {
          name: 'FE-TEST-001',
          level: 1,
        }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { name: 'Record information' }),
      ).toBeInTheDocument()
      await waitFor(() => {
        expect(router.state.location.pathname).toBe(
          `/app/inspections/${inspectionId}`,
        )
        expect(router.state.location.search).toEqual({
          ...dashboardSearch,
          reviewFormId: formId,
          returnContext: {
            kind: 'batchReview',
            formId,
            search: dashboardSearch,
          },
        })
      })

      fireEvent.click(
        screen.getByRole('link', { name: 'Back to batch review' }),
      )
      await screen.findByRole('heading', {
        name: 'Review before acknowledgement',
      })
      await waitFor(() => {
        expect(router.state.location.pathname).toBe(
          `/app/preventive-maintenance-forms/${formId}/review`,
        )
        expect(router.state.location.search).toEqual({
          ...dashboardSearch,
          returnContext: { kind: 'dashboard', search: dashboardSearch },
        })
      })

      fireEvent.click(
        screen.getByRole('link', { name: 'Back to PM dashboard' }),
      )
      await screen.findByRole('link', { name: 'Review batch' })
      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/app/dashboard')
        expect(router.state.location.search).toEqual(dashboardSearch)
      })
      expect(screen.getByLabelText('Department')).toHaveValue(departmentValue)
      expect(screen.getByLabelText('Condition')).toHaveValue('NonOperational')
      expect(screen.getByLabelText('Timeliness / status')).toHaveValue('Late')
      expect(screen.getByLabelText('Search assets')).toHaveValue('FE-TEST-001')
      expect(
        screen.getByText('Asset category').parentElement,
      ).toHaveTextContent('Fire Extinguisher')
      expect(
        screen.getByText('Scheduled month/year').parentElement,
      ).toHaveTextContent('July 2026')

      fireEvent.click(screen.getByRole('link', { name: 'Review batch' }))
      await screen.findByRole('heading', {
        name: 'Review before acknowledgement',
      })
      fireEvent.click(screen.getByRole('link', { name: 'View full PM form' }))
      expect(
        await screen.findByRole('heading', { name: 'GSD-SUBMITTED-001' }),
      ).toBeInTheDocument()
      expect(screen.getByText('Read-only submitted form')).toBeInTheDocument()
      await waitFor(() => {
        expect(router.state.location.pathname).toBe(
          `/app/preventive-maintenance-forms/${formId}`,
        )
        expect(router.state.location.search).toEqual({
          readonly: true,
          ...dashboardSearch,
          reviewFormId: formId,
          returnContext: {
            kind: 'batchReview',
            formId,
            search: dashboardSearch,
          },
        })
      })

      fireEvent.click(
        screen.getByRole('link', { name: 'Back to batch review' }),
      )
      await screen.findByRole('heading', {
        name: 'Review before acknowledgement',
      })
      await waitFor(() => {
        expect(router.state.location.pathname).toBe(
          `/app/preventive-maintenance-forms/${formId}/review`,
        )
        expect(router.state.location.search).toEqual({
          ...dashboardSearch,
          returnContext: { kind: 'dashboard', search: dashboardSearch },
        })
      })
      fireEvent.click(
        screen.getByRole('link', { name: 'Back to PM dashboard' }),
      )
      await screen.findByRole('link', { name: 'Review batch' })
      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/app/dashboard')
        expect(router.state.location.search).toEqual(dashboardSearch)
      })
      expect(screen.getByLabelText('Department')).toHaveValue(departmentValue)
    },
  )

  it('returns an acknowledged batch detail to its filtered dashboard scope', async () => {
    installDashboardNavigationHandlers('Acknowledged')
    const router = renderDashboardReviewWithProviders(
      allDepartmentDashboardSearch,
    )

    fireEvent.click(await screen.findByRole('link', { name: 'View batch' }))
    expect(
      await screen.findByRole('heading', { name: 'GSD-ACKNOWLEDGED-001' }),
    ).toBeInTheDocument()
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(
        `/app/preventive-maintenance-forms/${formId}`,
      )
      expect(router.state.location.search).toEqual({
        readonly: true,
        ...allDepartmentDashboardSearch,
        returnContext: {
          kind: 'dashboard',
          search: allDepartmentDashboardSearch,
        },
      })
    })

    fireEvent.click(screen.getByRole('link', { name: 'Back to PM dashboard' }))
    await screen.findByRole('link', { name: 'View batch' })
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/app/dashboard')
      expect(router.state.location.search).toEqual(allDepartmentDashboardSearch)
    })
    expect(screen.getByLabelText('Department')).toHaveValue('')
  })

  it('preserves omitted dashboard scope fields across the review-form return path', async () => {
    installDashboardNavigationHandlers('Submitted')
    const reviewSearch = {
      condition: 'NonOperational',
      timeliness: 'Late',
      search: 'FE-TEST-001',
    }
    const router = renderAppRouter(
      `/app/preventive-maintenance-forms/${formId}/review?condition=NonOperational&timeliness=Late&search=FE-TEST-001`,
    )

    fireEvent.click(
      await screen.findByRole('link', { name: 'View full PM form' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'GSD-SUBMITTED-001' }),
    ).toBeInTheDocument()
    await waitFor(() => {
      expect(router.state.location.search).toEqual({
        readonly: true,
        ...reviewSearch,
        reviewFormId: formId,
        returnContext: {
          kind: 'batchReview',
          formId,
          search: reviewSearch,
        },
      })
    })

    fireEvent.click(screen.getByRole('link', { name: 'Back to batch review' }))
    await screen.findByRole('heading', {
      name: 'Review before acknowledgement',
    })
    await waitFor(() => {
      expect(router.state.location.search).toEqual({
        ...reviewSearch,
        returnContext: { kind: 'dashboard', search: reviewSearch },
      })
    })
    fireEvent.click(screen.getByRole('link', { name: 'Back to PM dashboard' }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/app/dashboard')
      expect(router.state.location.search).toEqual(reviewSearch)
    })
  })

  it('keeps ordinary form-registry detail navigation scoped to the registry', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['Inspector']))),
      http.get(formsUrl, () => HttpResponse.json([form('Submitted')])),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json(form('Submitted')),
      ),
    )
    const router = renderAppRouter('/app/preventive-maintenance-forms')

    fireEvent.click(
      await screen.findByRole('link', { name: 'GSD-SUBMITTED-001' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'Inspection rows' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Back to form review' }),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('link', { name: 'Back to form review' }))
    expect(
      await screen.findByRole('heading', { name: 'Form review' }),
    ).toBeInTheDocument()
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(
        '/app/preventive-maintenance-forms',
      )
      expect(router.state.location.search).toEqual({})
    })
  })

  it('rejects an invalid review form id without requesting form or batch data', async () => {
    let formRequests = 0
    let dashboardRequests = 0
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/:requestedFormId`, () => {
        formRequests += 1
        return HttpResponse.json(form('Submitted'))
      }),
      http.get('*/api/v1/pm-period-dashboard', () => {
        dashboardRequests += 1
        return HttpResponse.json({})
      }),
    )

    renderWithProviders(
      <PmAcknowledgementReview
        formId="not-a-guid"
        search={{
          assetCategory: 'fire-extinguisher',
          year: 2026,
          pmCycle: '2026-07',
          department: 'GSD',
        }}
      />,
    )

    expect(
      await screen.findByRole('heading', { name: 'Review not found' }),
    ).toBeInTheDocument()
    expect(formRequests).toBe(0)
    expect(dashboardRequests).toBe(0)
  })

  it.each([
    { status: 404, title: 'Form not found' },
    { status: 503, title: 'Form unavailable' },
  ])(
    'shows a form failure before requesting batch context ($status)',
    async ({ status, title }) => {
      let dashboardRequested = false
      server.use(
        http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
        http.get(`${formsUrl}/${formId}`, () =>
          HttpResponse.json({}, { status }),
        ),
        http.get('*/api/v1/pm-period-dashboard', () => {
          dashboardRequested = true
          return HttpResponse.json({})
        }),
      )

      renderWithProviders(
        <PmAcknowledgementReview
          formId={formId}
          search={{
            assetCategory: 'fire-extinguisher',
            year: 2026,
            pmCycle: '2026-07',
            department: 'GSD',
          }}
        />,
      )

      expect(
        await screen.findByRole('heading', { name: title }),
      ).toBeInTheDocument()
      expect(dashboardRequested).toBe(false)
    },
  )

  it('invalidates the PM dashboard cache after acknowledgement', async () => {
    let dashboardRequests = 0
    server.use(
      http.get('*/api/v1/pm-period-dashboard', () => {
        dashboardRequests += 1
        return HttpResponse.json({ scheduled: dashboardRequests })
      }),
      http.post(`${formsUrl}/${formId}/acknowledge`, () =>
        HttpResponse.json({
          id: '99999999-9999-4999-8999-999999999999',
          formId,
          acknowledgedAt: '2026-07-29T02:00:00Z',
        }),
      ),
    )

    function CacheProbe() {
      const dashboard = usePmPeriodDashboard({
        assetCategory: 'fire-extinguisher',
        pmCycle: '2026-07',
        department: 'GSD',
      })
      const mutation = useAcknowledgePreventiveMaintenanceFormMutation()

      return (
        <>
          <output data-testid="dashboard-scheduled">
            {dashboard.data?.scheduled ?? 'loading'}
          </output>
          <button
            type="button"
            onClick={() =>
              mutation.mutate({
                id: formId,
                data: {
                  signatoryName: 'Synthetic Department Head',
                  signatoryPosition: 'Department Head',
                  signatureData: 'synthetic-signature',
                  signatureContentType: 'image/png',
                },
              })
            }
          >
            Acknowledge cache test
          </button>
        </>
      )
    }

    renderWithProviders(<CacheProbe />)

    await waitFor(() => expect(dashboardRequests).toBe(1))
    expect(screen.getByTestId('dashboard-scheduled')).toHaveTextContent('1')
    fireEvent.click(
      screen.getByRole('button', { name: 'Acknowledge cache test' }),
    )
    await waitFor(() => expect(dashboardRequests).toBe(2))
    expect(screen.getByTestId('dashboard-scheduled')).toHaveTextContent('2')
  })
})
