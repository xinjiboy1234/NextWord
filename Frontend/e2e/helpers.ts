import type { Page } from '@playwright/test'

export async function dismissOnboarding(page: Page) {
  const dismiss = page.getByRole('button', { name: '关闭引导' })
  await dismiss.click({ timeout: 5_000 }).catch(() => {})
}
