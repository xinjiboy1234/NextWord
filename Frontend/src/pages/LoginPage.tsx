import { LogIn, UserPlus } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '../contexts/AuthContext'

type Mode = 'login' | 'register'

interface LoginPageProps {
  onSuccess?: () => void
}

export function LoginPage({ onSuccess }: LoginPageProps) {
  const { login, register } = useAuth()
  const [mode, setMode] = useState<Mode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      if (mode === 'login') {
        await login(email, password)
      } else {
        await register(email, password, displayName || email)
      }
      onSuccess?.()
    } catch {
      setError(mode === 'login' ? '登录失败，请检查邮箱和密码。' : '注册失败，邮箱可能已被使用。')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-md rounded-md border border-neutral-200 bg-white p-6">
      <div className="mb-6 flex gap-2">
        <button
          type="button"
          onClick={() => setMode('login')}
          className={`flex-1 rounded-md border px-3 py-2 text-sm font-medium ${
            mode === 'login' ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-neutral-200'
          }`}
        >
          <span className="inline-flex items-center gap-2 justify-center w-full">
            <LogIn size={16} aria-hidden="true" />
            登录
          </span>
        </button>
        <button
          type="button"
          onClick={() => setMode('register')}
          className={`flex-1 rounded-md border px-3 py-2 text-sm font-medium ${
            mode === 'register' ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-neutral-200'
          }`}
        >
          <span className="inline-flex items-center gap-2 justify-center w-full">
            <UserPlus size={16} aria-hidden="true" />
            注册
          </span>
        </button>
      </div>

      <form className="grid gap-4" onSubmit={handleSubmit}>
        {mode === 'register' && (
          <label className="grid gap-1 text-sm">
            <span className="font-medium text-neutral-700">昵称</span>
            <input
              type="text"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              className="rounded-md border border-neutral-300 px-3 py-2"
              placeholder="可选"
            />
          </label>
        )}
        <label className="grid gap-1 text-sm">
          <span className="font-medium text-neutral-700">邮箱</span>
          <input
            type="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="rounded-md border border-neutral-300 px-3 py-2"
          />
        </label>
        <label className="grid gap-1 text-sm">
          <span className="font-medium text-neutral-700">密码</span>
          <input
            type="password"
            required
            minLength={6}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="rounded-md border border-neutral-300 px-3 py-2"
          />
        </label>
        {error && <p className="text-sm text-rose-700">{error}</p>}
        <button
          type="submit"
          disabled={submitting}
          className="rounded-md bg-emerald-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {submitting ? '提交中...' : mode === 'login' ? '登录' : '注册'}
        </button>
      </form>
    </div>
  )
}
