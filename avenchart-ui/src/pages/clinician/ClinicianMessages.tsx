import { useEffect, useState, type FormEvent } from 'react'
import { Link, useOutletContext, useSearchParams } from 'react-router-dom'
import { ChevronLeft, ChevronRight, Download, Forward, Mail, Paperclip, RefreshCw, Send } from 'lucide-react'
import {
  createPatientMessage,
  downloadStaffMessageAttachment,
  forwardPatientMessage,
  getPatientMessageAssignmentHistory,
  getPatientMessageAssignees,
  getPatientMessages,
  getStaffMessageAttachments,
  getStaffMessageInbox,
  isRequestCancellation,
  replyToPatientMessage,
  uploadStaffMessageAttachment,
  updatePatientMessageAssignment,
  type ClinicalWorkflowAssignee,
  type PatientMessageAssignmentHistoryResponse,
  type PatientMessageItem,
  type StaffMessageAttachmentItem,
  type StaffMessageInboxQuery,
  type StaffMessageInboxResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type ThreadPatient = {
  canonicalId: string
  displayName: string
  pubpid?: string
}

type PatientThread = {
  patient: ThreadPatient
  messages: PatientMessageItem[]
}

type ThreadState =
  | { status: 'idle' }
  | { status: 'loading'; patient: ThreadPatient }
  | { status: 'ready'; thread: PatientThread }
  | { status: 'error'; patient: ThreadPatient; message: string }

type FilterDraft = {
  status: string
  assignment: 'all' | 'mine' | 'unassigned'
  patient: string
  subject: string
  priority: 'all' | 'normal' | 'urgent'
  owner: string
  minimumAgeDays: string
}

const PAGE_SIZE = 20

function filtersFromParams(params: URLSearchParams): FilterDraft {
  const assignment = params.get('assignment')
  const priority = params.get('priority')
  return {
    status: params.get('status') ?? '',
    assignment: assignment === 'mine' || assignment === 'unassigned' ? assignment : 'all',
    patient: params.get('patient') ?? '',
    subject: params.get('subject') ?? '',
    priority: priority === 'normal' || priority === 'urgent' ? priority : 'all',
    owner: params.get('owner') ?? '',
    minimumAgeDays: params.get('minimumAgeDays') ?? '',
  }
}

function queryFromParams(params: URLSearchParams): StaffMessageInboxQuery {
  const filters = filtersFromParams(params)
  return {
    ...filters,
    minimumAgeDays: filters.minimumAgeDays ? Number(filters.minimumAgeDays) : undefined,
    offset: Math.max(0, Number(params.get('offset') ?? 0) || 0),
    limit: PAGE_SIZE,
  }
}

function messageStatusClass(status?: string | null) {
  const normalized = status?.trim().toLowerCase()
  if (normalized === 'new') return 'cl-badge-amber'
  if (normalized === 'done' || normalized === 'closed') return 'cl-badge-green'
  return 'cl-badge-muted'
}

function initials(name: string) {
  return name
    .replace(/\([^)]*\)/g, '')
    .split(/[\s,]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

export default function ClinicianMessages() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryKey = searchParams.toString()
  const [draft, setDraft] = useState<FilterDraft>(() => filtersFromParams(searchParams))
  const [inbox, setInbox] = useState<StaffMessageInboxResponse | null>(null)
  const [inboxLoading, setInboxLoading] = useState(true)
  const [inboxError, setInboxError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [threadState, setThreadState] = useState<ThreadState>({ status: 'idle' })
  const [replyBody, setReplyBody] = useState('')
  const [composeMode, setComposeMode] = useState(false)
  const [composeTo, setComposeTo] = useState('')
  const [composeSubject, setComposeSubject] = useState('')
  const [composeBody, setComposeBody] = useState('')
  const [sending, setSending] = useState(false)
  const [activeMessageId, setActiveMessageId] = useState<string | null>(null)
  const [assigningId, setAssigningId] = useState<string | null>(null)
  const [assignmentError, setAssignmentError] = useState<{ id: string; message: string } | null>(null)
  const [assignees, setAssignees] = useState<ClinicalWorkflowAssignee[]>([])
  const [assigneeError, setAssigneeError] = useState<string | null>(null)
  const [assignmentDrafts, setAssignmentDrafts] = useState<Record<string, { assignedTo: string; reason: string }>>({})
  const [assignmentHistory, setAssignmentHistory] = useState<Record<string, PatientMessageAssignmentHistoryResponse>>({})
  const [historyLoadingId, setHistoryLoadingId] = useState<string | null>(null)
  const [forwardingId, setForwardingId] = useState<string | null>(null)
  const [forwardError, setForwardError] = useState<{ id: string; message: string } | null>(null)
  const [forwardDrafts, setForwardDrafts] = useState<Record<string, { assignedTo: string; note: string }>>({})
  const [forwardingOpenId, setForwardingOpenId] = useState<string | null>(null)
  const [attachments, setAttachments] = useState<Record<string, StaffMessageAttachmentItem[]>>({})
  const [attachmentFiles, setAttachmentFiles] = useState<Record<string, File | null>>({})
  const [attachmentBusyId, setAttachmentBusyId] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setInboxLoading(true)
    setInboxError(null)
    getStaffMessageInbox(
      session.sessionId,
      queryFromParams(new URLSearchParams(queryKey)),
      controller.signal,
    )
      .then((result) => {
        setInbox(result)
        setLastUpdated(new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }))
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setInboxError(error instanceof Error ? error.message : 'Could not load the message inbox.')
      })
      .finally(() => setInboxLoading(false))
    return () => controller.abort()
  }, [queryKey, reload, session.sessionId])

  useEffect(() => {
    const controller = new AbortController()
    setAssigneeError(null)
    getPatientMessageAssignees(session.sessionId, controller.signal)
      .then((result) => setAssignees(result.assignees))
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setAssigneeError(error instanceof Error ? error.message : 'Could not load active staff for assignment.')
      })
    return () => controller.abort()
  }, [session.sessionId])

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    const params = new URLSearchParams()
    Object.entries(draft).forEach(([key, value]) => {
      if (!value || value === 'all') return
      params.set(key, value)
    })
    setSearchParams(params)
  }

  function setQuickFilter(key: 'status' | 'assignment', value: string) {
    const params = new URLSearchParams(searchParams)
    if (!value || value === 'all') params.delete(key)
    else params.set(key, value)
    params.delete('offset')
    setDraft(filtersFromParams(params))
    setSearchParams(params)
  }

  function changePage(nextOffset: number) {
    const params = new URLSearchParams(searchParams)
    if (nextOffset <= 0) params.delete('offset')
    else params.set('offset', String(nextOffset))
    setSearchParams(params)
  }

  function openThread(patient: ThreadPatient) {
    setThreadState({ status: 'loading', patient })
    setReplyBody('')
    setActiveMessageId(null)
    setAssignmentError(null)
    setForwardError(null)
    getPatientMessages(session.sessionId, patient.canonicalId)
      .then((data) => setThreadState({
        status: 'ready',
        thread: {
          patient: { ...patient, displayName: data.patientDisplayName },
          messages: data.messages.filter((message) => !message.deleted),
        },
      }))
      .catch((error: unknown) => setThreadState({
        status: 'error',
        patient,
        message: error instanceof Error ? error.message : 'Could not load messages.',
      }))
  }

  async function loadAttachments(messageId: string) {
    const result = await getStaffMessageAttachments(session.sessionId, messageId)
    setAttachments((current) => ({ ...current, [messageId]: result }))
  }

  async function uploadAttachment(messageId: string) {
    const file = attachmentFiles[messageId]
    if (!file) return
    const allowed = new Set(['application/pdf', 'image/png', 'image/jpeg', 'text/plain'])
    if (!allowed.has(file.type) || file.size === 0 || file.size > 4 * 1024 * 1024) {
      showToast('Choose a PDF, PNG, JPEG, or text file from 1 byte to 4 MiB.', 'error'); return
    }
    setAttachmentBusyId(messageId)
    try {
      const base64 = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(',')[1] ?? ''); reader.onerror = reject; reader.readAsDataURL(file) })
      await uploadStaffMessageAttachment(session.sessionId, messageId, { fileName: file.name, contentType: file.type, contentBase64: base64 })
      setAttachmentFiles((current) => ({ ...current, [messageId]: null }))
      await loadAttachments(messageId)
      showToast('Attachment added.', 'success')
    } catch (error) { showToast(error instanceof Error ? error.message : 'Could not add attachment.', 'error') } finally { setAttachmentBusyId(null) }
  }

  async function downloadAttachment(messageId: string, attachment: StaffMessageAttachmentItem) {
    try { const blob = await downloadStaffMessageAttachment(session.sessionId, messageId, attachment.id); const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = attachment.fileName; link.click(); URL.revokeObjectURL(url) }
    catch (error) { showToast(error instanceof Error ? error.message : 'Could not download attachment.', 'error') }
  }

  async function handleReply(messageId: string) {
    if (!replyBody.trim()) return
    setSending(true)
    try {
      const updated = await replyToPatientMessage(session.sessionId, messageId, {
        body: replyBody.trim(),
        assignedTo: session.username,
      })
      setThreadState((previous) =>
        previous.status === 'ready'
          ? {
              ...previous,
              thread: {
                ...previous.thread,
                messages: updated.messages.filter((message) => !message.deleted),
              },
            }
          : previous,
      )
      setReplyBody('')
      setActiveMessageId(null)
      setReload((value) => value + 1)
      showToast('Reply recorded.', 'success')
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Reply failed.', 'error')
    } finally {
      setSending(false)
    }
  }

  async function handleClaim(messageId: string) {
    const message = threadState.status === 'ready'
      ? threadState.thread.messages.find((item) => item.id === messageId)
      : undefined
    if (!message) return
    await handleAssignment(message, session.username, '')
  }

  async function handleAssignment(message: PatientMessageItem, requestedAssignee?: string, requestedReason?: string) {
    const draft = assignmentDrafts[message.id]
    const assignedTo = requestedAssignee ?? draft?.assignedTo ?? message.assignedTo ?? ''
    const reason = requestedReason ?? draft?.reason ?? ''
    setAssigningId(message.id)
    setAssignmentError(null)
    try {
      const updated = await updatePatientMessageAssignment(
        session.sessionId,
        message.id,
        {
          assignedTo: assignedTo || null,
          expectedVersion: message.assignmentVersion,
          reason: reason.trim() || null,
        },
      )
      setThreadState((previous) =>
        previous.status === 'ready'
          ? {
              ...previous,
              thread: {
                ...previous.thread,
                messages: updated.messages.filter((message) => !message.deleted),
              },
            }
          : previous,
      )
      setAssignmentDrafts((current) => {
        const next = { ...current }
        delete next[message.id]
        return next
      })
      setAssignmentHistory((current) => {
        const next = { ...current }
        delete next[message.id]
        return next
      })
      setReload((value) => value + 1)
      showToast(
        assignedTo
          ? `Message assigned to ${assignedTo}.`
          : 'Message assignment removed.',
        'success',
      )
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Could not update the assignment.'
      setAssignmentError({ id: message.id, message: errorMessage })
      showToast(errorMessage, 'error')
    } finally {
      setAssigningId(null)
    }
  }

  function updateForwardDraft(messageId: string, field: 'assignedTo' | 'note', value: string) {
    setForwardDrafts((current) => ({
      ...current,
      [messageId]: {
        assignedTo: current[messageId]?.assignedTo ?? '',
        note: current[messageId]?.note ?? '',
        [field]: value,
      },
    }))
  }

  async function handleForward(message: PatientMessageItem) {
    const draft = forwardDrafts[message.id]
    const assignedTo = draft?.assignedTo ?? ''
    if (!assignedTo) return
    setForwardingId(message.id)
    setForwardError(null)
    try {
      const updated = await forwardPatientMessage(session.sessionId, message.id, {
        assignedTo,
        expectedVersion: message.assignmentVersion,
        note: draft?.note.trim() || null,
      })
      setThreadState((previous) => previous.status === 'ready'
        ? { ...previous, thread: { ...previous.thread, messages: updated.messages.filter((item) => !item.deleted) } }
        : previous)
      setForwardDrafts((current) => {
        const next = { ...current }
        delete next[message.id]
        return next
      })
      setAssignmentHistory((current) => {
        const next = { ...current }
        delete next[message.id]
        return next
      })
      setForwardingOpenId(null)
      setReload((value) => value + 1)
      showToast(`Message forwarded to ${assignedTo}.`, 'success')
    } catch (error) {
      const messageText = error instanceof Error ? error.message : 'Could not forward the message.'
      setForwardError({ id: message.id, message: messageText })
      showToast(messageText, 'error')
    } finally {
      setForwardingId(null)
    }
  }

  function updateAssignmentDraft(messageId: string, field: 'assignedTo' | 'reason', value: string) {
    setAssignmentDrafts((current) => ({
      ...current,
      [messageId]: {
        assignedTo: current[messageId]?.assignedTo ?? '',
        reason: current[messageId]?.reason ?? '',
        [field]: value,
      },
    }))
  }

  async function toggleAssignmentHistory(messageId: string) {
    if (assignmentHistory[messageId]) {
      setAssignmentHistory((current) => {
        const next = { ...current }
        delete next[messageId]
        return next
      })
      return
    }

    setHistoryLoadingId(messageId)
    try {
      const history = await getPatientMessageAssignmentHistory(session.sessionId, messageId)
      setAssignmentHistory((current) => ({ ...current, [messageId]: history }))
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not load assignment history.', 'error')
    } finally {
      setHistoryLoadingId(null)
    }
  }

  async function handleCompose() {
    if (!composeTo || !composeSubject || !composeBody) return
    setSending(true)
    try {
      await createPatientMessage(session.sessionId, {
        patientId: composeTo,
        title: composeSubject,
        body: composeBody,
        assignedTo: session.username,
      })
      showToast('Message created.', 'success')
      setComposeMode(false)
      setComposeSubject('')
      setComposeBody('')
      setComposeTo('')
      setReload((value) => value + 1)
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not create message.', 'error')
    } finally {
      setSending(false)
    }
  }

  const currentOffset = inbox?.offset ?? (Number(searchParams.get('offset') ?? 0) || 0)
  const hasNextPage = inbox ? currentOffset + inbox.items.length < inbox.total : false

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Message inbox</h1>
          <p className="clinician-page-subtitle">
            Filter and open patient messages without finding the patient first.
            {lastUpdated ? ` Last updated ${lastUpdated}.` : ''}
          </p>
        </div>
        <div className="clinician-header-actions">
          <button
            className="cl-btn-secondary"
            type="button"
            disabled={inboxLoading}
            onClick={() => setReload((value) => value + 1)}
          >
            <RefreshCw size={15} aria-hidden="true" />
            Refresh
          </button>
          <button className="cl-btn-primary" type="button" onClick={() => setComposeMode(true)}>
            <Mail size={15} aria-hidden="true" />
            New message
          </button>
        </div>
      </div>

      {inbox && (
        <div className="message-inbox-counts" aria-label="Inbox counts">
          <button type="button" onClick={() => setQuickFilter('status', '')}>
            <strong>{inbox.counts.total}</strong><span>All</span>
          </button>
          <button type="button" onClick={() => setQuickFilter('status', 'new')}>
            <strong>{inbox.counts.unread}</strong><span>Unread</span>
          </button>
          <button type="button" onClick={() => setQuickFilter('assignment', 'mine')}>
            <strong>{inbox.counts.assignedToMe}</strong><span>Assigned to me</span>
          </button>
          <button type="button" onClick={() => setQuickFilter('assignment', 'unassigned')}>
            <strong>{inbox.counts.unassigned}</strong><span>Unassigned</span>
          </button>
        </div>
      )}

      <form className="cl-card message-inbox-filters" onSubmit={applyFilters}>
        <label className="cl-admin-field">
          <span>Status</span>
          <select
            className="ne-input"
            value={draft.status}
            onChange={(event) => setDraft((current) => ({ ...current, status: event.target.value }))}
          >
            <option value="">All statuses</option>
            <option value="new">Unread / new</option>
            <option value="done">Done</option>
          </select>
        </label>
        <label className="cl-admin-field">
          <span>Assignment</span>
          <select
            className="ne-input"
            value={draft.assignment}
            onChange={(event) => setDraft((current) => ({
              ...current,
              assignment: event.target.value as FilterDraft['assignment'],
            }))}
          >
            <option value="all">All</option>
            <option value="mine">Assigned to me</option>
            <option value="unassigned">Unassigned</option>
          </select>
        </label>
        <label className="cl-admin-field">
          <span>Patient</span>
          <input
            className="ne-input"
            value={draft.patient}
            onChange={(event) => setDraft((current) => ({ ...current, patient: event.target.value }))}
            placeholder="Name or ID"
          />
        </label>
        <label className="cl-admin-field">
          <span>Subject</span>
          <input
            className="ne-input"
            value={draft.subject}
            onChange={(event) => setDraft((current) => ({ ...current, subject: event.target.value }))}
          />
        </label>
        <label className="cl-admin-field">
          <span>Priority</span>
          <select
            className="ne-input"
            value={draft.priority}
            onChange={(event) => setDraft((current) => ({
              ...current,
              priority: event.target.value as FilterDraft['priority'],
            }))}
          >
            <option value="all">All</option>
            <option value="urgent">Urgent</option>
            <option value="normal">Normal</option>
          </select>
        </label>
        <label className="cl-admin-field">
          <span>Owner</span>
          <input
            className="ne-input"
            value={draft.owner}
            onChange={(event) => setDraft((current) => ({ ...current, owner: event.target.value }))}
          />
        </label>
        <label className="cl-admin-field">
          <span>Minimum age (days)</span>
          <input
            className="ne-input"
            type="number"
            min={0}
            value={draft.minimumAgeDays}
            onChange={(event) => setDraft((current) => ({ ...current, minimumAgeDays: event.target.value }))}
          />
        </label>
        <div className="cl-inline-form-actions">
          <button className="cl-btn-primary" type="submit">Apply filters</button>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => {
              const empty = filtersFromParams(new URLSearchParams())
              setDraft(empty)
              setSearchParams(new URLSearchParams())
            }}
          >
            Clear
          </button>
        </div>
      </form>

      {composeMode && (
        <section className="ne-done cl-card" aria-labelledby="compose-message-title">
          <h2 id="compose-message-title" className="cl-card-title">Compose new message</h2>
          <label className="ne-field">
            <span className="ne-label">Patient ID</span>
            <input className="ne-input" value={composeTo} onChange={(event) => setComposeTo(event.target.value)} />
          </label>
          <label className="ne-field">
            <span className="ne-label">Subject</span>
            <input className="ne-input" value={composeSubject} onChange={(event) => setComposeSubject(event.target.value)} />
          </label>
          <label className="ne-field">
            <span className="ne-label">Message</span>
            <textarea className="ne-soap-textarea" rows={4} value={composeBody} onChange={(event) => setComposeBody(event.target.value)} />
          </label>
          <div className="ne-actions">
            <button className="cl-btn-secondary" type="button" onClick={() => setComposeMode(false)}>Cancel</button>
            <button className="cl-btn-primary" type="button" disabled={sending || !composeTo || !composeSubject || !composeBody} onClick={() => void handleCompose()}>
              <Send size={14} aria-hidden="true" />
              {sending ? 'Sending…' : 'Send'}
            </button>
          </div>
        </section>
      )}

      {inboxError && (
        <div className="error-banner message-inbox-error" role="alert">
          <span>{inboxError}</span>
          <button className="cl-btn-secondary" type="button" onClick={() => setReload((value) => value + 1)}>
            Retry
          </button>
        </div>
      )}

      <div className="msg-layout">
        <section className="msg-sidebar" aria-label="Filtered message inbox" aria-busy={inboxLoading}>
          {inboxLoading && !inbox && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((item) => <div key={item} className="skeleton-row" style={{ height: 72 }} />)}
            </div>
          )}
          {inbox && inbox.items.length === 0 && !inboxLoading && (
            <p className="cl-empty-text">No messages match these filters.</p>
          )}
          {inbox && inbox.items.length > 0 && (
            <ul className="message-inbox-list">
              {inbox.items.map((item) => (
                <li key={item.id}>
                  <button
                    className={`message-inbox-item${item.unread ? ' message-inbox-item-unread' : ''}`}
                    type="button"
                    onClick={() => openThread({
                      canonicalId: item.patientId,
                      displayName: item.patientDisplayName,
                      pubpid: item.pubpid,
                    })}
                  >
                    <span className="message-inbox-item-heading">
                      <strong>{item.patientDisplayName}</strong>
                      {item.priority === 'urgent' && <span className="cl-badge cl-badge-coral">Urgent</span>}
                    </span>
                    <span className="message-inbox-subject">{item.subject}</span>
                    <span className="message-inbox-preview">{item.preview || 'No message preview.'}</span>
                    <span className="message-inbox-meta">
                      {item.ageDays}d old · {item.assignedTo ? `Owner: ${item.assignedTo}` : 'Unassigned'}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {inbox && (
            <div className="message-inbox-pagination">
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={currentOffset === 0}
                onClick={() => changePage(Math.max(0, currentOffset - PAGE_SIZE))}
              >
                <ChevronLeft size={14} aria-hidden="true" />
                Previous
              </button>
              <span>{inbox.total === 0 ? '0' : `${currentOffset + 1}–${currentOffset + inbox.items.length}`} of {inbox.total}</span>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={!hasNextPage}
                onClick={() => changePage(currentOffset + PAGE_SIZE)}
              >
                Next
                <ChevronRight size={14} aria-hidden="true" />
              </button>
            </div>
          )}
        </section>

        <section className="msg-thread-panel" aria-label="Message thread">
          {threadState.status === 'idle' && (
            <div className="msg-thread-empty">
              <Mail size={40} aria-hidden="true" />
              <p>Select an inbox message to open its patient thread.</p>
            </div>
          )}
          {threadState.status === 'loading' && (
            <div className="cl-card">
              <div className="skeleton-list">
                {[0, 1, 2].map((item) => <div key={item} className="skeleton-row" style={{ height: 80 }} />)}
              </div>
            </div>
          )}
          {threadState.status === 'error' && (
            <div className="error-banner" role="alert">
              {threadState.message}
              <button className="cl-btn-secondary" type="button" onClick={() => openThread(threadState.patient)}>Retry</button>
            </div>
          )}
          {threadState.status === 'ready' && (
            <>
              <div className="msg-thread-header">
                <div className="msg-thread-patient">
                  <div className="msg-patient-avatar msg-patient-avatar-lg">
                    {initials(threadState.thread.patient.displayName)}
                  </div>
                  <div>
                    <p className="msg-thread-name">{threadState.thread.patient.displayName}</p>
                    <p className="msg-patient-meta">
                      {threadState.thread.patient.pubpid ?? threadState.thread.patient.canonicalId}
                      {' · '}
                      <Link to={`/clinician/patients/${threadState.thread.patient.canonicalId}/summary`}>Open chart</Link>
                    </p>
                  </div>
                </div>
              </div>
              {threadState.thread.messages.length === 0 ? (
                <p className="cl-empty-text">No active messages for this patient.</p>
              ) : (
                <div className="msg-messages">
                  {threadState.thread.messages.map((message) => (
                    <article key={message.id} className="msg-item cl-card">
                      <div className="msg-item-header">
                        <div>
                          <h3 className="msg-item-title">{message.title ?? '(no subject)'}</h3>
                          <p className="msg-item-meta">
                            {message.date}{message.assignedTo ? ` · Assigned: ${message.assignedTo}` : ' · Unassigned'}
                          </p>
                        </div>
                        <span className={`cl-badge ${messageStatusClass(message.status)}`}>
                          {message.status ?? 'Unknown'}
                        </span>
                      </div>
                      {message.body && <p className="msg-item-body">{message.body}</p>}
                      <div className="msg-reply-form">
                        <label className="ne-field"><span className="ne-label">Attach file</span><input className="ne-input" type="file" accept="application/pdf,image/png,image/jpeg,text/plain" onChange={(event) => setAttachmentFiles((current) => ({ ...current, [message.id]: event.target.files?.[0] ?? null }))} /></label>
                        <div className="ne-actions"><button className="cl-btn-secondary" type="button" disabled={!attachmentFiles[message.id] || attachmentBusyId !== null} onClick={() => void uploadAttachment(message.id)}><Paperclip size={14} />{attachmentBusyId === message.id ? 'Uploading…' : 'Add attachment'}</button><button className="cl-btn-secondary" type="button" onClick={() => void loadAttachments(message.id)}>Show attachments</button></div>
                        {attachments[message.id]?.map((attachment) => <div key={attachment.id} className="msg-item-meta">{attachment.fileName} · {Math.ceil(attachment.sizeBytes / 1024)} KiB · {attachment.uploadedBy} <button className="cl-link" type="button" onClick={() => void downloadAttachment(message.id, attachment)}><Download size={13} /> Download</button></div>)}
                      </div>
                      <div className="ne-actions">
                        {message.assignedTo === session.username ? (
                          <span className="cl-badge cl-badge-green">Assigned to you</span>
                        ) : (
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={assigningId !== null}
                            onClick={() => void handleClaim(message.id)}
                          >
                            {assigningId === message.id
                              ? 'Assigning…'
                              : message.assignedTo
                                ? 'Reassign to me'
                                : 'Claim'}
                          </button>
                        )}
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          disabled={forwardingId !== null || assignees.length === 0}
                          onClick={() => setForwardingOpenId((current) => current === message.id ? null : message.id)}
                        >
                          <Forward size={14} aria-hidden="true" />
                          Forward
                        </button>
                      </div>
                      {forwardingOpenId === message.id && (
                        <div className="msg-reply-form">
                          <p className="msg-item-meta">Forward keeps this message in the patient thread, changes its active recipient, and records the actor, time, prior recipient, and optional note.</p>
                          <label className="ne-field">
                            <span className="ne-label">Forward to</span>
                            <select
                              className="ne-input"
                              value={forwardDrafts[message.id]?.assignedTo ?? ''}
                              disabled={forwardingId !== null || assignees.length === 0}
                              onChange={(event) => updateForwardDraft(message.id, 'assignedTo', event.target.value)}
                            >
                              <option value="">Choose active staff</option>
                              {assignees.filter((assignee) => assignee.username !== message.assignedTo).map((assignee) => (
                                <option key={assignee.username} value={assignee.username}>
                                  {assignee.displayName} ({assignee.username})
                                </option>
                              ))}
                            </select>
                          </label>
                          <label className="ne-field">
                            <span className="ne-label">Forwarding note (optional)</span>
                            <textarea
                              className="ne-soap-textarea"
                              rows={2}
                              maxLength={500}
                              value={forwardDrafts[message.id]?.note ?? ''}
                              disabled={forwardingId !== null}
                              onChange={(event) => updateForwardDraft(message.id, 'note', event.target.value)}
                            />
                          </label>
                          <div className="ne-actions">
                            <button className="cl-btn-secondary" type="button" disabled={forwardingId !== null} onClick={() => setForwardingOpenId(null)}>Cancel</button>
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={forwardingId !== null || !(forwardDrafts[message.id]?.assignedTo ?? '')}
                              onClick={() => void handleForward(message)}
                            >
                              <Forward size={14} aria-hidden="true" />
                              {forwardingId === message.id ? 'Forwarding…' : 'Forward message'}
                            </button>
                          </div>
                        </div>
                      )}
                      <div className="msg-reply-form">
                        <label className="ne-field">
                          <span className="ne-label">Assign to</span>
                          <select
                            className="ne-input"
                            value={assignmentDrafts[message.id]?.assignedTo ?? message.assignedTo ?? ''}
                            disabled={assigningId !== null || assignees.length === 0}
                            onChange={(event) => updateAssignmentDraft(message.id, 'assignedTo', event.target.value)}
                          >
                            <option value="">Unassigned</option>
                            {assignees.map((assignee) => (
                              <option key={assignee.username} value={assignee.username}>
                                {assignee.displayName} ({assignee.username})
                              </option>
                            ))}
                          </select>
                        </label>
                        <label className="ne-field">
                          <span className="ne-label">
                            Reason{message.assignedTo ? ' (required to reassign or unassign)' : ' (optional when claiming)'}
                          </span>
                          <input
                            className="ne-input"
                            maxLength={500}
                            value={assignmentDrafts[message.id]?.reason ?? ''}
                            disabled={assigningId !== null}
                            onChange={(event) => updateAssignmentDraft(message.id, 'reason', event.target.value)}
                          />
                        </label>
                        <div className="ne-actions">
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={assigningId !== null || assignees.length === 0 || (assignmentDrafts[message.id]?.assignedTo ?? message.assignedTo ?? '') === (message.assignedTo ?? '')}
                            onClick={() => void handleAssignment(message)}
                          >
                            {assigningId === message.id ? 'Savingâ€¦' : 'Save assignment'}
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={historyLoadingId === message.id}
                            onClick={() => void toggleAssignmentHistory(message.id)}
                          >
                            {historyLoadingId === message.id
                              ? 'Loading historyâ€¦'
                              : assignmentHistory[message.id]
                                ? 'Hide assignment history'
                                : 'Assignment history'}
                          </button>
                        </div>
                        {assigneeError && <p className="field-error" role="alert">{assigneeError}</p>}
                        {assignmentHistory[message.id] && (
                          <div className="cl-card" aria-label="Assignment history">
                            <p className="msg-item-meta">
                              Assignment version {assignmentHistory[message.id].currentVersion}
                            </p>
                            {assignmentHistory[message.id].events.length === 0 ? (
                              <p className="cl-empty-text">No modernized assignment events have been recorded for this message.</p>
                            ) : (
                              <ul className="message-inbox-list">
                                {assignmentHistory[message.id].events.map((event) => (
                                  <li key={event.eventId}>
                                    <strong>{event.action}</strong>
                                    {' â€” '}
                                    {event.previousAssignedTo ?? 'Unassigned'} to {event.assignedTo ?? 'Unassigned'}
                                    {' Â· '}{event.actor}{' Â· '}{new Date(event.occurredAt).toLocaleString()}
                                    {event.reason ? ` Â· ${event.reason}` : ''}
                                  </li>
                                ))}
                              </ul>
                            )}
                          </div>
                        )}
                      </div>
                      {assignmentError?.id === message.id && (
                        <p className="field-error" role="alert">{assignmentError.message}</p>
                      )}
                      {forwardError?.id === message.id && (
                        <p className="field-error" role="alert">{forwardError.message}</p>
                      )}
                      {activeMessageId === message.id ? (
                        <div className="msg-reply-form">
                          <label className="ne-field">
                            <span className="ne-label">Reply</span>
                            <textarea className="ne-soap-textarea" rows={3} value={replyBody} onChange={(event) => setReplyBody(event.target.value)} />
                          </label>
                          <div className="ne-actions">
                            <button className="cl-btn-secondary" type="button" onClick={() => { setActiveMessageId(null); setReplyBody('') }}>Cancel</button>
                            <button className="cl-btn-primary" type="button" disabled={sending || !replyBody.trim()} onClick={() => void handleReply(message.id)}>
                              <Send size={14} aria-hidden="true" />
                              {sending ? 'Sending…' : 'Reply'}
                            </button>
                          </div>
                        </div>
                      ) : (
                        <button className="cl-link msg-reply-btn" type="button" onClick={() => { setActiveMessageId(message.id); setReplyBody('') }}>
                          Reply
                        </button>
                      )}
                    </article>
                  ))}
                </div>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  )
}
