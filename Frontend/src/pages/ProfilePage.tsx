import { LogOut } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from '../components/ui/Badge'
import { useAuth } from '../contexts/AuthContext'
import type { UserProfile } from '../types/auth'

export function ProfilePage() {
  const { logout, user } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function loadProfile() {
      setLoading(true)
      setError(null)
      try {
        const response = await api.get<UserProfile>(endpoints.profile)
        setProfile(response.data)
      } catch {
        setError('个人主页加载失败。')
      } finally {
        setLoading(false)
      }
    }

    void loadProfile()
  }, [])

  if (loading) {
    return <p className="text-sm" style={{ color: 'var(--muted)' }}>正在加载个人主页...</p>
  }

  if (error || !profile) {
    return <div className="alert alert-error">{error ?? '暂无数据。'}</div>
  }

  const stats = [
    { label: '已学词', value: profile.totalLearned },
    { label: '待复习', value: profile.dueReviews },
    { label: '正确率', value: `${profile.accuracyPercent}%` },
    { label: '连续打卡', value: `${profile.streakDays} 天` },
  ]

  return (
    <div>
      <div className="profile-header">
        <div className="profile-avatar-lg">{profile.displayName.slice(0, 1).toUpperCase()}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
            {profile.displayName}
          </p>
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)' }}>{profile.email}</p>
          <div style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 'var(--space-2)', flexWrap: 'wrap' }}>
            <Badge variant="fg">{profile.overallLevel} · 总体等级</Badge>
            {profile.isUpgradeCandidate ? <Badge variant="info">升级候选</Badge> : null}
          </div>
        </div>
        <button
          type="button"
          className="btn btn-secondary btn-sm"
          style={{ color: 'var(--danger)', borderColor: 'var(--danger)' }}
          onClick={logout}
        >
          <LogOut size={16} aria-hidden="true" />
          退出 ({user?.displayName})
        </button>
      </div>

      <div className="section-header">
        <h2>学习统计</h2>
      </div>
      <div className="stat-grid">
        {stats.map((stat) => (
          <div key={stat.label} className="stat-item">
            <div className="stat-num">{stat.value}</div>
            <div className="stat-desc">{stat.label}</div>
          </div>
        ))}
      </div>
    </div>
  )
}
