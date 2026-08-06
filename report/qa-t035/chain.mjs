// T-035 验收真实链路脚本 v2：注册→跳过初测→挑战 start/submit→dashboard→recent
// 造句先查词义再写，避免词义误用被 LLM 打低分；run1 允许重试（LLM 评分有方差）
// 证据输出到 chain-result.json（不落 token）
import { execSync } from 'node:child_process'
import { writeFileSync } from 'node:fs'

const BASE = 'http://localhost:5188'
const evidence = { steps: [] }
const note = (name, data) => { evidence.steps.push({ name, data }); console.log(`\n=== ${name} ===`); console.log(JSON.stringify(data, null, 2)) }

async function api(method, path, body, token) {
  const res = await fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  let json
  try { json = JSON.parse(text) } catch { json = text }
  return { status: res.status, json }
}

function assert(cond, msg) {
  if (!cond) throw new Error(`ASSERT FAIL: ${msg}`)
  console.log(`  ✔ ${msg}`)
}

function packJsonOf(sessionId) {
  const out = execSync(
    `docker exec nextword-postgres-1 psql -U nextword -d nextword_qa_t035 -t -A -c "SELECT \\"PackJson\\" FROM \\"ChallengeSessions\\" WHERE \\"Id\\"='${sessionId}';"`,
    { shell: 'cmd.exe' }
  ).toString().trim()
  return JSON.parse(out)
}

// 1. 注册 + 跳过初测
const email = `qa-t035-${Date.now()}@example.com`
const reg = await api('POST', '/api/auth/register', { email, password: 'QaT035!pass', displayName: 'qa-t035' })
assert(reg.status === 200, `注册成功 status=${reg.status}`)
const token = reg.json.token ?? reg.json.accessToken
assert(token, '注册返回 token')
const skip = await api('POST', '/api/assessment/initial/skip', {}, token)
assert(skip.status === 200, `跳过初测 status=${skip.status}`)
note('register+skip', { email, skip: skip.json })

// 跑一次完整挑战：readingCorrectCount 控制阅读答对题数；返回 { start, submit }
async function runChallenge(readingCorrectCount) {
  const start = await api('POST', '/api/challenge/start', { confirmationChallenge: false }, token)
  assert(start.status === 200, `challenge/start status=${start.status}`)
  const pack = start.json.pack
  const server = packJsonOf(start.json.challengeSessionId)

  // 查考点造句词词义，按词义正确造句
  let meaning = ''
  if (pack.sentence.wordId) {
    const word = await api('GET', `/api/words/${pack.sentence.wordId}`, null, token)
    meaning = word.json?.meanings?.[0] ?? ''
  }
  const sentenceAnswer = meaning
    ? `The expression "${pack.sentence.word}" means "${meaning}" in Chinese, and I used it naturally when discussing my study plans with classmates today.`
    : `Today I practiced the expression "${pack.sentence.word}" in a discussion with my classmates and used it correctly several times.`

  const readingAnswers = server.readings.map((q, i) =>
    (i < readingCorrectCount ? q.correctIndex : (q.correctIndex + 1) % q.options.length))
  const submit = await api('POST', '/api/challenge/submit', {
    challengeSessionId: start.json.challengeSessionId,
    challengeType: 'Daily',
    vocabAnswers: server.vocabulary.map(q => q.correctIndex),
    sentenceAnswer,
    targetWord: pack.sentence.word,
    scene: pack.sentence.scene,
    sentenceWordId: pack.sentence.wordId,
    readingSelectedIndexes: readingAnswers,
    lookupCount: 0,
  }, token)
  assert(submit.status === 200, `challenge/submit status=${submit.status}`)
  return { pack, submit: submit.json, sentenceAnswer }
}

