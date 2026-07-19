import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { WordDefinition, WordExample } from '../types/article'
import type { ReadingLookupResult } from '../types/score'

function mapExamples(examples?: ReadingLookupResult['examples']): WordExample[] {
  if (!examples?.length) return []
  return examples.map((item) => ({
    kind: item.kind === 'general' ? 'general' : 'contextual',
    sentence: item.sentence,
    explanation: item.explanation,
  }))
}

function mapLookupToDefinition(result: ReadingLookupResult, context?: string): WordDefinition {
  return {
    word: result.word,
    phonetics: result.phonetic ?? '',
    meanings: [{ definition: result.contextDefinition, isContextual: true, context: context ?? '' }],
    collocations: [],
    examples: mapExamples(result.examples),
    specialUsage: result.specialUsage ?? (result.offline ? '离线释义' : ''),
    difficultyLevel: 'Intermediate',
    cefrLevel: 'B1',
  }
}

export function useWordLookup(articleId: string | null) {
  const [definition, setDefinition] = useState<WordDefinition | null>(null)
  const [lookupMeta, setLookupMeta] = useState<ReadingLookupResult | null>(null)
  const [selectedWord, setSelectedWord] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const lookup = useCallback(async (word: string, context?: string) => {
    setSelectedWord(word)
    setLoading(true)
    try {
      const { data } = await api.post<ReadingLookupResult>(endpoints.readingLookup, {
        word,
        sentence: context ?? '',
        articleId: articleId ?? null,
      })
      setLookupMeta(data)
      setDefinition(mapLookupToDefinition(data, context))
    } catch {
      const fallback: ReadingLookupResult = {
        word,
        contextDefinition: word,
        intrinsicScore: null,
        personalDifficulty: null,
        estimatedKnownRate: 0.5,
        phonetic: null,
        offline: true,
        confidence: null,
        specialUsage: null,
        examples: [],
        fromCache: false,
      }
      setLookupMeta(fallback)
      setDefinition(mapLookupToDefinition(fallback, context))
    } finally {
      setLoading(false)
    }
  }, [articleId])

  function clear() {
    setDefinition(null)
    setLookupMeta(null)
    setSelectedWord(null)
  }

  return { definition, lookupMeta, selectedWord, loading, lookup, clear }
}
