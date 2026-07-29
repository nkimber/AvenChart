import { useEffect, useRef, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Activity,
  BarChart2,
  Bell,
  CalendarClock,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ClipboardPlus,
  FileText,
  FlaskConical,
  Gauge,
  LayoutDashboard,
  LogOut,
  Mail,
  MapPin,
  Menu,
  Package,
  Pill,
  ReceiptText,
  RefreshCw,
  Route,
  ScanText,
  Settings,
  Users,
  Workflow,
  X,
} from 'lucide-react'
import {
  getCurrentSession,
  getOperationalReports,
  getProcedureReportQueue,
  isInvalidSessionError,
  isRequestCancellation,
  logout,
  SESSION_INVALID_EVENT,
} from '../../api.ts'
import { clearClinicianSession, loadClinicianSession, type ClinicianSession } from '../../auth/session.ts'

export type ClinicianOutletContext = {
  session: ClinicianSession
  signOut: () => Promise<void>
}

const NAV_ITEMS = [
  { path: '/clinician/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/clinician/schedule', label: 'Schedule', icon: CalendarClock },
  { path: '/clinician/calendar', label: 'Calendar', icon: CalendarDays },
  { path: '/clinician/flow', label: 'Flow', icon: Workflow },
  { path: '/clinician/scheduling', label: 'Scheduling', icon: CalendarClock },
  { path: '/clinician/patients', label: 'Patients', icon: Users },
  { path: '/clinician/labs', label: 'Lab queue', icon: FlaskConical },
  { path: '/clinician/lab-directory', label: 'Lab directory', icon: FlaskConical },
  { path: '/clinician/messages', label: 'Messages', icon: Mail },
  { path: '/clinician/office-notes', label: 'Office notes', icon: Mail },
  { path: '/clinician/address-book', label: 'Address book', icon: Users },
  { path: '/clinician/tracks', label: 'Configure tracks', icon: Workflow },
  { path: '/clinician/track-entries', label: 'Track entries', icon: ClipboardPlus },
  { path: '/clinician/track-history', label: 'Track history', icon: BarChart2 },
  { path: '/clinician/patient-education', label: 'Patient education', icon: Activity },
  { path: '/clinician/recalls', label: 'Recalls', icon: Bell },
  { path: '/clinician/batch-communication', label: 'Batch communication', icon: Mail },
  { path: '/clinician/chart-tracker', label: 'Chart tracker', icon: MapPin },
  { path: '/clinician/documents', label: 'Document queue', icon: Route },
  { path: '/clinician/document-ocr', label: 'OCR queue', icon: ScanText },
  { path: '/clinician/document-templates', label: 'Document templates', icon: FileText },
  { path: '/clinician/duplicate-review', label: 'Duplicate review', icon: Users },
  { path: '/clinician/renewals', label: 'Renewals', icon: Pill },
  { path: '/clinician/reports', label: 'Reports', icon: BarChart2 },
  { path: '/clinician/groups', label: 'Groups', icon: Users },
  { path: '/clinician/billing', label: 'Billing', icon: ReceiptText },
  { path: '/clinician/inventory', label: 'Inventory', icon: Package },
  { path: '/clinician/admin', label: 'Admin', icon: Settings },
  { path: '/clinician/experience', label: 'Experience baseline', icon: Gauge },
] as const

type NavigationListProps = {
  currentPath: string
  collapsed?: boolean
  onNavigate?: () => void
  label: string
}

