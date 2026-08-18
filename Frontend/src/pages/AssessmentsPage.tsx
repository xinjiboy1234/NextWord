import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { AiRevision } from '../components/AiRevision'
import { ErrorAnalysis } from '../components/ErrorAnalysis'
import { ProficiencyRubric } from '../components/ProficiencyRubric'
import { ScoreCard } from '../components/ScoreCard'
import { Badge } from '../components/ui/Badge'
import type {
  AssessmentAnswerItem,
  AssessmentBlockPayload,
  AssessmentBlockScores,
  AssessmentDetail,
  AssessmentFinalResult,
  AssessmentListItem,
} from '../types/assessment'
import type { SentenceRating } from '../types/sentence'

/**
 * T-054 测评记录页（DESIGN-assessment-visibility R1/R2）：历次测评列表 + 按块按题详情
 * （题目、作答原文、四维分、总评档、AI 评语与改写、错误标签）。
 * 旧记录无 Suggestion/AiRevision 字段时只显示四维分 + 错误标签，优雅降级。
 * T-055：详情头部接入人话 rubric（总体标签 + 四维特征描述），旧记录无 rubric 字段时降级不显示。
 */
export function AssessmentsPage() {
  const [list, setList] = useState<AssessmentListItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  // T-069：?detail=<id> 深链——「我的」页最近测评卡的「查看测评结果」直达最新测评详情
  const detailParam = searchParams.get('detail')

  useEffect(() => {
    if (detailParam) {
      setSelectedId(detailParam)
    }
  }, [detailParam])

  useEffect(() => {
    async function load() {
      try {
        const response = await api.get<AssessmentListItem[]>(endpoints.assessments)
        setList(response.data)
      } catch {
        setError('测评记录加载失败。')
      }
    }

    void load()
  }, [])

  function closeDetail() {
    setSelectedId(null)
    if (detailParam) {
      setSearchParams({}, { replace: true })
    }
  }

  if (error) {
    return <div className="alert alert-error">{error}</div>
  }

  if (list === null) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载测评记录...</p>
  }

  if (selectedId) {
    return <AssessmentDetailView assessmentId={selectedId} onBack={closeDetail} />
  }

  return (
    <div className="stack stack-md">
      <div className="section-header">
        <h2>测评记录</h2>
        <p>历次水平测评的结果与逐题详情。</p>
      </div>

      {list.length === 0 ? (
        <div className="card stack stack-sm">
          <p style={{ fontSize: 'var(--text-sm)' }}>还没有测评记录。</p>
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            完成一次水平测评后，这里会记录每次的定级与逐题表现；定期重测可以追踪你的表达进步。
          </p>
          <Link to="/assessment" className="btn btn-primary" style={{ alignSelf: 'flex-start' }}>
            去测评
          </Link>
        </div>
      ) : (
        <>
          <div className="stack stack-sm">
            {list.map((item) => (
              <button
                key={item.id}
                type="button"
                className="manage-card"
                onClick={() => setSelectedId(item.id)}
              >
                <div className="manage-card-info">
                  <h3>
                    {formatDateTime(item.startAt)}
                    {item.status === 'InProgress' && (
                      <>
                        {' '}
                        <Badge variant="info">进行中</Badge>
                      </>
                    )}
                  </h3>
                  <p>
                    {item.finalLevel ? `定级 ${item.finalLevel}` : '未定级'}
                    {item.expressionScore != null && ` · 表达综合分 ${item.expressionScore}/100`}
                    {item.guardAdjusted && ' · 经识别矫正'}
                  </p>
                </div>
                {item.finalLevel && (
                  <span className="badge-level" style={{ marginInline: 'var(--space-3)' }}>{item.finalLevel}</span>
                )}
                <span className="manage-card-chevron">
                  <ChevronRight size={18} aria-hidden="true" />
                </span>
              </button>
            ))}
          </div>
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
            定期<Link to="/assessment">重新测评</Link>可以追踪你的表达进步轨迹。
          </p>
        </>
      )}
    </div>
  )
}

