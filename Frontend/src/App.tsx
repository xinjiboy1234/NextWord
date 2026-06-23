import { ArrowLeft, Sparkles } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { api } from './api/client'
import { endpoints } from './api/endpoints'
import { UpgradeCandidateBanner } from './components/UpgradeCandidateBanner'
import { UserAvatar } from './components/UserAvatar'
import { useAuth } from './contexts/AuthContext'
import { LoginPage } from './pages/LoginPage'

import type { ProgressSummary } from './types/models'
import { ArticleLibrary } from './pages/ArticleLibrary'
import { ArticleReader } from './pages/ArticleReader'
import { ChallengeMode } from './pages/ChallengeMode'
import { Dashboard } from './pages/Dashboard'
import { Home } from './pages/Home'
import { InitialAssessment } from './pages/InitialAssessment'
import { LevelDashboardPage } from './pages/LevelDashboard'
import { ProfilePage } from './pages/ProfilePage'
import { Progress } from './pages/Progress'
import { ReviewQueue } from './pages/ReviewQueue'
import { SentenceStudio } from './pages/SentenceStudio'
import { SpellingMode } from './pages/SpellingMode'
import { WordCard } from './pages/WordCard'

type View =
  | 'dashboard'
  | 'learn'
  | 'spelling'
  | 'sentence'
  | 'reading'
  | 'assessment'
  | 'challenge'
  | 'level'
  | 'review'
  | 'home'
  | 'progress'
  | 'profile'

const UPGRADE_DISMISS_KEY = 'nextword.upgrade.dismissed'

function App() {
  const { isAuthenticated, user, loading } = useAuth()
  const [view, setView] = useState<View>('dashboard')
  const [readingArticleId, setReadingArticleId] = useState<string | null>(null)
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [upgradeDismissed, setUpgradeDismissed] = useState(
    () => localStorage.getItem(UPGRADE_DISMISS_KEY) === '1',
  )

  const loadProgress = useCallback(async () => {
    try {
      const response = await api.get<ProgressSummary>(endpoints.progress)
      setProgress(response.data)
      return response.data
    } catch {
      setProgress(null)
      return null
    }
  }, [])

  useEffect(() => {
    if (!isAuthenticated) {
      setProgress(null)
      return
    }

    void loadProgress()
  }, [isAuthenticated, loadProgress])

  // 未完成首次测评时自动进入测评流程
  useEffect(() => {
    if (progress && !progress.hasCompletedInitialAssessment) {
      setView('assessment')
    }
  }, [progress])

  const handleAssessmentComplete = useCallback(() => {
    void loadProgress().then((data) => {
      if (data?.hasCompletedInitialAssessment) {
        setView('dashboard')
      }
    })
  }, [loadProgress])

  const goHome = useCallback(() => {
    setReadingArticleId(null)
    if (progress && !progress.hasCompletedInitialAssessment) {
      setView('assessment')
      return
    }
    setView('dashboard')
  }, [progress])

  const showUpgradeCandidate = progress !== null
    && progress.hasCompletedInitialAssessment
    && progress.isUpgradeCandidate
    && !upgradeDismissed
    && view === 'dashboard'

  const needsAssessment = progress !== null && !progress.hasCompletedInitialAssessment
  const showBackButton = view !== 'dashboard' && !(needsAssessment && view === 'assessment')
  const awaitingProgress = isAuthenticated && progress === null

  if (loading) {
    return (
      <div className="grid min-h-dvh place-items-center bg-stone-50 text-neutral-600">
        加载中...
      </div>
    )
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-dvh bg-stone-50 text-neutral-950">
        <header className="border-b border-neutral-200 bg-white">
          <div className="mx-auto flex max-w-6xl items-center gap-3 px-4 py-4">
            <div className="grid h-11 w-11 place-items-center rounded-md bg-emerald-700 text-white">
              <Sparkles size={22} aria-hidden="true" />
            </div>
            <div>
              <h1 className="text-xl font-semibold leading-tight">NextWord</h1>
              <p className="text-sm text-neutral-600">请登录后使用学习功能</p>
            </div>
          </div>
        </header>
        <main className="mx-auto max-w-6xl px-4 py-8">
          <LoginPage />
        </main>
      </div>
    )
  }

  return (
    <div className="min-h-dvh bg-stone-50 text-neutral-950">
      <header className="border-b border-neutral-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-4">
          <div className="flex items-center gap-3">
            <div className="grid h-11 w-11 place-items-center rounded-md bg-emerald-700 text-white">
              <Sparkles size={22} aria-hidden="true" />
            </div>
            <div>
              <h1 className="text-xl font-semibold leading-tight">NextWord</h1>
              <p className="text-sm text-neutral-600">你好，{user?.displayName}</p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {showBackButton && (
              <button
                type="button"
                onClick={goHome}
                className="inline-flex h-11 items-center gap-2 rounded-md border border-neutral-200 bg-white px-3 text-sm font-medium text-neutral-700 hover:bg-neutral-100"
              >
                <ArrowLeft size={18} aria-hidden="true" />
                返回首页
              </button>
            )}
            <UserAvatar
              displayName={user?.displayName ?? '用户'}
              active={view === 'profile'}
              onClick={() => setView('profile')}
            />
          </div>
        </div>
      </header>

      <main className="mx-auto grid max-w-6xl gap-5 px-4 py-6">
        {awaitingProgress ? (
          <p className="text-sm text-neutral-600">加载中...</p>
        ) : (
          <>
        {showUpgradeCandidate && (
          <UpgradeCandidateBanner
            onOpenLevel={() => setView('level')}
            onDismiss={() => {
              localStorage.setItem(UPGRADE_DISMISS_KEY, '1')
              setUpgradeDismissed(true)
            }}
          />
        )}
        {view === 'dashboard' && <Dashboard onNavigate={setView} />}
        {view === 'learn' && <WordCard />}
        {view === 'spelling' && <SpellingMode />}
        {view === 'sentence' && <SentenceStudio />}
        {view === 'reading' && (
          readingArticleId ? (
            <ArticleReader
              articleId={readingArticleId}
              onBack={() => {
                setReadingArticleId(null)
              }}
            />
          ) : (
            <ArticleLibrary
              onOpen={(articleId) => {
                setReadingArticleId(articleId)
              }}
            />
          )
        )}
        {view === 'assessment' && (
          <InitialAssessment
            autoStart={needsAssessment}
            onComplete={handleAssessmentComplete}
          />
        )}
        {view === 'challenge' && <ChallengeMode />}
        {view === 'level' && <LevelDashboardPage />}
        {view === 'review' && <ReviewQueue />}
        {view === 'home' && <Home onStart={() => setView('learn')} />}
        {view === 'progress' && <Progress />}
        {view === 'profile' && (
          <ProfilePage
            onNavigate={(target) => setView(target)}
          />
        )}
          </>
        )}
      </main>
    </div>
  )
}

export default App
