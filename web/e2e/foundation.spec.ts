import { expect, test } from '@playwright/test'
test('foundation page renders', async ({ page }) => {
  await page.goto('/')
  await expect(
    page.getByRole('heading', { name: 'Application foundation' }),
  ).toBeVisible()
})

test('login placeholder renders directly', async ({ page }) => {
  await page.goto('/login')
  await expect(
    page.getByRole('heading', { name: 'Login integration is deferred' }),
  ).toBeVisible()
})

test('unauthenticated protected navigation redirects to login', async ({
  page,
}) => {
  await page.goto('/app/dashboard')
  await expect(
    page.getByRole('heading', { name: 'Login integration is deferred' }),
  ).toBeVisible()
})

test('unknown routes render the not-found boundary', async ({ page }) => {
  await page.goto('/unknown')
  await expect(
    page.getByRole('heading', { name: 'Page not found' }),
  ).toBeVisible()
})

test('foundation links are keyboard reachable', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'View login placeholder' }).focus()
  await page.keyboard.press('Tab')
  await expect(
    page.getByRole('link', { name: 'Open protected placeholder' }),
  ).toBeFocused()
})
