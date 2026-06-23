import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { Word } from '../types/models'
import type { SpellingResult } from '../types/sentence'

export function useSpellingSession() {
  const [words, setWords] = useState<Word[]>([])
  const [index, setIndex] = useState(0)
  const [result, setResult] = useState<SpellingResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const { data } = await api.get<Word[]>(endpoints.spellingQueue, { params: { count: 8 } })
      setWords(data)
      setIndex(0)
      setResult(null)
    } catch {
      setError('拼写队列加载失败')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const currentWord = words[index] ?? null

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
    } catch {
      setError('拼写提交失败')
    } finally {
      setSubmitting(false)
    }
  }

  function next() {
    setResult(null)
    setIndex((value) => Math.min(value + 1, Math.max(words.length - 1, 0)))
  }

  function prev() {
    setResult(null)
    setIndex((value) => Math.max(0, value - 1))
  }

  return useMemo(
    () => ({
      words,
      currentWord,
      index,
      total: words.length,
      result,
      loading,
      submitting,
      error,
      reload: load,
      submit,
      next,
      prev,
    }),
    [words, currentWord, index, result, loading, submitting, error],
  )
}
