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
    <div className="flex flex-wrap gap-2">
      {scenes.map((scene) => {
        const Icon = scene.icon
        const active = value === scene.id
        return (
          <button
            key={scene.id}
            type="button"
            onClick={() => onChange(scene.id)}
            className={`inline-flex h-10 items-center gap-2 rounded-md border px-3 text-sm font-medium ${
              active ? 'border-emerald-700 bg-emerald-700 text-white' : 'border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100'
            }`}
          >
            <Icon size={16} aria-hidden="true" />
            {scene.label}
          </button>
        )
      })}
    </div>
  )
}
