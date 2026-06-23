import { Check } from 'lucide-react'

interface AssessmentTimelineProps {
  steps: readonly string[]
  currentStep: number
  maxReachedStep: number
  onStepClick: (step: number) => void
}

export function AssessmentTimeline({ steps, currentStep, maxReachedStep, onStepClick }: AssessmentTimelineProps) {
  return (
    <nav aria-label="测评进度" className="mt-4">
      <ol className="flex flex-wrap items-center gap-1">
        {steps.map((label, index) => {
          const step = index + 1
          const isCurrent = currentStep === step
          const isReachable = step <= maxReachedStep
          const isCompleted = step < currentStep || (step < maxReachedStep && !isCurrent)

          return (
            <li key={label} className="flex items-center">
              {index > 0 && (
                <span
                  className={`mx-1 hidden h-px w-6 sm:block ${
                    step <= maxReachedStep ? 'bg-emerald-400' : 'bg-neutral-200'
                  }`}
                  aria-hidden="true"
                />
              )}
              <button
                type="button"
                disabled={!isReachable}
                onClick={() => isReachable && onStepClick(step)}
                className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition disabled:cursor-not-allowed ${
                  isCurrent
                    ? 'bg-emerald-700 text-white shadow-sm'
                    : isReachable
                      ? 'bg-emerald-50 text-emerald-800 ring-1 ring-emerald-200 hover:bg-emerald-100'
                      : 'bg-neutral-100 text-neutral-400'
                }`}
              >
                <span
                  className={`grid h-5 w-5 place-items-center rounded-full text-[10px] font-bold ${
                    isCurrent
                      ? 'bg-white/20 text-white'
                      : isCompleted
                        ? 'bg-emerald-600 text-white'
                        : isReachable
                          ? 'bg-emerald-200 text-emerald-900'
                          : 'bg-neutral-200 text-neutral-500'
                  }`}
                >
                  {isCompleted && !isCurrent ? <Check size={12} aria-hidden="true" /> : step}
                </span>
                {label}
              </button>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
