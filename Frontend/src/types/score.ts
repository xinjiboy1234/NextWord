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
  findings?: ProfileFindingItem[]
}

/** T-005：WeaknessProfile 已验证 Finding（schemaVersion 2 报告内容携带） */
export interface ProfileFindingItem {
  dimension: string
  dimensionKey: string
  polarity: string
  statement: string
  confidence: string
  evidence: Array<{ kind: string; refId: string; metric: string | null; op: string | null; value: number | null }>
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
  specialUsage?: string | null
  examples?: Array<{ kind: string; sentence: string; explanation: string }>
  fromCache?: boolean
}

export interface DailyWordItem {
  id: string
  lemma: string
  meanings: string[]
  effectiveDifficulty: number
  isWeak: boolean
  phonetics: string | null
  /** T-006：来自当日 LearningPlan */
  fromPlan?: boolean
  /** T-006：超带接触词，认识即可 */
  isExposure?: boolean
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
