import { expect, test } from '@playwright/test'
import { loginAsTestUser, registerTestUser, applyAuthToPage, skipInitialAssessment } from './helpers'

test.describe('首次水平测评', () => {
  test('新用户自动进入测评并开始第一块', async ({ page, request }) => {
    await loginAsTestUser(page, request)
    await page.goto('/dashboard')

    await expect(page).toHaveURL(/\/assessment/, { timeout: 15_000 })
    await expect(page.getByRole('button', { name: '跳过本次测评' })).toBeVisible()
    await expect(page.getByRole('navigation', { name: '主导航' })).not.toBeVisible()
    await expect(page.getByText('提示造句').first()).toBeVisible({ timeout: 30_000 })
    await expect(page.getByText('情境表达').first()).toBeVisible()
  })

  test('已完成测评用户可从管理页进入', async ({ page, request }) => {
    const auth = await registerTestUser(request)
    await skipInitialAssessment(request, auth.token)
    await applyAuthToPage(page, auth)

    await page.goto('/profile')
    await page.getByRole('link', { name: /管理后台/ }).click()
    await page.getByRole('button', { name: '水平测评' }).click()
    await expect(page).toHaveURL(/\/assessment/)

    await expect(page.getByRole('heading', { name: '首次水平测评' })).toBeVisible()
  })
})
