import { Button as BaseButton } from '@base-ui/react/button'
import type { ComponentProps } from 'react'

type ButtonVariant = 'primary' | 'secondary' | 'ghost'
type ButtonSize = 'sm' | 'md'

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary: 'btn btn-primary',
  secondary: 'btn btn-secondary',
  ghost: 'btn btn-ghost',
}

interface ButtonProps extends ComponentProps<typeof BaseButton> {
  variant?: ButtonVariant
  size?: ButtonSize
}

export function Button({
  variant = 'primary',
  size = 'md',
  className,
  ...props
}: ButtonProps) {
  const sizeClass = size === 'sm' ? 'btn-sm' : ''
  const classes = [VARIANT_CLASS[variant], sizeClass, className].filter(Boolean).join(' ')
  return <BaseButton className={classes} {...props} />
}
