import { ChevronDown, ChevronRight, ClipboardCheck } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { EvaluationReportView } from './EvaluationReportView'
import { LoadingSkeleton } from './LoadingSkeleton'
import { Badge } from './ui/Badge'
import { useEvaluationReport } from '../hooks/useProfileScores'
import { useLatestAssessment } from '../hooks/useLatestAssessment'

/**
 * T-069 「我的」页最近测评简要卡：只显示当前测评的简要信息（不敷衍）——
 * 人话标签 + 定级 + 表达力综合分 + 测评日期 + 常见问题；完整详情（rubric/四维/逐题评语）
 * 由「查看测评结果」打开，能力画像（评估报告）在卡内展开查看。
 */
export function AssessmentBriefCard() {
  const navigate = useNavigate()
  const { brief, loading, error } = useLatestAssessment()
  const [reportOpen, setReportOpen] = useState(false)
  // 展开评估报告时才拉取（复用 LevelPanel 同款轮询：Ready 即停，正常一次取到）
  const { report, content } = useEvaluationReport(reportOpen && brief ? 1 : null)

  if (error) {
    return null
  }

  if (loading) {
    return (
      <section className="card dashboard-info-card">
        <LoadingSkeleton lines={3} />
      </section>
    )
  }

  if (!brief) {
    return (
      <section className="card stack stack-sm">
        <div className="dashboard-info-card-head">
          <h3>
            <ClipboardCheck size={16} aria-hidden="true" />
            最近测评
          </h3>
        </div>
        <p className="dashboard-info-empty">
          还没有测评记录。完成一次水平测评后，这里会显示你的最新定级与常见问题。
        </p>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          style={{ width: 'fit-content' }}
          onClick={() => navigate('/assessment')}
        >
          去测评
        </button>
      </section>
    )
  }

  return (
    <section className="card stack stack-sm">
      <div className="dashboard-info-card-head">
        <h3>
          <ClipboardCheck size={16} aria-hidden="true" />
          最近测评
        </h3>
        <Badge variant="muted">{formatDate(brief.startAt)}</Badge>
      </div>

      <div className="stack stack-sm">
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
          <span style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
            {brief.rubricLabel ?? '—'}
          </span>
          <span className="badge-level">{brief.finalLevel}</span>
          {brief.guardAdjusted && <Badge variant="warn">经识别矫正</Badge>}
        </div>
        {brief.expressionScore != null && (
          <p className="dashboard-info-meta">表达力综合分 {brief.expressionScore}/100</p>
        )}
        {brief.topErrorTags.length > 0 && (
          <p className="dashboard-info-meta">
            常见问题：{brief.topErrorTags.slice(0, 3).join('、')}
          </p>
        )}
      </div>

      <div className="stack stack-sm" style={{ marginTop: 'var(--space-2)' }}>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          style={{ width: 'fit-content' }}
          onClick={() => navigate(`/assessments?detail=${brief.id}`)}
        >
          查看测评结果
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          style={{ width: 'fit-content' }}
          onClick={() => setReportOpen((value) => !value)}
          aria-expanded={reportOpen}
        >
          {reportOpen ? <ChevronDown size={16} aria-hidden="true" /> : <ChevronRight size={16} aria-hidden="true" />}
          评估报告
        </button>
      </div>

      {reportOpen && (
        <div className="stack stack-sm" style={{ borderTop: '1px solid var(--border)', paddingTop: 'var(--space-3)' }}>
          {content ? (
            <EvaluationReportView content={content} />
          ) : report && report.status !== 'Ready' ? (
            <p className="dashboard-info-meta">评估报告生成中，稍后自动出现…</p>
          ) : (
            <p className="dashboard-info-meta">暂无评估报告，完成测评后自动生成。</p>
          )}
        </div>
      )}
    </section>
  )
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' })
}
