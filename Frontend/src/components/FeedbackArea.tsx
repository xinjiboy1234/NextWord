import { CalendarClock, CheckCircle2, XCircle } from 'lucide-react'
import type { LearningResult } from '../types/models'
import { stageLabel } from './WordDisplay'

interface FeedbackAreaProps {
  result: LearningResult | null
  error: string | null
}

export function FeedbackArea({ result, error }: FeedbackAreaProps) {
  if (error) {
    return <div className="alert alert-error">{error}</div>
  }

  if (!result) {
    return null
  }

  const Icon = result.isCorrect ? CheckCircle2 : XCircle
  return (
    <div className="card">
      <div className="row" style={{ marginBottom: 'var(--space-3)' }}>
        <Icon size={20} style={{ color: result.isCorrect ? 'var(--success)' : 'var(--danger)' }} aria-hidden="true" />
        <h3 style={{ fontWeight: 540 }}>{result.isCorrect ? '回答正确' : '需要复习'}</h3>
      </div>
      <dl className="stack stack-sm" style={{ fontSize: 'var(--text-sm)' }}>
        <div>
          <dt className="mono-label" style={{ textTransform: 'none', marginBottom: 4 }}>释义</dt>
          <dd>{result.meanings.join('；')}</dd>
        </div>
        <div>
          <dt className="mono-label" style={{ textTransform: 'none', marginBottom: 4 }}>例句</dt>
          <dd style={{ fontStyle: 'italic', color: 'var(--muted)' }}>{result.exampleSentences[0] ?? '暂无例句'}</dd>
        </div>
        <div className="row" style={{ color: 'var(--muted)' }}>
          <CalendarClock size={18} aria-hidden="true" />
          <span>
            下次复习间隔 {result.intervalDays} 天，阶段 {stageLabel(result.stage)}（掌握度 {Math.round(result.masteryScore)}%）
          </span>
        </div>
      </dl>
    </div>
  )
}
