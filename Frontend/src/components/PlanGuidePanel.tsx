import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { CurrentLearningPlan, ExplorationWeek, ScenarioCatalog } from '../types/planner'

interface PlanGuidePanelProps {
  /** 点击「开始今日练习」时的回调（App 层退出 onboarding 并跳转）；缺省直接跳 /learn */
  onStart?: () => void
}

type GuideStatus = 'waiting' | 'ready' | 'timeout'

/**
 * T-066：首次测评完成后的「计划+练习安排」引导——轮询 /api/planner/current
 * （每 3 秒、最多 60 秒），计划或探索任务就绪后展示概览 +「开始今日练习」；
 * 超时降级为「计划后台生成，先去学今天的新词」。不再把用户直接丢回首页随意安排。
 */
export function PlanGuidePanel({ onStart }: PlanGuidePanelProps) {
  const navigate = useNavigate()
  const [status, setStatus] = useState<GuideStatus>('waiting')
  const [plan, setPlan] = useState<CurrentLearningPlan | null>(null)
  const [exploration, setExploration] = useState<ExplorationWeek | null>(null)
  const [scenarioNames, setScenarioNames] = useState<Record<string, string>>({})
  const attempts = useRef(0)

  useEffect(() => {
    let cancelled = false
    let timer: number | null = null

    async function loadScenarios() {
      try {
        const { data } = await api.get<ScenarioCatalog>(endpoints.scenarios)
        const map: Record<string, string> = {}
        for (const category of data.categories) {
          for (const sub of category.subScenarios) map[sub.key] = sub.zhName
        }
        if (!cancelled) setScenarioNames(map)
      } catch {
        // 映射失败回退原始 key，不阻塞
      }
    }

    async function poll() {
      attempts.current += 1
      const giveUp = () => {
        if (!cancelled) setStatus('timeout')
      }
      try {
        const { data } = await api.get<CurrentLearningPlan>(endpoints.plannerCurrent)
        if (cancelled) return
        if (data.active) {
          setPlan(data)
          setStatus('ready')
          return
        }
        if (data.exploration?.active) {
          setExploration(data.exploration)
          setStatus('ready')
          return
        }
        if (attempts.current >= 20) {
          giveUp()
          return
        }
        timer = window.setTimeout(() => void poll(), 3000)
      } catch {
        if (attempts.current >= 20) {
          giveUp()
          return
        }
        timer = window.setTimeout(() => void poll(), 3000)
      }
    }

    void loadScenarios()
    void poll()
    return () => {
      cancelled = true
      if (timer !== null) window.clearTimeout(timer)
    }
  }, [])

  function handleStart() {
    if (onStart) {
      onStart()
      return
    }
    navigate('/learn')
  }

  if (status === 'waiting') {
    return (
      <div className="alert alert-info" style={{ marginTop: 'var(--space-4)' }}>
        <p style={{ fontWeight: 540 }}>正在生成你的专属学习计划…</p>
        <p style={{ fontSize: 'var(--text-sm)', marginTop: 'var(--space-1)' }}>
          我们正根据你的测评结果安排主攻场景与每日练习（通常 10–30 秒），请稍候。
        </p>
      </div>
    )
  }

  if (status === 'timeout') {
    return (
      <div className="alert alert-info" style={{ marginTop: 'var(--space-4)' }}>
        <p style={{ fontWeight: 540 }}>学习计划正在后台生成</p>
        <p style={{ fontSize: 'var(--text-sm)', marginTop: 'var(--space-1)' }}>
          计划生成需要一点时间，你可以先去学今天的新词，稍后回到首页查看「今日学习计划」。
        </p>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          style={{ marginTop: 'var(--space-3)' }}
          onClick={handleStart}
        >
          先去学今天的新词
        </button>
      </div>
    )
  }

  // ready：优先展示计划；探索周且计划未就绪时展示探索任务
  if (plan?.active) {
    const focusNames = (plan.focusScenarios ?? []).map((key) => scenarioNames[key] ?? key)
    return (
      <div className="alert alert-success" style={{ marginTop: 'var(--space-4)' }}>
        <p style={{ fontWeight: 540 }}>今日学习计划已就绪</p>
        <ul style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }} className="stack stack-sm">
          {focusNames.length > 0 && <li>主攻场景：{focusNames.join('、')}</li>}
          <li>
            今日词队列：{plan.todayWordCount ?? 0} 词
            {(plan.todayExposureCount ?? 0) > 0 ? '（含 ' + (plan.todayExposureCount ?? 0) + ' 个接触词）' : ''}
          </li>
          {(plan.todaySentenceTargets ?? []).length > 0 && (
            <li>造句目标：{(plan.todaySentenceTargets ?? []).slice(0, 3).join('、')}</li>
          )}
        </ul>
        <button
          type="button"
          className="btn btn-primary"
          style={{ marginTop: 'var(--space-3)' }}
          onClick={handleStart}
        >
          开始今日练习
        </button>
      </div>
    )
  }

  if (exploration?.active) {
    return (
      <div className="alert alert-success" style={{ marginTop: 'var(--space-4)' }}>
        <p style={{ fontWeight: 540 }}>探索周 · 第 {exploration.day}/{exploration.totalDays} 天</p>
        <p style={{ fontSize: 'var(--text-sm)', marginTop: 'var(--space-1)' }}>
          今天试着写一段：{exploration.prompt ?? '围绕主题表达你的想法'}（再完成 {exploration.remainingEvidence} 条表达就能生成你的专属画像）
        </p>
        <button
          type="button"
          className="btn btn-primary"
          style={{ marginTop: 'var(--space-3)' }}
          onClick={() => (onStart ? onStart() : navigate('/sentence'))}
        >
          去写今日表达
        </button>
      </div>
    )
  }

  return (
    <div className="alert alert-info" style={{ marginTop: 'var(--space-4)' }}>
      <p style={{ fontWeight: 540 }}>学习计划准备中</p>
      <p style={{ fontSize: 'var(--text-sm)', marginTop: 'var(--space-1)' }}>
        你的个性化计划正在生成，先去首页看看，稍后回来就会出现在「今日学习计划」卡片里。
      </p>
      <button
        type="button"
        className="btn btn-primary btn-sm"
        style={{ marginTop: 'var(--space-3)' }}
        onClick={handleStart}
      >
        去学今天的新词
      </button>
    </div>
  )
}
