import { useCallback, useState } from 'react'

const SPELLING_MODE_KEY = 'nextword.settings.spellingMode'

/** T-052：拼写队列模式（复习/新词/混合，默认混合；mixed 新旧 3:7，后端 /api/spelling/queue mode 参数）。 */
export type SpellingQueueMode = 'review' | 'new' | 'mixed'
export const SPELLING_MODE_OPTIONS: { value: SpellingQueueMode; label: string }[] = [
  { value: 'mixed', label: '混合' },
  { value: 'review', label: '复习' },
  { value: 'new', label: '新词' },
]
export const DEFAULT_SPELLING_MODE: SpellingQueueMode = 'mixed'

function readStoredMode(): SpellingQueueMode {
  const raw = localStorage.getItem(SPELLING_MODE_KEY)
  return SPELLING_MODE_OPTIONS.some((option) => option.value === raw)
    ? (raw as SpellingQueueMode)
    : DEFAULT_SPELLING_MODE
}

/** 拼写模式选择（复习/新词/混合，默认混合），持久化 localStorage。 */
export function useSpellingMode() {
  const [spellingMode, setSpellingModeState] = useState(readStoredMode)

  const setSpellingMode = useCallback((value: string) => {
    const next = SPELLING_MODE_OPTIONS.some((option) => option.value === value)
      ? (value as SpellingQueueMode)
      : DEFAULT_SPELLING_MODE
    localStorage.setItem(SPELLING_MODE_KEY, next)
    setSpellingModeState(next)
  }, [])

  return { spellingMode, setSpellingMode }
}
