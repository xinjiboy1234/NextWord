/**
 * NextWord 产品演示录屏脚本（非测试，用 Playwright 库 API 逐场景录制 webm）。
 *
 * 前置条件：
 *   - 后端已运行在 http://localhost:5108（Development 自动迁移+种子，LLM Mock）
 *   - 前端已运行在 http://localhost:5173
 *
 * 运行：cd Frontend && node e2e/record-demo.mjs
 * 输出：report/videos/00-dashboard-agent.webm, 01-login.webm ... 09-profile.webm
 * 截图：report/screenshots/dashboard-plan-card.png, dashboard-insight-card.png
 */
import { chromium } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const FRONTEND_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const OUT_DIR = path.resolve(FRONTEND_DIR, '..', 'report', 'videos')
const RAW_DIR = path.join(OUT_DIR, 'raw')
const SHOT_DIR = path.resolve(FRONTEND_DIR, '..', 'report', 'screenshots')
const BASE = 'http://localhost:5173'
const API = 'http://localhost:5108'
const VIEW = { width: 1280, height: 720 }
const PASSWORD = 'demo123456'

fs.mkdirSync(RAW_DIR, { recursive: true })
fs.mkdirSync(SHOT_DIR, { recursive: true })

const results = []
let browser

// ---------- 通用辅助 ----------

function pause(page, ms) {
  return page.waitForTimeout(ms)
}

/** 容错步骤：元素找不到/超时只记日志，不中断录制 */
async function tryStep(label, fn) {
  try {
    await fn()
  } catch (error) {
    console.log(`  [skip] ${label}: ${String(error?.message ?? error).split('\n')[0]}`)
  }
}

async function humanType(locator, text, delay = 80) {
  await locator.click({ timeout: 8000 })
  await locator.pressSequentially(text, { delay })
}

async function smoothScroll(page, delta) {
  const steps = 14
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, delta / steps)
    await page.waitForTimeout(60)
  }
}

// ---------- API 辅助 ----------

