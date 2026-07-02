interface AiRevisionProps {
  value?: string
}

export function AiRevision({ value }: AiRevisionProps) {
  if (!value) return null

  return (
    <div className="side-panel">
      <h4 className="side-panel-title">AI 改写</h4>
      <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.6, padding: 'var(--space-3)', background: 'var(--brand-soft)', borderRadius: 'var(--radius-md)' }}>
        {value}
      </p>
    </div>
  )
}
