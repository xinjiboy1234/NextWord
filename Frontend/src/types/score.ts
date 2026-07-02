export interface UserProfileScores {
  vocabulary: number | null
  reading: number | null
  writing: number | null
  spelling: number | null
  overall: number
  difficultyBucket: string
  cefrDisplay: string | null
  updatedAt: string | null
}

export interface EvaluationReportRecord {
  id: number
  status: string
  contentJson: string
  triggerType: string
}

export interface EvaluationReportContent {
  summary: string
  strengths: string[]
  weaknesses: string[]
  recommendations: Array<{ action: string; module: string }>
  evidence: Record<string, number>
  profileSnapshot: UserProfileScores
}

export interface ReadingLookupResult {
  word: string
  contextDefinition: string
  intrinsicScore: number | null
  personalDifficulty: number | null
  estimatedKnownRate: number
  phonetic: string | null
  offline: boolean
  confidence: number | null
}

export interface DailyWordItem {
  id: string
  lemma: string
  meanings: string[]
  effectiveDifficulty: number
  isWeak: boolean
  phonetics: string | null
}

export interface ChallengeStartResponse {
  challengeSessionId: string
  pack: {
    vocabulary: Array<{ word: string; options: string[]; difficulty: string }>
    sentence: { wordId: string | null; word: string; scene: string }
    reading: { articleId: string; question: string; options: string[]; articleExcerpt: string }
    attemptedLevel: string
  }
}

export interface ChallengeSubmitResponse {
  passed: boolean
  totalScore: number
  vocabularyScore: number
  writingScore: number
  readingScore: number
  evaluationReportId: number | null
}
