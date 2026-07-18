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

test('a stale refresh cannot overwrite the cookie from a later logout and login', async ({
  context,
  page,
}) => {
  let loginCount = 0
  let refreshCount = 0
  let releaseStaleRefresh!: () => void
  let markStaleRefreshStarted!: () => void
  let markStaleResponseHandled!: () => void
  const staleRefreshGate = new Promise<void>((resolve) => {
    releaseStaleRefresh = resolve
  })
  const staleRefreshStarted = new Promise<void>((resolve) => {
    markStaleRefreshStarted = resolve
  })
  const staleResponseHandled = new Promise<void>((resolve) => {
    markStaleResponseHandled = resolve
  })

  await page.route(`${apiPattern}/login`, (route) => {
    loginCount += 1
    const suffix = loginCount === 1 ? 'a' : 'b'
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: {
        'set-cookie': `unipm_refresh=session-${suffix}; Path=/; HttpOnly; SameSite=Lax`,
      },
      body: JSON.stringify({
        ...fictionalSession,
        accessToken: `synthetic-session-${suffix}-access-token`,
      }),
    })
  })
  await page.route(`${apiPattern}/logout`, (route) => {
    expect(route.request().headers().cookie).toContain(
      'unipm_refresh=stale-session-a',
    )
    return route.fulfill({
      status: 204,
      headers: {
        'set-cookie':
          'unipm_refresh=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0',
      },
    })
  })
  await page.route(`${apiPattern}/refresh`, async (route) => {
    refreshCount += 1
    if (refreshCount === 1) {
      return route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Unauthorized', status: 401 }),
      })
    }

    if (refreshCount === 2) {
      expect(route.request().headers().cookie).toContain(
        'unipm_refresh=session-a',
      )
      markStaleRefreshStarted()
      await staleRefreshGate
      try {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          headers: {
            'set-cookie':
              'unipm_refresh=stale-session-a; Path=/; HttpOnly; SameSite=Lax',
          },
          body: JSON.stringify({
            ...fictionalSession,
            accessToken: 'stale-session-a-access-token',
          }),
        })
      } catch {
        // The page may close before a delayed intercepted response is fulfilled.
      } finally {
        markStaleResponseHandled()
      }
      return
    }

    expect(route.request().headers().cookie).toContain(
      'unipm_refresh=session-b',
    )
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: {
        'set-cookie':
          'unipm_refresh=session-b-refreshed; Path=/; HttpOnly; SameSite=Lax',
      },
      body: JSON.stringify({
        ...fictionalSession,
        accessToken: 'refreshed-session-b-access-token',
      }),
    })
  })

  await page.goto('/login')
  await completeLogin(page)
  await expect(page).toHaveURL(/\/app\/dashboard$/)

  await page.evaluate(() => {
    window.eval(
      "window.__staleRefresh = import('/src/features/auth/auth-session-service.ts').then((module) => module.refreshAccessToken()).catch(() => null)",
    )
  })
  await staleRefreshStarted
  await page.evaluate(() =>
    window.eval(`import('/src/features/auth/auth-session-service.ts').then((module) => {
      window.__logoutDuringRefresh = module.logout()
      window.__loginAfterLogout = module.authenticate({
        email: 'fictional.inspector@example.test',
        password: 'Synthetic Browser Password'
      })
      return true
    })`),
  )
  releaseStaleRefresh()
  await staleResponseHandled
  await page.evaluate(() =>
    window.eval(
      'Promise.all([window.__staleRefresh, window.__logoutDuringRefresh, window.__loginAfterLogout])',
    ),
  )

  let refreshCookie = (await context.cookies()).find(
    (cookie) => cookie.name === 'unipm_refresh',
  )
  expect(refreshCookie?.value).toBe('session-b')

  await page.evaluate(() =>
    window.eval(
      "import('/src/features/auth/auth-session-service.ts').then((module) => module.refreshAccessToken())",
    ),
  )
  refreshCookie = (await context.cookies()).find(
    (cookie) => cookie.name === 'unipm_refresh',
  )
  expect(refreshCookie?.value).toBe('session-b-refreshed')
  expect(refreshCount).toBe(3)
})

test('unknown routes keep the safe not-found boundary', async ({ page }) => {
  await mockAnonymousRefresh(page)
  await page.goto('/unknown')
  await expect(
    page.getByRole('heading', { name: 'Page not found' }),
  ).toBeVisible()
})
