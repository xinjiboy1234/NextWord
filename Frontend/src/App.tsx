import { useCallback, useEffect, useState } from 'react'
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { api } from './api/client'
import { endpoints } from './api/endpoints'
import { AppShell } from './components/layout/AppShell'
import { UpgradeCandidateBanner } from './components/UpgradeCandidateBanner'
import { useAuth } from './contexts/AuthContext'
import { useProfileScores } from './hooks/useProfileScores'
import { pathForView, viewFromPathname } from './navigation/routes'
import type { ManageView } from './navigation/views'
import { LoginPage } from './pages/LoginPage'
import { ManagePage } from './pages/ManagePage'
import type { ProgressSummary } from './types/models'
import { ArticleLibrary } from './pages/ArticleLibrary'
import { ArticleReaderRoute } from './pages/ArticleReaderRoute'
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

const UPGRADE_DISMISS_KEY = 'nextword.upgrade.dismissed'

function AuthenticatedApp() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user } = useAuth()
  const { scores: profileScores } = useProfileScores()
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [upgradeDismissed, setUpgradeDismissed] = useState(
    () => localStorage.getItem(UPGRADE_DISMISS_KEY) === '1',
  )

  const view = viewFromPathname(location.pathname)

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
    void loadProgress()
  }, [loadProgress])

  useEffect(() => {
    if (progress && !progress.hasCompletedInitialAssessment && location.pathname !== '/assessment') {
      navigate('/assessment', { replace: true })
    }
  }, [progress, location.pathname, navigate])

  const handleAssessmentComplete = useCallback(() => {
    void loadProgress().then((data) => {
      if (data?.hasCompletedInitialAssessment) {
        navigate('/dashboard', { replace: true })
      }
    })
  }, [loadProgress, navigate])

  const goHome = useCallback(() => {
    if (progress && !progress.hasCompletedInitialAssessment) {
      navigate('/assessment', { replace: true })
      return
    }
    navigate('/dashboard')
  }, [progress, navigate])

  const manageNavigate = useCallback((target: ManageView) => {
    if (target === 'assessment') navigate('/assessment')
    else if (target === 'challenge') navigate('/challenge')
    else if (target === 'home') navigate('/word-bank')
    else navigate('/progress')
  }, [navigate])

  const showUpgradeCandidate = progress !== null
    && progress.hasCompletedInitialAssessment
    && progress.isUpgradeCandidate
    && !upgradeDismissed
    && view === 'dashboard'

  const needsAssessment = progress !== null && !progress.hasCompletedInitialAssessment
  const showBackButton = view !== 'dashboard' && !(needsAssessment && view === 'assessment')
  const awaitingProgress = progress === null

  return (
    <AppShell
      displayName={user?.displayName ?? '用户'}
      overallLevel={profileScores?.cefrDisplay ?? progress?.overallLevel}
      overallScore={profileScores?.overall}
      showBackButton={showBackButton}
      onBack={goHome}
    >
      {awaitingProgress ? (
        <p className="text-sm" style={{ color: 'var(--muted)' }}>加载中...</p>
      ) : (
        <>
          {showUpgradeCandidate && (
            <UpgradeCandidateBanner
              currentLevel={progress?.overallLevel}
              onOpenLevel={() => navigate('/level')}
              onDismiss={() => {
                localStorage.setItem(UPGRADE_DISMISS_KEY, '1')
                setUpgradeDismissed(true)
              }}
            />
          )}
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route
              path="/dashboard"
              element={<Dashboard progress={progress} onNavigate={(v) => navigate(pathForView(v))} />}
            />
            <Route path="/learn" element={<WordCard />} />
            <Route path="/spelling" element={<SpellingMode />} />
            <Route path="/sentence" element={<SentenceStudio userLevel={progress?.overallLevel} />} />
            <Route
              path="/reading"
              element={<ArticleLibrary onOpen={(id) => navigate(pathForView('reading', id))} />}
            />
            <Route path="/reading/:articleId" element={<ArticleReaderRoute />} />
            <Route
              path="/assessment"
              element={(
                <InitialAssessment
                  autoStart={needsAssessment}
                  onComplete={handleAssessmentComplete}
                />
              )}
            />
            <Route path="/challenge" element={<ChallengeMode />} />
            <Route path="/level" element={<LevelDashboardPage />} />
            <Route path="/review" element={<ReviewQueue />} />
            <Route path="/word-bank" element={<Home onStart={() => navigate('/learn')} />} />
            <Route path="/progress" element={<Progress />} />
            <Route path="/profile" element={<ProfilePage />} />
            <Route path="/manage" element={<ManagePage onNavigate={manageNavigate} />} />
            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </Routes>
        </>
      )}
    </AppShell>
  )
}

function App() {
  const { isAuthenticated, loading } = useAuth()

  if (loading) {
    return (
      <div className="auth-page">
        <p style={{ color: 'var(--muted)' }}>加载中...</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <LoginPage />
  }

  return <AuthenticatedApp />
}

export default App
