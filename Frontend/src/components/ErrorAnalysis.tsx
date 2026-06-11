interface ErrorAnalysisProps {
  items?: string[]
  suggestion?: string
}

export function ErrorAnalysis({ items = [], suggestion }: ErrorAnalysisProps) {
  if (items.length === 0 && !suggestion) return null

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-5">
      <h3 className="text-base font-semibold">问题分析</h3>
      <ul className="mt-3 grid gap-2 text-sm text-neutral-700">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
      {suggestion && <p className="mt-3 rounded-md bg-neutral-100 p-3 text-sm text-neutral-800">{suggestion}</p>}
    </section>
  )
}
