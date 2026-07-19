import { Send, X } from 'lucide-react'
import type { FormEvent } from 'react'

interface AnswerInputProps {
  value: string
  disabled?: boolean
  onChange: (value: string) => void
  onSubmit: () => void
  onForgot?: () => void
}

export function AnswerInput({ value, disabled, onChange, onSubmit, onForgot }: AnswerInputProps) {
  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    onSubmit()
  }

  return (
    <form onSubmit={handleSubmit} className="card" autoComplete="off">
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
            autoComplete="off"
          />
          <button
            type="submit"
            disabled={disabled || value.trim().length === 0}
            className="btn btn-primary"
          >
            <Send size={18} aria-hidden="true" />
            提交
          </button>
          {onForgot ? (
            <button
              type="button"
              disabled={disabled}
              onClick={onForgot}
              className="btn btn-danger-outline"
            >
              <X size={18} aria-hidden="true" />
              不会
            </button>
          ) : null}
        </div>
      </div>
    </form>
  )
}
