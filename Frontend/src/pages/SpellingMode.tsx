import { ArrowRight, RotateCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { AudioPlayer } from '../components/AudioPlayer'
import { ErrorHighlight } from '../components/ErrorHighlight'
import { SpellingInput } from '../components/SpellingInput'
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
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载拼写队列...</div>
  }

  if (session.error && !session.currentWord) {
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

  if (!session.currentWord) {
    return <section className="rounded-md border border-neutral-200 bg-white p-6">暂无拼写任务</section>
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
      <section className="grid gap-4 rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-sm text-neutral-500">第 {session.index + 1} / {session.total} 个</p>
            <h2 className="mt-2 text-2xl font-semibold">{session.currentWord.meanings.join('；')}</h2>
            <p className="mt-2 text-sm text-neutral-600">{session.currentWord.partOfSpeech} · {session.currentWord.phonetics}</p>
          </div>
          <AudioPlayer text={session.currentWord.lemma} />
        </div>

        <SpellingInput
          value={answer}
          disabled={session.submitting || Boolean(session.result)}
          onChange={setAnswer}
          onSubmit={() => void session.submit(answer, attempts)}
        />

        {session.result && (
          <section className="rounded-md border border-neutral-200 bg-neutral-50 p-4">
            <ErrorHighlight answer={session.result.userSpelling} correct={session.result.correctSpelling} positions={session.result.errorPositions} />
          </section>
        )}

        <div className="flex flex-wrap gap-2">
          {!session.result && (
            <button type="button" onClick={() => setAttempts((value) => value + 1)} className="h-11 rounded-md border border-neutral-200 bg-white px-4 text-sm font-semibold text-neutral-700 hover:bg-neutral-100">
              再想想
            </button>
          )}
          {session.result && (
            <button type="button" onClick={session.next} className="inline-flex h-11 items-center gap-2 rounded-md bg-neutral-950 px-4 text-sm font-semibold text-white">
              下一个
              <ArrowRight size={18} aria-hidden="true" />
            </button>
          )}
        </div>
      </section>

      <aside className="grid content-start gap-4">
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h3 className="text-base font-semibold">例句</h3>
          <ul className="mt-3 grid gap-2 text-sm leading-6 text-neutral-700">
            {session.currentWord.exampleSentences.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>
        {session.result && (
          <section className={`rounded-md border p-5 ${session.result.isCorrect ? 'border-emerald-200 bg-emerald-50' : 'border-rose-200 bg-rose-50'}`}>
            <p className={`text-sm font-semibold ${session.result.isCorrect ? 'text-emerald-800' : 'text-rose-800'}`}>
              {session.result.isCorrect ? '已写入复习成功记录' : '已加入更高优先级复习'}
            </p>
          </section>
        )}
      </aside>
    </div>
  )
}
