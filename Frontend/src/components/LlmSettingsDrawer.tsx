import { Save } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { LlmPreset, UserLlmSettings, UserProfile } from '../types/auth'
import { Drawer } from './ui/Drawer'

interface LlmSettingsDrawerProps {
  open: boolean
  onClose: () => void
  /** T-070：配置场景标题（首次测评前置配置为「连接模型服务」，管理页保持「系统设置」） */
  title?: string
  /** T-070：打开时预选的服务商（仅用户尚未保存过设置时生效） */
  initialPresetId?: string
  /** T-070：配置场景引导语（管理页不传则使用底部默认说明） */
  intro?: string
}

export function LlmSettingsDrawer({ open, onClose, title = '系统设置', initialPresetId, intro }: LlmSettingsDrawerProps) {
  const [presets, setPresets] = useState<LlmPreset[]>([])
  const [presetId, setPresetId] = useState('openai')
  const [provider, setProvider] = useState('OpenAI')
  const [baseUrl, setBaseUrl] = useState('')
  const [model, setModel] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [hasApiKey, setHasApiKey] = useState(false)
  const [maskedApiKey, setMaskedApiKey] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)

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
    if (!open) return

    async function loadProfile() {
      try {
        const response = await api.get<UserProfile>(endpoints.profile)
        const settings = response.data.llmSettings as UserLlmSettings | null
        if (settings) {
          setProvider(settings.provider)
          setBaseUrl(settings.baseUrl)
          setModel(settings.model)
          setHasApiKey(settings.hasApiKey)
          setMaskedApiKey(settings.maskedApiKey)
        } else if (initialPresetId) {
          // T-070：首次配置场景——打开即预选用户在欢迎卡上选中的服务商
          applyPreset(initialPresetId)
        }
      } catch {
        // 占位：加载失败时保留表单默认值
      }
    }

    void loadProfile()
  }, [open])

  function applyPreset(id: string) {
    setPresetId(id)
    const preset = presets.find((item) => item.id === id)
    if (!preset) return
    setProvider(preset.provider)
    setBaseUrl(preset.baseUrl)
    setModel(preset.defaultModel)
  }

  async function save() {
    setSaving(true)
    setSaveMessage(null)
    try {
      await api.put<UserLlmSettings>(endpoints.profileLlm, {
        presetId,
        provider,
        baseUrl,
        model,
        apiKey: apiKey.trim() || undefined,
      })
      setApiKey('')
      setHasApiKey(true)
      setSaveMessage('已保存')
      setTimeout(() => {
        onClose()
        setSaveMessage(null)
      }, 800)
    } catch {
      setSaveMessage('保存失败，请稍后重试。')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Drawer
      open={open}
      title={title}
      onClose={onClose}
      footer={(
        <button
          type="button"
          className="btn btn-primary"
          style={{ width: '100%' }}
          disabled={saving}
          onClick={() => void save()}
        >
          <Save size={16} aria-hidden="true" />
          {saving ? '保存中...' : '保存'}
        </button>
      )}
    >
      <div className="stack stack-md">
        {saveMessage ? <p className="text-sm">{saveMessage}</p> : null}
        {intro ? <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)', lineHeight: 1.6 }}>{intro}</p> : null}
        <div className="field">
          <label htmlFor="llm-preset">预设</label>
          <select
            id="llm-preset"
            className="select"
            value={presetId}
            onChange={(event) => applyPreset(event.target.value)}
          >
            {presets.map((preset) => (
              <option key={preset.id} value={preset.id}>{preset.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="llm-provider">类型</label>
          <input
            id="llm-provider"
            className="input"
            value={provider}
            onChange={(event) => setProvider(event.target.value)}
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="llm-model">模型</label>
          <input
            id="llm-model"
            className="input"
            value={model}
            onChange={(event) => setModel(event.target.value)}
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="llm-base-url">API URL</label>
          <input
            id="llm-base-url"
            className="input"
            value={baseUrl}
            onChange={(event) => setBaseUrl(event.target.value)}
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="llm-api-key">API Key</label>
          <input
            id="llm-api-key"
            className="input"
            type="password"
            value={apiKey}
            onChange={(event) => setApiKey(event.target.value)}
            placeholder={hasApiKey && maskedApiKey ? `已配置 (${maskedApiKey})` : '输入 API Key'}
            autoComplete="off"
          />
        </div>
        <p className="text-sm" style={{ color: 'var(--muted)' }}>
          配置后，造句评分、阅读辅助等将使用你的 API Key（未配置则使用服务端默认）。
        </p>
      </div>
    </Drawer>
  )
}
