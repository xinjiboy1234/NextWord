import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { BottleneckInsightResult } from '../types/planner'

export type BottleneckInsightStatus = 'loading' | 'error' | 'none' | 'found'

export interface BottleneckInsightView {
  status: BottleneckInsightStatus
  natureName: string
  /** 面向用户的一句话解释 */
  natureHint: string
  statement: string
  createdAt: string
  replanTriggered: boolean
}

/** 瓶颈性质枚举名 → 中文名 + 一句人话解释（与后端 BottleneckNature XML 注释对应） */
const NATURE_META: Record<string, { name: string; hint: string }> = {
  VocabularyInsufficient: { name: '词汇量不足', hint: '认识的词不够用，表达时容易卡壳' },
  CannotOrganizeSentences: { name: '组句困难', hint: '单词都认识，但组织不成通顺的句子' },
  GrammarErrors: { name: '语法错误偏多', hint: '句子结构错误较多，影响表达的准确性' },
  MonotonousExpression: { name: '表达单调', hint: '语法没问题，但表达方式单一、缺少变化' },
  AvoidancePattern: { name: '回避模式', hint: '在回避复杂表达，舒适圈在慢慢收缩' },
  ChinglishCollocation: { name: '中式搭配', hint: '用词搭配偏中式思维，不够地道' },
  SafeWordStrategy: { name: '安全词策略', hint: '只用熟悉的老词，新学的内容还没用起来' },
}

const INITIAL: BottleneckInsightView = {
  status: 'loading',
  natureName: '',
  natureHint: '',
  statement: '',
  createdAt: '',
  replanTriggered: false,
}

/**
 * T-019：拉取最新瓶颈洞察。失败静默降级为 error（卡片不展示）。
 */
export function useBottleneckInsight() {
  const [view, setView] = useState<BottleneckInsightView>(INITIAL)

  useEffect(() => {
    let cancelled = false

    async function load() {
      let result: BottleneckInsightResult
      try {
        const { data } = await api.get<BottleneckInsightResult>(endpoints.insightBottleneckLatest)
        result = data
      } catch {
        if (!cancelled) setView({ ...INITIAL, status: 'error' })
        return
      }

      if (cancelled) return
      if (!result.found) {
        setView({ ...INITIAL, status: 'none' })
        return
      }

      const meta = (result.nature && NATURE_META[result.nature]) || {
        name: result.nature ?? '',
        hint: '',
      }
      setView({
        status: 'found',
        natureName: meta.name,
        natureHint: meta.hint,
        statement: result.statement ?? '',
        createdAt: result.createdAt ?? '',
        replanTriggered: result.replanTriggered ?? false,
      })
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [])

  return view
}
