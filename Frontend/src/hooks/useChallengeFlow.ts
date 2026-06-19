import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ChallengePack } from '../types/assessment'

export function useChallengeFlow() {
  const [pack, setPack] = useState<ChallengePack | null>(null)
  const [result, setResult] = useState<{ passed: boolean; totalScore: number } | null>(null)
  const [loading, setLoading] = useState(false)

  const start = useCallback(async (confirmation = false) => {
    setLoading(true)
    try {
      const response = await api.post<ChallengePack>(endpoints.challengeStart, { confirmationChallenge: confirmation })
      setPack(response.data)
      setResult(null)
    } finally {
      setLoading(false)
    }
  }, [])

  async function submit(vocabScore: number, sentenceScore: number, readingScore: number, confirmation = false) {
    const response = await api.post<{ passed: boolean; totalScore: number }>(endpoints.challengeSubmit, {
      challengeType: confirmation ? 'LevelConfirmation' : 'Daily',
      vocabularyScore: vocabScore,
      sentenceScore,
      readingScore,
      confirmationChallenge: confirmation,
    })
    setResult(response.data)
  }

  return { pack, result, loading, start, submit }
}
