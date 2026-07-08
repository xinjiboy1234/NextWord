import { useState } from 'react'
import { Tabs } from '../components/ui/Tabs'
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
      <div className="section-header">
        <h2>造句训练</h2>
        <p>指定词造句与自由表达，AI 多维度评分。</p>
      </div>
      <Tabs
        value={mode}
        onValueChange={(value) => setMode(value as SentenceMode)}
        items={[
          { value: 'targeted', label: '指定词', panel: <SentenceCard userLevel={userLevel} /> },
          { value: 'free', label: '自由表达', panel: <FreeExpression userLevel={userLevel} /> },
        ]}
      />
    </div>
  )
}