function AssessmentDetailView({ assessmentId, onBack }: { assessmentId: string; onBack: () => void }) {
  const [detail, setDetail] = useState<AssessmentDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function load() {
      try {
        const response = await api.get<AssessmentDetail>(endpoints.assessmentDetail(assessmentId))
        setDetail(response.data)
      } catch {
        setError('测评详情加载失败。')
      }
    }

    void load()
  }, [assessmentId])

  if (error) {
    return (
      <div className="stack stack-md">
        <BackButton onBack={onBack} />
        <div className="alert alert-error">{error}</div>
      </div>
    )
  }

  if (!detail) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载测评详情...</p>
  }

  const finalRecord = detail.records.find((record) => record.step === 'FinalLevel')
  const final = finalRecord ? parseJson<AssessmentFinalResult>(finalRecord.scoresJson) : null
  const blocks = detail.records
    .filter((record) => record.step === 'AdaptiveBlock' && record.scoresJson !== '{}')
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp))
  const hasLegacySteps = detail.records.some(
    (record) => record.step !== 'AdaptiveBlock' && record.step !== 'FinalLevel',
  )

  return (
    <div className="stack stack-md">
      <BackButton onBack={onBack} />

      <div className="section-header">
        <h2>测评详情</h2>
        <p>{formatDateTime(detail.startAt)}</p>
      </div>

      {final && (
        <section className="card stack stack-sm">
          <div className="row-between">
            <h3 style={{ fontWeight: 540 }}>定级结果</h3>
            <span className="badge-level">{final.overallLevel}</span>
          </div>
          {/* T-055：总体人话标签 + 四维特征描述（旧记录无 rubric 字段时降级不显示） */}
          {final.rubric && <ProficiencyRubric rubric={final.rubric} />}
          <p style={{ fontSize: 'var(--text-sm)' }}>表达力综合分：{final.expressionScore}/100</p>
          {final.originalLevelBeforeGuard && (
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
              表达表现 {final.originalLevelBeforeGuard}，综合词汇掌握情况矫正为 {final.overallLevel}。
            </p>
          )}
          <ul className="stack stack-sm" style={{ fontSize: 'var(--text-sm)', paddingLeft: '1.1em' }}>
            {final.dimensions.comments.map((comment, index) => (
              <li key={index}>{comment}</li>
            ))}
          </ul>
        </section>
      )}

      {detail.status === 'InProgress' && (
        <p className="alert alert-info">本次测评尚未完成，仅展示已提交的块。</p>
      )}
      {hasLegacySteps && (
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          本次测评为旧版流程，仅保留定级结果，无逐题详情。
        </p>
      )}

      {blocks.map((record, index) => (
        <AssessmentBlockView key={record.id} index={index + 1} record={record} />
      ))}
    </div>
  )
}

