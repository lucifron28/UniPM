import { expect, test, type Page } from '@playwright/test'

const apiPattern = '**/api/v1/auth'
const fictionalCredentials = {
  email: 'fictional.inspector@example.test',
  password: 'Synthetic Browser Password',
}
const fictionalSession = {
  accessToken: 'synthetic-browser-access-token',
  expiresAtUtc: '2026-07-18T12:00:00Z',
  user: {
    id: '11111111-1111-4111-8111-111111111111',
    email: fictionalCredentials.email,
    displayName: 'Fictional Inspector',
    roles: ['Inspector'],
  },
}

async function mockAnonymousRefresh(page: Page) {
  await page.route(`${apiPattern}/refresh`, (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Unauthorized', status: 401 }),
    }),
  )
}

async function mockSuccessfulLogin(page: Page) {
  await page.route(`${apiPattern}/login`, async (route) => {
    const body = route.request().postDataJSON()
    expect(body).toEqual(fictionalCredentials)
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(fictionalSession),
    })
  })
}

async function completeLogin(page: Page) {
  await page.getByLabel('Institutional email').fill(fictionalCredentials.email)
  await page.getByLabel('Password').fill(fictionalCredentials.password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

test('login form renders and supports keyboard submission', async ({
  page,
}) => {
  await mockAnonymousRefresh(page)
  await mockSuccessfulLogin(page)
  await page.goto('/login')

  await expect(
    page.getByRole('heading', { name: 'Welcome back' }),
  ).toBeVisible()
  await page.getByLabel('Institutional email').focus()
  await page.keyboard.type(fictionalCredentials.email)
  await page.keyboard.press('Tab')
  await expect(page.getByLabel('Password')).toBeFocused()
  await page.keyboard.type(fictionalCredentials.password)
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/app\/dashboard$/)
})

test('invalid form submission shows associated validation errors', async ({
  page,
}) => {
  await mockAnonymousRefresh(page)
  await page.goto('/login')
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page.getByText('Enter a valid email address.')).toBeVisible()
  await expect(page.getByText('Enter your password.')).toBeVisible()
  await expect(page.getByLabel('Institutional email')).toHaveAttribute(
    'aria-invalid',
    'true',
  )
})

test('failed login shows only a generic authentication error', async ({
  page,
}) => {
  await mockAnonymousRefresh(page)
  await page.route(`${apiPattern}/login`, (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        title: 'Unauthorized',
        detail: 'Account-specific detail that must stay hidden.',
      }),
    }),
  )
  await page.goto('/login')
  await completeLogin(page)

  await expect(page.getByText('Invalid email or password.')).toBeVisible()
  await expect(page.getByText(/Account-specific detail/)).toHaveCount(0)
})

test('successful login opens the dashboard and displays returned identity', async ({
  page,
}) => {
  await mockAnonymousRefresh(page)
  await mockSuccessfulLogin(page)
  await page.goto('/login')
  await completeLogin(page)

  await expect(page).toHaveURL(/\/app\/dashboard$/)
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  const sidebar = page.getByRole('complementary')
  await expect(sidebar.getByText('Fictional Inspector')).toBeVisible()
  await expect(sidebar.getByText(fictionalCredentials.email)).toBeVisible()
})

test('direct protected navigation restores through the refresh cookie contract', async ({
  page,
}) => {
  await page.route(`${apiPattern}/refresh`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(fictionalSession),
    }),
  )
  await page.goto('/app/dashboard')

  await expect(page).toHaveURL(/\/app\/dashboard$/)
  await expect(
    page.getByRole('complementary').getByText('Fictional Inspector'),
  ).toBeVisible()
})

test('anonymous protected navigation redirects only after restoration', async ({
  page,
}) => {
  await mockAnonymousRefresh(page)
  await page.goto('/app/dashboard')

  await expect(page).toHaveURL(/\/login\?redirect=%2Fapp%2Fdashboard$/)
  await expect(
    page.getByRole('heading', { name: 'Welcome back' }),
  ).toBeVisible()
})

test('logout is locally final and returns to login', async ({ page }) => {
  await page.route(`${apiPattern}/refresh`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(fictionalSession),
    }),
  )
  await page.route(`${apiPattern}/logout`, (route) =>
    route.fulfill({ status: 204 }),
  )
  await page.goto('/app/dashboard')
  await page.getByRole('button', { name: 'Sign out' }).first().click()

  await expect(page).toHaveURL(/\/login$/)
  await expect(
    page.getByRole('heading', { name: 'Welcome back' }),
  ).toBeVisible()
})

test('access token is never stored in browser storage', async ({ page }) => {
  await mockAnonymousRefresh(page)
  await mockSuccessfulLogin(page)
  await page.goto('/login')
  await completeLogin(page)

  expect(
    await page.evaluate(() => ({
      local: { ...localStorage },
      session: { ...sessionStorage },
    })),
  ).toEqual({ local: {}, session: {} })
})

test('unsafe redirect input falls back to the dashboard', async ({ page }) => {
  await mockAnonymousRefresh(page)
  await mockSuccessfulLogin(page)
  await page.goto('/login?redirect=%2F%2Fevil.example')
  await completeLogin(page)

  await expect(page).toHaveURL(/\/app\/dashboard$/)
})

test('unknown routes keep the safe not-found boundary', async ({ page }) => {
  await mockAnonymousRefresh(page)
  await page.goto('/unknown')
  await expect(
    page.getByRole('heading', { name: 'Page not found' }),
  ).toBeVisible()
})
