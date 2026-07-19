export interface ProgressDetailData {
  totalLearned: number
  dueReviews: number
  accuracyPercent: number
  streakDays: number
  totalLogs: number
  lastStudyDate: string | null
}

interface ProgressDetailProps {
  data: ProgressDetailData
}

export function ProgressDetail({ data }: ProgressDetailProps) {
  const stats = [
    { label: '已学词', value: data.totalLearned },
    { label: '待复习', value: data.dueReviews },
    { label: '正确率', value: `${data.accuracyPercent}%` },
    { label: '连续打卡', value: `${data.streakDays} 天` },
  ]

  return (
    <div className="stack stack-md">
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
            <dd className="val">{data.totalLogs}</dd>
          </div>
          <div className="activity-stat">
            <dt>连续天数</dt>
            <dd className="val">{data.streakDays}</dd>
          </div>
          <div className="activity-stat" style={{ borderBottom: 'none' }}>
            <dt>最后学习</dt>
            <dd className="val">{data.lastStudyDate ?? '尚未开始'}</dd>
          </div>
        </dl>
      </div>
    </div>
  )
}
