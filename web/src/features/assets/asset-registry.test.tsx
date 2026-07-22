import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { configureApiRuntime } from '@/api/http-client'
import { AssetRegistry } from '@/features/assets/asset-registry'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const assetsWildcardUrl = 'http://localhost:5000/api/v1/assets*'
const categoriesUrl =
  'http://localhost:5000/api/v1/reference-data/asset-categories'
const meUrl = 'http://localhost:5000/api/v1/auth/me'

const gsdUser = {
  id: '11111111-1111-4111-8111-111111111111',
  email: 'gsd.user@example.test',
  displayName: 'GSD User',
  roles: ['GSD'],
}

const sampleAssets = [
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
    location: '2nd floor',
    qrCodeValue: 'UNIPM-FA-002',
    status: 'Active',
    createdAt: '2026-07-19T00:00:00Z',
    updatedAt: '2026-07-19T00:00:00Z',
  },
]

const sampleCategories = [
  { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
  { code: 'fire-alarm', displayName: 'Fire alarm systems' },
]

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

describe('AssetRegistry feature component', () => {
  beforeEach(() => {
    setupAuth()
    server.use(http.get(meUrl, () => HttpResponse.json(gsdUser)))
  })

  it('renders assets table, summary cards, and handles filter draft state', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    renderWithProviders(
      <AssetRegistry
        search={{ text: '', building: '', department: '', page: 1 }}
        onSearchChange={onSearchChange}
      />,
    )

    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
    expect(screen.getAllByText('FA-002').length).toBeGreaterThan(0)

    const actor = userEvent.setup()
    const searchInput = screen.getByPlaceholderText(
      'Asset code, QR, building, department, or location',
    )
    await actor.type(searchInput, 'FE')

    expect(onSearchChange).not.toHaveBeenCalled()

    await actor.click(screen.getByRole('button', { name: 'Apply filters' }))
    expect(onSearchChange).toHaveBeenCalledWith(
      expect.objectContaining({ text: 'FE', page: 1 }),
    )
  })

  it('distinguishes empty registry from filter no-match state', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json([])),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{ text: '', building: '', department: '', page: 1 }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(
      await screen.findByText('No assets are registered yet.'),
    ).toBeInTheDocument()
  })

  it('shows retry button on summary card failure without displaying false zeros', async () => {
    server.use(
      http.get(assetsWildcardUrl, () =>
        HttpResponse.json(null, { status: 500 }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{ text: '', building: '', department: '', page: 1 }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(
      await screen.findByText('Summary statistics are currently unavailable.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Retry summary' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('0')).not.toBeInTheDocument()
  })
})
