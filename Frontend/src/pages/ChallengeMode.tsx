import { useMemo, useState } from 'react'
import { useChallengeFlow } from '../hooks/useChallengeFlow'

export function ChallengeMode() {
  const challenge = useChallengeFlow()
  const [vocabCorrect, setVocabCorrect] = useState(0)
  const [sentenceScore, setSentenceScore] = useState(3.5)
  const [readingCorrect, setReadingCorrect] = useState(true)

  const vocabScore = useMemo(() => {
    if (!challenge.pack) return 0
    return (vocabCorrect / challenge.pack.vocabulary.length) * 100
  }, [challenge.pack, vocabCorrect])

  if (!challenge.pack) {
    return (
      <section className="rounded-md border border-neutral-200 bg-white p-6">
        <h2 className="text-2xl font-semibold">挑战测评</h2>
        <p className="mt-2 text-sm text-neutral-600">词汇 + 造句 + 阅读综合挑战。</p>
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

  return (
    <section className="rounded-md border border-neutral-200 bg-white p-6">
      <h2 className="text-2xl font-semibold">挑战测评 · {challenge.pack.attemptedLevel}</h2>

      <div className="mt-4 space-y-4">
        <div>
          <h3 className="font-medium">词汇（自评正确数 / {challenge.pack.vocabulary.length}）</h3>
          <input
            type="number"
            min={0}
            max={challenge.pack.vocabulary.length}
            value={vocabCorrect}
            onChange={(event) => setVocabCorrect(Number(event.target.value))}
            className="mt-1 h-10 w-32 rounded-md border border-neutral-300 px-3"
          />
        </div>

        <div>
          <h3 className="font-medium">造句目标词：{challenge.pack.sentence.word}</h3>
          <label className="mt-1 block text-sm">
            造句评分 (0-5)
            <input
              type="number"
              min={0}
              max={5}
              step={0.1}
              value={sentenceScore}
              onChange={(event) => setSentenceScore(Number(event.target.value))}
              className="mt-1 h-10 w-32 rounded-md border border-neutral-300 px-3"
            />
          </label>
        </div>

        <div>
          <h3 className="font-medium">阅读：{challenge.pack.reading.question}</h3>
          <p className="mt-1 text-sm text-neutral-700">{challenge.pack.reading.articleExcerpt}</p>
          <label className="mt-2 inline-flex items-center gap-2 text-sm">
            <input type="checkbox" checked={readingCorrect} onChange={(event) => setReadingCorrect(event.target.checked)} />
            阅读题答对
          </label>
        </div>
      </div>

      <button
        type="button"
        onClick={() => void challenge.submit(vocabScore, sentenceScore, readingCorrect ? 100 : 0)}
        className="mt-5 inline-flex h-10 items-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white"
      >
        提交挑战结果
      </button>

      {challenge.result && (
        <p className={`mt-4 rounded-md p-3 text-sm ${challenge.result.passed ? 'bg-emerald-50 text-emerald-900' : 'bg-amber-50 text-amber-900'}`}>
          {challenge.result.passed ? '挑战成功' : '挑战未通过'} · 总分 {challenge.result.totalScore}
        </p>
      )}
    </section>
  )
}
