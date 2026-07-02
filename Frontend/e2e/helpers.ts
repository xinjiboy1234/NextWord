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

  const start = await request.post(`${API_BASE}/api/assessment/initial/start`, {
    headers,
    data: {},
  })
  if (!start.ok()) {
    throw new Error(`Start assessment failed: ${start.status()}`)
  }
  const { assessmentId } = await start.json() as { assessmentId: string }

  for (const step of [1, 2, 3, 4]) {
    const questionsRes = await request.get(`${API_BASE}/api/assessment/${assessmentId}/step/${step}`, { headers })
    if (!questionsRes.ok()) {
      throw new Error(`Get step ${step} failed: ${questionsRes.status()}`)
    }
    const questions = await questionsRes.json()
    const answersJson = buildAssessmentAnswers(step, questions)

    const submit = await request.post(`${API_BASE}/api/assessment/${assessmentId}/step/${step}`, {
      headers,
      data: { answersJson },
    })
    if (!submit.ok()) {
      throw new Error(`Submit step ${step} failed: ${submit.status()}`)
    }
  }

  const complete = await request.post(`${API_BASE}/api/assessment/${assessmentId}/complete`, { headers })
  if (!complete.ok()) {
    throw new Error(`Complete assessment failed: ${complete.status()}`)
  }
}

function buildAssessmentAnswers(step: number, questions: unknown): string {
  if (step === 1) {
    const items = questions as Array<{ correctIndex: number }>
    return JSON.stringify(items.map((item) => item.correctIndex))
  }
  if (step === 2) {
    const items = questions as Array<{ correctSpelling: string }>
    return JSON.stringify(items.map((item) => item.correctSpelling))
  }
  if (step === 3) {
    const items = questions as unknown[]
    return JSON.stringify(items.map(() => 'This is a complete practice sentence for testing.'))
  }
  if (step === 4) {
    const payload = questions as { question: { correctIndex: number } }
    return JSON.stringify({ selectedIndex: payload.question.correctIndex, lookupCount: 0 })
  }
  return '[]'
}

export async function dismissOnboarding(page: Page) {
  const dismiss = page.getByRole('button', { name: '关闭引导' })
  await dismiss.click({ timeout: 5_000 }).catch(() => {})
}
