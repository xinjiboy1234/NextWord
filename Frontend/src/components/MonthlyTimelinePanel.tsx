import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { NATURE_META } from '../hooks/useBottleneckInsight'
import type { UserProfileScores } from '../types/score'
import type { MonthlyTimeline, MonthlyTimelineEvent, ScoreHistorySnapshot } from '../types/timeline'
import { Badge } from './ui/Badge'

/** T-030 同款口径：等级变更原因 → 中文（避免英文外露） */
const LEVEL_CHANGE_REASON_LABELS: Record<string, string> = {
  Initial: '首次定级',
  Upgrade: '等级提升',
  Rollback: '等级回调',
}

const DIMENSION_LABELS: Record<string, string> = {
  scenario: '场景',
  skill: '技能',
  reading: '阅读',
}

const POLARITY_BADGE: Record<string, { label: string; variant: 'success' | 'warn' | 'muted' }> = {
  strength: { label: '强项', variant: 'success' },
  weakness: { label: '待提升', variant: 'warn' },
  neutral: { label: '中性', variant: 'muted' },
}

/** 趋势图三维（视觉权重刻意压低：细线、低饱和、无网格大标题） */
const TREND_DIMS = [
  { key: 'vocabulary', label: '词汇', color: 'var(--info)' },
  { key: 'reading', label: '阅读', color: 'var(--success)' },
  { key: 'writing', label: '写作', color: 'var(--warn)' },
] as const

type TrendDimKey = (typeof TREND_DIMS)[number]['key']

interface TrendPoint {
  date: string
  value: number
}

function formatMonthDay(iso: string): string {
  const date = new Date(iso)
  return `${date.getMonth() + 1}月${date.getDate()}日`
}

function eventText(event: MonthlyTimelineEvent): string {
  switch (event.type) {
    case 'word_graduation':
      return `「${event.word}」毕业——已能自发使用`
    case 'challenge_first_pass':
      return `首次通过 ${event.level} 挑战`
    case 'level_change': {
      const reason = LEVEL_CHANGE_REASON_LABELS[event.reason ?? ''] ?? event.reason ?? '等级变更'
      return `${reason}：${event.fromLevel} → ${event.toLevel}`
    }
    case 'profile_generated':
      return '能力画像更新'
    default:
      return ''
  }
}

function eventBadge(event: MonthlyTimelineEvent): { label: string; variant: 'success' | 'info' | 'muted' } {
  switch (event.type) {
    case 'word_graduation':
      return { label: '毕业', variant: 'success' }
    case 'challenge_first_pass':
      return { label: '挑战', variant: 'info' }
    case 'level_change':
      return { label: '等级', variant: 'info' }
    default:
      return { label: '画像', variant: 'muted' }
  }
}

function parseTrend(snapshots: ScoreHistorySnapshot[]): Record<TrendDimKey, TrendPoint[]> {
  const series: Record<TrendDimKey, TrendPoint[]> = { vocabulary: [], reading: [], writing: [] }
  const sorted = [...snapshots].sort((a, b) => a.date.localeCompare(b.date))
  for (const snapshot of sorted) {
    let scores: Partial<UserProfileScores>
    try {
      scores = JSON.parse(snapshot.scoresJson) as Partial<UserProfileScores>
    } catch {
      continue
    }
    for (const dim of TREND_DIMS) {
      const value = scores[dim.key]
      if (typeof value === 'number') {
        series[dim.key].push({ date: snapshot.date, value })
      }
    }
  }
  return series
}

/** 轻量自绘 SVG 折线（T-036：不引图表库）；数据不足 7 天由调用方显示空态 */
function ScoreTrendChart({ series }: { series: Record<TrendDimKey, TrendPoint[]> }) {
  const { paths, firstDate, lastDate } = useMemo(() => {
    const all = TREND_DIMS.flatMap((dim) => series[dim.key])
    const values = all.map((point) => point.value)
    const min = Math.min(...values)
    const max = Math.max(...values)
    const span = Math.max(max - min, 10)
    const low = min - span * 0.15
    const high = max + span * 0.15
    const width = 600
    const height = 120
    const pad = 8

    const toPath = (points: TrendPoint[]) => {
      if (points.length === 0) return ''
      const step = points.length > 1 ? (width - pad * 2) / (points.length - 1) : 0
      return points
        .map((point, index) => {
          const x = pad + step * index
          const y = height - pad - ((point.value - low) / (high - low)) * (height - pad * 2)
          return `${index === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`
        })
        .join(' ')
    }

    return {
      paths: TREND_DIMS.map((dim) => ({ key: dim.key, color: dim.color, d: toPath(series[dim.key]) })),
      firstDate: all[0]?.date ?? '',
      lastDate: all[all.length - 1]?.date ?? '',
    }
  }, [series])

  return (
    <div>
      <svg viewBox="0 0 600 120" role="img" aria-label="近 30 天分数趋势" className="trend-chart">
        {paths.map((line) =>
          line.d ? (
            <path key={line.key} d={line.d} fill="none" stroke={line.color} strokeWidth={1.5} strokeLinejoin="round" />
          ) : null,
        )}
      </svg>
      <div className="trend-legend">
        {TREND_DIMS.map((dim) => (
          <span key={dim.key}>
            <span className="trend-dot" style={{ background: dim.color }} />
            {dim.label}
          </span>
        ))}
        <span style={{ marginLeft: 'auto' }}>
          {formatMonthDay(firstDate)} – {formatMonthDay(lastDate)}
        </span>
      </div>
    </div>
  )
}