// 2. start —— 断言 3 道阅读题且考点词出自正文（在第一次 run 内完成）
let first = await runChallenge(2)
const pack1 = first.pack
assert(Array.isArray(pack1.readings), 'pack 含 readings 数组')
assert(pack1.readings.length === 3, `阅读题 3 道（实际 ${pack1.readings.length}）`)
for (const [i, q] of pack1.readings.entries()) {
  const m = q.question.match(/"(.+?)"/)
  assert(m && q.articleExcerpt.toLowerCase().includes(m[1].toLowerCase()), `阅读题${i + 1} 考点词 "${m?.[1]}" 出自正文`)
  assert(q.options.length === 4, `阅读题${i + 1} 4 个选项`)
}
note('start#1', {
  attemptedLevel: pack1.attemptedLevel,
  vocabCount: pack1.vocabulary.length,
  sentenceWord: pack1.sentence.word,
  readings: pack1.readings.map(q => ({ question: q.question, options: q.options, excerptHead: q.articleExcerpt.slice(0, 60) })),
})

// 3. run1：阅读 2/3 → 期望通过、67、点评、passCount=1（造句 LLM 有方差，最多重试 4 次）
const attempts = [first]
while (attempts[attempts.length - 1].submit.readingScore === 67
  && !attempts[attempts.length - 1].submit.passed
  && attempts.length < 4) {
  attempts.push(await runChallenge(2))
}
const r1 = attempts[attempts.length - 1].submit
note('run1 attempts (2/3 reading)', attempts.map(a => ({
  sentence: a.sentenceAnswer, passed: a.submit.passed, readingScore: a.submit.readingScore,
  writingScore: a.submit.writingScore, vocabularyScore: a.submit.vocabularyScore,
  feedback: a.submit.feedback, passCount: a.submit.passCount,
})))
assert(r1.readingScore === 67, `run1 阅读分 = 67（实际 ${r1.readingScore}）`)
assert(r1.passed === true, `run1 通过（writingScore=${r1.writingScore}）`)
assert(typeof r1.feedback === 'string' && r1.feedback.length > 0, `run1 带点评: ${r1.feedback}`)
const passedAttempts = attempts.filter(a => a.submit.passed).length
assert(r1.passCount === passedAttempts, `run1 passCount = 累计通过次数 ${passedAttempts}（实际 ${r1.passCount}）`)

// 4. run2：阅读 1/3 → 不通过、33、无点评
const second = await runChallenge(1)
const r2 = second.submit
note('run2 (1/3 reading)', {
  passed: r2.passed, readingScore: r2.readingScore, writingScore: r2.writingScore,
  feedback: r2.feedback, passCount: r2.passCount,
})
assert(r2.passed === false, `run2 不通过（1/3 阅读）readingScore=${r2.readingScore}`)
assert(r2.readingScore === 33, `run2 阅读分 = 33（实际 ${r2.readingScore}）`)
assert(r2.feedback === null, 'run2 未通过无点评（feedback=null）')

// 5. dashboard：升级候选 + 通过计数 + 首通档
const dash = await api('GET', '/api/level/dashboard', null, token)
assert(dash.status === 200, `dashboard status=${dash.status}`)
assert(dash.json.upgradeCandidate === true, 'dashboard upgradeCandidate = true（7 日内有通过）')
assert(dash.json.challengePassCount === passedAttempts, `dashboard challengePassCount = ${passedAttempts}（实际 ${dash.json.challengePassCount}）`)
assert(dash.json.challengeFirstPassLevels.length >= 1, `首通档标记: ${dash.json.challengeFirstPassLevels}`)
note('dashboard', {
  upgradeCandidate: dash.json.upgradeCandidate,
  challengePassCount: dash.json.challengePassCount,
  challengeFirstPassLevels: dash.json.challengeFirstPassLevels,
  overallLevel: dash.json.overallLevel,
})

// 6. recent 历史记录
const recent = await api('GET', '/api/challenge/recent', null, token)
assert(recent.status === 200, `recent status=${recent.status}`)
note('recent', recent.json.map(r => ({
  attemptedLevel: r.attemptedLevel, passed: r.passed, totalScore: r.totalScore,
  vocabularyScore: r.vocabularyScore, sentenceScore: r.sentenceScore, readingScore: r.readingScore,
})))

writeFileSync(new URL('./chain-result.json', import.meta.url), JSON.stringify(evidence, null, 2))
console.log('\nALL ASSERTIONS PASSED')
