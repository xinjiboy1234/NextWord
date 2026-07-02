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

export type DashboardView = 'learn' | 'spelling' | 'sentence' | 'reading' | 'level' | 'review' | 'progress'

export type ManageView = 'assessment' | 'challenge' | 'home' | 'progress'

interface NavItem {
  id: AppView
  label: string
  icon: LucideIcon
}

export const SIDEBAR_LEARNING: NavItem[] = [
  { id: 'dashboard', label: '学习中心', icon: LayoutGrid },
  { id: 'learn', label: '学习', icon: BookOpen },
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

export const BOTTOM_TABS: { id: AppView; label: string; icon: LucideIcon }[] = [
  { id: 'dashboard', label: '学习', icon: LayoutGrid },
  { id: 'learn', label: '练习', icon: BookOpen },
  { id: 'reading', label: '阅读', icon: BookOpenText },
  { id: 'profile', label: '我的', icon: GraduationCap },
]

/** 底栏「我的」用 profile；图标在 AppShell 内替换为 User */
export const VIEW_TITLES: Partial<Record<AppView, string>> = {
  dashboard: '学习中心',
  learn: '背单词',
  spelling: '拼写练习',
  sentence: '造句工作室',
  reading: '阅读',
  assessment: '水平测评',
  challenge: '综合挑战',
  level: '等级',
  review: '复习队列',
  home: '词库',
  progress: '学习进度',
  profile: '个人主页',
  manage: '管理',
}
