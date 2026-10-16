// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { DemoRequestError, demoRequest, demoStageLabel, newDemoKey, type DemoParticipant, type DemoRole, type DemoStatus } from './demoApi.ts'
import type { TelehealthConnectionGrant, TelehealthInternetCallingConfiguration, TelehealthLocalWebRtcSignalRead } from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import TelehealthLocalWebRtcPocPanel from './TelehealthLocalWebRtcPocPanel.tsx'
import './telehealth.css'

const InternetCall = lazy(() => import('./TelehealthInternetCallingPocPanel.tsx'))
type RoomSession = { key: string; participant?: DemoParticipant }
function readSession(storageKey: string): RoomSession | null {
  try { return JSON.parse(sessionStorage.getItem(storageKey) ?? 'null') as RoomSession | null } catch { return null }
}

export default function TelehealthDemoRoom() {
  const { runId = '', role = '' } = useParams()
  return <DemoRoomSession key={`${runId}/${role}`} runId={runId} role={role} />
}

function DemoRoomSession({ runId, role }: { runId: string; role: string }) {
  const valid = /^[0-9a-f-]{36}$/i.test(runId) && (role === 'patient' || role === 'physician')
  const storageKey = `avenchart-ui.telehealth-demo.${runId}.${role}`
  const session = useMemo(() => readSession(storageKey) ?? { key: newDemoKey() }, [storageKey])
  const invitation = useRef(new URLSearchParams(window.location.hash.slice(1)).get('invite'))
  const [participant, setParticipant] = useState<DemoParticipant | null>(null)
  const [status, setStatus] = useState<DemoStatus | null>(null)
  const [ready, setReady] = useState(false)
  const [expired, setExpired] = useState(false)
  const [revoked, setRevoked] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [statusError, setStatusError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const stage = useRef('Opened')
  const path = `/runs/${runId}/${role}`

  useEffect(() => {
    if (!valid) return
    const controller = new AbortController()
    async function activate() {
      try {
        sessionStorage.setItem(storageKey, JSON.stringify(session))
        const member = invitation.current
          ? await demoRequest<DemoParticipant>(`${path}/redeem`, session.key, { invitation: invitation.current, sessionKey: session.key }, 'POST', controller.signal)
          : session.participant
        if (!member) throw new Error('Open the one-use invitation from the demo launcher.')
        await demoRequest<DemoStatus>(`/runs/${runId}`, session.key, undefined, 'GET', controller.signal)
        if (controller.signal.aborted) return
        sessionStorage.setItem(storageKey, JSON.stringify({ key: session.key, participant: member }))
        window.history.replaceState(null, '', window.location.pathname + window.location.search)
        invitation.current = null
        setParticipant(member); setExpired(false); setError(null)
      } catch (caught) { if (!controller.signal.aborted) setError(caught instanceof Error ? caught.message : 'The invitation could not be opened. Retry or request a new demo.') }
    }
    void activate()
    return () => controller.abort()
  }, [valid, storageKey, session, path, runId, attempt])

  useEffect(() => {
    if (!participant) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout>
    const expiration = setTimeout(() => { setExpired(true); setReady(false); setError('This synthetic demo has expired. Create a new demo to connect again.') }, Math.max(0, Date.parse(participant.expiresAt) - Date.now()))
    async function poll() {
      try {
        const result = await demoRequest<DemoStatus>(`/runs/${runId}`, session.key, undefined, 'GET', controller.signal)
        if (controller.signal.aborted) return
        setStatus(result)
        setStatusError(null)
        if (result.status !== 'Active') { setReady(false); return }
        await demoRequest(`${path}/heartbeat`, session.key, { stage: stage.current }, 'POST', controller.signal)
      } catch (caught) {
        if (!controller.signal.aborted) {
          if (caught instanceof DemoRequestError && caught.status === 404) { setReady(false); setRevoked(true); setStatusError(null) }
          else setStatusError('Live room status is temporarily unavailable. Reconnect when your connection returns.')
        }
      }
      finally { if (!controller.signal.aborted) timer = setTimeout(() => void poll(), 3000) }
    }
    void poll()
    return () => { controller.abort(); clearTimeout(timer); clearTimeout(expiration) }
  }, [participant, path, runId, session.key])

  const connectionChanged = useCallback((connected: boolean) => { stage.current = connected ? 'Connected' : 'DevicesReady' }, [])
  async function prepare() {
    setBusy(true); setError(null)
    try {
      const check = await runTelehealthDevicePreflight()
      if (check.status === 'failed') throw new Error(check.message)
      const current = await demoRequest<DemoStatus>(`/runs/${runId}`, session.key)
      if (current.status !== 'Active') throw new Error('This demo has ended or expired. Create a new demo.')
      stage.current = 'DevicesReady'; setReady(true)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Check your devices and try again.') }
    finally { setBusy(false) }
  }
  async function end() {
    setBusy(true)
    try { setStatus(await demoRequest<DemoStatus>(`/runs/${runId}/end`, session.key, {})); setReady(false); setError(null) }
    catch { setError('The demo could not be ended. Check your connection and try again.') }
    finally { setBusy(false) }
  }
  const grant = useMemo<TelehealthConnectionGrant | null>(() => participant ? {
    sessionId: runId, grantId: participant.participantId, requestId: runId, requestVersion: 1, requestStatus: 'Connecting',
    participantRole: role as DemoRole, adapterMode: 'NON_PRODUCTION', joinCredential: session.key,
    expiresAt: participant.expiresAt, recordingEnabled: false, transcriptionEnabled: false,
    mediaTransportEnabled: true, mediaTransportMode: participant.transport, waitingRoomMessage: 'Synthetic connection demo', limitations: [],
  } : null, [participant, runId, role, session.key])
  const ended = status?.status === 'Ended' || status?.status === 'Expired' || expired || revoked
  const visibleError = error ?? statusError
  const terminationMessage = status?.status === 'Ended' ? 'This synthetic demo has ended.'
    : status?.status === 'Expired' || expired ? 'This synthetic demo has expired.' : 'This synthetic demo is no longer available.'
  return <main className="telehealth-page" id="main-content">
    <h1>Synthetic {role === 'physician' ? 'physician' : 'patient'} session</h1>
    <p className="telehealth-synthetic" role="note">Connection demo only. You are using a fictional role with no access to a patient chart or clinical workflow. Audio and video are not recorded by AvenChart.</p>
    {visibleError ? <p className="telehealth-error" role="alert">{visibleError}</p> : null}
    {!valid ? <p role="alert">This demo invitation is invalid.</p> : !participant ? <section><p>Opening your invitation…</p>{error ? <button className="telehealth-button" onClick={() => setAttempt(value => value + 1)}>Retry invitation</button> : null}</section> : ended ? <p role="status">{terminationMessage} The camera and microphone are released.</p> : <>
      <p>Room <strong>{runId.slice(0, 8)}</strong>. Expires <time dateTime={participant.expiresAt}>{new Date(participant.expiresAt).toLocaleTimeString()}</time>.</p>
      {status ? <p aria-live="polite">Patient: {demoStageLabel[status.patientStatus]}. Physician: {demoStageLabel[status.physicianStatus]}.</p> : null}
      {!ready ? <button className="telehealth-button" disabled={busy} onClick={() => void prepare()}>{busy ? 'Checking devices…' : 'Check devices and enter room'}</button> : null}
      {ready && grant ? participant.transport === 'NON_PRODUCTION_INTERNET_ACS_CALLING_POC'
        ? <Suspense fallback={<p role="status">Loading video controls…</p>}><InternetCall grant={grant} role={role as DemoRole} getCallingConfiguration={() => demoRequest<TelehealthInternetCallingConfiguration>(`${path}/calling`, session.key, {})} onConnectionStateChange={connectionChanged} /></Suspense>
        : <TelehealthLocalWebRtcPocPanel grant={grant} role={role as DemoRole} writeSignal={(kind, payload) => demoRequest(`${path}/signals/write`, session.key, { kind, payload })} readSignals={(afterSequence, signal) => demoRequest<TelehealthLocalWebRtcSignalRead>(`${path}/signals/read`, session.key, { afterSequence }, 'POST', signal)} onConnectionStateChange={connectionChanged} /> : null}
      <button className="telehealth-button telehealth-button-danger" disabled={busy} onClick={() => void end()}>End this demo</button>
    </>}
    <p><Link to="/telehealth/demo">Return to demo launcher</Link></p>
  </main>
}