async function apiRegister(displayName) {
  const email = `demo-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`
  const res = await fetch(`${API}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, displayName }),
  })
  if (!res.ok) throw new Error(`register failed: ${res.status} ${await res.text()}`)
  const data = await res.json()
  return { token: data.token, user: data.user, email }
}

async function apiLogin(email, password) {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error(`login failed: ${res.status} ${await res.text()}`)
  const data = await res.json()
  return { token: data.token, user: data.user, email }
}

async function apiSkipAssessment(token) {
  const res = await fetch(`${API}/api/assessment/initial/skip`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: '{}',
  })
  if (!res.ok) throw new Error(`skip assessment failed: ${res.status}`)
}

async function apiGet(token, url) {
  const res = await fetch(`${API}${url}`, { headers: { Authorization: `Bearer ${token}` } })
  if (!res.ok) throw new Error(`GET ${url} failed: ${res.status}`)
  return res.json()
}

async function applyAuth(page, auth) {
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' })
  // Vite 冷启动可能触发整页 reload，evaluate 需要重试
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      await page.evaluate(
        ([token, user]) => {
          localStorage.setItem('nextword.auth.token', token)
          localStorage.setItem('nextword.auth.user', JSON.stringify(user))
        },
        [auth.token, auth.user],
      )
      break
    } catch (e) {
      if (attempt === 4) throw e
      await page.waitForTimeout(1000)
    }
  }
  await page.reload({ waitUntil: 'domcontentloaded' })
}

// ---------- 场景框架 ----------

async function recordScene(name, fn) {
  console.log(`\n=== 录制 ${name} ===`)
  const dir = path.join(RAW_DIR, name)
  fs.mkdirSync(dir, { recursive: true })
  const context = await browser.newContext({
    viewport: VIEW,
    recordVideo: { dir, size: VIEW },
  })
  const page = await context.newPage()
  let ok = true
  let error = null
  try {
    await fn(page)
  } catch (e) {
    ok = false
    error = String(e?.message ?? e).split('\n')[0]
    console.error(`  [fail] ${error}`)
  }
  await pause(page, 800).catch(() => {})
  await context.close()
  // context.close() 后视频落盘为随机名 webm，按场景改名
  let file = null
  const webms = fs.readdirSync(dir).filter((f) => f.endsWith('.webm'))
  if (webms.length > 0) {
    file = path.join(OUT_DIR, `${name}.webm`)
    fs.renameSync(path.join(dir, webms[0]), file)
  } else {
    ok = false
    error = (error ? `${error}; ` : '') + 'no webm produced'
  }
  results.push({ name, ok, error, file })
  console.log(`  -> ${file ?? '(no video)'} ${ok ? 'OK' : 'FAIL'}`)
}

// ---------- 场景定义 ----------

// 场景 00：I6 新功能——林晓（演示数据集）登录后的首页
// 展示「今日学习计划」卡（个性化徽章）与「学习洞察」卡（已调整计划徽章），并截 PNG 供 PPT 使用
async function scene00DashboardAgent(page) {
  const linxiao = await apiLogin('linxiao@demo.nextword.local', 'Linxiao#2026')
  await applyAuth(page, linxiao)
  await page.goto(`${BASE}/dashboard`)
  await page.waitForSelector('text=今日学习计划', { timeout: 20000 })
  await page.waitForSelector('text=学习洞察', { timeout: 20000 })
  await pause(page, 3000) // 让观众看清两张卡片整体

  const cards = page.locator('.dashboard-info-card')
  const planCard = cards.nth(0)
  const insightCard = cards.nth(1)

  // 悬停「今日学习计划」卡并截图
  await tryStep('hover plan card', async () => {
    await planCard.hover({ timeout: 5000 })
  })
  await pause(page, 2500)
  await tryStep('screenshot plan card', async () => {
    await planCard.screenshot({ path: path.join(SHOT_DIR, 'dashboard-plan-card.png'), timeout: 8000 })
  })

  // 悬停「学习洞察」卡并截图
  await tryStep('hover insight card', async () => {
    await insightCard.hover({ timeout: 5000 })
  })
  await pause(page, 2500)
  await tryStep('screenshot insight card', async () => {
    await insightCard.screenshot({ path: path.join(SHOT_DIR, 'dashboard-insight-card.png'), timeout: 8000 })
  })

  // 首屏整图（含两张卡片）
  await tryStep('screenshot dashboard full', async () => {
    await page.screenshot({ path: path.join(SHOT_DIR, 'dashboard-home.png'), timeout: 8000 })
  })

  // 缓慢滚动到模块区再回顶部，展示首页全貌
  await smoothScroll(page, 350)
  await pause(page, 1500)
  await smoothScroll(page, -350)
  await pause(page, 2000)
}

let uiUser = null // 场景 01 通过 UI 注册的用户，场景 02 复用

async function scene01Login(page) {
  await page.goto(`${BASE}/`)
  await page.getByRole('tab', { name: '登录' }).waitFor({ timeout: 15000 })
  await pause(page, 2500)

  await page.getByRole('tab', { name: '注册' }).click()
  await pause(page, 1500)

  const email = `demo-${Date.now()}@example.com`
  await humanType(page.locator('#displayName'), '演示用户')
  await pause(page, 800)
  await humanType(page.locator('#email-register'), email)
  await pause(page, 800)
  await humanType(page.locator('#password-register'), PASSWORD)
  await pause(page, 1500)

  await page.getByRole('button', { name: '注册' }).click()
  // 新用户自动进入首次测评
  await page
    .waitForSelector('text=/首次水平测评|正在准备测评|即将开始|第 1 块/', { timeout: 20000 })
  await pause(page, 3000)

  const token = await page.evaluate(() => localStorage.getItem('nextword.auth.token'))
  const user = JSON.parse(await page.evaluate(() => localStorage.getItem('nextword.auth.user')))
  uiUser = { token, user, email }
}

async function scene02Assessment(page) {
  await applyAuth(page, uiUser)
  await page.goto(`${BASE}/assessment`)
  // 等待第一块题目加载（产出题 textarea 出现）
  await page.waitForSelector('textarea', { timeout: 30000 })
  await pause(page, 2500)

  // 逐区作答：有 textarea 的写句子，有选项的点第一个选项
  const sentences = [
    'I always try my best to learn something new every day.',
    'She gave me some very useful advice about my study plan.',
    'We decided to take a short break and then keep working.',
  ]
  let sentenceIndex = 0
  const sections = page.locator('div.mt-5.space-y-4')
  const count = Math.min(await sections.count(), 4)
  for (let i = 0; i < count; i++) {
    const section = sections.nth(i)
    await section.scrollIntoViewIfNeeded().catch(() => {})
    await pause(page, 1200)
    const textarea = section.locator('textarea')
    if ((await textarea.count()) > 0) {
      await tryStep(`block textarea ${i}`, async () => {
        await humanType(textarea.first(), sentences[sentenceIndex % sentences.length], 50)
        sentenceIndex++
      })
    } else {
      await tryStep(`block option ${i}`, async () => {
        await section.locator('span[role="radio"]').first().dispatchEvent('click')
      })
    }
    await pause(page, 1500)
  }

  // 不逐块提交，展示 2-3 题后通过 API skip 完成测评进入主流程
  await pause(page, 1500)
  await apiSkipAssessment(uiUser.token)
  await page.goto(`${BASE}/dashboard`)
  await page.waitForSelector('text=选择模块开始今日练习', { timeout: 20000 })
  await pause(page, 2500)
}

async function scene03Dashboard(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/dashboard`)
  await page.waitForSelector('text=选择模块开始今日练习', { timeout: 20000 })
  await pause(page, 2500)

  // 依次悬停 5 个模块卡片
  const cards = page.locator('.module-card')
  const cardCount = await cards.count()
  for (let i = 0; i < cardCount; i++) {
    await tryStep(`hover card ${i}`, async () => {
      await cards.nth(i).hover({ timeout: 5000 })
    })
    await pause(page, 1200)
  }
  await smoothScroll(page, 300)
  await pause(page, 1200)
  await smoothScroll(page, -300)
  await pause(page, 1500)
}

