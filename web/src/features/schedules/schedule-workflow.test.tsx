import { render, screen, waitFor } from '@testing-library/react'
import { useState } from 'react'
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
import { toast } from 'sonner'
import { ScheduleCreate } from '@/features/schedules/schedule-create'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'
import {
  ScheduleRegistry,
  type ScheduleSearch,
} from '@/features/schedules/schedule-registry'
import { getCurrentManilaYear } from '@/features/schedules/schedule-presentation'
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
  hasVerificationLocation: false,
  qrCodeValue: 'UNIPM-FE-001',
  status: 'Active',
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
}
const fireAlarmAsset = {
  ...asset,
  id: '99999999-9999-4999-8999-999999999999',
  assetCode: 'FA-001',
  assetCategory: 'fire-alarm',
}
const schedule = {
  id: scheduleId,
  assetId,
  scheduleDate: '2026-08-01T00:00:00+08:00',
  pmCycle: '2026-08',
  periodType: 'Quarter',
  status: 'Due',
  quarter: 'Q3',
  semester: null,
  year: 2026,
  academicYear: null,
  assignedToUserId: null,
  assignedSupervisorUserId: null,
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
const workerId = '44444444-4444-4444-8444-444444444444'
const supervisorId = '55555555-5555-4555-8555-555555555555'

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
    http.get(`${base}/schedules/supervisor-assignment-options`, () =>
      roles.includes('GSD')
        ? HttpResponse.json({
            supervisors: [
              { id: supervisorId, displayName: 'Fictional Supervisor' },
            ],
          })
        : HttpResponse.json({}, { status: 403 }),
    ),
    http.get(`${base}/schedules/assignment-options`, () =>
      roles.includes('Supervisor')
        ? HttpResponse.json({
            workers: [{ id: workerId, displayName: 'Fictional Inspector' }],
          })
        : HttpResponse.json({}, { status: 403 }),
    ),
    http.get(`${base}/schedules/enrollment-deferrals`, () =>
      HttpResponse.json({
        page: 1,
        pageSize: 10,
        total: 0,
        pendingCount: 0,
        reviewedCount: 0,
        items: [],
      }),
    ),
    http.get(`${base}/assets`, () => HttpResponse.json([asset])),
    http.get(`${base}/reference-data/asset-categories`, () =>
      HttpResponse.json([
        {
          code: 'fire-extinguisher',
          displayName: 'Fire extinguishers',
          scheduledMonths: [2, 5, 8, 11],
        },
        {
          code: 'fire-alarm',
          displayName: 'Fire alarms',
          scheduledMonths: [6, 12],
        },
      ]),
    ),
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
    vi.restoreAllMocks()
    setupAuth()
    vi.spyOn(toast, 'success')
    mockReferences()
  })

  it('uses the Manila calendar year even before UTC reaches the new year', () => {
    expect(getCurrentManilaYear(new Date('2025-12-31T16:30:00.000Z'))).toBe(
      2026,
    )
  })

  it('lets GSD generate missing cycles for a bounded year and reports the result', async () => {
    const currentYear = getCurrentManilaYear()
    let submittedYear: number | undefined
    server.use(
      http.get(`${base}/schedules`, () => HttpResponse.json([schedule])),
      http.post(`${base}/schedules/generate`, async ({ request }) => {
        const body = (await request.json()) as { year: number }
        submittedYear = body.year
        return HttpResponse.json({
          year: body.year,
          eligibleAssets: 5,
          existingSchedules: 8,
          createdSchedules: 4,
          deferredSchedules: 0,
          cyclesRequiringGsdCoverageReview: 2,
        })
      }),
    )

    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )
    const actor = userEvent.setup()
    const yearInput = await screen.findByRole('spinbutton', {
      name: 'Generation year',
    })
    const getGenerateButton = () =>
      screen.getByRole('button', { name: 'Generate missing schedules' })

    expect(yearInput).toHaveValue(currentYear)
    expect(yearInput).toHaveAttribute('max', String(currentYear))
    expect(getGenerateButton()).toBeEnabled()

    await actor.clear(yearInput)
    await actor.type(yearInput, String(currentYear + 1))
    expect(getGenerateButton()).toBeDisabled()
    expect(submittedYear).toBeUndefined()

    await actor.clear(yearInput)
    await actor.type(yearInput, String(currentYear))
    await actor.click(getGenerateButton())

    const resultMessage = await screen.findByText(
      `Year ${currentYear}: created 4 schedules; 8 already existed; 0 deferred for GSD review. 2 earlier current-year cycles remain uncreated until GSD confirms the approved scheduling coverage start date.`,
    )
    expect(resultMessage).toHaveAttribute('role', 'status')
    expect(submittedYear).toBe(currentYear)
  })

  it('does not expose schedule generation to Supervisors', async () => {
    mockReferences(['Supervisor'])
    server.use(
      http.get(`${base}/schedules`, () => HttpResponse.json([schedule])),
    )
    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    await screen.findAllByText('FE-001')
    expect(
      screen.queryByRole('button', { name: 'Generate missing schedules' }),
    ).not.toBeInTheDocument()
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

  it('applies the combined schedule filters only after submission and keeps them in the detail return', async () => {
    const urls: URL[] = []
    server.use(
      http.get(`${base}/schedules`, ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json([schedule])
      }),
    )

    function FilterHarness() {
      const [search, setSearch] = useState<ScheduleSearch>({})
      return (
        <ScheduleRegistry
          search={search}
          onSearchChange={(next) => setSearch(next)}
        />
      )
    }

    renderWithProviders(<FilterHarness />)
    const actor = userEvent.setup()
    const detailLinks = await screen.findAllByRole('link', {
      name: 'View details',
    })
    expect(detailLinks).toHaveLength(2)
    expect(urls).toHaveLength(1)

    await actor.type(
      screen.getByRole('textbox', { name: 'Search schedules' }),
      'FE-001',
    )
    await actor.selectOptions(
      screen.getByLabelText('Asset category'),
      'fire-alarm',
    )
    await actor.selectOptions(screen.getByLabelText('Department'), 'GSD')
    expect(urls).toHaveLength(1)

    await actor.click(screen.getByRole('button', { name: 'Apply filters' }))
    await waitFor(() => {
      expect(
        urls.some((url) => url.searchParams.get('search') === 'FE-001'),
      ).toBe(true)
    })
    const applied = urls.find(
      (url) => url.searchParams.get('search') === 'FE-001',
    )
    expect(applied?.searchParams.get('assetCategory')).toBe('fire-alarm')
    expect(applied?.searchParams.get('department')).toBe('GSD')

    const href = screen
      .getAllByRole('link', { name: 'View details' })[0]!
      .getAttribute('href')
    const returnContext = JSON.parse(
      new URL(href!, 'http://localhost').searchParams.get('returnContext')!,
    )
    expect(returnContext.search).toMatchObject({
      assetCategory: 'fire-alarm',
      department: 'GSD',
      search: 'FE-001',
      page: 1,
    })
  })

  it('explains schedule read authorization failures and keeps retry controls', async () => {
    mockReferences(['Admin'])
    server.use(
      http.get(`${base}/schedules`, () =>
        HttpResponse.json(null, { status: 403 }),
      ),
    )

    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    const accessMessages = await screen.findAllByText(
      /Schedule reads require a GSD, Inspector, or Supervisor role\./,
    )
    expect(accessMessages).toHaveLength(2)
    expect(
      screen.getAllByText(
        /Admin is a technical system administration role and cannot read operational schedules\./,
      ),
    ).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Retry summary' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled()
  })

  it('keeps the schedule filter and pagination focus after changing pages', async () => {
    const records = Array.from({ length: 11 }, (_, index) => ({
      ...schedule,
      id: `10000000-0000-4000-8000-${(index + 1).toString().padStart(12, '0')}`,
    }))
    server.use(http.get(`${base}/schedules`, () => HttpResponse.json(records)))
    const onSearchChange = vi.fn()

    function PaginationHarness() {
      const [search, setSearch] = useState<ScheduleSearch>({
        status: 'Due',
        page: 1,
      })
      return (
        <ScheduleRegistry
          search={search}
          onSearchChange={(next, options) => {
            onSearchChange(next, options)
            setSearch(next)
          }}
        />
      )
    }

    renderWithProviders(<PaginationHarness />)
    await screen.findByText('Page 1 of 2')
    expect(screen.getByText('Showing 1-10 of 11')).toBeInTheDocument()
    const next = screen.getByRole('button', { name: 'Next' })
    await userEvent.setup().click(next)
    expect(onSearchChange).toHaveBeenCalledWith(
      { status: 'Due', page: 2 },
      { preserveScroll: true },
    )
    expect(await screen.findByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.getByText('Showing 11-11 of 11')).toBeInTheDocument()
    expect(next).toHaveFocus()
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

  it('lets GSD assign the Supervisor stage for the full department, category, and cycle batch', async () => {
    let requestBody: unknown
    server.use(
      http.get(`${base}/schedules/${scheduleId}`, () =>
        HttpResponse.json(schedule),
      ),
      http.put(
        `${base}/schedules/${scheduleId}/supervisor-assignment`,
        async ({ request }) => {
          requestBody = await request.json()
          return HttpResponse.json({
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-08',
            workerUserId: null,
            workerDisplayName: null,
            supervisorUserId: supervisorId,
            supervisorDisplayName: 'Fictional Supervisor',
            scheduleIds: [scheduleId, assetId],
          })
        },
      ),
    )

    renderWithProviders(<ScheduleDetail scheduleId={scheduleId} />)
    const actor = userEvent.setup()
    expect(await screen.findByLabelText('Supervisor (oversight)')).toBeVisible()
    await actor.selectOptions(
      screen.getByLabelText('Supervisor (oversight)'),
      supervisorId,
    )
    await actor.click(
      screen.getByRole('button', {
        name: 'Assign Supervisor to entire batch',
      }),
    )

    await waitFor(() =>
      expect(toast.success).toHaveBeenCalledWith(
        'Supervisor assignment saved for 2 schedules.',
      ),
    )
    expect(requestBody).toEqual({ supervisorUserId: supervisorId })
    expect(screen.queryByLabelText('Skilled worker (Inspector)')).toBeNull()
  })

  it('lets only the assigned Supervisor choose an Inspector for the batch', async () => {
    let requestBody: unknown
    server.use(
      http.get(`${base}/schedules/${scheduleId}`, () =>
        HttpResponse.json({ ...schedule, assignedSupervisorUserId: user.id }),
      ),
      http.put(
        `${base}/schedules/${scheduleId}/assignment`,
        async ({ request }) => {
          requestBody = await request.json()
          return HttpResponse.json({
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-08',
            workerUserId: workerId,
            workerDisplayName: 'Fictional Inspector',
            supervisorUserId: user.id,
            supervisorDisplayName: 'GSD User',
            scheduleIds: [scheduleId],
          })
        },
      ),
    )

    mockReferences(['Supervisor'])
    renderWithProviders(<ScheduleDetail scheduleId={scheduleId} />)
    const actor = userEvent.setup()
    await actor.selectOptions(
      await screen.findByLabelText('Skilled worker (Inspector)'),
      workerId,
    )
    await actor.click(
      screen.getByRole('button', { name: 'Assign Inspector to entire batch' }),
    )

    await waitFor(() =>
      expect(toast.success).toHaveBeenCalledWith(
        'Inspector assignment saved for 1 schedule.',
      ),
    )
    expect(requestBody).toEqual({ workerUserId: workerId })
    expect(screen.queryByLabelText('Supervisor (oversight)')).toBeNull()
  })

  it('hides Inspector assignment from a Supervisor who does not own the batch', async () => {
    let optionsRequested = false
    mockReferences(['Supervisor'])
    server.use(
      http.get(`${base}/schedules/${scheduleId}`, () =>
        HttpResponse.json({
          ...schedule,
          assignedSupervisorUserId: supervisorId,
        }),
      ),
      http.get(`${base}/schedules/assignment-options`, () => {
        optionsRequested = true
        return HttpResponse.json({ workers: [] })
      }),
    )

    renderWithProviders(<ScheduleDetail scheduleId={scheduleId} />)

    expect(
      await screen.findByText(
        'Only the Supervisor assigned to this PM batch can assign or update its Inspector.',
      ),
    ).toBeVisible()
    expect(screen.queryByLabelText('Skilled worker (Inspector)')).toBeNull()
    expect(optionsRequested).toBe(false)
  })

  it('does not load assignment options or show assignment controls to Inspectors', async () => {
    mockReferences(['Inspector'])
    let supervisorOptionsRequested = false
    let workerOptionsRequested = false
    server.use(
      http.get(`${base}/schedules/${scheduleId}`, () =>
        HttpResponse.json(schedule),
      ),
      http.get(`${base}/schedules/assignment-options`, () => {
        workerOptionsRequested = true
        return HttpResponse.json({}, { status: 403 })
      }),
      http.get(`${base}/schedules/supervisor-assignment-options`, () => {
        supervisorOptionsRequested = true
        return HttpResponse.json({}, { status: 403 })
      }),
    )

    renderWithProviders(<ScheduleDetail scheduleId={scheduleId} />)

    await screen.findByText('Batch assignment')
    expect(screen.queryByLabelText('Skilled worker (Inspector)')).toBeNull()
    expect(screen.queryByLabelText('Supervisor (oversight)')).toBeNull()
    expect(supervisorOptionsRequested).toBe(false)
    expect(workerOptionsRequested).toBe(false)
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

  it('clears the selected month when the asset category changes', async () => {
    server.use(
      http.get(`${base}/assets`, () =>
        HttpResponse.json([asset, fireAlarmAsset]),
      ),
    )

    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    const assetSelect = await screen.findByLabelText('Asset')
    const monthSelect = screen.getByLabelText('Scheduled month')
    const yearInput = screen.getByLabelText('Scheduled year')
    await actor.selectOptions(assetSelect, assetId)
    await actor.type(yearInput, '2026')
    await actor.selectOptions(monthSelect, '8')
    expect(monthSelect).toHaveValue('8')
    expect(screen.getByLabelText('Due date')).toHaveValue('Aug 31, 2026')

    await actor.selectOptions(assetSelect, fireAlarmAsset.id)
    expect(monthSelect).toHaveValue('')
    expect(
      [...(monthSelect as HTMLSelectElement).options].map(
        (option) => option.value,
      ),
    ).toEqual(['', '6', '12'])
    expect(screen.getByLabelText('Due date')).toHaveValue(
      'Choose a scheduled year and month',
    )
  })

  it('creates a schedule from the selected cycle without an exact date', async () => {
    let requestBody: unknown
    server.use(
      http.get(`${base}/assets`, () => HttpResponse.json([asset])),
      http.post(`${base}/schedules`, async ({ request }) => {
        requestBody = await request.json()
        return HttpResponse.json(schedule)
      }),
    )
    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    await actor.selectOptions(await screen.findByLabelText('Asset'), assetId)
    await actor.type(screen.getByLabelText('Scheduled year'), '2026')
    await actor.selectOptions(screen.getByLabelText('Scheduled month'), '8')
    expect(screen.getByLabelText('Due date')).toHaveValue('Aug 31, 2026')
    expect(screen.queryByLabelText('Schedule date')).not.toBeInTheDocument()
    expect(document.querySelector('input[type="date"]')).toBeNull()
    await actor.click(screen.getByRole('button', { name: 'Create schedule' }))
    await vi.waitFor(() =>
      expect(requestBody).toMatchObject({
        assetId,
        pmCycle: '2026-08',
        periodType: 'Quarter',
        quarter: 'Q3',
        year: 2026,
      }),
    )
    expect(Object.keys(requestBody as object).sort()).toEqual(
      ['assetId', 'periodType', 'quarter', 'pmCycle', 'year'].sort(),
    )
  })

  it('blocks schedule creation when the category response has no allowed months', async () => {
    let postCalled = false
    server.use(
      http.get(`${base}/reference-data/asset-categories`, () =>
        HttpResponse.json([
          { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
        ]),
      ),
      http.post(`${base}/schedules`, () => {
        postCalled = true
        return HttpResponse.json(schedule)
      }),
    )

    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    await actor.selectOptions(await screen.findByLabelText('Asset'), assetId)
    expect(screen.getByLabelText('Scheduled month')).toBeDisabled()
    expect(
      screen.getByText(
        /Allowed PM months are unavailable for this asset category/,
      ),
    ).toBeInTheDocument()
    await actor.click(screen.getByRole('button', { name: 'Create schedule' }))
    expect(postCalled).toBe(false)
  })

  it('offers only active assets with a department for scheduling', async () => {
    server.use(
      http.get(`${base}/assets`, () =>
        HttpResponse.json([
          asset,
          {
            ...asset,
            id: '66666666-6666-4666-8666-666666666666',
            assetCode: 'FE-INACTIVE',
            status: 'Inactive',
          },
          {
            ...asset,
            id: '77777777-7777-4777-8777-777777777777',
            assetCode: 'FE-RETIRED',
            status: 'Retired',
          },
          {
            ...asset,
            id: '88888888-8888-4888-8888-888888888888',
            assetCode: 'FE-NO-DEPT',
            department: null,
          },
        ]),
      ),
    )

    renderWithProviders(<ScheduleCreate />)
    const select = await screen.findByLabelText('Asset')
    expect(select).toHaveTextContent('FE-001')
    expect(select).not.toHaveTextContent('FE-INACTIVE')
    expect(select).not.toHaveTextContent('FE-RETIRED')
    expect(select).not.toHaveTextContent('FE-NO-DEPT')
    expect(
      screen.getByText(
        'Only active assets with a department can be scheduled.',
      ),
    ).toBeInTheDocument()
  })
})
