import { useCallback, useRef, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type {
  AssessmentAnswerItem,
  AssessmentBlock,
  AssessmentBlockResponse,
  AssessmentFinalResult,
} from '../types/assessment'

/**
 * T-004/T-065 自适应分块测评：开始 → 取下一块 → 提交（先存答案，后台评分）→
 * 轮询 next-block（evaluating 标记）直到出下一块或收敛定级，2–3 块出结果。
 */
export function useAssessmentFlow() {
  const [assessmentId, setAssessmentId] = useState<string | null>(null)
  const [block, setBlock] = useState<AssessmentBlock | null>(null)
  const [finalResult, setFinalResult] = useState<AssessmentFinalResult | null>(null)
  const [loading, setLoading] = useState(false)
  // T-065：评分中（提交已受理、后台评分中）——区别于旧同步 submitting 语义
  const [evaluating, setEvaluating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pollTimer = useRef<number | null>(null)

  const stopPolling = useCallback(() => {
    if (pollTimer.current !== null) {
      window.clearInterval(pollTimer.current)
      pollTimer.current = null
    }
  }, [])

  const loadNextBlock = useCallback(async (id: string) => {
    stopPolling()
    setEvaluating(false)
    const response = await api.get<AssessmentBlockResponse>(endpoints.assessmentNextBlock(id))
    if (response.data.converged) {
      setBlock(null)
      setFinalResult(response.data.final ?? null)
    } else {
      setBlock(response.data.block ?? null)
    }
  }, [stopPolling])

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
    setEvaluating(true)
    setError(null)
    try {
      // T-065：提交只受理答案，评分在后台任务进行；前端轮询 next-block 直到出题/收敛
      await api.post(endpoints.assessmentSubmitBlock(assessmentId, block.blockIndex), { answers })
      pollTimer.current = window.setInterval(() => {
        void (async () => {
          try {
            const response = await api.get<AssessmentBlockResponse>(endpoints.assessmentNextBlock(assessmentId))
            if (response.data.converged) {
              stopPolling()
              setEvaluating(false)
              setBlock(null)
              setFinalResult(response.data.final ?? null)
            } else if (response.data.evaluating) {
              // 仍在评分，继续轮询
            } else {
              stopPolling()
              setEvaluating(false)
              setBlock(response.data.block ?? null)
            }
          } catch {
            // 轮询失败继续等下一轮
          }
        })()
      }, 2000)
      return true
    } catch {
      setEvaluating(false)
      setError('提交失败，请重试。')
      return false
    }
  }

  return {
    assessmentId,
    block,
    finalResult,
    loading,
    evaluating,
    error,
    start,
    submitBlock,
  }
}
