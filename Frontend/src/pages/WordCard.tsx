import { RotateCcw } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { AnswerInput } from '../components/AnswerInput'
import { FeedbackArea } from '../components/FeedbackArea'
import { ProgressBar } from '../components/ProgressBar'
import { RatingButtons } from '../components/RatingButtons'
import { WordDisplay } from '../components/WordDisplay'
import { useLearningLog } from '../hooks/useLearningLog'
import { useWordSession } from '../hooks/useWordSession'
import type { AssessmentResult } from '../types/models'

export function WordCard() {
  const session = useWordSession()
  const learning = useLearningLog()
  const [answer, setAnswer] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const startedAt = useRef(Date.now())

  useEffect(() => {
    startedAt.current = Date.now()
    setAnswer('')
    setSubmitted(false)
    learning.reset()
  }, [session.currentWord?.id])

  async function submit(rating: AssessmentResult = 'Fuzzy') {
    if (!session.currentWord || answer.trim().length === 0) return
    const elapsed = Date.now() - startedAt.current
    const result = await learning.submit(session.currentWord.id, answer, rating, elapsed)
    if (result) {
      setSubmitted(true)
    }
  }

  function nextWord() {
    session.next()
  }

  if (session.loading) {
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载今日单词...</div>
  }

  if (session.error) {
    return (
      <section className="rounded-md border border-rose-200 bg-rose-50 p-6">
        <p className="text-sm text-rose-900">{session.error}</p>
        <button type="button" onClick={session.reload} className="mt-4 inline-flex h-11 items-center gap-2 rounded-md bg-rose-700 px-4 text-sm font-semibold text-white">
          <RotateCcw size={18} aria-hidden="true" />
          重试
        </button>
      </section>
    )
  }

  if (session.completed || !session.currentWord) {
    return (
      <section className="rounded-md border border-neutral-200 bg-white p-6">
        <h2 className="text-2xl font-semibold">今日新词完成</h2>
        <p className="mt-2 text-sm text-neutral-600">当前没有更多新词。可以刷新词库或等待复习队列。</p>
        <button type="button" onClick={session.reload} className="mt-5 inline-flex h-11 items-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white">
          <RotateCcw size={18} aria-hidden="true" />
          重新加载
        </button>
      </section>
    )
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
      <div className="grid gap-4">
        <div className="rounded-md border border-neutral-200 bg-white p-4">
          <div className="mb-3 flex items-center justify-between text-sm text-neutral-600">
            <span>第 {session.index + 1} / {session.total} 个</span>
            <span>{session.progress}%</span>
          </div>
          <ProgressBar value={session.progress} />
        </div>

        <WordDisplay word={session.currentWord} />
        <AnswerInput value={answer} onChange={setAnswer} onSubmit={() => void submit('Fuzzy')} disabled={learning.submitting || submitted} />

        {!submitted ? (
          <div className="rounded-md border border-neutral-200 bg-white p-5">
            <h3 className="mb-3 text-sm font-semibold text-neutral-800">主观熟练度</h3>
            <RatingButtons disabled={learning.submitting} onRate={(rating) => void submit(rating)} />
          </div>
        ) : (
          <div className="rounded-md border border-neutral-200 bg-white p-5">
            <button
              type="button"
              onClick={nextWord}
              className="h-11 rounded-md bg-neutral-950 px-4 text-sm font-semibold text-white"
            >
              下一个
            </button>
          </div>
        )}
      </div>

      <aside className="grid content-start gap-4">
        <FeedbackArea result={learning.result} error={learning.error} />
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h3 className="text-base font-semibold">本轮目标</h3>
          <ul className="mt-3 grid gap-2 text-sm text-neutral-700">
            <li>先回忆中文含义。</li>
            <li>提交后根据实际熟练度选择记住、模糊或不会。</li>
            <li>系统会写入日志并计算下次复习时间。</li>
          </ul>
        </section>
      </aside>
    </div>
  )
}