function AssessmentBlockView({ index, record }: { index: number; record: AssessmentDetail['records'][number] }) {
  const payload = parseJson<AssessmentBlockPayload>(record.questionsJson)
  const scores = parseJson<AssessmentBlockScores>(record.scoresJson)
  const answers = parseJson<AssessmentAnswerItem[]>(record.answersJson) ?? []

  if (!payload || !scores) {
    return null
  }

  return (
    <section className="stack stack-md">
      <div className="section-header">
        <h3>第 {index} 块（难度带 {payload.band}）</h3>
        <p>块表达分 {scores.blockExpressionScore}/100</p>
      </div>

      {scores.production.map((productionScore) => {
        const prompt = payload.production.find((item) => item.id === productionScore.id)
        const answer = answers.find((item) => item.id === productionScore.id)?.text?.trim()
        return (
          <div key={productionScore.id} className="card stack stack-sm">
            <p className="mono-label">{prompt?.kind === 'scenario' ? '情境表达' : '提示造句'}</p>
            <p style={{ fontSize: 'var(--text-sm)', fontWeight: 540 }}>{prompt?.prompt ?? '（题目已不可考）'}</p>
            <p style={{ fontSize: 'var(--text-sm)', lineHeight: 1.6 }}>
              我的作答：{answer || '（未作答）'}
            </p>
            <ScoreCard rating={toSentenceRating(productionScore, prompt?.targetWord ?? '', answer ?? '')} />
            <AiRevision value={productionScore.aiRevision ?? undefined} />
            <ErrorAnalysis items={productionScore.errorTags} suggestion={productionScore.suggestion ?? undefined} />
          </div>
        )
      })}

      {payload.vocabulary.map((item) => {
        const selected = answers.find((answer) => answer.id === item.id)?.selectedIndex
        const correct = scores.vocabulary.find((score) => score.id === item.id)?.correct
        return (
          <div key={item.id} className="card stack stack-sm">
            <p className="mono-label">词汇识别（参考）</p>
            <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 700 }}>{item.word}</p>
            <ChoiceResult options={item.options} correctIndex={item.correctIndex} selectedIndex={selected ?? null} />
            <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
              {selected == null ? '未作答' : correct ? '答对了' : '答错了'}
            </p>
          </div>
        )
      })}

      {payload.reading && (
        <div className="card stack stack-sm">
          <p className="mono-label">阅读理解（参考）</p>
          <p style={{ fontSize: 'var(--text-sm)', fontWeight: 540 }}>{payload.reading.title}</p>
          <p style={{ fontSize: 'var(--text-sm)' }}>{payload.reading.question}</p>
          <ChoiceResult
            options={payload.reading.options}
            correctIndex={payload.reading.correctIndex}
            selectedIndex={answers.find((answer) => answer.id === payload.reading!.id)?.selectedIndex ?? null}
          />
          <p style={{ fontSize: 'var(--text-xs)', color: 'var(--muted)' }}>
            {scores.reading == null ? '未作答' : scores.reading.correct ? '答对了' : '答错了'}
          </p>
        </div>
      )}
    </section>
  )
}

function ChoiceResult({ options, correctIndex, selectedIndex }: {
  options: string[]
  correctIndex: number
  selectedIndex: number | null
}) {
  return (
    <ul className="stack stack-sm" style={{ listStyle: 'none', padding: 0, fontSize: 'var(--text-sm)' }}>
      {options.map((option, optionIndex) => {
        const isCorrect = optionIndex === correctIndex
        const isSelected = optionIndex === selectedIndex
        return (
          <li key={optionIndex} style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
            <span style={{ flex: 1 }}>{option}</span>
            {isCorrect && <Badge variant="success">正确答案</Badge>}
            {isSelected && <Badge variant={isCorrect ? 'info' : 'warn'}>我的选择</Badge>}
          </li>
        )
      })}
    </ul>
  )
}

function BackButton({ onBack }: { onBack: () => void }) {
  return (
    <button type="button" className="btn btn-secondary btn-sm" onClick={onBack} style={{ alignSelf: 'flex-start' }}>
      <ChevronLeft size={16} aria-hidden="true" />
      返回列表
    </button>
  )
}

/** 测评块评分 → 练习流 ScoreCard 复用所需的 SentenceRating 形状 */
function toSentenceRating(
  score: AssessmentBlockScores['production'][number],
  targetWord: string,
  answer: string,
): SentenceRating {
  return {
    id: score.id,
    wordId: null,
    targetWord,
    scene: 'assessment',
    userSentence: answer,
    aiRevision: score.aiRevision ?? '',
    grammarScore: score.grammar,
    naturalScore: score.natural,
    vocabularyScore: score.vocabulary,
    relevanceScore: score.relevance,
    // 总评档：块综合分 0–100 本地映射（A 优秀 / B 良好 / C 及格 / D 需重写，与复习页图例一致）
    overallGrade: score.score >= 80 ? 'A' : score.score >= 60 ? 'B' : score.score >= 40 ? 'C' : 'D',
    errorTags: score.errorTags,
    difficultyLevel: 'Basic',
    suggestion: score.suggestion ?? '',
    timestamp: '',
  }
}

function parseJson<T>(json: string): T | null {
  try {
    return JSON.parse(json) as T
  } catch {
    return null
  }
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}
