import { useCallback, useState } from 'react'

const DAILY_WORD_COUNT_KEY = 'nextword.settings.dailyWordCount'

/** T-050：每日词量可选项（后端 /api/words/daily count 上限 20）。 */
export const DAILY_WORD_COUNT_OPTIONS = [10, 15, 20] as const
export const DEFAULT_DAILY_WORD_COUNT = 15

function readStoredCount(): number {
  const raw = Number(localStorage.getItem(DAILY_WORD_COUNT_KEY))
  return (DAILY_WORD_COUNT_OPTIONS as readonly number[]).includes(raw) ? raw : DEFAULT_DAILY_WORD_COUNT
}

/** 每日背词量选择（10/15/20，默认 15），持久化 localStorage。 */
export function useDailyWordCount() {
  const [dailyWordCount, setDailyWordCountState] = useState(readStoredCount)

  const setDailyWordCount = useCallback((value: number) => {
    const next = (DAILY_WORD_COUNT_OPTIONS as readonly number[]).includes(value)
      ? value
      : DEFAULT_DAILY_WORD_COUNT
    localStorage.setItem(DAILY_WORD_COUNT_KEY, String(next))
    setDailyWordCountState(next)
  }, [])

  return { dailyWordCount, setDailyWordCount }
}
