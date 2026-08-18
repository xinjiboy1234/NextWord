import { Badge } from './ui/Badge'
import type { EvaluationReportContent } from '../types/score'

/** T-005 画像维度/置信度中文标签（自 LevelPanel 提取，T-069 起评估报告视图供简要卡复用） */
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

/**
 * T-069 评估报告视图（schemaVersion 2 findings 优先，旧模板 strengths/weaknesses 兜底）。
 * 详情放背后：由「最近测评」简要卡的「查看评估报告」展开。
 */
export function EvaluationReportView({ content }: { content: EvaluationReportContent }) {
  return (
    <div className="stack stack-sm">
      <p style={{ fontSize: 'var(--text-sm)' }}>{content.summary}</p>
      {content.findings && content.findings.length > 0 ? (
        <div>
          <p className="mono-label" style={{ marginBottom: 4 }}>能力画像（已交叉验证）</p>
          <ul className="stack stack-sm" style={{ listStyle: 'none', padding: 0 }}>
            {content.findings.map((finding, index) => (
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
          {content.strengths.length > 0 && (
            <div>
              <p className="mono-label" style={{ marginBottom: 4 }}>优势</p>
              <ul style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.2em' }}>
                {content.strengths.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          )}
          {content.weaknesses.length > 0 && (
            <div>
              <p className="mono-label" style={{ marginBottom: 4 }}>待提升</p>
              <ul style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.2em' }}>
                {content.weaknesses.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </div>
  )
}
