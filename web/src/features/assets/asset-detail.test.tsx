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
const verificationLocationUrl = `${assetUrl}/verification-location`
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
  verificationLatitude: null,
  verificationLongitude: null,
  verificationRadiusMeters: null,
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
      http.get(
        `http://localhost:5000/api/v1/inspections/history/${assetId}`,
        () => HttpResponse.json([]),
      ),
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

  it('shows an unavailable QR state when the asset QR value is null', async () => {
    server.use(
      http.get(assetUrl, () =>
        HttpResponse.json({ ...sampleAsset, qrCodeValue: null }),
      ),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(await screen.findByText('QR code not generated')).toBeInTheDocument()
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
      name: 'Copy identifier',
    })
    fireEvent.click(copyBtn)

    await vi.waitFor(() => {
      expect(errorSpy).toHaveBeenCalledWith(
        'QR identifier could not be copied.',
      )
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
    expect(screen.getAllByText('Main Building').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Ground floor').length).toBeGreaterThan(0)
    expect(
      screen.getByRole('region', { name: 'Asset QR label for FE-001' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('UNIPM-FE-001')).not.toBeInTheDocument()
    expect(
      screen.queryByText('Editing is not available yet'),
    ).not.toBeInTheDocument()
  })

  it('allows GSD to save the optional verification location through the generated API client', async () => {
    let submitted: Record<string, unknown> | undefined
    server.use(
      http.get(assetUrl, () => HttpResponse.json(sampleAsset)),
      http.put(verificationLocationUrl, async ({ request }) => {
        submitted = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ ...sampleAsset, ...submitted })
      }),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)
    const actor = userEvent.setup()
    await actor.clear(await screen.findByLabelText('Latitude'))
    await actor.type(screen.getByLabelText('Latitude'), '14.5995')
    await actor.type(screen.getByLabelText('Longitude'), '120.9842')
    await actor.type(screen.getByLabelText('Radius (meters)'), '25')
    await actor.click(screen.getByRole('button', { name: 'Save location' }))

    await vi.waitFor(() => {
      expect(submitted).toEqual({
        verificationLatitude: 14.5995,
        verificationLongitude: 120.9842,
        verificationRadiusMeters: 25,
      })
    })
    expect(toast.success).toHaveBeenCalledWith('Verification location saved.')
  })

  it('does not expose verification configuration to non-GSD users', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '22222222-2222-4222-8222-222222222222',
          email: 'inspector@example.test',
          displayName: 'Inspector User',
          roles: ['Inspector'],
        }),
      ),
      http.get(assetUrl, () => HttpResponse.json(sampleAsset)),
    )

    renderWithProviders(<AssetDetail assetId={assetId} />)

    expect(
      await screen.findByRole('heading', { name: 'FE-001' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', {
        name: 'Inspection location verification',
      }),
    ).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Latitude')).not.toBeInTheDocument()
  })

  it('keeps registry search context on the return link', async () => {
    server.use(http.get(assetUrl, () => HttpResponse.json(sampleAsset)))
    renderWithProviders(
      <AssetDetail
        assetId={assetId}
        registrySearch={{ text: 'FE', page: 2 }}
      />,
    )

    const back = await screen.findByRole('link', { name: 'Back to assets' })
    expect(back).toHaveAttribute('href', expect.stringContaining('text=FE'))
    expect(back).toHaveAttribute('href', expect.stringContaining('page=2'))
  })
})
