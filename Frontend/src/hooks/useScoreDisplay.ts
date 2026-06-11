import type { SentenceRating } from '../types/sentence'

export function useScoreDisplay(rating: SentenceRating | null) {
  if (!rating) {
    return { average: 0, label: '未评分' }
  }

  const average = (rating.grammarScore + rating.naturalScore + rating.vocabularyScore + rating.relevanceScore) / 4
  const label = average >= 4.5 ? '稳定' : average >= 3.5 ? '可用' : average >= 2.5 ? '需打磨' : '需重写'
  return { average, label }
}
