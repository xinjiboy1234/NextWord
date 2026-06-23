import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { FinalLevelResult, ReadingQuizPayload, SentenceQuizQuestion, SpellingQuizQuestion, VocabQuizQuestion } from '../types/assessment'

const STEPS = ['词汇', '拼写', '造句', '阅读', '定级'] as const

export function useAssessmentFlow() {
  const [assessmentId, setAssessmentId] = useState<string | null>(null)
  const [step, setStep] = useState(1)
  const [maxReachedStep, setMaxReachedStep] = useState(1)
  const [vocabQuestions, setVocabQuestions] = useState<VocabQuizQuestion[]>([])
  const [spellingQuestions, setSpellingQuestions] = useState<SpellingQuizQuestion[]>([])
  const [sentenceQuestions, setSentenceQuestions] = useState<SentenceQuizQuestion[]>([])
  const [readingPayload, setReadingPayload] = useState<ReadingQuizPayload | null>(null)
  const [finalResult, setFinalResult] = useState<FinalLevelResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [stepError, setStepError] = useState<string | null>(null)

  const goToStep = useCallback((target: number) => {
    if (target < 1 || target > 5 || target > maxReachedStep) {
      return
    }
    setStepError(null)
    setStep(target)
  }, [maxReachedStep])

  const start = useCallback(async () => {
    setLoading(true)
    setError(null)
    setStepError(null)
    try {
      const response = await api.post<{ assessmentId: string }>(endpoints.assessmentStart, {})
      setAssessmentId(response.data.assessmentId)
      setStep(1)
      setMaxReachedStep(1)
      setFinalResult(null)
      setSpellingQuestions([])
      setSentenceQuestions([])
      setReadingPayload(null)
      const questions = await api.get<VocabQuizQuestion[]>(endpoints.assessmentStep(response.data.assessmentId, 1))
      setVocabQuestions(questions.data)
    } catch {
      setError('无法开始测评。')
    } finally {
      setLoading(false)
    }
  }, [])

  async function submitVocab(answers: number[]) {
    if (!assessmentId) return false
    setSubmitting(true)
    setStepError(null)
    try {
      await api.post(endpoints.assessmentSubmit(assessmentId, 1), { answersJson: JSON.stringify(answers) })
      const questions = await api.get<SpellingQuizQuestion[]>(endpoints.assessmentStep(assessmentId, 2))
      setSpellingQuestions(questions.data)
      setMaxReachedStep((value) => Math.max(value, 2))
      setStep(2)
      return true
    } catch {
      setStepError('词汇提交失败，请重试。')
      return false
    } finally {
      setSubmitting(false)
    }
  }

  async function submitSpelling(answers: string[]) {
    if (!assessmentId) return false
    setSubmitting(true)
    setStepError(null)
    try {
      await api.post(endpoints.assessmentSubmit(assessmentId, 2), { answersJson: JSON.stringify(answers) })
      const questions = await api.get<SentenceQuizQuestion[]>(endpoints.assessmentStep(assessmentId, 3))
      setSentenceQuestions(questions.data)
      setMaxReachedStep((value) => Math.max(value, 3))
      setStep(3)
      return true
    } catch {
      setStepError('拼写提交失败，请重试。')
      return false
    } finally {
      setSubmitting(false)
    }
  }

  async function submitSentence(answers: string[]) {
    if (!assessmentId) return false
    setSubmitting(true)
    setStepError(null)
    try {
      await api.post(endpoints.assessmentSubmit(assessmentId, 3), { answersJson: JSON.stringify(answers) })
      const payload = await api.get<ReadingQuizPayload>(endpoints.assessmentStep(assessmentId, 4))
      setReadingPayload(payload.data)
      setMaxReachedStep((value) => Math.max(value, 4))
      setStep(4)
      return true
    } catch {
      setStepError('造句提交失败，请重试。')
      return false
    } finally {
      setSubmitting(false)
    }
  }

  async function submitReading(selectedIndex: number, lookupCount: number) {
    if (!assessmentId) return false
    setSubmitting(true)
    setStepError(null)
    try {
      await api.post(endpoints.assessmentSubmit(assessmentId, 4), {
        answersJson: JSON.stringify({ selectedIndex, lookupCount }),
      })
      const result = await api.post<FinalLevelResult>(endpoints.assessmentComplete(assessmentId))
      setFinalResult(result.data)
      setMaxReachedStep(5)
      setStep(5)
      return true
    } catch {
      setStepError('阅读提交或定级失败，请重试。')
      return false
    } finally {
      setSubmitting(false)
    }
  }

  return {
    steps: STEPS,
    step,
    maxReachedStep,
    assessmentId,
    vocabQuestions,
    spellingQuestions,
    sentenceQuestions,
    readingPayload,
    finalResult,
    loading,
    submitting,
    error,
    stepError,
    start,
    goToStep,
    submitVocab,
    submitSpelling,
    submitSentence,
    submitReading,
  }
}
