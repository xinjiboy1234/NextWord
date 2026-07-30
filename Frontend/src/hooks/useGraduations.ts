import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { GraduatedWord } from '../types/models'

export interface GraduationsView {
  status: 'loading' | 'error' | 'ready'
  words: GraduatedWord[]
  /** 近 7 天毕业数（Dashboard 计划卡下方计数，0 不展示） */
  weeklyCount: number
  /** 已毕业词 Id 集合（词库「已毕业」标记用） */
  graduatedWordIds: Set<string>
}

const EMPTY: GraduationsView = {
  status: 'loading',
  words: [],
  weeklyCount: 0,
  graduatedWordIds: new Set<string>(),
}

const WEEK_MS = 7 * 24 * 60 * 60 * 1000

/**
 * T-034：拉取当前用户已毕业（spontaneous_use）词列表。
 * 请求失败静默降级为 error（毕业计数与标记不展示），不影响页面其它模块。
 */
export function useGraduations(): GraduationsView {
  const [view, setView] = useState<GraduationsView>(EMPTY)

  useEffect(() => {
    let cancelled = false

    async function load() {
      try {
        const { data } = await api.get<GraduatedWord[]>(endpoints.wordsGraduated)
        if (cancelled) return
        const cutoff = Date.now() - WEEK_MS
        const weeklyCount = data.filter(
          (item) => item.graduatedAt !== null && new Date(item.graduatedAt).getTime() >= cutoff,
        ).length
        setView({
          status: 'ready',
          words: data,
          weeklyCount,
          graduatedWordIds: new Set(data.map((item) => item.wordId)),
        })
      } catch {
        if (!cancelled) setView({ ...EMPTY, status: 'error' })
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [])

  return view
}
