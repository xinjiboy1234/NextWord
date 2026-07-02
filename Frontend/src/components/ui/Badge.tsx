import type { BadgeVariant } from '../../hooks/useDashboardStats'

const VARIANT_CLASS: Record<BadgeVariant | 'fg', string> = {
  muted: 'badge-muted',
  success: 'badge-success',
  warn: 'badge-warn',
  info: 'badge-info',
  fg: 'badge-fg',
}

interface BadgeProps {
  children: React.ReactNode
  variant?: BadgeVariant | 'fg'
  className?: string
}

export function Badge({ children, variant = 'muted', className = '' }: BadgeProps) {
  return (
    <span className={`badge ${VARIANT_CLASS[variant]} ${className}`.trim()}>
      {children}
    </span>
  )
}
