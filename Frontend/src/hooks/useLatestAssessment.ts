import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { AssessmentListItem } from '../types/assessment'

/**
 * T-069 「我的」页最近测评简要卡数据：取最新一次已完成的初始测评，
 * 简要但有用（定级 + 人话标签 + 表达分 + 常见问题），详情放背后由「查看测评结果」打开。
 */
export interface LatestAssessmentBrief {
  id: string
  finalLevel: string
  expressionScore: number | null
  rubricLabel: string | null
  guardAdjusted: boolean
  startAt: string
  topErrorTags: string[]
}

export function useLatestAssessment() {
  const [brief, setBrief] = useState<LatestAssessmentBrief | null>(null)
  const [loading, setLoading] = useState(true)
  // 请求失败静默降级：不渲染卡片（与 Dashboard 计划/洞察卡同口径）
  const [error, setError] = useState(false)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const { data } = await api.get<AssessmentListItem[]>(endpoints.assessments)
      // 当前测评 = 最新一次已完成的初始测评（进行中/挑战类不计入）
      const latest = data.find(
        (item) => item.type === 'Initial' && item.status === 'Completed' && item.finalLevel != null,
      )
      if (!latest || latest.finalLevel == null) {
        setBrief(null)
        return
      }
      setBrief({
        id: latest.id,
        finalLevel: latest.finalLevel,
        expressionScore: latest.expressionScore ?? null,
        rubricLabel: latest.rubricLabel ?? null,
        guardAdjusted: latest.guardAdjusted,
        startAt: latest.startAt,
        topErrorTags: latest.topErrorTags ?? [],
      })
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return { brief, loading, error, refresh }
}
