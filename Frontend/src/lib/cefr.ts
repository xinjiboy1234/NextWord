const CEFR_ORDER = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'] as const

export function nextCefrLevel(level: string): string | null {
  const index = CEFR_ORDER.indexOf(level as (typeof CEFR_ORDER)[number])
  if (index < 0 || index >= CEFR_ORDER.length - 1) return null
  return CEFR_ORDER[index + 1]
}
