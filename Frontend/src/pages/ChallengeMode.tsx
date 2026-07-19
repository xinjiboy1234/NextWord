import { useEffect, useState } from 'react'
import { AssessmentTimeline } from '../components/AssessmentTimeline'
import { ChallengeRecentList } from '../components/ChallengeRecentList'
import { OptionTags } from '../components/OptionTags'
import { StepNavigator } from '../components/StepNavigator'
import { useChallengeFlow } from '../hooks/useChallengeFlow'

type ChallengePhase = 'vocab' | 'sentence' | 'reading'

export function ChallengeMode() {
  const challenge = useChallengeFlow()
  const [phase, setPhase] = useState<ChallengePhase>('vocab')
  const [vocabIndex, setVocabIndex] = useState(0)
  const [vocabAnswers, setVocabAnswers] = useState<number[]>([])
  const [sentenceText, setSentenceText] = useState('')
  const [readingIndex, setReadingIndex] = useState(-1)
  const [lookupCount, setLookupCount] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [maxReachedStep, setMaxReachedStep] = useState(1)

  const phaseStep = phase === 'vocab' ? 1 : phase === 'sentence' ? 2 : 3

  useEffect(() => {
    if (!challenge.pack) {
      setPhase('vocab')
      setVocabIndex(0)
      setVocabAnswers([])
      setSentenceText('')
      setReadingIndex(-1)
      setLookupCount(0)
      setMaxReachedStep(1)
    }
  }, [challenge.pack])

  async function submitChallenge() {
    if (!challenge.pack || readingIndex < 0 || submitting) return
    setSubmitting(true)
    try {
      await challenge.submit({
        vocabAnswers,
        sentenceAnswer: sentenceText,
        targetWord: challenge.pack.sentence.word,
        scene: challenge.pack.sentence.scene,
        sentenceWordId: challenge.pack.sentence.wordId,
        readingSelectedIndex: readingIndex,
        lookupCount,
      })
    } finally {
      setSubmitting(false)
    }
  }

  if (!challenge.pack) {
    return (
      <div className="stack stack-md">
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

  return (
    <section className="card stack stack-md">
      <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
        挑战测评 · {challenge.pack.attemptedLevel}
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

      {phase === 'reading' && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读挑战</h3>
          <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.7, color: 'var(--muted)' }}>{challenge.pack.reading.articleExcerpt}</p>
          <div className="card" style={{ padding: 'var(--space-4)' }}>
            <p className="text-sm font-medium">{challenge.pack.reading.question}</p>
            <div className="mt-3">
              <OptionTags
                options={challenge.pack.reading.options}
                selectedIndex={readingIndex >= 0 ? readingIndex : undefined}
                onSelect={setReadingIndex}
              />
            </div>
          </div>
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
            阅读查词次数：{lookupCount}（可在阅读模块查词后计入）
          </p>
          <StepNavigator
            index={0}
            total={1}
            onPrevious={() => setPhase('sentence')}
            onNext={() => void submitChallenge()}
            canNext={readingIndex >= 0 && !challenge.result && !submitting}
            nextLabel={submitting ? '提交中...' : '提交挑战结果'}
            showProgress={false}
          />
        </div>
      )}

      {challenge.result && (
        <div className={`alert ${challenge.result.passed ? 'alert-success' : 'alert-error'}`} style={{ marginTop: 'var(--space-4)' }}>
          <p>{challenge.result.passed ? '挑战成功' : '挑战未通过'} · 总分 {challenge.result.totalScore.toFixed(0)}</p>
          <p style={{ fontSize: 'var(--text-sm)', marginTop: 4 }}>
            词汇 {challenge.result.vocabularyScore.toFixed(0)} · 写作 {challenge.result.writingScore.toFixed(0)} · 阅读 {challenge.result.readingScore.toFixed(0)}
          </p>
        </div>
      )}
    </section>
  )
}
