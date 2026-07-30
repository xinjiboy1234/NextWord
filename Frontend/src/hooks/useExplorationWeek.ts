import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { CurrentLearningPlan, ExplorationWeek } from '../types/planner'

/**
 * T-032：拉取探索周状态（planner/current 附带字段）。
 * 请求失败静默为 null（不展示探索任务入口），不影响页面其它模块。
 */
export function useExplorationWeek() {
  const [exploration, setExploration] = useState<ExplorationWeek | null>(null)

  useEffect(() => {
    let cancelled = false
    api
      .get<CurrentLearningPlan>(endpoints.plannerCurrent)
      .then(({ data }) => {
        if (!cancelled) setExploration(data.exploration ?? null)
      })
      .catch(() => {
        // ignore，探索任务入口静默降级
      })
    return () => {
      cancelled = true
    }
  }, [])

  return exploration
}
