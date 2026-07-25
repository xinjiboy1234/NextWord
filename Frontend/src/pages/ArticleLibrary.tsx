import { BookOpenText } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Select } from '../components/ui/Select'
import type { ArticleSummary, DifficultyLevel } from '../types/article'

interface ArticleLibraryProps {
  onOpen: (articleId: string) => void
}

const LEVEL_LABELS: Record<string, string> = {
  All: '全部难度',
  Basic: '基础',
  Intermediate: '中级',
  Advanced: '高级',
}

export function ArticleLibrary({ onOpen }: ArticleLibraryProps) {
  const [articles, setArticles] = useState<ArticleSummary[]>([])
  const [recommended, setRecommended] = useState<ArticleSummary[]>([])
  const [recommendedFromPlan, setRecommendedFromPlan] = useState(false)
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

  // T-006：今日阅读推荐（有当日计划时来自计划主攻场景）
  useEffect(() => {
    api
      .get<{ fromPlan: boolean; articles: ArticleSummary[] }>(endpoints.articlesRecommended)
      .then((response) => {
        setRecommended(response.data.articles)
        setRecommendedFromPlan(response.data.fromPlan)
      })
      .catch(() => {
        setRecommended([])
        setRecommendedFromPlan(false)
      })
  }, [])

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
    <div className="stack stack-md">
      <div className="section-header row-between" style={{ flexWrap: 'wrap' }}>
        <div>
          <h2>短文库</h2>
          <p>内置 {articles.length} 篇分级短文，支持点击查词与词汇提取。</p>
        </div>
        <div className="field" style={{ minWidth: 160 }}>
          <label htmlFor="article-level">难度筛选</label>
          <Select
            value={level}
            onValueChange={(value) => setLevel(value as DifficultyLevel | 'All')}
            options={[
              { value: 'All', label: LEVEL_LABELS.All },
              { value: 'Basic', label: LEVEL_LABELS.Basic },
              { value: 'Intermediate', label: LEVEL_LABELS.Intermediate },
              { value: 'Advanced', label: LEVEL_LABELS.Advanced },
            ]}
            aria-label="难度筛选"
          />
        </div>
      </div>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {recommended.length > 0 ? (
        <div className="card">
          <p className="mono-label" style={{ textTransform: 'none' }}>
            今日推荐{recommendedFromPlan ? '（来自今日计划）' : ''}
          </p>
          <div className="stack stack-sm" style={{ marginTop: 'var(--space-3)' }}>
            {recommended.map((article) => (
              <div key={article.id} className="row-between" style={{ flexWrap: 'wrap' }}>
                <div>
                  <h4 style={{ fontWeight: 540, fontSize: 'var(--text-base)' }}>{article.title}</h4>
                  <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
                    {article.wordCount} 词 · {article.cefrLevel}
                  </p>
                </div>
                <button type="button" onClick={() => onOpen(article.id)} className="btn btn-primary btn-sm">
                  <BookOpenText size={16} aria-hidden="true" />
                  阅读
                </button>
              </div>
            ))}
          </div>
        </div>
      ) : null}
      {loading ? (
        <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)' }}>加载中...</p>
      ) : grouped.length === 0 ? (
        <div className="empty-state"><p>暂无文章</p></div>
      ) : (
        <div className="stack stack-md">
          {grouped.map(([label, items]) => (
            <div key={label}>
              <p className="article-group-label">{label}</p>
              <div className="stack stack-sm">
                {items.map((article) => (
                  <article key={article.id} className="card row-between" style={{ flexWrap: 'wrap' }}>
                    <div>
                      <h4 style={{ fontWeight: 540, fontSize: 'var(--text-base)' }}>{article.title}</h4>
                      <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
                        {article.wordCount} 词 · {article.source}
                        {article.topicTag ? ` · ${article.topicTag}` : ''}
                      </p>
                    </div>
                    <button type="button" onClick={() => onOpen(article.id)} className="btn btn-primary btn-sm">
                      <BookOpenText size={16} aria-hidden="true" />
                      阅读
                    </button>
                  </article>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
