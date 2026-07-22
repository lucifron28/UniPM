import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { toast } from 'sonner'
import { configureApiRuntime } from '@/api/http-client'
import { AssetDetail } from '@/features/assets/asset-detail'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const assetId = '11111111-1111-4111-8111-111111111111'
const assetUrl = `http://localhost:5000/api/v1/assets/${assetId}`
const categoriesUrl =
  'http://localhost:5000/api/v1/reference-data/asset-categories'
const meUrl = 'http://localhost:5000/api/v1/auth/me'

const gsdUser = {
  id: '11111111-1111-4111-8111-111111111111',
  email: 'gsd.user@example.test',
  displayName: 'GSD User',
  roles: ['GSD'],
}

const sampleAsset = {
  id: assetId,
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Ground floor',
  qrCodeValue: 'UNIPM-FE-001',
  status: 'Active',
  createdAt: '2026-07-19T00:00:00Z',
  updatedAt: '2026-07-19T00:00:00Z',
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

describe('AssetDetail feature component', () => {
  beforeEach(() => {
    setupAuth()
    vi.restoreAllMocks()
    vi.spyOn(toast, 'error')
    vi.spyOn(toast, 'success')
    server.use(
      http.get(meUrl, () => HttpResponse.json(gsdUser)),
      http.get(categoriesUrl, () => HttpResponse.json([])),
    )
  })

  it('handles invalid route UUID without making an API request', async () => {
    let called = false
    server.use(
      http.get('http://localhost:5000/api/v1/assets/*', () => {
        called = true
        return HttpResponse.json(sampleAsset)
      }),
    )

    renderWithProviders(<AssetDetail assetId="invalid-uuid" />)

    expect(await screen.findByText('Asset not found')).toBeInTheDocument()
    expect(
      screen.getByText(
        'The asset link is invalid. No registry request was made.',
      ),
    ).toBeInTheDocument()
    expect(called).toBe(false)
  })

  it('shows 404 error state when asset does not exist', async () => {
    server.use(
      http.get(assetUrl, () => HttpResponse.json(null, { status: 404 })),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(await screen.findByText('Asset not found')).toBeInTheDocument()
    expect(
      screen.getByText('This record may no longer be available.'),
    ).toBeInTheDocument()
  })

  it('handles network failure with retry button and refetches asset cleanly', async () => {
    let attempts = 0
    server.use(
      http.get(assetUrl, () => {
        attempts++
        if (attempts === 1) return HttpResponse.error()
        return HttpResponse.json(sampleAsset)
      }),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(await screen.findByText('Service unavailable')).toBeInTheDocument()

    const actor = userEvent.setup()
    await actor.click(screen.getByRole('button', { name: 'Retry' }))

    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
  })

  it('displays "Not generated" when asset QR code value is null', async () => {
    server.use(
      http.get(assetUrl, () =>
        HttpResponse.json({ ...sampleAsset, qrCodeValue: null }),
      ),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(await screen.findByText('Not generated')).toBeInTheDocument()
  })

  it('displays error toast feedback when clipboard copy fails', async () => {
    Object.defineProperty(window.navigator, 'clipboard', {
      value: {
        writeText: vi
          .fn()
          .mockRejectedValue(new Error('Clipboard write error')),
      },
      writable: true,
      configurable: true,
    })

    server.use(http.get(assetUrl, () => HttpResponse.json(sampleAsset)))

    const errorSpy = vi.spyOn(toast, 'error')

    renderWithProviders(<AssetDetail assetId={assetId} />)

    const copyBtn = await screen.findByRole('button', {
      name: 'Copy asset code',
    })
    fireEvent.click(copyBtn)

    await vi.waitFor(() => {
      expect(errorSpy).toHaveBeenCalledWith('Asset code could not be copied.')
    })
  })

  it('handles runtime contract failure with data integrity message and retry', async () => {
    server.use(
      http.get(assetUrl, () =>
        HttpResponse.json({
          ...sampleAsset,
          assetCategory: 'invalid-category',
        }),
      ),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(await screen.findByText('Asset record error')).toBeInTheDocument()
    expect(
      screen.getByText(
        'This asset record could not be loaded due to a data integrity issue.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('renders asset details cleanly when loaded', async () => {
    server.use(
      http.get(assetUrl, () => HttpResponse.json(sampleAsset)),
      http.get(categoriesUrl, () =>
        HttpResponse.json([
          { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
        ]),
      ),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
    expect(screen.getByText('Main Building')).toBeInTheDocument()
    expect(screen.getByText('Ground floor')).toBeInTheDocument()
  })
})
