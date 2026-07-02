import { BriefcaseBusiness, GraduationCap, Home } from 'lucide-react'

interface SceneSelectorProps {
  value: string
  onChange: (value: string) => void
}

const scenes = [
  { id: 'life', label: '生活', icon: Home },
  { id: 'work', label: '职场', icon: BriefcaseBusiness },
  { id: 'academic', label: '学术', icon: GraduationCap },
] as const

export function SceneSelector({ value, onChange }: SceneSelectorProps) {
  return (
    <div className="scene-group">
      {scenes.map((scene) => {
        const Icon = scene.icon
        const active = value === scene.id
        return (
          <button
            key={scene.id}
            type="button"
            onClick={() => onChange(scene.id)}
            className={`scene-btn${active ? ' active' : ''}`}
          >
            <Icon size={16} aria-hidden="true" style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} />
            {scene.label}
          </button>
        )
      })}
    </div>
  )
}
