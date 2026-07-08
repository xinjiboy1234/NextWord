import { BookOpenText } from 'lucide-react'
import type { ReactNode } from 'react'

interface OnboardingLayoutProps {
  children: ReactNode
  step?: number
  totalSteps?: number
  onSkip?: () => void
  skipDisabled?: boolean
}

export function OnboardingLayout({
  children,
  step,
  totalSteps = 5,
  onSkip,
  skipDisabled = false,
}: OnboardingLayoutProps) {
  const stepLabel = step != null ? `第 ${step} / ${totalSteps} 步` : null

  return (
    <div className="onboarding-shell">
      <header className="onboarding-header">
        <div className="onboarding-brand">
          <BookOpenText size={24} className="text-[var(--brand)]" aria-hidden="true" />
          <span>NextWord</span>
        </div>
        <div className="onboarding-header-actions">
          {stepLabel ? <span className="onboarding-step-label">{stepLabel}</span> : null}
          {onSkip ? (
            <button
              type="button"
              className="btn btn-ghost btn-sm onboarding-skip"
              onClick={onSkip}
              disabled={skipDisabled}
            >
              跳过本次测评
            </button>
          ) : null}
        </div>
      </header>
      <main className="onboarding-main">{children}</main>
    </div>
  )
}
