import { Star, X } from 'lucide-react'
import { nextCefrLevel } from '../lib/cefr'

interface UpgradeCandidateBannerProps {
  currentLevel?: string
  /** T-035：主行动改为直达确认挑战（/challenge，confirmationChallenge=true） */
  onStartChallenge: () => void
  onDismiss: () => void
}

export function UpgradeCandidateBanner({
  currentLevel,
  onStartChallenge,
  onDismiss,
}: UpgradeCandidateBannerProps) {
  const nextLevel = currentLevel ? nextCefrLevel(currentLevel) : null

  return (
    <div className="upgrade-banner">
      <Star size={20} aria-hidden="true" style={{ color: 'var(--info)', flexShrink: 0 }} />
      <p>
        <strong>升级候选：</strong>
        {nextLevel
          ? `你已具备冲击 ${nextLevel} 的实力，来确认挑战。`
          : '你已具备冲击下一级的实力，来确认挑战。'}
      </p>
      <button type="button" className="btn btn-sm btn-primary" onClick={onStartChallenge}>
        去确认挑战
      </button>
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={onDismiss}
        aria-label="关闭通知"
      >
        <X size={16} />
      </button>
    </div>
  )
}
