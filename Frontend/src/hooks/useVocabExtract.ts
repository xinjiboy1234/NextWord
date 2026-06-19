import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ArticleVocabMapping } from '../types/article'

export function useVocabExtract(articleId: string | null) {
  const [items, setItems] = useState<ArticleVocabMapping[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadExisting = useCallback(async () => {
    if (!articleId) return
    const response = await api.get<ArticleVocabMapping[]>(endpoints.articleVocab(articleId))
    setItems(response.data)
  }, [articleId])

  const extract = useCallback(async () => {
    if (!articleId) return
    setLoading(true)
    setError(null)
    try {
      const response = await api.post<ArticleVocabMapping[]>(endpoints.articleVocabExtract(articleId), {})
      setItems(response.data)
    } catch {
      setError('词汇提取失败，请稍后重试。')
    } finally {
      setLoading(false)
    }
  }, [articleId])

  return { items, loading, error, loadExisting, extract }
}
