import { ChevronDown, ChevronUp } from 'lucide-react'
import { useState } from 'react'
import type { ArticleVocabMapping, WordExample } from '../types/article'

interface VocabExtractPanelProps {
  items: ArticleVocabMapping[]
  onWordSelect: (word: string) => void
}

function exampleLabel(kind: WordExample['kind']) {
  return kind === 'contextual' ? '文中用法' : '其他场景'
}

function UsageCell({ item }: { item: ArticleVocabMapping }) {
  const [expanded, setExpanded] = useState(false)
  const examples = item.examples ?? []

  if (examples.length === 0) {
    return <span style={{ color: 'var(--muted)' }}>{item.specialUsage || '—'}</span>
  }

  return (
    <div>
      <button type="button" className="btn btn-ghost btn-sm" onClick={() => setExpanded((value) => !value)}>
        {expanded ? '收起用法' : '查看用法'}
      </button>
      {expanded ? (
        <div className="stack stack-sm" style={{ marginTop: 'var(--space-2)' }}>
          {examples.map((example, index) => (
            <div key={`${example.kind}-${index}`}>
              <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>{exampleLabel(example.kind)}</p>
              <p style={{ fontStyle: 'italic', fontSize: 'var(--text-sm)' }}>{example.sentence}</p>
              <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)', marginTop: 2 }}>{example.explanation}</p>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  )
}

export function VocabExtractPanel({ items, onWordSelect }: VocabExtractPanelProps) {
  const [expanded, setExpanded] = useState(false)

  return (
    <section className="card vocab-panel">
      <button
        type="button"
        className="vocab-panel-toggle row-between"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
      >
        <div style={{ textAlign: 'left' }}>
          <h2 style={{ fontWeight: 540, fontSize: 'var(--text-base)' }}>重点词汇</h2>
          {expanded ? (
            <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
              结合你的等级提取值得学习的词。
            </p>
          ) : null}
        </div>
        <div className="row" style={{ alignItems: 'center', gap: 'var(--space-2)', flexShrink: 0 }}>
          {items.length > 0 ? (
            <span style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>{items.length} 词</span>
          ) : null}
          {expanded ? (
            <ChevronUp size={18} aria-hidden="true" />
          ) : (
            <ChevronDown size={18} aria-hidden="true" />
          )}
        </div>
      </button>

      {expanded ? (
        items.length > 0 ? (
          <div style={{ marginTop: 'var(--space-4)', overflowX: 'auto' }}>
            <table className="vocab-table">
              <thead>
                <tr>
                  <th>单词</th>
                  <th>文中含义</th>
                  <th>用法</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <button type="button" onClick={() => onWordSelect(item.wordLemma)} className="vocab-word">
                        {item.wordLemma}
                      </button>
                      {item.phonetics ? (
                        <p style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)', color: 'var(--muted)', marginTop: 2 }}>
                          {item.phonetics}
                        </p>
                      ) : null}
                    </td>
                    <td>{item.contextMeaning}</td>
                    <td>
                      <UsageCell item={item} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p style={{ marginTop: 'var(--space-4)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>暂无重点词汇。</p>
        )
      ) : null}
    </section>
  )
}
