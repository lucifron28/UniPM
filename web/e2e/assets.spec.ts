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

async function mockAssetRegistry(page: Page) {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(gsdSession),
    }),
  )
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(gsdSession.user),
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
      body: JSON.stringify(assets),
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

  test('renders primary navigation landmark in mobile viewport', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 667 })
    await mockAssetRegistry(page)
    await page.goto('/app/assets')

    const nav = page.locator('nav[aria-label="Primary"]')
    await expect(nav).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Assets' })).toBeVisible()
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
})
