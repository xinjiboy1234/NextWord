import type { DifficultyLevel } from './models'

export interface SentencePrompt {
  id: string
  wordId: string | null
  content: string
  targetWord: string
  difficultyLevel: DifficultyLevel
  cefrLevel: string
  scene: string
}

export interface SentenceRating {
  id: string
  wordId: string | null
  targetWord: string
  scene: string
  userSentence: string
  aiRevision: string
  grammarScore: number
  naturalScore: number
  vocabularyScore: number
  relevanceScore: number
  overallGrade: string
  errorTags: string[]
  difficultyLevel: DifficultyLevel
  suggestion: string
  timestamp: string
}

export interface FreeExpressionRating {
  id: string
  userText: string
  aiScore: number
  overallGrade: string
  aiRevision: string
  errorSentences: string[]
  suggestions: string[]
  difficultyLevel: DifficultyLevel
  timestamp: string
}

export interface SpellingResult {
  id: string
  wordId: string
  userSpelling: string
  correctSpelling: string
  isCorrect: boolean
  errorPositions: number[]
  timestamp: string
  attempts: number
}

export interface LogSummary {
  sentenceCount: number
  freeExpressionCount: number
  spellingCount: number
  spellingAccuracyPercent: number
  dueReviews: number
}

export interface RecentLog {
  type: string
  label: string
  result: string
  timestamp: string
}
