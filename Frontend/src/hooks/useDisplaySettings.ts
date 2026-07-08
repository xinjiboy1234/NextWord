import { useCallback, useState } from 'react'

const SHOW_CEFR_KEY = 'nextword.settings.showCefr'

export function useDisplaySettings() {
  const [showCefr, setShowCefrState] = useState(
    () => localStorage.getItem(SHOW_CEFR_KEY) !== '0',
  )

  const setShowCefr = useCallback((value: boolean) => {
    localStorage.setItem(SHOW_CEFR_KEY, value ? '1' : '0')
    setShowCefrState(value)
  }, [])

  return { showCefr, setShowCefr }
}

export function formatLevelLabel(cefr: string | undefined | null, score: number | null | undefined, showCefr: boolean) {
  if (score != null && showCefr && cefr) {
    return `Score ${score.toFixed(0)} · CEFR ${cefr}`
  }
  if (score != null) {
    return `Score ${score.toFixed(0)}`
  }
  return cefr ? `CEFR ${cefr}` : '等级待测评'
}
