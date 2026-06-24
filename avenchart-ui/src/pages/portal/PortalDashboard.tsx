import { useNavigate, useOutletContext, Link } from 'react-router-dom'
import { CalendarClock, Download, FolderOpen, Mail, PenLine } from 'lucide-react'
import { useState } from 'react'
import { downloadPatientPortalGeneratedMedicalReportPdf } from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'

function formatTime(value?: string | null) {
  if (!value) return ''
  return value.length >= 5 ? value.slice(0, 5) : value
}

function formatApptDate(dateStr: string) {
  const [y, m, d] = dateStr.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  return {
    month: date.toLocaleString('en-US', { month: 'short' }),
    day: date.getDate(),
    weekday: date.toLocaleString('en-US', { weekday: 'short' }),
  }
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

export default function PortalDashboard() {
  const { session, home, homeLoading } = useOutletContext<PortalOutletContext>()
  const navigate = useNavigate()
  const [reportDownloading, setReportDownloading] = useState(false)
  const [reportError, setReportError] = useState<string | null>(null)

  function handleDownloadReport() {
    setReportDownloading(true)
    setReportError(null)
    downloadPatientPortalGeneratedMedicalReportPdf(session.sessionId)
      .then((blob) => triggerBlobDownload(blob, `medical-report-${session.portalUsername}.pdf`))
      .catch((err) => setReportError(err instanceof Error ? err.message : 'Could not generate the report.'))
      .finally(() => setReportDownloading(false))
  }

  const quickActions = [
    {
      icon: Mail,
      title: 'Messages',
      desc: 'View inbox and message your care team.',
      action: () => navigate('/portal/messages'),
    },
    {
      icon: CalendarClock,
      title: 'Appointments',
      desc: 'Upcoming visits and appointment requests.',
      action: () => navigate('/portal/appointments'),
    },
    {
      icon: FolderOpen,
      title: 'Records',
      desc: 'Documents, lab results, and health summary.',
      action: () => navigate('/portal/records'),
    },
    {
      icon: Download,
      title: reportDownloading ? 'Preparing report…' : 'Download medical report',
      desc: 'Generates a PDF summary of your record.',
      action: handleDownloadReport,
      disabled: reportDownloading,
    },
  ]

  return (
    <div className="portal-page">
      {/* Quick actions */}
      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Quick actions</h2>
        </div>
        <div className="quick-action-grid">
          {quickActions.map((qa) => {
            const Icon = qa.icon
            return (
              <button
                key={qa.title}
                className="quick-action-tile"
                onClick={qa.action}
                disabled={qa.disabled}
                type="button"
              >
                <span className="quick-action-icon">
                  <Icon size={20} />
                </span>
                <span className="quick-action-title">{qa.title}</span>
                <span className="quick-action-desc">{qa.desc}</span>
              </button>
            )
          })}
        </div>
        {reportError && <div className="error-banner" style={{ marginTop: 12 }}>{reportError}</div>}
      </section>

      {/* Upcoming appointments */}
      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Upcoming appointments</h2>
          <Link to="/portal/appointments" className="portal-section-link">
            See all
          </Link>
        </div>

        {homeLoading ? (
          <div className="skeleton-list">
            {[0, 1].map((i) => <div key={i} className="skeleton-row" />)}
          </div>
        ) : !home || home.upcomingAppointments.length === 0 ? (
          <div className="empty-state">
            <CalendarClock size={32} className="empty-state-icon" />
            <p className="empty-state-text">No upcoming appointments.</p>
            <Link to="/portal/appointments" className="button-secondary" style={{ display: 'inline-flex', width: 'auto' }}>
              Request an appointment
            </Link>
          </div>
        ) : (
          <ul className="appt-list">
            {home.upcomingAppointments.slice(0, 3).map((appt) => {
              const { month, day, weekday } = formatApptDate(appt.date)
              return (
                <li key={appt.id} className="appt-card">
                  <div className="appt-date-block">
                    <p className="appt-date-month">{month}</p>
                    <p className="appt-date-day">{day}</p>
                    <p className="appt-date-weekday">{weekday}</p>
                  </div>
                  <div className="appt-body">
                    <p className="appt-title">{appt.title}</p>
                    <p className="appt-meta">
                      {formatTime(appt.startTime)}
                      {appt.providerName ? ` · ${appt.providerName}` : ''}
                      {appt.facilityName ? ` · ${appt.facilityName}` : ''}
                    </p>
                  </div>
                  {appt.status && (
                    <span className={`appt-status appt-status-${appt.status.toLowerCase().replace(/\s+/g, '-')}`}>
                      {appt.status}
                    </span>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </section>

      {/* Messages summary */}
      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Messages</h2>
          <Link to="/portal/messages" className="portal-section-link">
            View inbox
          </Link>
        </div>

        {homeLoading ? (
          <div className="skeleton-list">
            <div className="skeleton-row" />
          </div>
        ) : !home ? null : (
          <div className="message-summary-row">
            <div className="message-summary-stats">
              <div className="message-summary-stat">
                <span className="message-summary-value">{home.messages.newMessages}</span>
                <span className="message-summary-label">New</span>
              </div>
              <div className="message-summary-stat">
                <span className="message-summary-value">{home.messages.totalMessages}</span>
                <span className="message-summary-label">Total</span>
              </div>
            </div>
            {home.messages.latestMessageTitle && (
              <p className="message-summary-latest">
                Latest: "{home.messages.latestMessageTitle}"
                {home.messages.latestMessageDate ? ` · ${home.messages.latestMessageDate}` : ''}
              </p>
            )}
            <button
              className="button-secondary"
              style={{ width: 'auto' }}
              type="button"
              onClick={() => navigate('/portal/messages', { state: { compose: true } })}
            >
              <PenLine size={15} style={{ marginRight: 6 }} />
              Message your care team
            </button>
          </div>
        )}
      </section>
    </div>
  )
}
