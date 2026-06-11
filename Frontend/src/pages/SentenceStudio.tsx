import { PenLine, TextCursorInput } from 'lucide-react'
import { useState } from 'react'
import { FreeExpression } from './FreeExpression'
import { SentenceCard } from './SentenceCard'

type SentenceMode = 'targeted' | 'free'

export function SentenceStudio() {
  const [mode, setMode] = useState<SentenceMode>('targeted')

  return (
    <div className="grid gap-5">
      <section className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">造句训练</h2>
          <p className="mt-1 text-sm text-neutral-600">指定词造句与自由表达。</p>
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => setMode('targeted')}
            className={`inline-flex h-10 items-center gap-2 rounded-md border px-3 text-sm font-medium ${mode === 'targeted' ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100'}`}
          >
            <PenLine size={16} aria-hidden="true" />
            指定词
          </button>
          <button
            type="button"
            onClick={() => setMode('free')}
            className={`inline-flex h-10 items-center gap-2 rounded-md border px-3 text-sm font-medium ${mode === 'free' ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100'}`}
          >
            <TextCursorInput size={16} aria-hidden="true" />
            自由表达
          </button>
        </div>
      </section>
      {mode === 'targeted' ? <SentenceCard /> : <FreeExpression />}
    </div>
  )
}
