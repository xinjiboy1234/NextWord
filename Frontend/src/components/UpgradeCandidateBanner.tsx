import { GraduationCap, X } from 'lucide-react'

interface UpgradeCandidateBannerProps {
  onOpenLevel: () => void
  onDismiss: () => void
}

export function UpgradeCandidateBanner({ onOpenLevel, onDismiss }: UpgradeCandidateBannerProps) {
  return (
    <section className="rounded-md border border-sky-300 bg-sky-50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-sky-950">你已具备升级条件</h2>
          <p className="mt-1 text-sm text-sky-900">
            近期学习表现稳定，可前往等级页查看升级建议或参加挑战测试。
          </p>
        </div>
        <div className="flex shrink-0 gap-2">
          <button
            type="button"
            onClick={onOpenLevel}
            className="inline-flex h-10 items-center gap-2 rounded-md bg-emerald-700 px-3 text-sm font-medium text-white"
          >
            <GraduationCap size={16} />
            查看等级
          </button>
          <button
            type="button"
            onClick={onDismiss}
            aria-label="关闭通知"
            className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-sky-300 text-sky-900"
          >
            <X size={16} />
          </button>
        </div>
      </div>
    </section>
  )
}
