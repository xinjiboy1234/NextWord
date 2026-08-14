import { useCallback, useState } from 'react'

const SPELLING_COUNT_KEY = 'nextword.settings.spellingCount'

/** T-051：拼写每组题量可选项（后端 /api/spelling/queue count 上限 20）。 */
export const SPELLING_COUNT_OPTIONS = [8, 12, 16, 20] as const
export const DEFAULT_SPELLING_COUNT = 12

function readStoredCount(): number {
  const raw = Number(localStorage.getItem(SPELLING_COUNT_KEY))
  return (SPELLING_COUNT_OPTIONS as readonly number[]).includes(raw) ? raw : DEFAULT_SPELLING_COUNT
}

/** 拼写每组题量选择（8/12/16/20，默认 12），持久化 localStorage。 */
export function useSpellingCount() {
  const [spellingCount, setSpellingCountState] = useState(readStoredCount)

  const setSpellingCount = useCallback((value: number) => {
    const next = (SPELLING_COUNT_OPTIONS as readonly number[]).includes(value)
      ? value
      : DEFAULT_SPELLING_COUNT
    localStorage.setItem(SPELLING_COUNT_KEY, String(next))
    setSpellingCountState(next)
  }, [])

  return { spellingCount, setSpellingCount }
}
