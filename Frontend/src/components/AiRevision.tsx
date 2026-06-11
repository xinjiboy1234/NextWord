interface AiRevisionProps {
  value?: string
}

export function AiRevision({ value }: AiRevisionProps) {
  if (!value) return null

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-5">
      <h3 className="text-base font-semibold">AI 改写</h3>
      <p className="mt-3 rounded-md bg-emerald-50 p-3 text-sm leading-6 text-emerald-950">{value}</p>
    </section>
  )
}
