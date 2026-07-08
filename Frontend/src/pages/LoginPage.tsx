import { BookOpenText } from 'lucide-react'
import { useState } from 'react'
import { Tabs } from '../components/ui/Tabs'
import { useAuth } from '../contexts/AuthContext'

type Mode = 'login' | 'register'

export function LoginPage() {
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
    } catch {
      setError(mode === 'login' ? '登录失败，请检查邮箱和密码。' : '注册失败，邮箱可能已被使用。')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <div className="auth-brand">
          <BookOpenText size={32} style={{ color: 'var(--brand)' }} aria-hidden="true" />
          NextWord
        </div>

        <Tabs
          value={mode}
          onValueChange={(value) => setMode(value as Mode)}
          listClassName="tabs auth-tabs"
          items={[
            {
              value: 'login',
              label: '登录',
              panel: (
                <>
                  {error ? <div className="auth-error">{error}</div> : null}
                  <form className="auth-fields" onSubmit={handleSubmit}>
                    <div className="field">
                      <label htmlFor="email">邮箱</label>
                      <input
                        id="email"
                        type="email"
                        required
                        className="input"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                      />
                    </div>
                    <div className="field">
                      <label htmlFor="password">密码</label>
                      <input
                        id="password"
                        type="password"
                        required
                        minLength={6}
                        className="input"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                      />
                    </div>
                    <button type="submit" disabled={submitting} className="btn btn-primary auth-submit">
                      {submitting ? '提交中...' : '登录'}
                    </button>
                  </form>
                </>
              ),
            },
            {
              value: 'register',
              label: '注册',
              panel: (
                <>
                  {error ? <div className="auth-error">{error}</div> : null}
                  <form className="auth-fields" onSubmit={handleSubmit}>
                    <div className="field">
                      <label htmlFor="displayName">昵称</label>
                      <input
                        id="displayName"
                        type="text"
                        className="input"
                        value={displayName}
                        onChange={(event) => setDisplayName(event.target.value)}
                        placeholder="可选"
                      />
                    </div>
                    <div className="field">
                      <label htmlFor="email-register">邮箱</label>
                      <input
                        id="email-register"
                        type="email"
                        required
                        className="input"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                      />
                    </div>
                    <div className="field">
                      <label htmlFor="password-register">密码</label>
                      <input
                        id="password-register"
                        type="password"
                        required
                        minLength={6}
                        className="input"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                      />
                    </div>
                    <button type="submit" disabled={submitting} className="btn btn-primary auth-submit">
                      {submitting ? '提交中...' : '注册'}
                    </button>
                  </form>
                </>
              ),
            },
          ]}
        />
      </div>
    </div>
  )
}
