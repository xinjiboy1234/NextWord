import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { FinalLevelResult, ReadingQuizPayload, SentenceQuizQuestion, SpellingQuizQuestion, VocabQuizQuestion } from '../types/assessment'

const STEPS = ['词汇', '拼写', '造句', '阅读', '定级'] as const

export function useAssessmentFlow() {
  const [assessmentId, setAssessmentId] = useState<string | null>(null)
  const [step, setStep] = useState(1)
  const [vocabQuestions, setVocabQuestions] = useState<VocabQuizQuestion[]>([])
  const [spellingQuestions, setSpellingQuestions] = useState<SpellingQuizQuestion[]>([])
  const [sentenceQuestions, setSentenceQuestions] = useState<SentenceQuizQuestion[]>([])
  const [readingPayload, setReadingPayload] = useState<ReadingQuizPayload | null>(null)
  const [finalResult, setFinalResult] = useState<FinalLevelResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const start = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await api.post<{ assessmentId: string }>(endpoints.assessmentStart)
      setAssessmentId(response.data.assessmentId)
      setStep(1)
      setFinalResult(null)
      const questions = await api.get<VocabQuizQuestion[]>(endpoints.assessmentStep(response.data.assessmentId, 1))
      setVocabQuestions(questions.data)
    } catch {
      setError('无法开始测评。')
    } finally {
      setLoading(false)
    }
  }, [])

  async function submitVocab(answers: number[]) {
    if (!assessmentId) return
    await api.post(endpoints.assessmentSubmit(assessmentId, 1), { answersJson: JSON.stringify(answers) })
    const questions = await api.get<SpellingQuizQuestion[]>(endpoints.assessmentStep(assessmentId, 2))
    setSpellingQuestions(questions.data)
    setStep(2)
  }

  async function submitSpelling(answers: string[]) {
    if (!assessmentId) return
    await api.post(endpoints.assessmentSubmit(assessmentId, 2), { answersJson: JSON.stringify(answers) })
    const questions = await api.get<SentenceQuizQuestion[]>(endpoints.assessmentStep(assessmentId, 3))
    setSentenceQuestions(questions.data)
    setStep(3)
  }

  async function submitSentence(answers: string[]) {
    if (!assessmentId) return
    await api.post(endpoints.assessmentSubmit(assessmentId, 3), { answersJson: JSON.stringify(answers) })
    const payload = await api.get<ReadingQuizPayload>(endpoints.assessmentStep(assessmentId, 4))
    setReadingPayload(payload.data)
    setStep(4)
  }

  async function submitReading(selectedIndex: number, lookupCount: number) {
    if (!assessmentId) return
    await api.post(endpoints.assessmentSubmit(assessmentId, 4), {
      answersJson: JSON.stringify({ selectedIndex, lookupCount }),
    })
    const result = await api.post<FinalLevelResult>(endpoints.assessmentComplete(assessmentId))
    setFinalResult(result.data)
    setStep(5)
  }

  return {
    steps: STEPS,
    step,
    assessmentId,
    vocabQuestions,
    spellingQuestions,
    sentenceQuestions,
    readingPayload,
    finalResult,
    loading,
    error,
    start,
    submitVocab,
    submitSpelling,
    submitSentence,
    submitReading,
  }
}
