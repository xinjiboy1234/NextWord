import { BookOpenText, Filter } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ArticleSummary, DifficultyLevel } from '../types/article'

interface ArticleLibraryProps {
  onOpen: (articleId: string) => void
}

export function ArticleLibrary({ onOpen }: ArticleLibraryProps) {
  const [articles, setArticles] = useState<ArticleSummary[]>([])
  const [level, setLevel] = useState<DifficultyLevel | 'All'>('All')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const params = level === 'All' ? undefined : { level }
      const response = await api.get<ArticleSummary[]>(endpoints.articles, { params })
      setArticles(response.data)
    } catch {
      setError('短文库加载失败。')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [level])

  const grouped = useMemo(() => {
    const map = new Map<string, ArticleSummary[]>()
    for (const article of articles) {
      const key = `${article.difficultyLevel} / ${article.cefrLevel}`
      const list = map.get(key) ?? []
      list.push(article)
      map.set(key, list)
    }
    return [...map.entries()]
  }, [articles])

  return (
    <div className="grid gap-5">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-2xl font-semibold">短文库</h2>
            <p className="mt-1 text-sm text-neutral-600">内置 {articles.length} 篇分级短文，支持点击查词与词汇提取。</p>
          </div>
          <label className="inline-flex items-center gap-2 text-sm">
            <Filter size={16} />
            <select
              value={level}
              onChange={(event) => setLevel(event.target.value as DifficultyLevel | 'All')}
              className="h-10 rounded-md border border-neutral-300 px-3"
            >
              <option value="All">全部难度</option>
              <option value="Basic">Basic</option>
              <option value="Intermediate">Intermediate</option>
              <option value="Advanced">Advanced</option>
            </select>
          </label>
        </div>

        {error && <p className="mt-4 rounded-md bg-rose-50 p-3 text-sm text-rose-900">{error}</p>}
        {loading ? (
          <p className="mt-6 text-sm text-neutral-600">加载中...</p>
        ) : (
          <div className="mt-5 grid gap-5">
            {grouped.map(([label, items]) => (
              <div key={label}>
                <h3 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">{label}</h3>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  {items.map((article) => (
                    <article key={article.id} className="rounded-md border border-neutral-200 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <h4 className="text-lg font-semibold">{article.title}</h4>
                          <p className="mt-1 text-sm text-neutral-600">
                            {article.wordCount} 词 · {article.source}
                            {article.topicTag ? ` · ${article.topicTag}` : ''}
                          </p>
                        </div>
                        <button
                          type="button"
                          onClick={() => onOpen(article.id)}
                          className="inline-flex h-10 items-center gap-2 rounded-md bg-emerald-700 px-3 text-sm font-medium text-white"
                        >
                          <BookOpenText size={16} />
                          阅读
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
