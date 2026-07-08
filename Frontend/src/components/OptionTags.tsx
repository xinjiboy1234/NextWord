import { RadioGroup } from './ui/RadioGroup'

interface OptionTagsProps {
  options: string[]
  selectedIndex?: number
  onSelect: (index: number) => void
  disabled?: boolean
}

export function OptionTags({ options, selectedIndex, onSelect, disabled = false }: OptionTagsProps) {
  const radioOptions = options.map((option, index) => ({
    value: String(index),
    label: option,
  }))

  return (
    <RadioGroup
      name="option-tags"
      value={selectedIndex !== undefined ? String(selectedIndex) : undefined}
      onValueChange={(value) => onSelect(Number(value))}
      options={radioOptions}
      disabled={disabled}
    />
  )
}
