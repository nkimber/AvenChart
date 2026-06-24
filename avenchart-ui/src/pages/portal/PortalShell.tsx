import { useEffect, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { CalendarClock, FolderOpen, Home, Mail, UserCircle } from 'lucide-react'
import {
  getPatientPortalHome,
  getPatientPortalSession,
  type PatientPortalHomeSummaryResponse,
} from '../../api.ts'
import { clearPortalSession, loadPortalSession, type PortalSession } from '../../auth/session.ts'
import { PulseBadgeIllustration } from '../../illustrations.tsx'

export type PortalOutletContext = {
  session: PortalSession
  home: PatientPortalHomeSummaryResponse | null
  homeLoading: boolean
  /** Call to immediately decrement the unread badge (optimistic read) */
  markReadOptimistic: (id: string) => void
  refreshHome: () => void
  signOut: () => void
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase())
    .join('')
}

function formatNextAppt(home: PatientPortalHomeSummaryResponse | null): string {
  const next = home?.upcomingAppointments?.[0]
  if (!next) return 'None scheduled'
  const [y, m, d] = next.date.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

const TABS = [
  { path: '/portal/home', label: 'Home', icon: Home },
  { path: '/portal/messages', label: 'Messages', icon: Mail },
  { path: '/portal/appointments', label: 'Appointments', icon: CalendarClock },
  { path: '/portal/records', label: 'Records', icon: FolderOpen },
  { path: '/portal/account', label: 'Account', icon: UserCircle },
]

export default function PortalShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const [session] = useState(() => loadPortalSession())
  const [home, setHome] = useState<PatientPortalHomeSummaryResponse | null>(null)
  const [homeLoading, setHomeLoading] = useState(true)
  const [homeError, setHomeError] = useState<string | null>(null)
  // Optimistic unread IDs — message IDs the patient has opened this session
  const [optimisticReadIds, setOptimisticReadIds] = useState<Set<string>>(() => new Set())

  useEffect(() => {
    if (!session) {
      navigate('/portal/login', { replace: true })
      return
    }
    const controller = new AbortController()
    Promise.all([
      getPatientPortalSession(session.sessionId, controller.signal),
      getPatientPortalHome(session.sessionId, controller.signal),
    ])
      .then(([sessionResult, homeResult]) => {
        if (!sessionResult.authenticated || !homeResult.authenticated) {
          clearPortalSession()
          navigate('/portal/login', { replace: true })
          return
        }
        setHome(homeResult)
        setHomeLoading(false)
      })
      .catch((err) => {
        if ((err as Error)?.name === 'AbortError') return
        setHomeError(
          err instanceof Error
            ? err.message
            : 'Could not load your portal. Please try refreshing.',
        )
        setHomeLoading(false)
      })
    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!session) return null

  function signOut() {
    clearPortalSession()
    navigate('/portal/login')
  }

  function refreshHome() {
    if (!session) return
    getPatientPortalHome(session.sessionId)
      .then((result) => {
        if (result.authenticated) {
          setHome(result)
          // Clear optimistic reads once server confirms new counts
          setOptimisticReadIds(new Set())
        }
      })
      .catch(() => {})
  }

  function markReadOptimistic(id: string) {
    setOptimisticReadIds((prev) => new Set([...prev, id]))
  }

  // Effective unread count subtracts optimistically-read messages
  const serverUnread = home?.messages.newMessages ?? 0
  const effectiveUnread = Math.max(0, serverUnread - optimisticReadIds.size)

  const context: PortalOutletContext = {
    session,
    home,
    homeLoading,
    markReadOptimistic,
    refreshHome,
    signOut,
  }

  return (
    <div className="portal-shell">
      <header className="portal-hero">
        <div className="portal-hero-illustration" aria-hidden="true">
          <PulseBadgeIllustration />
        </div>
        <div className="portal-hero-inner">
          <div className="portal-hero-top">
            <div className="row">
              <div className="avatar avatar-on-dark" aria-hidden="true">
                {initials(session.displayName)}
              </div>
              <div>
                <p className="dashboard-hero-greeting">Hello, {session.displayName.split(' ')[0]}</p>
                <p className="dashboard-hero-sub">Patient portal</p>
              </div>
            </div>
            <button className="link-button-on-dark" onClick={signOut}>
              Sign out
            </button>
          </div>

          <div className="hero-stat-row" aria-label="Portal summary">
            {homeLoading ? (
              <>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
              </>
            ) : homeError ? (
              <p className="hero-error-text">{homeError}</p>
            ) : (
              <>
                <Link to="/portal/appointments" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon" aria-hidden="true">
                    <CalendarClock size={16} />
                  </span>
                  <div>
                    <p className="hero-stat-value">{home?.upcomingAppointmentCount ?? 0}</p>
                    <p className="hero-stat-label">Upcoming appointments</p>
                  </div>
                </Link>
                <Link to="/portal/messages" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon" aria-hidden="true">
                    <Mail size={16} />
                  </span>
                  <div>
                    <p className="hero-stat-value">{effectiveUnread}</p>
                    <p className="hero-stat-label">New messages</p>
                  </div>
                </Link>
                <Link to="/portal/appointments" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon" aria-hidden="true">
                    <CalendarClock size={16} />
                  </span>
                  <div>
                    <p className="hero-stat-value">{formatNextAppt(home)}</p>
                    <p className="hero-stat-label">Next appointment</p>
                  </div>
                </Link>
              </>
            )}
          </div>
        </div>
      </header>

      <nav className="portal-tab-nav" aria-label="Portal sections">
        <div className="portal-tab-inner">
          {TABS.map((tab) => {
            const Icon = tab.icon
            const isActive =
              location.pathname === tab.path ||
              (tab.path !== '/portal/home' && location.pathname.startsWith(tab.path))
            const badge =
              tab.path === '/portal/messages' && effectiveUnread > 0 ? effectiveUnread : null
            return (
              <Link
                key={tab.path}
                to={tab.path}
                className={`portal-tab${isActive ? ' portal-tab-active' : ''}`}
                aria-current={isActive ? 'page' : undefined}
              >
                <span className="portal-tab-icon-wrap">
                  <Icon size={18} aria-hidden="true" />
                  {badge != null && (
                    <span className="portal-tab-badge" aria-label={`${badge} unread`}>
                      {badge}
                    </span>
                  )}
                </span>
                <span className="portal-tab-label">{tab.label}</span>
              </Link>
            )
          })}
        </div>
      </nav>

      <div className="portal-content">
        {homeLoading ? (
          <div className="portal-page">
            <div className="portal-section">
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 64 }} />)}
              </div>
            </div>
            <div className="portal-section">
              <div className="skeleton-list">
                {[0, 1].map((i) => <div key={i} className="skeleton-row" style={{ height: 80 }} />)}
              </div>
            </div>
          </div>
        ) : homeError ? (
          <div className="portal-page">
            <div className="portal-section">
              <div className="error-banner" style={{ marginBottom: 0 }}>{homeError}</div>
              <button
                className="button-secondary"
                style={{ marginTop: 16, width: 'auto' }}
                type="button"
                onClick={() => window.location.reload()}
              >
                Retry
              </button>
            </div>
          </div>
        ) : (
          <Outlet context={context} />
        )}
      </div>
    </div>
  )
}