async function scene04Learn(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/learn`)
  await page.waitForSelector('.word-card, .celebration-card, form.card', { timeout: 25000 })
  await pause(page, 2500) // 展示单词、音标、阶段徽标

  for (let round = 0; round < 3; round++) {
    const done = await page.locator('.celebration-card').isVisible().catch(() => false)
    if (done) break
    if (round % 2 === 0) {
      // 点「不会」展示自评流程
      await tryStep('mark forgot', async () => {
        await page.getByRole('button', { name: '不会' }).click({ timeout: 6000 })
      })
    } else {
      // 输入一个释义再提交
      await tryStep('type meaning', async () => {
        await humanType(page.locator('#answer'), '测试', 120)
        await pause(page, 800)
        await page.getByRole('button', { name: '提交' }).click({ timeout: 6000 })
      })
    }
    await pause(page, 2500) // 展示反馈区
    await tryStep('next word', async () => {
      await page.getByRole('button', { name: '下一个' }).click({ timeout: 6000 })
    })
    await pause(page, 2000)
  }
}

async function scene05Spelling(page, auth) {
  // 先通过 API 拿到拼写队列的词，用于演示「故意写错再改对」
  let lemmas = []
  try {
    const queue = await apiGet(auth.token, '/api/spelling/queue?count=8')
    lemmas = queue.map((w) => w.lemma).filter(Boolean)
  } catch (e) {
    console.log(`  [skip] fetch spelling queue: ${e.message}`)
  }

  await applyAuth(page, auth)
  await page.goto(`${BASE}/spelling`)
  await page.waitForSelector('#spelling-input', { timeout: 25000 }).catch(() => {})
  await pause(page, 2000)

  const empty = await page.locator('text=暂无拼写任务').isVisible().catch(() => false)
  if (empty) {
    console.log('  [skip] 拼写队列为空')
    await pause(page, 2000)
    return
  }

  // 播放发音
  await tryStep('play audio', async () => {
    await page.getByRole('button', { name: '播放发音' }).click({ timeout: 6000 })
  })
  await pause(page, 2000)

  // 第一词：故意写错一个字母，展示错误高亮
  const first = lemmas[0] ?? 'apple'
  const wrong = first.length > 2 ? `${first.slice(0, -1)}x` : `${first}x`
  await humanType(page.locator('#spelling-input'), wrong, 110)
  await pause(page, 1000)
  await page.getByRole('button', { name: '提交' }).click()
  await pause(page, 3000) // 错误位置高亮

  // 第二词：写对
  await tryStep('next spelling word', async () => {
    await page.getByRole('button', { name: '下一个' }).click({ timeout: 6000 })
  })
  await pause(page, 1500)
  const second = lemmas[1] ?? first
  await tryStep('type correct spelling', async () => {
    await humanType(page.locator('#spelling-input'), second, 110)
    await pause(page, 800)
    await page.getByRole('button', { name: '提交' }).click({ timeout: 6000 })
  })
  await pause(page, 3000) // 成功提示
}

async function scene06Sentence(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/sentence`)
  await page.waitForSelector('textarea', { timeout: 25000 })
  await pause(page, 2500)

  // Tab 1：指定词造句
  let targetWord = 'improve'
  await tryStep('read target word', async () => {
    const text = await page.locator('.card h2').first().innerText({ timeout: 5000 })
    if (text && /^[a-zA-Z][a-zA-Z -]+$/.test(text.trim())) targetWord = text.trim()
  })
  await humanType(
    page.locator('textarea').first(),
    `I want to use the word "${targetWord}" correctly when I talk with my friends.`,
    45,
  )
  await pause(page, 1200)
  await page.getByRole('button', { name: '提交评分' }).click()
  // Mock LLM 评分，等待右侧评分面板
  await page.waitForSelector('text=表达状态', { timeout: 40000 }).catch(() => {})
  await pause(page, 3500)
  await smoothScroll(page, 250)
  await pause(page, 1500)
  await smoothScroll(page, -250)

  // Tab 2：自由表达
  await page.getByRole('tab', { name: '自由表达' }).click()
  await pause(page, 1500)
  await humanType(
    page.locator('textarea').first(),
    'Last weekend I went to the park with my family. The weather was nice and we had a picnic there. I really enjoyed the time with them.',
    35,
  )
  await pause(page, 1000)
  await page.getByRole('button', { name: '获取反馈' }).click()
  await page.waitForSelector('text=综合分', { timeout: 40000 }).catch(() => {})
  await pause(page, 3500)
}

