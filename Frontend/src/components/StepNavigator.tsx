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
    <div className="stack stack-sm">
      {showProgress && total > 0 && (
        <div className="row-between" style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          <span>第 {index + 1} / {total} 题</span>
          <span>{progress}%</span>
        </div>
      )}
      {showProgress && total > 0 && <ProgressBar value={progress} />}
      <div className="row" style={{ flexWrap: 'wrap' }}>
        {onPrevious && (
          <button
            type="button"
            onClick={onPrevious}
            disabled={!canPrevious}
            className="btn btn-secondary"
          >
            <ChevronLeft size={18} aria-hidden="true" />
            {previousLabel}
          </button>
        )}
        <button
          type="button"
          onClick={onNext}
          disabled={!canNext}
          className="btn btn-primary"
        >
          {nextLabel}
          <ChevronRight size={18} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}
