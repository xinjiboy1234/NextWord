import { Check } from 'lucide-react'

interface AssessmentTimelineProps {
  steps: readonly string[]
  currentStep: number
  maxReachedStep: number
  maxNavigableStep?: number
  onStepClick: (step: number) => void
}

export function AssessmentTimeline({
  steps,
  currentStep,
  maxReachedStep,
  maxNavigableStep,
  onStepClick,
}: AssessmentTimelineProps) {
  const navigableLimit = maxNavigableStep ?? maxReachedStep

  return (
    <nav aria-label="测评进度" className="steps" style={{ overflowX: 'auto' }}>
      {steps.map((label, index) => {
        const step = index + 1
        const isCurrent = currentStep === step
        const isReachable = step <= navigableLimit
        const isCompleted = step < currentStep || (step < maxReachedStep && !isCurrent)

        return (
          <span key={label} style={{ display: 'contents' }}>
            {index > 0 && (
              <span
                className="step-connector"
                style={{ background: step <= maxReachedStep ? 'var(--fg)' : undefined }}
                aria-hidden="true"
              />
            )}
            <button
              type="button"
              disabled={!isReachable}
              onClick={() => isReachable && onStepClick(step)}
              className={`step${isCurrent ? ' active' : ''}${isCompleted ? ' completed' : ''}`}
            >
              <span className="step-dot">
                {isCompleted && !isCurrent ? <Check size={12} aria-hidden="true" /> : step}
              </span>
              {label}
            </button>
          </span>
        )
      })}
    </nav>
  )
}