async function scene07Reading(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/reading`)
  await page.waitForSelector('text=短文库', { timeout: 20000 })
  await pause(page, 2500)
  await smoothScroll(page, 350)
  await pause(page, 1200)
  await smoothScroll(page, -350)
  await pause(page, 1000)

  // 打开第一篇文章
  await page.getByRole('button', { name: '阅读' }).first().click()
  await page.waitForSelector('.article-body', { timeout: 20000 })
  await pause(page, 2500)

  // 点击文中单词，弹出查词弹层
  await tryStep('click word', async () => {
    await page.locator('.word-clickable').nth(6).click({ timeout: 8000 })
  })
  await page.waitForSelector('.word-popover-panel', { timeout: 15000 }).catch(() => {})
  await pause(page, 2500) // 音标/释义
  await tryStep('show examples', async () => {
    await page.getByRole('button', { name: '查看例句' }).click({ timeout: 6000 })
  })
  await pause(page, 2500)
  await tryStep('close popover', async () => {
    await page.getByRole('button', { name: '关闭', exact: true }).click({ timeout: 5000 })
  })
  await pause(page, 1000)

  // 词汇提取面板
  await tryStep('scroll vocab panel', async () => {
    await page.locator('.vocab-panel').scrollIntoViewIfNeeded({ timeout: 6000 })
  })
  await pause(page, 2000)
  await tryStep('expand vocab panel', async () => {
    const table = page.locator('.vocab-table')
    if (!(await table.isVisible().catch(() => false))) {
      await page.locator('.vocab-panel-toggle').click({ timeout: 5000 })
    }
  })
  await pause(page, 2500)

  // 评论区
  await tryStep('scroll comments', async () => {
    await page.getByText('段落评论').scrollIntoViewIfNeeded({ timeout: 6000 })
  })
  await pause(page, 2500)
}

async function scene08Challenge(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/challenge`)
  await page.waitForSelector('text=挑战测评', { timeout: 20000 })
  await pause(page, 2000)

  await page.getByRole('button', { name: '开始挑战' }).click()
  // 生成挑战包（含 LLM Mock），等待词汇阶段
  await page.waitForSelector('text=词汇挑战', { timeout: 60000 })
  await pause(page, 2000)

  // 阶段一：词汇，逐题作答
  // 注意：OptionTags 的 RadioGroup 是非受控的，同一实例跨题保留内部选中值，
  // 每题必须点与上题不同的选项，否则 base-ui 认为值未变化、不触发 onValueChange。
  let lastPicked = -1
  for (let i = 0; i < 8; i++) {
    const inSentencePhase = await page.getByText('造句挑战').isVisible().catch(() => false)
    if (inSentencePhase) break
    await tryStep(`vocab option ${i}`, async () => {
      const radios = page.locator('span[role="radio"]')
      const total = await radios.count()
      lastPicked = (lastPicked + 1) % Math.max(total, 1)
      await radios.nth(lastPicked).dispatchEvent('click')
    })
    await pause(page, 1300)
    await tryStep(`vocab next ${i}`, async () => {
      const nextBtn = page.getByRole('button', { name: /下一个|下一步/ })
      await nextBtn.waitFor({ state: 'visible', timeout: 5000 })
      await page.waitForFunction(
        (name) => [...document.querySelectorAll('button')].some((b) => name.test(b.textContent ?? '') && !b.disabled),
        /下一个|下一步/,
        { timeout: 5000 },
      )
      await nextBtn.click({ timeout: 3000 })
    })
    await pause(page, 1500)
  }

  // 阶段二：造句
  await page.waitForSelector('text=造句挑战', { timeout: 15000 })
  await pause(page, 1500)
  let challengeWord = 'improve'
  await tryStep('read challenge word', async () => {
    const text = await page.getByText('使用单词：').innerText({ timeout: 5000 })
    const w = text.split('：')[1]?.trim()
    if (w) challengeWord = w
  })
  await humanType(
    page.locator('textarea').first(),
    `Reading books every day helps me ${challengeWord} my English step by step.`,
    45,
  )
  await pause(page, 1200)
  await page.getByRole('button', { name: '下一步' }).click()
  await pause(page, 2000)

  // 阶段三：阅读
  await page.waitForSelector('text=阅读挑战', { timeout: 15000 })
  await pause(page, 2000)
  await smoothScroll(page, 250)
  await pause(page, 1200)
  await tryStep('reading option', async () => {
    await page.locator('span[role="radio"]').first().dispatchEvent('click')
  })
  await pause(page, 1500)
  await tryStep('submit challenge', async () => {
    await page.getByRole('button', { name: '提交挑战结果' }).click({ timeout: 8000 })
  })
  await page.waitForSelector('text=/挑战成功|挑战未通过/', { timeout: 40000 }).catch(() => {})
  await pause(page, 3500)
}

