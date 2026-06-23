interface OptionTagsProps {
  options: string[]
  selectedIndex?: number
  onSelect: (index: number) => void
  disabled?: boolean
}

export function OptionTags({ options, selectedIndex, onSelect, disabled = false }: OptionTagsProps) {
  return (
    <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="选项">
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
            className={`rounded-full border px-4 py-2 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-50 ${
              selected
                ? 'border-emerald-700 bg-emerald-700 text-white shadow-sm'
                : 'border-neutral-200 bg-white text-neutral-700 hover:border-emerald-400 hover:bg-emerald-50'
            }`}
          >
            {option}
          </button>
        )
      })}
    </div>
  )
}
