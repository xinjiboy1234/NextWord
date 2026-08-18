import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from './ui/Badge'
import { DIMENSION_HINTS, getCefrMeta } from '../lib/cefrMeta'
import type { LevelDashboard } from '../types/assessment'

/** T-030：后端枚举/内部字段 → 中文标签（避免 Overall/Initial/Intermediate 等英文外露） */
const LEVEL_CHANGE_REASON_LABELS: Record<string, string> = {
  Initial: '首次测评',
  Upgrade: '等级提升',
  Rollback: '等级回调',
}
const DIFFICULTY_BUCKET_LABELS: Record<string, string> = {
  Basic: '基础',
  Intermediate: '中级',
  Advanced: '高级',
}

export function LevelPanel() {
  const [dashboard, setDashboard] = useState<LevelDashboard | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function load() {
      setLoading(true)
      try {
        const response = await api.get<LevelDashboard>(endpoints.levelDashboard)
        setDashboard(response.data)
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  if (loading) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载等级数据...</p>
  }

  if (!dashboard) {
    return <div className="alert alert-error">等级数据不可用。</div>
  }

  const overallMeta = getCefrMeta(dashboard.overallLevel)
  const scores = dashboard.scores

  const dimensions = [
    { label: '词汇', value: dashboard.vocabLevel, score: scores?.vocabulary },
    { label: '拼写', value: dashboard.spellingLevel, score: scores?.spelling },
    { label: '造句', value: dashboard.sentenceLevel, score: scores?.writing },
    { label: '阅读', value: dashboard.readingLevel, score: scores?.reading },
  ]

  return (
    <div className="stack stack-md">
      <section className="level-hero" style={{ textAlign: 'center', padding: 'var(--space-6) 0' }}>
        <p className="mono-label">你的等级</p>
        <div
          className="overall-level"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 96,
            height: 96,
            borderRadius: '50%',
            background: 'var(--fg)',
            color: 'var(--bg)',
            fontFamily: 'var(--font-display)',
            fontSize: 'var(--text-3xl)',
            fontWeight: 700,
            margin: 'var(--space-4) auto',
          }}
        >
          {dashboard.overallLevel}
        </div>
        <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 540 }}>
          {overallMeta.label}
        </p>
        {scores && (
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)', marginTop: 4 }}>
            综合 Score {scores.overall.toFixed(0)} · {DIFFICULTY_BUCKET_LABELS[scores.difficultyBucket] ?? scores.difficultyBucket}
          </p>
        )}
        <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)', maxWidth: '36ch', margin: '4px auto 0' }}>
          {overallMeta.description}
        </p>
        {dashboard.upgradeCandidate ? (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Badge variant="info">升级候选</Badge>
          </div>
        ) : null}
        {!dashboard.hasCompletedInitialAssessment && (
          <p className="alert alert-info" style={{ marginTop: 'var(--space-4)', maxWidth: 480, marginInline: 'auto' }}>
            尚未完成首次测评，登录后将自动进入测评流程。
          </p>
        )}
      </section>

      <div className="section-header">
        <h3>技能维度</h3>
      </div>
      <div className="dim-grid">
        {dimensions.map((item) => (
          <div key={item.label} className="dim-card">
            <div className="badge-level">{item.value}</div>
            <p style={{ fontWeight: 540 }}>{item.label}</p>
            {item.score != null && (
              <p style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)', marginTop: 4 }}>
                Score {item.score.toFixed(0)}
              </p>
            )}
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)', marginTop: 4 }}>
              {DIMENSION_HINTS[item.label] ?? '—'}
            </p>
          </div>
        ))}
      </div>

      {/* T-069：评估报告详情已收拢到「最近测评」简要卡（AssessmentBriefCard）内展开查看 */}

      <div className="section-header row-between">
        <h3>等级历史</h3>
        <Link to="/assessments" style={{ fontSize: 'var(--text-sm)' }}>测评记录 →</Link>
      </div>
      {dashboard.challengePassCount > 0 && (
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          挑战已通过 <strong>{dashboard.challengePassCount}</strong> 次
          {dashboard.challengeFirstPassLevels.length > 0 && (
            <>
              {' · 首次通过 '}
              {dashboard.challengeFirstPassLevels.map((level) => (
                <span key={level} style={{ marginRight: 4, display: 'inline-block' }}>
                  <Badge variant="success">{level}</Badge>
                </span>
              ))}
            </>
          )}
        </p>
      )}
      {dashboard.recentHistory.length === 0 ? (
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>暂无记录。</p>
      ) : (
        <table className="history-table">
          <thead>
            <tr>
              <th>变更</th>
              <th>原因</th>
            </tr>
          </thead>
          <tbody>
            {dashboard.recentHistory.map((item) => (
              <tr key={item.id}>
                <td>
                  <Badge variant="info">{item.fromLevel} → {item.toLevel}</Badge>
                </td>
                <td style={{ color: 'var(--muted)' }}>{LEVEL_CHANGE_REASON_LABELS[item.reason] ?? item.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
