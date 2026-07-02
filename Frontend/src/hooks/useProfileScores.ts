import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { EvaluationReportContent, EvaluationReportRecord, UserProfileScores } from '../types/score'

export function useProfileScores() {
  const [scores, setScores] = useState<UserProfileScores | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const { data } = await api.get<UserProfileScores>(endpoints.profileScores)
      setScores(data)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return { scores, loading, refresh }
}

export function useEvaluationReport(pollKey?: number | null) {
  const [report, setReport] = useState<EvaluationReportRecord | null>(null)
  const [content, setContent] = useState<EvaluationReportContent | null>(null)

  useEffect(() => {
    if (pollKey == null) return
    let cancelled = false
    let attempts = 0

    async function poll() {
      while (!cancelled && attempts < 15) {
        attempts += 1
        try {
          const { data } = await api.get<EvaluationReportRecord>(endpoints.evaluationLatest)
          if (!cancelled) {
            setReport(data)
            if (data.status === 'Ready') {
              setContent(JSON.parse(data.contentJson) as EvaluationReportContent)
              return
            }
          }
        } catch {
          // ignore until ready
        }
        await new Promise((resolve) => setTimeout(resolve, 2000))
      }
    }

    void poll()
    return () => {
      cancelled = true
    }
  }, [pollKey])

  return { report, content }
}
