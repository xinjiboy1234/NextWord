import { ChevronLeft, ChevronRight } from 'lucide-react'
import { ProgressBar } from './ProgressBar'

interface StepNavigatorProps {
  index: number
  total: number
  onPrevious?: () => void
  onNext: () => void
  previousLabel?: string
  nextLabel?: string
  canPrevious?: boolean
  canNext?: boolean
  showProgress?: boolean
}

export function StepNavigator({
  index,
  total,
  onPrevious,
  onNext,
  previousLabel = '上一个',
  nextLabel = '下一个',
  canPrevious = index > 0,
  canNext = true,
  showProgress = true,
}: StepNavigatorProps) {
  const progress = total > 0 ? Math.round(((index + 1) / total) * 100) : 0

  return (
    <div className="grid gap-3">
      {showProgress && total > 0 && (
        <div className="flex items-center justify-between text-sm text-neutral-600">
          <span>第 {index + 1} / {total} 题</span>
          <span>{progress}%</span>
        </div>
      )}
      {showProgress && total > 0 && <ProgressBar value={progress} />}
      <div className="flex flex-wrap gap-2">
        {onPrevious && (
          <button
            type="button"
            onClick={onPrevious}
            disabled={!canPrevious}
            className="inline-flex h-10 items-center gap-1 rounded-md border border-neutral-200 bg-white px-4 text-sm font-medium text-neutral-700 hover:bg-neutral-100 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <ChevronLeft size={18} aria-hidden="true" />
            {previousLabel}
          </button>
        )}
        <button
          type="button"
          onClick={onNext}
          disabled={!canNext}
          className="inline-flex h-10 items-center gap-1 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white disabled:cursor-not-allowed disabled:bg-neutral-300"
        >
          {nextLabel}
          <ChevronRight size={18} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}
