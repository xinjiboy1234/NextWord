import { Badge } from './ui/Badge'

interface WritingScoreBadgeProps {
  before?: number | null
  after?: number | null
}

/** T-022：练习评分后 Writing 分变化徽标；无变化或字段为空不显示。 */
export function WritingScoreBadge({ before, after }: WritingScoreBadgeProps) {
  if (before == null || after == null || after === before) {
    return null
  }

  const delta = after - before
  const sign = delta > 0 ? '+' : ''
  return (
    <Badge variant={delta > 0 ? 'success' : 'warn'}>
      写作 {before}→{after}（{sign}{delta}）
    </Badge>
  )
}
