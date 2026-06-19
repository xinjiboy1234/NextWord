export type DifficultyLevel = 'Basic' | 'Intermediate' | 'Advanced'
export type CefrLevel = 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2'
export type AssessmentResult = 'Remembered' | 'Fuzzy' | 'Forgot'

export interface Word {
  id: string
  lemma: string
  partOfSpeech: string
  phonetics: string
  meanings: string[]
  exampleSentences: string[]
  difficultyLevel: DifficultyLevel
  cefrLevel: CefrLevel
  isCore: boolean
}

export interface LearningResult {
  isCorrect: boolean
  meanings: string[]
  exampleSentences: string[]
  masteryScore: number
  nextReviewDue: string
  intervalDays: number
}

export interface ProgressSummary {
  userId: string
  displayName: string
  overallLevel: string
  vocabLevel: string
  streakDays: number
  lastStudyDate: string | null
  totalLearned: number
  dueReviews: number
  pendingReviewCount: number
  totalLogs: number
  accuracyPercent: number
  hasCompletedInitialAssessment: boolean
  isUpgradeCandidate: boolean
}
