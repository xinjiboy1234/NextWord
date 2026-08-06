export type CefrLevel = 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2'

export interface AssessmentProductionPrompt {
  id: string
  kind: 'sentence' | 'scenario'
  targetWord?: string | null
  scenarioZh: string
  prompt: string
}

export interface AssessmentVocabChoice {
  id: string
  word: string
  options: string[]
}

export interface AssessmentReadingItem {
  id: string
  title: string
  content: string
  question: string
  options: string[]
}

export interface AssessmentBlock {
  blockIndex: number
  maxBlocks: number
  band: CefrLevel
  production: AssessmentProductionPrompt[]
  vocabulary: AssessmentVocabChoice[]
  reading: AssessmentReadingItem | null
}

export interface AssessmentAnswerItem {
  id: string
  text?: string | null
  selectedIndex?: number | null
  lookupCount?: number | null
}

export interface AssessmentBlockResponse {
  converged: boolean
  block?: AssessmentBlock | null
  final?: AssessmentFinalResult | null
}

export interface AssessmentBlockResult {
  converged: boolean
  blockIndex: number
  band: CefrLevel
  nextBand?: CefrLevel | null
  blockExpressionScore: number
  final?: AssessmentFinalResult | null
}

export interface AssessmentDimensionSummary {
  grammar: number
  natural: number
  vocabulary: number
  relevance: number
  topErrorTags: string[]
  comments: string[]
}

export interface AssessmentFinalResult {
  overallLevel: CefrLevel
  expressionScore: number
  vocabularyReferenceScore: number
  readingReferenceScore: number
  vocabularyReferenceLevel: CefrLevel
  readingReferenceLevel: CefrLevel
  dimensions: AssessmentDimensionSummary
  evaluationReportId?: number | null
  /** T-042 识别防伪闸留痕：发生矫正时为表达定级原档，未矫正为 null */
  originalLevelBeforeGuard?: CefrLevel | null
}

export interface SentenceQuizQuestion {
  wordId?: string | null
  word: string
  scene: string
}

export interface ChallengeReadingQuestion {
  articleId: string
  question: string
  options: string[]
  articleExcerpt: string
}

export interface ChallengePackClient {
  vocabulary: Array<{ word: string; options: string[]; difficulty: string }>
  sentence: SentenceQuizQuestion
  /** T-035：阅读题组（3 题） */
  readings: ChallengeReadingQuestion[]
  attemptedLevel: CefrLevel
}

export interface ChallengeRecord {
  id: string
  challengeType: string
  vocabularyScore: number
  sentenceScore: number
  readingScore: number
  totalScore: number
  passed: boolean
  attemptedLevel: CefrLevel
  timestamp: string
}

export interface LevelDashboard {
  overallLevel: CefrLevel
  vocabLevel: CefrLevel
  spellingLevel: CefrLevel
  sentenceLevel: CefrLevel
  readingLevel: CefrLevel
  hasCompletedInitialAssessment: boolean
  upgradeCandidate: boolean
  recentHistory: Array<{
    id: string
    fromLevel: CefrLevel
    toLevel: CefrLevel
    reason: string
    timestamp: string
  }>
  scores?: {
    vocabulary: number | null
    reading: number | null
    writing: number | null
    spelling: number | null
    overall: number
    difficultyBucket: string
    cefrDisplay: string | null
  } | null
  /** T-035：挑战累计通过次数（ChallengeRecords 派生） */
  challengePassCount: number
  /** T-035：各档首次通过标记（按首通时间排序） */
  challengeFirstPassLevels: CefrLevel[]
}
