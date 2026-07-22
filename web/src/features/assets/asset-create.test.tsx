import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { configureApiRuntime } from '@/api/http-client'
import { AssetCreate } from '@/features/assets/asset-create'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const createUrl = 'http://localhost:5000/api/v1/assets'
const categoriesUrl =
  'http://localhost:5000/api/v1/reference-data/asset-categories'
const meUrl = 'http://localhost:5000/api/v1/auth/me'

const sampleCategories = [
  { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
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

describe('AssetCreate feature component', () => {
  beforeEach(() => {
    setupAuth()
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

  it('handles backend validation error 400 mapping keys to fields', async () => {
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
              assetCode: ['That asset code is invalid.'],
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
    await actor.selectOptions(
      screen.getByLabelText('Category'),
      'fire-extinguisher',
    )
    await actor.type(screen.getByLabelText(/^Building/i), 'Main Building')
    await actor.type(screen.getByLabelText(/^Department/i), 'GSD')
    await actor.type(screen.getByLabelText(/^Location/i), 'Room 101')
    await actor.click(screen.getByRole('button', { name: 'Create asset' }))

    expect(
      await screen.findByText(
        'Please correct the highlighted validation errors.',
      ),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('That asset code is invalid.'),
    ).toBeInTheDocument()
  })
})
