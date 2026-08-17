import {
  BarChart3,
  ChevronRight,
  ClipboardCheck,
  History,
  Layers,
  Settings,
  Trophy,
} from 'lucide-react'
import { useState } from 'react'
import { LlmSettingsDrawer } from '../components/LlmSettingsDrawer'
import type { ManageView } from '../navigation/views'

interface ManagePageProps {
  onNavigate: (view: ManageView) => void
}

// T-063：管理页重组为「学习工具」+「系统设置」两区——测评/挑战/词库等学习活动与
// LLM 系统配置不再混排，标题与描述按用户语义重写
const MANAGE_SECTIONS = [
  {
    label: '学习工具',
    items: [
      {
        id: 'assessment' as const,
        title: '水平测评',
        description: '自适应分块定级：以造句与情境表达为主，2–3 块出结果',
        icon: ClipboardCheck,
      },
      {
        id: 'assessments' as const,
        title: '测评记录',
        description: '历次测评结果与逐题评语',
        icon: History,
      },
      {
        id: 'challenge' as const,
        title: '综合挑战',
        description: '词汇、造句、阅读三阶段综合测试',
        icon: Trophy,
      },
      {
        id: 'home' as const,
        title: '词库',
        description: '全量词条列表，支持搜索与筛选',
        icon: Layers,
      },
      {
        id: 'progress' as const,
        title: '学习数据',
        description: '进度统计、连续打卡与学习记录',
        icon: BarChart3,
      },
    ],
  },
  {
    label: '系统设置',
    items: [
      {
        id: 'settings' as const,
        title: '模型服务配置',
        description: '模型提供商、API Key、模型配置（影响测评与评分质量）',
        icon: Settings,
      },
    ],
  },
]

export function ManagePage({ onNavigate }: ManagePageProps) {
  const [settingsOpen, setSettingsOpen] = useState(false)

  function handleItemClick(id: string) {
    if (id === 'settings') {
      setSettingsOpen(true)
      return
    }
    onNavigate(id as ManageView)
  }

  return (
    <>
      <div className="section-header">
        <h2>管理</h2>
        <p>学习工具与系统配置分开管理：测评/挑战/词库在学习工具区，模型服务在系统设置区。</p>
      </div>

      {MANAGE_SECTIONS.map((section) => (
        <div key={section.label} className="manage-section" style={{ marginBottom: 'var(--space-8)' }}>
          <p className="mono-label" style={{ marginBottom: 'var(--space-4)' }}>{section.label}</p>
          <div className="manage-card-grid">
            {section.items.map((item) => {
              const Icon = item.icon
              return (
                <button
                  key={item.id}
                  type="button"
                  className="manage-card"
                  onClick={() => handleItemClick(item.id)}
                >
                  <div className="manage-card-icon">
                    <Icon size={22} aria-hidden="true" />
                  </div>
                  <div className="manage-card-info">
                    <h3>{item.title}</h3>
                    <p>{item.description}</p>
                  </div>
                  <span className="manage-card-chevron">
                    <ChevronRight size={18} aria-hidden="true" />
                  </span>
                </button>
              )
            })}
          </div>
        </div>
      ))}

      <LlmSettingsDrawer open={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </>
  )
}
