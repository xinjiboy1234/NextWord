export interface AuthUser {
  id: string
  email: string
  displayName: string
}

export interface AuthResult {
  token: string
  user: AuthUser
}

export interface LlmPreset {
  id: string
  name: string
  provider: string
  baseUrl: string
  defaultModel: string
}

export interface UserLlmSettings {
  provider: string
  baseUrl: string
  model: string
  maskedApiKey: string | null
  hasApiKey: boolean
}

export interface LevelHistoryItem {
  id: string
  fromLevel: string
  toLevel: string
  reason: string
  changedAt: string
}

export interface UserProfile {
  userId: string
  email: string
  displayName: string
  overallLevel: string
  vocabLevel: string
  spellingLevel: string
  sentenceLevel: string
  readingLevel: string
  streakDays: number
  lastStudyDate: string | null
  hasCompletedInitialAssessment: boolean
  isUpgradeCandidate: boolean
  totalLearned: number
  dueReviews: number
  pendingReviewCount: number
  totalLogs: number
  accuracyPercent: number
  recentHistory: LevelHistoryItem[]
  llmSettings: UserLlmSettings | null
}
