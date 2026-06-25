import { useEffect, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { getPatientMessages, replyToPatientMessage, type PatientMessageItem } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function relativeDate(dateStr?: string | null) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  const now = Date.now()
  const diff = now - d.getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 2) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`
  return d.toLocaleDateString()
}

function statusClass(status?: string | null) {
  if (!status) return ''
  const s = status.toLowerCase()
  if (s === 'done') return 'cl-badge-green'
  if (s === 'new' || s === 'unread') return 'cl-badge-amber'
  return 'cl-badge-muted'
}

export default function PatientMessages() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<PatientMessageItem[]>>({ status: 'loading' })
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [replyBody, setReplyBody] = useState('')
  const [replying, setReplying] = useState(false)

  function load() {
    getPatientMessages(session.sessionId, patientId)
      .then((data) => {
        setState({ status: 'ready', data: data.messages.filter((m) => !m.deleted) })
      })
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  useEffect(() => { load() }, [patientId]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleReply(e: FormEvent) {
    e.preventDefault()
    if (!selectedId || !replyBody.trim()) return
    setReplying(true)
    try {
      await replyToPatientMessage(session.sessionId, selectedId, { body: replyBody.trim(), assignedTo: '' })
      showToast('Reply sent')
      setReplyBody('')
      load()
    } catch {
      showToast('Could not send reply.', 'error')
    } finally {
      setReplying(false)
    }
  }

  const messages = state.status === 'ready' ? state.data : []
  const selected = messages.find((m) => m.id === selectedId) ?? null

  return (
    <div className="clinician-page">
      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 60 }} />)}
          </div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && (
        <div className="cl-messages-layout">
          <ul className="cl-msg-list">
            {messages.length === 0 && <li className="cl-empty-text">No messages.</li>}
            {messages.map((msg) => (
              <li key={msg.id}>
                <button
                  className={`cl-msg-item${msg.id === selectedId ? ' cl-msg-item-active' : ''}`}
                  type="button"
                  onClick={() => setSelectedId(msg.id)}
                >
                  <div className="cl-msg-item-top">
                    <span className="cl-msg-title">{msg.title ?? '(no subject)'}</span>
                    {msg.status && (
                      <span className={`cl-badge ${statusClass(msg.status)}`}>{msg.status}</span>
                    )}
                  </div>
                  <div className="cl-msg-item-meta">
                    <span>{relativeDate(msg.date)}</span>
                    {msg.assignedTo && <span>→ {msg.assignedTo}</span>}
                  </div>
                </button>
              </li>
            ))}
          </ul>

          <div className="cl-msg-detail">
            {!selected ? (
              <p className="cl-empty-text">Select a message to read.</p>
            ) : (
              <>
                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">{selected.title ?? '(no subject)'}</h2>
                    {selected.status && (
                      <span className={`cl-badge ${statusClass(selected.status)}`}>{selected.status}</span>
                    )}
                  </div>
                  <ul className="fact-list">
                    {selected.date && <li className="fact-row"><span>Date</span><span>{selected.date}</span></li>}
                    {selected.assignedTo && <li className="fact-row"><span>Assigned</span><span>{selected.assignedTo}</span></li>}
                    {selected.portalRelation && <li className="fact-row"><span>Portal</span><span>{selected.portalRelation}</span></li>}
                  </ul>
                  {selected.body && <p className="cl-msg-body">{selected.body}</p>}
                </div>

                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">Reply</h2>
                  </div>
                  <form onSubmit={handleReply}>
                    <textarea
                      className="cl-textarea"
                      value={replyBody}
                      onChange={(e) => setReplyBody(e.target.value)}
                      placeholder="Type your reply…"
                      rows={4}
                      aria-label="Reply body"
                    />
                    <button
                      className="cl-btn-primary"
                      type="submit"
                      disabled={replying || !replyBody.trim()}
                      style={{ marginTop: 8 }}
                    >
                      {replying ? 'Sending…' : 'Send reply'}
                    </button>
                  </form>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
