// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { CalendarClock, FolderOpen, Home, Mail, UserCircle } from 'lucide-react'
import {
  endPatientPortalSession,
  getPatientPortalHome,
  getPatientPortalSession,
  isInvalidSessionError,
  isRequestCancellation,
  SESSION_INVALID_EVENT,
  type PatientPortalHomeSummaryResponse,
} from '../../api.ts'
import { clearPortalSession, loadPortalSession, type PortalSession } from '../../auth/session.ts'
import { PulseBadgeIllustration } from '../../illustrations.tsx'

export type PortalOutletContext = {
  session: PortalSession
  home: PatientPortalHomeSummaryResponse | null
  homeLoading: boolean
  /** Call to immediately decrement the unread badge (optimistic read). */
  markReadOptimistic: (id: string) => void
  refreshHome: () => Promise<void>
  signOut: () => Promise<void>
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

function formatNextAppt(home: PatientPortalHomeSummaryResponse | null): string {
  const next = home?.upcomingAppointments?.[0]
  if (!next) return 'None scheduled'
  const [year, month, day] = next.date.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

const TABS = [
  { path: '/portal/home', label: 'Home', icon: Home },
  { path: '/portal/messages', label: 'Messages', icon: Mail },
  { path: '/portal/appointments', label: 'Appointments', icon: CalendarClock },
  { path: '/portal/records', label: 'Records', icon: FolderOpen },
  { path: '/portal/account', label: 'Account', icon: UserCircle },
] as const

export default function PortalShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const [session] = useState(() => loadPortalSession())
  const [home, setHome] = useState<PatientPortalHomeSummaryResponse | null>(null)
  const [homeLoading, setHomeLoading] = useState(true)
  const [homeError, setHomeError] = useState<string | null>(null)
  const [loadAttempt, setLoadAttempt] = useState(0)
  const [signingOut, setSigningOut] = useState(false)
  const [optimisticReadIds, setOptimisticReadIds] = useState<Set<string>>(() => new Set())

  useEffect(() => {
    if (!session) {
      navigate('/portal/login', { replace: true })
      return
    }

    const controller = new AbortController()
    setHomeLoading(true)
    setHomeError(null)
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
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        if (isInvalidSessionError(error)) {
          clearPortalSession()
          navigate('/portal/login', { replace: true })
          return
        }
        setHomeError(
          error instanceof Error
            ? error.message
            : 'Could not load your portal. Check your connection and try again.',
        )
        setHomeLoading(false)
      })
    return () => controller.abort()
  }, [loadAttempt, navigate, session])

  useEffect(() => {
    function handleInvalidSession(event: Event) {
      const detail = (event as CustomEvent<{ scope?: string }>).detail
      if (detail?.scope !== 'portal') return
      clearPortalSession()
      navigate('/portal/login', { replace: true })
    }

    window.addEventListener(SESSION_INVALID_EVENT, handleInvalidSession)
    return () => window.removeEventListener(SESSION_INVALID_EVENT, handleInvalidSession)
  }, [navigate])

  if (!session) return null

  async function signOut() {
    if (signingOut) return
    setSigningOut(true)
    const controller = new AbortController()
    const timeout = window.setTimeout(() => controller.abort(), 5_000)
    try {
      await endPatientPortalSession(session!.sessionId, controller.signal)
    } catch {
      // Local cleanup remains reliable while the API is unavailable.
    } finally {
      window.clearTimeout(timeout)
      clearPortalSession()
      navigate('/portal/login', { replace: true })
    }
  }

  async function refreshHome() {
    if (!session) return
    setHomeError(null)
    try {
      const result = await getPatientPortalHome(session.sessionId)
      if (!result.authenticated) {
        clearPortalSession()
        navigate('/portal/login', { replace: true })
        return
      }
      setHome(result)
      setOptimisticReadIds(new Set())
    } catch (error) {
      if (isInvalidSessionError(error)) return
      setHomeError(
        error instanceof Error ? error.message : 'Could not refresh the portal summary.',
      )
    }
  }

  function markReadOptimistic(id: string) {
    setOptimisticReadIds((previous) => new Set([...previous, id]))
  }

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
            <button
              className="link-button-on-dark"
              type="button"
              disabled={signingOut}
              onClick={() => void signOut()}
            >
              {signingOut ? 'Signing out…' : 'Sign out'}
            </button>
          </div>

          <div className="hero-stat-row" role="region" aria-label="Portal summary">
            {homeLoading ? (
              <>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
                <div className="hero-stat-chip"><div className="skeleton-chip" /></div>
              </>
            ) : homeError ? (
              <p className="hero-error-text" role="status">{homeError}</p>
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
                aria-label={tab.label}
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

      <div className="portal-content" id="main-content">
        {homeLoading ? (
          <div className="portal-page" aria-live="polite">
            <span className="sr-only">Loading portal</span>
            <div className="portal-section">
              <div className="skeleton-list">
                {[0, 1, 2].map((item) => (
                  <div key={item} className="skeleton-row" style={{ height: 64 }} />
                ))}
              </div>
            </div>
            <div className="portal-section">
              <div className="skeleton-list">
                {[0, 1].map((item) => (
                  <div key={item} className="skeleton-row" style={{ height: 80 }} />
                ))}
              </div>
            </div>
          </div>
        ) : homeError ? (
          <div className="portal-page">
            <div className="portal-section">
              <div className="error-banner" style={{ marginBottom: 0 }} role="alert">
                {homeError}
              </div>
              <button
                className="button-secondary"
                style={{ marginTop: 16, width: 'auto' }}
                type="button"
                onClick={() => setLoadAttempt((attempt) => attempt + 1)}
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
