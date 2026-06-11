import type { CefrLevel, DifficultyLevel } from './models'

export interface DifficultyRating {
  itemType: 'Word' | 'Sentence' | 'Article'
  difficultyLevel: DifficultyLevel
  cefrLevel: CefrLevel
  reason: string
  recommendedAction: 'LearnNow' | 'ReviewLater' | 'ChallengeOnly'
  confidence: number
  modelProfileId: string
}
