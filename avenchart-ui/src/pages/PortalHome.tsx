import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { CalendarClock, Mail, MessageCircle } from 'lucide-react'
import { PulseBadgeIllustration } from '../illustrations.tsx'
import {
  composePatientPortalMessage,
  downloadPatientPortalDocuments,
  downloadPatientPortalGeneratedMedicalReportPdf,
  getPatientPortalAppointmentRequestOptions,
  getPatientPortalClinicalSummary,
  getPatientPortalDocuments,
  getPatientPortalHome,
  getPatientPortalLabResults,
  getPatientPortalMessages,
  getPatientPortalMessageThread,
  getPatientPortalSession,
  markPatientPortalMessageRead,
  replyToPatientPortalMessage,
  requestPatientPortalAppointment,
  type PatientPortalAppointmentRequestOptionsResponse,
  type PatientPortalClinicalSummaryResponse,
  type PatientPortalDocumentItem,
  type PatientPortalDocumentsResponse,
  type PatientPortalHomeSummaryResponse,
  type PatientPortalLabResultsResponse,
  type PatientPortalMessagesResponse,
  type PatientPortalMessageThreadResponse,
  type PatientPortalSessionResponse,
} from '../api.ts'
import { clearPortalSession, loadPortalSession } from '../auth/session.ts'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

function formatTime(value?: string | null) {
  if (!value) return ''
  return value.length >= 5 ? value.slice(0, 5) : value
}

