import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { HeartPulse, ShieldCheck } from 'lucide-react'
import { loginPatientPortal } from '../api.ts'
import { savePortalSession } from '../auth/session.ts'
import { PatientIllustration } from '../illustrations.tsx'

export default function PortalLogin() {
  const navigate = useNavigate()
  const [username, setUsername] = useState('mod-pat-0004@example.test')
  const [password, setPassword] = useState('PortalPass207!')
  const [status, setStatus] = useState<'idle' | 'checking' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setStatus('checking')
    setError(null)
    try {
      const result = await loginPatientPortal({ username, password })
      if (!result.authenticated || !result.sessionId) {
        setStatus('error')
        setError(result.failureReason ?? 'Those credentials were not recognized.')
        return
      }
      savePortalSession({
        sessionId: result.sessionId,
        username: result.username,
        portalUsername: result.portalUsername,
        displayName: result.displayName,
      })
      navigate('/portal/home')
    } catch (err) {
      setStatus('error')
      setError(err instanceof Error ? err.message : 'Sign-in failed.')
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-hero">
        <div className="auth-hero-brand">
          <span className="auth-hero-brand-mark" aria-hidden="true">
            <HeartPulse size={16} />
          </span>
          Patient portal
        </div>
        <h1 className="auth-hero-title">Your records, your messages, your care — always within reach.</h1>
        <p className="auth-hero-text">
          Check appointments, message your care team, and review results, all in one secure place.
        </p>
        <div className="auth-hero-illustration">
          <PatientIllustration />
        </div>
        <div className="auth-hero-badges">
          <span className="auth-hero-badge">
            <span className="auth-hero-badge-icon" aria-hidden="true">
              <ShieldCheck size={12} />
            </span>
            Private &amp; secure
          </span>
        </div>
      </div>
      <div className="auth-panel">
        <div className="auth-card">
          <p className="eyebrow">Patient portal</p>
          <h2 className="title">Hello, welcome back</h2>
          <p className="subtitle">Sign in to view your messages and appointments.</p>

          <div className="hint-banner">Demo credentials are pre-filled: mod-pat-0004@example.test / PortalPass207!</div>

          {error && <div className="error-banner">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="field">
              <label className="label" htmlFor="portal-username">Email or username</label>
              <input
                id="portal-username"
                className="input"
                type="text"
                name="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                spellCheck={false}
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="portal-password">Password</label>
              <input
                id="portal-password"
                className="input"
                type="password"
                name="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
              />
            </div>
            <button className="button-primary" type="submit" disabled={status === 'checking'}>
              {status === 'checking' ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}
