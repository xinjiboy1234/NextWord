import type { WordDefinition } from '../types/article'

interface WordPopoverProps {
  word: string | null
  definition: WordDefinition | null
  loading: boolean
  onClose: () => void
}

export function WordPopover({ word, definition, loading, onClose }: WordPopoverProps) {
  if (!word) return null

  return (
    <aside className="rounded-md border border-emerald-200 bg-emerald-50 p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold text-emerald-900">{word}</h3>
          {definition?.phonetics && <p className="text-sm text-emerald-800">{definition.phonetics}</p>}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-900"
        >
          关闭
        </button>
      </div>

      {loading ? (
        <p className="mt-3 text-sm text-emerald-800">查词中...</p>
      ) : (
        <div className="mt-3 space-y-2 text-sm text-emerald-950">
          {definition?.meanings.map((meaning, index) => (
            <p key={index}>{meaning.definition}</p>
          ))}
          {definition?.specialUsage && (
            <p className="rounded-md bg-white/70 p-2 text-emerald-900">{definition.specialUsage}</p>
          )}
        </div>
      )}
    </aside>
  )
}
