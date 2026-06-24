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
      .catch(() => setHomeLoading(false))
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
        if (result.authenticated) setHome(result)
      })
      .catch(() => {})
  }

  const context: PortalOutletContext = { session, home, homeLoading, refreshHome, signOut }

  return (
    <div className="portal-shell">
      <header className="portal-hero">
        <div className="portal-hero-illustration">
          <PulseBadgeIllustration />
        </div>
        <div className="portal-hero-inner">
          <div className="portal-hero-top">
            <div className="row">
              <div className="avatar avatar-on-dark">{initials(session.displayName)}</div>
              <div>
                <p className="dashboard-hero-greeting">Hello, {session.displayName.split(' ')[0]}</p>
                <p className="dashboard-hero-sub">Patient portal</p>
              </div>
            </div>
            <button className="link-button-on-dark" onClick={signOut}>
              Sign out
            </button>
          </div>

          <div className="hero-stat-row">
            {homeLoading ? (
              <>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
              </>
            ) : (
              <>
                <Link to="/portal/appointments" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon">
                    <CalendarClock size={16} />
                  </span>
                  <div>
                    <p className="hero-stat-value">{home?.upcomingAppointmentCount ?? 0}</p>
                    <p className="hero-stat-label">Upcoming appointments</p>
                  </div>
                </Link>
                <Link to="/portal/messages" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon">
                    <Mail size={16} />
                  </span>
                  <div>
                    <p className="hero-stat-value">{home?.messages.newMessages ?? 0}</p>
                    <p className="hero-stat-label">New messages</p>
                  </div>
                </Link>
                <Link to="/portal/appointments" className="hero-stat-chip hero-stat-link">
                  <span className="hero-stat-icon">
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

      <nav className="portal-tab-nav">
        <div className="portal-tab-inner">
          {TABS.map((tab) => {
            const Icon = tab.icon
            const isActive =
              location.pathname === tab.path ||
              (tab.path !== '/portal/home' && location.pathname.startsWith(tab.path))
            const badge =
              tab.path === '/portal/messages' && (home?.messages.newMessages ?? 0) > 0
                ? home!.messages.newMessages
                : null
            return (
              <Link
                key={tab.path}
                to={tab.path}
                className={`portal-tab${isActive ? ' portal-tab-active' : ''}`}
              >
                <span className="portal-tab-icon-wrap">
                  <Icon size={18} />
                  {badge != null && <span className="portal-tab-badge">{badge}</span>}
                </span>
                <span className="portal-tab-label">{tab.label}</span>
              </Link>
            )
          })}
        </div>
      </nav>

      <div className="portal-content">
        <Outlet context={context} />
      </div>
    </div>
  )
}
