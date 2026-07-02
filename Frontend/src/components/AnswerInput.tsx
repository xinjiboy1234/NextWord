import { Send } from 'lucide-react'
import type { FormEvent } from 'react'

interface AnswerInputProps {
  value: string
  disabled?: boolean
  onChange: (value: string) => void
  onSubmit: () => void
}

export function AnswerInput({ value, disabled, onChange, onSubmit }: AnswerInputProps) {
  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    onSubmit()
  }

  return (
    <form onSubmit={handleSubmit} className="card">
      <div className="field">
        <label htmlFor="answer">中文释义</label>
        <div className="row" style={{ flexWrap: 'wrap' }}>
          <input
            id="answer"
            value={value}
            disabled={disabled}
            onChange={(event) => onChange(event.target.value)}
            className="input"
            style={{ flex: 1, minWidth: 200 }}
            placeholder="输入你想到的中文含义"
          />
          <button
            type="submit"
            disabled={disabled || value.trim().length === 0}
            className="btn btn-primary"
          >
            <Send size={18} aria-hidden="true" />
            提交
          </button>
        </div>
      </div>
    </form>
  )
}
