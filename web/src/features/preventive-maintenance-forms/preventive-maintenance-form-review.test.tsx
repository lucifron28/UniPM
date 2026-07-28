import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'
import { FormRegistry } from '@/features/preventive-maintenance-forms/form-registry'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const meUrl = 'http://localhost:5000/api/v1/auth/me'
const formsUrl = 'http://localhost:5000/api/v1/preventive-maintenance-forms'
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
    expect(screen.getByText('Submitted')).toBeInTheDocument()
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
})
