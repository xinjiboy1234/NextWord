interface ErrorAnalysisProps {
  items?: string[]
  suggestion?: string
}

export function ErrorAnalysis({ items = [], suggestion }: ErrorAnalysisProps) {
  if (items.length === 0 && !suggestion) return null

  return (
    <div className="side-panel">
      <h4 className="side-panel-title">问题分析</h4>
      <ul className="stack stack-sm" style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.1em' }}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
      {suggestion ? (
        <p style={{ marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)', padding: 'var(--space-3)', background: 'var(--border-soft)', borderRadius: 'var(--radius-md)' }}>
          {suggestion}
        </p>
      ) : null}
    </div>
  )
}
