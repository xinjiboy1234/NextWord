import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { DailyWordItem } from '../types/score'
import { DEFAULT_DAILY_WORD_COUNT } from './useDailyWordCount'

export function useWordSession(count: number = DEFAULT_DAILY_WORD_COUNT) {
  const [words, setWords] = useState<DailyWordItem[]>([])
  const [index, setIndex] = useState(0)
  const [answeredIds, setAnsweredIds] = useState<ReadonlySet<string>>(new Set())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await api.get<DailyWordItem[]>(endpoints.dailyWords, { params: { count } })
      setWords(response.data)
      setIndex(0)
      setAnsweredIds(new Set())
    } catch {
      setError('无法加载今日单词，请确认后端 API 已启动。')
    } finally {
      setLoading(false)
    }
  }, [count])

  useEffect(() => {
    void load()
  }, [load])

  const currentWord = words[index] ?? null
  // T-050 进度口径：已作答数 / 本次队列总数（按词 id 去重，prev 回退不倒退）；
  // 末词提交后 100%，再点「完成」进完成页
  const progress = useMemo(() => {
    if (words.length === 0) return 0
    return Math.round((Math.min(answeredIds.size, words.length) / words.length) * 100)
  }, [answeredIds, words.length])

  const markAnswered = useCallback((wordId: string) => {
    setAnsweredIds((current) => {
      if (current.has(wordId)) return current
      const next = new Set(current)
      next.add(wordId)
      return next
    })
  }, [])

  const next = useCallback(() => {
    setIndex((value) => Math.min(value + 1, words.length))
  }, [words.length])

  const prev = useCallback(() => {
    setIndex((value) => Math.max(0, value - 1))
  }, [])

  return {
    words,
    currentWord,
    index,
    total: words.length,
    progress,
    answeredCount: answeredIds.size,
    loading,
    error,
    reload: load,
    markAnswered,
    next,
    prev,
    completed: words.length > 0 && index >= words.length,
  }
}
