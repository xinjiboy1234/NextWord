import { expect, test } from '@playwright/test'
import { applyAuthToPage, configureTestLlmKey, loginAsTestUser, registerTestUser, skipInitialAssessment } from './helpers'

test.describe('首次水平测评', () => {
  test('新用户自动进入测评并开始第一块（T-064 先配置 AI 服务）', async ({ page, request }) => {
    const auth = await loginAsTestUser(page, request)
    // T-064：首次测评前强制配置 API Key——测试用户配一个假 key 通过配置门（真实调用失败回退 Mock）
    await configureTestLlmKey(request, auth.token)
    await page.goto('/dashboard')

    await expect(page).toHaveURL(/\/assessment/, { timeout: 15_000 })
    await expect(page.getByRole('button', { name: '跳过本次测评' })).toBeVisible()
    await expect(page.getByRole('navigation', { name: '主导航' })).not.toBeVisible()
    await expect(page.getByText('提示造句').first()).toBeVisible({ timeout: 30_000 })
    await expect(page.getByText('情境表达').first()).toBeVisible()
  })

  test('已完成测评用户可从管理页进入（T-063 快捷入口）', async ({ page, request }) => {
    const auth = await registerTestUser(request)
    await skipInitialAssessment(request, auth.token)
    await applyAuthToPage(page, auth)

    await page.goto('/profile')
    await page.getByRole('button', { name: '系统设置' }).click()
    await page.getByRole('button', { name: '水平测评' }).click()
    await expect(page).toHaveURL(/\/assessment/)

    // T-030：已完成首次测评的用户按「重新水平测评」展示
    await expect(page.getByRole('heading', { name: '重新水平测评' })).toBeVisible()
  })
})
