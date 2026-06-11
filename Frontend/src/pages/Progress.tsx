import { BarChart3, CalendarDays, Target } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ProgressSummary } from '../types/models'

export function Progress() {
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const response = await api.get<ProgressSummary>(endpoints.progress)
        setProgress(response.data)
      } catch {
        setError('进度加载失败。')
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  if (loading) {
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载进度...</div>
  }

  if (error || !progress) {
    return <div className="rounded-md border border-rose-200 bg-rose-50 p-6 text-sm text-rose-900">{error ?? '暂无进度。'}</div>
  }

  const stats = [
    { label: '已学词', value: progress.totalLearned, icon: Target },
    { label: '待复习', value: progress.dueReviews, icon: CalendarDays },
    { label: '正确率', value: `${progress.accuracyPercent}%`, icon: BarChart3 },
  ]

  return (
    <div className="grid gap-5">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <h2 className="text-2xl font-semibold">{progress.displayName}</h2>
        <p className="mt-1 text-sm text-neutral-600">总体等级 {progress.overallLevel}，词汇等级 {progress.vocabLevel}</p>
      </section>

      <section className="grid gap-3 sm:grid-cols-3">
        {stats.map((stat) => {
          const Icon = stat.icon
          return (
            <article key={stat.label} className="rounded-md border border-neutral-200 bg-white p-5">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium text-neutral-600">{stat.label}</p>
                <Icon size={20} className="text-emerald-700" aria-hidden="true" />
              </div>
              <p className="mt-3 text-3xl font-semibold">{stat.value}</p>
            </article>
          )
        })}
      </section>

      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <h3 className="text-base font-semibold">学习日志</h3>
        <dl className="mt-4 grid gap-3 text-sm">
          <div className="flex justify-between border-b border-neutral-100 pb-2">
            <dt className="text-neutral-600">总记录</dt>
            <dd className="font-semibold">{progress.totalLogs}</dd>
          </div>
          <div className="flex justify-between border-b border-neutral-100 pb-2">
            <dt className="text-neutral-600">连续天数</dt>
            <dd className="font-semibold">{progress.streakDays}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-neutral-600">最后学习</dt>
            <dd className="font-semibold">{progress.lastStudyDate ?? '尚未开始'}</dd>
          </div>
        </dl>
      </section>
    </div>
  )
}
