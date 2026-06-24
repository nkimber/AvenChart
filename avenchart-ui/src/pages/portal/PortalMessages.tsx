import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation, useOutletContext } from 'react-router-dom'
import { ArrowLeft, PenLine, Send } from 'lucide-react'
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

type View = 'list' | 'compose' | 'thread'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

const SUBJECT_PRESETS = ['General', 'Insurance', 'Prior Auth', 'Bill/Collect', 'Referral', 'Pharmacy']

export default function PortalMessages() {
  const { session, refreshHome } = useOutletContext<PortalOutletContext>()
  const location = useLocation()
  const listRef = useRef<HTMLUListElement>(null)

  const [view, setView] = useState<View>(() =>
    location.state?.compose === true ? 'compose' : 'list',
  )
  const [messagesState, setMessagesState] = useState<AsyncState<PatientPortalMessagesResponse>>({
    status: 'idle',
  })
  const [selectedMessage, setSelectedMessage] = useState<PatientPortalMessageItem | null>(null)
  const [threadState, setThreadState] = useState<AsyncState<PatientPortalMessageThreadResponse>>({
    status: 'idle',
  })

  const [composeTitle, setComposeTitle] = useState('')
  const [composeBody, setComposeBody] = useState('')
  const [composeSubmitting, setComposeSubmitting] = useState(false)
  const [composeResult, setComposeResult] = useState<string | null>(null)
  const [composeError, setComposeError] = useState<string | null>(null)

  const [replyBody, setReplyBody] = useState('')
  const [replySubmitting, setReplySubmitting] = useState(false)
  const [replyResult, setReplyResult] = useState<string | null>(null)
  const [replyError, setReplyError] = useState<string | null>(null)

  useEffect(() => {
    loadMessages()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

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
    setReplyResult(null)
    setReplyError(null)
    setView('thread')
    setThreadState({ status: 'loading' })
    getPatientPortalMessageThread(session.sessionId, msg.id)
      .then((data) => {
        setThreadState({ status: 'ready', data })
        if (msg.status === 'New') {
          markPatientPortalMessageRead(session.sessionId, msg.id)
            .then(() => {
              loadMessages()
              refreshHome()
            })
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

  function submitCompose(event: FormEvent) {
    event.preventDefault()
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
        loadMessages()
        refreshHome()
      })
      .catch((err) =>
        setComposeError(err instanceof Error ? err.message : 'Could not send the message.'),
      )
      .finally(() => setComposeSubmitting(false))
  }

  function submitReply(event: FormEvent) {
    event.preventDefault()
    if (!selectedMessage) return
    setReplySubmitting(true)
    setReplyError(null)
    replyToPatientPortalMessage(session.sessionId, selectedMessage.id, { body: replyBody })
      .then((result) => {
        if (!result.created) {
          setReplyError(result.failureReason ?? 'The reply was not sent.')
          return
        }
        setReplyResult('Reply sent.')
        setReplyBody('')
        return getPatientPortalMessageThread(session.sessionId, selectedMessage.id).then((data) =>
          setThreadState({ status: 'ready', data }),
        )
      })
      .catch((err) => setReplyError(err instanceof Error ? err.message : 'Could not send the reply.'))
      .finally(() => setReplySubmitting(false))
  }

  return (
    <div className="portal-page">
      {/* ─── Thread view ─── */}
      {view === 'thread' && selectedMessage && (
        <section className="portal-section">
          <div className="portal-section-header">
            <button className="back-button" type="button" onClick={backToList}>
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
              <ul className="thread-list">
                {threadState.data.threadMessages.map((m) => {
                  const isPatient = m.senderName === selectedMessage.senderName || m.recipientName !== selectedMessage.recipientName
                  return (
                    <li key={m.id} className={`thread-bubble ${isPatient ? 'thread-bubble-sent' : 'thread-bubble-received'}`}>
                      <div className="thread-bubble-meta">
                        {m.senderName} · {m.date}
                      </div>
                      <div className="thread-bubble-body">{m.body}</div>
                    </li>
                  )
                })}
              </ul>

              <form className="reply-form" onSubmit={submitReply}>
                <div className="reply-input-row">
                  <textarea
                    className="textarea reply-textarea"
                    placeholder="Write a reply…"
                    value={replyBody}
                    onChange={(e) => setReplyBody(e.target.value)}
                    required
                    rows={3}
                  />
                  <button className="reply-send-button" type="submit" disabled={replySubmitting || !replyBody.trim()}>
                    <Send size={16} />
                  </button>
                </div>
                {replyError && <div className="error-banner" style={{ marginTop: 8 }}>{replyError}</div>}
                {replyResult && <div className="hint-banner" style={{ marginTop: 8 }}>{replyResult}</div>}
              </form>
            </>
          )}
        </section>
      )}

      {/* ─── Compose view ─── */}
      {view === 'compose' && (
        <section className="portal-section">
          <div className="portal-section-header">
            <button className="back-button" type="button" onClick={() => { setView('list'); setComposeResult(null); setComposeError(null) }}>
              <ArrowLeft size={16} />
              Back to inbox
            </button>
          </div>
          <h2 className="portal-section-title" style={{ marginBottom: 20 }}>New message</h2>

          {composeResult ? (
            <div className="compose-success">
              <div className="hint-banner">{composeResult}</div>
              <button className="button-secondary" style={{ width: 'auto' }} type="button" onClick={() => { setView('list'); setComposeResult(null) }}>
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
              {composeError && <div className="error-banner">{composeError}</div>}
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={composeSubmitting}>
                  {composeSubmitting ? 'Sending…' : 'Send message'}
                </button>
                <button
                  className="button-secondary"
                  type="button"
                  onClick={() => { setView('list'); setComposeError(null) }}
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
            <button
              className="compose-button"
              type="button"
              onClick={() => { setView('compose'); setComposeResult(null); setComposeError(null) }}
            >
              <PenLine size={15} />
              New message
            </button>
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
              <ul className="message-list" ref={listRef}>
                {messagesState.data.messages.map((msg) => {
                  const isUnread = msg.status === 'New'
                  return (
                    <li key={msg.id}>
                      <button
                        className={`message-row${isUnread ? ' message-row-unread' : ''}`}
                        type="button"
                        onClick={() => openThread(msg)}
                      >
                        {isUnread && <span className="unread-dot" />}
                        <div className="message-row-body">
                          <div className="message-row-top">
                            <span className="message-row-title">{msg.title}</span>
                            <span className="message-row-date">{msg.date}</span>
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
