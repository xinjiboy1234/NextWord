import { useEffect, useState } from 'react'
import type { WordDefinition, WordExample } from '../types/article'
import { FeedbackButton } from './FeedbackButton'

interface WordPopoverProps {
  word: string | null
  definition: WordDefinition | null
  loading: boolean
  knownRate?: number | null
  personalDifficulty?: number | null
  onClose: () => void
}

function exampleLabel(kind: WordExample['kind']) {
  return kind === 'contextual' ? '文中场景' : '其他场景'
}

export function WordPopover({ word, definition, loading, knownRate, personalDifficulty, onClose }: WordPopoverProps) {
  const [showExamples, setShowExamples] = useState(false)

  useEffect(() => {
    setShowExamples(false)
  }, [word])

  if (!word) return null

  const examples = definition?.examples ?? []

  return (
    <div className="word-popover-panel">
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
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={() => setShowExamples((value) => !value)}
            style={{ alignSelf: 'flex-start' }}
          >
            {showExamples ? '收起例句' : '查看例句'}
          </button>
          {showExamples ? (
            <div className="stack stack-sm">
              {examples.length > 0 ? (
                examples.map((example, index) => (
                  <div
                    key={`${example.kind}-${index}`}
                    style={{ padding: 'var(--space-3)', background: 'var(--border-soft)', borderRadius: 'var(--radius-md)' }}
                  >
                    <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)', marginBottom: 4 }}>
                      {exampleLabel(example.kind)}
                    </p>
                    <p style={{ fontStyle: 'italic' }}>{example.sentence}</p>
                    <p style={{ marginTop: 4, color: 'var(--muted)' }}>{example.explanation}</p>
                  </div>
                ))
              ) : (
                <p style={{ color: 'var(--muted)' }}>该词在当前等级暂无合适例句。</p>
              )}
            </div>
          ) : null}
        </div>
      )}
      <FeedbackButton word={word} disabled={loading} />
    </div>
  )
}
