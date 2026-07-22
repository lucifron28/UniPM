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

  it('restores URL search filter parameters into form state and active filter message', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{
          text: 'FE',
          building: 'Main Building',
          department: 'GSD',
          assetCategory: 'fire-extinguisher',
          status: 'Active',
          page: 1,
        }}
        onSearchChange={vi.fn()}
      />,
    )

    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
    expect(
      screen.getByPlaceholderText(
        'Asset code, QR, building, department, or location',
      ),
    ).toHaveValue('FE')
    expect(screen.getByLabelText('Asset category')).toHaveValue(
      'fire-extinguisher',
    )
    expect(screen.getByLabelText('Asset status')).toHaveValue('Active')
    expect(screen.getByText(/Active filters:/)).toBeInTheDocument()
    expect(
      screen.getByText(/fire-extinguisher · Active · Main Building · GSD · FE/),
    ).toBeInTheDocument()
  })

  it('replaces navigation when requested page is out of bounds', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    renderWithProviders(
      <AssetRegistry search={{ page: 99 }} onSearchChange={onSearchChange} />,
    )

    expect(await screen.findAllByText('FE-001')).toBeTruthy()
    expect(onSearchChange).toHaveBeenCalledWith(
      expect.objectContaining({ page: undefined }),
      { replace: true },
    )
  })

  it('resets draft inputs and triggers search reset on Clear button click', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    renderWithProviders(
      <AssetRegistry
        search={{ text: 'FE', building: 'Main Building', page: 1 }}
        onSearchChange={onSearchChange}
      />,
    )

    const actor = userEvent.setup()
    await screen.findByPlaceholderText(
      'Asset code, QR, building, department, or location',
    )
    await actor.click(screen.getByRole('button', { name: 'Clear' }))

    expect(onSearchChange).toHaveBeenCalledWith({ page: 1 })
    expect(
      screen.getByPlaceholderText(
        'Asset code, QR, building, department, or location',
      ),
    ).toHaveValue('')
  })

  it('passes server-side filter parameters to API without including client-side text search param', async () => {
    let capturedUrl: URL | null = null
    server.use(
      http.get(assetsWildcardUrl, ({ request }) => {
        capturedUrl = new URL(request.url)
        return HttpResponse.json(sampleAssets)
      }),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{
          assetCategory: 'fire-alarm',
          status: 'Active',
          building: 'Science Hall',
          department: 'Maintenance',
          text: 'FA-002',
          page: 1,
        }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(await screen.findAllByText('FA-002')).toBeTruthy()
    expect(capturedUrl).not.toBeNull()
    expect(capturedUrl!.searchParams.get('assetCategory')).toBe('fire-alarm')
    expect(capturedUrl!.searchParams.get('status')).toBe('Active')
    expect(capturedUrl!.searchParams.get('building')).toBe('Science Hall')
    expect(capturedUrl!.searchParams.get('department')).toBe('Maintenance')
    expect(capturedUrl!.searchParams.get('text')).toBeNull()
  })

  it('displays loading skeleton elements while queries are pending', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(gsdUser)),
      http.get(assetsWildcardUrl, async () => {
        await new Promise((r) => setTimeout(r, 2000))
        return HttpResponse.json(sampleAssets)
      }),
      http.get(categoriesUrl, async () => {
        await new Promise((r) => setTimeout(r, 2000))
        return HttpResponse.json(sampleCategories)
      }),
    )

    renderWithProviders(
      <AssetRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    expect(
      await screen.findByRole('status', { name: 'Loading summary statistics' }),
    ).toBeInTheDocument()
  })

  it('handles category reference data failure with retry state without hiding asset totals', async () => {
    let categoryAttempts = 0
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => {
        categoryAttempts++
        if (categoryAttempts === 1) {
          return HttpResponse.json(null, { status: 500 })
        }
        return HttpResponse.json(sampleCategories)
      }),
    )

    renderWithProviders(
      <AssetRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    expect(
      await screen.findByText('Category statistics are currently unavailable.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Categories unavailable')).toBeInTheDocument()

    const actor = userEvent.setup()
    const retryBtns = screen.getAllByRole('button', {
      name: 'Retry categories',
    })
    expect(retryBtns[0]).toBeDefined()
    await actor.click(retryBtns[0]!)

    expect(
      (await screen.findAllByText('Fire extinguishers')).length,
    ).toBeGreaterThan(0)
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

  it('shows no-match state when filters produce zero results on non-empty registry', async () => {
    server.use(
      http.get(assetsWildcardUrl, () => HttpResponse.json(sampleAssets)),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{ text: 'NONEXISTENT-ASSET-QUERY', page: 1 }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(
      await screen.findByText('No assets match the current filters.'),
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
