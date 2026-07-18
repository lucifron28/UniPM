import { expect, test } from '@playwright/test'
test('foundation page and public routes render', async ({ page }) => {
  await page.goto('/')
  await expect(
    page.getByRole('heading', { name: 'Application foundation' }),
  ).toBeVisible()
  await page.goto('/app/dashboard')
  await expect(
    page.getByRole('heading', { name: 'Login integration is deferred' }),
  ).toBeVisible()
  await page.goto('/unknown')
  await expect(
    page.getByRole('heading', { name: 'Page not found' }),
  ).toBeVisible()
})
