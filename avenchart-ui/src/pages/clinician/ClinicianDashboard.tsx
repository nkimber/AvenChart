import { useEffect, useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { CalendarClock, ChevronRight, ClipboardList, Clock, FlaskConical, Mail, Plus, Users } from 'lucide-react'
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
    const fresh = [{ ...p, visitedAt: new Date().toISOString() }, ...existing.filter((r) => r.canonicalId !== p.canonicalId)].slice(0, 8)
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

  // Refresh recent patients on focus
  useEffect(() => {
    const onFocus = () => setRecentPatients(loadRecentPatients())
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [])

  const todayLabel = new Date().toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })

  const unreviewedLabs = labState.status === 'ready' ? labState.data.unreviewedReports : 0
  const newMessages = reportsState.status === 'ready' ? reportsState.data.counts.newMessages : 0
  const hasActions = unreviewedLabs > 0 || newMessages > 0

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Good {greeting()}, {session.displayName.split(' ')[0]}</h1>
          <p className="clinician-page-subtitle">{todayLabel}</p>
        </div>
        <div className="clinician-header-actions">
          <button className="cl-btn-secondary" type="button" onClick={() => navigate('/clinician/encounters/new')}>
            <Plus size={15} /> New encounter
          </button>
          <button className="cl-btn-primary" type="button" onClick={() => navigate('/clinician/patients')}>
            <Users size={15} /> Patient search
          </button>
        </div>
      </div>

      {/* Action inbox */}
      {hasActions && (
        <section className="cl-card dash-inbox">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><ClipboardList size={16} /> Needs attention</h2>
          </div>
          <div className="dash-inbox-items">
            {unreviewedLabs > 0 && (
              <Link to="/clinician/labs" className="dash-inbox-item dash-inbox-labs">
                <div className="dash-inbox-icon"><FlaskConical size={18} /></div>
                <div className="dash-inbox-body">
                  <p className="dash-inbox-count">{unreviewedLabs}</p>
                  <p className="dash-inbox-label">lab report{unreviewedLabs !== 1 ? 's' : ''} pending review</p>
                </div>
                <ChevronRight size={16} className="dash-inbox-arrow" />
              </Link>
            )}
            {newMessages > 0 && (
              <Link to="/clinician/messages" className="dash-inbox-item dash-inbox-messages">
                <div className="dash-inbox-icon"><Mail size={18} /></div>
                <div className="dash-inbox-body">
                  <p className="dash-inbox-count">{newMessages}</p>
                  <p className="dash-inbox-label">new patient message{newMessages !== 1 ? 's' : ''}</p>
                </div>
                <ChevronRight size={16} className="dash-inbox-arrow" />
              </Link>
            )}
          </div>
        </section>
      )}

      <div className="clinician-dashboard-grid">
        {/* Today's schedule */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <CalendarClock size={16} />
              Today's schedule
            </h2>
            <Link to="/clinician/schedule" className="cl-link">
              Full schedule
              <ChevronRight size={14} />
            </Link>
          </div>

          {apptState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 56 }} />)}
            </div>
          )}
          {apptState.status === 'error' && (
            <p className="cl-empty-text">Could not load today's appointments.</p>
          )}
          {apptState.status === 'ready' && apptState.data.length === 0 && (
            <p className="cl-empty-text">No appointments scheduled for today.</p>
          )}
          {apptState.status === 'ready' && apptState.data.length > 0 && (
            <ul className="cl-appt-list">
              {apptState.data.map((appt) => (
                <li key={appt.id} className="cl-appt-row">
                  <div className="cl-appt-time"><Clock size={12} />{formatTime(appt.startTime)}</div>
                  <div className="cl-appt-body">
                    <button
                      className="cl-appt-patient"
                      type="button"
                      onClick={() => navigate(`/clinician/patients/${appt.patientId}`)}
                    >
                      {appt.patientDisplayName}
                    </button>
                    <p className="cl-appt-meta">
                      {appt.title}
                      {appt.providerName ? ` · ${appt.providerName}` : ''}
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
          )}
        </section>

        {/* Right column */}
        <div className="clinician-dashboard-right">
          {/* Recent patients */}
          {recentPatients.length > 0 && (
            <section className="cl-card">
              <div className="cl-card-header">
                <h2 className="cl-card-title"><Clock size={16} /> Recent patients</h2>
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

          {/* Quick links */}
          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">Quick links</h2>
            </div>
            <div className="cl-quick-links">
              {[
                { label: 'Patient search', path: '/clinician/patients', icon: Users },
                { label: 'Full schedule', path: '/clinician/schedule', icon: CalendarClock },
                { label: 'Lab queue', path: '/clinician/labs', icon: FlaskConical },
                { label: 'Renewals', path: '/clinician/renewals', icon: ClipboardList },
              ].map((link) => {
                const Icon = link.icon
                return (
                  <Link key={link.path} to={link.path} className="cl-quick-link">
                    <Icon size={16} aria-hidden="true" />
                    {link.label}
                  </Link>
                )
              })}
            </div>
          </section>

          {/* Lab queue summary */}
          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">
                <FlaskConical size={16} />
                Lab queue
              </h2>
              <Link to="/clinician/labs" className="cl-link">
                View all
                <ChevronRight size={14} />
              </Link>
            </div>

            {labState.status === 'loading' && (
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 44 }} />)}
              </div>
            )}
            {labState.status === 'error' && (
              <p className="cl-empty-text">Could not load lab queue.</p>
            )}
            {labState.status === 'ready' && (
              <>
                {labState.data.unreviewedReports > 0 && (
                  <div className="cl-stat-banner">
                    <span className="cl-stat-number">{labState.data.unreviewedReports}</span>
                    <span className="cl-stat-label">reports awaiting review</span>
                  </div>
                )}
                {labState.data.reports.length === 0 ? (
                  <p className="cl-empty-text">No pending reports.</p>
                ) : (
                  <ul className="cl-lab-list">
                    {labState.data.reports.slice(0, 4).map((r) => (
                      <li key={r.reportId} className="cl-lab-row">
                        <div>
                          <p className="cl-lab-patient">{r.patientDisplayName}</p>
                          <p className="cl-lab-meta">
                            {r.procedureName ?? r.procedureCode ?? '—'}
                            {r.labName ? ` · ${r.labName}` : ''}
                          </p>
                        </div>
                        <span className={`cl-badge ${r.reviewedBy ? 'cl-badge-green' : 'cl-badge-amber'}`}>
                          {r.reviewedBy ? 'reviewed' : 'pending'}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
          </section>
        </div>
      </div>
    </div>
  )
}

function greeting() {
  const h = new Date().getHours()
  if (h < 12) return 'morning'
  if (h < 17) return 'afternoon'
  return 'evening'
}
