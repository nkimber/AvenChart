import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Mail, Send, User } from 'lucide-react'
import {
  searchPatients,
  getPatientMessages,
  replyToPatientMessage,
  createPatientMessage,
  type PatientListItem,
  type PatientMessageItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type PatientThread = {
  patient: PatientListItem
  messages: PatientMessageItem[]
}

type SearchState =
  | { status: 'idle' }
  | { status: 'searching' }
  | { status: 'results'; patients: PatientListItem[] }
  | { status: 'empty' }

type ThreadState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; thread: PatientThread }
  | { status: 'error'; message: string }

export default function ClinicianMessages() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [search, setSearch] = useState('')
  const [searchState, setSearchState] = useState<SearchState>({ status: 'idle' })
  const [threadState, setThreadState] = useState<ThreadState>({ status: 'idle' })
  const [replyBody, setReplyBody] = useState('')
  const [composeMode, setComposeMode] = useState(false)
  const [composeTo, setComposeTo] = useState('')
  const [composeSubject, setComposeSubject] = useState('')
  const [composeBody, setComposeBody] = useState('')
  const [sending, setSending] = useState(false)
  const [activeMessageId, setActiveMessageId] = useState<string | null>(null)

  useEffect(() => {
    if (!search.trim()) { setSearchState({ status: 'idle' }); return }
    const t = setTimeout(() => {
      setSearchState({ status: 'searching' })
      searchPatients(session.sessionId, { search: search.trim(), limit: 8 })
        .then((data) => {
          setSearchState(data.patients.length > 0
            ? { status: 'results', patients: data.patients }
            : { status: 'empty' })
        })
        .catch(() => setSearchState({ status: 'empty' }))
    }, 300)
    return () => clearTimeout(t)
  }, [search, session.sessionId])

  function openThread(patient: PatientListItem) {
    setSearch('')
    setSearchState({ status: 'idle' })
    setThreadState({ status: 'loading' })
    setReplyBody('')
    setActiveMessageId(null)
    getPatientMessages(session.sessionId, patient.canonicalId)
      .then((data) => setThreadState({
        status: 'ready',
        thread: { patient, messages: data.messages.filter((m) => !m.deleted) },
      }))
      .catch((err) => setThreadState({ status: 'error', message: err instanceof Error ? err.message : 'Could not load messages.' }))
  }

  async function handleReply(messageId: string) {
    if (!replyBody.trim()) return
    setSending(true)
    try {
      const updated = await replyToPatientMessage(session.sessionId, messageId, {
        body: replyBody.trim(),
        assignedTo: session.username,
      })
      setThreadState((prev) =>
        prev.status === 'ready'
          ? { ...prev, thread: { ...prev.thread, messages: updated.messages.filter((m) => !m.deleted) } }
          : prev,
      )
      setReplyBody('')
      setActiveMessageId(null)
      showToast('Reply sent.', 'success')
    } catch {
      showToast('Reply failed. Please try again.', 'error')
    } finally {
      setSending(false)
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
    } catch {
      showToast('Could not create message. Please try again.', 'error')
    } finally {
      setSending(false)
    }
  }

  function statusBadge(status?: string | null) {
    if (!status) return null
    const cls = status === 'new' ? 'cl-badge-amber' : status === 'done' ? 'cl-badge-green' : 'cl-badge-muted'
    return <span className={`cl-badge ${cls}`}>{status}</span>
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Patient messages</h1>
          <p className="clinician-page-subtitle">View and reply to patient portal messages</p>
        </div>
        <div className="clinician-header-actions">
          <button className="cl-btn-primary" type="button" onClick={() => setComposeMode(true)}>
            <Mail size={15} /> New message
          </button>
        </div>
      </div>

      {/* Compose modal */}
      {composeMode && (
        <div className="ne-done cl-card" style={{ marginBottom: 20 }}>
          <h2 className="cl-card-title" style={{ marginBottom: 16 }}>Compose new message</h2>
          <div className="ne-field">
            <label className="ne-label">Patient ID</label>
            <input className="ne-input" type="text" placeholder="Patient canonical ID…" value={composeTo} onChange={(e) => setComposeTo(e.target.value)} />
          </div>
          <div className="ne-field">
            <label className="ne-label">Subject</label>
            <input className="ne-input" type="text" placeholder="Subject…" value={composeSubject} onChange={(e) => setComposeSubject(e.target.value)} />
          </div>
          <div className="ne-field">
            <label className="ne-label">Message</label>
            <textarea className="ne-soap-textarea" rows={4} value={composeBody} onChange={(e) => setComposeBody(e.target.value)} placeholder="Message…" />
          </div>
          <div className="ne-actions">
            <button className="cl-btn-secondary" type="button" onClick={() => setComposeMode(false)}>Cancel</button>
            <button className="cl-btn-primary" type="button" disabled={sending || !composeTo || !composeSubject || !composeBody} onClick={handleCompose}>
              <Send size={14} /> {sending ? 'Sending…' : 'Send'}
            </button>
          </div>
        </div>
      )}

      <div className="msg-layout">
        {/* Patient search sidebar */}
        <div className="msg-sidebar">
          <div className="msg-search-box">
            <input
              className="ne-input"
              type="text"
              placeholder="Search patient…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {searchState.status === 'searching' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 48 }} />)}
            </div>
          )}
          {searchState.status === 'empty' && <p className="cl-empty-text">No patients found.</p>}
          {searchState.status === 'results' && (
            <ul className="msg-patient-list">
              {searchState.patients.map((p) => (
                <li key={p.canonicalId}>
                  <button
                    className="msg-patient-btn"
                    type="button"
                    onClick={() => openThread(p)}
                  >
                    <div className="msg-patient-avatar"><User size={14} /></div>
                    <div>
                      <p className="msg-patient-name">{p.displayName}</p>
                      <p className="msg-patient-meta">DOB {p.dateOfBirth} · {p.counts.messages} msg</p>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {searchState.status === 'idle' && (
            <p className="cl-empty-text" style={{ padding: '12px 16px' }}>Search for a patient to view their messages.</p>
          )}
        </div>

        {/* Thread panel */}
        <div className="msg-thread-panel">
          {threadState.status === 'idle' && (
            <div className="msg-thread-empty">
              <Mail size={40} />
              <p>Select a patient to view messages</p>
            </div>
          )}
          {threadState.status === 'loading' && (
            <div className="cl-card">
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 80 }} />)}
              </div>
            </div>
          )}
          {threadState.status === 'error' && <div className="error-banner">{threadState.message}</div>}
          {threadState.status === 'ready' && (() => {
            const { thread } = threadState
            return (
              <>
                <div className="msg-thread-header">
                  <div className="msg-thread-patient">
                    <div className="msg-patient-avatar msg-patient-avatar-lg">
                      {thread.patient.firstName[0]}{thread.patient.lastName[0]}
                    </div>
                    <div>
                      <p className="msg-thread-name">{thread.patient.displayName}</p>
                      <p className="msg-patient-meta">DOB {thread.patient.dateOfBirth}</p>
                    </div>
                  </div>
                </div>

                {thread.messages.length === 0 ? (
                  <p className="cl-empty-text">No messages for this patient.</p>
                ) : (
                  <div className="msg-messages">
                    {thread.messages.map((msg) => (
                      <div key={msg.id} className="msg-item cl-card">
                        <div className="msg-item-header">
                          <div>
                            <p className="msg-item-title">{msg.title ?? '(no subject)'}</p>
                            <p className="msg-item-meta">{msg.date}{msg.assignedTo ? ` · Assigned: ${msg.assignedTo}` : ''}</p>
                          </div>
                          {statusBadge(msg.status)}
                        </div>
                        {msg.body && <p className="msg-item-body">{msg.body}</p>}

                        {activeMessageId === msg.id ? (
                          <div className="msg-reply-form">
                            <textarea
                              className="ne-soap-textarea"
                              rows={3}
                              placeholder="Reply…"
                              value={replyBody}
                              onChange={(e) => setReplyBody(e.target.value)}
                            />
                            <div className="ne-actions">
                              <button className="cl-btn-secondary" type="button" onClick={() => { setActiveMessageId(null); setReplyBody('') }}>Cancel</button>
                              <button className="cl-btn-primary" type="button" disabled={sending || !replyBody.trim()} onClick={() => handleReply(msg.id)}>
                                <Send size={14} /> {sending ? 'Sending…' : 'Reply'}
                              </button>
                            </div>
                          </div>
                        ) : (
                          <button
                            className="cl-link msg-reply-btn"
                            type="button"
                            onClick={() => { setActiveMessageId(msg.id); setReplyBody('') }}
                          >
                            Reply
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </>
            )
          })()}
        </div>
      </div>
    </div>
  )
}
