interface ErrorHighlightProps {
  answer: string
  correct: string
  positions: number[]
}

export function ErrorHighlight({ answer, correct, positions }: ErrorHighlightProps) {
  if (positions.length === 0) {
    return <p style={{ fontSize: 'var(--text-sm)', fontWeight: 540, color: 'var(--success)' }}>拼写正确</p>
  }

  return (
    <div className="stack stack-sm" style={{ fontSize: 'var(--text-sm)' }}>
      <p style={{ color: 'var(--muted)' }}>
        正确拼写：<span style={{ fontWeight: 540, color: 'var(--fg)' }}>{correct}</span>
      </p>
      <div className="row" style={{ flexWrap: 'wrap' }}>
        {answer.split('').map((char, index) => (
          <span
            key={`${char}-${index}`}
            className={`spell-char ${positions.includes(index) ? 'err' : 'ok'}`}
          >
            {char}
          </span>
        ))}
      </div>
    </div>
  )
}
