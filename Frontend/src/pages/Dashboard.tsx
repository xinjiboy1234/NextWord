import type { LucideIcon } from 'lucide-react'
import { BookOpen, BookOpenText, GraduationCap, Keyboard, LineChart, PenLine, Repeat } from 'lucide-react'

export type DashboardView = 'learn' | 'spelling' | 'sentence' | 'reading' | 'level' | 'review' | 'progress'

interface DashboardItem {
  id: DashboardView
  label: string
  description: string
  icon: LucideIcon
}

const DASHBOARD_ITEMS: DashboardItem[] = [
  { id: 'learn', label: '学习', description: '新词记忆，巩固词汇', icon: BookOpen },
  { id: 'spelling', label: '拼写', description: '听写与拼写练习', icon: Keyboard },
  { id: 'sentence', label: '造句', description: 'AI 辅助造句评分', icon: PenLine },
  { id: 'reading', label: '阅读', description: '分级短文阅读', icon: BookOpenText },
  { id: 'level', label: '等级', description: '查看各维度等级', icon: GraduationCap },
  { id: 'review', label: '复习', description: '待复习词汇队列', icon: Repeat },
  { id: 'progress', label: '进度', description: '学习统计与记录', icon: LineChart },
]

interface DashboardProps {
  onNavigate: (view: DashboardView) => void
}

export function Dashboard({ onNavigate }: DashboardProps) {
  return (
    <div className="grid gap-5">
      <section>
        <h2 className="text-2xl font-semibold">学习中心</h2>
        <p className="mt-1 text-sm text-neutral-600">选择功能开始学习</p>
      </section>

      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {DASHBOARD_ITEMS.map((item) => {
          const Icon = item.icon
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onNavigate(item.id)}
              className="group rounded-md border border-neutral-200 bg-white p-5 text-left transition hover:border-emerald-300 hover:shadow-sm"
            >
              <div className="flex items-start gap-4">
                <div className="grid h-12 w-12 shrink-0 place-items-center rounded-md bg-emerald-50 text-emerald-700 transition group-hover:bg-emerald-100">
                  <Icon size={24} aria-hidden="true" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold">{item.label}</h3>
                  <p className="mt-1 text-sm text-neutral-600">{item.description}</p>
                </div>
              </div>
            </button>
          )
        })}
      </section>
    </div>
  )
}
