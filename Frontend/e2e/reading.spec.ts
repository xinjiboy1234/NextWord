import { expect, test } from '@playwright/test'

test.describe('阅读模块', () => {
  test('短文库加载并打开文章', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: '阅读' }).click()

    await expect(page.getByRole('heading', { name: '短文库' })).toBeVisible()
    await expect(page.getByText(/内置 21 篇分级短文/)).toBeVisible({ timeout: 15_000 })

    const readInCard = page.locator('main article').first().getByRole('button', { name: '阅读' })
    await expect(readInCard).toBeVisible()
    await readInCard.click()

    await expect(page.getByRole('button', { name: '返回文库' })).toBeVisible({ timeout: 15_000 })
  })
})
