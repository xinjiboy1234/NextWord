import { RotateCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { AudioPlayer } from '../components/AudioPlayer'
import { ErrorHighlight } from '../components/ErrorHighlight'
import { SpellingInput } from '../components/SpellingInput'
import { StepNavigator } from '../components/StepNavigator'
import { useSpellingSession } from '../hooks/useSpellingSession'

export function SpellingMode() {
  const session = useSpellingSession()
  const [answer, setAnswer] = useState('')
  const [attempts, setAttempts] = useState(1)

  useEffect(() => {
    setAnswer('')
    setAttempts(1)
  }, [session.currentWord?.id])

  if (session.loading) {
    return <div className="card"><p className="text-sm" style={{ color: 'var(--muted)' }}>正在加载拼写队列...</p></div>
  }

  if (session.error && !session.currentWord) {
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

  if (!session.currentWord) {
    return <div className="card">暂无拼写任务</div>
  }

  return (
    <section className="stack stack-md">
      <div className="card">
          <div className="row-between" style={{ alignItems: 'flex-start' }}>
            <div>
              <p className="mono-label" style={{ textTransform: 'none' }}>第 {session.index + 1} / {session.total} 个</p>
              <h2 style={{ marginTop: 'var(--space-2)', fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
                {session.currentWord.meanings.join('；')}
              </h2>
              <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
                {session.currentWord.partOfSpeech} · {session.currentWord.phonetics}
              </p>
            </div>
            <AudioPlayer text={session.currentWord.lemma} />
          </div>

          <div style={{ marginTop: 'var(--space-6)' }}>
            <SpellingInput
              value={answer}
              disabled={session.submitting || Boolean(session.result)}
              onChange={setAnswer}
              onSubmit={() => void session.submit(answer, attempts)}
            />
          </div>

          {session.result && (
            <div style={{ marginTop: 'var(--space-4)', padding: 'var(--space-4)', background: 'var(--border-soft)', borderRadius: 'var(--radius-md)' }}>
              <ErrorHighlight answer={session.result.userSpelling} correct={session.result.correctSpelling} positions={session.result.errorPositions} />
            </div>
          )}

          <div className="row" style={{ marginTop: 'var(--space-4)', flexWrap: 'wrap' }}>
            {!session.result && (
              <button type="button" onClick={() => setAttempts((value) => value + 1)} className="btn btn-secondary">
                再想想
              </button>
            )}
            {session.result && (
              <StepNavigator
                index={session.index}
                total={session.total}
                onPrevious={session.prev}
                onNext={session.next}
                canPrevious={session.index > 0}
                canNext={session.index < session.total - 1}
                nextLabel="下一个"
              />
            )}
          </div>

          {session.result && (
            <div className={`alert ${session.result.isCorrect ? 'alert-success' : 'alert-error'}`} style={{ marginTop: 'var(--space-4)' }}>
              {session.result.isCorrect ? '已写入复习成功记录' : '已加入更高优先级复习'}
            </div>
          )}
        </div>
    </section>
  )
}
