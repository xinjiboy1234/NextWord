import { BookOpen, RefreshCcw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { Word } from '../types/models'

interface HomeProps {
  onStart: () => void
}

export function Home({ onStart }: HomeProps) {
  const [words, setWords] = useState<Word[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const response = await api.get<Word[]>(endpoints.words)
      setWords(response.data)
    } catch {
      setError('词库加载失败。')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-2xl font-semibold">核心词库</h2>
            <p className="mt-1 text-sm text-neutral-600">MVP 种子词汇和后续新增词会显示在这里。</p>
          </div>
          <button
            type="button"
            onClick={onStart}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white"
          >
            <BookOpen size={18} aria-hidden="true" />
            开始学习
          </button>
        </div>

        {error && <p className="mt-4 rounded-md bg-rose-50 p-3 text-sm text-rose-900">{error}</p>}
        {loading ? (
          <p className="mt-6 text-sm text-neutral-600">加载中...</p>
        ) : (
          <div className="mt-5 grid gap-3">
            {words.map((word) => (
              <article key={word.id} className="rounded-md border border-neutral-200 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h3 className="text-lg font-semibold">{word.lemma}</h3>
                    <p className="text-sm text-neutral-600">{word.partOfSpeech} · {word.phonetics}</p>
                  </div>
                  <span className="rounded border border-neutral-200 px-2 py-1 text-xs font-medium text-neutral-700">
                    {word.difficultyLevel} / {word.cefrLevel}
                  </span>
                </div>
                <p className="mt-3 text-sm text-neutral-700">{word.meanings.join('；')}</p>
              </article>
            ))}
          </div>
        )}
      </section>

      <aside className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">词库操作</h2>
          <button
            type="button"
            onClick={() => void load()}
            className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-neutral-200 text-neutral-700"
            aria-label="刷新词库"
          >
            <RefreshCcw size={18} aria-hidden="true" />
          </button>
        </div>
        <dl className="mt-4 grid gap-3 text-sm">
          <div className="flex justify-between border-b border-neutral-100 pb-2">
            <dt className="text-neutral-600">总词数</dt>
            <dd className="font-semibold">{words.length}</dd>
          </div>
          <div className="flex justify-between border-b border-neutral-100 pb-2">
            <dt className="text-neutral-600">核心词</dt>
            <dd className="font-semibold">{words.filter((word) => word.isCore).length}</dd>
          </div>
        </dl>
      </aside>
    </div>
  )
}
