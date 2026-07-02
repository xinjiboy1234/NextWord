import {
  BookOpen,
  BookOpenText,
  ChevronLeft,
  ChevronRight,
  LayoutGrid,
  User,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { getGreeting } from '../../lib/greeting'
import { pathForView, viewFromPathname } from '../../navigation/routes'
import {
  BOTTOM_TABS,
  SIDEBAR_LEARNING,
  SIDEBAR_MANAGE,
  SIDEBAR_PROGRESS,
  VIEW_TITLES,
  type AppView,
} from '../../navigation/views'

const SIDEBAR_EXPANDED_KEY = 'nextword.sidebar.expanded'

interface AppShellProps {
  displayName: string
  overallLevel?: string
  overallScore?: number | null
  showBackButton?: boolean
  onBack: () => void
  children: React.ReactNode
}

const BOTTOM_ICONS: Record<string, typeof LayoutGrid> = {
  dashboard: LayoutGrid,
  learn: BookOpen,
  reading: BookOpenText,
  profile: User,
}

export function AppShell({
  displayName,
  overallLevel,
  overallScore,
  showBackButton,
  onBack,
  children,
}: AppShellProps) {
  const location = useLocation()
  const navigate = useNavigate()
  const view = viewFromPathname(location.pathname)

  const [collapsed, setCollapsed] = useState(
    () => localStorage.getItem(SIDEBAR_EXPANDED_KEY) !== '1',
  )

  useEffect(() => {
    localStorage.setItem(SIDEBAR_EXPANDED_KEY, collapsed ? '0' : '1')
  }, [collapsed])

  function onNavigate(target: AppView) {
    navigate(pathForView(target))
  }

  const pageTitle = VIEW_TITLES[view]
  const isDashboard = view === 'dashboard'

  return (
    <div className="app-shell">
      <aside className={`sidebar${collapsed ? ' collapsed' : ''}`}>
        <button
          type="button"
          className="sidebar-toggle"
          title={collapsed ? '展开侧栏' : '收起侧栏'}
          onClick={() => setCollapsed((value) => !value)}
        >
          {collapsed ? (
            <ChevronRight size={14} aria-hidden="true" />
          ) : (
            <ChevronLeft size={14} aria-hidden="true" />
          )}
        </button>

        <div className="sidebar-brand">
          <BookOpenText size={28} className="text-[var(--brand)]" aria-hidden="true" />
          <span>NextWord</span>
        </div>

        <nav className="sidebar-nav" aria-label="学习导航">
          <p className="sidebar-section-label">学习</p>
          {SIDEBAR_LEARNING.map((item) => {
            const Icon = item.icon
            const active = view === item.id
            return (
              <button
                key={item.id}
                type="button"
                className={active ? 'active' : undefined}
                onClick={() => onNavigate(item.id)}
              >
                <Icon size={20} aria-hidden="true" />
                <span>{item.label}</span>
              </button>
            )
          })}

          <p className="sidebar-section-label">进度</p>
          {SIDEBAR_PROGRESS.map((item) => {
            const Icon = item.icon
            return (
              <button
                key={item.id}
                type="button"
                className={view === item.id ? 'active' : undefined}
                onClick={() => onNavigate(item.id)}
              >
                <Icon size={20} aria-hidden="true" />
                <span>{item.label}</span>
              </button>
            )
          })}

          <div className="sidebar-divider" />

          <div className="sidebar-nav-manage">
            <button
              type="button"
              className={view === SIDEBAR_MANAGE.id ? 'active' : undefined}
              onClick={() => onNavigate(SIDEBAR_MANAGE.id)}
            >
              <SIDEBAR_MANAGE.icon size={20} aria-hidden="true" />
              <span>{SIDEBAR_MANAGE.label}</span>
            </button>
          </div>
        </nav>

        <div className="sidebar-footer">
          <button
            type="button"
            className="sidebar-user"
            onClick={() => onNavigate('profile')}
          >
            <div className="sidebar-avatar">{displayName.slice(0, 1).toUpperCase()}</div>
            <div className="sidebar-user-info">
              <div className="sidebar-user-name">
                <span>{displayName}</span>
              </div>
              <div className="sidebar-user-level">
                <span>
                  {overallLevel ? `CEFR ${overallLevel}` : '等级待测评'}
                  {overallScore != null ? ` · Score ${overallScore.toFixed(0)}` : ''}
                </span>
              </div>
            </div>
          </button>
        </div>
      </aside>

      <div className="main-area">
        <header className="topbar">
          {showBackButton ? (
            <button type="button" className="btn btn-ghost btn-sm" onClick={onBack}>
              <ChevronLeft size={16} aria-hidden="true" />
              返回首页
            </button>
          ) : (
            <span className="topbar-greeting">
              {getGreeting()}，<strong>{displayName}</strong>
              {!isDashboard && pageTitle ? ` · ${pageTitle}` : ' · 今天也要坚持学习'}
            </span>
          )}
          <div className="topbar-actions">
            {!isDashboard && showBackButton && pageTitle ? (
              <span className="topbar-greeting">{pageTitle}</span>
            ) : null}
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={() => onNavigate('profile')}
            >
              个人主页
            </button>
          </div>
        </header>

        <main className="content">{children}</main>
      </div>

      <nav className="bottombar" aria-label="底部导航">
        <div className="bottombar-inner">
          {BOTTOM_TABS.map((tab) => {
            const Icon = BOTTOM_ICONS[tab.id] ?? tab.icon
            const active =
              view === tab.id
              || (tab.id === 'reading' && view === 'reading')
              || (tab.id === 'learn' && (view === 'learn' || view === 'spelling' || view === 'sentence'))
            return (
              <button
                key={tab.id}
                type="button"
                className={`bottombar-tab${active ? ' active' : ''}`}
                onClick={() => onNavigate(tab.id)}
              >
                <Icon size={24} aria-hidden="true" />
                {tab.label}
              </button>
            )
          })}
        </div>
      </nav>
    </div>
  )
}
