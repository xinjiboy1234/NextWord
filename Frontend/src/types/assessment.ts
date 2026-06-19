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
}

export interface ChallengePack {
  vocabulary: VocabQuizQuestion[]
  sentence: SentenceQuizQuestion
  reading: {
    articleId: string
    question: string
    options: string[]
    correctIndex: number
    articleExcerpt: string
  }
  attemptedLevel: CefrLevel
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
}
