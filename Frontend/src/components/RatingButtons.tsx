import { Check, HelpCircle, X } from 'lucide-react'
import type { AssessmentResult } from '../types/models'

interface RatingButtonsProps {
  disabled?: boolean
  onRate: (rating: AssessmentResult) => void
}

const buttons: Array<{ rating: AssessmentResult; label: string; icon: typeof Check; className: string }> = [
  { rating: 'Remembered', label: '记住', icon: Check, className: 'border-emerald-700 bg-emerald-700 text-white' },
  { rating: 'Fuzzy', label: '模糊', icon: HelpCircle, className: 'border-amber-500 bg-amber-50 text-amber-900' },
  { rating: 'Forgot', label: '不会', icon: X, className: 'border-rose-600 bg-rose-50 text-rose-900' },
]

export function RatingButtons({ disabled, onRate }: RatingButtonsProps) {
  return (
    <div className="grid gap-2 sm:grid-cols-3">
      {buttons.map((button) => {
        const Icon = button.icon
        return (
          <button
            key={button.rating}
            type="button"
            disabled={disabled}
            onClick={() => onRate(button.rating)}
            className={`inline-flex h-11 items-center justify-center gap-2 rounded-md border px-3 text-sm font-semibold disabled:opacity-50 ${button.className}`}
          >
            <Icon size={18} aria-hidden="true" />
            {button.label}
          </button>
        )
      })}
    </div>
  )
}
