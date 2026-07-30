import { RotateCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { AiRevision } from '../components/AiRevision'
import { Badge } from '../components/ui/Badge'
import { ErrorAnalysis } from '../components/ErrorAnalysis'
import { SceneSelector } from '../components/SceneSelector'
import { ScoreCard } from '../components/ScoreCard'
import { StepNavigator } from '../components/StepNavigator'
import { WritingScoreBadge } from '../components/WritingScoreBadge'
import { useScoreDisplay } from '../hooks/useScoreDisplay'
import { useSentenceSession } from '../hooks/useSentenceSession'

interface SentenceCardProps {
  userLevel?: string
}

export function SentenceCard({ userLevel = 'A2' }: SentenceCardProps) {
  const session = useSentenceSession(userLevel)
  const [scene, setScene] = useState('life')
  const [sentence, setSentence] = useState('')
  const score = useScoreDisplay(session.rating)

  useEffect(() => {
    setSentence('')
    if (session.current?.scene) {
      setScene(session.current.scene)
    }
  }, [session.current?.id, session.current?.scene])

  if (session.loading) {
    return <div className="card"><p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)' }}>正在加载造句题...</p></div>
  }

  if (session.error && !session.current) {
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

  if (!session.current) {
    return <div className="card">暂无造句题</div>
  }

  return (
    <div className="grid-2-1">
      <div className="stack stack-md">
        <div className="card">
          <div className="row-between" style={{ alignItems: 'flex-start' }}>
            <div>
              <p className="mono-label" style={{ textTransform: 'none' }}>第 {session.index + 1} / {session.total} 题</p>
              <h2 style={{ marginTop: 'var(--space-1)', fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
                {session.current.targetWord}
              </h2>
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
              {session.current.fromPlan ? <Badge variant="info">来自今日计划</Badge> : null}
              <Badge variant="muted">{session.current.cefrLevel}</Badge>
            </div>
          </div>

          {session.current.content ? (
            <p style={{ marginTop: 'var(--space-4)', padding: 'var(--space-4)', background: 'var(--border-soft)', borderRadius: 'var(--radius-md)', fontSize: 'var(--text-sm)', lineHeight: 1.6 }}>
              {session.current.content}
            </p>
          ) : null}

          <div style={{ marginTop: 'var(--space-4)' }}>
            <SceneSelector value={scene} onChange={setScene} />
          </div>

          <textarea
            value={sentence}
            onChange={(event) => setSentence(event.target.value)}
            disabled={session.submitting || Boolean(session.rating)}
            className="textarea"
            style={{ marginTop: 'var(--space-4)', minHeight: 140 }}
            placeholder={`用 "${session.current.targetWord}" 造一个英文句子`}
            autoComplete="off"
          />

          <div className="row" style={{ marginTop: 'var(--space-4)', flexWrap: 'wrap' }}>
            <button
              type="button"
              disabled={session.submitting || sentence.trim().length === 0 || Boolean(session.rating)}
              onClick={() => void session.submit(sentence, scene)}
              className="btn btn-primary"
            >
              提交评分
            </button>
            {session.rating && (
              <StepNavigator
                index={session.index}
                total={session.total}
                onPrevious={session.prev}
                onNext={session.next}
                canPrevious={session.index > 0}
                canNext={session.index < session.total - 1}
                nextLabel="下一题"
                showProgress={false}
              />
            )}
          </div>
        </div>
      </div>

      <aside className="stack stack-md">
        {session.rating && (
          <div className="side-panel">
            <p className="mono-label" style={{ textTransform: 'none' }}>表达状态</p>
            <p style={{ marginTop: 'var(--space-2)', fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
              {score.label}
            </p>
            <div style={{ marginTop: 'var(--space-2)' }}>
              <WritingScoreBadge before={session.rating.writingScoreBefore} after={session.rating.writingScoreAfter} />
            </div>
          </div>
        )}
        <ScoreCard rating={session.rating} />
        <AiRevision value={session.rating?.aiRevision} />
        <ErrorAnalysis items={session.rating?.errorTags} suggestion={session.rating?.suggestion} />
      </aside>
    </div>
  )
}
