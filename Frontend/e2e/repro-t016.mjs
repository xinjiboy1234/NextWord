// T-016 复现验证脚本：挑战页词汇题跨题点「同一下标」选项，验证 onValueChange 正常触发、「下一个」可用
// 修复前：第 2 题点同一下标选项后「下一个」永远禁用；修复后：应正常解禁
// 前置：API 跑在 :8080（对齐 .env 的 VITE_API_PROXY_TARGET），前端 :5173
import { chromium } from 'playwright'

const API = 'http://localhost:8080'
const WEB = 'http://localhost:5173'

async function main() {
  // 注册 + 跳过测评
  const email = `t016-${Date.now()}@example.com`
  const reg = await fetch(`${API}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'password123', displayName: 'T016 验证' }),
  })
  if (!reg.ok) throw new Error(`register failed: ${reg.status}`)
  const auth = await reg.json()
  const skip = await fetch(`${API}/api/assessment/initial/skip`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${auth.token}`, 'Content-Type': 'application/json' },
    body: '{}',
  })
  if (!skip.ok) throw new Error(`skip failed: ${skip.status}`)

  const browser = await chromium.launch()
  const context = await browser.newContext()
  const page = await context.newPage()
  await page.goto(WEB)
  await page.evaluate(({ token, user }) => {
    localStorage.setItem('nextword.auth.token', token)
    localStorage.setItem('nextword.auth.user', JSON.stringify(user))
  }, auth)
  await page.goto(`${WEB}/challenge`)

  // 开始挑战
  await page.getByRole('button', { name: '开始挑战' }).click()
  await page.getByText('词汇挑战').waitFor({ timeout: 15000 })

  const nextBtn = page.getByRole('button', { name: /下一个|下一步/ })
  const results = []

  // 连续两题都点「下标 0」的选项——修复前第 2 题不会触发 onValueChange
  for (let q = 0; q < 2; q++) {
    const firstOption = page.locator('[role="radio"]').first()
    await firstOption.dispatchEvent('click')
    await page.waitForTimeout(500)
    const enabled = await nextBtn.isEnabled()
    const word = await page.locator('p').filter({ hasText: /^[a-z]/ }).first().textContent().catch(() => '?')
    results.push(`第 ${q + 1} 题（${word?.trim()}）点下标 0 后「下一个」可用 = ${enabled}`)
    if (!enabled) break
    await nextBtn.click()
    await page.waitForTimeout(800)
  }

  console.log(results.join('\n'))
  const pass = results.length === 2 && results.every((r) => r.endsWith('true'))
  console.log(pass ? 'T-016 修复验证：通过' : 'T-016 修复验证：失败（bug 仍在）')

  await browser.close()
  process.exit(pass ? 0 : 1)
}

main().catch((err) => {
  console.error('验证脚本异常：', err.message)
  process.exit(2)
})
