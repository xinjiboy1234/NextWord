import { Badge } from './ui/Badge'
import type { DailyWordItem } from '../types/score'

interface WordDisplayProps {
  word: DailyWordItem
}

/** T-014：生命周期阶段标识（认识→回忆→会用→毕业）。 */
export const STAGE_LABELS: Record<string, string> = {
  recognized: '认识',
  recalled: '回忆',
  prompted_use: '会用',
  spontaneous_use: '毕业',
}

export function stageLabel(stage?: string) {
  return (stage && STAGE_LABELS[stage]) || '认识'
}

export function WordDisplay({ word }: WordDisplayProps) {
  return (
    <section className="word-card" style={{ padding: 'var(--space-12) var(--space-8)', minHeight: 280 }}>
      <p className="word-pos">
        {word.isWeak ? '薄弱词' : word.isExposure ? '接触词 · 认识即可' : '今日新词'} · 难度 {word.effectiveDifficulty}
      </p>
      <h2 className="word-word">{word.lemma}</h2>
      <p className="word-phonetic">{word.phonetics || '暂无音标'}</p>
      <div style={{ marginTop: 'var(--space-4)' }}>
        <Badge variant="muted">{stageLabel(word.stage)} · 看词知义</Badge>{' '}
        {word.fromPlan ? <Badge variant="info">来自今日计划</Badge> : null}
        {word.isWeak ? <Badge variant="info">优先复习</Badge> : null}
        {!word.fromPlan && !word.isWeak ? <Badge variant="muted">Score 选词</Badge> : null}
      </div>
    </section>
  )
}
