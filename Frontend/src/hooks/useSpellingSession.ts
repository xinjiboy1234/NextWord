import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { SpellingQueueWord } from '../types/models'
import type { SpellingResult } from '../types/sentence'
import { DEFAULT_SPELLING_COUNT } from './useSpellingCount'
import { DEFAULT_SPELLING_MODE, type SpellingQueueMode } from './useSpellingMode'

// T-052：mode（review/new/mixed）透传后端；改模式即重载队列
export function useSpellingSession(count: number = DEFAULT_SPELLING_COUNT, mode: SpellingQueueMode = DEFAULT_SPELLING_MODE) {
  const [words, setWords] = useState<SpellingQueueWord[]>([])
  const [index, setIndex] = useState(0)
  const [result, setResult] = useState<SpellingResult | null>(null)
  const [answeredIds, setAnsweredIds] = useState<ReadonlySet<string>>(new Set())
  const [results, setResults] = useState<Record<string, SpellingResult>>({})
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const { data } = await api.get<SpellingQueueWord[]>(endpoints.spellingQueue, { params: { count, mode } })
      setWords(data)
      setIndex(0)
      setResult(null)
      setAnsweredIds(new Set())
      setResults({})
    } catch {
      setError('拼写队列加载失败')
    } finally {
      setLoading(false)
    }
  }, [count, mode])

  useEffect(() => {
    void load()
  }, [load])

  const currentWord = words[index] ?? null

  // T-051 进度口径与 /learn（T-050）一致：已作答数 / 本次队列总数
  //（按词 id 去重，prev 回退不倒退）；末词提交后 100%，再点「完成」进完成页
  const progress = useMemo(() => {
    if (words.length === 0) return 0
    return Math.round((Math.min(answeredIds.size, words.length) / words.length) * 100)
  }, [answeredIds, words.length])

  async function submit(userSpelling: string, attempts = 1) {
    if (!currentWord || userSpelling.trim().length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      const { data } = await api.post<SpellingResult>(endpoints.spellingSubmit, {
        wordId: currentWord.id,
        userSpelling,
        attempts,
      })
      setResult(data)
      setAnsweredIds((current) => {
        if (current.has(currentWord.id)) return current
        const next = new Set(current)
        next.add(currentWord.id)
        return next
      })
      setResults((current) => ({ ...current, [currentWord.id]: data }))
    } catch {
      setError('拼写提交失败')
    } finally {
      setSubmitting(false)
    }
  }

  // T-051：不再钳制在最后一词，末词后可越过下标进入完成态
  function next() {
    setResult(null)
    setIndex((value) => Math.min(value + 1, words.length))
  }

  function prev() {
    setResult(null)
    setIndex((value) => Math.max(0, value - 1))
  }

  const completed = words.length > 0 && index >= words.length
  const correctCount = useMemo(
    () => Object.values(results).filter((item) => item.isCorrect).length,
    [results],
  )
  const missedWords = useMemo(
    () => words.filter((word) => results[word.id] && !results[word.id].isCorrect),
    [words, results],
  )

  return useMemo(
    () => ({
      words,
      currentWord,
      index,
      total: words.length,
      progress,
      answeredCount: answeredIds.size,
      completed,
      correctCount,
      missedWords,
      result,
      loading,
      submitting,
      error,
      reload: load,
      submit,
      next,
      prev,
    }),
    [words, currentWord, index, progress, answeredIds, completed, correctCount, missedWords, result, loading, submitting, error, load],
  )
}
