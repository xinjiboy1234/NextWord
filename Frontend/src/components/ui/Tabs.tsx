import { Tabs as BaseTabs } from '@base-ui/react/tabs'
import type { ReactNode } from 'react'

interface TabItem {
  value: string
  label: string
  panel: ReactNode
}

interface TabsProps {
  value: string
  onValueChange: (value: string) => void
  items: TabItem[]
  className?: string
  listClassName?: string
}

export function Tabs({
  value,
  onValueChange,
  items,
  className = 'tabs-root',
  listClassName = 'tabs',
}: TabsProps) {
  return (
    <BaseTabs.Root value={value} onValueChange={onValueChange} className={className}>
      <BaseTabs.List className={listClassName} aria-label="标签页">
        {items.map((item) => (
          <BaseTabs.Tab key={item.value} value={item.value} className="tab">
            {item.label}
          </BaseTabs.Tab>
        ))}
      </BaseTabs.List>
      {items.map((item) => (
        <BaseTabs.Panel key={item.value} value={item.value} className="tab-panel">
          {item.panel}
        </BaseTabs.Panel>
      ))}
    </BaseTabs.Root>
  )
}
