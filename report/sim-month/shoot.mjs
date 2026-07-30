// UI walkthrough for "one-month rookie" user (xiaocai.sim@example.com)
// Saves screenshots to report/sim-month/screenshots/ and raw observations to report/sim-month/data/walkthrough-raw.json
import { createRequire } from 'node:module'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const require = createRequire('D:/files/projects/netcore/AI/NextWord/Frontend/package.json')
const { chromium } = require('playwright')

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SHOTS = path.join(__dirname, 'screenshots')
const DATA = path.join(__dirname, 'data')
fs.mkdirSync(SHOTS, { recursive: true })
fs.mkdirSync(DATA, { recursive: true })

const BASE = 'http://localhost:5173'
const API = 'http://localhost:5108'
const EMAIL = 'xiaocai.sim@example.com'
const PASSWORD = 'Xiaocai@2026'
const PAGE_TIMEOUT = 15_000

const observations = []
const consoleErrors = []
const pageTexts = {}

function obs(page, type, detail) {
  observations.push({ page, type, detail })
  console.log(`[${type}] ${page}: ${detail}`)
}

async function shot(page, name) {
  const file = `${name}.png`
  await page.screenshot({ path: path.join(SHOTS, file) })
  console.log(`shot: ${file}`)
  return file
}

async function captureText(page, name) {
  try {
    pageTexts[name] = (await page.locator('main, #root, body').first().innerText()).slice(0, 3000)
  } catch { /* ignore */ }
}

