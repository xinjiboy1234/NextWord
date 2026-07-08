import { Select as BaseSelect } from '@base-ui/react/select'
import { ChevronDown } from 'lucide-react'

interface SelectOption {
  value: string
  label: string
}

interface SelectProps {
  value: string
  onValueChange: (value: string) => void
  options: SelectOption[]
  placeholder?: string
  disabled?: boolean
  className?: string
  'aria-label'?: string
}

export function Select({
  value,
  onValueChange,
  options,
  placeholder = '请选择',
  disabled = false,
  className = 'select',
  'aria-label': ariaLabel,
}: SelectProps) {
  return (
    <BaseSelect.Root
      value={value}
      onValueChange={(next) => { if (next != null) onValueChange(next) }}
      disabled={disabled}
    >
      <BaseSelect.Trigger className={className} aria-label={ariaLabel}>
        <BaseSelect.Value placeholder={placeholder} />
        <BaseSelect.Icon className="select-icon">
          <ChevronDown size={16} aria-hidden="true" />
        </BaseSelect.Icon>
      </BaseSelect.Trigger>
      <BaseSelect.Portal>
        <BaseSelect.Backdrop className="select-backdrop" />
        <BaseSelect.Positioner className="select-positioner" sideOffset={4}>
          <BaseSelect.Popup className="select-popup">
            <BaseSelect.List className="select-list">
              {options.map((option) => (
                <BaseSelect.Item key={option.value} value={option.value} className="select-item">
                  <BaseSelect.ItemText>{option.label}</BaseSelect.ItemText>
                  <BaseSelect.ItemIndicator className="select-item-indicator">✓</BaseSelect.ItemIndicator>
                </BaseSelect.Item>
              ))}
            </BaseSelect.List>
          </BaseSelect.Popup>
        </BaseSelect.Positioner>
      </BaseSelect.Portal>
    </BaseSelect.Root>
  )
}
