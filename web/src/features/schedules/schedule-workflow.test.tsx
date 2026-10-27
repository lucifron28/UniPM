import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
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
import type { AssetResponse } from '@/api/generated/models'
import { toast } from 'sonner'
import { ScheduleCreate } from '@/features/schedules/schedule-create'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'
import { ScheduleAssignmentQueue } from '@/features/schedules/schedule-assignment-queue'
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

function assetLookupHandlers(
  assets: AssetResponse[],
  onLookup?: (url: URL) => void,
) {
  return [
    http.get(`${base}/assets/registry`, ({ request }) => {
      const url = new URL(request.url)
      onLookup?.(url)
      const params = url.searchParams
      const search = params.get('search')?.toLocaleLowerCase()
      const status = params.get('status')
      const requiresDepartment = params.get('hasDepartment') === 'true'
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 20)
      const matchingAssets = assets.filter((candidate) => {
        const matchesSearch =
          !search ||
          [
            candidate.assetCode,
            candidate.department,
            candidate.building,
            candidate.location,
          ]
            .filter(Boolean)
            .some((value) => value!.toLocaleLowerCase().includes(search))
        return (
          (!status || candidate.status === status) &&
          (!requiresDepartment || Boolean(candidate.department?.trim())) &&
          matchesSearch
        )
      })
      const start = (page - 1) * pageSize
      return HttpResponse.json({
        items: matchingAssets.slice(start, start + pageSize),
        page,
        pageSize,
        totalCount: matchingAssets.length,
      })
    }),
    http.get(`${base}/assets/:assetId`, ({ params }) => {
      const selected = assets.find(
        (candidate) => candidate.id === params.assetId,
      )
      return selected
        ? HttpResponse.json(selected)
        : HttpResponse.json({}, { status: 404 })
    }),
  ]
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
    status: asset.status,
  },
}
const workerId = '44444444-4444-4444-8444-444444444444'
const supervisorId = '55555555-5555-4555-8555-555555555555'

function scheduleRegistryItem(item: typeof schedule) {
  return {
    id: item.id,
    assetId: item.assetId,
    scheduleDate: item.scheduleDate,
    pmCycle: item.pmCycle,
    periodType: item.periodType,
    status: item.status,
    quarter: item.quarter,
    semester: item.semester,
    year: item.year,
    academicYear: item.academicYear,
    completedAt: item.completedAt,
    createdAt: item.createdAt,
    updatedAt: item.updatedAt,
    asset: {
      id: item.asset.id,
      assetCode: item.asset.assetCode,
      assetCategory: item.asset.assetCategory,
      building: item.asset.building,
      department: item.asset.department,
      location: item.asset.location,
    },
  }
}

function assignmentBatch(overrides: Record<string, unknown> = {}) {
  return {
    batchKey: 'GSD|fire-extinguisher|2026-08',
    department: 'GSD',
    assetCategory: 'fire-extinguisher',
    pmCycle: '2026-08',
    scheduleCount: 2,
    unfinishedScheduleCount: 2,
    completedCount: 0,
    ongoingCount: 0,
    overdueCount: 0,
    buildings: ['Main'],
    supervisorState: 'Unassigned',
    supervisorUserId: null,
    supervisorDisplayName: null,
    inspectorState: 'Unassigned',
    inspectorUserId: null,
    inspectorDisplayName: null,
    assignmentLocked: false,
    supervisorOwnershipIsConsistent: false,
    representativeSchedule: schedule,
    ...overrides,
  }
}

