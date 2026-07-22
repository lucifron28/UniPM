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
import { AssetCreate } from '@/features/assets/asset-create'
import { useAuthStore } from '@/stores/auth-store'
import { getGetAssetQueryKey } from '@/api/generated/endpoints'
import { server } from '@/test/server'

const createUrl = 'http://localhost:5000/api/v1/assets'
const categoriesUrl =
  'http://localhost:5000/api/v1/reference-data/asset-categories'
const meUrl = 'http://localhost:5000/api/v1/auth/me'

const sampleCategories = [
  { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
]

const createdAsset = {
  id: '77777777-7777-4777-8777-777777777777',
  assetCode: 'FE-999',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Room 101',
  qrCodeValue: 'UNIPM-FE-999',
  status: 'Active',
  createdAt: '2026-07-22T00:00:00Z',
  updatedAt: '2026-07-22T00:00:00Z',
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

function renderWithProviders(ui: React.ReactNode, queryClient?: QueryClient) {
  const qc =
    queryClient ??
    new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })

  const rootRoute = createRootRoute({
    component: () => (
      <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
    ),
  })

  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })

  return { ...render(<RouterProvider router={router} />), queryClient: qc }
}

describe('AssetCreate feature component', () => {
  beforeEach(() => {
    setupAuth()
    vi.restoreAllMocks()
    vi.spyOn(toast, 'success')
    vi.spyOn(toast, 'error')
  })

  it('prevents non-GSD users from creating assets', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'inspector@example.test',
          displayName: 'Inspector User',
          roles: ['Inspector'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(<AssetCreate />)

    expect(await screen.findByText('GSD access required')).toBeInTheDocument()
  })

  it('shows error state when category reference data fails', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(null, { status: 500 })),
    )

    renderWithProviders(<AssetCreate />)

    expect(
      await screen.findByText('Asset categories unavailable'),
    ).toBeInTheDocument()
  })

  it('maps client validation errors to untouched fields on submit and focuses first invalid field', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText('Please review and correct the required fields.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Asset code is required.')).toBeInTheDocument()
    expect(screen.getByText('Choose an asset category.')).toBeInTheDocument()
    expect(document.activeElement).toBe(screen.getByLabelText('Asset code'))
  })

  it('enforces maximum length client validations on submit without blurring fields', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    fireEvent.change(screen.getByLabelText('Asset code'), {
      target: { value: 'A'.repeat(65) },
    })
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    fireEvent.change(screen.getByLabelText(/^Building/i), {
      target: { value: 'B'.repeat(257) },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText('Asset code must not exceed 64 characters.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Building must not exceed 256 characters.'),
    ).toBeInTheDocument()
  })

  it('handles backend 403 Forbidden response cleanly', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () =>
        HttpResponse.json({ title: 'Forbidden' }, { status: 403 }),
      ),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-001')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText(
        'Only GSD users can create assets in the current workflow.',
      ),
    ).toBeInTheDocument()
  })

  it('handles backend 409 Conflict response mapping to assetCode field error', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () =>
        HttpResponse.json({ title: 'Conflict' }, { status: 409 }),
      ),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-001')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      (await screen.findAllByText('That asset code already exists.')).length,
    ).toBeGreaterThan(0)
  })

  it('strips DTO property prefixes when mapping backend 400 error keys to form fields', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () =>
        HttpResponse.json(
          {
            title: 'Validation Failed',
            errors: {
              'CreateAssetDto.AssetCode': ['That asset code is invalid.'],
              Building: ['Building is invalid.'],
            },
          },
          { status: 400 },
        ),
      ),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-001')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText(
        'Please correct the highlighted validation errors.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('That asset code is invalid.')).toBeInTheDocument()
    expect(screen.getByText('Building is invalid.')).toBeInTheDocument()
  })

  it('handles network failure cleanly during submission', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () => HttpResponse.error()),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-001')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText(
        'The service could not be reached. Please check your network connection and try again.',
      ),
    ).toBeInTheDocument()
  })

  it('handles malformed success response with verification wording without triggering toast or navigation', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () => HttpResponse.json({ invalidProperty: 123 })),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-001')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText(
        'The server returned an invalid response, so the creation result could not be verified. Check the registry before trying again.',
      ),
    ).toBeInTheDocument()

    expect(toast.success).not.toHaveBeenCalled()
  })

  it('seeds query cache and triggers success toast on valid creation', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, () => HttpResponse.json(createdAsset)),
    )

    const successSpy = vi.spyOn(toast, 'success')
    const { queryClient } = renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-999')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    await vi.waitFor(() => {
      expect(successSpy).toHaveBeenCalledWith('Asset created.')
      expect(
        queryClient.getQueryData(getGetAssetQueryKey(createdAsset.id)),
      ).toEqual(createdAsset)
    })
  })

  it('disables submit button to prevent duplicate submission while pending', async () => {
    server.use(
      http.get(meUrl, () =>
        HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'gsd@example.test',
          displayName: 'GSD Admin',
          roles: ['GSD'],
        }),
      ),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
      http.post(createUrl, async () => {
        await new Promise((r) => setTimeout(r, 1000))
        return HttpResponse.json(createdAsset)
      }),
    )

    renderWithProviders(<AssetCreate />)

    const actor = userEvent.setup()
    await screen.findByLabelText('Asset code')

    await actor.type(screen.getByLabelText('Asset code'), 'FE-999')
    fireEvent.change(screen.getByLabelText('Category'), {
      target: { value: 'fire-extinguisher' },
    })

    const submitBtn = screen.getByRole('button', { name: 'Create asset' })
    await actor.click(submitBtn)

    expect(
      screen.getByRole('button', { name: /Creating asset.../i }),
    ).toBeDisabled()
  })
})
