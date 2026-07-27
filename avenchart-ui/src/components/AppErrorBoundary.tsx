import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle, Home, RefreshCw } from 'lucide-react'

type Props = { children: ReactNode }
type State = { error: Error | null; reference: string | null }

function createErrorReference() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID()
  }
  return `ui-${Date.now().toString(36)}`
}

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null, reference: null }

  static getDerivedStateFromError(error: Error): State {
    return { error, reference: createErrorReference() }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Keep the diagnostic in the browser console for local support evidence.
    // The user-facing surface exposes only a correlation reference.
    console.error('Unexpected Modern UI render failure', {
      reference: this.state.reference,
      error,
      componentStack: info.componentStack,
    })
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <main className="unexpected-error-page" id="main-content">
        <AlertTriangle size={36} aria-hidden="true" />
        <h1>This page could not be displayed</h1>
        <p>
          Your session data has not been cleared. Retry the page, or return to the application entry screen.
        </p>
        {this.state.reference && (
          <p className="unexpected-error-reference">
            Error reference: <code>{this.state.reference}</code>
          </p>
        )}
        <div className="unexpected-error-actions">
          <button className="cl-btn-primary" type="button" onClick={() => window.location.reload()}>
            <RefreshCw size={16} aria-hidden="true" />
            Retry page
          </button>
          <button className="cl-btn-secondary" type="button" onClick={() => window.location.assign('/')}>
            <Home size={16} aria-hidden="true" />
            Application home
          </button>
        </div>
      </main>
    )
  }
}
