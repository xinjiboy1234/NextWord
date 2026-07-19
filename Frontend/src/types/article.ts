export type ArticleSource = 'Builtin' | 'Llm'
export type DifficultyLevel = 'Basic' | 'Intermediate' | 'Advanced'
export type CefrLevel = 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2'
export type RecommendedAction = 'LearnNow' | 'ReviewLater' | 'ChallengeOnly'
export type WordExampleKind = 'contextual' | 'general'

export interface WordExample {
  kind: WordExampleKind
  sentence: string
  explanation: string
}

export interface ArticleSummary {
  id: string
  title: string
  difficultyLevel: DifficultyLevel
  cefrLevel: CefrLevel
  wordCount: number
  source: ArticleSource
  topicTag?: string | null
}

export interface ArticleDetail extends ArticleSummary {
  content: string
  vocabMappings: ArticleVocabMapping[]
}

export interface ArticleVocabMapping {
  id: string
  wordLemma: string
  contextMeaning: string
  phonetics: string
  examples: WordExample[]
  specialUsage: string
  difficultyInContext: DifficultyLevel
  recommendedAction: RecommendedAction
  isKeyVocab: boolean
}

export interface ReadingLog {
  id: string
  articleId: string
  startTime: string
  endTime?: string | null
  durationSeconds: number
  lookupCount: number
  commentsCount: number
}

export interface WordDefinition {
  word: string
  phonetics: string
  meanings: Array<{ definition: string; isContextual: boolean; context: string }>
  collocations: string[]
  examples: WordExample[]
  specialUsage: string
  difficultyLevel: DifficultyLevel
  cefrLevel: CefrLevel
}

export interface ArticleComment {
  id: string
  paragraphIndex: number
  paragraphText: string
  commentText: string
  aiReply?: string | null
  timestamp: string
}

export interface ReadingAgentResponse {
  message: string
  skillCalls: Array<{ skillName: string; summary: string }>
  definition?: WordDefinition | null
  vocabExtract?: {
    keyVocab: Array<{
      word: string
      phonetics: string
      contextMeaning: string
      usageExample?: WordExample | null
      generalExample?: WordExample | null
      difficulty: string
      action: string
    }>
    skippedBasic: string[]
    skippedRare: string[]
  } | null
  commentReply?: { reply: string } | null
}
