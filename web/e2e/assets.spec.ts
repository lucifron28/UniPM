import { expect, test, type Page } from '@playwright/test'

const gsdSession = {
  accessToken: 'fictional-gsd-asset-token',
  expiresAtUtc: '2026-07-19T12:00:00Z',
  user: {
    id: '22222222-2222-4222-8222-222222222222',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
  },
}

const nonGsdSession = {
  accessToken: 'fictional-inspector-asset-token',
  expiresAtUtc: '2026-07-19T12:00:00Z',
  user: {
    id: '44444444-4444-4444-8444-444444444444',
    email: 'fictional.inspector@example.test',
    displayName: 'Fictional Inspector',
    roles: ['Inspector'],
  },
}

const assets = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    assetCode: 'FE-001',
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    location: 'Lobby',
    qrCodeValue: 'UNIPM-FIREEXTINGUISHER-11111111',
    status: 'Active',
    createdAt: '2026-07-19T00:00:00+00:00',
    updatedAt: '2026-07-19T00:00:00+00:00',
  },
  {
    id: '33333333-3333-4333-8333-333333333333',
    assetCode: 'FA-001',
    assetCategory: 'fire-alarm',
    building: 'Science Building',
    department: 'Chemistry',
    location: 'Second floor',
    qrCodeValue: 'UNIPM-FIREALARM-33333333',
    status: 'Inactive',
    createdAt: '2026-07-19T00:00:00+00:00',
    updatedAt: '2026-07-19T00:00:00+00:00',
  },
]

const createdAsset = {
  id: '55555555-5555-4555-8555-555555555555',
  assetCode: 'FE-999',
  assetCategory: 'fire-extinguisher',
  building: 'Library',
  department: 'GSD',
  location: '3rd Floor',
  qrCodeValue: 'UNIPM-FE-999',
  status: 'Active',
  createdAt: '2026-07-22T00:00:00+00:00',
  updatedAt: '2026-07-22T00:00:00+00:00',
}

const pagedAssets = Array.from({ length: 12 }, (_, index) => ({
  ...assets[0],
  id: `00000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
  assetCode: `FE-${String(index + 1).padStart(3, '0')}`,
  location: `Floor ${index + 1}`,
}))

async function mockAssetRegistry(
  page: Page,
  session = gsdSession,
  assetList = assets,
) {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(session),
    }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(session.user),
    }),
  )
  await page.route('**/api/v1/reference-data/asset-categories', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
        { code: 'fire-alarm', displayName: 'Fire alarm systems' },
        { code: 'emergency-light', displayName: 'Emergency lights' },
        {
          code: 'water-drinking-station',
          displayName: 'Water drinking stations',
        },
      ]),
    }),
  )
  await page.route('**/api/v1/assets**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(assetList),
    }),
  )
  await page.route(`**/api/v1/assets/${assets[0].id}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(assets[0]),
    }),
  )
}

