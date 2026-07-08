import type { APIRequestContext, Page } from '@playwright/test'

const API_BASE = 'http://localhost:5108'

export interface TestAuth {
  token: string
  user: { id: string; email: string; displayName: string }
}

export async function registerTestUser(request: APIRequestContext): Promise<TestAuth> {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`
  const response = await request.post(`${API_BASE}/api/auth/register`, {
    data: {
      email,
      password: 'password123',
      displayName: 'E2E 用户',
    },
  })
  if (!response.ok()) {
    throw new Error(`Register failed: ${response.status()} ${await response.text()}`)
  }
  const payload = await response.json() as { token: string; user: TestAuth['user'] }
  return { token: payload.token, user: payload.user }
}

export async function applyAuthToPage(page: Page, auth: TestAuth) {
  await page.goto('/')
  await page.evaluate(({ token, user }) => {
    localStorage.setItem('nextword.auth.token', token)
    localStorage.setItem('nextword.auth.user', JSON.stringify(user))
  }, auth)
  await page.reload()
}

export async function loginAsTestUser(page: Page, request: APIRequestContext) {
  const auth = await registerTestUser(request)
  await applyAuthToPage(page, auth)
  return auth
}

export async function skipInitialAssessment(request: APIRequestContext, token: string) {
  const headers = { Authorization: `Bearer ${token}` }
  const response = await request.post(`${API_BASE}/api/assessment/initial/skip`, {
    headers,
    data: {},
  })
  if (!response.ok()) {
    throw new Error(`Skip assessment failed: ${response.status()} ${await response.text()}`)
  }
}

export async function dismissOnboarding(page: Page) {
  const dismiss = page.getByRole('button', { name: '关闭引导' })
  await dismiss.click({ timeout: 5_000 }).catch(() => {})
}
