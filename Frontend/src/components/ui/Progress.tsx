import { Progress as BaseProgress } from '@base-ui/react/progress'

interface ProgressProps {
  value: number
  max?: number
  label?: string
  className?: string
}

export function Progress({
  value,
  max = 100,
  label,
  className = 'ui-progress',
}: ProgressProps) {
  const percent = max > 0 ? Math.round((value / max) * 100) : 0
  return (
    <BaseProgress.Root value={value} max={max} className={className} aria-label={label}>
      <BaseProgress.Track className="ui-progress-track">
        <BaseProgress.Indicator
          className="ui-progress-indicator"
          style={{ width: `${percent}%` }}
        />
      </BaseProgress.Track>
      {label ? <BaseProgress.Label className="ui-progress-label">{label}</BaseProgress.Label> : null}
    </BaseProgress.Root>
  )
}
