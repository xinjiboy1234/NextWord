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
  /** T-055 人话 rubric（DESIGN-assessment-visibility §3.1）：新测评起随定级结果持久化，旧记录无此字段为 null，降级不显示 */
  rubric?: ProficiencyRubricView | null
}

/** T-055 人话 rubric 视图：总体标签 + 四维（中文名、得分、特征描述），后端已装配为用户可读中文 */
export interface ProficiencyRubricView {
  overallLabel: string
  overallDescription: string
  dimensions: RubricDimensionView[]
}

export interface RubricDimensionView {
  name: string
  score: number
  description: string
}

// ── T-054 测评记录页（/assessments）────────────────────────

/** GET /api/assessments 列表项 */
export interface AssessmentListItem {
  id: string
  type: 'Initial' | 'Challenge'
  status: 'InProgress' | 'Completed'
  startAt: string
  endAt?: string | null
  finalLevel?: CefrLevel | null
  expressionScore?: number | null
  /** 是否触发识别防伪闸矫正 */
  guardAdjusted: boolean
}

/** GET /api/assessment/{id} 详情（DTO 投影，无导航回引用；题目/作答/评分在各记录的 JSON 字符串里） */
export interface AssessmentDetail {
  id: string
  userId: string
  type: 'Initial' | 'Challenge'
  status: 'InProgress' | 'Completed'
  startAt: string
  endAt?: string | null
  finalLevel?: CefrLevel | null
  records: AssessmentRecordView[]
}

export interface AssessmentRecordView {
  id: string
  step: 'AdaptiveBlock' | 'FinalLevel' | 'Vocabulary' | 'Spelling' | 'Sentence' | 'Reading'
  questionType: string
  questionsJson: string
  answersJson: string
  scoresJson: string
  timestamp: string
}

/** AdaptiveBlock 记录 questionsJson 的反序列化结构（块题目载荷） */
export interface AssessmentBlockPayload {
  blockIndex: number
  band: CefrLevel
  production: Array<{
    id: string
    kind: 'sentence' | 'scenario'
    targetWord?: string | null
    scenarioZh: string
    prompt: string
  }>
  vocabulary: Array<{ id: string; word: string; options: string[]; correctIndex: number }>
  reading?: {
    id: string
    title: string
    content: string
    question: string
    options: string[]
    correctIndex: number
  } | null
}

/** AdaptiveBlock 记录 scoresJson 的反序列化结构（块评分） */
export interface AssessmentBlockScores {
  blockExpressionScore: number
  production: AssessmentProductionScore[]
  vocabulary: Array<{ id: string; correct: boolean }>
  reading?: { correct: boolean; lookupCount: number } | null
  nextBand?: CefrLevel
}

export interface AssessmentProductionScore {
  id: string
  /** 表达综合分 0–100 */
  score: number
  grammar: number
  natural: number
  vocabulary: number
  relevance: number
  errorTags: string[]
  /** T-054 起新测评带 AI 评语；旧记录无此字段为 null，前端降级只显示四维分 */
  suggestion?: string | null
  aiRevision?: string | null
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
