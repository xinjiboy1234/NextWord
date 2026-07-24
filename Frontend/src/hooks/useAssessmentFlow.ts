import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type {
  AssessmentAnswerItem,
  AssessmentBlock,
  AssessmentBlockResponse,
  AssessmentBlockResult,
  AssessmentFinalResult,
} from '../types/assessment'

/**
 * T-004 自适应分块测评：开始 → 取下一块 → 提交 → 收敛或继续，2–3 块出定级。
 */
export function useAssessmentFlow() {
  const [assessmentId, setAssessmentId] = useState<string | null>(null)
  const [block, setBlock] = useState<AssessmentBlock | null>(null)
  const [finalResult, setFinalResult] = useState<AssessmentFinalResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadNextBlock = useCallback(async (id: string) => {
    const response = await api.get<AssessmentBlockResponse>(endpoints.assessmentNextBlock(id))
    if (response.data.converged) {
      setBlock(null)
      setFinalResult(response.data.final ?? null)
    } else {
      setBlock(response.data.block ?? null)
    }
  }, [])

  const start = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await api.post<{ assessmentId: string }>(endpoints.assessmentStart, {})
      setAssessmentId(response.data.assessmentId)
      setFinalResult(null)
      await loadNextBlock(response.data.assessmentId)
    } catch {
      setError('无法开始测评。')
    } finally {
      setLoading(false)
    }
  }, [loadNextBlock])

  async function submitBlock(answers: AssessmentAnswerItem[]): Promise<boolean> {
    if (!assessmentId || !block) return false
    setSubmitting(true)
    setError(null)
    try {
      const result = await api.post<AssessmentBlockResult>(
        endpoints.assessmentSubmitBlock(assessmentId, block.blockIndex),
        { answers },
      )
      if (result.data.converged) {
        setBlock(null)
        setFinalResult(result.data.final ?? null)
      } else {
        await loadNextBlock(assessmentId)
      }
      return true
    } catch {
      setError('提交失败，请重试。')
      return false
    } finally {
      setSubmitting(false)
    }
  }

  return {
    assessmentId,
    block,
    finalResult,
    loading,
    submitting,
    error,
    start,
    submitBlock,
  }
}
