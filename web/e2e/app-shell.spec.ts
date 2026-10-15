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
  await expect(sidebar).toHaveCSS('width', '76px')
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
    path: 'test-results/web-ui-polish-sidebar-collapsed-desktop.png',
  })
  const collapsedAlignment = await page.evaluate(() => {
    const sidebar = document.querySelector('.app-sidebar')!
    const nav = sidebar.querySelector('nav')!
    const elements = [
      sidebar.querySelector('img[alt="UniPM"]')!,
      sidebar.querySelector('[aria-label="Expand navigation"]')!,
      ...nav.querySelectorAll('a'),
      sidebar.querySelector('details summary')!,
    ]
    const boxes = elements.map((element) => {
      const bounds = element.getBoundingClientRect()
      return {
        centerX: bounds.left + bounds.width / 2,
        width: bounds.width,
        height: bounds.height,
      }
    })
    return boxes
  })
  expect(
    Math.max(...collapsedAlignment.map((box) => box.centerX)) -
      Math.min(...collapsedAlignment.map((box) => box.centerX)),
  ).toBeLessThan(1)
  expect(
    collapsedAlignment
      .slice(2, -1)
      .every((box) => box.width === 44 && box.height === 44),
  ).toBe(true)

  await page.getByLabel('Account menu').click()
  await expect(sidebar.getByText('fictional.gsd@example.test')).toBeVisible()
  await expect(sidebar.getByRole('button', { name: 'Sign out' })).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-account-menu-collapsed.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar.locator('details')).not.toHaveAttribute('open', '')

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
  await expect(sidebar).toHaveCSS('width', '248px')
  await expect(
    sidebar.getByText('Institutional operations').last(),
  ).toBeVisible()
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-expanded-desktop.png',
  })
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
  await page.screenshot({
    path: 'test-results/web-ui-polish-sidebar-account-menu-expanded.png',
  })
  await page.keyboard.press('Escape')
  await expect(sidebar.locator('details')).not.toHaveAttribute('open', '')
  await page.setViewportSize({ width: 1024, height: 768 })
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1)
  await page.screenshot({
    path: 'test-results/web-ui-polish-small-laptop.png',
  })
})

test('active navigation indicator slides and follows browser history', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 })
  await mockSession(page)
  await page.goto('/app/dashboard')

  const indicator = page.locator('.sidebar-active-indicator')
  const assetsLink = page.getByRole('link', { name: 'Assets' })
  await expect(indicator).toHaveCSS('opacity', '1')
  await indicator.evaluate((element) => {
    const marker = element as HTMLElement
    const handler = (event: Event) => {
      if ((event as TransitionEvent).propertyName !== 'transform') return
      marker.removeEventListener('transitionrun', handler)
      const samples: number[] = []
      const startedAt = performance.now()
      const sample = () => {
        samples.push(marker.getBoundingClientRect().top)
        if (performance.now() - startedAt >= 260) {
          marker.dataset.transitionSamples = JSON.stringify(samples)
        } else {
          requestAnimationFrame(sample)
        }
      }
      requestAnimationFrame(sample)
    }
    marker.addEventListener('transitionrun', handler)
  })
  await assetsLink.click()
  await expect(assetsLink).toHaveAttribute('aria-current', 'page')
  await page.waitForFunction(
    () =>
      document
        .querySelector('.sidebar-active-indicator')
        ?.getAttribute('data-transition-samples') !== null,
  )
  const samples = JSON.parse(
    (await indicator.getAttribute('data-transition-samples')) ?? '[]',
  ) as number[]
  expect(samples.length).toBeGreaterThan(3)
  expect(
    new Set(samples.map((value) => Math.round(value * 10))).size,
  ).toBeGreaterThan(2)

  await page.goBack()
  const dashboardLink = page.getByRole('link', { name: 'Dashboard' })
  await expect(dashboardLink).toHaveAttribute('aria-current', 'page')
  await expect
    .poll(async () =>
      indicator.evaluate((element) => {
        const active = document.querySelector(
          'nav[aria-label="Primary"] [aria-current="page"]',
        )!
        return Math.abs(
          element.getBoundingClientRect().top -
            active.getBoundingClientRect().top,
        )
      }),
    )
    .toBeLessThan(1)
  await page.goForward()
  await expect(assetsLink).toHaveAttribute('aria-current', 'page')

  await page.emulateMedia({ reducedMotion: 'reduce' })
  const reducedMotionDuration = await indicator.evaluate((element) =>
    Number.parseFloat(getComputedStyle(element).transitionDuration),
  )
  expect(reducedMotionDuration).toBeLessThan(0.001)
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
    path: 'test-results/web-ui-polish-mobile-navigation.png',
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
