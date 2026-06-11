interface SpellingInputProps {
  value: string
  disabled?: boolean
  onChange: (value: string) => void
  onSubmit: () => void
}

export function SpellingInput({ value, disabled, onChange, onSubmit }: SpellingInputProps) {
  return (
    <form
      className="rounded-md border border-neutral-200 bg-white p-5"
      onSubmit={(event) => {
        event.preventDefault()
        onSubmit()
      }}
    >
      <label className="text-sm font-semibold text-neutral-800" htmlFor="spelling-input">
        拼写
      </label>
      <div className="mt-3 flex gap-2">
        <input
          id="spelling-input"
          value={value}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
          className="h-11 min-w-0 flex-1 rounded-md border border-neutral-300 px-3 text-base outline-none focus:border-emerald-700"
          autoComplete="off"
        />
        <button type="submit" disabled={disabled || value.trim().length === 0} className="h-11 rounded-md bg-neutral-950 px-4 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-neutral-300">
          提交
        </button>
      </div>
    </form>
  )
}
