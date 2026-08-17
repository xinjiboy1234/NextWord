import { useEffect, useRef, useState } from 'react'
import { Tabs } from '../components/ui/Tabs'
import { useExplorationWeek } from '../hooks/useExplorationWeek'
import { FreeExpression } from './FreeExpression'
import { SentenceCard } from './SentenceCard'

type SentenceMode = 'targeted' | 'free'

interface SentenceStudioProps {
  userLevel?: string
}

export function SentenceStudio({ userLevel = 'A2' }: SentenceStudioProps) {
  const [mode, setMode] = useState<SentenceMode>('targeted')
  // T-032：探索周内默认落到「自由表达」Tab（今日探索任务在此完成）；用户手动切 Tab 后不再覆盖
  const exploration = useExplorationWeek()
  const modeTouched = useRef(false)

  useEffect(() => {
    if (exploration?.active && !modeTouched.current) {
      setMode('free')
    }
  }, [exploration])

  return (
    <div className="stack stack-md">
      <div className="section-header">
        <h2>造句训练</h2>
        <p>指定词造句与自由表达，多维度反馈。</p>
      </div>
      <Tabs
        value={mode}
        onValueChange={(value) => {
          modeTouched.current = true
          setMode(value as SentenceMode)
        }}
        items={[
          { value: 'targeted', label: '指定词', panel: <SentenceCard userLevel={userLevel} /> },
          { value: 'free', label: '自由表达', panel: <FreeExpression userLevel={userLevel} /> },
        ]}
      />
    </div>
  )
}
