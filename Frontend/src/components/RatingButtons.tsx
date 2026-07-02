import { Check, HelpCircle, X } from 'lucide-react'
import type { AssessmentResult } from '../types/models'

interface RatingButtonsProps {
  disabled?: boolean
  onRate: (rating: AssessmentResult) => void
}

const buttons: Array<{ rating: AssessmentResult; label: string; icon: typeof Check; className: string }> = [
  { rating: 'Remembered', label: '记住', icon: Check, className: 'remember' },
  { rating: 'Fuzzy', label: '模糊', icon: HelpCircle, className: 'fuzzy' },
  { rating: 'Forgot', label: '不会', icon: X, className: 'forgot' },
]

export function RatingButtons({ disabled, onRate }: RatingButtonsProps) {
  return (
    <div className="rating-group">
      {buttons.map((button) => {
        const Icon = button.icon
        return (
          <button
            key={button.rating}
            type="button"
            disabled={disabled}
            onClick={() => onRate(button.rating)}
            className={`rating-btn ${button.className}`}
          >
            <Icon size={18} aria-hidden="true" />
            {button.label}
          </button>
        )
      })}
    </div>
  )
}
