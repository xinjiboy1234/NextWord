import { BarChart3, Bot, CalendarDays, ClipboardCheck, Layers, LogOut, Save, Target, Trophy } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { useAuth } from '../contexts/AuthContext'
import { LoginPage } from './LoginPage'
import type { LlmPreset, UserLlmSettings, UserProfile } from '../types/auth'

type ProfileMenuView = 'assessment' | 'challenge' | 'home'

interface ProfilePageProps {
  onNavigate?: (view: ProfileMenuView) => void
}

const PROFILE_MENU_ITEMS: { id: ProfileMenuView; label: string; description: string; icon: typeof ClipboardCheck }[] = [
  { id: 'assessment', label: '测评', description: '首次水平测评与重新定级', icon: ClipboardCheck },
  { id: 'challenge', label: '挑战', description: '词汇、造句、阅读综合挑战', icon: Trophy },
  { id: 'home', label: '词库', description: '查看核心词汇列表', icon: Layers },
]

export function ProfilePage({ onNavigate }: ProfilePageProps) {
  const { isAuthenticated, logout, user } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [presets, setPresets] = useState<LlmPreset[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)

  const [presetId, setPresetId] = useState('openai')
  const [provider, setProvider] = useState('OpenAI')
  const [baseUrl, setBaseUrl] = useState('')
  const [model, setModel] = useState('')
  const [apiKey, setApiKey] = useState('')

  useEffect(() => {
    async function loadPresets() {
      try {
        const response = await api.get<LlmPreset[]>(endpoints.llmPresets)
        setPresets(response.data)
      } catch {
        setPresets([])
      }
    }

    void loadPresets()
  }, [])

  useEffect(() => {
    if (!isAuthenticated) {
      setLoading(false)
      return
    }

    async function loadProfile() {
      setLoading(true)
      setError(null)
      try {
        const response = await api.get<UserProfile>(endpoints.profile)
        setProfile(response.data)
        const settings = response.data.llmSettings
        if (settings) {
          setProvider(settings.provider)
          setBaseUrl(settings.baseUrl)
          setModel(settings.model)
        }
      } catch {
        setError('个人主页加载失败。')
      } finally {
        setLoading(false)
      }
    }

    void loadProfile()
  }, [isAuthenticated])

  function applyPreset(id: string) {
    setPresetId(id)
    const preset = presets.find((item) => item.id === id)
    if (!preset) {
      return
    }
    setProvider(preset.provider)
    setBaseUrl(preset.baseUrl)
    setModel(preset.defaultModel)
  }

  async function saveLlmSettings() {
    setSaving(true)
    setSaveMessage(null)
    try {
      const response = await api.put<UserLlmSettings>(endpoints.profileLlm, {
        presetId,
        provider,
        baseUrl,
        model,
        apiKey: apiKey.trim() || undefined,
      })
      setApiKey('')
      setSaveMessage('LLM 设置已保存。')
      setProfile((current) => current ? { ...current, llmSettings: response.data } : current)
    } catch {
      setSaveMessage('保存失败，请稍后重试。')
    } finally {
      setSaving(false)
    }
  }

  if (!isAuthenticated) {
    return (
      <div className="grid gap-4">
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h2 className="text-xl font-semibold">个人主页</h2>
          <p className="mt-1 text-sm text-neutral-600">登录后可查看学习进度并配置个人 LLM。</p>
        </section>
        <LoginPage />
      </div>
    )
  }

  if (loading) {
    return <div className="rounded-md border border-neutral-200 bg-white p-6 text-sm text-neutral-600">正在加载个人主页...</div>
  }

  if (error || !profile) {
    return <div className="rounded-md border border-rose-200 bg-rose-50 p-6 text-sm text-rose-900">{error ?? '暂无数据。'}</div>
  }

  const stats = [
    { label: '已学词', value: profile.totalLearned, icon: Target },
    { label: '待复习', value: profile.dueReviews, icon: CalendarDays },
    { label: '正确率', value: `${profile.accuracyPercent}%`, icon: BarChart3 },
  ]

  const dimensions = [
    { label: '词汇', value: profile.vocabLevel },
    { label: '拼写', value: profile.spellingLevel },
    { label: '造句', value: profile.sentenceLevel },
    { label: '阅读', value: profile.readingLevel },
  ]

  return (
    <div className="grid gap-5">
      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-2xl font-semibold">{profile.displayName}</h2>
            <p className="mt-1 text-sm text-neutral-600">{profile.email}</p>
            <p className="mt-2 text-sm">
              总体等级 <span className="font-semibold">{profile.overallLevel}</span>
              {profile.isUpgradeCandidate ? ' · 升级候选' : ''}
            </p>
          </div>
          <button
            type="button"
            onClick={logout}
            className="inline-flex items-center gap-2 rounded-md border border-neutral-200 px-3 py-2 text-sm hover:bg-neutral-50"
          >
            <LogOut size={16} aria-hidden="true" />
            退出 ({user?.displayName})
          </button>
        </div>
      </section>

      {onNavigate && (
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h3 className="text-lg font-semibold">更多功能</h3>
          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            {PROFILE_MENU_ITEMS.map((item) => {
              const Icon = item.icon
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => onNavigate(item.id)}
                  className="rounded-md border border-neutral-200 p-4 text-left transition hover:border-emerald-300 hover:bg-neutral-50"
                >
                  <Icon size={20} className="text-emerald-700" aria-hidden="true" />
                  <p className="mt-2 font-semibold">{item.label}</p>
                  <p className="mt-1 text-sm text-neutral-600">{item.description}</p>
                </button>
              )
            })}
          </div>
        </section>
      )}

      <section className="grid gap-3 sm:grid-cols-3">
        {stats.map((stat) => {
          const Icon = stat.icon
          return (
            <article key={stat.label} className="rounded-md border border-neutral-200 bg-white p-5">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium text-neutral-600">{stat.label}</p>
                <Icon size={20} className="text-emerald-700" aria-hidden="true" />
              </div>
              <p className="mt-3 text-3xl font-semibold">{stat.value}</p>
            </article>
          )
        })}
      </section>

      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <h3 className="text-lg font-semibold">等级详情</h3>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {dimensions.map((item) => (
            <article key={item.label} className="rounded-md border border-neutral-100 p-3">
              <p className="text-sm text-neutral-600">{item.label}</p>
              <p className="mt-1 text-xl font-semibold">{item.value}</p>
            </article>
          ))}
        </div>
        <dl className="mt-4 grid gap-2 text-sm">
          <div className="flex justify-between border-b border-neutral-100 pb-2">
            <dt className="text-neutral-600">连续学习</dt>
            <dd className="font-semibold">{profile.streakDays} 天</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-neutral-600">学习记录</dt>
            <dd className="font-semibold">{profile.totalLogs} 条</dd>
          </div>
        </dl>
      </section>

      {profile.recentHistory.length > 0 && (
        <section className="rounded-md border border-neutral-200 bg-white p-5">
          <h3 className="text-lg font-semibold">等级历史</h3>
          <div className="mt-3 space-y-2">
            {profile.recentHistory.map((item) => (
              <div key={item.id} className="rounded-md border border-neutral-100 px-3 py-2 text-sm">
                {item.fromLevel} → {item.toLevel} · {item.reason}
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex items-center gap-2">
          <Bot size={20} className="text-emerald-700" aria-hidden="true" />
          <h3 className="text-lg font-semibold">LLM 配置</h3>
        </div>
        <p className="mt-1 text-sm text-neutral-600">配置后，造句评分、阅读辅助等将使用你的 API Key（未配置则使用服务端默认）。</p>

        <div className="mt-4 grid gap-4">
          <label className="grid gap-1 text-sm">
            <span className="font-medium">预设</span>
            <select
              value={presetId}
              onChange={(event) => applyPreset(event.target.value)}
              className="rounded-md border border-neutral-300 px-3 py-2"
            >
              {presets.map((preset) => (
                <option key={preset.id} value={preset.id}>{preset.name}</option>
              ))}
            </select>
          </label>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="grid gap-1 text-sm">
              <span className="font-medium">类型</span>
              <input
                type="text"
                value={provider}
                onChange={(event) => setProvider(event.target.value)}
                className="rounded-md border border-neutral-300 px-3 py-2"
              />
            </label>
            <label className="grid gap-1 text-sm">
              <span className="font-medium">模型</span>
              <input
                type="text"
                value={model}
                onChange={(event) => setModel(event.target.value)}
                className="rounded-md border border-neutral-300 px-3 py-2"
              />
            </label>
          </div>

          <label className="grid gap-1 text-sm">
            <span className="font-medium">API URL</span>
            <input
              type="url"
              value={baseUrl}
              onChange={(event) => setBaseUrl(event.target.value)}
              className="rounded-md border border-neutral-300 px-3 py-2"
            />
          </label>

          <label className="grid gap-1 text-sm">
            <span className="font-medium">API Key</span>
            <input
              type="password"
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
              placeholder={profile.llmSettings?.hasApiKey ? `已配置 (${profile.llmSettings.maskedApiKey})` : '输入 API Key'}
              className="rounded-md border border-neutral-300 px-3 py-2"
            />
          </label>

          {saveMessage && <p className="text-sm text-neutral-700">{saveMessage}</p>}

          <button
            type="button"
            disabled={saving}
            onClick={() => void saveLlmSettings()}
            className="inline-flex w-fit items-center gap-2 rounded-md bg-emerald-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
          >
            <Save size={16} aria-hidden="true" />
            {saving ? '保存中...' : '保存 LLM 设置'}
          </button>
        </div>
      </section>
    </div>
  )
}
