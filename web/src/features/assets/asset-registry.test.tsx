import { render, screen, waitFor, within } from '@testing-library/react'
import { useState } from 'react'
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
import {
  AssetRegistry,
  type AssetSearch,
} from '@/features/assets/asset-registry'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const assetRegistryUrl = 'http://localhost:5000/api/v1/assets/registry'
const assetRegistryOptionsUrl =
  'http://localhost:5000/api/v1/assets/registry/options'
const assetRegistrySummaryUrl =
  'http://localhost:5000/api/v1/assets/registry/summary'
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
    hasVerificationLocation: false,
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
    hasVerificationLocation: false,
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

const registryCategories = [
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
  {
    code: 'emergency-light',
    displayName: 'Emergency lights',
    scheduledMonths: [2, 5, 8, 11],
  },
  {
    code: 'water-drinking-station',
    displayName: 'Water drinking stations',
    scheduledMonths: [6, 12],
  },
]

function registrySummaryFor(allAssets: typeof sampleAssets) {
  const categorySummary = registryCategories.map((category) => {
    const assets = allAssets.filter(
      (asset) => asset.assetCategory === category.code,
    )
    return {
      assetCategory: category.code,
      displayName: category.displayName,
      totalCount: assets.length,
      activeCount: assets.filter((asset) => asset.status === 'Active').length,
      inactiveCount: assets.filter((asset) => asset.status === 'Inactive')
        .length,
      retiredCount: assets.filter((asset) => asset.status === 'Retired').length,
    }
  })
  const summary = {
    totalCount: allAssets.length,
    activeCount: allAssets.filter((asset) => asset.status === 'Active').length,
    inactiveCount: allAssets.filter((asset) => asset.status === 'Inactive')
      .length,
    retiredCount: allAssets.filter((asset) => asset.status === 'Retired')
      .length,
    categories: categorySummary,
  }
  return summary
}

