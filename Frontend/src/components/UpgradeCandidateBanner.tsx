import { Star, X } from 'lucide-react'
import { nextCefrLevel } from '../lib/cefr'

interface UpgradeCandidateBannerProps {
  currentLevel?: string
  onOpenLevel: () => void
  onDismiss: () => void
}

export function UpgradeCandidateBanner({
  currentLevel,
  onOpenLevel,
  onDismiss,
}: UpgradeCandidateBannerProps) {
  const nextLevel = currentLevel ? nextCefrLevel(currentLevel) : null
  const levelHint = currentLevel && nextLevel
    ? `${currentLevel} 升至 ${nextLevel}`
    : '你已达到升级候选条件'

  return (
    <div className="upgrade-banner">
      <Star size={20} aria-hidden="true" style={{ color: 'var(--info)', flexShrink: 0 }} />
      <p>
        <strong>升级候选：</strong>
        {levelHint}。前往等级页查看详情或参加挑战测试。
      </p>
      <button type="button" className="btn btn-sm btn-primary" onClick={onOpenLevel}>
        查看等级
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
