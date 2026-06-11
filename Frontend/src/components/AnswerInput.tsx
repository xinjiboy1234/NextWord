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
    <form onSubmit={handleSubmit} className="rounded-md border border-neutral-200 bg-white p-5">
      <label htmlFor="answer" className="text-sm font-semibold text-neutral-800">中文释义</label>
      <div className="mt-3 flex flex-col gap-3 sm:flex-row">
        <input
          id="answer"
          value={value}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
          className="min-h-11 flex-1 rounded-md border border-neutral-300 bg-white px-3 text-base outline-none transition focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100"
          placeholder="输入你想到的中文含义"
        />
        <button
          type="submit"
          disabled={disabled || value.trim().length === 0}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-neutral-950 px-4 text-sm font-semibold text-white disabled:opacity-50"
        >
          <Send size={18} aria-hidden="true" />
          提交
        </button>
      </div>
    </form>
  )
}
