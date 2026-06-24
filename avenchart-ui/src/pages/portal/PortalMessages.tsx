import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation, useOutletContext } from 'react-router-dom'
import { ArrowLeft, CheckCheck, PenLine, RefreshCw, Send } from 'lucide-react'
import {
  composePatientPortalMessage,
  getPatientPortalMessages,
  getPatientPortalMessageThread,
  markPatientPortalMessageRead,
  replyToPatientPortalMessage,
  type PatientPortalMessageItem,
  type PatientPortalMessagesResponse,
  type PatientPortalMessageThreadResponse,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type View = 'list' | 'compose' | 'thread'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

const SUBJECT_PRESETS = ['General', 'Insurance', 'Prior Auth', 'Bill/Collect', 'Referral', 'Pharmacy']

function relativeDate(dateStr: string): string {
  if (!dateStr) return dateStr
  // Try to parse "YYYY-MM-DD" or "YYYY-MM-DD HH:MM"
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

export default function PortalMessages() {
  const { session, refreshHome, markReadOptimistic } = useOutletContext<PortalOutletContext>()
  const location = useLocation()
  const threadEndRef = useRef<HTMLDivElement>(null)

  const [view, setView] = useState<View>(() =>
    location.state?.compose === true ? 'compose' : 'list',
  )
  const [messagesState, setMessagesState] = useState<AsyncState<PatientPortalMessagesResponse>>({
    status: 'idle',
  })
  // Local optimistic set — for per-row unread styling (shell handles badge)
  const [readOptimistic, setReadOptimistic] = useState<Set<string>>(() => new Set())
  const [selectedMessage, setSelectedMessage] = useState<PatientPortalMessageItem | null>(null)
  const [threadState, setThreadState] = useState<AsyncState<PatientPortalMessageThreadResponse>>({
    status: 'idle',
  })
  const [markingAllRead, setMarkingAllRead] = useState(false)

  const [composeTitle, setComposeTitle] = useState('')
  const [composeBody, setComposeBody] = useState('')
  const [composeSubmitting, setComposeSubmitting] = useState(false)
  const [composeDone, setComposeDone] = useState(false)

  const [replyBody, setReplyBody] = useState('')
  const [replySubmitting, setReplySubmitting] = useState(false)

  useEffect(() => {
    loadMessages()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (threadState.status === 'ready') {
      threadEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    }
  }, [threadState])

  useEffect(() => {
    if (view !== 'compose') return
    const hasDraft = composeTitle !== '' || composeBody !== ''
    if (!hasDraft) return
    function handleBeforeUnload(e: BeforeUnloadEvent) { e.preventDefault() }
    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [view, composeTitle, composeBody])

  function loadMessages() {
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

  function openThread(msg: PatientPortalMessageItem) {
    setSelectedMessage(msg)
    setReplyBody('')
    setView('thread')
    setThreadState({ status: 'loading' })
    // Optimistically mark read so badge drops immediately (#2)
    if (msg.status === 'New') {
      setReadOptimistic((prev) => new Set([...prev, msg.id]))
      markReadOptimistic(msg.id)
    }
    getPatientPortalMessageThread(session.sessionId, msg.id)
      .then((data) => {
        setThreadState({ status: 'ready', data })
        if (msg.status === 'New') {
          markPatientPortalMessageRead(session.sessionId, msg.id)
            .then(() => { loadMessages(); refreshHome() })
            .catch(() => {})
        }
      })
      .catch((err) =>
        setThreadState({
          status: 'error',
          message: err instanceof Error ? err.message : 'Could not load this conversation.',
        }),
      )
  }

  function backToList() {
    setView('list')
    setSelectedMessage(null)
    setThreadState({ status: 'idle' })
  }

  function leaveCompose() {
    const hasDraft = composeTitle !== '' || composeBody !== ''
    if (hasDraft && !window.confirm('Discard this message draft?')) return
    setView('list')
    setComposeTitle('')
    setComposeBody('')
    setComposeDone(false)
  }

  function markAllRead() {
    if (messagesState.status !== 'ready') return
    const unread = messagesState.data.messages.filter(
      (m) => m.status === 'New' && !readOptimistic.has(m.id),
    )
    if (unread.length === 0) return
    setMarkingAllRead(true)
    setReadOptimistic((prev) => new Set([...prev, ...unread.map((m) => m.id)]))
    Promise.all(
      unread.map((m) => markPatientPortalMessageRead(session.sessionId, m.id).catch(() => {})),
    ).finally(() => {
      setMarkingAllRead(false)
      loadMessages()
      refreshHome()
      showToast('All messages marked as read')
    })
  }

  function submitCompose(event: FormEvent) {
    event.preventDefault()
    setComposeSubmitting(true)
    composePatientPortalMessage(session.sessionId, { title: composeTitle, body: composeBody })
      .then((result) => {
        if (!result.created) {
          showToast(result.failureReason ?? 'The message was not sent.', 'error')
          return
        }
        showToast(`Message sent to ${result.recipientName}.`)
        setComposeDone(true)
        setComposeTitle('')
        setComposeBody('')
        loadMessages()
        refreshHome()
      })
      .catch((err) => showToast(err instanceof Error ? err.message : 'Could not send the message.', 'error'))
      .finally(() => setComposeSubmitting(false))
  }

  function submitReply(event: FormEvent) {
    event.preventDefault()
    if (!selectedMessage) return
    setReplySubmitting(true)
    replyToPatientPortalMessage(session.sessionId, selectedMessage.id, { body: replyBody })
      .then((result) => {
        if (!result.created) {
          showToast(result.failureReason ?? 'The reply was not sent.', 'error')
          return
        }
        showToast('Reply sent.')
        setReplyBody('')
        return getPatientPortalMessageThread(session.sessionId, selectedMessage.id).then((data) =>
          setThreadState({ status: 'ready', data }),
        )
      })
      .catch((err) => showToast(err instanceof Error ? err.message : 'Could not send the reply.', 'error'))
      .finally(() => setReplySubmitting(false))
  }

  function isSentByPatient(msg: PatientPortalMessageItem): boolean {
    return msg.senderName === session.displayName
  }

  // Compute effective unread status considering optimistic reads
  function isUnread(msg: PatientPortalMessageItem): boolean {
    return msg.status === 'New' && !readOptimistic.has(msg.id)
  }

  return (
    <div className="portal-page">
      {/* ─── Thread view ─── */}
      {view === 'thread' && selectedMessage && (
        <section className="portal-section">
          <div className="portal-section-header">
            <button className="back-button" type="button" onClick={backToList}
              aria-label="Back to inbox">
              <ArrowLeft size={16} />
              Back to inbox
            </button>
          </div>

          {threadState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" />)}
            </div>
          )}
          {threadState.status === 'error' && (
            <div className="error-banner">{threadState.message}</div>
          )}
          {threadState.status === 'ready' && (
            <>
              <h2 className="thread-title">{selectedMessage.title}</h2>
              <ul className="thread-list" role="list" aria-label="Conversation thread">
                {threadState.data.threadMessages.map((m) => (
                  <li
                    key={m.id}
                    className={`thread-bubble ${isSentByPatient(m) ? 'thread-bubble-sent' : 'thread-bubble-received'}`}
                  >
                    <div className="thread-bubble-meta">
                      <span>{m.senderName}</span>
                      <span>·</span>
                      <time dateTime={m.date}>{relativeDate(m.date)}</time>
                    </div>
                    <div className="thread-bubble-body">{m.body}</div>
                  </li>
                ))}
              </ul>
              <div ref={threadEndRef} />

              <form className="reply-form" onSubmit={submitReply}>
                <div className="reply-input-row">
                  <textarea
                    className="textarea reply-textarea"
                    placeholder="Write a reply…"
                    value={replyBody}
                    onChange={(e) => setReplyBody(e.target.value)}
                    required
                    rows={3}
                    aria-label="Reply text"
                  />
                  <button
                    className="reply-send-button"
                    type="submit"
                    disabled={replySubmitting || !replyBody.trim()}
                    aria-label="Send reply"
                  >
                    <Send size={16} />
                  </button>
                </div>
              </form>
            </>
          )}
        </section>
      )}

      {/* ─── Compose view ─── */}
      {view === 'compose' && (
        <section className="portal-section">
          <div className="portal-section-header">
            <button className="back-button" type="button" onClick={leaveCompose}>
              <ArrowLeft size={16} />
              Back to inbox
            </button>
          </div>
          <h2 className="portal-section-title" style={{ marginBottom: 20 }}>New message</h2>

          {composeDone ? (
            <div className="compose-success">
              <p style={{ color: 'var(--teal)', fontWeight: 500 }}>Message sent successfully.</p>
              <button
                className="button-secondary"
                style={{ width: 'auto' }}
                type="button"
                onClick={() => { setView('list'); setComposeDone(false) }}
              >
                Back to inbox
              </button>
            </div>
          ) : (
            <form className="compose-form" onSubmit={submitCompose}>
              <div className="field">
                <label className="label" htmlFor="compose-subject">Subject</label>
                <input
                  id="compose-subject"
                  className="input"
                  list="subject-presets"
                  value={composeTitle}
                  onChange={(e) => setComposeTitle(e.target.value)}
                  placeholder="Choose a subject or type your own"
                  required
                />
                <datalist id="subject-presets">
                  {SUBJECT_PRESETS.map((s) => <option key={s} value={s} />)}
                </datalist>
              </div>
              <div className="field">
                <label className="label" htmlFor="compose-body">Message</label>
                <textarea
                  id="compose-body"
                  className="textarea"
                  value={composeBody}
                  onChange={(e) => setComposeBody(e.target.value)}
                  rows={6}
                  required
                />
              </div>
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={composeSubmitting}>
                  {composeSubmitting ? 'Sending…' : 'Send message'}
                </button>
                <button
                  className="button-secondary"
                  type="button"
                  onClick={leaveCompose}
                  style={{ width: 'auto', flex: 'none' }}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
        </section>
      )}

      {/* ─── Inbox list view ─── */}
      {view === 'list' && (
        <>
          <div className="inbox-header">
            <h2 className="portal-section-title">Inbox</h2>
            <div className="inbox-actions">
              {messagesState.status === 'ready' &&
                messagesState.data.messages.some((m) => isUnread(m)) && (
                  <button
                    className="inbox-action-button"
                    type="button"
                    disabled={markingAllRead}
                    onClick={markAllRead}
                    title="Mark all messages as read"
                  >
                    <CheckCheck size={14} />
                    {markingAllRead ? 'Marking…' : 'Mark all read'}
                  </button>
                )}
              <button
                className="inbox-action-button"
                type="button"
                disabled={messagesState.status === 'loading'}
                onClick={loadMessages}
                title="Refresh inbox"
              >
                <RefreshCw size={14} className={messagesState.status === 'loading' ? 'spin' : ''} />
                Refresh
              </button>
              <button
                className="compose-button"
                type="button"
                onClick={() => { setView('compose'); setComposeDone(false) }}
              >
                <PenLine size={15} />
                New message
              </button>
            </div>
          </div>

          {messagesState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 68 }} />)}
            </div>
          )}
          {messagesState.status === 'error' && (
            <div className="error-banner">{messagesState.message}</div>
          )}
          {messagesState.status === 'ready' && (
            messagesState.data.messages.length === 0 ? (
              <div className="empty-state">
                <div className="empty-state-icon-wrap">
                  <PenLine size={28} />
                </div>
                <p className="empty-state-text">Your inbox is empty.</p>
                <button
                  className="button-secondary"
                  style={{ width: 'auto' }}
                  type="button"
                  onClick={() => setView('compose')}
                >
                  Message your care team
                </button>
              </div>
            ) : (
              <ul className="message-list" role="list" aria-label="Inbox messages">
                {messagesState.data.messages.map((msg) => {
                  const unread = isUnread(msg)
                  return (
                    <li key={msg.id}>
                      <button
                        className={`message-row${unread ? ' message-row-unread' : ''}`}
                        type="button"
                        onClick={() => openThread(msg)}
                        aria-label={`${unread ? 'Unread: ' : ''}${msg.title}, from ${msg.senderName}`}
                      >
                        {unread && <span className="unread-dot" aria-hidden="true" />}
                        <div className="message-row-body">
                          <div className="message-row-top">
                            <span className="message-row-title">{msg.title}</span>
                            <time className="message-row-date" dateTime={msg.date}>
                              {relativeDate(msg.date)}
                            </time>
                          </div>
                          <p className="message-row-preview">
                            {msg.senderName} · {msg.body.slice(0, 80)}{msg.body.length > 80 ? '…' : ''}
                          </p>
                        </div>
                      </button>
                    </li>
                  )
                })}
              </ul>
            )
          )}
        </>
      )}
    </div>
  )
}
