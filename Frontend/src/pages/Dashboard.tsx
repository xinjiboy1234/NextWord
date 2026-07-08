import type { LucideIcon } from 'lucide-react'
import {
  BookOpen,
  BookOpenText,
  GraduationCap,
  Keyboard,
  LineChart,
  PenLine,
  Repeat,
} from 'lucide-react'
import { Badge } from '../components/ui/Badge'
import { useDashboardStats, type ModuleBadge } from '../hooks/useDashboardStats'
import type { ProgressSummary } from '../types/models'
import type { DashboardView } from '../navigation/views'

interface DashboardItem {
  id: DashboardView
  label: string
  description: string
  icon: LucideIcon
  badge: ModuleBadge
}

interface DashboardProps {
  progress: ProgressSummary | null
  onNavigate: (view: DashboardView) => void
}

export function Dashboard({ progress, onNavigate }: DashboardProps) {
  const stats = useDashboardStats(progress)

  const items: DashboardItem[] = [
    {
      id: 'learn',
      label: '学习',
      description: '新词记忆，SM-2 间隔重复巩固词汇',
      icon: BookOpen,
      badge: stats.learn,
    },
    {
      id: 'spelling',
      label: '拼写',
      description: '听写与拼写练习，错误位置高亮',
      icon: Keyboard,
      badge: stats.spelling,
    },
    {
      id: 'sentence',
      label: '造句',
      description: 'AI 辅助造句，多维度评分反馈',
      icon: PenLine,
      badge: stats.sentence,
    },
    {
      id: 'reading',
      label: '阅读',
      description: '分级短文阅读，点词查义与词汇提取',
      icon: BookOpenText,
      badge: stats.reading,
    },
    {
      id: 'level',
      label: '等级',
      description: '查看各技能维度的 CEFR 等级',
      icon: GraduationCap,
      badge: stats.level,
    },
    {
      id: 'review',
      label: '复习',
      description: '浏览到期复习队列，翻转卡片复习',
      icon: Repeat,
      badge: stats.review,
    },
    {
      id: 'progress',
      label: '进度',
      description: '学习统计、连续打卡与活动记录',
      icon: LineChart,
      badge: stats.progress,
    },
  ]

  return (
    <div>
      <div className="welcome-strip">
        <h1>首页</h1>
        <p>选择模块开始今日练习。</p>
      </div>

      <div className="module-grid">
        {items.map((item, index) => {
          const Icon = item.icon
          return (
            <button
              key={item.id}
              type="button"
              className="module-card"
              onClick={() => onNavigate(item.id)}
              style={{
                animation: 'cardIn 0.4s var(--ease-spring) both',
                animationDelay: `${index * 60}ms`,
              }}
            >
              <div className="module-card-icon">
                <Icon size={22} aria-hidden="true" />
              </div>
              <h3>{item.label}</h3>
              <p>{item.description}</p>
              {!stats.loading ? (
                <Badge variant={item.badge.variant}>{item.badge.text}</Badge>
              ) : (
                <span className="skeleton skeleton-text short" style={{ width: 72, height: 20 }} />
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
