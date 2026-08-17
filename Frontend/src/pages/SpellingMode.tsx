import { RotateCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AudioPlayer } from '../components/AudioPlayer'
import { ErrorHighlight } from '../components/ErrorHighlight'
import { ProgressBar } from '../components/ProgressBar'
import { SpellingInput } from '../components/SpellingInput'
import { StepNavigator } from '../components/StepNavigator'
import { Badge } from '../components/ui/Badge'
import { RadioGroup } from '../components/ui/RadioGroup'
import { useSpellingCount, SPELLING_COUNT_OPTIONS } from '../hooks/useSpellingCount'
import { useSpellingMode, SPELLING_MODE_OPTIONS } from '../hooks/useSpellingMode'
import { useSpellingSession } from '../hooks/useSpellingSession'

export function SpellingMode() {
  // T-062：空态「去学新词」引导跳转
  const navigate = useNavigate()
  // T-051：每组题量可选 8/12/16/20（默认 12，localStorage 持久化），改量在未作答时生效并重载队列
  const { spellingCount, setSpellingCount } = useSpellingCount()
  // T-052：队列模式 复习/新词/混合（默认混合，localStorage 持久化），同样未作答时生效并重载队列
  const { spellingMode, setSpellingMode } = useSpellingMode()
  const session = useSpellingSession(spellingCount, spellingMode)
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

  // T-051：末词提交后进入完成页（本次正确数 + 错词回顾）
  if (session.completed) {
    return (
      <div className="stack stack-md" style={{ maxWidth: 720, margin: '0 auto', width: '100%' }}>
        <div className="card celebration-card" style={{ textAlign: 'center' }}>
          <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>本次拼写完成</h2>
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)', marginTop: 'var(--space-2)' }}>
            共 {session.total} 词 · 正确 {session.correctCount} 词 · 拼错 {session.missedWords.length} 词
          </p>
          <button type="button" onClick={session.reload} className="btn btn-primary" style={{ marginTop: 'var(--space-5)' }}>
            <RotateCcw size={16} aria-hidden="true" />
            再来一组
          </button>
        </div>
        {session.missedWords.length > 0 && (
          <div className="card">
            <p className="mono-label" style={{ textTransform: 'none' }}>错词回顾</p>
            <div className="stack stack-sm" style={{ marginTop: 'var(--space-3)' }}>
              {session.missedWords.map((word) => (
                <div key={word.id} className="row-between" style={{ fontSize: 'var(--text-sm)' }}>
                  <span>
                    <strong>{word.lemma}</strong>{' '}
                    <span style={{ color: 'var(--muted)' }}>{word.meanings.join('；')}</span>
                  </span>
                  <AudioPlayer text={word.lemma} />
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    )
  }

  // T-052/T-062：队列为空时给行动引导（不再说「太棒了」误导）——先去学新词或换模式
  if (!session.currentWord) {
    return (
      <section className="stack stack-md" style={{ maxWidth: 720, margin: '0 auto', width: '100%' }}>
        <div className="card" style={{ textAlign: 'center', padding: 'var(--space-8) var(--space-4)' }}>
          <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>暂无拼写任务</p>
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)', marginTop: 'var(--space-2)' }}>
            当前模式下没有可拼写的词：可能是新词还没开始学，或到期复习词都已完成。
          </p>
          <div className="stack stack-sm" style={{ marginTop: 'var(--space-5)', maxWidth: 320, marginLeft: 'auto', marginRight: 'auto' }}>
            <button type="button" className="btn btn-primary" onClick={() => navigate('/learn')}>
              去学新词
            </button>
            <button type="button" className="btn btn-secondary" onClick={session.reload}>
              重新加载队列
            </button>
          </div>
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-xs)', marginTop: 'var(--space-4)' }}>
            也可以在上方把队列模式切换为「复习」或「新词」再试。
          </p>
        </div>
      </section>
    )
  }

  return (
    <section className="stack stack-md" style={{ maxWidth: 720, margin: '0 auto', width: '100%' }}>
      <div className="card" style={{ padding: 'var(--space-4)' }}>
        <div className="row-between" style={{ marginBottom: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          <span className="mono-label" style={{ textTransform: 'none', letterSpacing: 0 }}>
            第 {session.index + 1} / {session.total} 个
          </span>
          <span>{session.progress}%</span>
        </div>
        <ProgressBar value={session.progress} />
        {session.answeredCount === 0 && (
          <div className="stack stack-sm" style={{ marginTop: 'var(--space-3)' }}>
            <div className="row-between" style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
              <span>每组题量</span>
              <RadioGroup
                name="spelling-count"
                value={String(spellingCount)}
                onValueChange={(value) => setSpellingCount(Number(value))}
                options={SPELLING_COUNT_OPTIONS.map((option) => ({ value: String(option), label: `${option} 题` }))}
              />
            </div>
            <div className="row-between" style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
              <span>队列模式</span>
              <RadioGroup
                name="spelling-mode"
                value={spellingMode}
                onValueChange={setSpellingMode}
                options={SPELLING_MODE_OPTIONS}
              />
            </div>
          </div>
        )}
      </div>

      <div className="card">
          <div className="row-between" style={{ alignItems: 'flex-start' }}>
            <div>
              <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
                {session.currentWord.meanings.join('；')}
              </h2>
              <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
                {session.currentWord.partOfSpeech} · {session.currentWord.phonetics}{' '}
                <Badge variant={session.currentWord.isReview ? 'info' : 'muted'}>
                  {session.currentWord.isReview ? '复习' : '新词'}
                </Badge>
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
                nextLabel={session.index < session.total - 1 ? '下一个' : '完成'}
                showProgress={false}
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
