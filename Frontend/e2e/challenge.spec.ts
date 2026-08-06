import { expect, test } from '@playwright/test'
import { applyAuthToPage, registerTestUser, skipInitialAssessment } from './helpers'

const API_BASE = 'http://localhost:5108'

test.describe('挑战测评', () => {
  test('服务端计分 submit API 接受原始答案', async ({ request }) => {
    const auth = await registerTestUser(request)
    await skipInitialAssessment(request, auth.token)
    const headers = { Authorization: `Bearer ${auth.token}` }

    const start = await request.post(`${API_BASE}/api/challenge/start`, {
      headers,
      data: { confirmationChallenge: false },
    })
    expect(start.ok()).toBeTruthy()
    const started = await start.json() as {
      challengeSessionId: string
      pack: {
        vocabulary: unknown[]
        sentence: { word: string; scene: string; wordId: string | null }
        readings: { options: string[] }[]
      }
    }

    const submit = await request.post(`${API_BASE}/api/challenge/submit`, {
      headers,
      data: {
        challengeSessionId: started.challengeSessionId,
        challengeType: 'Daily',
        vocabAnswers: started.pack.vocabulary.map(() => 0),
        sentenceAnswer: `This is a practice sentence using ${started.pack.sentence.word}.`,
        targetWord: started.pack.sentence.word,
        scene: started.pack.sentence.scene,
        sentenceWordId: started.pack.sentence.wordId,
        readingSelectedIndexes: started.pack.readings.map(() => 0),
        lookupCount: 0,
      },
    })

    expect(submit.ok()).toBeTruthy()
    const result = await submit.json() as { totalScore: number; passed: boolean }
    expect(typeof result.totalScore).toBe('number')
    expect(typeof result.passed).toBe('boolean')
  })

  test('已完成测评用户可打开挑战页', async ({ page, request }) => {
    const auth = await registerTestUser(request)
    await skipInitialAssessment(request, auth.token)
    await applyAuthToPage(page, auth)

    await page.goto('/challenge')
    await expect(page.getByRole('button', { name: '开始挑战' })).toBeVisible({ timeout: 15_000 })
  })
})
