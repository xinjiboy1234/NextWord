export const endpoints = {
  words: '/api/words',
  dailyWords: '/api/words/daily',
  learningSubmit: '/api/learning/submit',
  progress: '/api/progress',
  llmRateDifficulty: '/api/llm/rate-difficulty',
  sentencePrompts: '/api/sentences/prompts',
  sentenceRate: '/api/sentences/rate',
  freeExpressionRate: '/api/free-expression/rate',
  spellingQueue: '/api/spelling/queue',
  spellingSubmit: '/api/spelling/submit',
  logSummary: '/api/logs/summary',
  recentLogs: '/api/logs/recent',
} as const
