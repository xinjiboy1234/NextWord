import type { LucideIcon } from 'lucide-react'
import {
  BookOpen,
  BookOpenText,
  CalendarDays,
  Keyboard,
  Lightbulb,
  PenLine,
  Repeat,
} from 'lucide-react'
import { Badge } from '../components/ui/Badge'
import { useDashboardStats, type ModuleBadge } from '../hooks/useDashboardStats'
import { useLearningPlan } from '../hooks/useLearningPlan'
import { useBottleneckInsight } from '../hooks/useBottleneckInsight'
import { useGraduations } from '../hooks/useGraduations'
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
  const plan = useLearningPlan()
  const insight = useBottleneckInsight()
  const graduations = useGraduations()

  // T-018/T-019：加载中与请求失败（静默降级）都不展示对应卡片
  const showPlanCard = plan.status === 'active' || plan.status === 'none'
  const showInsightCard = insight.status === 'found' || insight.status === 'none'

  const items: DashboardItem[] = [
    {
      id: 'learn',
      label: '新词',
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
      id: 'review',
      label: '复习',
      description: '浏览到期复习队列，翻转卡片复习',
      icon: Repeat,
      badge: stats.review,
    },
  ]

  return (
    <div>
      <div className="welcome-strip">
        <h1>首页</h1>
        <p>选择模块开始今日练习。</p>
      </div>

      {(showPlanCard || showInsightCard) && (
        <div className="dashboard-info-grid">
          {showPlanCard && (
            <section className="card dashboard-info-card">
              <div className="dashboard-info-card-head">
                <h3>
                  <CalendarDays size={16} aria-hidden="true" />
                  今日学习计划
                </h3>
                {plan.status === 'active' && (
                  <Badge variant={plan.personalized ? 'info' : 'muted'}>
                    {plan.personalized ? '个性化·依据你的弱点画像' : '探索期·积累数据后更精准'}
                  </Badge>
                )}
              </div>
              {plan.status === 'active' ? (
                <div className="stack stack-sm">
                  <p className="dashboard-info-title">
                    {plan.focusScenarioNames.slice(0, 2).join('、') || '综合练习'}
                    {' · '}第 {plan.dayIndex + 1}/7 天
                  </p>
                  <p className="dashboard-info-meta">
                    带内词 {plan.todayWordCount} 个 · 接触词 {plan.todayExposureCount} 个
                  </p>
                  {plan.todaySentenceTargets.length > 0 && (
                    <p className="dashboard-info-meta">
                      造句目标：{plan.todaySentenceTargets.join('、')}
                    </p>
                  )}
                </div>
              ) : (
                <p className="dashboard-info-empty">
                  完成初始测评后，AI 将为你生成个性化学习计划。
                </p>
              )}
              {/* T-034：本周毕业计数（无毕业不显示） */}
              {graduations.weeklyCount > 0 && (
                <p className="dashboard-info-meta" style={{ marginTop: 'var(--space-2)' }}>
                  🎓 本周毕业 {graduations.weeklyCount} 个词
                </p>
              )}
            </section>
          )}

          {showInsightCard && (
            <section className="card dashboard-info-card">
              <div className="dashboard-info-card-head">
                <h3>
                  <Lightbulb size={16} aria-hidden="true" />
                  学习洞察
                </h3>
                {insight.status === 'found' && insight.replanTriggered && (
                  <Badge variant="success">已为你调整学习计划</Badge>
                )}
              </div>
              {insight.status === 'found' ? (
                <div className="stack stack-sm">
                  <p className="dashboard-info-title">
                    {insight.natureName}
                    {insight.natureHint ? ` · ${insight.natureHint}` : ''}
                  </p>
                  {insight.statement && (
                    <p className="dashboard-info-meta">{insight.statement}</p>
                  )}
                  <p className="dashboard-info-time">
                    {new Date(insight.createdAt).toLocaleString('zh-CN')}
                  </p>
                </div>
              ) : (
                <p className="dashboard-info-empty">
                  近期学习状态良好，未发现明显瓶颈。
                </p>
              )}
            </section>
          )}
        </div>
      )}

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
