interface OptionTagsProps {
  options: string[]
  selectedIndex?: number
  onSelect: (index: number) => void
  disabled?: boolean
}

export function OptionTags({ options, selectedIndex, onSelect, disabled = false }: OptionTagsProps) {
  return (
    <div className="option-tags" role="radiogroup" aria-label="选项">
      {options.map((option, index) => {
        const selected = selectedIndex === index
        return (
          <button
            key={`${index}-${option}`}
            type="button"
            role="radio"
            aria-checked={selected}
            disabled={disabled}
            onClick={() => onSelect(index)}
            className={`option-tag${selected ? ' selected' : ''}`}
          >
            {option}
          </button>
        )
      })}
    </div>
  )
}
