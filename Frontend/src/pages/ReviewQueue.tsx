import { RefreshCw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { StepNavigator } from '../components/StepNavigator'
import type { Word } from '../types/models'
import type { LogSummary, RecentLog } from '../types/sentence'

export function ReviewQueue() {
  const [summary, setSummary] = useState<LogSummary | null>(null)
  const [words, setWords] = useState<Word[]>([])
  const [logs, setLogs] = useState<RecentLog[]>([])
  const [loading, setLoading] = useState(true)
  const [index, setIndex] = useState(0)
  const [revealed, setRevealed] = useState(false)

  async function load() {
    setLoading(true)
    const [summaryResponse, wordsResponse, logsResponse] = await Promise.all([
      api.get<LogSummary>(endpoints.logSummary),
      api.get<Word[]>(endpoints.spellingQueue, { params: { count: 10 } }),
      api.get<RecentLog[]>(endpoints.recentLogs, { params: { count: 10 } }),
    ])
    setSummary(summaryResponse.data)
    setWords(wordsResponse.data)
    setLogs(logsResponse.data)
    setIndex(0)
    setRevealed(false)
    setLoading(false)
  }

  useEffect(() => {
    void load()
  }, [])

  useEffect(() => {
    setRevealed(false)
  }, [index])

  if (loading) {
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载复习队列...</div>
  }

  const currentWord = words[index] ?? null

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_340px]">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-2xl font-semibold">今日复习</h2>
            <p className="mt-1 text-sm text-neutral-600">{summary?.dueReviews ?? 0} 个词到期，逐词复习。</p>
          </div>
          <button type="button" onClick={() => void load()} title="刷新" className="grid h-10 w-10 place-items-center rounded-md border border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100">
            <RefreshCw size={18} aria-hidden="true" />
          </button>
        </div>

        {!currentWord ? (
          <p className="mt-5 text-sm text-neutral-600">暂无到期复习词。</p>
        ) : (
          <div className="mt-5 space-y-4">
            <article className="rounded-md border border-neutral-200 p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h3 className="text-2xl font-semibold">{currentWord.lemma}</h3>
                <span className="rounded-md bg-neutral-100 px-2 py-1 text-xs font-medium text-neutral-600">{currentWord.cefrLevel}</span>
              </div>
              <p className="mt-2 text-sm text-neutral-500">{currentWord.partOfSpeech} · {currentWord.phonetics}</p>
              {revealed ? (
                <p className="mt-4 text-base text-neutral-800">{currentWord.meanings.join('；')}</p>
              ) : (
                <button
                  type="button"
                  onClick={() => setRevealed(true)}
                  className="mt-4 inline-flex h-10 items-center rounded-md border border-neutral-200 bg-white px-4 text-sm font-medium text-neutral-700 hover:bg-neutral-100"
                >
                  显示释义
                </button>
              )}
            </article>
            <StepNavigator
              index={index}
              total={words.length}
              onPrevious={() => setIndex((value) => Math.max(0, value - 1))}
              onNext={() => setIndex((value) => Math.min(value + 1, words.length - 1))}
              canPrevious={index > 0}
              canNext={index < words.length - 1}
            />
          </div>
        )}
      </section>

      <aside className="grid content-start gap-4">
        {summary && (
          <section className="grid grid-cols-2 gap-3 rounded-md border border-neutral-200 bg-white p-5">
            <Metric label="造句" value={summary.sentenceCount} />
            <Metric label="自由表达" value={summary.freeExpressionCount} />
            <Metric label="拼写" value={summary.spellingCount} />
            <Metric label="正确率" value={`${summary.spellingAccuracyPercent}%`} />
          </section>
        )}
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h3 className="text-base font-semibold">最近记录</h3>
          <div className="mt-3 grid gap-2">
            {logs.map((log) => (
              <div key={`${log.type}-${log.label}-${log.timestamp}`} className="flex items-center justify-between gap-3 rounded-md bg-neutral-50 px-3 py-2 text-sm">
                <span className="font-medium text-neutral-800">{log.label}</span>
                <span className="text-neutral-500">{log.result}</span>
              </div>
            ))}
          </div>
        </section>
      </aside>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-md bg-neutral-50 p-3">
      <p className="text-xs text-neutral-500">{label}</p>
      <p className="mt-1 text-xl font-semibold">{value}</p>
    </div>
  )
}
