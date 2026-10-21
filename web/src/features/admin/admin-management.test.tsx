import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'
import { AdminManagement } from './admin-management'

const apiRoot = 'http://localhost:5000/api/v1/admin'
const overview = {
  activeAccounts: 8,
  inactiveAccounts: 2,
  assets: 12,
  schedules: 3,
  inspections: 4,
  draftForms: 1,
  submittedForms: 1,
  acknowledgedForms: 2,
  acknowledgements: 2,
}

const account = {
  id: '11111111-1111-4111-8111-111111111111',
  displayName: 'Jordan Inspector',
  email: 'jordan@example.test',
  role: 'Inspector',
  isActive: true,
}

const assets = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    assetCode: 'FE-001',
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    location: 'Ground floor',
    qrCodeValue: 'UNIPM-FE-001',
    status: 'Active',
    createdAt: '2026-07-19T00:00:00Z',
    updatedAt: '2026-07-19T00:00:00Z',
  },
  {
    id: '22222222-2222-4222-8222-222222222222',
    assetCode: 'FA-002',
    assetCategory: 'fire-alarm',
    building: 'Science Hall',
    department: 'Maintenance',
    location: 'Second floor',
    qrCodeValue: 'UNIPM-FA-002',
    status: 'Active',
    createdAt: '2026-07-19T00:00:00Z',
    updatedAt: '2026-07-19T00:00:00Z',
  },
]

function setupAuth() {
  useAuthStore.getState().establishSession('synthetic-admin-test-token')
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration: () => 0,
    refreshAccessToken: async () => null,
    onTerminalUnauthorized: () => undefined,
  })
}

function renderAdmin() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminManagement />
    </QueryClientProvider>,
  )
}

function page<T>(items: T[]) {
  return { items, page: 1, pageSize: 25, totalCount: items.length }
}

