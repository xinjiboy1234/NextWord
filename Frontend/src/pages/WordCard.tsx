import { RotateCcw } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { AnswerInput } from '../components/AnswerInput'
import { FeedbackArea } from '../components/FeedbackArea'
import { ProgressBar } from '../components/ProgressBar'
import { RatingButtons } from '../components/RatingButtons'
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
    <div className="grid-2-1">
      <div className="stack stack-md">
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
        <AnswerInput
          value={answer}
          onChange={setAnswer}
          onSubmit={() => void submit('Fuzzy')}
          disabled={learning.submitting || submitted}
        />

        {!submitted ? (
          <div className="card">
            <h3 className="mono-label" style={{ marginBottom: 'var(--space-3)', textTransform: 'none' }}>主观熟练度</h3>
            <RatingButtons disabled={learning.submitting} onRate={(rating) => void submit(rating)} />
          </div>
        ) : (
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
        )}
      </div>

      <aside className="stack stack-md">
        <FeedbackArea result={learning.result} error={learning.error} />
        <div className="side-panel">
          <h4 className="side-panel-title">本轮目标</h4>
          <ul className="stack stack-sm" style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)', paddingLeft: '1.1em' }}>
            <li>先回忆中文含义。</li>
            <li>提交后根据实际熟练度选择记住、模糊或不会。</li>
            <li>系统会写入日志并计算下次复习时间。</li>
          </ul>
        </div>
      </aside>
    </div>
  )
}
