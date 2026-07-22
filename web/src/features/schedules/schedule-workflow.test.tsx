import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { configureApiRuntime } from '@/api/http-client'
import { ScheduleCreate } from '@/features/schedules/schedule-create'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'
import { ScheduleRegistry } from '@/features/schedules/schedule-registry'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const base = 'http://localhost:5000/api/v1'
const assetId = '22222222-2222-4222-8222-222222222222'
const scheduleId = '11111111-1111-4111-8111-111111111111'
const user = {
  id: '33333333-3333-4333-8333-333333333333',
  email: 'gsd@example.test',
  displayName: 'GSD User',
  roles: ['GSD'],
}
const asset = {
  id: assetId,
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main',
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
  status: 'Due',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: null,
  assignedToUserId: null,
  completedAt: null,
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
  asset: {
    id: asset.id,
    assetCode: asset.assetCode,
    assetCategory: asset.assetCategory,
    building: asset.building,
    department: asset.department,
    location: asset.location,
  },
}

function setupAuth() {
  useAuthStore.getState().establishSession('synthetic-schedule-token')
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
}

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

function mockReferences(roles = ['GSD']) {
  server.use(
    http.get(`${base}/auth/me`, () => HttpResponse.json({ ...user, roles })),
    http.get(`${base}/assets`, () => HttpResponse.json([asset])),
    http.get(`${base}/reference-data/schedule-statuses`, () =>
      HttpResponse.json([
        { code: 'Due', displayName: 'Due' },
        { code: 'Completed', displayName: 'Completed' },
      ]),
    ),
    http.get(`${base}/reference-data/schedule-period-types`, () =>
      HttpResponse.json([
        { code: 'Quarter', displayName: 'Quarter' },
        { code: 'Annual', displayName: 'Annual' },
      ]),
    ),
    http.get(`${base}/reference-data/schedule-quarters`, () =>
      HttpResponse.json([{ code: 'Q3', displayName: 'Q3' }]),
    ),
  )
}

describe('schedule workflows', () => {
  beforeEach(() => {
    setupAuth()
    mockReferences()
  })

  it('keeps summary unfiltered while sending supported registry filters', async () => {
    const urls: URL[] = []
    server.use(
      http.get(`${base}/schedules`, ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json([schedule])
      }),
    )
    renderWithProviders(
      <ScheduleRegistry
        search={{
          assetId,
          status: 'Due',
          from: '2026-08-01T00:00:00.000Z',
          to: '2026-08-31T23:59:59.000Z',
          quarter: 'Q3',
          year: 2026,
          page: 1,
        }}
        onSearchChange={vi.fn()}
      />,
    )
    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
    expect(screen.getByText('All schedules')).toBeInTheDocument()
    expect(
      urls.some((url) => url.searchParams.get('assetId') === assetId),
    ).toBe(true)
    expect(urls.some((url) => url.searchParams.get('quarter') === 'Q3')).toBe(
      true,
    )
    expect(
      urls.some(
        (url) =>
          url.searchParams.get('from') === '2026-08-01T00:00:00.000Z' &&
          url.searchParams.get('to') === '2026-08-31T23:59:59.000Z',
      ),
    ).toBe(true)
  })

  it('disables failed reference selectors and retries the affected data', async () => {
    let assetAttempts = 0
    server.use(
      http.get(`${base}/assets`, () => {
        assetAttempts++
        return assetAttempts === 1
          ? HttpResponse.json(null, { status: 500 })
          : HttpResponse.json([asset])
      }),
      http.get(`${base}/schedules`, () => HttpResponse.json([schedule])),
    )

    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    await screen.findByRole('button', { name: 'Retry asset options' })
    const assetSelect = screen.getByLabelText('Asset')
    expect(assetSelect).toBeDisabled()
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Retry asset options' }))
    await vi.waitFor(() => expect(assetSelect).not.toBeDisabled())
    expect(screen.getByRole('option', { name: 'FE-001' })).toBeInTheDocument()
  })

  it('rejects an invalid detail UUID without making a schedule request', async () => {
    let called = false
    server.use(
      http.get(`${base}/schedules/*`, () => {
        called = true
        return HttpResponse.json(schedule)
      }),
    )
    renderWithProviders(<ScheduleDetail scheduleId="invalid" />)
    expect(await screen.findByText('Schedule not found')).toBeInTheDocument()
    expect(called).toBe(false)
  })

  it('reports malformed schedule detail responses', async () => {
    server.use(
      http.get(`${base}/schedules/${scheduleId}`, () =>
        HttpResponse.json({ ...schedule, status: 'Invented' }),
      ),
    )
    renderWithProviders(<ScheduleDetail scheduleId={scheduleId} />)
    expect(await screen.findByText('Schedule record error')).toBeInTheDocument()
  })

  it('denies an Admin-only user and does not render a create action', async () => {
    mockReferences(['Admin'])
    renderWithProviders(<ScheduleCreate />)
    expect(
      await screen.findByText('Schedule manager access required'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Create schedule' }),
    ).not.toBeInTheDocument()
  })

  it('clears quarter when period changes and submits the approved fields', async () => {
    let requestBody: unknown
    server.use(
      http.post(`${base}/schedules`, async ({ request }) => {
        requestBody = await request.json()
        return HttpResponse.json({
          ...schedule,
          periodType: 'Annual',
          quarter: null,
        })
      }),
    )
    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    await screen.findByLabelText('Asset')
    fireEvent.change(screen.getByLabelText('Asset'), {
      target: { value: assetId },
    })
    fireEvent.change(screen.getByLabelText('Schedule date'), {
      target: { value: '2026-08-01' },
    })
    fireEvent.change(screen.getByLabelText('Quarter'), {
      target: { value: 'Q3' },
    })
    fireEvent.change(screen.getByLabelText('Period type'), {
      target: { value: 'Annual' },
    })
    expect(screen.queryByLabelText('Quarter')).not.toBeInTheDocument()
    await actor.click(screen.getByRole('button', { name: 'Create schedule' }))
    await vi.waitFor(() =>
      expect(requestBody).toMatchObject({
        assetId,
        periodType: 'Annual',
        quarter: null,
        year: 2026,
      }),
    )
    expect(Object.keys(requestBody as object).sort()).toEqual(
      ['assetId', 'periodType', 'quarter', 'scheduleDate', 'year'].sort(),
    )
  })
})