function deleteResult() {
  return {
    assetsRemoved: 0,
    schedulesRemoved: 0,
    inspectionsRemoved: 0,
    formsRemoved: 0,
    acknowledgementsRemoved: 0,
    photoFilesRemoved: 0,
    photoFileCleanupFailures: 0,
  }
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('AdminManagement', () => {
  it('shows the overview count and loads accounts from their section', async () => {
    const accountReads: URL[] = []
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.get(`${apiRoot}/accounts`, ({ request }) => {
        accountReads.push(new URL(request.url))
        return HttpResponse.json(page([account]))
      }),
      http.get(`${apiRoot}/accounts/gsd-head`, () =>
        HttpResponse.json({
          activeHead: null,
          accounts: [],
          needsResolution: false,
        }),
      ),
    )
    setupAuth()
    const user = userEvent.setup()

    renderAdmin()

    const activeCount = await screen.findByText('Active accounts')
    expect(activeCount.parentElement).toHaveTextContent('8')
    await user.click(screen.getByRole('tab', { name: 'Accounts' }))
    expect(await screen.findByText('Jordan Inspector')).toBeInTheDocument()
    expect(accountReads.length).toBeGreaterThan(0)
  })

  it('creates an account with the chosen role and sends its initial password only in the request', async () => {
    let requestBody: unknown
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.get(`${apiRoot}/accounts`, () => HttpResponse.json(page([account]))),
      http.get(`${apiRoot}/accounts/gsd-head`, () =>
        HttpResponse.json({
          activeHead: null,
          accounts: [],
          needsResolution: false,
        }),
      ),
      http.post(`${apiRoot}/accounts`, async ({ request }) => {
        requestBody = await request.json()
        return HttpResponse.json(
          {
            ...account,
            displayName: 'Casey Supervisor',
            email: 'casey@example.test',
            role: 'Supervisor',
          },
          { status: 201 },
        )
      }),
    )
    setupAuth()
    const user = userEvent.setup()

    renderAdmin()
    await user.click(screen.getByRole('tab', { name: 'Accounts' }))
    await screen.findByText('Jordan Inspector')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    const dialog = screen.getByRole('dialog')
    await user.type(
      within(dialog).getByLabelText('Display name'),
      'Casey Supervisor',
    )
    await user.type(
      within(dialog).getByLabelText('Login email'),
      'casey@example.test',
    )
    await user.selectOptions(
      within(dialog).getByLabelText('Role'),
      'Supervisor',
    )
    await user.type(
      within(dialog).getByLabelText('Initial password'),
      'Demo-Start-527!',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Create account' }),
    )

    await screen.findByText('Account created.')
    expect(requestBody).toEqual({
      displayName: 'Casey Supervisor',
      email: 'casey@example.test',
      role: 'Supervisor',
      initialPassword: 'Demo-Start-527!',
      isActive: true,
    })
    expect(document.body).not.toHaveTextContent('Demo-Start-527!')
  })

  it('resets a password for the chosen account without displaying the submitted password', async () => {
    let requestBody: unknown
    let resetUrl = ''
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.get(`${apiRoot}/accounts`, () => HttpResponse.json(page([account]))),
      http.get(`${apiRoot}/accounts/gsd-head`, () =>
        HttpResponse.json({
          activeHead: null,
          accounts: [],
          needsResolution: false,
        }),
      ),
      http.post(
        `${apiRoot}/accounts/:accountId/reset-password`,
        async ({ request }) => {
          resetUrl = request.url
          requestBody = await request.json()
          return new HttpResponse(null, { status: 204 })
        },
      ),
    )
    setupAuth()
    const user = userEvent.setup()

    renderAdmin()
    await user.click(screen.getByRole('tab', { name: 'Accounts' }))
    await screen.findByText('Jordan Inspector')
    await user.click(screen.getByRole('button', { name: 'Reset password' }))

    const dialog = screen.getByRole('dialog')
    await user.type(
      within(dialog).getByLabelText('New password'),
      'One-Time-721!',
    )
    await user.type(
      within(dialog).getByLabelText('Confirm new password'),
      'One-Time-721!',
    )
    await user.click(
      within(dialog).getByRole('button', { name: 'Reset password' }),
    )

    await screen.findByText('Password reset. Existing sessions were revoked.')
    expect(resetUrl).toBe(`${apiRoot}/accounts/${account.id}/reset-password`)
    expect(requestBody).toEqual({ newPassword: 'One-Time-721!' })
    expect(document.body).not.toHaveTextContent('One-Time-721!')
  })

  it('does not delete selected assets when canceled and sends the selected IDs when confirmed', async () => {
    let bulkDeleteBody: unknown
    let bulkDeleteCalls = 0
    let singleDeleteCalls = 0
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.get(`${apiRoot}/assets`, () => HttpResponse.json(page(assets))),
      http.post(`${apiRoot}/assets/bulk-delete`, async ({ request }) => {
        bulkDeleteCalls += 1
        bulkDeleteBody = await request.json()
        return HttpResponse.json(deleteResult())
      }),
      http.delete(`${apiRoot}/assets/:assetId`, () => {
        singleDeleteCalls += 1
        return HttpResponse.json(deleteResult())
      }),
    )
    setupAuth()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const user = userEvent.setup()

    renderAdmin()
    await user.click(screen.getByRole('tab', { name: 'Assets' }))
    await screen.findByText('FE-001')
    await user.click(screen.getByRole('checkbox', { name: 'Select FE-001' }))
    await user.click(screen.getByRole('checkbox', { name: 'Select FA-002' }))
    const deleteSelected = screen.getByRole('button', {
      name: 'Delete selected (2)',
    })

    await user.click(deleteSelected)
    expect(bulkDeleteCalls).toBe(0)
    expect(singleDeleteCalls).toBe(0)

    confirm.mockReturnValue(true)
    await user.click(deleteSelected)

    await waitFor(() => expect(bulkDeleteCalls).toBe(1))
    expect(bulkDeleteBody).toEqual({ ids: assets.map(({ id }) => id) })
    expect(singleDeleteCalls).toBe(0)
  })

  it('requires confirmation before clearing all assets and linked maintenance records', async () => {
    let clearCalls = 0
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.delete(`${apiRoot}/demo-data/assets`, () => {
        clearCalls += 1
        return HttpResponse.json(deleteResult())
      }),
    )
    setupAuth()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const user = userEvent.setup()

    renderAdmin()
    await user.click(screen.getByRole('tab', { name: 'Demo data' }))
    const clearAll = screen.getByRole('button', {
      name: 'Clear assets and maintenance',
    })
    await user.click(clearAll)
    expect(clearCalls).toBe(0)

    confirm.mockReturnValue(true)
    await user.click(clearAll)
    await waitFor(() => expect(clearCalls).toBe(1))
  })

  it('uses separate load and reset routes and shows a load error', async () => {
    let loadCalls = 0
    let resetCalls = 0
    server.use(
      http.get(`${apiRoot}/overview`, () => HttpResponse.json(overview)),
      http.post(`${apiRoot}/demo-data/validation/load`, () => {
        loadCalls += 1
        return HttpResponse.json(
          {
            title: 'Conflict',
            detail: 'Assets must be cleared before loading.',
          },
          { status: 409 },
        )
      }),
      http.post(`${apiRoot}/demo-data/validation/reset`, () => {
        resetCalls += 1
        return HttpResponse.json({
          deleted: deleteResult(),
          assetsSeeded: 2,
          schedulesSeeded: 3,
          inspectionsSeeded: 4,
          formsSeeded: 1,
          acknowledgementsSeeded: 1,
          wmsReferralsSeeded: 0,
          wmsReferralAuditsSeeded: 0,
        })
      }),
    )
    setupAuth()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const user = userEvent.setup()

    renderAdmin()
    await user.click(screen.getByRole('tab', { name: 'Demo data' }))
    await user.click(
      screen.getByRole('button', { name: 'Load validation data' }),
    )
    expect(
      await screen.findByText('Assets must be cleared before loading.'),
    ).toBeInTheDocument()
    expect(loadCalls).toBe(1)
    expect(resetCalls).toBe(0)

    const reset = screen.getByRole('button', { name: 'Reset and reseed' })
    await user.click(reset)
    expect(resetCalls).toBe(0)
    confirm.mockReturnValue(true)
    await user.click(reset)
    await waitFor(() => expect(resetCalls).toBe(1))
    expect(loadCalls).toBe(1)
  })
})
