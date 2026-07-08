import { Switch as BaseSwitch } from '@base-ui/react/switch'

interface SwitchProps {
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  disabled?: boolean
  'aria-label'?: string
}

export function Switch({
  checked,
  onCheckedChange,
  disabled = false,
  'aria-label': ariaLabel,
}: SwitchProps) {
  return (
    <BaseSwitch.Root
      checked={checked}
      onCheckedChange={onCheckedChange}
      disabled={disabled}
      aria-label={ariaLabel}
      className="ui-switch"
    >
      <BaseSwitch.Thumb className="ui-switch-thumb" />
    </BaseSwitch.Root>
  )
}
