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

/** T-034：已毕业词（spontaneous_use），graduatedAt 为毕业当刻（阶段流转时间） */
export interface GraduatedWord {
  wordId: string
  lemma: string
  graduatedAt: string | null
}

export interface LearningResult {
  isCorrect: boolean
  meanings: string[]
  exampleSentences: string[]
  masteryScore: number
  nextReviewDue: string
  intervalDays: number
  /** T-014：当前生命周期阶段（掌握度由阶段派生） */
  stage?: string
  /** T-014：下次考察模式 */
  quizMode?: 'recognition' | 'recall'
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
