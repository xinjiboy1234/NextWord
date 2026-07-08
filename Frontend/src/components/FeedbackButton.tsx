import { useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'

interface FeedbackButtonProps {
  word: string
  contextJson?: string | null
  disabled?: boolean
}

export function FeedbackButton({ word, contextJson, disabled }: FeedbackButtonProps) {
  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function submit(feedbackType: string) {
    if (disabled || submitting) return
    setSubmitting(true)
    setMessage(null)
    try {
      await api.post(endpoints.feedback, {
        feedbackType,
        targetWord: word,
        contextJson: contextJson ?? null,
      })
      setMessage(
        feedbackType === 'DefinitionWrong'
          ? '已提交释义反馈，将重新标注'
          : feedbackType === 'MarkKnown'
            ? '已标记为掌握'
            : '已加入不再推荐列表',
      )
    } catch {
      setMessage('反馈提交失败')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="stack stack-sm" style={{ marginTop: 'var(--space-4)' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--space-2)' }}>
        <button type="button" className="btn btn-ghost btn-sm" disabled={disabled || submitting} onClick={() => void submit('DefinitionWrong')}>
          释义有误
        </button>
        <button type="button" className="btn btn-ghost btn-sm" disabled={disabled || submitting} onClick={() => void submit('MarkKnown')}>
          标记已掌握
        </button>
        <button type="button" className="btn btn-ghost btn-sm" disabled={disabled || submitting} onClick={() => void submit('ExcludeWord')}>
          不再推荐
        </button>
      </div>
      {message ? <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>{message}</p> : null}
    </div>
  )
}
