import { useCallback, useEffect, useState } from 'react'
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { api } from './api/client'
import { endpoints } from './api/endpoints'
import { AppShell } from './components/layout/AppShell'
import { OnboardingLayout } from './components/layout/OnboardingLayout'
import { AlertDialog } from './components/ui/Dialog'
import { UpgradeCandidateBanner } from './components/UpgradeCandidateBanner'
import { useAuth } from './contexts/AuthContext'
import { pathForView, viewFromPathname } from './navigation/routes'
import type { ManageView } from './navigation/views'
import { LoginPage } from './pages/LoginPage'
import { ManagePage } from './pages/ManagePage'
import type { ProgressSummary } from './types/models'
import { ArticleLibrary } from './pages/ArticleLibrary'
import { ArticleReaderRoute } from './pages/ArticleReaderRoute'
import { AssessmentsPage } from './pages/AssessmentsPage'
import { ChallengeMode } from './pages/ChallengeMode'
import { Dashboard } from './pages/Dashboard'
import { Home } from './pages/Home'
import { InitialAssessment } from './pages/InitialAssessment'
import { ProfilePage } from './pages/ProfilePage'
import { ReviewQueue } from './pages/ReviewQueue'
import { SentenceStudio } from './pages/SentenceStudio'
import { SpellingMode } from './pages/SpellingMode'
import { WordCard } from './pages/WordCard'

const UPGRADE_DISMISS_KEY = 'nextword.upgrade.dismissed'

function AuthenticatedApp() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user } = useAuth()
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [upgradeDismissed, setUpgradeDismissed] = useState(
    () => localStorage.getItem(UPGRADE_DISMISS_KEY) === '1',
  )
  const [skipDialogOpen, setSkipDialogOpen] = useState(false)
  const [skipping, setSkipping] = useState(false)
  const [assessmentStep, setAssessmentStep] = useState(1)

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

  const handleSkipAssessment = useCallback(async () => {
    setSkipping(true)
    try {
      await api.post(endpoints.assessmentSkip, {})
      await loadProgress()
      navigate('/dashboard', { replace: true })
    } finally {
      setSkipping(false)
      setSkipDialogOpen(false)
    }
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
    else if (target === 'assessments') navigate('/assessments')
    else if (target === 'challenge') navigate('/challenge')
    else if (target === 'home') navigate('/word-bank')
    else navigate('/profile#profile-progress')
  }, [navigate])

  const showUpgradeCandidate = progress !== null
    && progress.hasCompletedInitialAssessment
    && progress.isUpgradeCandidate
    && !upgradeDismissed
    && view === 'dashboard'

  const needsAssessment = progress !== null && !progress.hasCompletedInitialAssessment
  const showBackButton = view !== 'dashboard' && view !== 'profile' && !needsAssessment
  const awaitingProgress = progress === null

  const appRoutes = (
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
            hasCompleted={progress?.hasCompletedInitialAssessment ?? false}
            onComplete={handleAssessmentComplete}
          />
        )}
      />
      <Route path="/challenge" element={<ChallengeMode />} />
      <Route path="/assessments" element={<AssessmentsPage />} />
      <Route path="/level" element={<Navigate to="/profile#profile-level" replace />} />
      <Route path="/review" element={<ReviewQueue />} />
      <Route path="/word-bank" element={<Home onStart={() => navigate('/learn')} />} />
      <Route path="/progress" element={<Navigate to="/profile#profile-progress" replace />} />
      <Route path="/profile" element={<ProfilePage />} />
      <Route path="/manage" element={<ManagePage onNavigate={manageNavigate} />} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )

  if (awaitingProgress) {
    return (
      <div className="auth-page">
        <p style={{ color: 'var(--muted)' }}>加载中...</p>
      </div>
    )
  }

  if (needsAssessment) {
    return (
      <>
        <OnboardingLayout
          step={assessmentStep}
          onSkip={() => setSkipDialogOpen(true)}
          skipDisabled={skipping}
        >
          <Routes>
            <Route
              path="/assessment"
              element={(
                <InitialAssessment
                  autoStart
                  immersive
                  onComplete={handleAssessmentComplete}
                  onStepChange={setAssessmentStep}
                />
              )}
            />
            <Route path="*" element={<Navigate to="/assessment" replace />} />
          </Routes>
        </OnboardingLayout>
        <AlertDialog
          open={skipDialogOpen}
          onOpenChange={setSkipDialogOpen}
          title="跳过水平测评？"
          description="跳过后将使用默认等级 A2 开始学习。你之后可以在「我的」页面重新进行测评。"
          confirmLabel="确认跳过"
          onConfirm={() => { void handleSkipAssessment() }}
          loading={skipping}
        />
      </>
    )
  }

  return (
    <AppShell
      displayName={user?.displayName ?? '用户'}
      showBackButton={showBackButton}
      onBack={goHome}
    >
      {showUpgradeCandidate && (
        <UpgradeCandidateBanner
          currentLevel={progress?.overallLevel}
          onStartChallenge={() => navigate('/challenge', { state: { confirmation: true } })}
          onDismiss={() => {
            localStorage.setItem(UPGRADE_DISMISS_KEY, '1')
            setUpgradeDismissed(true)
          }}
        />
      )}
      {appRoutes}
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
