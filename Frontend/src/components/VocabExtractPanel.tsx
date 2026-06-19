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
    <section className="rounded-md border border-neutral-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">重点词汇</h2>
          <p className="text-sm text-neutral-600">结合你的等级提取值得学习的词。</p>
        </div>
        <button
          type="button"
          onClick={onExtract}
          disabled={loading}
          className="inline-flex h-10 items-center rounded-md bg-emerald-700 px-3 text-sm font-medium text-white disabled:opacity-60"
        >
          {loading ? '提取中...' : items.length > 0 ? '重新提取' : '提取词汇'}
        </button>
      </div>

      {error && <p className="mt-3 rounded-md bg-rose-50 p-2 text-sm text-rose-900">{error}</p>}

      {items.length > 0 ? (
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b border-neutral-200 text-neutral-600">
              <tr>
                <th className="px-2 py-2">单词</th>
                <th className="px-2 py-2">文中含义</th>
                <th className="px-2 py-2">用法</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-b border-neutral-100">
                  <td className="px-2 py-2">
                    <button
                      type="button"
                      onClick={() => onWordSelect(item.wordLemma)}
                      className="font-medium text-emerald-800 underline"
                    >
                      {item.wordLemma}
                    </button>
                  </td>
                  <td className="px-2 py-2 text-neutral-700">{item.contextMeaning}</td>
                  <td className="px-2 py-2 text-neutral-600">{item.specialUsage || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="mt-4 text-sm text-neutral-600">尚未提取词汇，点击按钮开始。</p>
      )}
    </section>
  )
}
