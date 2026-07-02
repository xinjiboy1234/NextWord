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
    return <div className="card"><p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)' }}>正在加载进度...</p></div>
  }

  if (error || !progress) {
    return <div className="alert alert-error">{error ?? '暂无进度。'}</div>
  }

  const stats = [
    { label: '已学词', value: progress.totalLearned },
    { label: '待复习', value: progress.dueReviews },
    { label: '正确率', value: `${progress.accuracyPercent}%` },
    { label: '连续打卡', value: `${progress.streakDays} 天` },
  ]

  return (
    <div className="stack stack-md">
      <div className="section-header">
        <h2>{progress.displayName}</h2>
        <p>总体等级 {progress.overallLevel}，词汇等级 {progress.vocabLevel}</p>
      </div>

      <div className="stat-grid">
        {stats.map((stat) => (
          <div key={stat.label} className="stat-item">
            <div className="stat-num">{stat.value}</div>
            <div className="stat-desc">{stat.label}</div>
          </div>
        ))}
      </div>

      <div className="card">
        <h3 style={{ fontWeight: 540, marginBottom: 'var(--space-4)' }}>学习日志</h3>
        <dl className="stack stack-sm" style={{ fontSize: 'var(--text-sm)' }}>
          <div className="activity-stat">
            <dt>总记录</dt>
            <dd className="val">{progress.totalLogs}</dd>
          </div>
          <div className="activity-stat">
            <dt>连续天数</dt>
            <dd className="val">{progress.streakDays}</dd>
          </div>
          <div className="activity-stat" style={{ borderBottom: 'none' }}>
            <dt>最后学习</dt>
            <dd className="val">{progress.lastStudyDate ?? '尚未开始'}</dd>
          </div>
        </dl>
      </div>
    </div>
  )
}
