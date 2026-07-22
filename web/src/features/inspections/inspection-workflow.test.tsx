import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { InspectionDetail } from '@/features/inspections/inspection-detail'
import { InspectionHistory } from '@/features/inspections/inspection-history'
import { InspectionRegistry } from '@/features/inspections/inspection-registry'
import { server } from '@/test/server'

const base = 'http://localhost:5000/api/v1'
const inspectionId = '11111111-1111-4111-8111-111111111111'
const scheduleId = '22222222-2222-4222-8222-222222222222'
const assetId = '33333333-3333-4333-8333-333333333333'
const inspectorUserId = '44444444-4444-4444-8444-444444444444'
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
  },
}
const inspection = {
  id: inspectionId,
  scheduleId,
  assetId,
  inspectorUserId,
  dateInspected: '2026-07-22T01:00:00Z',
  isOperational: false,
  remarks: 'Low pressure <script>not executable</script>.',
  actionsRecommendations: 'Arrange a pressure check.',
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

function renderWithProviders(ui: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const rootRoute = createRootRoute({
    component: () => (
      <QueryClientProvider client={client}>{ui}</QueryClientProvider>
    ),
  })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })
  return render(<RouterProvider router={router} />)
}

function mockInspectionApi() {
  configureApiRuntime({
    getAccessToken: () => 'synthetic-inspection-token',
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
  server.use(
    http.get(`${base}/assets`, () => HttpResponse.json([asset])),
    http.get(`${base}/assets/${assetId}`, () => HttpResponse.json(asset)),
    http.get(`${base}/schedules`, () => HttpResponse.json([schedule])),
    http.get(`${base}/schedules/${scheduleId}`, () =>
      HttpResponse.json(schedule),
    ),
    http.get(`${base}/inspections/${inspectionId}`, () =>
      HttpResponse.json(inspection),
    ),
    http.get(`${base}/inspections/history/${assetId}`, () =>
      HttpResponse.json(history),
    ),
  )
}

describe('inspection review workflows', () => {
  it('sends only supported inspection filters while preserving an unfiltered summary', async () => {
    const urls: URL[] = []
    mockInspectionApi()
    server.use(
      http.get(`${base}/inspections`, ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json([inspection])
      }),
    )

    renderWithProviders(
      <InspectionRegistry
        search={{
          assetId,
          scheduleId,
          isOperational: false,
          dateFrom: '2026-07-01T00:00:00.000Z',
          dateTo: '2026-07-31T23:59:59.000Z',
          page: 1,
        }}
        onSearchChange={vi.fn()}
      />,
    )

    await screen.findAllByText('FE-001')
    const filtered = urls.find(
      (url) => url.searchParams.get('assetId') === assetId,
    )
    expect(filtered?.searchParams.get('scheduleId')).toBe(scheduleId)
    expect(filtered?.searchParams.get('isOperational')).toBe('false')
    expect(filtered?.searchParams.get('dateFrom')).toBe(
      '2026-07-01T00:00:00.000Z',
    )
    expect(filtered?.searchParams.get('page')).toBeNull()
    expect(filtered?.searchParams.get('building')).toBeNull()
  })

  it('renders source text safely with linked asset and schedule context', async () => {
    mockInspectionApi()
    renderWithProviders(<InspectionDetail inspectionId={inspectionId} />)

    expect(await screen.findByText('Remarks')).toBeInTheDocument()
    expect(screen.getByText(inspection.remarks)).toBeInTheDocument()
    expect(document.querySelector('script')).toBeNull()
    expect(
      await screen.findByRole('link', { name: 'FE-001' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Actions and recommendations')).toBeInTheDocument()
  })

  it('renders compact asset history and an honest unavailable state', async () => {
    mockInspectionApi()
    const view = renderWithProviders(<InspectionHistory assetId={assetId} />)
    expect(await screen.findByText(/Low pressure/)).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'View source' }),
    ).toBeInTheDocument()

    view.unmount()
    server.use(
      http.get(`${base}/inspections/history/${assetId}`, () =>
        HttpResponse.json(null, { status: 503 }),
      ),
    )
    renderWithProviders(<InspectionHistory assetId={assetId} />)
    expect(
      await screen.findByText('Inspection history is currently unavailable.'),
    ).toBeInTheDocument()
  })
})
