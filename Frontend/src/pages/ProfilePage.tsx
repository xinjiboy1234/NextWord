import { BarChart3, ChevronRight, ClipboardCheck, LogOut, Settings } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { LevelPanel } from '../components/LevelPanel'
import { MonthlyTimelinePanel } from '../components/MonthlyTimelinePanel'
import { ProgressDetail } from '../components/ProgressDetail'
import { Badge } from '../components/ui/Badge'
import { Switch } from '../components/ui/Switch'
import { useAuth } from '../contexts/AuthContext'
import { useDisplaySettings } from '../hooks/useDisplaySettings'
import type { UserProfile } from '../types/auth'

export function ProfilePage() {
  const { logout, user } = useAuth()
  const { showCefr, setShowCefr } = useDisplaySettings()
  const location = useLocation()
  const navigate = useNavigate()
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

  useEffect(() => {
    const hash = location.hash.slice(1)
    if (!hash) return
    const timer = window.setTimeout(() => {
      document.getElementById(hash)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }, 100)
    return () => window.clearTimeout(timer)
  }, [location.hash, loading])


  function scrollToProgress() {
    document.getElementById('profile-progress')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  if (loading) {
    return <p className="text-sm" style={{ color: 'var(--muted)' }}>正在加载个人主页...</p>
  }

  if (error || !profile) {
    return <div className="alert alert-error">{error ?? '暂无数据。'}</div>
  }

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

      <div id="profile-level" className="section-header" style={{ marginTop: 'var(--space-8)' }}>
        <h2>等级</h2>
      </div>
      <LevelPanel />

      <div className="section-header" style={{ marginTop: 'var(--space-8)' }}>
        <h2>我的这个月</h2>
      </div>
      <MonthlyTimelinePanel />

      <div id="profile-progress" className="section-header" style={{ marginTop: 'var(--space-8)' }}>
        <h2>学习进度</h2>
      </div>
      <ProgressDetail data={profile} />

      <div className="section-header" style={{ marginTop: 'var(--space-8)' }}>
        <h2>显示设置</h2>
      </div>
      <div className="card stack stack-sm">
        <label className="row-between" style={{ cursor: 'pointer' }}>
          <span style={{ fontSize: 'var(--text-sm)' }}>显示 CEFR 等级标签</span>
          <Switch
            checked={showCefr}
            onCheckedChange={setShowCefr}
            aria-label="显示 CEFR 等级标签"
          />
        </label>
        <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
          关闭后侧栏与等级页优先展示 Score 数值。
        </p>
      </div>

      {/* T-063：快捷入口区（替代孤立「高级→管理后台」链接）——测评记录 / 学习数据 / 系统设置 */}
      <div className="section-header" style={{ marginTop: 'var(--space-8)' }}>
        <h2>快捷入口</h2>
      </div>
      <div className="manage-card-grid">
        <button type="button" className="manage-card" onClick={() => navigate('/assessments')}>
          <div className="manage-card-icon"><ClipboardCheck size={22} aria-hidden="true" /></div>
          <div className="manage-card-info">
            <h3>测评记录</h3>
            <p>历次测评结果与逐题评语</p>
          </div>
          <span className="manage-card-chevron"><ChevronRight size={18} aria-hidden="true" /></span>
        </button>
        <button type="button" className="manage-card" onClick={() => scrollToProgress()}>
          <div className="manage-card-icon"><BarChart3 size={22} aria-hidden="true" /></div>
          <div className="manage-card-info">
            <h3>学习数据</h3>
            <p>进度统计、连续打卡与学习记录</p>
          </div>
          <span className="manage-card-chevron"><ChevronRight size={18} aria-hidden="true" /></span>
        </button>
        <button type="button" className="manage-card" onClick={() => navigate('/manage')}>
          <div className="manage-card-icon"><Settings size={22} aria-hidden="true" /></div>
          <div className="manage-card-info">
            <h3>系统设置</h3>
            <p>模型提供商、API Key、模型配置</p>
          </div>
          <span className="manage-card-chevron"><ChevronRight size={18} aria-hidden="true" /></span>
        </button>
      </div>
    </div>
  )
}
