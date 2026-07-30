/** T-018：GET /api/planner/current 响应（active=false 时只有 active 与 exploration 字段） */
export interface CurrentLearningPlan {
  active: boolean
  startDate?: string
  dayIndex?: number
  focusScenarios?: string[]
  sourceFindingIds?: string[]
  articleIds?: string[]
  todayWordCount?: number
  todayExposureCount?: number
  todaySentenceTargets?: string[]
  exploration?: ExplorationWeek
}

/** T-032：探索周状态（注册起 7 天；remainingEvidence = 还差几条证据触发画像重生成） */
export interface ExplorationWeek {
  active: boolean
  day: number
  totalDays: number
  evidenceCount: number
  remainingEvidence: number
  scenarioKey?: string | null
  scenarioName?: string | null
  prompt?: string | null
}

/** GET /api/scenarios 响应（taxonomy + 各子场景有效词数） */
export interface ScenarioCatalog {
  categories: Array<{
    key: string
    zhName: string
    subScenarios: Array<{ key: string; zhName: string; wordCount: number }>
  }>
  coreBucketWordCount: number
}

/** T-019：GET /api/insights/bottleneck/latest 响应（found=false 时只有 found 字段） */
export interface BottleneckInsightResult {
  found: boolean
  nature?: string
  signals?: string[]
  statement?: string
  evidenceLogIds?: string[]
  replanTriggered?: boolean
  createdAt?: string
}
