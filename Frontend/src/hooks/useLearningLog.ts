import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { AssessmentResult, LearningResult } from '../types/models'

export function useLearningLog() {
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<LearningResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const submit = useCallback(async (wordId: string, answer: string, rating: AssessmentResult, responseTimeMs: number) => {
    setSubmitting(true)
    setError(null)
    try {
      const response = await api.post<LearningResult>(endpoints.learningSubmit, {
        wordId,
        answer,
        rating,
        responseTimeMs,
      })
      setResult(response.data)
      return response.data
    } catch {
      setError('提交学习记录失败，请稍后再试。')
      return null
    } finally {
      setSubmitting(false)
    }
  }, [])

  const reset = useCallback(() => {
    setResult(null)
    setError(null)
  }, [])

  return { submit, submitting, result, error, reset }
}
