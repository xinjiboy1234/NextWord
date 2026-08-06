import { useEffect, useRef, useState } from 'react'
import { OptionTags } from '../components/OptionTags'
import { Progress } from '../components/ui/Progress'
import { useAssessmentFlow } from '../hooks/useAssessmentFlow'
import type { AssessmentAnswerItem } from '../types/assessment'

interface InitialAssessmentProps {
  autoStart?: boolean
  immersive?: boolean
  /** T-030：已完成首次测评的老用户进入 /assessment 时按「重新测评」口径展示 */
  hasCompleted?: boolean
  onComplete?: () => void
  onStepChange?: (step: number) => void
}

/**
 * T-004 自适应分块测评：每块 5 题（提示造句 ×2 + 情境表达 ×1 + 词义选择 ×1 + 阅读理解 ×1），
 * 2–3 块收敛。产出题走 LLM 真实评分，识别题仅作参考。
 */
export function InitialAssessment({ autoStart = false, immersive = false, hasCompleted = false, onComplete, onStepChange }: InitialAssessmentProps) {
  const flow = useAssessmentFlow()
  const autoStarted = useRef(false)
  const [productionAnswers, setProductionAnswers] = useState<Record<string, string>>({})
  const [choiceAnswers, setChoiceAnswers] = useState<Record<string, number>>({})
  const [localError, setLocalError] = useState<string | null>(null)

  useEffect(() => {
    if (!autoStart || autoStarted.current || flow.assessmentId) {
      return
    }
    autoStarted.current = true
    void flow.start()
  }, [autoStart, flow.assessmentId, flow.start])

  // 换块时清空本块作答
  useEffect(() => {
    if (flow.block) {
      setProductionAnswers({})
      setChoiceAnswers({})
      setLocalError(null)
      onStepChange?.(flow.block.blockIndex)
    }
  }, [flow.block, onStepChange])

  useEffect(() => {
    if (flow.finalResult) {
      onComplete?.()
    }
  }, [flow.finalResult, onComplete])

  const block = flow.block

  function allAnswered(): boolean {
    if (!block) return false
    const productionDone = block.production.every((item) => (productionAnswers[item.id] ?? '').trim().length > 0)
    const vocabDone = block.vocabulary.every((item) => choiceAnswers[item.id] !== undefined)
    const readingDone = block.reading === null || choiceAnswers[block.reading.id] !== undefined
    return productionDone && vocabDone && readingDone
  }

  async function handleSubmit() {
    if (!block) return
    if (!allAnswered()) {
      setLocalError('请完成本块所有题目后再提交。')
      return
    }
    setLocalError(null)
    const answers: AssessmentAnswerItem[] = [
      ...block.production.map((item) => ({ id: item.id, text: productionAnswers[item.id] ?? '' })),
      ...block.vocabulary.map((item) => ({ id: item.id, selectedIndex: choiceAnswers[item.id] ?? null })),
      ...(block.reading ? [{ id: block.reading.id, selectedIndex: choiceAnswers[block.reading.id] ?? null, lookupCount: 0 }] : []),
    ]
    await flow.submitBlock(answers)
  }

  const displayError = localError ?? flow.error
  const sectionClass = immersive ? 'onboarding-card' : 'card'

  if (!flow.assessmentId) {
    return (
      <section className={sectionClass}>
        <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
          {hasCompleted ? '重新水平测评' : '首次水平测评'}
        </h2>
        <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          2–3 块、共 10–15 题，以造句和情境表达为主，逐块自适应难度
        </p>
        {hasCompleted && (
          <p className="alert alert-info" style={{ marginTop: 'var(--space-3)' }}>
            你已完成过首次测评，本次结果将覆盖现有定级。
          </p>
        )}
        {displayError && <p className="alert alert-error" style={{ marginTop: 'var(--space-3)' }}>{displayError}</p>}
        {(autoStart || flow.loading) && !displayError ? (
          <p style={{ marginTop: 'var(--space-4)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>{flow.loading ? '正在准备测评...' : '即将开始...'}</p>
        ) : (
          <button
            type="button"
            onClick={() => void flow.start()}
            disabled={flow.loading}
            className="btn btn-primary"
            style={{ marginTop: 'var(--space-4)' }}
          >
            {flow.loading ? '准备中...' : hasCompleted ? '开始重新测评' : '开始测评'}
          </button>
        )}
      </section>
    )
  }

  if (flow.finalResult) {
    const final = flow.finalResult
    return (
      <section className={sectionClass}>
        <div className="alert alert-success">
          <h3 style={{ fontWeight: 540 }}>定级结果</h3>
          <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }}>总体等级：{final.overallLevel}</p>
          <p style={{ fontSize: 'var(--text-sm)' }}>表达力综合分：{final.expressionScore}/100</p>
          <ul style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }} className="stack stack-sm">
            {final.dimensions.comments.map((comment, index) => (
              <li key={index}>{comment}</li>
            ))}
          </ul>
          <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)' }}>
            识别参考（不计入定级）：词汇 {final.vocabularyReferenceScore}（{final.vocabularyReferenceLevel}）、阅读 {final.readingReferenceScore}（{final.readingReferenceLevel}）
          </p>
          {final.evaluationReportId && (
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)', marginTop: 'var(--space-2)' }}>
              评估报告 #{final.evaluationReportId} 生成中，可在等级面板查看。
            </p>
          )}
        </div>
      </section>
    )
  }

  if (!block) {
    return (
      <section className={sectionClass}>
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>正在加载题目...</p>
        {displayError && <p className="alert alert-error" style={{ marginTop: 'var(--space-3)' }}>{displayError}</p>}
      </section>
    )
  }

  return (
    <section className={`${sectionClass} stack stack-md`}>
      {immersive ? (
        <Progress value={block.blockIndex} max={block.maxBlocks} label={`测评进度 第 ${block.blockIndex} 块 / 最多 ${block.maxBlocks} 块`} />
      ) : null}
      <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
        第 {block.blockIndex} 块（难度带 {block.band}）：先写句子，再做两道识别题
      </p>

      {displayError && <p className="alert alert-error">{displayError}</p>}

      {block.production.map((item) => (
        <div key={item.id} className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">{item.kind === 'sentence' ? '提示造句' : '情境表达'}</h3>
          <label className="card field" style={{ display: 'block' }}>
            <span style={{ fontSize: 'var(--text-base)', fontWeight: 540 }}>{item.prompt}</span>
            <textarea
              className="textarea"
              style={{ marginTop: 'var(--space-3)' }}
              rows={item.kind === 'sentence' ? 3 : 4}
              value={productionAnswers[item.id] ?? ''}
              disabled={flow.submitting}
              onChange={(event) => {
                setProductionAnswers((current) => ({ ...current, [item.id]: event.target.value }))
              }}
              placeholder={item.kind === 'sentence' ? '用该单词造一个句子' : '写 2–3 句你的应对或感受'}
              autoComplete="off"
            />
          </label>
        </div>
      ))}

      {block.vocabulary.map((item) => (
        <div key={item.id} className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇识别（参考）</h3>
          <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>{item.word}</p>
          <OptionTags
            options={item.options}
            selectedIndex={choiceAnswers[item.id]}
            disabled={flow.submitting}
            onSelect={(optionIndex) => {
              setChoiceAnswers((current) => ({ ...current, [item.id]: optionIndex }))
            }}
          />
        </div>
      ))}

      {block.reading && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读理解（参考）</h3>
          <p className="text-sm font-medium">{block.reading.title}</p>
          <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.7, color: 'var(--muted)' }}>{block.reading.content}</p>
          <div className="card" style={{ padding: 'var(--space-4)' }}>
            <p className="text-sm font-medium">{block.reading.question}</p>
            <div className="mt-3">
              <OptionTags
                options={block.reading.options}
                selectedIndex={choiceAnswers[block.reading.id]}
                disabled={flow.submitting}
                onSelect={(optionIndex) => {
                  if (!block.reading) return
                  setChoiceAnswers((current) => ({ ...current, [block.reading!.id]: optionIndex }))
                }}
              />
            </div>
          </div>
        </div>
      )}

      <button
        type="button"
        className="btn btn-primary"
        disabled={flow.submitting || !allAnswered()}
        onClick={() => void handleSubmit()}
      >
        {flow.submitting ? '评分中...' : '提交本块'}
      </button>
    </section>
  )
}
