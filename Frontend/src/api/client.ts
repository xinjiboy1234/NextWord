import axios from 'axios'

const TOKEN_KEY = 'nextword.auth.token'
const USER_KEY = 'nextword.auth.user'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  timeout: 12000,
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const url = String(error.config?.url ?? '')
    const isAuthEndpoint = url.includes('/api/auth/login') || url.includes('/api/auth/register')
    if (status === 401 && !isAuthEndpoint) {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(USER_KEY)
    }
    return Promise.reject(error)
  },
)
