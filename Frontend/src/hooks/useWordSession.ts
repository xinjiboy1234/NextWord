import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { Word } from '../types/models'

export function useWordSession() {
  const [words, setWords] = useState<Word[]>([])
  const [index, setIndex] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await api.get<Word[]>(endpoints.dailyWords, { params: { count: 8 } })
      setWords(response.data)
      setIndex(0)
    } catch {
      setError('无法加载今日单词，请确认后端 API 已启动。')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const currentWord = words[index] ?? null
  const progress = useMemo(() => {
    if (words.length === 0) return 0
    return Math.round((index / words.length) * 100)
  }, [index, words.length])

  const next = useCallback(() => {
    setIndex((value) => Math.min(value + 1, words.length))
  }, [words.length])

  return {
    words,
    currentWord,
    index,
    total: words.length,
    progress,
    loading,
    error,
    reload: load,
    next,
    completed: words.length > 0 && index >= words.length,
  }
}
