import type { ProficiencyRubricView } from '../types/assessment'

/**
 * T-055 人话 rubric 展示块（DESIGN-assessment-visibility §3.1）：
 * 结论先行——总体人话标签 + 一句话描述居首，四维各带特征描述，分数靠后（外壳不喧宾夺主）。
 * 测评结果页与测评记录详情页共用；文案全部由后端 ProficiencyRubric 装配，前端不做映射。
 */
export function ProficiencyRubric({ rubric }: { rubric: ProficiencyRubricView }) {
  return (
    <div className="stack stack-sm">
      <p style={{ fontSize: 'var(--text-lg)', fontWeight: 700 }}>
        {rubric.overallLabel}
        <span style={{ marginLeft: 'var(--space-2)', fontSize: 'var(--text-sm)', fontWeight: 400, color: 'var(--muted)' }}>
          {rubric.overallDescription}
        </span>
      </p>
      <ul className="stack stack-sm" style={{ fontSize: 'var(--text-sm)', listStyle: 'none', padding: 0 }}>
        {rubric.dimensions.map((dimension) => (
          <li key={dimension.name}>
            {dimension.name} {dimension.score}/5：{dimension.description}
          </li>
        ))}
      </ul>
    </div>
  )
}