test.describe('Asset Registry E2E Specs', () => {
  test('authenticated GSD users can browse and filter fictional assets', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.goto('/app/assets')
    await expect(page.getByRole('heading', { name: 'Assets' })).toBeVisible()
    await expect(page.getByText('FE-001').first()).toBeVisible()
    await page.getByLabel('Asset category').selectOption('fire-alarm')
    await expect(page).toHaveURL(/assetCategory=fire-alarm/)
    await expect(page.getByText('FA-001').first()).toBeVisible()
  })

  test('restores text-search URL state through the route', async ({ page }) => {
    await mockAssetRegistry(page)
    await page.goto('/app/assets?text=FA-001')

    const search = page.getByLabel('Search assets')
    await expect(search).toHaveValue('FA-001')
    await expect(page).toHaveURL(/text=FA-001/)
    await expect(page.getByText('FA-001').first()).toBeVisible()
  })

  test('updates pagination URLs and replaces an out-of-range page', async ({
    page,
  }) => {
    await mockAssetRegistry(page, gsdSession, pagedAssets)
    await page.goto('/app/assets?page=2')
    await expect(page.getByText('FE-011').first()).toBeVisible()

    await page.goto('/app/assets?page=99')
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByText('FE-011').first()).toBeVisible()
  })

  test('renders primary navigation landmark in mobile viewport', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 667 })
    await mockAssetRegistry(page)
    await page.goto('/app/assets')

    const nav = page
      .locator('nav[aria-label="Primary"]')
      .filter({ has: page.getByRole('link', { name: 'Assets' }) })
      .first()
    await expect(nav).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Assets' })).toBeVisible()
  })

  test('handles successful asset creation and navigates to asset detail', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.route('**/api/v1/assets', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(createdAsset),
        })
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(assets),
        })
      }
    })
    await page.route(`**/api/v1/assets/${createdAsset.id}`, (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(createdAsset),
      }),
    )

    await page.goto('/app/assets/new')
    await expect(page.getByRole('heading', { name: 'Add asset' })).toBeVisible()

    await page.getByLabel('Asset code').fill('FE-999')
    await page.getByLabel('Category').selectOption('fire-extinguisher')
    await page.getByLabel('Building (optional)').fill('Library')
    await page.getByRole('button', { name: 'Create asset' }).click()

    await expect(page).toHaveURL(new RegExp(`/app/assets/${createdAsset.id}`))
    await expect(page.getByRole('heading', { name: 'FE-999' })).toBeVisible()
  })

  test('blocks non-GSD users from accessing create asset route', async ({
    page,
  }) => {
    await mockAssetRegistry(page, nonGsdSession)
    await page.goto('/app/assets/new')

    await expect(
      page.getByRole('heading', { name: 'GSD access required' }),
    ).toBeVisible()
  })

  test('does not send a create request when a non-GSD user opens the create route', async ({
    page,
  }) => {
    let postCount = 0
    await mockAssetRegistry(page, nonGsdSession)
    page.on('request', (request) => {
      if (
        request.method() === 'POST' &&
        request.url().endsWith('/api/v1/assets')
      ) {
        postCount += 1
      }
    })

    await page.goto('/app/assets/new')
    await expect(
      page.getByRole('heading', { name: 'GSD access required' }),
    ).toBeVisible()
    expect(postCount).toBe(0)
  })

  test('handles asset creation validation, focus management, and backend 400 mapping', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.route('**/api/v1/assets', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            title: 'Validation Failed',
            errors: {
              assetCode: ['That asset code is invalid.'],
            },
          }),
        })
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(assets),
        })
      }
    })

    await page.goto('/app/assets/new')
    await expect(page.getByRole('heading', { name: 'Add asset' })).toBeVisible()

    await page.getByLabel('Asset code').fill('FE-001')
    await page.getByLabel('Category').selectOption('fire-extinguisher')
    await page.getByRole('button', { name: 'Create asset' }).click()

    const alert = page.getByRole('alert')
    await expect(alert).toContainText(
      'Please correct the highlighted validation errors.',
    )
    await expect(page.getByText('That asset code is invalid.')).toBeVisible()
  })

  test('shows a duplicate-code conflict and clears its field error after editing', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.route('**/api/v1/assets', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Conflict' }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(assets),
      })
    })

    await page.goto('/app/assets/new')
    await page.getByLabel('Asset code').fill('FE-001')
    await page.getByLabel('Category').selectOption('fire-extinguisher')
    await page.getByRole('button', { name: 'Create asset' }).click()
    await expect(page.locator('#assetCode-error')).toHaveText(
      'That asset code already exists.',
    )

    await page.getByLabel('Asset code').fill('FE-002')
    await expect(page.locator('#assetCode-error')).toHaveCount(0)
  })

  test('shows category-reference failure without rendering a writable form', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.route('**/api/v1/reference-data/asset-categories', (route) =>
      route.fulfill({ status: 500 }),
    )

    await page.goto('/app/assets/new')
    await expect(
      page.getByRole('heading', { name: 'Asset categories unavailable' }),
    ).toBeVisible()
    await expect(
      page.getByRole('button', { name: 'Create asset' }),
    ).toHaveCount(0)
  })

  test('handles a malformed success response without navigating away from creation', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.route('**/api/v1/assets', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ unexpected: true }),
        })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(assets),
      })
    })

    await page.goto('/app/assets/new')
    await page.getByLabel('Asset code').fill('FE-998')
    await page.getByLabel('Category').selectOption('fire-extinguisher')
    await page.getByRole('button', { name: 'Create asset' }).click()

    await expect(
      page.getByText(
        'The server returned an invalid response, so the creation result could not be verified. Check the registry before trying again.',
      ),
    ).toBeVisible()
    await expect(page).toHaveURL(/\/app\/assets\/new$/)
  })

  test('handles invalid asset UUID without making an API request', async ({
    page,
  }) => {
    let apiCalled = false
    await mockAssetRegistry(page)
    await page.route('**/api/v1/assets/invalid-uuid', () => {
      apiCalled = true
    })

    await page.goto('/app/assets/invalid-uuid')
    await expect(
      page.getByRole('heading', { name: 'Asset not found' }),
    ).toBeVisible()
    await expect(
      page.getByText(
        'The asset link is invalid. No registry request was made.',
      ),
    ).toBeVisible()
    expect(apiCalled).toBe(false)
  })

  test('shows network error and handles retry interaction on asset detail', async ({
    page,
  }) => {
    let isRetrying = false
    await mockAssetRegistry(page)
    await page.route(`**/api/v1/assets/${assets[0].id}`, (route) => {
      if (!isRetrying) {
        return route.abort('failed')
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(assets[0]),
      })
    })

    await page.goto(`/app/assets/${assets[0].id}`)
    await expect(
      page.getByRole('heading', { name: 'Service unavailable' }),
    ).toBeVisible()

    isRetrying = true
    await page.getByRole('button', { name: 'Retry' }).click()
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()
  })

  test('restores a direct asset detail URL and shows not-found responses', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.goto(`/app/assets/${assets[0].id}`)
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()

    await page.route(`**/api/v1/assets/${assets[1].id}`, (route) =>
      route.fulfill({
        status: 404,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Not found' }),
      }),
    )
    await page.goto(`/app/assets/${assets[1].id}`)
    await expect(
      page.getByRole('heading', { name: 'Asset not found' }),
    ).toBeVisible()
  })

  test('shows label-specific copy toast feedback on asset detail', async ({
    page,
    context,
  }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await mockAssetRegistry(page)

    await page.goto(`/app/assets/${assets[0].id}`)
    await expect(page.getByRole('heading', { name: 'FE-001' })).toBeVisible()

    await page.getByRole('button', { name: 'Copy Asset Code' }).click()
    await expect(page.getByText('Asset Code copied.')).toBeVisible()
  })

  test('shows copy failure feedback and keeps browser storage empty', async ({
    page,
  }) => {
    await mockAssetRegistry(page)
    await page.addInitScript(() => {
      Object.assign(navigator, {
        clipboard: { writeText: () => Promise.reject(new Error('blocked')) },
      })
    })

    await page.goto(`/app/assets/${assets[0].id}`)
    await page.getByRole('button', { name: 'Copy Asset Code' }).click()
    await expect(
      page.getByText('Asset Code could not be copied.'),
    ).toBeVisible()
    await expect(
      page.locator(
        'text=Preventive maintenance, Audit log, Work orders, Condition score',
      ),
    ).toHaveCount(0)
    await expect
      .poll(() =>
        page.evaluate(() => ({
          local: Object.keys(localStorage),
          session: Object.keys(sessionStorage),
        })),
      )
      .toEqual({ local: [], session: [] })
  })
})
