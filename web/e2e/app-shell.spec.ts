import { expect, test, type Page } from '@playwright/test'

const session = {
  accessToken: 'synthetic-shell-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    id: '22222222-2222-4222-8222-222222222222',
    email: 'fictional.gsd@example.test',
    displayName: 'Fictional GSD User',
    roles: ['GSD'],
  },
}

async function mockSession(page: Page) {
  await page.route('**/api/v1/**', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Service unavailable', status: 503 }),
    }),
  )
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
}

test('desktop shell keeps its header and rail still while the workspace scrolls', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 720 })
  await mockSession(page)
  await page.goto('/app/dashboard')

  const heading = page
    .locator('.app-page-header')
    .getByRole('heading', { name: 'Preventive Maintenance Dashboard' })
  const workspace = page.locator('.app-workspace')
  const sidebar = page.locator('.app-sidebar')
  await expect(heading).toBeVisible()
  await expect(sidebar.getByRole('img', { name: 'UniPM' })).toBeVisible()
  await expect(
    page.getByRole('button', { name: 'Expand navigation' }),
  ).toBeVisible()
  await expect(
    workspace.getByRole('heading', {
      name: 'Preventive Maintenance Dashboard',
    }),
  ).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Assets' })).toHaveAttribute(
    'title',
    'Assets',
  )
  await page.screenshot({
    path: 'test-results/web-ui-refinement-shell-desktop-collapsed.png',
  })
  const aligned = await page.evaluate(() => {
    const heading = document
      .querySelector('.page-heading-host h1')!
      .getBoundingClientRect()
    const account = document
      .querySelector('.app-page-header details summary')!
      .getBoundingClientRect()
    return Math.abs(
      heading.top + heading.height / 2 - (account.top + account.height / 2),
    )
  })
  expect(aligned).toBeLessThan(18)

  const before = await page.evaluate(() => ({
    headerTop: document
      .querySelector('.app-page-header')!
      .getBoundingClientRect().top,
    sidebarTop: document.querySelector('.app-sidebar')!.getBoundingClientRect()
      .top,
  }))
  await workspace.evaluate((element) => {
    const spacer = document.createElement('div')
    spacer.style.height = '1400px'
    element.append(spacer)
    element.scrollTop = 400
  })
  const after = await page.evaluate(() => ({
    headerTop: document
      .querySelector('.app-page-header')!
      .getBoundingClientRect().top,
    sidebarTop: document.querySelector('.app-sidebar')!.getBoundingClientRect()
      .top,
    workspaceScroll: document.querySelector('.app-workspace')!.scrollTop,
    documentScroll: document.scrollingElement!.scrollTop,
    documentHeight: document.documentElement.scrollHeight,
    viewportHeight: window.innerHeight,
  }))
  expect(after.workspaceScroll).toBeGreaterThan(200)
  expect(after.headerTop).toBeCloseTo(before.headerTop, 0)
  expect(after.sidebarTop).toBeCloseTo(before.sidebarTop, 0)
  expect(after.documentScroll).toBe(0)
  expect(after.documentHeight).toBeLessThanOrEqual(after.viewportHeight + 1)
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
  expect(
    await page
      .locator('.app-shell-main')
      .evaluate((element) => getComputedStyle(element).overflowY),
  ).toBe('hidden')
  expect(
    await workspace.evaluate((element) => getComputedStyle(element).overflowY),
  ).toBe('auto')

  await page.getByRole('button', { name: 'Expand navigation' }).click()
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible()
  await expect(
    sidebar.getByText('Institutional operations').last(),
  ).toBeVisible()
  expect(
    await page.evaluate(() => localStorage.getItem('unipm.sidebar.collapsed')),
  ).toBe('false')
  await page.reload()
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible()

  await page.getByLabel('Account menu').click()
  await expect(page.getByText('fictional.gsd@example.test')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.getByLabel('Account menu')).not.toHaveAttribute('open', '')
  await page.setViewportSize({ width: 1024, height: 768 })
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
  await page.screenshot({
    path: 'test-results/web-ui-refinement-shell-small-laptop.png',
  })
})

test('mobile navigation opens, closes, and leaves the page usable', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 667 })
  await mockSession(page)
  await page.goto('/app/dashboard')
  const sidebar = page.locator('.app-sidebar')
  const open = page.getByRole('button', { name: 'Open navigation' })
  await expect(open).toBeVisible()
  await expect(sidebar).toBeHidden()
  await open.click()
  await expect(sidebar).toBeVisible()
  await expect(sidebar.getByRole('link', { name: 'Schedules' })).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-refinement-shell-mobile-navigation.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar).toBeHidden()
  await open.click()
  await sidebar.getByRole('link', { name: 'Assets' }).click()
  await expect(page).toHaveURL(/\/app\/assets/)
  await expect(sidebar).toBeHidden()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
})
