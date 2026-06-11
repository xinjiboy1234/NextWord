import { useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { AiRevision } from '../components/AiRevision'
import { ErrorAnalysis } from '../components/ErrorAnalysis'
import type { FreeExpressionRating } from '../types/sentence'

export function FreeExpression() {
  const [text, setText] = useState('')
  const [rating, setRating] = useState<FreeExpressionRating | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (text.trim().length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      const { data } = await api.post<FreeExpressionRating>(endpoints.freeExpressionRate, { userText: text, userLevel: 'A2' })
      setRating(data)
    } catch {
      setError('自由表达评分失败')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
      <section className="grid gap-4 rounded-md border border-neutral-200 bg-white p-5">
        <div>
          <h2 className="text-2xl font-semibold">自由表达</h2>
          <p className="mt-1 text-sm text-neutral-600">写一段 2-5 句英文，系统会给出整体反馈。</p>
        </div>
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          className="min-h-56 resize-y rounded-md border border-neutral-300 p-3 text-base leading-7 outline-none focus:border-emerald-700"
          placeholder="Today I want to talk about..."
        />
        {error && <p className="text-sm text-rose-700">{error}</p>}
        <button type="button" disabled={submitting || text.trim().length === 0} onClick={() => void submit()} className="h-11 w-fit rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-neutral-300">
          获取反馈
        </button>
      </section>

      <aside className="grid content-start gap-4">
        {rating && (
          <section className="rounded-md border border-neutral-200 bg-white p-5">
            <p className="text-sm text-neutral-500">综合分</p>
            <div className="mt-2 flex items-end gap-2">
              <span className="text-4xl font-semibold">{rating.aiScore}</span>
              <span className="pb-1 text-sm text-neutral-500">/ 100</span>
            </div>
            <span className="mt-3 inline-flex rounded-md bg-emerald-100 px-3 py-1 text-sm font-medium text-emerald-800">{rating.overallGrade}</span>
          </section>
        )}
        <AiRevision value={rating?.aiRevision} />
        <ErrorAnalysis items={rating?.errorSentences} suggestion={rating?.suggestions.join(' ')} />
      </aside>
    </div>
  )
}
