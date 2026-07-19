import { RotateCcw } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { AnswerInput } from '../components/AnswerInput'
import { FeedbackArea } from '../components/FeedbackArea'
import { ProgressBar } from '../components/ProgressBar'
import { StepNavigator } from '../components/StepNavigator'
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

  function normalizeAnswer(value: string) {
    return value.trim().toLowerCase().replaceAll('，', ',').replaceAll('。', '.')
  }

  function isAnswerCorrect(userAnswer: string, meanings: string[]) {
    const normalized = normalizeAnswer(userAnswer)
    if (!normalized) return false
    return meanings.some((meaning) => {
      const normalizedMeaning = normalizeAnswer(meaning)
      return normalizedMeaning.includes(normalized) || normalized.includes(normalizedMeaning)
    })
  }

  async function submitAnswer() {
    if (!session.currentWord || answer.trim().length === 0) return
    const rating: AssessmentResult = isAnswerCorrect(answer, session.currentWord.meanings) ? 'Remembered' : 'Forgot'
    const elapsed = Date.now() - startedAt.current
    const result = await learning.submit(session.currentWord.id, answer, rating, elapsed)
    if (result) {
      setSubmitted(true)
    }
  }

  async function markForgot() {
    if (!session.currentWord || learning.submitting || submitted) return
    const elapsed = Date.now() - startedAt.current
    const result = await learning.submit(session.currentWord.id, '', 'Forgot', elapsed)
    if (result) {
      setSubmitted(true)
    }
  }

  function nextWord() {
    session.next()
  }

  if (session.loading) {
    return <div className="card"><p className="text-sm" style={{ color: 'var(--muted)' }}>正在加载今日单词...</p></div>
  }

  if (session.error) {
    return (
      <div className="alert alert-error">
        <p>{session.error}</p>
        <button type="button" onClick={session.reload} className="btn btn-primary btn-sm" style={{ marginTop: 'var(--space-3)' }}>
          <RotateCcw size={16} aria-hidden="true" />
          重试
        </button>
      </div>
    )
  }

  if (session.completed || !session.currentWord) {
    return (
      <div className="card celebration-card" style={{ textAlign: 'center' }}>
        <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>今日新词完成</h2>
        <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)', marginTop: 'var(--space-2)' }}>
          当前没有更多新词。可以刷新词库或等待复习队列。
        </p>
        <button type="button" onClick={session.reload} className="btn btn-primary" style={{ marginTop: 'var(--space-5)' }}>
          <RotateCcw size={16} aria-hidden="true" />
          重新加载
        </button>
      </div>
    )
  }

  return (
    <div className="stack stack-md" style={{ maxWidth: 720, margin: '0 auto', width: '100%' }}>
      <div className="card" style={{ padding: 'var(--space-4)' }}>
        <div className="row-between" style={{ marginBottom: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          <span className="mono-label" style={{ textTransform: 'none', letterSpacing: 0 }}>
            第 {session.index + 1} / {session.total} 个
          </span>
          <span>{session.progress}%</span>
        </div>
        <ProgressBar value={session.progress} />
      </div>

      <WordDisplay word={session.currentWord} />

      {!submitted ? (
        <AnswerInput
          value={answer}
          onChange={setAnswer}
          onSubmit={() => void submitAnswer()}
          onForgot={() => void markForgot()}
          disabled={learning.submitting}
        />
      ) : (
        <>
          <FeedbackArea result={learning.result} error={learning.error} />
          <div className="card">
            <StepNavigator
              index={session.index}
              total={session.total}
              onPrevious={session.prev}
              onNext={nextWord}
              canPrevious={session.index > 0}
              canNext={session.index < session.total - 1}
              nextLabel="下一个"
            />
          </div>
        </>
      )}
    </div>
  )
}
