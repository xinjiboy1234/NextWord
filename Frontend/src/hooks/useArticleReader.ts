import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ArticleComment, ArticleDetail, ReadingLog } from '../types/article'

export function useArticleReader(articleId: string | null) {
  const [article, setArticle] = useState<ArticleDetail | null>(null)
  const [readingLog, setReadingLog] = useState<ReadingLog | null>(null)
  const [comments, setComments] = useState<ArticleComment[]>([])
  const [lookupCount, setLookupCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const startRef = useRef<number>(Date.now())

  const load = useCallback(async () => {
    if (!articleId) return
    setLoading(true)
    setError(null)
    try {
      const [articleRes, commentsRes, startRes] = await Promise.all([
        api.get<ArticleDetail>(endpoints.articleDetail(articleId)),
        api.get<ArticleComment[]>(endpoints.articleComments(articleId)),
        api.post<ReadingLog>(endpoints.articleReadingStart(articleId), {}),
      ])
      setArticle(articleRes.data)
      setComments(commentsRes.data)
      setReadingLog(startRes.data)
      startRef.current = Date.now()
      setLookupCount(0)
    } catch {
      setError('文章加载失败。')
    } finally {
      setLoading(false)
    }
  }, [articleId])

  useEffect(() => {
    void load()
  }, [load])

  async function finishReading(commentsCount: number) {
    if (!readingLog) return
    await api.post(endpoints.readingLogFinish(readingLog.id), {
      lookupCount,
      commentsCount,
    })
  }

  async function recordLookup() {
    if (!readingLog) return
    setLookupCount((count) => count + 1)
    await api.post(endpoints.readingLogLookup(readingLog.id))
  }

  async function refreshComments() {
    if (!articleId) return
    const response = await api.get<ArticleComment[]>(endpoints.articleComments(articleId))
    setComments(response.data)
  }

  return {
    article,
    readingLog,
    comments,
    lookupCount,
    loading,
    error,
    elapsedSeconds: Math.floor((Date.now() - startRef.current) / 1000),
    reload: load,
    finishReading,
    recordLookup,
    refreshComments,
  }
}
