import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { AssessmentTimeline } from '../components/AssessmentTimeline'
import { OptionTags } from '../components/OptionTags'
import { StepNavigator } from '../components/StepNavigator'
import { useChallengeFlow } from '../hooks/useChallengeFlow'
import type { SentenceRating } from '../types/sentence'

type ChallengePhase = 'vocab' | 'sentence' | 'reading'

function sentenceScoreFromRating(rating: SentenceRating) {
  return (rating.grammarScore + rating.naturalScore + rating.vocabularyScore + rating.relevanceScore) / 4
}

export function ChallengeMode() {
  const challenge = useChallengeFlow()
  const [phase, setPhase] = useState<ChallengePhase>('vocab')
  const [vocabIndex, setVocabIndex] = useState(0)
  const [vocabAnswers, setVocabAnswers] = useState<number[]>([])
  const [sentenceText, setSentenceText] = useState('')
  const [sentenceRating, setSentenceRating] = useState<SentenceRating | null>(null)
  const [readingIndex, setReadingIndex] = useState(-1)
  const [submitting, setSubmitting] = useState(false)
  const [maxReachedStep, setMaxReachedStep] = useState(1)

  const phaseStep = phase === 'vocab' ? 1 : phase === 'sentence' ? 2 : 3

  useEffect(() => {
    if (!challenge.pack) {
      setPhase('vocab')
      setVocabIndex(0)
      setVocabAnswers([])
      setSentenceText('')
      setSentenceRating(null)
      setReadingIndex(-1)
      setMaxReachedStep(1)
    }
  }, [challenge.pack])

  const vocabScore = useMemo(() => {
    if (!challenge.pack || challenge.pack.vocabulary.length === 0) return 0
    const correct = challenge.pack.vocabulary.reduce((count, question, index) => {
      return count + (vocabAnswers[index] === question.correctIndex ? 1 : 0)
    }, 0)
    return (correct / challenge.pack.vocabulary.length) * 100
  }, [challenge.pack, vocabAnswers])

  async function rateSentence() {
    if (!challenge.pack || sentenceText.trim().length === 0) return
    setSubmitting(true)
    try {
      const { data } = await api.post<SentenceRating>(endpoints.sentenceRate, {
        wordId: challenge.pack.sentence.wordId,
        targetWord: challenge.pack.sentence.word,
        userSentence: sentenceText,
        scene: challenge.pack.sentence.scene,
        userLevel: challenge.pack.attemptedLevel,
      })
      setSentenceRating(data)
      setMaxReachedStep((value) => Math.max(value, 3))
    } finally {
      setSubmitting(false)
    }
  }

  async function submitChallenge() {
    if (!challenge.pack || readingIndex < 0) return
    const readingCorrect = readingIndex === challenge.pack.reading.correctIndex
    const sentenceScore = sentenceRating ? sentenceScoreFromRating(sentenceRating) : 0
    await challenge.submit(vocabScore, sentenceScore, readingCorrect ? 100 : 0)
  }

  if (!challenge.pack) {
    return (
      <section className="rounded-md border border-neutral-200 bg-white p-6">
        <h2 className="text-2xl font-semibold">挑战测评</h2>
        <p className="mt-2 text-sm text-neutral-600">词汇 + 造句 + 阅读综合挑战，逐题完成。</p>
        <button
          type="button"
          onClick={() => void challenge.start(false)}
          disabled={challenge.loading}
          className="mt-4 inline-flex h-11 items-center rounded-md bg-emerald-700 px-4 text-sm font-semibold text-white"
        >
          {challenge.loading ? '生成挑战包...' : '开始挑战'}
        </button>
      </section>
    )
  }

  const vocabQuestion = challenge.pack.vocabulary[vocabIndex]

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-6">
      <h2 className="text-2xl font-semibold">挑战测评 · {challenge.pack.attemptedLevel}</h2>
      <AssessmentTimeline
        steps={['词汇', '造句', '阅读']}
        currentStep={phaseStep}
        maxReachedStep={maxReachedStep}
        onStepClick={(step) => {
          if (step === 1) setPhase('vocab')
          if (step === 2) setPhase('sentence')
          if (step === 3) setPhase('reading')
        }}
      />

      {phase === 'vocab' && vocabQuestion && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">词汇挑战</h3>
          <p className="text-2xl font-semibold">{vocabQuestion.word}</p>
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
          <p className="text-sm text-neutral-600">使用单词：{challenge.pack.sentence.word}</p>
          <textarea
            className="w-full rounded-md border border-neutral-300 px-3 py-2"
            rows={4}
            value={sentenceText}
            disabled={Boolean(sentenceRating) || submitting}
            onChange={(event) => setSentenceText(event.target.value)}
            placeholder="用目标词造句"
          />
          {sentenceRating && (
            <p className="rounded-md bg-emerald-50 p-3 text-sm text-emerald-900">
              评分：{sentenceScoreFromRating(sentenceRating).toFixed(1)} / 5
            </p>
          )}
          <StepNavigator
            index={0}
            total={1}
            onPrevious={() => {
              setPhase('vocab')
              setVocabIndex(challenge.pack!.vocabulary.length - 1)
            }}
            onNext={() => {
              if (!sentenceRating) {
                void rateSentence()
                return
              }
              setPhase('reading')
            }}
            canNext={sentenceText.trim().length > 0 && !submitting}
            nextLabel={sentenceRating ? '下一步' : '提交评分'}
            showProgress={false}
          />
        </div>
      )}

      {phase === 'reading' && (
        <div className="mt-5 space-y-4">
          <h3 className="text-lg font-semibold">阅读挑战</h3>
          <p className="text-sm leading-6 text-neutral-700">{challenge.pack.reading.articleExcerpt}</p>
          <div className="rounded-md border border-neutral-200 p-4">
            <p className="text-sm font-medium">{challenge.pack.reading.question}</p>
            <div className="mt-3">
              <OptionTags
                options={challenge.pack.reading.options}
                selectedIndex={readingIndex >= 0 ? readingIndex : undefined}
                onSelect={setReadingIndex}
              />
            </div>
          </div>
          <StepNavigator
            index={0}
            total={1}
            onPrevious={() => setPhase('sentence')}
            onNext={() => void submitChallenge()}
            canNext={readingIndex >= 0 && !challenge.result}
            nextLabel="提交挑战结果"
            showProgress={false}
          />
        </div>
      )}

      {challenge.result && (
        <p className={`mt-4 rounded-md p-3 text-sm ${challenge.result.passed ? 'bg-emerald-50 text-emerald-900' : 'bg-amber-50 text-amber-900'}`}>
          {challenge.result.passed ? '挑战成功' : '挑战未通过'} · 总分 {challenge.result.totalScore}
        </p>
      )}
    </section>
  )
}
