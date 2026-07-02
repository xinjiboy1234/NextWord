import { Badge } from './ui/Badge'
import type { DailyWordItem } from '../types/score'

interface WordDisplayProps {
  word: DailyWordItem
}

export function WordDisplay({ word }: WordDisplayProps) {
  return (
    <section className="word-card" style={{ padding: 'var(--space-12) var(--space-8)', minHeight: 280 }}>
      <p className="word-pos">
        {word.isWeak ? '薄弱词' : '今日新词'} · 难度 {word.effectiveDifficulty}
      </p>
      <h2 className="word-word">{word.lemma}</h2>
      <p className="word-phonetic">{word.phonetics || '暂无音标'}</p>
      <div style={{ marginTop: 'var(--space-4)' }}>
        {word.isWeak ? <Badge variant="info">优先复习</Badge> : <Badge variant="muted">Score 选词</Badge>}
      </div>
    </section>
  )
}
