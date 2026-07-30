import { useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { AiRevision } from '../components/AiRevision'
import { ErrorAnalysis } from '../components/ErrorAnalysis'
import { WritingScoreBadge } from '../components/WritingScoreBadge'
import type { FreeExpressionRating } from '../types/sentence'

interface FreeExpressionProps {
  userLevel?: string
}

export function FreeExpression({ userLevel = 'A2' }: FreeExpressionProps) {
  const [text, setText] = useState('')
  const [rating, setRating] = useState<FreeExpressionRating | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (text.trim().length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      const { data } = await api.post<FreeExpressionRating>(endpoints.freeExpressionRate, {
        userText: text,
        userLevel,
      })
      setRating(data)
    } catch {
      setError('自由表达评分失败')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="grid-2-1">
      <div className="card stack stack-md">
        <div>
          <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>自由表达</h2>
          <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            写一段 2–5 句英文，系统会给出整体反馈。
          </p>
        </div>
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          className="textarea"
          style={{ minHeight: 200 }}
          placeholder="Today I want to talk about..."
          autoComplete="off"
        />
        {error ? <div className="alert alert-error">{error}</div> : null}
        <button
          type="button"
          disabled={submitting || text.trim().length === 0}
          onClick={() => void submit()}
          className="btn btn-primary"
          style={{ width: 'fit-content' }}
        >
          获取反馈
        </button>
      </div>

      <aside className="stack stack-md">
        {/* T-034：毕业时刻提示（本次自由表达中自发使用的词毕业） */}
        {rating && (rating.graduatedWords?.length ?? 0) > 0 && (
          <div className="alert alert-success">
            🎉 {rating.graduatedWords!.map((word) => `『${word}』`).join('')}
            毕业了——你已经在自发使用{rating.graduatedWords!.length > 1 ? '它们' : '它'}
          </div>
        )}
        {rating && (
          <div className="side-panel" style={{ textAlign: 'center' }}>
            <p className="mono-label" style={{ textTransform: 'none' }}>综合分</p>
            <div className="score-value" style={{ marginTop: 'var(--space-2)' }}>{rating.aiScore}</div>
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>/ 100</p>
            <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)', justifyContent: 'center' }}>
              <span className="badge badge-success">{rating.overallGrade}</span>
              <WritingScoreBadge before={rating.writingScoreBefore} after={rating.writingScoreAfter} />
            </div>
          </div>
        )}
        <AiRevision value={rating?.aiRevision} />
        <ErrorAnalysis items={rating?.errorSentences} suggestion={rating?.suggestions.join(' ')} />
      </aside>
    </div>
  )
}
