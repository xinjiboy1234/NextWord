import { useCallback, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { WordDefinition } from '../types/article'

export function useWordLookup(articleId: string | null) {
  const [definition, setDefinition] = useState<WordDefinition | null>(null)
  const [selectedWord, setSelectedWord] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const lookup = useCallback(async (word: string, context?: string) => {
    if (!articleId) return
    setSelectedWord(word)
    setLoading(true)
    try {
      const response = await api.post<WordDefinition>(endpoints.articleLookup(articleId), {
        word,
        context,
      })
      setDefinition(response.data)
    } finally {
      setLoading(false)
    }
  }, [articleId])

  function clear() {
    setDefinition(null)
    setSelectedWord(null)
  }

  return { definition, selectedWord, loading, lookup, clear }
}
