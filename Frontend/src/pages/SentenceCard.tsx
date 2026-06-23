import { RotateCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { AiRevision } from '../components/AiRevision'
import { ErrorAnalysis } from '../components/ErrorAnalysis'
import { SceneSelector } from '../components/SceneSelector'
import { ScoreCard } from '../components/ScoreCard'
import { StepNavigator } from '../components/StepNavigator'
import { useScoreDisplay } from '../hooks/useScoreDisplay'
import { useSentenceSession } from '../hooks/useSentenceSession'

export function SentenceCard() {
  const session = useSentenceSession()
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
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载造句题...</div>
  }

  if (session.error && !session.current) {
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

  if (!session.current) {
    return <section className="rounded-md border border-neutral-200 bg-white p-6">暂无造句题</section>
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
      <section className="grid gap-4 rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm text-neutral-500">第 {session.index + 1} / {session.total} 题</p>
            <h2 className="mt-1 text-2xl font-semibold">{session.current.targetWord}</h2>
          </div>
          <span className="rounded-md bg-neutral-100 px-3 py-2 text-sm font-medium text-neutral-700">{session.current.cefrLevel}</span>
        </div>

        <p className="rounded-md bg-neutral-50 p-4 text-sm leading-6 text-neutral-700">{session.current.content}</p>
        <SceneSelector value={scene} onChange={setScene} />

        <textarea
          value={sentence}
          onChange={(event) => setSentence(event.target.value)}
          disabled={session.submitting || Boolean(session.rating)}
          className="min-h-36 resize-y rounded-md border border-neutral-300 p-3 text-base leading-7 outline-none focus:border-emerald-700"
          placeholder={`Use "${session.current.targetWord}" in your own sentence.`}
        />

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            disabled={session.submitting || sentence.trim().length === 0 || Boolean(session.rating)}
            onClick={() => void session.submit(sentence, scene)}
            className="inline-flex h-11 items-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-neutral-300"
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
      </section>

      <aside className="grid content-start gap-4">
        {session.rating && (
          <section className="rounded-md border border-neutral-200 bg-white p-5">
            <p className="text-sm text-neutral-500">表达状态</p>
            <p className="mt-1 text-xl font-semibold">{score.label}</p>
          </section>
        )}
        <ScoreCard rating={session.rating} />
        <AiRevision value={session.rating?.aiRevision} />
        <ErrorAnalysis items={session.rating?.errorTags} suggestion={session.rating?.suggestion} />
      </aside>
    </div>
  )
}
