export type CefrLevel = 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2'

export interface VocabQuizQuestion {
  word: string
  options: string[]
  correctIndex: number
}

export interface SpellingQuizQuestion {
  chinese: string
  correctSpelling: string
}

export interface SentenceQuizQuestion {
  wordId?: string | null
  word: string
  scene: string
}

export interface ReadingQuizPayload {
  articleId: string
  title: string
  content: string
  wordCount: number
  question: {
    articleId: string
    question: string
    options: string[]
    correctIndex: number
    articleExcerpt: string
  }
}

export interface FinalLevelResult {
  vocabLevel: CefrLevel
  spellingLevel: CefrLevel
  sentenceLevel: CefrLevel
  readingLevel: CefrLevel
  overallLevel: CefrLevel
  vocabularyScore?: number | null
  spellingScore?: number | null
  writingScore?: number | null
  readingScore?: number | null
  overallScore?: number | null
  evaluationReportId?: number | null
}

export interface ChallengePackClient {
  vocabulary: Array<{ word: string; options: string[]; difficulty: string }>
  sentence: SentenceQuizQuestion
  reading: {
    articleId: string
    question: string
    options: string[]
    articleExcerpt: string
  }
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
}
