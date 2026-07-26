import { useEffect, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Activity,
  BarChart2,
  Bell,
  CalendarClock,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  FlaskConical,
  FileText,
  LayoutDashboard,
  LogOut,
  Mail,
  MapPin,
  Pill,
  Package,
  ReceiptText,
  Settings,
  Users,
  Workflow,
} from 'lucide-react'
import { getCurrentSession, getProcedureReportQueue, getOperationalReports } from '../../api.ts'
import { clearClinicianSession, loadClinicianSession, type ClinicianSession } from '../../auth/session.ts'

export type ClinicianOutletContext = {
  session: ClinicianSession
  signOut: () => void
}

const NAV_ITEMS = [
  { path: '/clinician/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/clinician/schedule', label: 'Schedule', icon: CalendarClock },
  { path: '/clinician/calendar', label: 'Calendar', icon: CalendarDays },
  { path: '/clinician/flow', label: 'Flow', icon: Workflow },
  { path: '/clinician/scheduling', label: 'Scheduling', icon: CalendarClock },
  { path: '/clinician/patients', label: 'Patients', icon: Users },
  { path: '/clinician/labs', label: 'Lab queue', icon: FlaskConical },
  { path: '/clinician/messages', label: 'Messages', icon: Mail },
  { path: '/clinician/office-notes', label: 'Office notes', icon: Mail },
  { path: '/clinician/address-book', label: 'Address book', icon: Users },
  { path: '/clinician/tracks', label: 'Configure tracks', icon: Workflow },
  { path: '/clinician/patient-education', label: 'Patient education', icon: Activity },
  { path: '/clinician/recalls', label: 'Recalls', icon: Bell },
  { path: '/clinician/batch-communication', label: 'Batch communication', icon: Mail },
  { path: '/clinician/chart-tracker', label: 'Chart tracker', icon: MapPin },
  { path: '/clinician/document-templates', label: 'Document templates', icon: FileText },
  { path: '/clinician/renewals', label: 'Renewals', icon: Pill },
  { path: '/clinician/reports', label: 'Reports', icon: BarChart2 },
  { path: '/clinician/groups', label: 'Groups', icon: Users },
  { path: '/clinician/billing', label: 'Billing', icon: ReceiptText },
  { path: '/clinician/inventory', label: 'Inventory', icon: Package },
  { path: '/clinician/admin', label: 'Admin', icon: Settings },
]

function initials(name: string) {
  return name.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase()).join('')
}

export default function ClinicianShell() {
  const navigate = useNavigate()
  const location = useLocation()
  const [session] = useState(() => loadClinicianSession())
  const [collapsed, setCollapsed] = useState(false)
  const [authChecked, setAuthChecked] = useState(false)
  const [notifCount, setNotifCount] = useState(0)
  const [notifOpen, setNotifOpen] = useState(false)

  useEffect(() => {
    if (!session) { navigate('/login', { replace: true }); return }
    getCurrentSession(session.sessionId)
      .then((res) => {
        if (!res.authenticated) { clearClinicianSession(); navigate('/login', { replace: true }) }
        else setAuthChecked(true)
      })
      .catch(() => setAuthChecked(true)) // network error — allow through, will fail at data layer
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (!session || !authChecked) return
    function pollNotifs() {
      Promise.all([
        getProcedureReportQueue(session!.sessionId, { status: 'pending', limit: 1 }),
        getOperationalReports(session!.sessionId),
      ])
        .then(([labs, reports]) => {
          setNotifCount(labs.unreviewedReports + reports.counts.newMessages)
        })
        .catch(() => {})
    }
    pollNotifs()
    const timer = setInterval(pollNotifs, 60_000)
    return () => clearInterval(timer)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authChecked])

  if (!session) return null
  if (!authChecked) return (
    <div className="clinician-shell-loading">
      <div className="skeleton-row" style={{ width: 200, height: 20, borderRadius: 6 }} />
    </div>
  )

  function signOut() {
    clearClinicianSession()
    navigate('/login')
  }

  const context: ClinicianOutletContext = { session, signOut }

  return (
    <div className={`clinician-shell${collapsed ? ' clinician-shell-collapsed' : ''}`}>
      {/* Sidebar */}
      <aside className="clinician-sidebar">
        <div className="clinician-sidebar-header">
          {!collapsed && (
            <div className="clinician-brand">
              <Activity size={18} />
              <span>Legacy EHR</span>
            </div>
          )}
          <button
            className="sidebar-collapse-btn"
            type="button"
            onClick={() => setCollapsed((c) => !c)}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}
          </button>
        </div>

        <nav className="clinician-nav" aria-label="Main navigation">
          {NAV_ITEMS.map((item) => {
            const Icon = item.icon
            const isActive =
              location.pathname === item.path ||
              location.pathname.startsWith(item.path + '/')
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`clinician-nav-item${isActive ? ' clinician-nav-item-active' : ''}`}
                aria-current={isActive ? 'page' : undefined}
                title={collapsed ? item.label : undefined}
              >
                <Icon size={18} aria-hidden="true" />
                {!collapsed && <span className="clinician-nav-label">{item.label}</span>}
              </Link>
            )
          })}
        </nav>

        {/* Sidebar footer: notifications + user + sign out */}
        <div className="clinician-sidebar-footer">
          {/* Notification bell */}
          <div className="sidebar-notif-wrap">
            <button
              className="sidebar-notif-btn"
              type="button"
              title="Notifications"
              aria-label="Notifications"
              onClick={() => setNotifOpen((o) => !o)}
            >
              <Bell size={16} />
              {notifCount > 0 && <span className="sidebar-notif-badge">{notifCount > 99 ? '99+' : notifCount}</span>}
              {!collapsed && <span>Alerts</span>}
            </button>
            {notifOpen && (
              <div className="sidebar-notif-panel">
                <p className="sidebar-notif-title">Attention required</p>
                <Link
                  to="/clinician/labs"
                  className="sidebar-notif-item"
                  onClick={() => setNotifOpen(false)}
                >
                  <FlaskConical size={14} />
                  <span>Unreviewed lab reports</span>
                </Link>
                <Link
                  to="/clinician/messages"
                  className="sidebar-notif-item"
                  onClick={() => setNotifOpen(false)}
                >
                  <Mail size={14} />
                  <span>New patient messages</span>
                </Link>
              </div>
            )}
          </div>

          {!collapsed && (
            <div className="clinician-sidebar-user">
              <div className="sidebar-avatar">{initials(session.displayName)}</div>
              <div className="sidebar-user-info">
                <p className="sidebar-user-name">{session.displayName}</p>
                <p className="sidebar-user-role">{session.role}</p>
              </div>
            </div>
          )}
          {collapsed && (
            <div className="sidebar-avatar sidebar-avatar-collapsed" title={session.displayName}>
              {initials(session.displayName)}
            </div>
          )}
          <button
            className="sidebar-signout-btn"
            type="button"
            onClick={signOut}
            title="Sign out"
            aria-label="Sign out"
          >
            <LogOut size={16} />
            {!collapsed && <span>Sign out</span>}
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main className="clinician-main" id="main-content">
        <Outlet context={context} />
      </main>
    </div>
  )
}
