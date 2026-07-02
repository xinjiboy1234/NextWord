interface ProgressBarProps {
  value: number
  brand?: boolean
}

export function ProgressBar({ value, brand = false }: ProgressBarProps) {
  const clamped = Math.min(100, Math.max(0, value))
  return (
    <div className="progress-bar" aria-label={`学习进度 ${clamped}%`}>
      <div
        className={`progress-bar-fill${brand ? ' brand' : ''}`}
        style={{ width: `${clamped}%` }}
      />
    </div>
  )
}