function registryHandlers(allAssets = sampleAssets) {
  const summary = registrySummaryFor(allAssets)
  return [
    http.get(assetRegistryUrl, ({ request }) => {
      const params = new URL(request.url).searchParams
      const search = params.get('search')?.toLowerCase()
      const filtered = allAssets.filter(
        (asset) =>
          (!search ||
            `${asset.assetCode} ${asset.building} ${asset.department} ${asset.location}`
              .toLowerCase()
              .includes(search)) &&
          (!params.get('assetCategory') ||
            asset.assetCategory === params.get('assetCategory')) &&
          (!params.get('status') || asset.status === params.get('status')) &&
          (!params.get('building') ||
            asset.building === params.get('building')) &&
          (!params.get('department') ||
            asset.department === params.get('department')),
      )
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      const start = (page - 1) * pageSize
      return HttpResponse.json({
        items: filtered.slice(start, start + pageSize),
        page,
        pageSize,
        totalCount: filtered.length,
      })
    }),
    http.get(assetRegistryOptionsUrl, () =>
      HttpResponse.json({
        categories: registryCategories,
        statuses: ['Active', 'Inactive', 'Retired'],
        departments: [
          ...new Set(
            allAssets.map((asset) => asset.department).filter(Boolean),
          ),
        ],
        buildings: [
          ...new Set(allAssets.map((asset) => asset.building).filter(Boolean)),
        ],
      }),
    ),
    http.get(assetRegistrySummaryUrl, () => HttpResponse.json(summary)),
  ]
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

describe('AssetRegistry feature component', () => {
  it.each([
    ['fire-extinguisher', 'Fire extinguishers'],
    ['fire-alarm', 'Fire alarm systems'],
    ['emergency-light', 'Emergency lights'],
    ['water-drinking-station', 'Water drinking stations'],
    ['unknown-category', 'Unknown Category'],
  ])('formats the %s category for readers', (code, label) => {
    expect(formatAssetCategory(code)).toBe(label)
  })

  beforeEach(() => {
    setupAuth()
    server.use(http.get(meUrl, () => HttpResponse.json(gsdUser)))
  })

  it('renders asset records and handles filter draft state', async () => {
    server.use(
      ...registryHandlers(),
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
    const headingOrder = screen
      .getAllByRole('heading')
      .map((heading) => heading.textContent?.trim())
    expect(headingOrder.indexOf('Assets')).toBeLessThan(
      headingOrder.indexOf('Find assets'),
    )
    expect(headingOrder.indexOf('Find assets')).toBeLessThan(
      headingOrder.indexOf('Asset records'),
    )
    expect(screen.getByText('2 matching assets')).toBeVisible()
    expect(
      screen.queryByText('Overall asset inventory'),
    ).not.toBeInTheDocument()
    expect(screen.getByLabelText('Search assets')).toBeVisible()
    expect(screen.getByLabelText('Asset category')).toBeVisible()
    expect(screen.getByLabelText('Status')).toBeVisible()
    expect(screen.getByLabelText('Building')).toBeVisible()
    expect(screen.getByLabelText('Department')).toBeVisible()
    expect(
      screen.getByRole('columnheader', { name: 'QR label' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Showing 1-2 of 2')).toBeInTheDocument()
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.queryByText('UNIPM-FE-001')).not.toBeInTheDocument()

    const actor = userEvent.setup()
    const zoomGroup = screen.getByRole('group', {
      name: 'Asset records zoom controls',
    })
    await actor.click(
      within(zoomGroup).getByRole('button', {
        name: 'Increase Asset records zoom',
      }),
    )
    expect(
      screen
        .getByRole('region', { name: 'Assets table' })
        .querySelector<HTMLElement>(':scope > div')?.style.zoom,
    ).toBe('1.1')
    await actor.click(
      within(zoomGroup).getByRole('button', {
        name: 'Reset Asset records zoom to 100 percent',
      }),
    )
    const searchInput = screen.getByPlaceholderText(
      'Asset code, QR, building, department, or location',
    )
    await actor.type(searchInput, 'FE')

    expect(onSearchChange).not.toHaveBeenCalled()

    await actor.click(screen.getByRole('button', { name: 'Apply filters' }))
    expect(onSearchChange).toHaveBeenCalledWith(
      expect.objectContaining({ text: 'FE', page: 1 }),
      { preserveScroll: true },
    )
  })

  it('restores URL filters as readable chips and removes an active filter', async () => {
    const records = Array.from({ length: 26 }, (_, index) => ({
      ...sampleAssets[0]!,
      id: `10000000-0000-4000-8000-${(index + 1).toString().padStart(12, '0')}`,
      assetCode: `FE-${(index + 1).toString().padStart(3, '0')}`,
    }))
    server.use(
      ...registryHandlers(records),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    function FilterHarness() {
      const [search, setSearch] = useState<AssetSearch>({
        text: 'FE',
        building: 'Main Building',
        department: 'GSD',
        assetCategory: 'fire-extinguisher',
        status: 'Active',
        page: 2,
      })

      return (
        <AssetRegistry
          search={search}
          onSearchChange={(next, options) => {
            onSearchChange(next, options)
            setSearch(next)
          }}
        />
      )
    }

    renderWithProviders(<FilterHarness />)

    expect((await screen.findAllByText('FE-026')).length).toBeGreaterThan(0)
    expect(
      screen.getByPlaceholderText(
        'Asset code, QR, building, department, or location',
      ),
    ).toHaveValue('FE')
    expect(
      screen.getByText('Asset category: Fire extinguishers'),
    ).toBeInTheDocument()
    expect(screen.getByText('Status: Active')).toBeInTheDocument()
    expect(screen.getByText('Building: Main Building')).toBeInTheDocument()
    expect(screen.getByText('Department: GSD')).toBeInTheDocument()
    expect(screen.getByText('Search: FE')).toBeInTheDocument()

    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Remove Search filter' }))

    expect(onSearchChange).toHaveBeenCalledWith(
      {
        text: undefined,
        building: 'Main Building',
        department: 'GSD',
        assetCategory: 'fire-extinguisher',
        status: 'Active',
        page: 1,
      },
      { preserveScroll: true },
    )
    await waitFor(() => {
      expect(screen.queryByText('Search: FE')).not.toBeInTheDocument()
      expect(
        screen.getByPlaceholderText(
          'Asset code, QR, building, department, or location',
        ),
      ).toHaveValue('')
    })
    expect(
      screen.getByText('Asset category: Fire extinguishers'),
    ).toBeInTheDocument()
    expect(screen.getByText('Status: Active')).toBeInTheDocument()
    expect(screen.getByText('Building: Main Building')).toBeInTheDocument()
    expect(screen.getByText('Department: GSD')).toBeInTheDocument()
    expect(screen.getByText('Showing 1-25 of 26')).toBeInTheDocument()
  })

  it('replaces navigation when requested page is out of bounds', async () => {
    server.use(
      ...registryHandlers(),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    function OutOfBoundsHarness() {
      const [search, setSearch] = useState<AssetSearch>({ page: 99 })
      return (
        <AssetRegistry
          search={search}
          onSearchChange={(next, options) => {
            onSearchChange(next, options)
            setSearch(next)
          }}
        />
      )
    }

    renderWithProviders(<OutOfBoundsHarness />)

    expect(await screen.findAllByText('FE-001')).toBeTruthy()
    expect(onSearchChange).toHaveBeenCalledWith(
      expect.objectContaining({ page: undefined }),
      { replace: true, preserveScroll: true },
    )
  })

  it('keeps filters and leaves focus on pagination after changing pages', async () => {
    const records = Array.from({ length: 26 }, (_, index) => ({
      ...sampleAssets[0]!,
      id: `10000000-0000-4000-8000-${(index + 1).toString().padStart(12, '0')}`,
      assetCode: `FE-${(index + 1).toString().padStart(3, '0')}`,
    }))
    server.use(
      ...registryHandlers(records),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )
    const onSearchChange = vi.fn()

    function PaginationHarness() {
      const [search, setSearch] = useState<AssetSearch>({ text: 'FE', page: 1 })
      return (
        <AssetRegistry
          search={search}
          onSearchChange={(next, options) => {
            onSearchChange(next, options)
            setSearch(next)
          }}
        />
      )
    }

    renderWithProviders(<PaginationHarness />)
    await screen.findAllByText('FE-001')
    const next = screen.getByRole('button', { name: 'Next' })
    expect(screen.getByText('Showing 1-25 of 26')).toBeInTheDocument()
    await userEvent.setup().click(next)
    expect(onSearchChange).toHaveBeenCalledWith(
      { text: 'FE', page: 2 },
      { preserveScroll: true },
    )
    expect(await screen.findAllByText('FE-026')).not.toHaveLength(0)
    expect(screen.getByText('Showing 26-26 of 26')).toBeInTheDocument()
    expect(next).toHaveFocus()
  })

  it('resets draft inputs and triggers search reset on Clear button click', async () => {
    server.use(
      ...registryHandlers(),
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
    await actor.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(onSearchChange).toHaveBeenCalledWith(
      { page: 1 },
      { preserveScroll: true },
    )
    expect(
      screen.getByPlaceholderText(
        'Asset code, QR, building, department, or location',
      ),
    ).toHaveValue('')
  })

  it('passes the combined server-side filters to the API', async () => {
    let capturedUrl: URL | null = null
    server.use(
      http.get(assetRegistryUrl, ({ request }) => {
        capturedUrl = new URL(request.url)
        return HttpResponse.json({
          items: sampleAssets,
          page: 1,
          pageSize: 25,
          totalCount: sampleAssets.length,
        })
      }),
      ...registryHandlers(),
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
    expect(capturedUrl!.searchParams.get('search')).toBe('FA-002')
    expect(capturedUrl!.searchParams.get('text')).toBeNull()
  })

  it('searches long building and department lists before applying combined filters', async () => {
    const records = Array.from({ length: 60 }, (_, index) => {
      const sequence = String(index + 1).padStart(3, '0')
      return {
        ...sampleAssets[0]!,
        id: `77777777-7777-4777-8777-${String(index + 1).padStart(12, '0')}`,
        assetCode: `FE-${sequence}`,
        building: `Science Building ${sequence}`,
        department: `Department ${sequence}`,
      }
    })
    server.use(
      ...registryHandlers(records),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    const onSearchChange = vi.fn()
    function FilterHarness() {
      const [search, setSearch] = useState<AssetSearch>({})
      return (
        <AssetRegistry
          search={search}
          onSearchChange={(next, options) => {
            onSearchChange(next, options)
            setSearch(next)
          }}
        />
      )
    }

    renderWithProviders(<FilterHarness />)
    const actor = userEvent.setup()
    await screen.findAllByText('FE-001')
    await actor.selectOptions(
      screen.getByRole('combobox', { name: 'Asset category' }),
      'fire-extinguisher',
    )
    await actor.selectOptions(screen.getByLabelText('Status'), 'Active')

    const buildingPicker = screen.getByRole('combobox', { name: 'Building' })
    await actor.click(buildingPicker)
    await screen.findByRole('option', { name: 'Science Building 059' })
    await actor.type(buildingPicker, 'Science Building 059')
    await screen.findByRole('option', { name: 'Science Building 059' })
    await actor.keyboard('{ArrowDown}{Enter}')
    const departmentPicker = screen.getByRole('combobox', {
      name: 'Department',
    })
    await actor.click(departmentPicker)
    await screen.findByRole('option', { name: 'Department 059' })
    await actor.type(departmentPicker, 'Department 059')
    await screen.findByRole('option', { name: 'Department 059' })
    await actor.keyboard('{ArrowDown}{Enter}')
    await actor.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => {
      expect(onSearchChange).toHaveBeenCalledWith(
        expect.objectContaining({
          assetCategory: 'fire-extinguisher',
          status: 'Active',
          building: 'Science Building 059',
          department: 'Department 059',
          page: 1,
        }),
        { preserveScroll: true },
      )
    })
    expect(await screen.findAllByText('FE-059')).not.toHaveLength(0)
  })

  it('displays loading skeleton elements while queries are pending', async () => {
    server.use(
      http.get(meUrl, () => HttpResponse.json(gsdUser)),
      http.get(assetRegistryUrl, async ({ request }) => {
        await new Promise((r) => setTimeout(r, 2000))
        const params = new URL(request.url).searchParams
        const page = Number(params.get('page') ?? 1)
        const pageSize = Number(params.get('pageSize') ?? 25)
        return HttpResponse.json({
          items: sampleAssets,
          page,
          pageSize,
          totalCount: sampleAssets.length,
        })
      }),
      ...registryHandlers(),
      http.get(categoriesUrl, async () => {
        await new Promise((r) => setTimeout(r, 2000))
        return HttpResponse.json(sampleCategories)
      }),
    )

    renderWithProviders(
      <AssetRegistry search={{ page: 1 }} onSearchChange={vi.fn()} />,
    )

    expect(
      await screen.findByRole('status', { name: 'Loading assets' }),
    ).toBeInTheDocument()
  })

  it('handles category reference data failure with retry state without hiding asset totals', async () => {
    let categoryAttempts = 0
    server.use(
      ...registryHandlers(),
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
      await screen.findByText('Categories unavailable'),
    ).toBeInTheDocument()
    expect(await screen.findByText('2 matching assets')).toBeVisible()
    expect(screen.getAllByText('FE-001').length).toBeGreaterThan(0)

    const actor = userEvent.setup()
    await actor.click(screen.getByRole('button', { name: 'Retry categories' }))

    expect(
      (await screen.findAllByText('Fire extinguishers')).length,
    ).toBeGreaterThan(0)
  })

  it('distinguishes empty registry from filter no-match state', async () => {
    server.use(
      ...registryHandlers([]),
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
      ...registryHandlers(),
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

  it('retries a failed registry request without displaying false empty results', async () => {
    let registryAttempts = 0
    server.use(
      http.get(assetRegistryUrl, () => {
        registryAttempts++
        return registryAttempts === 1
          ? HttpResponse.json(null, { status: 500 })
          : HttpResponse.json({
              items: sampleAssets,
              page: 1,
              pageSize: 25,
              totalCount: sampleAssets.length,
            })
      }),
      ...registryHandlers(),
      http.get(categoriesUrl, () => HttpResponse.json(sampleCategories)),
    )

    renderWithProviders(
      <AssetRegistry
        search={{ text: '', building: '', department: '', page: 1 }}
        onSearchChange={vi.fn()}
      />,
    )

    expect(
      await screen.findByText('Assets could not be loaded. Please try again.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Retry', exact: true }),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('No assets are registered yet.'),
    ).not.toBeInTheDocument()
    await userEvent
      .setup()
      .click(screen.getByRole('button', { name: 'Retry', exact: true }))
    expect((await screen.findAllByText('FE-001')).length).toBeGreaterThan(0)
    expect(screen.getByText('2 matching assets')).toBeVisible()
  })
})
