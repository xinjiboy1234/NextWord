import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { ProgressDetail } from '../components/ProgressDetail'
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

  return (
    <div className="stack stack-md">
      <div className="section-header">
        <h2>{progress.displayName}</h2>
        <p>总体等级 {progress.overallLevel}，词汇等级 {progress.vocabLevel}</p>
      </div>
      <ProgressDetail data={progress} />
    </div>
  )
}
