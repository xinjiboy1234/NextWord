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
  PRIMARY_NAV,
  VIEW_TITLES,
  isNavActive,
  type AppView,
} from '../../navigation/views'

const SIDEBAR_EXPANDED_KEY = 'nextword.sidebar.expanded'

interface AppShellProps {
  displayName: string
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

        <nav className="sidebar-nav" aria-label="主导航">
          {PRIMARY_NAV.map((item) => {
            const Icon = item.icon
            const active = isNavActive(view, item.id)
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
        </nav>
      </aside>

      <div className="main-area">
        <header className="topbar">
          {showBackButton ? (
            <button type="button" className="btn btn-ghost btn-sm" onClick={onBack}>
              <ChevronLeft size={16} aria-hidden="true" />
              返回
            </button>
          ) : (
            <span className="topbar-greeting">
              {getGreeting()}，<strong>{displayName}</strong>
              {!isDashboard && pageTitle ? ` · ${pageTitle}` : ''}
            </span>
          )}
          <div className="topbar-actions">
            {!isDashboard && showBackButton && pageTitle ? (
              <span className="topbar-greeting">{pageTitle}</span>
            ) : null}
          </div>
        </header>

        <main className="content">{children}</main>
      </div>

      <nav className="bottombar" aria-label="底部导航">
        <div className="bottombar-inner">
          {BOTTOM_TABS.map((tab) => {
            const Icon = BOTTOM_ICONS[tab.id] ?? tab.icon
            const active = isNavActive(view, tab.id)
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
