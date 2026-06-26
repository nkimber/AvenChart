import { useEffect, useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import {
  CalendarClock, ChevronRight, ClipboardList,
  Clock, FileText, FlaskConical, Mail, Plus, RefreshCw, Users, UserPlus,
} from 'lucide-react'
import {
  getProcedureReportQueue,
  searchAppointments,
  getOperationalReports,
  type AppointmentListItem,
  type ProcedureReportQueueResponse,
  type OperationalReportsResponse,
} from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error' }

type RecentPatient = { canonicalId: string; displayName: string; dateOfBirth: string; visitedAt: string }

const RECENT_KEY = 'clinician-recent-patients'

export function recordRecentPatient(p: { canonicalId: string; displayName: string; dateOfBirth: string }) {
  try {
    const existing: RecentPatient[] = JSON.parse(sessionStorage.getItem(RECENT_KEY) ?? '[]')
    const fresh = [{ ...p, visitedAt: new Date().toISOString() }, ...existing.filter((r) => r.canonicalId !== p.canonicalId)].slice(0, 6)
    sessionStorage.setItem(RECENT_KEY, JSON.stringify(fresh))
  } catch { /* ignore */ }
}

function loadRecentPatients(): RecentPatient[] {
  try { return JSON.parse(sessionStorage.getItem(RECENT_KEY) ?? '[]') } catch { return [] }
}

function today() { return new Date().toISOString().slice(0, 10) }
function formatTime(t?: string | null) { return t ? t.slice(0, 5) : '' }

function apptStatusClass(status?: string | null) {
  if (!status) return ''
  const s = status.toLowerCase()
  if (s.includes('cancel')) return 'appt-status-cancelled'
  if (s.includes('complet') || s.includes('check')) return 'appt-status-completed'
  if (s.includes('pend') || s.includes('arriv') || s.includes('room')) return 'appt-status-pending'
  return 'appt-status-scheduled'
}

function greeting() {
  const h = new Date().getHours()
  if (h < 12) return 'morning'
  if (h < 17) return 'afternoon'
  return 'evening'
}

const DASH_LIMIT = 5

export default function ClinicianDashboard() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [apptState, setApptState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const [labState, setLabState] = useState<AsyncState<ProcedureReportQueueResponse>>({ status: 'loading' })
  const [reportsState, setReportsState] = useState<AsyncState<OperationalReportsResponse>>({ status: 'loading' })
  const [recentPatients, setRecentPatients] = useState<RecentPatient[]>(() => loadRecentPatients())

  useEffect(() => {
    const todayStr = today()
    searchAppointments(session.sessionId, { fromDate: todayStr, toDate: todayStr, limit: 20 })
      .then((data) => setApptState({ status: 'ready', data: data.appointments }))
      .catch(() => setApptState({ status: 'error' }))

    getProcedureReportQueue(session.sessionId, { status: 'pending', limit: 5 })
      .then((data) => setLabState({ status: 'ready', data }))
      .catch(() => setLabState({ status: 'error' }))

    getOperationalReports(session.sessionId)
      .then((data) => setReportsState({ status: 'ready', data }))
      .catch(() => setReportsState({ status: 'error' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const onFocus = () => setRecentPatients(loadRecentPatients())
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [])

  const todayLabel = new Date().toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })
  const appts = apptState.status === 'ready' ? apptState.data : []
  const unreviewedLabs = labState.status === 'ready' ? labState.data.unreviewedReports : null
  const newMessages = reportsState.status === 'ready' ? reportsState.data.counts.newMessages : null

  return (
    <div className="clinician-page">
      {/* Header */}
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Good {greeting()}, {session.displayName.split(' ')[0]}</h1>
          <p className="clinician-page-subtitle">{todayLabel}</p>
        </div>
        <div className="clinician-header-actions">
          <button className="cl-btn-secondary" type="button" onClick={() => navigate('/clinician/patients/new')}>
            <UserPlus size={15} /> Register patient
          </button>
          <button className="cl-btn-secondary" type="button" onClick={() => navigate('/clinician/encounters/new')}>
            <Plus size={15} /> New encounter
          </button>
          <button className="cl-btn-primary" type="button" onClick={() => navigate('/clinician/patients')}>
            <Users size={15} /> Patient search
          </button>
        </div>
      </div>

      {/* Stat tiles */}
      <div className="dash-stat-row">
        <Link to="/clinician/schedule" className="dash-stat-tile">
          <div className="dash-stat-icon dash-stat-icon-blue"><CalendarClock size={18} /></div>
          <div className="dash-stat-body">
            <p className="dash-stat-value">
              {apptState.status === 'loading' ? '—' : appts.length}
            </p>
            <p className="dash-stat-label">Today's appointments</p>
          </div>
          <ChevronRight size={14} className="dash-stat-arrow" />
        </Link>

        <Link to="/clinician/labs" className={`dash-stat-tile${unreviewedLabs ? ' dash-stat-tile-alert' : ''}`}>
          <div className={`dash-stat-icon${unreviewedLabs ? ' dash-stat-icon-amber' : ' dash-stat-icon-muted'}`}><FlaskConical size={18} /></div>
          <div className="dash-stat-body">
            <p className="dash-stat-value">
              {labState.status === 'loading' ? '—' : (unreviewedLabs ?? 0)}
            </p>
            <p className="dash-stat-label">Labs pending review</p>
          </div>
          <ChevronRight size={14} className="dash-stat-arrow" />
        </Link>

        <Link to="/clinician/messages" className={`dash-stat-tile${newMessages ? ' dash-stat-tile-alert' : ''}`}>
          <div className={`dash-stat-icon${newMessages ? ' dash-stat-icon-indigo' : ' dash-stat-icon-muted'}`}><Mail size={18} /></div>
          <div className="dash-stat-body">
            <p className="dash-stat-value">
              {reportsState.status === 'loading' ? '—' : (newMessages ?? 0)}
            </p>
            <p className="dash-stat-label">New messages</p>
          </div>
          <ChevronRight size={14} className="dash-stat-arrow" />
        </Link>

        <Link to="/clinician/renewals" className="dash-stat-tile">
          <div className="dash-stat-icon dash-stat-icon-muted"><RefreshCw size={18} /></div>
          <div className="dash-stat-body">
            <p className="dash-stat-value dash-stat-value-sm">Renewals</p>
            <p className="dash-stat-label">Prescription queue</p>
          </div>
          <ChevronRight size={14} className="dash-stat-arrow" />
        </Link>
      </div>

      <div className="clinician-dashboard-grid">
        {/* Today's schedule — capped + scrollable */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><CalendarClock size={16} /> Today's schedule</h2>
            <Link to="/clinician/schedule" className="cl-link">
              {appts.length > DASH_LIMIT ? `All ${appts.length}` : 'Full schedule'}
              <ChevronRight size={14} />
            </Link>
          </div>

          {apptState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 48 }} />)}
            </div>
          )}
          {apptState.status === 'error' && (
            <p className="cl-empty-text">Could not load today's appointments.</p>
          )}
          {apptState.status === 'ready' && appts.length === 0 && (
            <div className="cl-empty-state-sm">
              <CalendarClock size={24} className="cl-empty-icon-sm" />
              <p>No appointments today.</p>
              <Link to="/clinician/calendar" className="cl-link">Open calendar</Link>
            </div>
          )}
          {apptState.status === 'ready' && appts.length > 0 && (
            <>
              <ul className="cl-appt-list dash-appt-list">
                {appts.slice(0, DASH_LIMIT).map((appt) => (
                  <li key={appt.id} className="cl-appt-row">
                    <div className="cl-appt-time"><Clock size={12} />{formatTime(appt.startTime)}</div>
                    <div className="cl-appt-body">
                      <button
                        className="cl-appt-patient"
                        type="button"
                        onClick={() => navigate(`/clinician/patients/${appt.patientId}/summary`)}
                      >
                        {appt.patientDisplayName}
                      </button>
                      <p className="cl-appt-meta">
                        {appt.title}
                        {appt.room ? ` · Room ${appt.room}` : ''}
                      </p>
                    </div>
                    {appt.status && (
                      <span className={`appt-status ${apptStatusClass(appt.status)}`}>
                        {appt.status}
                      </span>
                    )}
                  </li>
                ))}
              </ul>
              {appts.length > DASH_LIMIT && (
                <Link to="/clinician/schedule" className="dash-show-more">
                  +{appts.length - DASH_LIMIT} more appointments — view full schedule
                </Link>
              )}
            </>
          )}
        </section>

        {/* Right column */}
        <div className="clinician-dashboard-right">
          {/* Quick actions */}
          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">Quick actions</h2>
            </div>
            <div className="dash-quick-actions">
              {[
                { label: 'Patient search', path: '/clinician/patients', icon: Users, color: 'blue' },
                { label: "Today's schedule", path: '/clinician/schedule', icon: CalendarClock, color: 'blue' },
                { label: 'New encounter', path: '/clinician/encounters/new', icon: FileText, color: 'teal' },
                { label: 'Register patient', path: '/clinician/patients/new', icon: UserPlus, color: 'teal' },
                { label: 'Lab queue', path: '/clinician/labs', icon: FlaskConical, color: 'amber' },
                { label: 'Rx renewals', path: '/clinician/renewals', icon: RefreshCw, color: 'muted' },
                { label: 'Messages', path: '/clinician/messages', icon: Mail, color: newMessages ? 'indigo' : 'muted' },
                { label: 'Reports', path: '/clinician/reports', icon: ClipboardList, color: 'muted' },
              ].map(({ label, path, icon: Icon, color }) => (
                <Link key={path} to={path} className={`dash-action-tile dash-action-${color}`}>
                  <div className="dash-action-icon"><Icon size={15} /></div>
                  <p className="dash-action-label">{label}</p>
                </Link>
              ))}
            </div>
          </section>

          {/* Recent patients */}
          {recentPatients.length > 0 && (
            <section className="cl-card">
              <div className="cl-card-header">
                <h2 className="cl-card-title"><Clock size={16} /> Recently viewed</h2>
                <Link to="/clinician/patients" className="cl-link">Search <ChevronRight size={14} /></Link>
              </div>
              <ul className="cl-recent-patients">
                {recentPatients.map((p) => (
                  <li key={p.canonicalId}>
                    <button
                      className="cl-recent-patient-btn"
                      type="button"
                      onClick={() => navigate(`/clinician/patients/${p.canonicalId}/summary`)}
                    >
                      <div className="cl-recent-avatar">
                        {p.displayName.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <p className="cl-recent-name">{p.displayName}</p>
                        <p className="cl-recent-dob">DOB {p.dateOfBirth}</p>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>
    </div>
  )
}