async function scene09Profile(page, auth) {
  await applyAuth(page, auth)
  await page.goto(`${BASE}/profile`)
  await page.waitForSelector('text=总体等级', { timeout: 20000 })
  await pause(page, 3000) // 头像、等级徽标

  await smoothScroll(page, 450) // 等级面板 / 评估报告区域
  await pause(page, 2500)
  await smoothScroll(page, 450) // 学习进度 / 弱项画像
  await pause(page, 2500)
  await smoothScroll(page, 450) // 显示设置 / 高级
  await pause(page, 2000)

  // 管理后台 → LLM 设置抽屉（BYOK 预设，不填真实 key）
  await page.getByRole('link', { name: /管理后台/ }).click()
  await page.waitForSelector('text=系统设置', { timeout: 15000 })
  await pause(page, 2000)
  await page.getByRole('button', { name: /系统设置/ }).click()
  await page.waitForSelector('#llm-preset', { timeout: 15000 })
  await pause(page, 2500)
  await tryStep('switch preset', async () => {
    await page.locator('#llm-preset').selectOption({ index: 1 }, { timeout: 5000 })
  })
  await pause(page, 2500)
  await smoothScroll(page, 300)
  await pause(page, 2000)
}

// ---------- 主流程 ----------

async function main() {
  // 可选参数：只录制指定场景，如 node e2e/record-demo.mjs 02 08
  const only = process.argv.slice(2).map((s) => s.trim()).filter(Boolean)
  const wanted = (name) => only.length === 0 || only.some((o) => name.startsWith(o))

  // 健康检查
  const health = await fetch(`${API}/api/health`).catch(() => null)
  if (!health?.ok) throw new Error('后端 /api/health 不可用，请先启动后端与前端')

  browser = await chromium.launch({ headless: true })

  // 场景 00：I6 新功能——林晓的首页（学习计划卡 + 学习洞察卡）
  if (wanted('00-dashboard-agent')) await recordScene('00-dashboard-agent', scene00DashboardAgent)

  // 场景 01：UI 注册新用户（「演示用户」），落地首次测评
  if (wanted('01-login')) await recordScene('01-login', scene01Login)
  if (!uiUser?.token) {
    console.error('场景 01 未拿到用户，场景 02 改用 API 注册用户')
    uiUser = await apiRegister('演示用户')
  }

  // 场景 02：同一用户答几道题后 API skip 完成测评
  if (wanted('02-assessment')) await recordScene('02-assessment', (page) => scene02Assessment(page))

  // 场景 03-09：已完成测评（skip → A2）的演示用户
  const demoUser = await apiRegister('演示用户')
  await apiSkipAssessment(demoUser.token)

  if (wanted('03-dashboard')) await recordScene('03-dashboard', (page) => scene03Dashboard(page, demoUser))
  if (wanted('04-learn')) await recordScene('04-learn', (page) => scene04Learn(page, demoUser))
  if (wanted('05-spelling')) await recordScene('05-spelling', (page) => scene05Spelling(page, demoUser))
  if (wanted('06-sentence')) await recordScene('06-sentence', (page) => scene06Sentence(page, demoUser))
  if (wanted('07-reading')) await recordScene('07-reading', (page) => scene07Reading(page, demoUser))
  if (wanted('08-challenge')) await recordScene('08-challenge', (page) => scene08Challenge(page, demoUser))
  if (wanted('09-profile')) await recordScene('09-profile', (page) => scene09Profile(page, demoUser))

  await browser.close()
  fs.rmSync(RAW_DIR, { recursive: true, force: true })

  // 汇总
  console.log('\n===== 录制结果 =====')
  for (const r of results) {
    const size = r.file && fs.existsSync(r.file) ? `${(fs.statSync(r.file).size / 1024 / 1024).toFixed(2)} MB` : '-'
    console.log(`${r.ok ? 'OK  ' : 'FAIL'} ${r.name}  ${size}${r.error ? `  (${r.error})` : ''}`)
  }
}

main().catch((e) => {
  console.error('录制脚本异常退出:', e)
  process.exit(1)
})
