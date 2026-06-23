import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { AuthResult, AuthUser } from '../types/auth'

const TOKEN_KEY = 'nextword.auth.token'
const USER_KEY = 'nextword.auth.user'

interface AuthContextValue {
  user: AuthUser | null
  token: string | null
  isAuthenticated: boolean
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  logout: () => void
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? JSON.parse(raw) as AuthUser : null
  })
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY))
  const [loading, setLoading] = useState(true)

  const persist = useCallback((nextToken: string | null, nextUser: AuthUser | null) => {
    setToken(nextToken)
    setUser(nextUser)
    if (nextToken) {
      localStorage.setItem(TOKEN_KEY, nextToken)
    } else {
      localStorage.removeItem(TOKEN_KEY)
    }
    if (nextUser) {
      localStorage.setItem(USER_KEY, JSON.stringify(nextUser))
    } else {
      localStorage.removeItem(USER_KEY)
    }
  }, [])

  const refreshUser = useCallback(async () => {
    if (!token) {
      return
    }
    try {
      const response = await api.get<AuthUser>(endpoints.authMe)
      persist(token, response.data)
    } catch {
      persist(null, null)
    }
  }, [persist, token])

  useEffect(() => {
    async function bootstrap() {
      if (!token) {
        setLoading(false)
        return
      }
      try {
        const response = await api.get<AuthUser>(endpoints.authMe)
        persist(token, response.data)
      } catch {
        persist(null, null)
      } finally {
        setLoading(false)
      }
    }

    void bootstrap()
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const login = useCallback(async (email: string, password: string) => {
    const response = await api.post<AuthResult>(endpoints.authLogin, { email, password })
    persist(response.data.token, response.data.user)
  }, [persist])

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const response = await api.post<AuthResult>(endpoints.authRegister, { email, password, displayName })
    persist(response.data.token, response.data.user)
  }, [persist])

  const logout = useCallback(() => {
    persist(null, null)
  }, [persist])

  const value = useMemo(
    () => ({
      user,
      token,
      isAuthenticated: Boolean(token && user),
      loading,
      login,
      register,
      logout,
      refreshUser,
    }),
    [user, token, loading, login, register, logout, refreshUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return context
}
