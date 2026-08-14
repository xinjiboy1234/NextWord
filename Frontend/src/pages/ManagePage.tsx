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

const MANAGE_SECTIONS = [
  {
    label: '系统',
    items: [
      {
        id: 'settings' as const,
        title: '系统设置',
        description: 'LLM 提供商、API Key、模型配置',
        icon: Settings,
      },
    ],
  },
  {
    label: '测评与挑战',
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
        description: '历次测评结果与逐题 AI 评语',
        icon: History,
      },
      {
        id: 'challenge' as const,
        title: '综合挑战',
        description: '词汇、造句、阅读三阶段综合测试',
        icon: Trophy,
      },
    ],
  },
  {
    label: '数据',
    items: [
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
        <p>系统配置、水平测评、挑战与词库数据。</p>
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
