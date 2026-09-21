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
import {
  useAcknowledgePreventiveMaintenanceFormMutation,
} from '@/features/preventive-maintenance-forms/form-queries'
import { usePmPeriodDashboard } from '@/features/reports/pm-period-dashboard-queries'
import { AppShell } from '@/components/layout/app-shell'
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
    expect(
      screen.getByRole('link', { name: 'GSD-SUBMITTED-001' }),
    ).toHaveAttribute(
      'href',
      '/app/preventive-maintenance-forms/77777777-7777-4777-8777-777777777777',
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
    fireEvent.pointerDown(screen.getByLabelText('Signature'), {
      clientX: 20,
      clientY: 20,
      pointerId: 1,
    })
    fireEvent.pointerUp(screen.getByLabelText('Signature'), { pointerId: 1 })
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge form' }))

    expect(
      await screen.findByRole('dialog', {
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
    toDataUrl.mockRestore()
  })

  it('shows submitted batch review metrics, rows, and awaiting acknowledgement', async () => {
    const reviewForm = {
      ...form('Submitted'),
      pmCycle: '2026-07',
      fieldWorkCompletedAt: '2026-07-28T03:00:00Z',
    }
    let dashboardRequest: URL | undefined
    server.use(
      http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))),
      http.get(`${formsUrl}/${formId}`, () =>
        HttpResponse.json(reviewForm),
      ),
      http.get('*/api/v1/pm-period-dashboard', ({ request }) => {
        dashboardRequest = new URL(request.url)
        return HttpResponse.json({
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
          batches: [
            {
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
            },
          ],
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
              formStatus: 'Submitted',
              isAcknowledged: false,
              acknowledgedAt: null,
            },
          ],
        })
      }),
    )

    renderWithProviders(
      <PmAcknowledgementReview formId={formId} search={{}} />,
    )

    expect(
      await screen.findByRole('heading', {
        name: 'Review before acknowledgement',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Awaiting acknowledgement')).toBeInTheDocument()
    expect(screen.getByText('100%')).toBeInTheDocument()
    expect(screen.getByText('Pressure is low.')).toBeInTheDocument()
    expect(screen.getByText('Inspect and recharge the unit.')).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Finding' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Recommendation' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View full PM form' })).toHaveAttribute(
      'href',
      expect.stringContaining(`/app/preventive-maintenance-forms/${formId}?`),
    )
    expect(screen.getByRole('link', { name: 'View inspection detail' })).toHaveAttribute(
      'href',
      expect.stringContaining(`/app/inspections/${inspectionId}?`),
    )
    expect(screen.getByRole('link', { name: 'View full PM form' })).toHaveAttribute(
      'href',
      expect.stringContaining('reviewFormId='),
    )
    expect(screen.getByRole('link', { name: 'View inspection detail' })).toHaveAttribute(
      'href',
      expect.stringContaining('pmCycle=2026-07'),
    )
    await waitFor(() => {
      expect(dashboardRequest?.searchParams.get('assetCategory')).toBe(
        'fire-extinguisher',
      )
      expect(dashboardRequest?.searchParams.get('pmCycle')).toBe('2026-07')
      expect(dashboardRequest?.searchParams.get('department')).toBe('GSD')
    })
  })

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
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge cache test' }))
    await waitFor(() => expect(dashboardRequests).toBe(2))
    expect(screen.getByTestId('dashboard-scheduled')).toHaveTextContent('2')
  })
})
