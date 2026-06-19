import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('UI error boundary caught:', error, info)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="rounded-md border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
          页面出现问题，请刷新后重试。
        </div>
      )
    }

    return this.props.children
  }
}
