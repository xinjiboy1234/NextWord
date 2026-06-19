import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { LevelDashboard } from '../types/assessment'

export function LevelDashboardPage() {
  const [dashboard, setDashboard] = useState<LevelDashboard | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function load() {
      setLoading(true)
      try {
        const response = await api.get<LevelDashboard>(endpoints.levelDashboard)
        setDashboard(response.data)
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  if (loading) {
    return <p className="text-sm text-neutral-600">加载等级面板...</p>
  }

  if (!dashboard) {
    return <p className="text-sm text-rose-700">等级数据不可用。</p>
  }

  const dimensions = [
    { label: '词汇', value: dashboard.vocabLevel },
    { label: '拼写', value: dashboard.spellingLevel },
    { label: '造句', value: dashboard.sentenceLevel },
    { label: '阅读', value: dashboard.readingLevel },
  ]

  return (
    <div className="grid gap-5">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <h2 className="text-2xl font-semibold">等级面板</h2>
        <p className="mt-1 text-sm text-neutral-600">
          总体等级 {dashboard.overallLevel}
          {dashboard.upgradeCandidate ? ' · 已达升级候选' : ''}
        </p>
        {!dashboard.hasCompletedInitialAssessment && (
          <p className="mt-2 rounded-md bg-amber-50 p-2 text-sm text-amber-900">尚未完成首次测评，请前往「测评」页。</p>
        )}
      </section>

      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {dimensions.map((item) => (
          <article key={item.label} className="rounded-md border border-neutral-200 bg-white p-4">
            <p className="text-sm text-neutral-600">{item.label}</p>
            <p className="mt-2 text-2xl font-semibold">{item.value}</p>
          </article>
        ))}
      </section>

      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <h3 className="text-lg font-semibold">等级历史</h3>
        <div className="mt-3 space-y-2">
          {dashboard.recentHistory.length === 0 ? (
            <p className="text-sm text-neutral-600">暂无记录。</p>
          ) : (
            dashboard.recentHistory.map((item) => (
              <div key={item.id} className="rounded-md border border-neutral-100 px-3 py-2 text-sm">
                {item.fromLevel} → {item.toLevel} · {item.reason}
              </div>
            ))
          )}
        </div>
      </section>
    </div>
  )
}
