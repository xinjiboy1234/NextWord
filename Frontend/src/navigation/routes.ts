import type { AppView } from './views'

export const ROUTE_PATHS: Record<AppView, string> = {
  dashboard: '/dashboard',
  learn: '/learn',
  spelling: '/spelling',
  sentence: '/sentence',
  reading: '/reading',
  assessment: '/assessment',
  challenge: '/challenge',
  level: '/level',
  review: '/review',
  home: '/word-bank',
  progress: '/progress',
  profile: '/profile',
  manage: '/manage',
  assessments: '/assessments',
}

const PATH_TO_VIEW: Record<string, AppView> = Object.fromEntries(
  Object.entries(ROUTE_PATHS).map(([view, path]) => [path, view as AppView]),
)

export function pathForView(view: AppView, articleId?: string): string {
  if (view === 'reading' && articleId) {
    return `/reading/${articleId}`
  }
  return ROUTE_PATHS[view]
}

export function viewFromPathname(pathname: string): AppView {
  if (pathname === '/' || pathname === '') return 'dashboard'
  if (pathname.startsWith('/reading')) return 'reading'
  const base = pathname.split('?')[0]
  return PATH_TO_VIEW[base] ?? 'dashboard'
}

export function isReadingArticlePath(pathname: string): boolean {
  return /^\/reading\/[^/]+$/.test(pathname.split('?')[0])
}
