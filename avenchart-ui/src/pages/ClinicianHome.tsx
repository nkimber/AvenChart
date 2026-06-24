import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Clock3, ShieldCheck, UserCircle2 } from 'lucide-react'
import { getCurrentSession, type AuthSessionResponse } from '../api.ts'
import { clearClinicianSession, loadClinicianSession } from '../auth/session.ts'
import { PulseBadgeIllustration } from '../illustrations.tsx'

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

export default function ClinicianHome() {
  const navigate = useNavigate()
  const [session] = useState(() => loadClinicianSession())
  const [live, setLive] = useState<AuthSessionResponse | null>(null)
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!session) {
      navigate('/login')
      return
    }

    const controller = new AbortController()
    getCurrentSession(session.sessionId, controller.signal)
      .then((result) => {
        if (!result.authenticated) {
          clearClinicianSession()
          navigate('/login')
          return
        }
        setLive(result)
        setStatus('ready')
      })
      .catch((err) => {
        setStatus('error')
        setError(err instanceof Error ? err.message : 'Could not verify session.')
      })

    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!session) return null

  function handleSignOut() {
    clearClinicianSession()
    navigate('/login')
  }

  return (
    <div className="shell-top">
      <div className="dashboard">
        <div className="dashboard-hero">
          <div className="dashboard-hero-illustration">
            <PulseBadgeIllustration />
          </div>
          <div className="dashboard-hero-row">
            <div className="row">
              <div className="avatar avatar-on-dark">{initials(session.displayName)}</div>
              <div>
                <p className="dashboard-hero-greeting">Welcome, {session.displayName.split(' ')[0]}</p>
                <p className="dashboard-hero-sub">{session.role}</p>
              </div>
            </div>
            <button className="link-button-on-dark" onClick={handleSignOut}>Sign out</button>
          </div>

          {status === 'ready' && live && (
            <div className="hero-stat-row">
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><UserCircle2 size={16} /></span>
                <div>
                  <p className="hero-stat-value">{live.role}</p>
                  <p className="hero-stat-label">Role</p>
                </div>
              </div>
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><ShieldCheck size={16} /></span>
                <div>
                  <p className="hero-stat-value">{live.sessionSource}</p>
                  <p className="hero-stat-label">Session source</p>
                </div>
              </div>
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><Clock3 size={16} /></span>
                <div>
                  <p className="hero-stat-value">{live.lastSeenAt ?? 'Just now'}</p>
                  <p className="hero-stat-label">Last seen</p>
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="panel">
          <p className="subtitle">You're signed in to the modernized Legacy EHR backend. This is a minimal landing screen — more of the application will live here later.</p>

          {status === 'loading' && <p className="muted">Confirming your session…</p>}
          {status === 'error' && <div className="error-banner">{error}</div>}

          {status === 'ready' && live && (
            <ul className="fact-list">
              <li className="fact-row"><span>Username</span><span>{live.username}</span></li>
              <li className="fact-row"><span>Role</span><span>{live.role}</span></li>
              <li className="fact-row"><span>Session source</span><span>{live.sessionSource}</span></li>
              <li className="fact-row"><span>Last seen</span><span>{live.lastSeenAt ?? 'just now'}</span></li>
            </ul>
          )}
        </div>
      </div>
    </div>
  )
}