function scheduleRegistryHandlers(items = [schedule]) {
  const registryItems = items.map(scheduleRegistryItem)
  return [
    http.get(`${base}/schedules/registry`, ({ request }) => {
      const params = new URL(request.url).searchParams
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      const start = (page - 1) * pageSize
      return HttpResponse.json({
        items: registryItems.slice(start, start + pageSize),
        page,
        pageSize,
        totalCount: registryItems.length,
        overallCount: registryItems.length,
      })
    }),
    http.get(`${base}/schedules/registry/options`, () =>
      HttpResponse.json({
        categories: [
          {
            code: 'fire-extinguisher',
            displayName: 'Fire extinguishers',
            scheduledMonths: [2, 5, 8, 11],
          },
          {
            code: 'fire-alarm',
            displayName: 'Fire alarm systems',
            scheduledMonths: [6, 12],
          },
        ],
        quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
        departments: ['GSD'],
        years: ['2026'],
      }),
    ),
    http.get(`${base}/schedules/registry/summary`, () =>
      HttpResponse.json({
        totalCount: registryItems.length,
        dueCount: registryItems.filter((item) => item.status === 'Due').length,
        ongoingCount: registryItems.filter((item) => item.status === 'Ongoing')
          .length,
        overdueCount: registryItems.filter((item) => item.status === 'Overdue')
          .length,
        completedCount: registryItems.filter(
          (item) => item.status === 'Completed',
        ).length,
        cancelledCount: registryItems.filter(
          (item) => item.status === 'Cancelled',
        ).length,
      }),
    ),
  ]
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
    http.get(`${base}/schedules/coverage-review`, () =>
      HttpResponse.json({
        items: [],
        page: 1,
        pageSize: 10,
        total: 0,
      }),
    ),
    ...scheduleRegistryHandlers(),
    ...assetLookupHandlers([asset]),
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

  it('shows only unlocked, unassigned batches in the GSD assignment queue', async () => {
    server.use(
      http.get(`${base}/schedules/assignment-batches`, () =>
        HttpResponse.json({
          totalScheduleCount: 6,
          batches: [
            assignmentBatch(),
            assignmentBatch({
              batchKey: 'GSD|fire-extinguisher|2026-11',
              pmCycle: '2026-11',
              supervisorState: 'Assigned',
              supervisorUserId: supervisorId,
              supervisorDisplayName: 'Assigned Supervisor',
            }),
            assignmentBatch({
              batchKey: 'GSD|fire-alarm|2026-12',
              assetCategory: 'fire-alarm',
              pmCycle: '2026-12',
              assignmentLocked: true,
            }),
          ],
        }),
      ),
    )

    renderWithProviders(
      <ScheduleAssignmentQueue
        role="GSD"
        search={{ view: 'assignments' }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(await screen.findByText('GSD · Fire extinguishers')).toBeVisible()
    expect(
      screen.getByText('1 pending batch · 2 unfinished schedules'),
    ).toBeVisible()
    expect(screen.queryByText('Assigned Supervisor')).toBeNull()
    expect(
      screen.queryByText('Only show batches needing a Supervisor'),
    ).toBeNull()
  })

  it('assigns each selected full batch once and leaves partial failures selected', async () => {
    const secondScheduleId = '66666666-6666-4666-8666-666666666666'
    const successfulBatch = assignmentBatch({
      batchKey: 'CCMS|fire-extinguisher|2026-08',
      department: 'CCMS',
      buildings: ['Library Complex', 'Main Building'],
      scheduleCount: 5,
      unfinishedScheduleCount: 5,
    })
    const failingBatch = assignmentBatch({
      batchKey: 'Library|fire-extinguisher|2026-08',
      department: 'Library',
      buildings: ['Library Complex'],
      scheduleCount: 2,
      unfinishedScheduleCount: 2,
      representativeSchedule: {
        ...schedule,
        id: secondScheduleId,
        asset: { ...schedule.asset, department: 'Library' },
      },
    })
    const outsideBatch = assignmentBatch({
      batchKey: 'Administration|fire-extinguisher|2026-08',
      department: 'Administration',
      buildings: ['Administration Building'],
    })
    let pendingBatches = [successfulBatch, failingBatch, outsideBatch]
    const assignedScheduleIds: string[] = []
    server.use(
      http.get(`${base}/schedules/assignment-batches`, () =>
        HttpResponse.json({
          totalScheduleCount: pendingBatches.reduce(
            (total, batch) => total + Number(batch.scheduleCount),
            0,
          ),
          batches: pendingBatches,
        }),
      ),
      http.put(
        `${base}/schedules/:scheduleId/supervisor-assignment`,
        async ({ params }) => {
          const id = String(params.scheduleId)
          assignedScheduleIds.push(id)
          if (id === secondScheduleId) {
            return HttpResponse.json({}, { status: 409 })
          }
          pendingBatches = pendingBatches.filter(
            (batch) => batch.representativeSchedule.id !== id,
          )
          return HttpResponse.json({
            scheduleIds: [
              scheduleId,
              secondScheduleId,
              assetId,
              user.id,
              workerId,
            ],
          })
        },
      ),
    )

    const actor = userEvent.setup()
    renderWithProviders(
      <ScheduleAssignmentQueue
        role="GSD"
        search={{ view: 'assignments', year: 2026 }}
        onSearchChange={vi.fn()}
      />,
    )

    const buildingPicker = await screen.findByRole('combobox', {
      name: 'Building',
    })
    await actor.click(buildingPicker)
    await actor.type(buildingPicker, 'Library Complex')
    await actor.click(
      await screen.findByRole('option', { name: 'Library Complex' }),
    )

    expect(await screen.findByText('CCMS · Fire extinguishers')).toBeVisible()
    expect(screen.getByText('Library · Fire extinguishers')).toBeVisible()
    expect(screen.queryByText('Administration · Fire extinguishers')).toBeNull()
    await actor.click(
      screen.getByRole('button', { name: 'Select all visible' }),
    )
    await actor.click(
      screen.getByRole('button', { name: 'Assign Supervisor to 2 batches' }),
    )

    const dialog = screen.getByRole('dialog')
    expect(
      within(dialog).getByText(
        'This batch spans 2 buildings. Assignment applies to all 5 schedules. Buildings: Library Complex, Main Building.',
      ),
    ).toBeVisible()
    await waitFor(() =>
      expect(
        within(dialog).queryByText('Loading eligible Supervisors…'),
      ).toBeNull(),
    )
    await actor.type(
      within(dialog).getByLabelText('Search active Supervisor'),
      'Fictional Supervisor',
    )
    const confirmButton = within(dialog).getByRole('button', {
      name: 'Assign Supervisor to 2 batches',
    })
    await waitFor(() => expect(confirmButton).toBeEnabled())
    await actor.click(confirmButton)

    expect(
      await screen.findByText(
        '1 of 2 batches assigned. 1 failed; review the refreshed queue.',
      ),
    ).toBeVisible()
    expect(assignedScheduleIds).toEqual([scheduleId, secondScheduleId])
    expect(
      screen.getByRole('checkbox', {
        name: 'Select Library Fire extinguishers August 2026 batch',
      }),
    ).toBeChecked()
  })

  it('shows only the current Supervisor’s unlocked batches awaiting an Inspector', async () => {
    mockReferences(['Supervisor'])
    server.use(
      http.get(`${base}/schedules/assignment-batches`, () =>
        HttpResponse.json({
          totalScheduleCount: 6,
          batches: [
            assignmentBatch({
              supervisorState: 'Assigned',
              supervisorUserId: user.id,
              supervisorDisplayName: user.displayName,
              supervisorOwnershipIsConsistent: true,
            }),
            assignmentBatch({
              batchKey: 'Library|fire-extinguisher|2026-08',
              department: 'Library',
              supervisorState: 'Assigned',
              supervisorUserId: supervisorId,
              supervisorDisplayName: 'Another Supervisor',
              supervisorOwnershipIsConsistent: true,
            }),
            assignmentBatch({
              batchKey: 'GSD|fire-alarm|2026-12',
              assetCategory: 'fire-alarm',
              pmCycle: '2026-12',
              supervisorState: 'Assigned',
              supervisorUserId: user.id,
              supervisorOwnershipIsConsistent: true,
              assignmentLocked: true,
            }),
          ],
        }),
      ),
    )

    renderWithProviders(
      <ScheduleAssignmentQueue
        role="Supervisor"
        search={{ view: 'assignments' }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(await screen.findByText('GSD · Fire extinguishers')).toBeVisible()
    expect(
      screen.getByText('1 pending batch · 2 unfinished schedules'),
    ).toBeVisible()
    expect(screen.queryByText('Another Supervisor')).toBeNull()
    expect(
      screen.queryByText('Only show batches needing an Inspector'),
    ).toBeNull()
  })

  it('reassigns the Supervisor from Schedule History with a reason', async () => {
    let submittedBody: unknown
    server.use(
      ...scheduleRegistryHandlers(),
      http.get(`${base}/schedules/assignment-batches`, () =>
        HttpResponse.json({
          totalScheduleCount: 2,
          batches: [
            assignmentBatch({
              supervisorState: 'Assigned',
              supervisorUserId: supervisorId,
              supervisorDisplayName: 'Current Supervisor',
              inspectorState: 'Assigned',
              inspectorUserId: workerId,
              inspectorDisplayName: 'Current Inspector',
              unfinishedScheduleCount: 1,
              completedCount: 1,
              assignmentLocked: true,
            }),
          ],
        }),
      ),
      http.put(
        `${base}/schedules/${scheduleId}/supervisor-reassignment`,
        async ({ request }) => {
          submittedBody = await request.json()
          return HttpResponse.json({
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-08',
            workerUserId: workerId,
            workerDisplayName: 'Current Inspector',
            supervisorUserId: supervisorId,
            supervisorDisplayName: 'Fictional Supervisor',
            scheduleIds: [scheduleId],
          })
        },
      ),
    )

    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )
    const actor = userEvent.setup()
    await actor.click(
      (
        await screen.findAllByRole('button', { name: 'Open batch history' })
      )[0]!,
    )
    await actor.click(
      (await screen.findAllByRole('button', { name: 'Reassign' }))[0]!,
    )
    const dialog = await screen.findByRole('dialog', {
      name: 'Schedule batch assignment',
    })
    expect(
      await within(dialog).findByText('Current Supervisor', { selector: 'dd' }),
    ).toBeVisible()
    expect(
      within(dialog).getByText('Current Inspector', { selector: 'dd' }),
    ).toBeVisible()
    expect(within(dialog).getByText('1', { selector: 'dd' })).toBeVisible()
    await actor.click(
      screen.getByRole('button', { name: 'Reassign Supervisor' }),
    )
    const replacement = screen.getByRole('combobox', {
      name: 'Replacement Supervisor',
    })
    await actor.type(replacement, 'Fictional Supervisor')
    const reasonInput = screen.getByRole('textbox', {
      name: 'Reason for reassignment',
    })
    await actor.clear(reasonInput)
    await actor.type(reasonInput, 'Coverage change')
    await actor.click(
      screen.getByRole('button', { name: 'Confirm reassignment' }),
    )

    await waitFor(() =>
      expect(submittedBody).toEqual({
        assigneeUserId: supervisorId,
        reason: 'Coverage change',
      }),
    )
    expect(toast.success).toHaveBeenCalledWith(
      'Supervisor reassigned for 1 unfinished schedule.',
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
    server.use(...scheduleRegistryHandlers())
    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    await userEvent
      .setup()
      .click(
        (
          await screen.findAllByRole('button', { name: 'Open batch history' })
        )[0]!,
      )
    await screen.findAllByText('FE-001')
    expect(
      screen.queryByRole('button', { name: 'Generate missing schedules' }),
    ).not.toBeInTheDocument()
  })

  it('orders the schedule page sections and exposes management navigation', async () => {
    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    const pageHeading = await screen.findByRole('heading', {
      name: 'Schedules',
      level: 1,
    })
    const findHeading = screen.getByRole('heading', {
      name: 'Find schedules',
      level: 2,
    })
    const summaryHeading = screen.getByRole('heading', {
      name: 'Schedule summary',
      level: 2,
    })
    const recordsHeading = await screen.findByRole('heading', {
      name: 'Schedule records',
      level: 2,
    })
    const managementHeading = screen.getByRole('heading', {
      name: 'Schedule management',
      level: 2,
    })
    const follows = (before: HTMLElement, after: HTMLElement) =>
      Boolean(
        before.compareDocumentPosition(after) &
        Node.DOCUMENT_POSITION_FOLLOWING,
      )

    expect(follows(pageHeading, findHeading)).toBe(true)
    expect(follows(findHeading, summaryHeading)).toBe(true)
    expect(follows(summaryHeading, recordsHeading)).toBe(true)
    expect(follows(recordsHeading, managementHeading)).toBe(true)
    expect(
      screen.getByRole('link', { name: 'Manage schedules' }),
    ).toHaveAttribute('href', '#schedule-management')
    expect(
      screen.queryByRole('link', { name: 'Add schedule' }),
    ).not.toBeInTheDocument()

    const moreFilters = screen.getByText('More filters', { exact: true })
    expect(moreFilters.tagName).toBe('SUMMARY')
    expect(screen.getByLabelText('Department')).toBeVisible()
    expect(screen.getByLabelText('Specific asset')).not.toBeVisible()
    await userEvent.setup().click(moreFilters)
    expect(screen.getByLabelText('Department')).toBeVisible()
    expect(
      screen.getByRole('combobox', { name: 'Specific asset' }),
    ).toBeVisible()
    await userEvent.setup().click(moreFilters)
    expect(screen.getByLabelText('Department')).toBeVisible()
    expect(screen.getByLabelText('Specific asset')).not.toBeVisible()
  })

  it('keeps summary unfiltered while sending supported registry filters', async () => {
    const urls: URL[] = []
    server.use(
      http.get(`${base}/schedules/registry`, ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json({
          items: [scheduleRegistryItem(schedule)],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          overallCount: 1,
        })
      }),
      http.get(`${base}/schedules/registry/options`, () =>
        HttpResponse.json({
          categories: [
            {
              code: 'fire-extinguisher',
              displayName: 'Fire extinguishers',
              scheduledMonths: [2, 5, 8, 11],
            },
            {
              code: 'fire-alarm',
              displayName: 'Fire alarm systems',
              scheduledMonths: [6, 12],
            },
          ],
          quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
          departments: ['GSD'],
          years: ['2026'],
        }),
      ),
      http.get(`${base}/schedules/coverage-review`, () =>
        HttpResponse.json({
          year: 2026,
          page: 1,
          pageSize: 10,
          total: 0,
          items: [],
        }),
      ),
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
          pmCycle: '2026-08',
          page: 1,
        }}
        onSearchChange={vi.fn()}
      />,
    )
    await userEvent
      .setup()
      .click(
        (
          await screen.findAllByRole('button', { name: 'Open batch history' })
        )[0]!,
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
      urls.some((url) => url.searchParams.get('pmCycle') === '2026-08'),
    ).toBe(true)
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
      http.get(`${base}/schedules/registry`, ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json({
          items: [scheduleRegistryItem(schedule)],
          page: 1,
          pageSize: 25,
          totalCount: 1,
          overallCount: 1,
        })
      }),
      http.get(`${base}/schedules/registry/options`, () =>
        HttpResponse.json({
          categories: [
            {
              code: 'fire-extinguisher',
              displayName: 'Fire extinguishers',
              scheduledMonths: [2, 5, 8, 11],
            },
            {
              code: 'fire-alarm',
              displayName: 'Fire alarm systems',
              scheduledMonths: [6, 12],
            },
          ],
          quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
          departments: ['GSD'],
          years: ['2026'],
        }),
      ),
      http.get(`${base}/schedules/coverage-review`, () =>
        HttpResponse.json({
          year: 2026,
          page: 1,
          pageSize: 10,
          total: 0,
          items: [],
        }),
      ),
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
    await actor.click(
      (
        await screen.findAllByRole('button', { name: 'Open batch history' })
      )[0]!,
    )
    const detailLinks = await screen.findAllByRole('link', {
      name: 'View details',
    })
    expect(detailLinks).toHaveLength(1)
    await actor.click(screen.getByRole('button', { name: 'Back to batches' }))
    const initialRequestCount = urls.length
    expect(initialRequestCount).toBeGreaterThan(0)

    await actor.type(
      screen.getByRole('textbox', { name: 'Search schedules' }),
      'FE-001',
    )
    await actor.selectOptions(
      screen.getByLabelText('Asset category'),
      'fire-alarm',
    )
    const departmentPicker = screen.getByRole('combobox', {
      name: 'Department',
    })
    await actor.click(departmentPicker)
    await actor.click(await screen.findByRole('option', { name: 'GSD' }))
    expect(urls).toHaveLength(initialRequestCount)

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

    await actor.click(
      (
        await screen.findAllByRole('button', { name: 'Open batch history' })
      )[0]!,
    )
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
      http.get(`${base}/schedules/registry/summary`, () =>
        HttpResponse.json(null, { status: 403 }),
      ),
      http.get(`${base}/schedules/registry`, () =>
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
    const records = Array.from({ length: 26 }, (_, index) => ({
      ...schedule,
      id: `10000000-0000-4000-8000-${(index + 1).toString().padStart(12, '0')}`,
    }))
    server.use(
      http.get(`${base}/schedules/registry`, ({ request }) => {
        const params = new URL(request.url).searchParams
        const page = Number(params.get('page') ?? 1)
        const pageSize = Number(params.get('pageSize') ?? 25)
        const start = (page - 1) * pageSize
        return HttpResponse.json({
          items: records
            .slice(start, start + pageSize)
            .map(scheduleRegistryItem),
          page,
          pageSize,
          totalCount: records.length,
          overallCount: records.length,
        })
      }),
    )
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
    expect(screen.getByText('Showing 1-25 of 26')).toBeInTheDocument()
    const next = screen.getByRole('button', { name: 'Next' })
    await userEvent.setup().click(next)
    expect(onSearchChange).toHaveBeenCalledWith(
      { status: 'Due', page: 2 },
      { preserveScroll: true },
    )
    expect(await screen.findByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.getByText('Showing 26-26 of 26')).toBeInTheDocument()
    expect(next).toHaveFocus()
  })

  it('disables failed reference selectors and retries the affected data', async () => {
    let optionsAttempts = 0
    server.use(
      http.get(`${base}/schedules/registry/options`, () => {
        optionsAttempts++
        return optionsAttempts === 1
          ? HttpResponse.json(null, { status: 500 })
          : HttpResponse.json({
              categories: [
                {
                  code: 'fire-extinguisher',
                  displayName: 'Fire extinguishers',
                  scheduledMonths: [2, 5, 8, 11],
                },
              ],
              quarters: ['Q1', 'Q2', 'Q3', 'Q4'],
              departments: ['GSD'],
              years: ['2026'],
            })
      }),
    )

    renderWithProviders(
      <ScheduleRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    await screen.findByRole('button', { name: 'Retry registry options' })
    const categorySelect = screen.getByLabelText('Asset category')
    expect(categorySelect).toBeDisabled()
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Retry registry options' }))
    await vi.waitFor(() => expect(categorySelect).not.toBeDisabled())
    expect(
      screen.getByRole('option', { name: 'Fire extinguishers' }),
    ).toBeInTheDocument()
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
      http.get(`${base}/schedules/registry`, () =>
        HttpResponse.json({
          items: [scheduleRegistryItem(schedule)],
          page: 1,
          pageSize: 1,
          totalCount: 2,
          overallCount: 2,
        }),
      ),
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
    expect(
      await screen.findByText(
        'This assignment affects 2 schedules in this batch.',
      ),
    ).toBeVisible()
    await actor.clear(screen.getByLabelText('Supervisor (oversight)'))
    await actor.type(
      screen.getByLabelText('Supervisor (oversight)'),
      'Fictional Supervisor',
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
    expect(screen.queryByLabelText('Inspector')).toBeNull()
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
    expect(
      await screen.findByText(
        'This assignment affects 1 schedule in this batch.',
      ),
    ).toBeVisible()
    await actor.clear(await screen.findByLabelText('Inspector'))
    await actor.type(screen.getByLabelText('Inspector'), 'Fictional Inspector')
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
    expect(screen.queryByLabelText('Inspector')).toBeNull()
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
    expect(screen.queryByLabelText('Inspector')).toBeNull()
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
    server.use(...assetLookupHandlers([asset, fireAlarmAsset]))

    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    const assetPicker = await screen.findByRole('combobox', { name: 'Asset' })
    const monthSelect = screen.getByLabelText('Scheduled month')
    const yearInput = screen.getByLabelText('Scheduled year')
    await actor.type(assetPicker, 'FE-001')
    await actor.click(await screen.findByRole('option', { name: /FE-001/ }))
    await actor.type(yearInput, '2026')
    await actor.selectOptions(monthSelect, '8')
    expect(monthSelect).toHaveValue('8')
    expect(screen.getByLabelText('Due date')).toHaveValue('Aug 31, 2026')

    await actor.clear(assetPicker)
    await actor.type(assetPicker, 'FA-001')
    await actor.click(await screen.findByRole('option', { name: /FA-001/ }))
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
      ...assetLookupHandlers([asset]),
      http.post(`${base}/schedules`, async ({ request }) => {
        requestBody = await request.json()
        return HttpResponse.json(schedule)
      }),
    )
    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    const assetPicker = await screen.findByRole('combobox', { name: 'Asset' })
    await actor.type(assetPicker, 'FE-001')
    await actor.click(await screen.findByRole('option', { name: /FE-001/ }))
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
    const assetPicker = await screen.findByRole('combobox', { name: 'Asset' })
    await actor.type(assetPicker, 'FE-001')
    await actor.click(await screen.findByRole('option', { name: /FE-001/ }))
    expect(screen.getByLabelText('Scheduled month')).toBeDisabled()
    expect(
      screen.getByText(
        /Allowed PM months are unavailable for this asset category/,
      ),
    ).toBeInTheDocument()
    await actor.click(screen.getByRole('button', { name: 'Create schedule' }))
    expect(postCalled).toBe(false)
  })

  it('requests bounded searchable assets that are active and have a department', async () => {
    let lookupUrl: URL | undefined
    server.use(
      ...assetLookupHandlers(
        [
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
        ],
        (url) => {
          lookupUrl = url
        },
      ),
    )

    renderWithProviders(<ScheduleCreate />)
    const assetPicker = await screen.findByRole('combobox', { name: 'Asset' })
    const actor = userEvent.setup()
    await actor.type(assetPicker, 'FE-')
    expect(await screen.findByRole('option', { name: /FE-001/ })).toBeVisible()
    expect(screen.queryByRole('option', { name: /FE-INACTIVE/ })).toBeNull()
    expect(screen.queryByRole('option', { name: /FE-RETIRED/ })).toBeNull()
    expect(screen.queryByRole('option', { name: /FE-NO-DEPT/ })).toBeNull()
    expect(lookupUrl?.searchParams.get('search')).toBe('FE-')
    expect(lookupUrl?.searchParams.get('status')).toBe('Active')
    expect(lookupUrl?.searchParams.get('hasDepartment')).toBe('true')
    expect(lookupUrl?.searchParams.get('pageSize')).toBe('20')
    expect(
      screen.getByText(
        'Only active assets with a department are available for scheduling.',
      ),
    ).toBeInTheDocument()
  })

  it('blocks form submission if the selected asset is no longer eligible', async () => {
    let postCalled = false
    server.use(
      http.get(`${base}/assets/${assetId}`, () =>
        HttpResponse.json({ ...asset, status: 'Inactive' }),
      ),
      ...assetLookupHandlers([asset]),
      http.post(`${base}/schedules`, () => {
        postCalled = true
        return HttpResponse.json(schedule)
      }),
    )

    renderWithProviders(<ScheduleCreate />)
    const actor = userEvent.setup()
    const assetPicker = await screen.findByRole('combobox', { name: 'Asset' })
    await actor.type(assetPicker, 'FE-001')
    await actor.click(await screen.findByRole('option', { name: /FE-001/ }))
    await screen.findByText(/could not be verified as active with a department/)
    expect(
      screen.getByRole('button', { name: 'Create schedule' }),
    ).toBeDisabled()

    fireEvent.submit(
      screen.getByRole('button', { name: 'Create schedule' }).closest('form')!,
    )

    expect(
      await screen.findByText('Choose an active asset with a department.'),
    ).toBeVisible()
    expect(postCalled).toBe(false)
  })
})
