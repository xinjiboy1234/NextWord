/** T-036「我的这个月」月度时间轴类型（GET /api/profile/monthly-timeline + scores/history） */

export interface ScoreHistorySnapshot {
  date: string
  /** UserProfileScores 序列化 JSON（vocabulary/reading/writing 等，可能为 null） */
  scoresJson: string
}

export type MonthlyEventType = 'word_graduation' | 'challenge_first_pass' | 'level_change' | 'profile_generated'

export interface MonthlyTimelineEvent {
  type: MonthlyEventType
  occurredAt: string
  word: string | null
  level: string | null
  fromLevel: string | null
  toLevel: string | null
  reason: string | null
}

export interface ProfileChangeItem {
  dimension: string
  dimensionKey: string
  statement: string
}

export interface ProfileFindingSummary {
  dimension: string
  dimensionKey: string
  polarity: string
  statement: string
}

export interface MonthlyProfileChange {
  hasProfile: boolean
  hasComparison: boolean
  currentProfileAt: string | null
  newStrengths: ProfileChangeItem[]
  improvedWeaknesses: ProfileChangeItem[]
  currentFindings: ProfileFindingSummary[]
}

export interface MonthlyInsight {
  nature: string
  statement: string
  createdAt: string
}

export interface MonthlyTimeline {
  days: number
  events: MonthlyTimelineEvent[]
  profileChange: MonthlyProfileChange
  insights: MonthlyInsight[]
}
