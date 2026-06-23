import { expect, test } from '@playwright/test'
import { dismissOnboarding } from './helpers'

test.describe('首次水平测评', () => {
  test('进入测评页并开始流程', async ({ page }) => {
    await page.goto('/')
    await dismissOnboarding(page)
    await page.getByRole('button', { name: '测评' }).click()

    await expect(page.getByRole('heading', { name: '首次水平测评' })).toBeVisible()
    await page.locator('section').filter({ hasText: '首次水平测评' }).getByRole('button', { name: '开始测评' }).click()

    await expect(page.getByText('词汇识别')).toBeVisible({ timeout: 15_000 })
    await expect(page.getByText('1. 词汇')).toBeVisible()
  })
})
