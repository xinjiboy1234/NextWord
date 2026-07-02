import type { ArticleVocabMapping } from '../types/article'

interface VocabExtractPanelProps {
  items: ArticleVocabMapping[]
  loading: boolean
  error: string | null
  onExtract: () => void
  onWordSelect: (word: string) => void
}

export function VocabExtractPanel({ items, loading, error, onExtract, onWordSelect }: VocabExtractPanelProps) {
  return (
    <section className="card">
      <div className="row-between" style={{ flexWrap: 'wrap' }}>
        <div>
          <h2 style={{ fontWeight: 540, fontSize: 'var(--text-base)' }}>重点词汇</h2>
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>结合你的等级提取值得学习的词。</p>
        </div>
        <button type="button" onClick={onExtract} disabled={loading} className="btn btn-primary btn-sm">
          {loading ? '提取中...' : items.length > 0 ? '重新提取' : '提取词汇'}
        </button>
      </div>

      {error ? <div className="alert alert-error" style={{ marginTop: 'var(--space-3)' }}>{error}</div> : null}

      {items.length > 0 ? (
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
                  </td>
                  <td>{item.contextMeaning}</td>
                  <td style={{ color: 'var(--muted)' }}>{item.specialUsage || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p style={{ marginTop: 'var(--space-4)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          尚未提取词汇，点击按钮开始。
        </p>
      )}
    </section>
  )
}
