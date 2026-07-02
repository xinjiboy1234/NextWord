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
    <div className="side-panel">
      <div className="row-between" style={{ marginBottom: 'var(--space-4)' }}>
        <h3 style={{ fontWeight: 540 }}>评分</h3>
        <span className="score-card" style={{ padding: 'var(--space-2) var(--space-3)' }}>
          <span className="score-value" style={{ fontSize: 'var(--text-xl)' }}>{rating.overallGrade}</span>
        </span>
      </div>
      <div className="stack stack-sm">
        {scoreLabels.map(([key, label]) => (
          <div key={key}>
            <div className="row-between" style={{ fontSize: 'var(--text-sm)', marginBottom: 4 }}>
              <span>{label}</span>
              <span style={{ fontFamily: 'var(--font-mono)' }}>{rating[key]} / 5</span>
            </div>
            <div className="progress-bar">
              <div className="progress-bar-fill brand" style={{ width: `${rating[key] * 20}%` }} />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
