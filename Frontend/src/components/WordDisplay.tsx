import type { Word } from '../types/models'

interface WordDisplayProps {
  word: Word
}

export function WordDisplay({ word }: WordDisplayProps) {
  return (
    <section className="rounded-md border border-neutral-200 bg-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-neutral-500">{word.partOfSpeech} · {word.cefrLevel}</p>
          <h2 className="mt-2 text-5xl font-semibold tracking-normal text-neutral-950">{word.lemma}</h2>
          <p className="mt-2 text-lg text-neutral-600">{word.phonetics || 'No phonetics yet'}</p>
        </div>
        <span className="rounded border border-emerald-200 bg-emerald-50 px-3 py-1 text-sm font-medium text-emerald-800">
          {word.difficultyLevel}
        </span>
      </div>
    </section>
  )
}