async function main() {
  const browser = await chromium.launch()
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } })
  // vite dev server proxy is misconfigured (targets dead :8080); forward /api to the real backend
  await context.route('http://localhost:5173/api/**', async (route) => {
    const url = route.request().url().replace('http://localhost:5173', API)
    try {
      const response = await route.fetch({ url })
      await route.fulfill({ response })
    } catch {
      await route.continue()
    }
  })
  const page = await context.newPage()

  page.on('pageerror', (err) => {
    consoleErrors.push({ url: page.url(), kind: 'pageerror', text: String(err).slice(0, 500) })
  })
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      consoleErrors.push({ url: page.url(), kind: 'console.error', text: msg.text().slice(0, 500) })
    }
  })

  async function gotoPage(route, name, { waitMs = 1500 } = {}) {
    const t0 = Date.now()
    try {
      await page.goto(`${BASE}${route}`, { timeout: PAGE_TIMEOUT, waitUntil: 'domcontentloaded' })
      await page.waitForTimeout(waitMs)
      const ms = Date.now() - t0
      if (ms > 5000) obs(name, 'slow-load', `${ms}ms`)
      await captureText(page, name)
      return await shot(page, name)
    } catch (e) {
      obs(name, 'load-fail', String(e).slice(0, 300))
      try { return await shot(page, `${name}-FAIL`) } catch { return null }
    }
  }

  // ---------- login (UI) ----------
  await page.goto(BASE, { timeout: PAGE_TIMEOUT })
  await page.waitForTimeout(1000)
  await shot(page, '00-login')
  try {
    await page.locator('input[type="email"], input[name="email"]').first().fill(EMAIL, { timeout: 5000 })
    await page.locator('input[type="password"]').first().fill(PASSWORD)
    await shot(page, '00-login-filled')
    await page.locator('button[type="submit"]').first().click()
    await page.waitForTimeout(4000)
    const token = await page.evaluate(() => localStorage.getItem('nextword.auth.token'))
    obs('login', token ? 'ok' : 'fail', `ui login token=${token ? 'set' : 'missing'} url=${page.url()}`)
    if (!token) throw new Error('ui login did not set token')
  } catch (e) {
    obs('login', 'fallback-api', String(e).slice(0, 200))
    const res = await page.request.post(`${API}/api/auth/login`, {
      data: { email: EMAIL, password: PASSWORD },
    })
    const payload = await res.json()
    await page.evaluate(({ token, user }) => {
      localStorage.setItem('nextword.auth.token', token)
      localStorage.setItem('nextword.auth.user', JSON.stringify(user))
    }, payload)
    await page.goto(BASE, { timeout: PAGE_TIMEOUT })
    await page.waitForTimeout(1500)
  }

  // dismiss onboarding if present
  try {
    await page.getByRole('button', { name: '关闭引导' }).click({ timeout: 3000 })
    obs('global', 'onboarding', 'onboarding dialog shown and dismissed')
  } catch { /* no onboarding */ }

  // ---------- 1. dashboard ----------
  await gotoPage('/dashboard', '01-dashboard', { waitMs: 3000 })

  // ---------- 2. learn ----------
  await gotoPage('/learn', '02-learn', { waitMs: 3000 })
  try {
    const input = page.locator('input.input').first()
    await input.fill('工作', { timeout: 8000 })
    // submit button is the first button inside AnswerInput
    await page.locator('button[type="submit"], form button, button:has-text("确定"), button:has-text("提交")').first().click({ timeout: 5000 })
    await page.waitForTimeout(2500)
    await captureText(page, '02b-learn-answered')
    await shot(page, '02b-learn-answered')
    obs('learn', 'interaction', 'answered one word card via text input')
  } catch (e) {
    obs('learn', 'interaction-fail', String(e).slice(0, 250))
    await shot(page, '02b-learn-FAIL')
  }

  // ---------- 3. sentence ----------
  await gotoPage('/sentence', '03-sentence', { waitMs: 3000 })
  try {
    const tabs = page.getByRole('tab')
    const tabCount = await tabs.count()
    obs('sentence', 'info', `tabs=${tabCount}`)
    if (tabCount > 1) {
      await tabs.nth(1).click({ timeout: 5000 })
      await page.waitForTimeout(2000)
      await captureText(page, '03b-sentence-tab2')
      await shot(page, '03b-sentence-tab2')
      await tabs.nth(0).click({ timeout: 5000 })
      await page.waitForTimeout(1500)
    }
  } catch (e) {
    obs('sentence', 'tab-fail', String(e).slice(0, 200))
  }
  // submit a sentence on targeted tab
  try {
    const textarea = page.locator('textarea.textarea').first()
    await textarea.fill('I go to work by bus every day.', { timeout: 8000 })
    await page.locator('button:has-text("提交评分")').first().click({ timeout: 5000 })
    await page.waitForTimeout(2000)
    await shot(page, '03c-sentence-scoring')
    // LLM scoring can take 5-15s; poll for feedback up to 30s
    const t0 = Date.now()
    await page.waitForTimeout(16_000)
    await captureText(page, '03d-sentence-feedback')
    await shot(page, '03d-sentence-feedback')
    obs('sentence', 'interaction', `submitted sentence; waited ${Math.round((Date.now() - t0) / 1000)}s for feedback`)
  } catch (e) {
    obs('sentence', 'submit-fail', String(e).slice(0, 250))
    await shot(page, '03d-sentence-FAIL')
  }

  // ---------- 4. reading ----------
  await gotoPage('/reading', '04-reading', { waitMs: 3000 })

  // ---------- 5. reading detail + word lookup ----------
  try {
    await page.locator('section, div').filter({ hasText: '今日推荐' }).first()
    const openBtn = page.locator('button:has-text("开始阅读"), button.btn-primary').first()
    await openBtn.click({ timeout: 8000 })
    await page.waitForTimeout(3000)
    obs('reading-detail', 'info', `url=${page.url()}`)
    await captureText(page, '05-reading-detail')
    await shot(page, '05-reading-detail')
    const words = page.locator('button.word-clickable')
    const n = await words.count()
    if (n > 3) {
      await words.nth(Math.min(12, n - 1)).click({ timeout: 5000 })
      await page.waitForTimeout(2500)
      await captureText(page, '05b-word-lookup')
      await shot(page, '05b-word-lookup')
      obs('reading-detail', 'interaction', `clicked word #${Math.min(12, n - 1)} of ${n}`)
    } else {
      obs('reading-detail', 'lookup-skip', `only ${n} clickable words`)
      await shot(page, '05b-word-lookup-skip')
    }
  } catch (e) {
    obs('reading-detail', 'fail', String(e).slice(0, 250))
    await shot(page, '05-reading-detail-FAIL')
  }

  // ---------- 6. profile ----------
  await gotoPage('/profile', '06-profile', { waitMs: 3000 })
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await page.waitForTimeout(800)
  await shot(page, '06b-profile-bottom')
  await page.evaluate(() => window.scrollTo(0, 0))

  // ---------- 7. review ----------
  await gotoPage('/review', '07-review', { waitMs: 3000 })

  // ---------- 8. challenge ----------
  await gotoPage('/challenge', '08-challenge', { waitMs: 3000 })
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await page.waitForTimeout(800)
  await shot(page, '08b-challenge-bottom')

  // ---------- 9. assessment ----------
  await gotoPage('/assessment', '09-assessment', { waitMs: 3000 })

  fs.writeFileSync(
    path.join(DATA, 'walkthrough-raw.json'),
    JSON.stringify({ observations, consoleErrors, pageTexts }, null, 2),
  )
  console.log(`\n=== ${consoleErrors.length} console/page errors ===`)
  for (const e of consoleErrors.slice(0, 30)) console.log(`- [${e.kind}] ${e.url} :: ${e.text.slice(0, 160)}`)

  await browser.close()
}

main().catch((e) => { console.error(e); process.exit(1) })
