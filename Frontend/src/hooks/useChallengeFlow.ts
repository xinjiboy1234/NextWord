import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ChallengeStartResponse, ChallengeSubmitResponse } from '../types/score'

export function useChallengeFlow() {
  const [sessionId, setSessionId] = useState<string | null>(null)
  const [pack, setPack] = useState<ChallengeStartResponse['pack'] | null>(null)
  const [result, setResult] = useState<ChallengeSubmitResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [isConfirmation, setIsConfirmation] = useState(false)

  const start = useCallback(async (confirmation = false) => {
    setLoading(true)
    try {
      const response = await api.post<ChallengeStartResponse>(endpoints.challengeStart, { confirmationChallenge: confirmation })
      setSessionId(response.data.challengeSessionId)
      setPack(response.data.pack)
      setResult(null)
      setIsConfirmation(confirmation)
    } finally {
      setLoading(false)
    }
  }, [])

  async function submit(payload: {
    vocabAnswers: number[]
    sentenceAnswer: string
    targetWord: string
    scene: string
    sentenceWordId?: string | null
    /** T-035：阅读 3 题作答 */
    readingSelectedIndexes: number[]
    lookupCount: number
    confirmation?: boolean
  }) {
    if (!sessionId) return
    const response = await api.post<ChallengeSubmitResponse>(endpoints.challengeSubmit, {
      challengeSessionId: sessionId,
      challengeType: payload.confirmation ? 'LevelConfirmation' : 'Daily',
      vocabAnswers: payload.vocabAnswers,
      sentenceAnswer: payload.sentenceAnswer,
      targetWord: payload.targetWord,
      scene: payload.scene,
      sentenceWordId: payload.sentenceWordId,
      readingSelectedIndexes: payload.readingSelectedIndexes,
      lookupCount: payload.lookupCount,
    })
    setResult(response.data)
  }

  return { sessionId, pack, result, loading, isConfirmation, start, submit }
}
