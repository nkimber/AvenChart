import { useEffect, useState } from 'react'
import { useNavigate, useOutletContext, Link } from 'react-router-dom'
import { CalendarClock, Download, FolderOpen, Mail, PenLine } from 'lucide-react'
import {
  downloadPatientPortalGeneratedMedicalReportPdf,
  getPatientPortalMessages,
  type PatientPortalMessagesResponse,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error' }

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

function relativeDate(dateStr: string): string {
  if (!dateStr) return dateStr
  const [datePart] = dateStr.split(' ')
  const [y, m, d] = datePart.split('-').map(Number)
  if (!y || !m || !d) return dateStr
  const msgDate = new Date(y, m - 1, d)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const diff = Math.round((today.getTime() - msgDate.getTime()) / 86400000)
  if (diff === 0) return 'Today'
  if (diff === 1) return 'Yesterday'
  if (diff < 7) return msgDate.toLocaleDateString('en-US', { weekday: 'long' })
  return msgDate.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
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
  const [messagesState, setMessagesState] = useState<AsyncState<PatientPortalMessagesResponse>>({
    status: 'idle',
  })

  // Lazy-load recent messages for the dashboard preview (#4)
  useEffect(() => {
    if (!session) return
    setMessagesState({ status: 'loading' })
    getPatientPortalMessages(session.sessionId)
      .then((data) => setMessagesState({ status: 'ready', data }))
      .catch(() => setMessagesState({ status: 'error' }))
  }, [session])

  function handleDownloadReport() {
    setReportDownloading(true)
    downloadPatientPortalGeneratedMedicalReportPdf(session.sessionId)
      .then((blob) => triggerBlobDownload(blob, `medical-report-${session.portalUsername}.pdf`))
      .catch((err) => showToast(err instanceof Error ? err.message : 'Could not generate the report.', 'error'))
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

  const recentMessages =
    messagesState.status === 'ready'
      ? messagesState.data.messages.slice(0, 3)
      : []

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
                <span className="quick-action-icon" aria-hidden="true">
                  <Icon size={20} />
                </span>
                <span className="quick-action-title">{qa.title}</span>
                <span className="quick-action-desc">{qa.desc}</span>
              </button>
            )
          })}
        </div>
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
            <CalendarClock size={32} style={{ color: 'var(--teal)', opacity: 0.6 }} />
            <p className="empty-state-text">No upcoming appointments.</p>
            {/* Pass openRequest state so PortalAppointments auto-opens the modal (#5) */}
            <Link
              to="/portal/appointments"
              state={{ openRequest: true }}
              className="button-secondary"
              style={{ display: 'inline-flex', width: 'auto' }}
            >
              Request an appointment
            </Link>
          </div>
        ) : (
          <ul className="appt-list">
            {home.upcomingAppointments.slice(0, 3).map((appt) => {
              const { month, day, weekday } = formatApptDate(appt.date)
              return (
                <li key={appt.id} className="appt-card">
                  <div className="appt-date-block" aria-label={`${month} ${day}, ${weekday}`}>
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

      {/* Messages preview — live list instead of stat counts (#4) */}
      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Recent messages</h2>
          <Link to="/portal/messages" className="portal-section-link">
            View inbox
          </Link>
        </div>

        {messagesState.status === 'loading' && (
          <div className="skeleton-list">
            {[0, 1].map((i) => <div key={i} className="skeleton-row" style={{ height: 56 }} />)}
          </div>
        )}
        {messagesState.status === 'error' && (
          <p className="muted" style={{ fontSize: 13 }}>Could not load messages.</p>
        )}
        {messagesState.status === 'ready' && recentMessages.length === 0 && (
          <div className="empty-state" style={{ padding: '16px 0' }}>
            <p className="empty-state-text">No messages yet.</p>
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
        {messagesState.status === 'ready' && recentMessages.length > 0 && (
          <>
            <ul className="message-list" role="list" style={{ marginBottom: 12 }}>
              {recentMessages.map((msg) => {
                const isUnread = msg.status === 'New'
                return (
                  <li key={msg.id}>
                    <button
                      className={`message-row${isUnread ? ' message-row-unread' : ''}`}
                      type="button"
                      onClick={() => navigate('/portal/messages')}
                      aria-label={`${isUnread ? 'Unread: ' : ''}${msg.title}`}
                    >
                      {isUnread && <span className="unread-dot" aria-hidden="true" />}
                      <div className="message-row-body">
                        <div className="message-row-top">
                          <span className="message-row-title">{msg.title}</span>
                          <time className="message-row-date" dateTime={msg.date}>
                            {relativeDate(msg.date)}
                          </time>
                        </div>
                        <p className="message-row-preview">
                          {msg.senderName} · {msg.body.slice(0, 60)}{msg.body.length > 60 ? '…' : ''}
                        </p>
                      </div>
                    </button>
                  </li>
                )
              })}
            </ul>
            <button
              className="button-secondary"
              style={{ width: 'auto' }}
              type="button"
              onClick={() => navigate('/portal/messages', { state: { compose: true } })}
            >
              <PenLine size={15} style={{ marginRight: 6 }} />
              New message
            </button>
          </>
        )}
      </section>
    </div>
  )
}