/**
 * T-036「我的这个月」月度时间轴（DESIGN-monthly-timeline §2）：
 * 分数趋势（轻量）+ 本月里程碑 + 画像变化 + 洞察回放，全部消费只读数据；
 * 引导语强调「你会用的词越来越多」而非分数涨跌（分数是外壳纪律）。
 */
export function MonthlyTimelinePanel() {
  const [timeline, setTimeline] = useState<MonthlyTimeline | null>(null)
  const [snapshots, setSnapshots] = useState<ScoreHistorySnapshot[] | null>(null)
  const [timelineError, setTimelineError] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false

    async function load() {
      const [timelineResult, historyResult] = await Promise.allSettled([
        api.get<MonthlyTimeline>(`${endpoints.profileMonthlyTimeline}?days=30`),
        api.get<ScoreHistorySnapshot[]>(`${endpoints.profileScoresHistory}?days=30`),
      ])
      if (cancelled) return
      if (timelineResult.status === 'fulfilled') {
        setTimeline(timelineResult.value.data)
      } else {
        setTimelineError(true)
      }
      if (historyResult.status === 'fulfilled') {
        setSnapshots(historyResult.value.data)
      }
      setLoading(false)
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [])

  if (loading) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载这个月的足迹...</p>
  }

  if (timelineError || !timeline) {
    return <div className="alert alert-error">月度时间轴加载失败。</div>
  }

  const trendSeries = snapshots ? parseTrend(snapshots) : null
  const trendDays = trendSeries
    ? Math.max(...TREND_DIMS.map((dim) => trendSeries[dim.key].length), 0)
    : 0
  const change = timeline.profileChange

  return (
    <div className="stack stack-md">
      <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
        这个月的足迹：你会用的词越来越多，画像也越来越准。
      </p>

      {/* 1. 分数趋势（外壳，视觉权重压低） */}
      <section className="card stack stack-sm">
        <h3>分数趋势</h3>
        {trendSeries && trendDays >= 7 ? (
          <ScoreTrendChart series={trendSeries} />
        ) : (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            坚持 7 天后出曲线。
          </p>
        )}
      </section>

      {/* 2. 本月里程碑 */}
      <section className="card stack stack-sm">
        <h3>本月里程碑</h3>
        {timeline.events.length === 0 ? (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            还没有里程碑，去完成今天的练习吧。
          </p>
        ) : (
          <ul className="timeline-list">
            {timeline.events.map((event, index) => {
              const badge = eventBadge(event)
              return (
                <li key={index} className="timeline-item">
                  <span className="timeline-date">{formatMonthDay(event.occurredAt)}</span>
                  <Badge variant={badge.variant}>{badge.label}</Badge>
                  <span style={{ fontSize: 'var(--text-sm)' }}>{eventText(event)}</span>
                </li>
              )
            })}
          </ul>
        )}
      </section>

      {/* 3. 画像变化（规则 diff，零 LLM） */}
      <section className="card stack stack-sm">
        <h3>画像变化</h3>
        {!change.hasProfile ? (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            完成首次测评后，这里会展示你的能力画像。
          </p>
        ) : change.hasComparison ? (
          change.newStrengths.length === 0 && change.improvedWeaknesses.length === 0 ? (
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
              画像暂无变化，继续练习，下一次画像会更准。
            </p>
          ) : (
            <ul className="timeline-list">
              {change.newStrengths.map((item, index) => (
                <li key={`s-${index}`} className="timeline-item">
                  <Badge variant="success">新强项</Badge>
                  <span style={{ fontSize: 'var(--text-sm)' }}>
                    {DIMENSION_LABELS[item.dimension] ?? item.dimension} · {item.statement}
                  </span>
                </li>
              ))}
              {change.improvedWeaknesses.map((item, index) => (
                <li key={`w-${index}`} className="timeline-item">
                  <Badge variant="info">好转</Badge>
                  <span style={{ fontSize: 'var(--text-sm)' }}>
                    {DIMENSION_LABELS[item.dimension] ?? item.dimension} · {item.statement}
                  </span>
                </li>
              ))}
            </ul>
          )
        ) : (
          <>
            <ul className="timeline-list">
              {change.currentFindings.map((item, index) => {
                const polarity = POLARITY_BADGE[item.polarity] ?? POLARITY_BADGE.neutral
                return (
                  <li key={index} className="timeline-item">
                    <Badge variant={polarity.variant}>{polarity.label}</Badge>
                    <span style={{ fontSize: 'var(--text-sm)' }}>
                      {DIMENSION_LABELS[item.dimension] ?? item.dimension} · {item.statement}
                    </span>
                  </li>
                )
              })}
            </ul>
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
              这是你的第一份画像，下一次生成后这里会对比变化。
            </p>
          </>
        )}
      </section>

      {/* 4. 洞察回放（最近 3 条瓶颈洞察） */}
      <section className="card stack stack-sm">
        <h3>洞察回放</h3>
        {timeline.insights.length === 0 ? (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            还没有瓶颈洞察，多练习几天后会自动生成。
          </p>
        ) : (
          <ul className="timeline-list">
            {timeline.insights.map((insight, index) => {
              const meta = NATURE_META[insight.nature]
              return (
                <li key={index} className="timeline-item">
                  <span className="timeline-date">{formatMonthDay(insight.createdAt)}</span>
                  <Badge variant="warn">{meta?.name ?? insight.nature}</Badge>
                  <span style={{ fontSize: 'var(--text-sm)' }}>{insight.statement}</span>
                </li>
              )
            })}
          </ul>
        )}
      </section>
    </div>
  )
}
