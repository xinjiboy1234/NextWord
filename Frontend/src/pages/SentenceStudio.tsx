import { PenLine, TextCursorInput } from 'lucide-react'
import { useState } from 'react'
import { FreeExpression } from './FreeExpression'
import { SentenceCard } from './SentenceCard'

type SentenceMode = 'targeted' | 'free'

interface SentenceStudioProps {
  userLevel?: string
}

export function SentenceStudio({ userLevel = 'A2' }: SentenceStudioProps) {
  const [mode, setMode] = useState<SentenceMode>('targeted')

  return (
    <div className="stack stack-md">
      <div className="section-header row-between" style={{ flexWrap: 'wrap' }}>
        <div>
          <h2>造句训练</h2>
          <p>指定词造句与自由表达，AI 多维度评分。</p>
        </div>
        <div className="tabs">
          <button
            type="button"
            className={`tab${mode === 'targeted' ? ' active' : ''}`}
            onClick={() => setMode('targeted')}
          >
            <PenLine size={16} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} aria-hidden="true" />
            指定词
          </button>
          <button
            type="button"
            className={`tab${mode === 'free' ? ' active' : ''}`}
            onClick={() => setMode('free')}
          >
            <TextCursorInput size={16} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} aria-hidden="true" />
            自由表达
          </button>
        </div>
      </div>
      {mode === 'targeted' ? <SentenceCard userLevel={userLevel} /> : <FreeExpression userLevel={userLevel} />}
    </div>
  )
}
