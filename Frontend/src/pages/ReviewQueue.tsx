import { RefreshCw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from '../components/ui/Badge'
import { StepNavigator } from '../components/StepNavigator'
import type { Word } from '../types/models'
import type { LogSummary, RecentLog } from '../types/sentence'

export function ReviewQueue() {
  const [summary, setSummary] = useState<LogSummary | null>(null)
  const [words, setWords] = useState<Word[]>([])
  const [logs, setLogs] = useState<RecentLog[]>([])
  const [loading, setLoading] = useState(true)
  const [index, setIndex] = useState(0)
  const [flipped, setFlipped] = useState(false)

  async function load() {
    setLoading(true)
    const [summaryResponse, wordsResponse, logsResponse] = await Promise.all([
      api.get<LogSummary>(endpoints.logSummary),
      api.get<Word[]>(endpoints.spellingQueue, { params: { count: 10, mode: 'review' } }),
      api.get<RecentLog[]>(endpoints.recentLogs, { params: { count: 10 } }),
    ])
    setSummary(summaryResponse.data)
    setWords(wordsResponse.data)
    setLogs(logsResponse.data)
    setIndex(0)
    setFlipped(false)
    setLoading(false)
  }

  useEffect(() => {
    void load()
  }, [])

  useEffect(() => {
    setFlipped(false)
  }, [index])

  if (loading) {
    return <div className="card"><p className="text-sm" style={{ color: 'var(--muted)' }}>正在加载复习队列...</p></div>
  }

  const currentWord = words[index] ?? null
  const dueCount = summary?.dueReviews ?? words.length

  return (
    <div className="review-layout">
      <div>
        <div className="section-header row-between">
          <div>
            <h2>复习队列</h2>
            <p>{dueCount} 个词待复习。点击卡片翻转查看释义。</p>
          </div>
          <button type="button" onClick={() => void load()} title="刷新" className="btn btn-ghost btn-sm">
            <RefreshCw size={16} aria-hidden="true" />
            刷新
          </button>
        </div>

        {!currentWord ? (
          <div className="empty-state">
            <p>暂无到期复习词。</p>
          </div>
        ) : (
          <div className="stack stack-md">
            <p className="mono-label" style={{ textTransform: 'none' }}>
              {index + 1} / {words.length}
            </p>

            <div
              className="flip-card"
              onClick={() => setFlipped((value) => !value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  setFlipped((value) => !value)
                }
              }}
              role="button"
              tabIndex={0}
              aria-label="翻转卡片查看释义"
            >
              <div className={`flip-inner${flipped ? ' flipped' : ''}`}>
                <div className="flip-front">
                  <p className="flip-word">{currentWord.lemma}</p>
                  <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)', marginTop: 'var(--space-2)' }}>
                    {currentWord.partOfSpeech} · {currentWord.phonetics}
                  </p>
                  <Badge variant="muted">{currentWord.cefrLevel}</Badge>
                  <p className="flip-hint">点击翻转查看释义</p>
                </div>
                <div className="flip-back">
                  <p className="flip-word" style={{ fontSize: 'var(--text-xl)' }}>{currentWord.meanings.join('；')}</p>
                  <p className="flip-hint">{currentWord.lemma}</p>
                </div>
              </div>
            </div>

            <StepNavigator
              index={index}
              total={words.length}
              onPrevious={() => setIndex((value) => Math.max(0, value - 1))}
              onNext={() => setIndex((value) => Math.min(value + 1, words.length - 1))}
              canPrevious={index > 0}
              canNext={index < words.length - 1}
            />
          </div>
        )}
      </div>

      <aside className="stack stack-md">
        {summary && (
          <div className="side-panel">
            <h4 className="side-panel-title">活动统计</h4>
            <div className="activity-stat"><span>造句</span><span className="val">{summary.sentenceCount}</span></div>
            <div className="activity-stat"><span>自由表达</span><span className="val">{summary.freeExpressionCount}</span></div>
            <div className="activity-stat"><span>拼写</span><span className="val">{summary.spellingCount}</span></div>
            <div className="activity-stat"><span>拼写正确率</span><span className="val">{summary.spellingAccuracyPercent}%</span></div>
          </div>
        )}
        <div className="side-panel">
          <h4 className="side-panel-title">最近记录</h4>
          {logs.length === 0 ? (
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>暂无记录</p>
          ) : (
            <div className="stack stack-sm">
              {logs.map((log) => (
                <div key={`${log.type}-${log.label}-${log.timestamp}`} className="activity-stat">
                  <span>{log.label}</span>
                  <span className="val" style={{ color: 'var(--muted)' }}>{log.result}</span>
                </div>
              ))}
              {/* T-030：造句字母档图例，避免用户不知道 B 算不算好 */}
              <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
                造句评分档：A 优秀 · B 良好 · C 及格 · D 需重写
              </p>
            </div>
          )}
        </div>
      </aside>
    </div>
  )
}
