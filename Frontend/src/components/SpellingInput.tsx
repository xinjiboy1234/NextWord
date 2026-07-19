interface SpellingInputProps {
  value: string
  disabled?: boolean
  onChange: (value: string) => void
  onSubmit: () => void
}

export function SpellingInput({ value, disabled, onChange, onSubmit }: SpellingInputProps) {
  return (
    <form
      className="field"
      autoComplete="off"
      onSubmit={(event) => {
        event.preventDefault()
        onSubmit()
      }}
    >
      <label htmlFor="spelling-input">拼写</label>
      <div className="row" style={{ flexWrap: 'wrap' }}>
        <input
          id="spelling-input"
          value={value}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
          className="input"
          style={{ flex: 1, minWidth: 200 }}
          autoComplete="off"
        />
        <button
          type="submit"
          disabled={disabled || value.trim().length === 0}
          className="btn btn-primary"
        >
          提交
        </button>
      </div>
    </form>
  )
}
