// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { demoInvitationUrl, demoRequest, demoStageLabel, newDemoKey, type DemoRun, type DemoStatus } from './demoApi.ts'
import './telehealth.css'

const storageKey = 'avenchart-ui.telehealth-demo.launcher'
type Launcher = { key: string; commandId: string }
function savedLauncher(): Launcher | null {
  try { return JSON.parse(sessionStorage.getItem(storageKey) ?? 'null') as Launcher | null } catch { return null }
}

export default function TelehealthDemoLauncher() {
  const [confirmed, setConfirmed] = useState(false)
  const [launcher, setLauncher] = useState<Launcher | null>(savedLauncher)
  const [run, setRun] = useState<DemoRun | null>(null)
  const [status, setStatus] = useState<DemoStatus | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState<string | null>(null)
  const creating = useRef(false)
  const roomHeading = useRef<HTMLHeadingElement>(null)
  const createButton = useRef<HTMLButtonElement>(null)

  useEffect(() => { if (run && !busy) roomHeading.current?.focus() }, [run, busy])
  useEffect(() => { if (status?.status === 'Ended' || status?.status === 'Expired') createButton.current?.focus() }, [status?.status])

  useEffect(() => {
    if (!launcher) return
    const controller = new AbortController()
    setBusy(true); setError(null)
    demoRequest<DemoRun>('/runs', launcher.key, { syntheticDataConfirmed: true, observerKey: launcher.key }, 'POST', controller.signal, launcher.commandId)
      .then(result => { if (!controller.signal.aborted) setRun(result) })
      .catch(() => { if (!controller.signal.aborted) setError('The demo could not be prepared. Retry with the same launcher to recover an interrupted request.') })
      .finally(() => { if (!controller.signal.aborted) { setBusy(false); creating.current = false } })
    return () => controller.abort()
  }, [launcher])

  useEffect(() => {
    if (!run || !launcher) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout>
    async function poll() {
      try {
        const result = await demoRequest<DemoStatus>(`/runs/${run!.runId}`, launcher!.key, undefined, 'GET', controller.signal)
        if (!controller.signal.aborted) { setStatus(result); setError(null) }
      } catch { if (!controller.signal.aborted) setError('Participant status is temporarily unavailable. Keep this page open to retry.') }
      finally { if (!controller.signal.aborted) timer = setTimeout(() => void poll(), 3000) }
    }
    void poll()
    return () => { controller.abort(); clearTimeout(timer) }
  }, [run, launcher])

  function create() {
    if (!confirmed || busy || creating.current) return
    creating.current = true
    const next = { key: newDemoKey(), commandId: crypto.randomUUID() }
    sessionStorage.setItem(storageKey, JSON.stringify(next))
    setRun(null); setStatus(null); setLauncher(next)
  }
  async function end() {
    if (!run || !launcher || busy) return
    setBusy(true)
    try { setStatus(await demoRequest<DemoStatus>(`/runs/${run.runId}/end`, launcher.key, {})); setError(null) }
    catch { setError('The demo could not be ended. Check the connection and try again.') }
    finally { setBusy(false) }
  }
  async function copy(value: string, role: string) {
    try { await navigator.clipboard.writeText(value); setCopied(`${role} invitation copied.`) }
    catch { setCopied('Copy the invitation from the selectable field below.') }
  }
  const ended = status?.status === 'Ended' || status?.status === 'Expired'
  return <main className="telehealth-page" id="main-content">
    <h1>Launch a two-person telehealth demo</h1>
    <p className="telehealth-synthetic" role="note">Synthetic connection demonstration only. No patient chart, clinical visit, consent, prescription, billing, or claim is created.</p>
    <p>Open a patient session and a physician session, check devices, and connect. Each demo gets its own room and expires after 30 minutes.</p>
    {error ? <p className="telehealth-error" role="alert">{error}</p> : null}
    {error && !busy ? <button className="telehealth-button telehealth-button-secondary" onClick={() => { sessionStorage.removeItem(storageKey); setLauncher(null); setRun(null); setStatus(null); setError(null); creating.current = false }}>Start a fresh launcher</button> : null}
    {!run || ended ? <section className="telehealth-card">
      {ended ? <p role="status">This demo has {status?.status === 'Expired' ? 'expired' : 'ended'}. Both participants can start a fresh demo without resetting any records.</p> : null}
      <label className="telehealth-check"><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I will use fictional information in this synthetic connection demo.</label>
      <button ref={createButton} className="telehealth-button" disabled={!confirmed || busy} onClick={create}>{busy ? 'Preparing demo…' : ended ? 'Create another synthetic demo' : 'Create synthetic demo'}</button>
      {launcher && !run && !busy ? <button className="telehealth-button telehealth-button-secondary" onClick={() => setLauncher({ ...launcher })}>Retry preparation</button> : null}
    </section> : <section className="telehealth-card" aria-label="Your demo room">
      <h2 ref={roomHeading} tabIndex={-1}>Your two-session room</h2>
      <p>Room <strong>{run.runId.slice(0, 8)}</strong>. Expires <time dateTime={run.expiresAt}>{new Date(run.expiresAt).toLocaleTimeString()}</time>.</p>
      <div className="telehealth-demo-role-grid">{(['patient', 'physician'] as const).map(role => <section className="telehealth-demo-role-card" key={role} aria-label={`${role} invitation`}>
        <h3>{role === 'patient' ? 'Patient' : 'Physician'} session</h3>
        <p role="status">{demoStageLabel[role === 'patient' ? status?.patientStatus ?? 'Invited' : status?.physicianStatus ?? 'Invited']}</p>
        <div className="telehealth-actions"><a className="telehealth-button" href={demoInvitationUrl(run, role)} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer">Open {role} session</a>
        <button className="telehealth-button telehealth-button-secondary" onClick={() => void copy(demoInvitationUrl(run, role), role)}>Copy {role} invitation</button>
        </div><label className="telehealth-demo-invitation-field">One-use {role} invitation<input readOnly value={demoInvitationUrl(run, role)} onFocus={event => event.target.select()} /></label>
      </section>)}</div>
      <p>Each invitation can be activated in one browser session. Share the patient invitation with a second device for two-way audio, and use headphones. On one laptop, silence both tabs to prevent feedback.</p>
      {copied ? <p role="status">{copied}</p> : null}
      <button className="telehealth-button telehealth-button-danger" disabled={busy} onClick={() => void end()}>End this demo</button>
    </section>}
    <p><Link to="/telehealth">Full visit demonstration and existing demo accounts</Link></p>
  </main>
}
