import type { WordDefinition } from '../types/article'

interface WordPopoverProps {
  word: string | null
  definition: WordDefinition | null
  loading: boolean
  knownRate?: number | null
  personalDifficulty?: number | null
  onClose: () => void
}

export function WordPopover({ word, definition, loading, knownRate, personalDifficulty, onClose }: WordPopoverProps) {
  if (!word) return null

  return (
    <aside className="word-popover-panel">
      <button type="button" className="popover-close" onClick={onClose} aria-label="关闭">
        ×
      </button>
      <div style={{ paddingRight: 'var(--space-6)' }}>
        <h3 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 700 }}>{word}</h3>
        {definition?.phonetics ? (
          <p style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)', color: 'var(--muted)', marginTop: 4 }}>
            {definition.phonetics}
          </p>
        ) : null}
        {knownRate != null && (
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)', marginTop: 4 }}>
            熟悉度 {(knownRate * 100).toFixed(0)}%
            {personalDifficulty != null ? ` · 个人难度 ${personalDifficulty}` : ''}
          </p>
        )}
      </div>

      {loading ? (
        <p style={{ marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>查词中...</p>
      ) : (
        <div className="stack stack-sm" style={{ marginTop: 'var(--space-3)', fontSize: 'var(--text-sm)' }}>
          {definition?.meanings.map((meaning, index) => (
            <p key={index}>{meaning.definition}</p>
          ))}
          {definition?.specialUsage ? (
            <p style={{ padding: 'var(--space-3)', background: 'var(--border-soft)', borderRadius: 'var(--radius-md)' }}>
              {definition.specialUsage}
            </p>
          ) : null}
        </div>
      )}
    </aside>
  )
}
