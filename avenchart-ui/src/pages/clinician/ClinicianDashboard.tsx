import { useEffect, useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { CalendarClock, ChevronRight, FlaskConical, Users } from 'lucide-react'
import {
  getProcedureReportQueue,
  searchAppointments,
  type AppointmentListItem,
  type ProcedureReportQueueResponse,
} from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error' }

function today() {
  return new Date().toISOString().slice(0, 10)
}

function formatTime(t?: string | null) {
  if (!t) return ''
  return t.slice(0, 5)
}

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

  useEffect(() => {
    const todayStr = today()
    searchAppointments(session.sessionId, { fromDate: todayStr, toDate: todayStr, limit: 20 })
      .then((data) => setApptState({ status: 'ready', data: data.appointments }))
      .catch(() => setApptState({ status: 'error' }))

    getProcedureReportQueue(session.sessionId, { status: 'pending', limit: 5 })
      .then((data) => setLabState({ status: 'ready', data }))
      .catch(() => setLabState({ status: 'error' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const todayLabel = new Date().toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Good {greeting()}, {session.displayName.split(' ')[0]}</h1>
          <p className="clinician-page-subtitle">{todayLabel}</p>
        </div>
        <div className="clinician-header-actions">
          <button
            className="cl-btn-primary"
            type="button"
            onClick={() => navigate('/clinician/patients')}
          >
            <Users size={15} />
            Patient search
          </button>
        </div>
      </div>

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
                  <div className="cl-appt-time">{formatTime(appt.startTime)}</div>
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
                        <span className={`cl-badge ${r.reviewStatus === 'reviewed' ? 'cl-badge-green' : 'cl-badge-amber'}`}>
                          {r.reviewStatus ?? 'pending'}
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
