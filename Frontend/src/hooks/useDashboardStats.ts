import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { nextCefrLevel } from '../lib/cefr'
import type { ProgressSummary, Word } from '../types/models'
import type { SentencePrompt } from '../types/sentence'

export type BadgeVariant = 'muted' | 'success' | 'warn' | 'info'

export interface ModuleBadge {
  text: string
  variant: BadgeVariant
}

export interface DashboardStats {
  loading: boolean
  learn: ModuleBadge
  spelling: ModuleBadge
  sentence: ModuleBadge
  reading: ModuleBadge
  level: ModuleBadge
  review: ModuleBadge
  progress: ModuleBadge
}

const PLACEHOLDER: ModuleBadge = { text: '—', variant: 'muted' }

function badge(text: string, variant: BadgeVariant = 'muted'): ModuleBadge {
  return { text, variant }
}

export function useDashboardStats(progress: ProgressSummary | null) {
  const [stats, setStats] = useState<DashboardStats>({
    loading: true,
    learn: PLACEHOLDER,
    spelling: PLACEHOLDER,
    sentence: PLACEHOLDER,
    reading: PLACEHOLDER,
    level: PLACEHOLDER,
    review: PLACEHOLDER,
    progress: PLACEHOLDER,
  })

  useEffect(() => {
    if (!progress) {
      setStats((current) => ({ ...current, loading: true }))
      return
    }

    let cancelled = false

    async function load() {
      if (!progress) return

      const summary = progress
      setStats((current) => ({ ...current, loading: true }))

      const [dailyResult, spellingResult, sentenceResult] = await Promise.allSettled([
        api.get<Word[]>(endpoints.dailyWords, { params: { count: 8 } }),
        api.get<Word[]>(endpoints.spellingQueue, { params: { count: 8 } }),
        api.get<SentencePrompt[]>(endpoints.sentencePrompts, { params: { count: 10 } }),
      ])

      if (cancelled) return

      const dailyCount =
        dailyResult.status === 'fulfilled' ? dailyResult.value.data.length : null
      const spellingCount =
        spellingResult.status === 'fulfilled' ? spellingResult.value.data.length : null
      const sentenceCount =
        sentenceResult.status === 'fulfilled' ? sentenceResult.value.data.length : null

      const nextLevel = nextCefrLevel(summary.overallLevel)

      setStats({
        loading: false,
        learn: badge(
          dailyCount !== null ? `${dailyCount} 个新词` : '待加载',
          'muted',
        ),
        spelling: badge(
          spellingCount !== null ? `${spellingCount} 题` : '待加载',
          'muted',
        ),
        sentence: badge(
          sentenceCount !== null ? `${sentenceCount} 道题` : '待加载',
          'muted',
        ),
        reading: badge(summary.overallLevel || '—', 'muted'),
        level: summary.isUpgradeCandidate && nextLevel
          ? badge(`${summary.overallLevel} → ${nextLevel}`, 'info')
          : badge(summary.overallLevel || '—', 'muted'),
        review: badge(
          summary.dueReviews > 0 ? `${summary.dueReviews} 待复习` : '暂无',
          summary.dueReviews > 0 ? 'warn' : 'muted',
        ),
        progress: badge(
          summary.streakDays > 0 ? `连续 ${summary.streakDays} 天` : '开始打卡',
          summary.streakDays > 0 ? 'success' : 'muted',
        ),
      })
    }

    void load()

    return () => {
      cancelled = true
    }
  }, [progress])

  return stats
}
