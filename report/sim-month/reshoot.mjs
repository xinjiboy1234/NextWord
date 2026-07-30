// follow-up shots: dashboard plan card (long wait), profile mid section, word lookup result
import { createRequire } from 'node:module'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const require = createRequire('D:/files/projects/netcore/AI/NextWord/Frontend/package.json')
const { chromium } = require('playwright')
const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SHOTS = path.join(__dirname, 'screenshots')

const BASE = 'http://localhost:5173'
const API = 'http://localhost:5108'

const browser = await chromium.launch()
const context = await browser.newContext({ viewport: { width: 1280, height: 800 } })
await context.route('http://localhost:5173/api/**', async (route) => {
  const url = route.request().url().replace(BASE, API)
  try {
    await route.fulfill({ response: await route.fetch({ url }) })
  } catch {
    await route.continue()
  }
})
const page = await context.newPage()

// api login
const res = await page.request.post(`${API}/api/auth/login`, {
  data: { email: 'xiaocai.sim@example.com', password: 'Xiaocai@2026' },
})
const payload = await res.json()
await page.goto(BASE, { timeout: 15000 })
await page.evaluate(({ token, user }) => {
  localStorage.setItem('nextword.auth.token', token)
  localStorage.setItem('nextword.auth.user', JSON.stringify(user))
}, payload)

// 1. dashboard with long wait for plan/insight cards
await page.goto(`${BASE}/dashboard`, { timeout: 15000 })
try {
  await page.getByText('今日学习计划').waitFor({ timeout: 25_000 })
  console.log('plan card appeared')
} catch { console.log('plan card NOT shown after 25s') }
await page.waitForTimeout(1500)
await page.screenshot({ path: path.join(SHOTS, '01b-dashboard-plan.png') })
console.log('shot: 01b-dashboard-plan.png')
console.log('dashboard text:', (await page.locator('main, #root, body').first().innerText()).slice(0, 1500))

// 2. profile mid section (three-dim scores, report entry)
await page.goto(`${BASE}/profile`, { timeout: 15000 })
await page.waitForTimeout(3000)
await page.evaluate(() => window.scrollTo(0, 500))
await page.waitForTimeout(800)
await page.screenshot({ path: path.join(SHOTS, '06c-profile-mid.png') })
console.log('shot: 06c-profile-mid.png')
await page.evaluate(() => window.scrollTo(0, 1000))
await page.waitForTimeout(800)
await page.screenshot({ path: path.join(SHOTS, '06d-profile-mid2.png') })
console.log('shot: 06d-profile-mid2.png')

// 3. word lookup with long wait
await page.goto(`${BASE}/reading`, { timeout: 15000 })
await page.waitForTimeout(2500)
await page.locator('button:has-text("阅读")').first().click({ timeout: 8000 })
await page.waitForTimeout(2500)
const words = page.locator('button.word-clickable')
await words.nth(30).click({ timeout: 5000 })
try {
  await page.locator('.reader-sidebar').getByText(/[一-龥]/).first().waitFor({ timeout: 20_000 })
  console.log('lookup definition appeared')
} catch { console.log('lookup definition NOT shown after 20s') }
await page.waitForTimeout(1000)
await page.screenshot({ path: path.join(SHOTS, '05c-word-lookup-result.png') })
console.log('shot: 05c-word-lookup-result.png')

await browser.close()
