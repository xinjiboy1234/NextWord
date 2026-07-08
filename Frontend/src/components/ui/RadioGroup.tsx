import { Radio } from '@base-ui/react/radio'
import { RadioGroup as BaseRadioGroup } from '@base-ui/react/radio-group'

interface RadioOption {
  value: string
  label: string
}

interface RadioGroupProps {
  name: string
  value?: string
  onValueChange: (value: string) => void
  options: RadioOption[]
  disabled?: boolean
  className?: string
  optionClassName?: string
}

export function RadioGroup({
  name,
  value,
  onValueChange,
  options,
  disabled = false,
  className = 'option-tags',
  optionClassName = 'option-tag',
}: RadioGroupProps) {
  return (
    <BaseRadioGroup
      name={name}
      value={value ?? undefined}
      onValueChange={onValueChange}
      disabled={disabled}
      className={className}
      aria-label="选项"
    >
      {options.map((option) => {
        const selected = value === option.value
        return (
          <label
            key={option.value}
            className={`${optionClassName}${selected ? ' selected' : ''}`}
          >
            <Radio.Root value={option.value} className="sr-only">
              <Radio.Indicator />
            </Radio.Root>
            {option.label}
          </label>
        )
      })}
    </BaseRadioGroup>
  )
}
