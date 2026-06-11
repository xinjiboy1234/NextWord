import type { SentenceRating } from '../types/sentence'

interface ScoreCardProps {
  rating: SentenceRating | null
}

const scoreLabels = [
  ['grammarScore', '语法'],
  ['naturalScore', '自然度'],
  ['vocabularyScore', '用词'],
  ['relevanceScore', '契合度'],
] as const

export function ScoreCard({ rating }: ScoreCardProps) {
  if (!rating) {
    return null
  }

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-5">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold">评分</h3>
        <span className="grid h-10 w-10 place-items-center rounded-md bg-emerald-700 text-lg font-semibold text-white">{rating.overallGrade}</span>
      </div>
      <div className="mt-4 grid gap-3">
        {scoreLabels.map(([key, label]) => (
          <div key={key} className="grid gap-1">
            <div className="flex justify-between text-sm">
              <span className="text-neutral-700">{label}</span>
              <span className="font-medium">{rating[key]} / 5</span>
            </div>
            <div className="h-2 rounded-full bg-neutral-100">
              <div className="h-2 rounded-full bg-emerald-700" style={{ width: `${rating[key] * 20}%` }} />
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}
