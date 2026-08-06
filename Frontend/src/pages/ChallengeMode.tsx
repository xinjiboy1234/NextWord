import { useEffect, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { AssessmentTimeline } from '../components/AssessmentTimeline'
import { ChallengeRecentList } from '../components/ChallengeRecentList'
import { OptionTags } from '../components/OptionTags'
import { StepNavigator } from '../components/StepNavigator'
import { useChallengeFlow } from '../hooks/useChallengeFlow'
import { nextCefrLevel } from '../lib/cefr'
import type { ProgressSummary } from '../types/models'

type ChallengePhase = 'vocab' | 'sentence' | 'reading'

export function ChallengeMode() {
  const challenge = useChallengeFlow()
  const location = useLocation()
  const [phase, setPhase] = useState<ChallengePhase>('vocab')
  const [vocabIndex, setVocabIndex] = useState(0)
  const [vocabAnswers, setVocabAnswers] = useState<number[]>([])
  const [sentenceText, setSentenceText] = useState('')
  const [readingIndex, setReadingIndex] = useState(0)
  const [readingAnswers, setReadingAnswers] = useState<number[]>([])
  const [lookupCount, setLookupCount] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [maxReachedStep, setMaxReachedStep] = useState(1)
  const [progress, setProgress] = useState<ProgressSummary | null>(null)

  const phaseStep = phase === 'vocab' ? 1 : phase === 'sentence' ? 2 : 3

  useEffect(() => {
    if (!challenge.pack) {
      setPhase('vocab')
      setVocabIndex(0)
      setVocabAnswers([])
      setSentenceText('')
      setReadingIndex(0)
      setReadingAnswers([])
      setLookupCount(0)
      setMaxReachedStep(1)
    }
  }, [challenge.pack])

  // T-035：挑战页拉取进度，用于升级候选强引导
  useEffect(() => {
    api.get<ProgressSummary>(endpoints.progress)
      .then((response) => setProgress(response.data))
      .catch(() => setProgress(null))
  }, [])

  // T-035：Dashboard 引导条跳转而来时自动发起确认挑战
  useEffect(() => {
    const state = location.state as { confirmation?: boolean } | null
    if (state?.confirmation && !challenge.pack && !challenge.loading) {
      void challenge.start(true)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const readings = challenge.pack?.readings ?? []
  const allReadingsAnswered = readings.length > 0 && readings.every((_, index) => readingAnswers[index] !== undefined)

  async function submitChallenge() {
    if (!challenge.pack || !allReadingsAnswered || submitting) return
    setSubmitting(true)
    try {
      await challenge.submit({
        vocabAnswers,
        sentenceAnswer: sentenceText,
        targetWord: challenge.pack.sentence.word,
        scene: challenge.pack.sentence.scene,
        sentenceWordId: challenge.pack.sentence.wordId,
        readingSelectedIndexes: readings.map((_, index) => readingAnswers[index]),
        lookupCount,
        confirmation: challenge.isConfirmation,
      })
    } finally {
      setSubmitting(false)
    }
  }

  const upgradeNextLevel = progress?.isUpgradeCandidate ? nextCefrLevel(progress.overallLevel) : null
  const upgradeBanner = upgradeNextLevel && (
    <div className="alert alert-info" style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
      <p style={{ flex: 1, minWidth: 200 }}>
        你已具备冲击 <strong>{upgradeNextLevel}</strong> 的实力，来确认挑战。
      </p>
      {!challenge.pack && (
        <button
          type="button"
          className="btn btn-sm btn-primary"
          onClick={() => void challenge.start(true)}
          disabled={challenge.loading}
        >
          发起确认挑战
        </button>
      )}
    </div>
  )

  if (!challenge.pack) {
    return (
      <div className="stack stack-md">
        {upgradeBanner}
        <section className="card">
          <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>挑战测评</h2>
          <p style={{ marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>词汇 + 造句 + 阅读综合挑战，逐题完成。</p>
          <button
            type="button"
            onClick={() => void challenge.start(false)}
            disabled={challenge.loading}
            className="btn btn-primary"
            style={{ marginTop: 'var(--space-4)' }}
          >
            {challenge.loading ? '生成挑战包...' : '开始挑战'}
          </button>
        </section>
        <section className="card stack stack-sm">
          <h3 style={{ fontWeight: 540 }}>近期挑战</h3>
          <ChallengeRecentList refreshKey={challenge.result?.totalScore} />
        </section>
      </div>
    )
  }

  const vocabQuestion = challenge.pack.vocabulary[vocabIndex]
  const readingQuestion = readings[readingIndex]

  return (
    <section className="card stack stack-md">
      {upgradeBanner}
      <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
        {challenge.isConfirmation ? '确认挑战' : '挑战测评'} · {challenge.pack.attemptedLevel}
      </h2>
      <AssessmentTimeline
        steps={['词汇', '造句', '阅读']}
        currentStep={phaseStep}
        maxReachedStep={maxReachedStep}
        maxNavigableStep={3}
        onStepClick={(step) => {
          if (step === 1) setPhase('vocab')
          if (step === 2) setPhase('sentence')
          if (step === 3) setPhase('reading')
        }}
      />

      {phase === 'vocab' && vocabQuestion && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇挑战</h3>
          <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>{vocabQuestion.word}</p>
          <OptionTags
            options={vocabQuestion.options}
            selectedIndex={vocabAnswers[vocabIndex]}
            onSelect={(optionIndex) => {
              const next = [...vocabAnswers]
              next[vocabIndex] = optionIndex
              setVocabAnswers(next)
            }}
          />
          <StepNavigator
            index={vocabIndex}
            total={challenge.pack.vocabulary.length}
            onPrevious={() => setVocabIndex((value) => Math.max(0, value - 1))}
            onNext={() => {
              if (vocabIndex < challenge.pack!.vocabulary.length - 1) {
                setVocabIndex((value) => value + 1)
                return
              }
              setMaxReachedStep((value) => Math.max(value, 2))
              setPhase('sentence')
            }}
            canPrevious={vocabIndex > 0}
            canNext={vocabAnswers[vocabIndex] !== undefined}
            nextLabel={vocabIndex < challenge.pack.vocabulary.length - 1 ? '下一个' : '下一步'}
          />
        </div>
      )}

      {phase === 'sentence' && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">造句挑战</h3>
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>使用单词：{challenge.pack.sentence.word}</p>
          <textarea
            className="textarea"
            rows={4}
            value={sentenceText}
            disabled={submitting || Boolean(challenge.result)}
            onChange={(event) => setSentenceText(event.target.value)}
            placeholder="用目标词造句"
            autoComplete="off"
          />
          <StepNavigator
            index={0}
            total={1}
            onPrevious={() => {
              setPhase('vocab')
              setVocabIndex(challenge.pack!.vocabulary.length - 1)
            }}
            onNext={() => {
              setMaxReachedStep((value) => Math.max(value, 3))
              setPhase('reading')
            }}
            canNext={sentenceText.trim().length > 0}
            nextLabel="下一步"
            showProgress={false}
          />
        </div>
      )}

      {phase === 'reading' && readingQuestion && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读挑战</h3>
          <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.7, color: 'var(--muted)' }}>{readingQuestion.articleExcerpt}</p>
          <div className="card" style={{ padding: 'var(--space-4)' }}>
            <p className="text-sm font-medium">{readingQuestion.question}</p>
            <div className="mt-3">
              <OptionTags
                options={readingQuestion.options}
                selectedIndex={readingAnswers[readingIndex]}
                onSelect={(optionIndex) => {
                  const next = [...readingAnswers]
                  next[readingIndex] = optionIndex
                  setReadingAnswers(next)
                }}
              />
            </div>
          </div>
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
            阅读查词次数：{lookupCount}（可在阅读模块查词后计入）
          </p>
          <StepNavigator
            index={readingIndex}
            total={readings.length}
            onPrevious={() => {
              if (readingIndex > 0) {
                setReadingIndex((value) => value - 1)
                return
              }
              setPhase('sentence')
            }}
            onNext={() => {
              if (readingIndex < readings.length - 1) {
                setReadingIndex((value) => value + 1)
                return
              }
              void submitChallenge()
            }}
            canPrevious
            canNext={readingAnswers[readingIndex] !== undefined && !challenge.result && !submitting}
            nextLabel={
              readingIndex < readings.length - 1
                ? '下一篇'
                : submitting
                  ? '提交中...'
                  : '提交挑战结果'
            }
          />
        </div>
      )}

      {challenge.result && (
        <div className={`alert ${challenge.result.passed ? 'alert-success' : 'alert-error'}`} style={{ marginTop: 'var(--space-4)' }}>
          <p>{challenge.result.passed ? '挑战成功' : '挑战未通过'} · 总分 {challenge.result.totalScore.toFixed(0)}/100</p>
          <p style={{ fontSize: 'var(--text-sm)', marginTop: 4 }}>
            词汇 {challenge.result.vocabularyScore.toFixed(0)}/100 · 写作 {challenge.result.writingScore.toFixed(0)}/100 · 阅读 {challenge.result.readingScore.toFixed(0)}/100
          </p>
          {challenge.result.passed && challenge.result.feedback && (
            <p style={{ fontSize: 'var(--text-sm)', marginTop: 4 }}>{challenge.result.feedback}</p>
          )}
          {challenge.result.passed && challenge.result.passCount != null && (
            <p style={{ fontSize: 'var(--text-sm)', marginTop: 4 }}>
              这是你第 <strong>{challenge.result.passCount}</strong> 次通过挑战。
            </p>
          )}
          {!challenge.result.passed && (
            <p style={{ fontSize: 'var(--text-sm)', marginTop: 4 }}>
              差一点点，回看短板维度的得分，明天再来一次。
            </p>
          )}
        </div>
      )}
    </section>
  )
}
