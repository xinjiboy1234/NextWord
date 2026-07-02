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
  const [vocabIndex, setVocabIndex] = useState(0)
  const [spellingIndex, setSpellingIndex] = useState(0)
  const [sentenceIndex, setSentenceIndex] = useState(0)
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

  function currentAnswerFilled(): boolean {
    if (flow.step === 1) {
      return vocabAnswers[vocabIndex] !== undefined
    }
    if (flow.step === 2) {
      return (spellingAnswers[spellingIndex] ?? '').trim().length > 0
    }
    if (flow.step === 3) {
      return (sentenceAnswers[sentenceIndex] ?? '').trim().length > 0
    }
    if (flow.step === 4) {
      return readingIndex >= 0
    }
    return true
  }

  function handlePrevious() {
    setLocalError(null)
    if (flow.step === 1) setVocabIndex((value) => Math.max(0, value - 1))
    if (flow.step === 2) setSpellingIndex((value) => Math.max(0, value - 1))
    if (flow.step === 3) setSentenceIndex((value) => Math.max(0, value - 1))
  }

  async function handleNext() {
    setLocalError(null)

    if (flow.step === 1) {
      const total = flow.vocabQuestions.length
      if (vocabIndex < total - 1) {
        setVocabIndex((value) => value + 1)
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
      if (spellingIndex < total - 1) {
        setSpellingIndex((value) => value + 1)
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
      if (sentenceIndex < total - 1) {
        setSentenceIndex((value) => value + 1)
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
    const indices = [vocabIndex, spellingIndex, sentenceIndex]
    const index = indices[flow.step - 1] ?? 0
    return index < total - 1 ? '下一个' : '下一步'
  }

  function canGoPrevious(): boolean {
    if (flow.submitting) return false
    if (flow.step === 1) return vocabIndex > 0
    if (flow.step === 2) return spellingIndex > 0
    if (flow.step === 3) return sentenceIndex > 0
    return false
  }

  const displayError = localError ?? flow.stepError

  if (!flow.assessmentId) {
    return (
      <section className="card">
        <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>首次水平测评</h2>
        <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>5 步测评：词汇 → 拼写 → 造句 → 阅读 → 定级</p>
        {flow.error && <p className="alert alert-error" style={{ marginTop: 'var(--space-3)' }}>{flow.error}</p>}
        {(autoStart || flow.loading) && !flow.error && !flow.assessmentId ? (
          <p style={{ marginTop: 'var(--space-4)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>{flow.loading ? '正在准备测评...' : '即将开始...'}</p>
        ) : (
          <button
            type="button"
            onClick={() => void flow.start()}
            disabled={flow.loading}
            className="btn btn-primary"
            style={{ marginTop: 'var(--space-4)' }}
          >
            {flow.loading ? '准备中...' : '开始测评'}
          </button>
        )}
      </section>
    )
  }

  return (
    <section className="card stack stack-md">
      <AssessmentTimeline
        steps={flow.steps}
        currentStep={flow.step}
        maxReachedStep={flow.maxReachedStep}
        maxNavigableStep={flow.finalResult ? 5 : 4}
        onStepClick={(targetStep) => {
          if (targetStep === flow.step) return
          setLocalError(null)
          void flow.goToStep(targetStep)
        }}
      />

      {displayError && (
        <p className="alert alert-error">{displayError}</p>
      )}

      {flow.submitting && (
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在提交并加载下一阶段...</p>
      )}

      {flow.loadingStep && !flow.submitting && (
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在加载题目...</p>
      )}

      {flow.step === 1 && flow.vocabQuestions.length > 0 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇识别</h3>
          <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>{flow.vocabQuestions[vocabIndex]?.word}</p>
          <OptionTags
            options={flow.vocabQuestions[vocabIndex]?.options ?? []}
            selectedIndex={vocabAnswers[vocabIndex]}
            disabled={flow.submitting}
            onSelect={(optionIndex) => {
              const next = [...vocabAnswers]
              next[vocabIndex] = optionIndex
              setVocabAnswers(next)
            }}
          />
          <StepNavigator
            index={vocabIndex}
            total={flow.vocabQuestions.length}
            onPrevious={handlePrevious}
            onNext={() => void handleNext()}
            canPrevious={canGoPrevious()}
            canNext={currentAnswerFilled() && !flow.submitting}
            nextLabel={nextLabel()}
          />
        </div>
      )}

      {flow.step === 2 && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">拼写测评</h3>
          {flow.spellingQuestions.length === 0 ? (
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在加载拼写题目...</p>
          ) : (
            <>
              <label className="card field" style={{ display: 'block' }}>
                <span style={{ fontSize: 'var(--text-base)', fontWeight: 540 }}>{flow.spellingQuestions[spellingIndex]?.chinese}</span>
                <input
                  className="input"
                  style={{ marginTop: 'var(--space-3)' }}
                  value={spellingAnswers[spellingIndex] ?? ''}
                  disabled={flow.submitting}
                  onChange={(event) => {
                    const next = [...spellingAnswers]
                    next[spellingIndex] = event.target.value
                    setSpellingAnswers(next)
                  }}
                  placeholder="输入英文拼写"
                />
              </label>
              <StepNavigator
                index={spellingIndex}
                total={flow.spellingQuestions.length}
                onPrevious={handlePrevious}
                onNext={() => void handleNext()}
                canPrevious={canGoPrevious()}
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
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在加载造句题目...</p>
          ) : (
            <>
              <label className="card field" style={{ display: 'block' }}>
                <span style={{ fontSize: 'var(--text-base)', fontWeight: 540 }}>使用单词：{flow.sentenceQuestions[sentenceIndex]?.word}</span>
                <textarea
                  className="textarea"
                  style={{ marginTop: 'var(--space-3)' }}
                  rows={4}
                  value={sentenceAnswers[sentenceIndex] ?? ''}
                  disabled={flow.submitting}
                  onChange={(event) => {
                    const next = [...sentenceAnswers]
                    next[sentenceIndex] = event.target.value
                    setSentenceAnswers(next)
                  }}
                  placeholder="用该单词造一个句子"
                />
              </label>
              <StepNavigator
                index={sentenceIndex}
                total={flow.sentenceQuestions.length}
                onPrevious={handlePrevious}
                onNext={() => void handleNext()}
                canPrevious={canGoPrevious()}
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
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在加载阅读题目...</p>
          ) : (
            <>
              <p className="text-sm font-medium">{flow.readingPayload.title}</p>
              <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.7, color: 'var(--muted)' }}>{flow.readingPayload.content}</p>
              <div className="card" style={{ padding: 'var(--space-4)' }}>
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
        <div className="alert alert-success" style={{ marginTop: 'var(--space-4)' }}>
          <h3 style={{ fontWeight: 540 }}>定级结果</h3>
          <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }}>总体等级：{flow.finalResult.overallLevel}</p>
          {flow.finalResult.overallScore != null && (
            <p style={{ fontSize: 'var(--text-sm)' }}>综合 Score：{flow.finalResult.overallScore.toFixed(0)}</p>
          )}
          <ul style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }} className="stack stack-sm">
            <li>词汇：{flow.finalResult.vocabLevel}{flow.finalResult.vocabularyScore != null ? ` (${flow.finalResult.vocabularyScore.toFixed(0)})` : ''}</li>
            <li>拼写：{flow.finalResult.spellingLevel}{flow.finalResult.spellingScore != null ? ` (${flow.finalResult.spellingScore.toFixed(0)})` : ''}</li>
            <li>造句：{flow.finalResult.sentenceLevel}{flow.finalResult.writingScore != null ? ` (${flow.finalResult.writingScore.toFixed(0)})` : ''}</li>
            <li>阅读：{flow.finalResult.readingLevel}{flow.finalResult.readingScore != null ? ` (${flow.finalResult.readingScore.toFixed(0)})` : ''}</li>
          </ul>
          {flow.finalResult.evaluationReportId && (
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)', marginTop: 'var(--space-2)' }}>
              评估报告 #{flow.finalResult.evaluationReportId} 生成中，可在等级面板查看。
            </p>
          )}
        </div>
      )}
    </section>
  )
}
