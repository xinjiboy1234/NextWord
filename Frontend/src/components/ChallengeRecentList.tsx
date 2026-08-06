import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from './ui/Badge'
import type { ChallengeRecord } from '../types/assessment'

interface ChallengeRecentListProps {
  refreshKey?: number
}

export function ChallengeRecentList({ refreshKey }: ChallengeRecentListProps) {
  const [records, setRecords] = useState<ChallengeRecord[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function load() {
      setLoading(true)
      try {
        const response = await api.get<ChallengeRecord[]>(endpoints.challengeRecent)
        setRecords(response.data)
      } catch {
        setRecords([])
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [refreshKey])

  if (loading) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载挑战记录...</p>
  }

  if (records.length === 0) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>暂无挑战记录。</p>
  }

  return (
    <div className="stack stack-sm">
      {records.map((record) => (
        <article key={record.id} className="comment-card">
          <div className="row-between" style={{ flexWrap: 'wrap', gap: 'var(--space-2)' }}>
            <span className="c-meta">
              {new Date(record.timestamp).toLocaleString('zh-CN')} · {record.attemptedLevel}
            </span>
            <Badge variant={record.passed ? 'success' : 'warn'}>
              {record.passed ? '通过' : '未通过'}
            </Badge>
          </div>
          {/* T-035：分数带满分参照；AttemptedLevel 解释挑战难度口径 */}
          <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }}>
            总分 <strong>{Math.round(record.totalScore)}/100</strong>
            {' · '}
            词汇 {Math.round(record.vocabularyScore)}/100
            {' / '}
            造句 {record.sentenceScore.toFixed(1)}/5
            {' / '}
            阅读 {Math.round(record.readingScore)}/100
          </p>
          <p style={{ marginTop: 2, fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
            {record.attemptedLevel} 为挑战目标档：挑战比当前等级高一档的内容。
          </p>
        </article>
      ))}
    </div>
  )
}
