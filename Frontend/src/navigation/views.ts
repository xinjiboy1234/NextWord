import type { LucideIcon } from 'lucide-react'
import {
  BarChart3,
  BookOpen,
  BookOpenText,
  GraduationCap,
  Keyboard,
  LayoutGrid,
  PenLine,
  Repeat,
  Settings,
  User,
} from 'lucide-react'

export type AppView =
  | 'dashboard'
  | 'learn'
  | 'spelling'
  | 'sentence'
  | 'reading'
  | 'assessment'
  | 'challenge'
  | 'level'
  | 'review'
  | 'home'
  | 'progress'
  | 'profile'
  | 'manage'
  | 'assessments'

export type DashboardView = 'learn' | 'spelling' | 'sentence' | 'reading' | 'review'

export type ManageView = 'assessment' | 'assessments' | 'challenge' | 'home' | 'progress'

interface NavItem {
  id: AppView
  label: string
  icon: LucideIcon
}

/** 2C 主导航：首页 / 练习 / 阅读 / 我的 */
export const PRIMARY_NAV: NavItem[] = [
  { id: 'dashboard', label: '首页', icon: LayoutGrid },
  // { id: 'learn', label: '练习', icon: BookOpen },
  // { id: 'reading', label: '阅读', icon: BookOpenText },
  { id: 'profile', label: '我的', icon: User },
]

export const BOTTOM_TABS = PRIMARY_NAV

/** @deprecated 保留供 Dashboard 等引用 */
export const SIDEBAR_LEARNING: NavItem[] = [
  { id: 'dashboard', label: '学习中心', icon: LayoutGrid },
  { id: 'learn', label: '新词', icon: BookOpen },
  { id: 'spelling', label: '拼写', icon: Keyboard },
  { id: 'sentence', label: '造句', icon: PenLine },
  { id: 'reading', label: '阅读', icon: BookOpenText },
]

export const SIDEBAR_PROGRESS: NavItem[] = [
  { id: 'level', label: '等级', icon: GraduationCap },
  { id: 'review', label: '复习', icon: Repeat },
  { id: 'progress', label: '进度', icon: BarChart3 },
]

export const SIDEBAR_MANAGE: NavItem = { id: 'manage', label: '管理', icon: Settings }

export const VIEW_TITLES: Partial<Record<AppView, string>> = {
  dashboard: '首页',
  learn: '新词',
  spelling: '拼写练习',
  sentence: '造句工作室',
  reading: '阅读',
  assessment: '水平测评',
  challenge: '综合挑战',
  level: '等级',
  review: '复习队列',
  home: '词库',
  progress: '学习进度',
  profile: '我的',
  manage: '管理',
  assessments: '测评记录',
}

export function isPracticeView(view: AppView): boolean {
  return view === 'learn' || view === 'spelling' || view === 'sentence'
}

export function isNavActive(view: AppView, target: AppView): boolean {
  if (target === 'learn') return isPracticeView(view)
  if (target === 'reading') return view === 'reading'
  return view === target
}