function formatBytes(value?: number | null) {
  if (!value || value <= 0) return ''
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
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

export default function PortalHome() {
  const navigate = useNavigate()
  const [session] = useState(() => loadPortalSession())

  const [live, setLive] = useState<PatientPortalSessionResponse | null>(null)
  const [home, setHome] = useState<PatientPortalHomeSummaryResponse | null>(null)
  const [pageStatus, setPageStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [pageError, setPageError] = useState<string | null>(null)

  const [unavailableNotice, setUnavailableNotice] = useState<string | null>(null)

  const [messagesOpen, setMessagesOpen] = useState(false)
  const [messagesState, setMessagesState] = useState<AsyncState<PatientPortalMessagesResponse>>({ status: 'idle' })

  const [selectedMessageId, setSelectedMessageId] = useState<string | null>(null)
  const [threadState, setThreadState] = useState<AsyncState<PatientPortalMessageThreadResponse>>({ status: 'idle' })
  const [replyBody, setReplyBody] = useState('')
  const [replySubmitting, setReplySubmitting] = useState(false)
  const [replyResult, setReplyResult] = useState<string | null>(null)
  const [replyError, setReplyError] = useState<string | null>(null)

  const [documentsOpen, setDocumentsOpen] = useState(false)
  const [documentsState, setDocumentsState] = useState<AsyncState<PatientPortalDocumentsResponse>>({ status: 'idle' })
  const [downloadingDocId, setDownloadingDocId] = useState<number | null>(null)
  const [documentsNotice, setDocumentsNotice] = useState<string | null>(null)

  const [labResultsOpen, setLabResultsOpen] = useState(false)
  const [labResultsState, setLabResultsState] = useState<AsyncState<PatientPortalLabResultsResponse>>({
    status: 'idle',
  })

  const [clinicalSummaryOpen, setClinicalSummaryOpen] = useState(false)
  const [clinicalSummaryState, setClinicalSummaryState] = useState<
    AsyncState<PatientPortalClinicalSummaryResponse>
  >({ status: 'idle' })

  const [requestFormOpen, setRequestFormOpen] = useState(false)
  const [requestOptionsState, setRequestOptionsState] = useState<
    AsyncState<PatientPortalAppointmentRequestOptionsResponse>
  >({ status: 'idle' })
  const [requestForm, setRequestForm] = useState({
    categoryId: '',
    providerId: '',
    facilityId: '',
    date: '',
    startTime: '',
    durationMinutes: 20,
    reason: '',
  })
  const [requestSubmitting, setRequestSubmitting] = useState(false)
  const [requestResult, setRequestResult] = useState<string | null>(null)
  const [requestError, setRequestError] = useState<string | null>(null)

  const [composeOpen, setComposeOpen] = useState(false)
  const [composeTitle, setComposeTitle] = useState('')
  const [composeBody, setComposeBody] = useState('')
  const [composeSubmitting, setComposeSubmitting] = useState(false)
  const [composeResult, setComposeResult] = useState<string | null>(null)
  const [composeError, setComposeError] = useState<string | null>(null)

  const [reportDownloading, setReportDownloading] = useState(false)
  const [reportError, setReportError] = useState<string | null>(null)

  useEffect(() => {
    if (!session) {
      navigate('/portal/login')
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
          navigate('/portal/login')
          return
        }
        setLive(sessionResult)
        setHome(homeResult)
        setPageStatus('ready')
      })
      .catch((err) => {
        setPageStatus('error')
        setPageError(err instanceof Error ? err.message : 'Could not load your portal home.')
      })

    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (!session) return null

  function handleSignOut() {
    clearPortalSession()
    navigate('/portal/login')
  }

  function refreshHome() {
    if (!session) return
    getPatientPortalHome(session.sessionId)
      .then((result) => {
        if (result.authenticated) setHome(result)
      })
      .catch(() => {
        /* best-effort refresh; the page already has a usable snapshot */
      })
  }

  function showUnavailable(label: string) {
    setUnavailableNotice(
      `${label} isn't wired up yet — the modernized backend doesn't expose an API for this action yet.`,
    )
  }

  function toggleMessages() {
    setMessagesOpen((open) => {
      if (open) closeMessageThread()
      return !open
    })
    if (messagesState.status === 'idle' && session) {
      setMessagesState({ status: 'loading' })
      getPatientPortalMessages(session.sessionId)
        .then((data) => setMessagesState({ status: 'ready', data }))
        .catch((err) =>
          setMessagesState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load messages.',
          }),
        )
    }
  }

  function openMessageThread(messageId: string) {
    setSelectedMessageId(messageId)
    setReplyBody('')
    setReplyResult(null)
    setReplyError(null)
    if (!session) return
    setThreadState({ status: 'loading' })
    getPatientPortalMessageThread(session.sessionId, messageId)
      .then((data) => {
        setThreadState({ status: 'ready', data })
        markPatientPortalMessageRead(session.sessionId, messageId)
          .then(() => {
            setMessagesState({ status: 'idle' })
            refreshHome()
          })
          .catch(() => {
            /* best-effort mark-as-read; the thread is still readable */
          })
      })
      .catch((err) =>
        setThreadState({
          status: 'error',
          message: err instanceof Error ? err.message : 'Could not load this conversation.',
        }),
      )
  }

  function closeMessageThread() {
    setSelectedMessageId(null)
    setThreadState({ status: 'idle' })
    setReplyBody('')
    setReplyResult(null)
    setReplyError(null)
  }

  function submitReply(event: FormEvent) {
    event.preventDefault()
    if (!session || !selectedMessageId) return
    setReplySubmitting(true)
    setReplyError(null)
    replyToPatientPortalMessage(session.sessionId, selectedMessageId, { body: replyBody })
      .then((result) => {
        if (!result.created) {
          setReplyError(result.failureReason ?? 'The reply was not sent.')
          return
        }
        setReplyResult('Reply sent.')
        setReplyBody('')
        return getPatientPortalMessageThread(session.sessionId, selectedMessageId).then((data) =>
          setThreadState({ status: 'ready', data }),
        )
      })
      .catch((err) => setReplyError(err instanceof Error ? err.message : 'Could not send the reply.'))
      .finally(() => setReplySubmitting(false))
  }

  function toggleDocuments() {
    setDocumentsOpen((open) => !open)
    if (documentsState.status === 'idle' && session) {
      setDocumentsState({ status: 'loading' })
      getPatientPortalDocuments(session.sessionId)
        .then((data) => setDocumentsState({ status: 'ready', data }))
        .catch((err) =>
          setDocumentsState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load documents.',
          }),
        )
    }
  }

  function toggleLabResults() {
    setLabResultsOpen((open) => !open)
    if (labResultsState.status === 'idle' && session) {
      setLabResultsState({ status: 'loading' })
      getPatientPortalLabResults(session.sessionId)
        .then((data) => setLabResultsState({ status: 'ready', data }))
        .catch((err) =>
          setLabResultsState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load lab results.',
          }),
        )
    }
  }

  function toggleClinicalSummary() {
    setClinicalSummaryOpen((open) => !open)
    if (clinicalSummaryState.status === 'idle' && session) {
      setClinicalSummaryState({ status: 'loading' })
      getPatientPortalClinicalSummary(session.sessionId)
        .then((data) => setClinicalSummaryState({ status: 'ready', data }))
        .catch((err) =>
          setClinicalSummaryState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load your health summary.',
          }),
        )
    }
  }

  function toggleRequestForm() {
    setRequestFormOpen((open) => !open)
    setRequestResult(null)
    setRequestError(null)
    if (requestOptionsState.status === 'idle' && session) {
      setRequestOptionsState({ status: 'loading' })
      getPatientPortalAppointmentRequestOptions(session.sessionId)
        .then((data) => {
          setRequestOptionsState({ status: 'ready', data })
          setRequestForm({
            categoryId: data.defaults.categoryId != null ? String(data.defaults.categoryId) : '',
            providerId: data.defaults.providerId != null ? String(data.defaults.providerId) : '',
            facilityId: data.defaults.facilityId != null ? String(data.defaults.facilityId) : '',
            date: data.defaults.date,
            startTime: formatTime(data.defaults.startTime),
            durationMinutes: data.defaults.durationMinutes,
            reason: '',
          })
        })
        .catch((err) =>
          setRequestOptionsState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load appointment options.',
          }),
        )
    }
  }

  function submitRequestForm(event: FormEvent) {
    event.preventDefault()
    if (!session) return
    setRequestSubmitting(true)
    setRequestError(null)
    requestPatientPortalAppointment(session.sessionId, {
      categoryId: requestForm.categoryId ? Number(requestForm.categoryId) : undefined,
      providerId: requestForm.providerId ? Number(requestForm.providerId) : undefined,
      facilityId: requestForm.facilityId ? Number(requestForm.facilityId) : undefined,
      date: requestForm.date,
      startTime: requestForm.startTime,
      durationMinutes: requestForm.durationMinutes,
      reason: requestForm.reason || undefined,
    })
      .then((result) => {
        if (!result.created || !result.appointment) {
          setRequestError(result.failureReason ?? 'The appointment request was not accepted.')
          return
        }
        setRequestResult(
          `Requested: ${result.appointment.title} on ${result.appointment.date} at ${formatTime(result.appointment.startTime)}.`,
        )
        refreshHome()
      })
      .catch((err) => setRequestError(err instanceof Error ? err.message : 'Could not submit the request.'))
      .finally(() => setRequestSubmitting(false))
  }

  function toggleCompose() {
    setComposeOpen((open) => !open)
    setComposeResult(null)
    setComposeError(null)
  }

  function submitCompose(event: FormEvent) {
    event.preventDefault()
    if (!session) return
    setComposeSubmitting(true)
    setComposeError(null)
    composePatientPortalMessage(session.sessionId, { title: composeTitle, body: composeBody })
      .then((result) => {
        if (!result.created) {
          setComposeError(result.failureReason ?? 'The message was not sent.')
          return
        }
        setComposeResult(`Sent to ${result.recipientName}.`)
        setComposeTitle('')
        setComposeBody('')
        setMessagesState({ status: 'idle' })
        if (messagesOpen) toggleMessages()
        refreshHome()
      })
      .catch((err) => setComposeError(err instanceof Error ? err.message : 'Could not send the message.'))
      .finally(() => setComposeSubmitting(false))
  }

  function handleDownloadReport() {
    if (!session) return
    setReportDownloading(true)
    setReportError(null)
    downloadPatientPortalGeneratedMedicalReportPdf(session.sessionId)
      .then((blob) => triggerBlobDownload(blob, `medical-report-${session.portalUsername}.pdf`))
      .catch((err) => setReportError(err instanceof Error ? err.message : 'Could not generate the report.'))
      .finally(() => setReportDownloading(false))
  }

  function handleDownloadDocument(doc: PatientPortalDocumentItem) {
    if (!session) return
    setDocumentsNotice(null)
    setDownloadingDocId(doc.id)
    downloadPatientPortalDocuments(session.sessionId, { documentIds: [doc.id] })
      .then((blob) => triggerBlobDownload(blob, doc.name))
      .catch((err) =>
        setDocumentsNotice(err instanceof Error ? err.message : 'Could not download that document.'),
      )
      .finally(() => setDownloadingDocId(null))
  }

  return (
    <div className="shell-top">
      <div className="dashboard">
        <div className="dashboard-hero">
          <div className="dashboard-hero-illustration">
            <PulseBadgeIllustration />
          </div>
          <div className="dashboard-hero-row">
            <div className="row">
              <div className="avatar avatar-on-dark">{initials(session.displayName)}</div>
              <div>
                <p className="dashboard-hero-greeting">Hello, {session.displayName.split(' ')[0]}</p>
                <p className="dashboard-hero-sub">Patient portal</p>
              </div>
            </div>
            <button className="link-button-on-dark" onClick={handleSignOut}>
              Sign out
            </button>
          </div>

          {pageStatus === 'loading' && <p className="dashboard-hero-sub">Loading your portal home…</p>}
          {pageStatus === 'error' && <div className="error-banner">{pageError}</div>}

          {pageStatus === 'ready' && home && (
            <div className="hero-stat-row">
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><CalendarClock size={16} /></span>
                <div>
                  <p className="hero-stat-value">{home.upcomingAppointmentCount}</p>
                  <p className="hero-stat-label">Upcoming appointments</p>
                </div>
              </div>
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><Mail size={16} /></span>
                <div>
                  <p className="hero-stat-value">{home.messages.newMessages}</p>
                  <p className="hero-stat-label">New messages</p>
                </div>
              </div>
              <div className="hero-stat-chip">
                <span className="hero-stat-icon"><MessageCircle size={16} /></span>
                <div>
                  <p className="hero-stat-value">{home.messages.totalMessages}</p>
                  <p className="hero-stat-label">Total messages</p>
                </div>
              </div>
            </div>
          )}
        </div>

        {unavailableNotice && (
          <div className="panel">
            <div className="error-banner" style={{ marginBottom: 0 }}>
              {unavailableNotice}
            </div>
          </div>
        )}

        {pageStatus === 'ready' && home && (
          <>
            <div className="panel">
              <div className="section-header">
                <p className="section-title">Upcoming appointments</p>
                <button className="link-button" onClick={toggleRequestForm}>
                  {requestFormOpen ? 'Close' : 'Request an appointment'}
                </button>
              </div>

              {home.upcomingAppointments.length === 0 ? (
                <p className="muted empty-row">No upcoming appointments on file.</p>
              ) : (
                <ul className="panel-list">
                  {home.upcomingAppointments.map((appt) => (
                    <li className="panel-row" key={appt.id}>
                      <div>
                        <p className="panel-row-title">{appt.title}</p>
                        <p className="panel-row-meta">
                          {appt.date} at {formatTime(appt.startTime)}
                          {appt.providerName ? ` · ${appt.providerName}` : ''}
                          {appt.facilityName ? ` · ${appt.facilityName}` : ''}
                        </p>
                      </div>
                      {appt.status && <span className="badge-new">{appt.status}</span>}
                    </li>
                  ))}
                </ul>
              )}

              {requestFormOpen && (
                <div className="inline-panel">
                  {requestOptionsState.status === 'loading' && <p className="muted">Loading appointment options…</p>}
                  {requestOptionsState.status === 'error' && (
                    <div className="error-banner">{requestOptionsState.message}</div>
                  )}

                  {requestOptionsState.status === 'ready' && (
                    <form onSubmit={submitRequestForm}>
                      <div className="form-row">
                        <div className="field">
                          <label className="label" htmlFor="appt-category">
                            Visit type
                          </label>
                          <select
                            id="appt-category"
                            className="select"
                            value={requestForm.categoryId}
                            onChange={(e) => setRequestForm((f) => ({ ...f, categoryId: e.target.value }))}
                          >
                            {requestOptionsState.data.categories.map((c) => (
                              <option key={c.id} value={c.id}>
                                {c.name}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div className="field">
                          <label className="label" htmlFor="appt-provider">
                            Provider
                          </label>
                          <select
                            id="appt-provider"
                            className="select"
                            value={requestForm.providerId}
                            onChange={(e) => setRequestForm((f) => ({ ...f, providerId: e.target.value }))}
                          >
                            {requestOptionsState.data.providers.map((p) => (
                              <option key={p.id} value={p.id}>
                                {p.displayName}
                              </option>
                            ))}
                          </select>
                        </div>
                      </div>

                      <div className="form-row">
                        <div className="field">
                          <label className="label" htmlFor="appt-date">
                            Date
                          </label>
                          <input
                            id="appt-date"
                            type="date"
                            className="input"
                            value={requestForm.date}
                            onChange={(e) => setRequestForm((f) => ({ ...f, date: e.target.value }))}
                            required
                          />
                        </div>
                        <div className="field">
                          <label className="label" htmlFor="appt-time">
                            Time
                          </label>
                          <input
                            id="appt-time"
                            type="time"
                            className="input"
                            value={requestForm.startTime}
                            onChange={(e) => setRequestForm((f) => ({ ...f, startTime: e.target.value }))}
                            required
                          />
                        </div>
                      </div>

                      <div className="field">
                        <label className="label" htmlFor="appt-reason">
                          Reason for visit (optional)
                        </label>
                        <textarea
                          id="appt-reason"
                          className="textarea"
                          value={requestForm.reason}
                          onChange={(e) => setRequestForm((f) => ({ ...f, reason: e.target.value }))}
                        />
                      </div>

                      {requestError && <div className="error-banner">{requestError}</div>}
                      {requestResult && <div className="hint-banner">{requestResult}</div>}

                      <button className="button-primary" type="submit" disabled={requestSubmitting}>
                        {requestSubmitting ? 'Sending request…' : 'Send request'}
                      </button>
                    </form>
                  )}
                </div>
              )}
            </div>

            <div className="panel">
              <div className="section-header">
                <p className="section-title">Messages</p>
                <button className="link-button" onClick={toggleMessages}>
                  {messagesOpen ? 'Close inbox' : 'View inbox'}
                </button>
              </div>

              {home.messages.latestMessageTitle ? (
                <p className="muted">
                  Latest: "{home.messages.latestMessageTitle}"
                  {home.messages.latestMessageDate ? ` · ${home.messages.latestMessageDate}` : ''}
                </p>
              ) : (
                <p className="muted">No messages yet.</p>
              )}

              <div style={{ marginTop: 14 }}>
                <button className="button-secondary" type="button" onClick={toggleCompose} style={{ width: 'auto' }}>
                  {composeOpen ? 'Close' : 'Message your care team'}
                </button>
              </div>

              {composeOpen && (
                <div className="inline-panel">
                  <form onSubmit={submitCompose}>
                    <div className="field">
                      <label className="label" htmlFor="msg-title">
                        Subject
                      </label>
                      <input
                        id="msg-title"
                        className="input"
                        value={composeTitle}
                        onChange={(e) => setComposeTitle(e.target.value)}
                        required
                      />
                    </div>
                    <div className="field">
                      <label className="label" htmlFor="msg-body">
                        Message
                      </label>
                      <textarea
                        id="msg-body"
                        className="textarea"
                        value={composeBody}
                        onChange={(e) => setComposeBody(e.target.value)}
                        required
                      />
                    </div>
                    {composeError && <div className="error-banner">{composeError}</div>}
                    {composeResult && <div className="hint-banner">{composeResult}</div>}
                    <button className="button-primary" type="submit" disabled={composeSubmitting}>
                      {composeSubmitting ? 'Sending…' : 'Send message'}
                    </button>
                  </form>
                </div>
              )}

              {messagesOpen && !selectedMessageId && (
                <div className="inline-panel">
                  {messagesState.status === 'loading' && <p className="muted">Loading inbox…</p>}
                  {messagesState.status === 'error' && <div className="error-banner">{messagesState.message}</div>}
                  {messagesState.status === 'ready' &&
                    (messagesState.data.messages.length === 0 ? (
                      <p className="muted empty-row">Your inbox is empty.</p>
                    ) : (
                      <ul className="panel-list">
                        {messagesState.data.messages.map((msg) => (
                          <li className="panel-row" key={msg.id}>
                            <button
                              className="link-button"
                              type="button"
                              onClick={() => openMessageThread(msg.id)}
                              style={{ textAlign: 'left', padding: 0 }}
                            >
                              <span className="panel-row-title" style={{ display: 'block' }}>
                                {msg.title}
                              </span>
                              <span className="panel-row-meta" style={{ display: 'block' }}>
                                {msg.senderName} · {msg.date}
                              </span>
                            </button>
                            <span className="badge-new">{msg.status}</span>
                          </li>
                        ))}
                      </ul>
                    ))}
                </div>
              )}

              {messagesOpen && selectedMessageId && (
                <div className="inline-panel">
                  <div className="section-header" style={{ marginBottom: 8 }}>
                    <p className="section-title" style={{ fontSize: 13 }}>
                      Conversation
                    </p>
                    <button className="link-button" type="button" onClick={closeMessageThread}>
                      Back to inbox
                    </button>
                  </div>

                  {threadState.status === 'loading' && <p className="muted">Loading conversation…</p>}
                  {threadState.status === 'error' && <div className="error-banner">{threadState.message}</div>}
                  {threadState.status === 'ready' && (
                    <>
                      <ul className="panel-list">
                        {threadState.data.threadMessages.map((m) => (
                          <li className="panel-row" key={m.id}>
                            <div>
                              <p className="panel-row-title">
                                {m.senderName} · {m.date}
                              </p>
                              <p className="panel-row-meta" style={{ whiteSpace: 'pre-wrap' }}>
                                {m.body}
                              </p>
                            </div>
                          </li>
                        ))}
                      </ul>

                      <form onSubmit={submitReply} style={{ marginTop: 14 }}>
                        <div className="field">
                          <label className="label" htmlFor="reply-body">
                            Reply
                          </label>
                          <textarea
                            id="reply-body"
                            className="textarea"
                            value={replyBody}
                            onChange={(e) => setReplyBody(e.target.value)}
                            required
                          />
                        </div>
                        {replyError && <div className="error-banner">{replyError}</div>}
                        {replyResult && <div className="hint-banner">{replyResult}</div>}
                        <button className="button-primary" type="submit" disabled={replySubmitting}>
                          {replySubmitting ? 'Sending reply…' : 'Send reply'}
                        </button>
                      </form>
                    </>
                  )}
                </div>
              )}
            </div>

            <div className="panel">
              <div className="section-header">
                <p className="section-title">Records &amp; documents</p>
              </div>

              <div className="action-grid">
                <button className="action-tile" type="button" onClick={toggleDocuments}>
                  <span className="action-tile-title">{documentsOpen ? 'Hide documents' : 'View documents'}</span>
                  <span className="action-tile-desc">Records uploaded to your chart.</span>
                </button>

                <button className="action-tile" type="button" onClick={toggleLabResults}>
                  <span className="action-tile-title">
                    {labResultsOpen ? 'Hide lab results' : 'View lab results'}
                  </span>
                  <span className="action-tile-desc">Orders, reports, and result values.</span>
                </button>

                <button className="action-tile" type="button" onClick={toggleClinicalSummary}>
                  <span className="action-tile-title">
                    {clinicalSummaryOpen ? 'Hide health summary' : 'View health summary'}
                  </span>
                  <span className="action-tile-desc">Problems, allergies, medications, and prescriptions.</span>
                </button>

                <button
                  className="action-tile"
                  type="button"
                  onClick={handleDownloadReport}
                  disabled={reportDownloading}
                >
                  <span className="action-tile-title">
                    {reportDownloading ? 'Preparing report…' : 'Download medical report'}
                  </span>
                  <span className="action-tile-desc">Generates a PDF summary of your record.</span>
                </button>

                <button className="action-tile" type="button" onClick={() => showUnavailable('Paying a bill')}>
                  <span className="action-tile-title">Pay a bill</span>
                  <span className="action-tile-desc">Billing balance and online payment.</span>
                  <span className="badge-unavailable">API not available yet</span>
                </button>

                <button
                  className="action-tile"
                  type="button"
                  onClick={() => showUnavailable('Requesting a prescription refill')}
                >
                  <span className="action-tile-title">Request a refill</span>
                  <span className="action-tile-desc">Ask your provider to renew a prescription.</span>
                  <span className="badge-unavailable">API not available yet</span>
                </button>
              </div>

              {reportError && (
                <div className="error-banner" style={{ marginTop: 14 }}>
                  {reportError}
                </div>
              )}

              {documentsOpen && (
                <div className="inline-panel">
                  {documentsState.status === 'loading' && <p className="muted">Loading documents…</p>}
                  {documentsState.status === 'error' && <div className="error-banner">{documentsState.message}</div>}
                  {documentsNotice && <div className="error-banner">{documentsNotice}</div>}
                  {documentsState.status === 'ready' &&
                    (documentsState.data.documents.length === 0 ? (
                      <p className="muted empty-row">No documents on file.</p>
                    ) : (
                      <ul className="panel-list">
                        {documentsState.data.documents.map((doc) => (
                          <li className="panel-row" key={doc.id}>
                            <div>
                              <p className="panel-row-title">{doc.name}</p>
                              <p className="panel-row-meta">
                                {doc.categoryName} · {doc.docDate}
                                {formatBytes(doc.sizeBytes) ? ` · ${formatBytes(doc.sizeBytes)}` : ''}
                              </p>
                            </div>
                            {doc.canDownload ? (
                              <button
                                className="link-button"
                                onClick={() => handleDownloadDocument(doc)}
                                disabled={downloadingDocId === doc.id}
                              >
                                {downloadingDocId === doc.id ? 'Downloading…' : 'Download'}
                              </button>
                            ) : (
                              <span className="muted">Unavailable</span>
                            )}
                          </li>
                        ))}
                      </ul>
                    ))}
                </div>
              )}

              {labResultsOpen && (
                <div className="inline-panel">
                  {labResultsState.status === 'loading' && <p className="muted">Loading lab results…</p>}
                  {labResultsState.status === 'error' && (
                    <div className="error-banner">{labResultsState.message}</div>
                  )}
                  {labResultsState.status === 'ready' &&
                    (labResultsState.data.orders.length === 0 ? (
                      <p className="muted empty-row">No lab orders on file.</p>
                    ) : (
                      <ul className="panel-list">
                        {labResultsState.data.orders.map((order) => (
                          <li className="panel-row" key={order.id}>
                            <div>
                              <p className="panel-row-title">{order.procedureName}</p>
                              <p className="panel-row-meta">
                                Ordered {order.orderDate}
                                {order.orderStatus ? ` · ${order.orderStatus}` : ''} · {order.resultCount} result
                                {order.resultCount === 1 ? '' : 's'}
                              </p>
                            </div>
                          </li>
                        ))}
                      </ul>
                    ))}
                </div>
              )}

              {clinicalSummaryOpen && (
                <div className="inline-panel">
                  {clinicalSummaryState.status === 'loading' && (
                    <p className="muted">Loading your health summary…</p>
                  )}
                  {clinicalSummaryState.status === 'error' && (
                    <div className="error-banner">{clinicalSummaryState.message}</div>
                  )}
                  {clinicalSummaryState.status === 'ready' && (
                    <>
                      <p className="section-title" style={{ fontSize: 13 }}>
                        Problems ({clinicalSummaryState.data.problemCount})
                      </p>
                      {clinicalSummaryState.data.problems.length === 0 ? (
                        <p className="muted empty-row">No problems on file.</p>
                      ) : (
                        <ul className="panel-list">
                          {clinicalSummaryState.data.problems.map((problem) => (
                            <li className="panel-row" key={problem.id}>
                              <div>
                                <p className="panel-row-title">{problem.title}</p>
                                <p className="panel-row-meta">
                                  {problem.startDate ? `Since ${problem.startDate}` : ''}
                                  {problem.reportedDate ? ` · Reported ${problem.reportedDate}` : ''}
                                  {problem.endDate ? ` · Resolved ${problem.endDate}` : ''}
                                </p>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}

                      <p className="section-title" style={{ fontSize: 13, marginTop: 18 }}>
                        Allergies ({clinicalSummaryState.data.allergyCount})
                      </p>
                      {clinicalSummaryState.data.allergies.length === 0 ? (
                        <p className="muted empty-row">No known allergies on file.</p>
                      ) : (
                        <ul className="panel-list">
                          {clinicalSummaryState.data.allergies.map((allergy) => (
                            <li className="panel-row" key={allergy.id}>
                              <div>
                                <p className="panel-row-title">{allergy.title}</p>
                                <p className="panel-row-meta">
                                  {allergy.reaction ? allergy.reaction : 'Reaction not noted'}
                                  {allergy.severity ? ` · ${allergy.severity}` : ''}
                                </p>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}

                      <p className="section-title" style={{ fontSize: 13, marginTop: 18 }}>
                        Medications ({clinicalSummaryState.data.medicationCount})
                      </p>
                      {clinicalSummaryState.data.medications.length === 0 ? (
                        <p className="muted empty-row">No medications on file.</p>
                      ) : (
                        <ul className="panel-list">
                          {clinicalSummaryState.data.medications.map((med) => (
                            <li className="panel-row" key={med.id}>
                              <div>
                                <p className="panel-row-title">{med.title}</p>
                                <p className="panel-row-meta">
                                  {med.startDate ? `Started ${med.startDate}` : ''}
                                  {med.endDate ? ` · Ended ${med.endDate}` : ''}
                                </p>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}

                      <p className="section-title" style={{ fontSize: 13, marginTop: 18 }}>
                        Prescriptions ({clinicalSummaryState.data.prescriptionCount})
                      </p>
                      {clinicalSummaryState.data.prescriptions.length === 0 ? (
                        <p className="muted empty-row">No active prescriptions on file.</p>
                      ) : (
                        <ul className="panel-list">
                          {clinicalSummaryState.data.prescriptions.map((rx) => (
                            <li className="panel-row" key={rx.id}>
                              <div>
                                <p className="panel-row-title">{rx.drug}</p>
                                <p className="panel-row-meta">
                                  {rx.dosage ? rx.dosage : ''}
                                  {rx.quantity ? ` · Qty ${rx.quantity}` : ''}
                                  {rx.route ? ` · ${rx.route}` : ''}
                                </p>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}
                    </>
                  )}
                </div>
              )}
            </div>

            <div className="panel">
              <div className="section-header">
                <p className="section-title">Account</p>
              </div>
              <ul className="fact-list" style={{ marginBottom: 0 }}>
                <li className="fact-row">
                  <span>Portal username</span>
                  <span>{session.portalUsername}</span>
                </li>
                <li className="fact-row">
                  <span>Session source</span>
                  <span>{live?.sessionSource ?? home.sessionSource}</span>
                </li>
                <li className="fact-row">
                  <span>Last seen</span>
                  <span>{live?.lastSeenAt ?? 'just now'}</span>
                </li>
              </ul>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
