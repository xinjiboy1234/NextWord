interface ErrorHighlightProps {
  answer: string
  correct: string
  positions: number[]
}

export function ErrorHighlight({ answer, correct, positions }: ErrorHighlightProps) {
  if (positions.length === 0) {
    return <p className="text-sm font-medium text-emerald-700">拼写正确</p>
  }

  return (
    <div className="grid gap-2 text-sm">
      <p className="text-neutral-600">正确拼写：<span className="font-semibold text-neutral-950">{correct}</span></p>
      <div className="flex flex-wrap gap-1">
        {answer.split('').map((char, index) => (
          <span key={`${char}-${index}`} className={`grid h-8 min-w-8 place-items-center rounded-md border px-2 ${positions.includes(index) ? 'border-rose-300 bg-rose-50 text-rose-800' : 'border-neutral-200 bg-neutral-50 text-neutral-700'}`}>
            {char}
          </span>
        ))}
      </div>
    </div>
  )
}
