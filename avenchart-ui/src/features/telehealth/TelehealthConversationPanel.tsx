// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import {
  addPatientTelehealthConversationMessage,
  addPhysicianTelehealthConversationMessage,
  getPatientTelehealthConversation,
  getPhysicianTelehealthConversation,
  type TelehealthConversation,
} from './api.ts'

type Props =
  | { participant: 'patient'; requestId: string }
  | { participant: 'physician'; consultationId: string }

export default function TelehealthConversationPanel(props: Props) {
  const headingId = useId()
  const [conversation, setConversation] = useState<TelehealthConversation | null>(null)
  const [body, setBody] = useState('')
  const [syntheticConfirmed, setSyntheticConfirmed] = useState(false)
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    try {
      const result = props.participant === 'patient'
        ? await getPatientTelehealthConversation(props.requestId, signal)
        : await getPhysicianTelehealthConversation(props.consultationId, signal)
      setConversation(result)
      setError(null)
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setConversation(null)
      setError(caught instanceof Error ? caught.message : 'The synthetic transcript could not be loaded.')
    } finally {
      if (!signal?.aborted) setLoading(false)
    }
  }, [props])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    const refreshId = window.setInterval(() => {
      if (document.visibilityState === 'visible') void load()
    }, 5000)
    return () => {
      controller.abort()
      window.clearInterval(refreshId)
    }
  }, [load])

  async function send() {
    const message = body.trim()
    if (!message || !syntheticConfirmed || sending) return
    setSending(true)
    setError(null)
    try {
      const result = props.participant === 'patient'
        ? await addPatientTelehealthConversationMessage(props.requestId, message)
        : await addPhysicianTelehealthConversationMessage(props.consultationId, message)
      setConversation(result)
      setBody('')
      setSyntheticConfirmed(false)
      setStatus('Synthetic transcript message added. No external communication occurred.')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The synthetic transcript message was not added.')
    } finally {
      setSending(false)
    }
  }

  return (
    <section className="telehealth-conversation" aria-labelledby={headingId} aria-busy={loading}>
      <div><p className="telehealth-kicker">POC-only simulated communication</p><h4 id={headingId}>Synthetic consultation transcript</h4></div>
      <p role="note">This is plain-text demonstration data only—not a monitored message service, real consultation, video call, emergency channel, record, prescription, or care instruction. Call 911 for an emergency.</p>
      <div className="telehealth-conversation-log" aria-live="polite" aria-label="Synthetic transcript messages">
        {conversation?.messages.length ? conversation.messages.map((message) => (
          <article className={`telehealth-conversation-message is-${message.senderRole}`} key={message.messageId}>
            <strong>{message.senderRole === 'physician' ? 'Synthetic physician' : 'Synthetic patient'}</strong>
            <p>{message.body}</p>
            <small>{new Date(message.sentAt).toLocaleTimeString()} · no legal or clinical effect</small>
          </article>
        )) : <p>{loading ? 'Loading synthetic transcript…' : 'No synthetic messages yet.'}</p>}
      </div>
      {status ? <p role="status">{status}</p> : null}
      {error ? <p className="telehealth-error" role="alert">{error}</p> : null}
      <form onSubmit={(event) => { event.preventDefault(); void send() }}>
        <label>Demonstration message<textarea maxLength={1000} value={body} onChange={(event) => setBody(event.target.value)} disabled={sending} /></label>
        <p><small>{body.trim().length}/1000. Short HTTP polling refreshes the display while this page is visible; realtime delivery, recording, transcription, attachments, notifications, and external transmission are disabled.</small></p>
        <label className="telehealth-check"><input type="checkbox" checked={syntheticConfirmed} onChange={(event) => setSyntheticConfirmed(event.target.checked)} />I confirm this contains synthetic demonstration data only and is not care communication.</label>
        <div className="telehealth-actions">
          <button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading || sending} onClick={() => void load()}>Refresh transcript</button>
          <button className="telehealth-button" type="submit" disabled={sending || !body.trim() || !syntheticConfirmed}>{sending ? 'Adding message…' : 'Add synthetic message'}</button>
        </div>
      </form>
      {conversation ? <ul>{conversation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
    </section>
  )
}
