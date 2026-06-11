import { CalendarClock, CheckCircle2, XCircle } from 'lucide-react'
import type { LearningResult } from '../types/models'

interface FeedbackAreaProps {
  result: LearningResult | null
  error: string | null
}

export function FeedbackArea({ result, error }: FeedbackAreaProps) {
  if (error) {
    return <div className="rounded-md border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">{error}</div>
  }

  if (!result) {
    return <div className="rounded-md border border-dashed border-neutral-300 bg-white p-4 text-sm text-neutral-600">提交答案后会显示释义、例句和下次复习时间。</div>
  }

  const Icon = result.isCorrect ? CheckCircle2 : XCircle
  return (
    <section className="rounded-md border border-neutral-200 bg-white p-5">
      <div className="flex items-center gap-2">
        <Icon size={20} className={result.isCorrect ? 'text-emerald-700' : 'text-rose-700'} aria-hidden="true" />
        <h3 className="text-base font-semibold">{result.isCorrect ? '回答正确' : '需要复习'}</h3>
      </div>
      <dl className="mt-4 grid gap-3 text-sm text-neutral-700">
        <div>
          <dt className="font-semibold text-neutral-950">释义</dt>
          <dd className="mt-1">{result.meanings.join('；')}</dd>
        </div>
        <div>
          <dt className="font-semibold text-neutral-950">例句</dt>
          <dd className="mt-1">{result.exampleSentences[0] ?? 'No example yet.'}</dd>
        </div>
        <div className="flex items-center gap-2 text-neutral-600">
          <CalendarClock size={18} aria-hidden="true" />
          <span>下次复习间隔 {result.intervalDays} 天，掌握度 {Math.round(result.masteryScore)}%</span>
        </div>
      </dl>
    </section>
  )
}
