import { useEffect, useRef, useState } from 'react'
import { AssessmentTimeline } from '../components/AssessmentTimeline'
import { OptionTags } from '../components/OptionTags'
import { StepNavigator } from '../components/StepNavigator'
import { useAssessmentFlow } from '../hooks/useAssessmentFlow'

interface InitialAssessmentProps {
  autoStart?: boolean
  onComplete?: () => void
}

function denseAnswers<T>(count: number, answers: T[], isFilled: (value: T | undefined) => boolean) {
  return Array.from({ length: count }, (_, index) => answers[index]).every(isFilled)
}

export function InitialAssessment({ autoStart = false, onComplete }: InitialAssessmentProps) {
  const flow = useAssessmentFlow()
  const autoStarted = useRef(false)
  const [questionIndex, setQuestionIndex] = useState(0)
  const [vocabAnswers, setVocabAnswers] = useState<number[]>([])
  const [spellingAnswers, setSpellingAnswers] = useState<string[]>([])
  const [sentenceAnswers, setSentenceAnswers] = useState<string[]>([])
  const [readingIndex, setReadingIndex] = useState(-1)
  const [localError, setLocalError] = useState<string | null>(null)

  const vocabAnswersRef = useRef(vocabAnswers)
  const spellingAnswersRef = useRef(spellingAnswers)
  const sentenceAnswersRef = useRef(sentenceAnswers)
  vocabAnswersRef.current = vocabAnswers
  spellingAnswersRef.current = spellingAnswers
  sentenceAnswersRef.current = sentenceAnswers

  useEffect(() => {
    if (!autoStart || autoStarted.current || flow.assessmentId) {
      return
    }
    autoStarted.current = true
    void flow.start()
  }, [autoStart, flow.assessmentId, flow.start])

  useEffect(() => {
    if (flow.step === 5 && flow.finalResult) {
      onComplete?.()
    }
  }, [flow.step, flow.finalResult, onComplete])

  useEffect(() => {
    setQuestionIndex(0)
    setLocalError(null)
  }, [flow.step])

  function currentAnswerFilled(): boolean {
    if (flow.step === 1) {
      return vocabAnswers[questionIndex] !== undefined
    }
    if (flow.step === 2) {
      return (spellingAnswers[questionIndex] ?? '').trim().length > 0
    }
    if (flow.step === 3) {
      return (sentenceAnswers[questionIndex] ?? '').trim().length > 0
    }
    if (flow.step === 4) {
      return readingIndex >= 0
    }
    return true
  }

  function handlePrevious() {
    setLocalError(null)
    setQuestionIndex((value) => Math.max(0, value - 1))
  }

  async function handleNext() {
    setLocalError(null)

    if (flow.step === 1) {
      const total = flow.vocabQuestions.length
      if (questionIndex < total - 1) {
        setQuestionIndex((value) => value + 1)
        return
      }
      const answers = vocabAnswersRef.current
      if (!denseAnswers(total, answers, (value) => typeof value === 'number' && value >= 0)) {
        setLocalError('请完成本阶段所有题目后再进入下一步。')
        return
      }
      await flow.submitVocab(answers)
      return
    }

    if (flow.step === 2) {
      const total = flow.spellingQuestions.length
      if (questionIndex < total - 1) {
        setQuestionIndex((value) => value + 1)
        return
      }
      const answers = spellingAnswersRef.current
      if (!denseAnswers(total, answers, (value) => typeof value === 'string' && value.trim().length > 0)) {
        setLocalError('请完成本阶段所有题目后再进入下一步。')
        return
      }
      await flow.submitSpelling(answers)
      return
    }

    if (flow.step === 3) {
      const total = flow.sentenceQuestions.length
      if (questionIndex < total - 1) {
        setQuestionIndex((value) => value + 1)
        return
      }
      const answers = sentenceAnswersRef.current
      if (!denseAnswers(total, answers, (value) => typeof value === 'string' && value.trim().length > 0)) {
        setLocalError('请完成本阶段所有题目后再进入下一步。')
        return
      }
      await flow.submitSentence(answers)
      return
    }

    if (flow.step === 4 && readingIndex >= 0) {
      await flow.submitReading(readingIndex, 0)
    }
  }

  function nextLabel() {
    if (flow.step === 4) return flow.submitting ? '提交中...' : '提交并定级'
    const totals = [flow.vocabQuestions.length, flow.spellingQuestions.length, flow.sentenceQuestions.length]
    const total = totals[flow.step - 1] ?? 1
    if (flow.submitting) return '提交中...'
    return questionIndex < total - 1 ? '下一个' : '下一步'
  }

  function handleTimelineClick(targetStep: number) {
    if (targetStep === flow.step) return
    setLocalError(null)
    flow.goToStep(targetStep)
  }

  const displayError = localError ?? flow.stepError

  if (!flow.assessmentId) {
    return (
      <section className="rounded-md border border-neutral-200 bg-white p-6">
        <h2 className="text-2xl font-semibold">首次水平测评</h2>
        <p className="mt-2 text-sm text-neutral-600">5 步测评：词汇 → 拼写 → 造句 → 阅读 → 定级</p>
        {flow.error && <p className="mt-3 text-sm text-rose-700">{flow.error}</p>}
        {(autoStart || flow.loading) && !flow.error && !flow.assessmentId ? (
          <p className="mt-4 text-sm text-neutral-600">{flow.loading ? '正在准备测评...' : '即将开始...'}</p>
        ) : (
          <button
            type="button"
            onClick={() => void flow.start()}
            disabled={flow.loading}
            className="mt-4 inline-flex h-11 items-center rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white disabled:opacity-60"
          >
            {flow.loading ? '准备中...' : '开始测评'}
          </button>
        )}
      </section>
    )
  }

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-6">
      <AssessmentTimeline
        steps={flow.steps}
        currentStep={flow.step}
        maxReachedStep={flow.maxReachedStep}
        onStepClick={handleTimelineClick}
      />

      {displayError && (
        <p className="mt-4 rounded-md bg-rose-50 px-3 py-2 text-sm text-rose-800">{displayError}</p>
      )}

      {flow.submitting && (
        <p className="mt-4 text-sm text-neutral-600">正在提交并加载下一阶段...</p>
      )}

      {flow.step === 1 && flow.vocabQuestions.length > 0 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇识别</h3>
          <p className="text-2xl font-semibold text-neutral-900">{flow.vocabQuestions[questionIndex]?.word}</p>
          <OptionTags
            options={flow.vocabQuestions[questionIndex]?.options ?? []}
            selectedIndex={vocabAnswers[questionIndex]}
            disabled={flow.submitting}
            onSelect={(optionIndex) => {
              const next = [...vocabAnswers]
              next[questionIndex] = optionIndex
              setVocabAnswers(next)
            }}
          />
          <StepNavigator
            index={questionIndex}
            total={flow.vocabQuestions.length}
            onPrevious={handlePrevious}
            onNext={() => void handleNext()}
            canPrevious={questionIndex > 0 && !flow.submitting}
            canNext={currentAnswerFilled() && !flow.submitting}
            nextLabel={nextLabel()}
          />
        </div>
      )}

      {flow.step === 2 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">拼写测评</h3>
          {flow.spellingQuestions.length === 0 ? (
            <p className="text-sm text-neutral-600">正在加载拼写题目...</p>
          ) : (
            <>
              <label className="block rounded-md border border-neutral-200 p-4 text-sm">
                <span className="text-base font-medium">{flow.spellingQuestions[questionIndex]?.chinese}</span>
                <input
                  className="mt-3 h-11 w-full rounded-md border border-neutral-300 px-3"
                  value={spellingAnswers[questionIndex] ?? ''}
                  disabled={flow.submitting}
                  onChange={(event) => {
                    const next = [...spellingAnswers]
                    next[questionIndex] = event.target.value
                    setSpellingAnswers(next)
                  }}
                  placeholder="输入英文拼写"
                />
              </label>
              <StepNavigator
                index={questionIndex}
                total={flow.spellingQuestions.length}
                onPrevious={handlePrevious}
                onNext={() => void handleNext()}
                canPrevious={questionIndex > 0 && !flow.submitting}
                canNext={currentAnswerFilled() && !flow.submitting}
                nextLabel={nextLabel()}
              />
            </>
          )}
        </div>
      )}

      {flow.step === 3 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">造句测评</h3>
          {flow.sentenceQuestions.length === 0 ? (
            <p className="text-sm text-neutral-600">正在加载造句题目...</p>
          ) : (
            <>
              <label className="block rounded-md border border-neutral-200 p-4 text-sm">
                <span className="text-base font-medium">使用单词：{flow.sentenceQuestions[questionIndex]?.word}</span>
                <textarea
                  className="mt-3 w-full rounded-md border border-neutral-300 px-3 py-2"
                  rows={4}
                  value={sentenceAnswers[questionIndex] ?? ''}
                  disabled={flow.submitting}
                  onChange={(event) => {
                    const next = [...sentenceAnswers]
                    next[questionIndex] = event.target.value
                    setSentenceAnswers(next)
                  }}
                  placeholder="用该单词造一个句子"
                />
              </label>
              <StepNavigator
                index={questionIndex}
                total={flow.sentenceQuestions.length}
                onPrevious={handlePrevious}
                onNext={() => void handleNext()}
                canPrevious={questionIndex > 0 && !flow.submitting}
                canNext={currentAnswerFilled() && !flow.submitting}
                nextLabel={nextLabel()}
              />
            </>
          )}
        </div>
      )}

      {flow.step === 4 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读测评</h3>
          {!flow.readingPayload ? (
            <p className="text-sm text-neutral-600">正在加载阅读题目...</p>
          ) : (
            <>
              <p className="text-sm font-medium">{flow.readingPayload.title}</p>
              <p className="text-sm leading-6 text-neutral-700">{flow.readingPayload.content}</p>
              <div className="rounded-md border border-neutral-200 p-4">
                <p className="text-sm font-medium">{flow.readingPayload.question.question}</p>
                <div className="mt-3">
                  <OptionTags
                    options={flow.readingPayload.question.options}
                    selectedIndex={readingIndex >= 0 ? readingIndex : undefined}
                    disabled={flow.submitting}
                    onSelect={setReadingIndex}
                  />
                </div>
              </div>
              <StepNavigator
                index={0}
                total={1}
                onPrevious={() => flow.goToStep(3)}
                onNext={() => void handleNext()}
                canPrevious={!flow.submitting}
                canNext={readingIndex >= 0 && !flow.submitting}
                nextLabel={nextLabel()}
                showProgress={false}
              />
            </>
          )}
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
