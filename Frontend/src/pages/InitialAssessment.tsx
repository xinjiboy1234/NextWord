import { useState } from 'react'
import { useAssessmentFlow } from '../hooks/useAssessmentFlow'

export function InitialAssessment() {
  const flow = useAssessmentFlow()
  const [vocabAnswers, setVocabAnswers] = useState<number[]>([])
  const [spellingAnswers, setSpellingAnswers] = useState<string[]>([])
  const [sentenceAnswers, setSentenceAnswers] = useState<string[]>([])
  const [readingIndex, setReadingIndex] = useState(0)

  if (!flow.assessmentId) {
    return (
      <section className="rounded-md border border-neutral-200 bg-white p-6">
        <h2 className="text-2xl font-semibold">首次水平测评</h2>
        <p className="mt-2 text-sm text-neutral-600">5 步测评：词汇 → 拼写 → 造句 → 阅读 → 定级</p>
        {flow.error && <p className="mt-3 text-sm text-rose-700">{flow.error}</p>}
        <button
          type="button"
          onClick={() => void flow.start()}
          disabled={flow.loading}
          className="mt-4 inline-flex h-11 items-center rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white disabled:opacity-60"
        >
          {flow.loading ? '准备中...' : '开始测评'}
        </button>
      </section>
    )
  }

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-6">
      <div className="flex flex-wrap gap-2">
        {flow.steps.map((label, index) => (
          <span
            key={label}
            className={`rounded-full px-3 py-1 text-xs font-medium ${
              flow.step === index + 1 ? 'bg-emerald-700 text-white' : 'bg-neutral-100 text-neutral-600'
            }`}
          >
            {index + 1}. {label}
          </span>
        ))}
      </div>

      {flow.step === 1 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇识别</h3>
          {flow.vocabQuestions.map((question, index) => (
            <fieldset key={question.word} className="rounded-md border border-neutral-200 p-3">
              <legend className="px-1 text-sm font-medium">{question.word}</legend>
              <div className="mt-2 grid gap-2">
                {question.options.map((option, optionIndex) => (
                  <label key={option} className="flex items-center gap-2 text-sm">
                    <input
                      type="radio"
                      name={`vocab-${index}`}
                      checked={vocabAnswers[index] === optionIndex}
                      onChange={() => {
                        const next = [...vocabAnswers]
                        next[index] = optionIndex
                        setVocabAnswers(next)
                      }}
                    />
                    {option}
                  </label>
                ))}
              </div>
            </fieldset>
          ))}
          <button
            type="button"
            onClick={() => void flow.submitVocab(vocabAnswers)}
            className="inline-flex h-10 items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white"
          >
            下一步
          </button>
        </div>
      )}

      {flow.step === 2 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">拼写测评</h3>
          {flow.spellingQuestions.map((question, index) => (
            <label key={question.chinese} className="block text-sm">
              <span className="font-medium">{question.chinese}</span>
              <input
                className="mt-1 h-10 w-full rounded-md border border-neutral-300 px-3"
                value={spellingAnswers[index] ?? ''}
                onChange={(event) => {
                  const next = [...spellingAnswers]
                  next[index] = event.target.value
                  setSpellingAnswers(next)
                }}
              />
            </label>
          ))}
          <button type="button" onClick={() => void flow.submitSpelling(spellingAnswers)} className="inline-flex h-10 items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white">
            下一步
          </button>
        </div>
      )}

      {flow.step === 3 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">造句测评</h3>
          {flow.sentenceQuestions.map((question, index) => (
            <label key={question.word} className="block text-sm">
              <span className="font-medium">使用单词：{question.word}</span>
              <textarea
                className="mt-1 w-full rounded-md border border-neutral-300 px-3 py-2"
                rows={3}
                value={sentenceAnswers[index] ?? ''}
                onChange={(event) => {
                  const next = [...sentenceAnswers]
                  next[index] = event.target.value
                  setSentenceAnswers(next)
                }}
              />
            </label>
          ))}
          <button type="button" onClick={() => void flow.submitSentence(sentenceAnswers)} className="inline-flex h-10 items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white">
            下一步
          </button>
        </div>
      )}

      {flow.step === 4 && flow.readingPayload && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读测评</h3>
          <p className="text-sm font-medium">{flow.readingPayload.title}</p>
          <p className="text-sm leading-6 text-neutral-700">{flow.readingPayload.content}</p>
          <p className="text-sm font-medium">{flow.readingPayload.question.question}</p>
          {flow.readingPayload.question.options.map((option, index) => (
            <label key={option} className="flex items-center gap-2 text-sm">
              <input type="radio" name="reading" checked={readingIndex === index} onChange={() => setReadingIndex(index)} />
              {option}
            </label>
          ))}
          <button type="button" onClick={() => void flow.submitReading(readingIndex, 0)} className="inline-flex h-10 items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white">
            提交并定级
          </button>
        </div>
      )}

      {flow.step === 5 && flow.finalResult && (
        <div className="mt-5 rounded-md border border-emerald-200 bg-emerald-50 p-4">
          <h3 className="text-lg font-semibold text-emerald-900">定级结果</h3>
          <p className="mt-2 text-sm text-emerald-950">总体等级：{flow.finalResult.overallLevel}</p>
          <ul className="mt-2 space-y-1 text-sm text-emerald-900">
            <li>词汇：{flow.finalResult.vocabLevel}</li>
            <li>拼写：{flow.finalResult.spellingLevel}</li>
            <li>造句：{flow.finalResult.sentenceLevel}</li>
            <li>阅读：{flow.finalResult.readingLevel}</li>
          </ul>
        </div>
      )}
    </section>
  )
}
