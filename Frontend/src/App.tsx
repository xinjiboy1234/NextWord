import { BookOpen, Layers, LineChart, Sparkles } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Home } from './pages/Home'
import { Progress } from './pages/Progress'
import { WordCard } from './pages/WordCard'

type View = 'learn' | 'home' | 'progress'

function App() {
  const [view, setView] = useState<View>('learn')

  const navItems = useMemo(
    () => [
      { id: 'learn' as const, label: '学习', icon: BookOpen },
      { id: 'home' as const, label: '词库', icon: Layers },
      { id: 'progress' as const, label: '进度', icon: LineChart },
    ],
    [],
  )

  return (
    <div className="min-h-dvh bg-stone-50 text-neutral-950">
      <header className="border-b border-neutral-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="grid h-11 w-11 place-items-center rounded-md bg-emerald-700 text-white">
              <Sparkles size={22} aria-hidden="true" />
            </div>
            <div>
              <h1 className="text-xl font-semibold leading-tight">NextWord</h1>
              <p className="text-sm text-neutral-600">翻译识别 MVP</p>
            </div>
          </div>

          <nav aria-label="Primary navigation" className="flex gap-2">
            {navItems.map((item) => {
              const Icon = item.icon
              const active = view === item.id
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setView(item.id)}
                  className={`inline-flex h-11 items-center gap-2 rounded-md border px-3 text-sm font-medium transition ${
                    active
                      ? 'border-emerald-700 bg-emerald-700 text-white'
                      : 'border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100'
                  }`}
                >
                  <Icon size={18} aria-hidden="true" />
                  {item.label}
                </button>
              )
            })}
          </nav>
        </div>
      </header>

      <main className="mx-auto grid max-w-6xl gap-5 px-4 py-6">
        {view === 'learn' && <WordCard />}
        {view === 'home' && <Home onStart={() => setView('learn')} />}
        {view === 'progress' && <Progress />}
      </main>
    </div>
  )
}

export default App