function NavigationList({
  currentPath,
  collapsed = false,
  onNavigate,
  label,
}: NavigationListProps) {
  return (
    <nav className="clinician-nav" aria-label={label}>
      {NAV_ITEMS.map((item) => {
        const Icon = item.icon
        const isActive =
          currentPath === item.path || currentPath.startsWith(`${item.path}/`)
        return (
          <Link
            key={item.path}
            to={item.path}
            className={`clinician-nav-item${isActive ? ' clinician-nav-item-active' : ''}`}
            aria-current={isActive ? 'page' : undefined}
            title={collapsed ? item.label : undefined}
            onClick={onNavigate}
          >
            <Icon size={18} aria-hidden="true" />
            {!collapsed && <span className="clinician-nav-label">{item.label}</span>}
          </Link>
        )
      })}
    </nav>
  )
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

export default function ClinicianShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const [session] = useState(() => loadClinicianSession())
  const [collapsed, setCollapsed] = useState(false)
  const [authState, setAuthState] = useState<'checking' | 'authenticated' | 'unavailable'>('checking')
  const [authRetry, setAuthRetry] = useState(0)
  const [authError, setAuthError] = useState<string | null>(null)
  const [notifCount, setNotifCount] = useState(0)
  const [notifBreakdown, setNotifBreakdown] = useState({
    labs: 0,
    messages: 0,
  })
  const [notifOpen, setNotifOpen] = useState(false)
  const [notifError, setNotifError] = useState<string | null>(null)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [signingOut, setSigningOut] = useState(false)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const mobileDrawerRef = useRef<HTMLElement>(null)

  useEffect(() => {
    if (!session) {
      navigate('/login', { replace: true })
      return
    }

    const controller = new AbortController()
    setAuthState('checking')
    setAuthError(null)
    getCurrentSession(session.sessionId, controller.signal)
      .then((result) => {
        if (!result.authenticated) {
          clearClinicianSession()
          navigate('/login', { replace: true })
          return
        }
        setAuthState('authenticated')
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        if (isInvalidSessionError(error)) {
          clearClinicianSession()
          navigate('/login', { replace: true })
          return
        }
        setAuthError(
          error instanceof Error
            ? error.message
            : 'The server could not verify your session.',
        )
        setAuthState('unavailable')
      })
    return () => controller.abort()
  }, [authRetry, navigate, session])

  useEffect(() => {
    function handleInvalidSession(event: Event) {
      const detail = (event as CustomEvent<{ scope?: string }>).detail
      if (detail?.scope !== 'clinician') return
      clearClinicianSession()
      navigate('/login', { replace: true })
    }

    window.addEventListener(SESSION_INVALID_EVENT, handleInvalidSession)
    return () => window.removeEventListener(SESSION_INVALID_EVENT, handleInvalidSession)
  }, [navigate])

  useEffect(() => {
    if (!session || authState !== 'authenticated') return
    let cancelled = false

    function pollNotifications() {
      Promise.all([
        getProcedureReportQueue(session!.sessionId, { status: 'pending', limit: 1 }),
        getOperationalReports(session!.sessionId),
      ])
        .then(([labs, reports]) => {
          if (cancelled) return
          setNotifCount(labs.unreviewedReports + reports.counts.newMessages)
          setNotifBreakdown({
            labs: labs.unreviewedReports,
            messages: reports.counts.newMessages,
          })
          setNotifError(null)
        })
        .catch((error: unknown) => {
          if (cancelled || isInvalidSessionError(error)) return
          setNotifError(
            error instanceof Error ? error.message : 'Alerts could not be refreshed.',
          )
        })
    }

    pollNotifications()
    const timer = window.setInterval(pollNotifications, 60_000)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [authState, session])

  useEffect(() => {
    setMobileOpen(false)
  }, [location.pathname])

  useEffect(() => {
    if (!mobileOpen) return
    const drawer = mobileDrawerRef.current
    const previouslyFocused = document.activeElement as HTMLElement | null
    const menuButton = menuButtonRef.current
    const previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    drawer?.querySelector<HTMLElement>('a, button')?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMobileOpen(false)
        return
      }
      if (event.key !== 'Tab' || !drawer) return
      const focusable = [...drawer.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )]
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable.at(-1)
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last?.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = previousBodyOverflow
      ;(previouslyFocused ?? menuButton)?.focus()
    }
  }, [mobileOpen])

  if (!session) return null

  async function signOut() {
    if (signingOut) return
    setSigningOut(true)
    const controller = new AbortController()
    const timeout = window.setTimeout(() => controller.abort(), 5_000)
    try {
      await logout(session!.sessionId, controller.signal)
    } catch {
      // Local cleanup is still required if the server is unavailable.
    } finally {
      window.clearTimeout(timeout)
      clearClinicianSession()
      navigate('/login', { replace: true })
    }
  }

  if (authState === 'checking') {
    return (
      <div className="clinician-shell-loading" aria-live="polite">
        <div className="skeleton-row" style={{ width: 200, height: 20, borderRadius: 6 }} />
        <span className="sr-only">Verifying your session</span>
      </div>
    )
  }

  if (authState === 'unavailable') {
    return (
      <main className="session-unavailable" id="main-content">
        <Activity size={32} aria-hidden="true" />
        <h1>Unable to verify your session</h1>
        <p>{authError ?? 'The server could not verify your session. Check your connection and try again.'}</p>
        <div className="session-unavailable-actions">
          <button
            className="cl-btn-primary"
            type="button"
            onClick={() => setAuthRetry((value) => value + 1)}
          >
            <RefreshCw size={16} aria-hidden="true" />
            Retry
          </button>
          <button className="cl-btn-secondary" type="button" onClick={() => void signOut()}>
            Sign out
          </button>
        </div>
      </main>
    )
  }

  const context: ClinicianOutletContext = { session, signOut }
  const notificationPanel = notifOpen && (
    <div className="sidebar-notif-panel">
      <p className="sidebar-notif-title">Attention required</p>
      {notifError && <p className="sidebar-notif-error" role="status">{notifError}</p>}
      <Link
        to="/clinician/labs?status=pending"
        className="sidebar-notif-item"
        onClick={() => {
          setNotifOpen(false)
          setMobileOpen(false)
        }}
      >
        <FlaskConical size={14} aria-hidden="true" />
        <span>{notifBreakdown.labs} unreviewed lab reports</span>
      </Link>
      <Link
        to="/clinician/messages?status=new"
        className="sidebar-notif-item"
        onClick={() => {
          setNotifOpen(false)
          setMobileOpen(false)
        }}
      >
        <Mail size={14} aria-hidden="true" />
        <span>{notifBreakdown.messages} new patient messages</span>
      </Link>
    </div>
  )

  const sidebarFooter = (isCollapsed: boolean) => (
    <div className="clinician-sidebar-footer">
      <div className="sidebar-notif-wrap">
        <button
          className="sidebar-notif-btn"
          type="button"
          title="Notifications"
          aria-label="Notifications"
          aria-expanded={notifOpen}
          onClick={() => setNotifOpen((open) => !open)}
        >
          <Bell size={16} aria-hidden="true" />
          {notifCount > 0 && (
            <span className="sidebar-notif-badge">{notifCount > 99 ? '99+' : notifCount}</span>
          )}
          {!isCollapsed && <span>Alerts</span>}
        </button>
        {notificationPanel}
      </div>
      {!isCollapsed ? (
        <div className="clinician-sidebar-user">
          <div className="sidebar-avatar">{initials(session.displayName)}</div>
          <div className="sidebar-user-info">
            <p className="sidebar-user-name">{session.displayName}</p>
            <p className="sidebar-user-role">{session.role}</p>
          </div>
        </div>
      ) : (
        <div className="sidebar-avatar sidebar-avatar-collapsed" title={session.displayName}>
          {initials(session.displayName)}
        </div>
      )}
      <button
        className="sidebar-signout-btn"
        type="button"
        onClick={() => void signOut()}
        disabled={signingOut}
        title="Sign out"
        aria-label="Sign out"
      >
        <LogOut size={16} aria-hidden="true" />
        {!isCollapsed && <span>{signingOut ? 'Signing out…' : 'Sign out'}</span>}
      </button>
    </div>
  )

  return (
    <div className={`clinician-shell${collapsed ? ' clinician-shell-collapsed' : ''}`}>
      <header className="clinician-mobile-header">
        <div className="clinician-brand">
          <Activity size={18} aria-hidden="true" />
          <span>Legacy EHR</span>
        </div>
        <button
          ref={menuButtonRef}
          className="clinician-mobile-menu-button"
          type="button"
          aria-label="Open navigation"
          aria-expanded={mobileOpen}
          onClick={() => setMobileOpen(true)}
        >
          <Menu size={22} aria-hidden="true" />
        </button>
      </header>

      <aside className="clinician-sidebar">
        <div className="clinician-sidebar-header">
          {!collapsed && (
            <div className="clinician-brand">
              <Activity size={18} aria-hidden="true" />
              <span>Legacy EHR</span>
            </div>
          )}
          <button
            className="sidebar-collapse-btn"
            type="button"
            onClick={() => setCollapsed((value) => !value)}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}
          </button>
        </div>
        <NavigationList
          currentPath={location.pathname}
          collapsed={collapsed}
          label="Main navigation"
        />
        {sidebarFooter(collapsed)}
      </aside>

      {mobileOpen && (
        <>
          <button
            className="clinician-mobile-overlay"
            type="button"
            aria-label="Close navigation"
            onClick={() => setMobileOpen(false)}
          />
          <aside
            className="clinician-mobile-drawer"
            ref={mobileDrawerRef}
            role="dialog"
            aria-modal="true"
            aria-label="Main navigation"
          >
            <div className="clinician-sidebar-header">
              <div className="clinician-brand">
                <Activity size={18} aria-hidden="true" />
                <span>Legacy EHR</span>
              </div>
              <button
                className="clinician-mobile-menu-button"
                type="button"
                aria-label="Close navigation"
                onClick={() => setMobileOpen(false)}
              >
                <X size={22} aria-hidden="true" />
              </button>
            </div>
            <NavigationList
              currentPath={location.pathname}
              onNavigate={() => setMobileOpen(false)}
              label="Mobile navigation"
            />
            {sidebarFooter(false)}
          </aside>
        </>
      )}

      <main className="clinician-main" id="main-content">
        <Outlet context={context} />
      </main>
    </div>
  )
}
