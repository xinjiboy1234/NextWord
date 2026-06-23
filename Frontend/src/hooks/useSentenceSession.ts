import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { SentencePrompt, SentenceRating } from '../types/sentence'

export function useSentenceSession() {
  const [prompts, setPrompts] = useState<SentencePrompt[]>([])
  const [index, setIndex] = useState(0)
  const [rating, setRating] = useState<SentenceRating | null>(null)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const { data } = await api.get<SentencePrompt[]>(endpoints.sentencePrompts, { params: { count: 10 } })
      setPrompts(data)
      setIndex(0)
      setRating(null)
    } catch {
      setError('造句题库加载失败')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const current = prompts[index] ?? null

  async function submit(userSentence: string, scene: string) {
    if (!current || userSentence.trim().length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      const { data } = await api.post<SentenceRating>(endpoints.sentenceRate, {
        wordId: current.wordId,
        targetWord: current.targetWord,
        userSentence,
        scene,
        userLevel: 'A2',
      })
      setRating(data)
    } catch {
      setError('评分失败，请稍后重试')
    } finally {
      setSubmitting(false)
    }
  }

  function next() {
    setRating(null)
    setIndex((value) => Math.min(value + 1, Math.max(prompts.length - 1, 0)))
  }

  function prev() {
    setRating(null)
    setIndex((value) => Math.max(0, value - 1))
  }

  return useMemo(
    () => ({
      prompts,
      current,
      index,
      total: prompts.length,
      rating,
      loading,
      submitting,
      error,
      reload: load,
      submit,
      next,
      prev,
    }),
    [prompts, current, index, rating, loading, submitting, error],
  )
}
