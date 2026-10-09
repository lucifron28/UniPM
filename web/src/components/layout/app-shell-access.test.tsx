import { render, screen, waitFor, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { AppShell } from '@/components/layout/app-shell'
import { routeTree } from '@/routeTree.gen'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const meUrl = '*/api/v1/auth/me'

function currentUser(roles: readonly string[]) {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    email: 'admin@example.test',
    displayName: 'Synthetic Admin',
    roles: [...roles],
  }
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

function renderShell(initialEntry = '/') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const rootRoute = createRootRoute({ component: () => <AppShell /> })
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: [initialEntry] }),
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

function renderAppRoute(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const router = createRouter({
    routeTree,
    context: {
      queryClient,
      getAccessToken: () => useAuthStore.getState().accessToken,
    },
    history: createMemoryHistory({ initialEntries: [initialEntry] }),
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('app shell role access and layout', () => {
  beforeEach(() => setupAuth())

  it('hides operational navigation from Admin while retaining general pages', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['Admin']))))

    renderShell()

    await screen.findAllByText('Synthetic Admin')
    const navs = await screen.findAllByRole('navigation', { name: 'Primary' })
    expect(navs).toHaveLength(2)
    for (const nav of navs) {
      expect(within(nav).getByRole('link', { name: 'Dashboard' })).toBeVisible()
      expect(within(nav).getByRole('link', { name: 'Assets' })).toBeVisible()
      expect(
        within(nav).getByRole('link', { name: 'Inspections' }),
      ).toBeVisible()
      expect(within(nav).queryByRole('link', { name: /Schedules/ })).toBeNull()
      expect(
        within(nav).queryByRole('link', { name: /Form review|Forms/ }),
      ).toBeNull()
    }
  })

  it('keeps the desktop sidebar viewport-bound with a scrollable nav and fixed footer', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(currentUser(['GSD']))))

    renderShell()

    await screen.findAllByText('Synthetic Admin')
    const sidebar = screen.getByRole('complementary')
    expect(sidebar.className).toContain('lg:sticky')
    expect(sidebar.className).toContain('lg:top-0')
    expect(sidebar.className).toContain('lg:h-screen')
    expect(sidebar.className).toContain('lg:overflow-hidden')

    const navs = screen.getAllByRole('navigation', {
      name: 'Primary',
    })
    expect(navs).toHaveLength(2)
    const desktopNav = navs[0]!
    const mobileNav = navs[1]!
    expect(desktopNav.className).toContain('min-h-0')
    expect(desktopNav.className).toContain('overflow-y-auto')
    expect(mobileNav.className).toContain('flex-wrap')

    const footer = sidebar.lastElementChild
    expect(footer?.className).toContain('shrink-0')
    expect(
      within(sidebar).getByRole('button', { name: 'Sign out' }),
    ).toBeVisible()
  })

  it.each([
    { path: '/app/schedules', roles: ['Admin'] },
    {
      path: '/app/schedules/11111111-1111-4111-8111-111111111111',
      roles: ['DepartmentHead'],
    },
    { path: '/app/schedules/new', roles: ['Inspector'] },
    { path: '/app/assets/new', roles: ['Admin'] },
    { path: '/app/preventive-maintenance-forms', roles: ['Admin'] },
  ] as const)(
    'denies route access before loading page data at $path',
    async ({ path, roles }) => {
      let operationalRequests = 0
      server.use(
        http.get('*/api/v1/*', ({ request }) => {
          const pathname = new URL(request.url).pathname
          if (pathname === '/api/v1/auth/me') {
            return HttpResponse.json(currentUser(roles))
          }
          operationalRequests += 1
          return HttpResponse.json([])
        }),
      )

      renderAppRoute(path)

      expect(
        await screen.findByRole('heading', { name: 'Access denied' }),
      ).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /Retry/ })).toBeNull()
      expect(operationalRequests).toBe(0)
    },
  )

  it('does not deny a role-restricted route while roles are loading', async () => {
    let releaseCurrentUser!: () => void
    const userResponse = new Promise<void>((resolve) => {
      releaseCurrentUser = resolve
    })
    server.use(
      http.get(meUrl, async () => {
        await userResponse
        return HttpResponse.json(currentUser(['Admin']))
      }),
    )

    renderShell('/app/schedules')

    expect(await screen.findByText('Checking page access…')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
    releaseCurrentUser()
    expect(
      await screen.findByRole('heading', { name: 'Access denied' }),
    ).toBeInTheDocument()
  })

  it('reports current-user errors separately from role denial', async () => {
    server.use(http.get(meUrl, () => new HttpResponse(null, { status: 500 })))

    renderShell('/app/schedules')

    expect(
      await screen.findByRole('heading', { name: 'Unable to verify access' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
    expect(
      screen.getByRole('button', { name: 'Retry access check' }),
    ).toBeInTheDocument()
  })

  it('allows an assigned operational role alongside Admin', async () => {
    let scheduleReads = 0
    server.use(
      http.get('*/api/v1/*', ({ request }) => {
        const pathname = new URL(request.url).pathname
        if (pathname === '/api/v1/auth/me') {
          return HttpResponse.json(currentUser(['Admin', 'GSD']))
        }
        if (pathname === '/api/v1/schedules') scheduleReads += 1
        return HttpResponse.json([])
      }),
    )

    renderAppRoute('/app/schedules')

    await waitFor(() => expect(scheduleReads).toBeGreaterThan(0))
    expect(screen.queryByRole('heading', { name: 'Access denied' })).toBeNull()
  })
})
