import { ClipboardCheck, X } from 'lucide-react'

interface OnboardingBannerProps {
  onStartAssessment: () => void
  onDismiss: () => void
}

export function OnboardingBanner({ onStartAssessment, onDismiss }: OnboardingBannerProps) {
  return (
    <section className="rounded-md border border-amber-300 bg-amber-50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-amber-950">完成首次水平测评</h2>
          <p className="mt-1 text-sm text-amber-900">
            5 步测评（词汇、拼写、造句、阅读、定级）可帮你匹配合适的学习内容。
          </p>
        </div>
        <div className="flex shrink-0 gap-2">
          <button
            type="button"
            onClick={onStartAssessment}
            className="inline-flex h-10 items-center gap-2 rounded-md bg-emerald-700 px-3 text-sm font-medium text-white"
          >
            <ClipboardCheck size={16} />
            开始测评
          </button>
          <button
            type="button"
            onClick={onDismiss}
            aria-label="关闭引导"
            className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-amber-300 text-amber-900"
          >
            <X size={16} />
          </button>
        </div>
      </div>
    </section>
  )
}
