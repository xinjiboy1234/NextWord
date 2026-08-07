// T-036 QA 截图脚本：新用户空态（1280/375）+ 小菜真实数据（1280/375），并检测 375px 横向溢出
import { chromium } from 'playwright'

const BASE = 'http://localhost:5198'
const OUT = process.cwd()

async function login(email, password) {
  const res = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error(`login failed ${res.status}`)
  return res.json() // { token, user }
}

async function shoot(browser, account, name, width, height) {
  const page = await browser.newPage({ viewport: { width, height } })
  const errors = []
  page.on('pageerror', (err) => errors.push(String(err)))
  page.on('console', (msg) => { if (msg.type() === 'error') errors.push(msg.text()) })
  await page.goto(BASE + '/login')
  await page.evaluate(({ token, user }) => {
    localStorage.setItem('nextword.auth.token', token)
    localStorage.setItem('nextword.auth.user', JSON.stringify(user))
  }, account)
  await page.goto(BASE + '/profile')
  await page.waitForSelector('text=我的这个月', { timeout: 15000 })
  // 等时间轴面板加载完成（加载文案消失）
  await page.waitForFunction(
    () => !document.body.innerText.includes('加载这个月的足迹'),
    { timeout: 15000 },
  )
  await page.waitForTimeout(800)
  const overflow = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
    overflowX: document.documentElement.scrollWidth > window.innerWidth,
  }))
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: true })
  console.log(JSON.stringify({ name, width, overflow, errors }))
  await page.close()
}

const xiaocai = await login('xiaocai.sim@example.com', 'Xiaocai@2026')
const newbie = await login('qa.t036.new@example.com', 'QaNew@2026')
const browser = await chromium.launch()
await shoot(browser, newbie, 'newuser-profile-1280', 1280, 900)
await shoot(browser, newbie, 'newuser-profile-375', 375, 800)
await shoot(browser, xiaocai, 'xiaocai-profile-1280', 1280, 900)
await shoot(browser, xiaocai, 'xiaocai-profile-375', 375, 800)
await browser.close()
