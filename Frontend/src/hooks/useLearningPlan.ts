import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { CurrentLearningPlan, ExplorationWeek, ScenarioCatalog } from '../types/planner'

export type LearningPlanStatus = 'loading' | 'error' | 'none' | 'active'

export interface LearningPlanView {
  status: LearningPlanStatus
  /** 0 起的天索引，展示时用 dayIndex + 1 */
  dayIndex: number
  /** 主攻场景中文名（映射失败时回退原始 key） */
  focusScenarioNames: string[]
  todayWordCount: number
  todayExposureCount: number
  todaySentenceTargets: string[]
  /** sourceFindingIds 非空 = 个性化（弱点画像驱动），为空 = 探索期 */
  personalized: boolean
  /** T-032 探索周进度（注册起 7 天内有效；无 Plan 时也可能存在） */
  exploration: ExplorationWeek | null
}

const INITIAL: LearningPlanView = {
  status: 'loading',
  dayIndex: 0,
  focusScenarioNames: [],
  todayWordCount: 0,
  todayExposureCount: 0,
  todaySentenceTargets: [],
  personalized: false,
  exploration: null,
}

/**
 * T-018：拉取当日学习计划，并把 focusScenarios 的场景 key 映射为中文名。
 * 请求失败静默降级为 error（卡片不展示），不影响首页其它模块。
 */
export function useLearningPlan() {
  const [view, setView] = useState<LearningPlanView>(INITIAL)

  useEffect(() => {
    let cancelled = false

    async function load() {
      let plan: CurrentLearningPlan
      try {
        const { data } = await api.get<CurrentLearningPlan>(endpoints.plannerCurrent)
        plan = data
      } catch {
        if (!cancelled) setView({ ...INITIAL, status: 'error' })
        return
      }

      if (!plan.active) {
        if (!cancelled) setView({ ...INITIAL, status: 'none', exploration: plan.exploration ?? null })
        return
      }

      // 场景中文名映射失败不阻塞卡片展示，回退展示原始 key
      const nameByKey = new Map<string, string>()
      try {
        const { data } = await api.get<ScenarioCatalog>(endpoints.scenarios)
        for (const category of data.categories) {
          for (const sub of category.subScenarios) {
            nameByKey.set(sub.key, sub.zhName)
          }
        }
      } catch {
        // ignore，使用原始 key
      }

      if (cancelled) return
      const focusKeys = plan.focusScenarios ?? []
      setView({
        status: 'active',
        dayIndex: plan.dayIndex ?? 0,
        focusScenarioNames: focusKeys.map((key) => nameByKey.get(key) ?? key),
        todayWordCount: plan.todayWordCount ?? 0,
        todayExposureCount: plan.todayExposureCount ?? 0,
        todaySentenceTargets: plan.todaySentenceTargets ?? [],
        personalized: (plan.sourceFindingIds?.length ?? 0) > 0,
        exploration: plan.exploration ?? null,
      })
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [])

  return view
}
