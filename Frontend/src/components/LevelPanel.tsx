import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from './ui/Badge'
import { useEvaluationReport } from '../hooks/useProfileScores'
import { DIMENSION_HINTS, getCefrMeta } from '../lib/cefrMeta'
import type { LevelDashboard } from '../types/assessment'

/** T-005 画像维度/置信度中文标签 */
const DIMENSION_LABELS: Record<string, string> = {
  scenario: '场景',
  skill: '技能',
  reading: '阅读',
}
const CONFIDENCE_LABELS: Record<string, string> = {
  high: '高置信',
  medium: '中置信',
  low: '低置信',
}

export function LevelPanel() {
  const [dashboard, setDashboard] = useState<LevelDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const evaluation = useEvaluationReport(dashboard?.scores ? 1 : null)

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
            综合 Score {scores.overall.toFixed(0)} · {scores.difficultyBucket}
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

      {evaluation.content && (
        <section className="card stack stack-sm">
          <h3>AI 评估报告</h3>
          <p style={{ fontSize: 'var(--text-sm)' }}>{evaluation.content.summary}</p>
          {evaluation.content.findings && evaluation.content.findings.length > 0 ? (
            <div>
              <p className="mono-label" style={{ marginBottom: 4 }}>能力画像（已交叉验证）</p>
              <ul className="stack stack-sm" style={{ listStyle: 'none', padding: 0 }}>
                {evaluation.content.findings.map((finding, index) => (
                  <li key={index} style={{ fontSize: 'var(--text-sm)' }}>
                    <span style={{ display: 'inline-flex', gap: 6, marginRight: 6 }}>
                      <Badge variant={finding.polarity === 'strength' ? 'success' : finding.polarity === 'weakness' ? 'warn' : 'muted'}>
                        {DIMENSION_LABELS[finding.dimension] ?? finding.dimension}
                      </Badge>
                      <Badge variant="muted">{CONFIDENCE_LABELS[finding.confidence] ?? finding.confidence}</Badge>
                      {finding.confidence === 'low' && <Badge variant="muted">初步</Badge>}
                    </span>
                    {finding.statement}
                  </li>
                ))}
              </ul>
            </div>
          ) : (
            <>
              {evaluation.content.strengths.length > 0 && (
                <div>
                  <p className="mono-label" style={{ marginBottom: 4 }}>优势</p>
                  <ul style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.2em' }}>
                    {evaluation.content.strengths.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </div>
              )}
              {evaluation.content.weaknesses.length > 0 && (
                <div>
                  <p className="mono-label" style={{ marginBottom: 4 }}>待提升</p>
                  <ul style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.2em' }}>
                    {evaluation.content.weaknesses.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}
        </section>
      )}

      <div className="section-header">
        <h3>等级历史</h3>
      </div>
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
                <td style={{ color: 'var(--muted)' }}>{item.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
