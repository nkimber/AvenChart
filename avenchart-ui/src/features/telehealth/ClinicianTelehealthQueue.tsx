// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { abandonTelehealthConnection, endIdleClinicianShift, enterTelehealthConsultationWrapUp, getClinicianActiveWork, getPhysicianInternetCallingConfiguration, getTelehealthConsultationWorkspace, listClinicianQueue, preparePhysicianConnection, readPhysicianLocalWebRtcSignals, releaseTelehealthReservation, reserveNextRequest, saveTelehealthConsultationDocumentationDraft, startClinicianShift, startTelehealthConsultation, writePhysicianLocalWebRtcSignal, type TelehealthConnectionGrant, type TelehealthConsultationStartInput, type TelehealthConsultationWorkspace, type TelehealthDevicePreflight, type TelehealthQueueItem, type TelehealthReservation, type TelehealthShift } from './api.ts'
import { isRequestCancellation } from '../../api/transport.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import TelehealthPharmacyChoicePanel from './TelehealthPharmacyChoicePanel.tsx'
import TelehealthPrescriptionPreparationPanel from './TelehealthPrescriptionPreparationPanel.tsx'
import TelehealthSafetyDispositionPanel from './TelehealthSafetyDispositionPanel.tsx'
import TelehealthCompletionPrerequisitesPanel from './TelehealthCompletionPrerequisitesPanel.tsx'
import TelehealthConversationPanel from './TelehealthConversationPanel.tsx'
import TelehealthLocalWebRtcPocPanel from './TelehealthLocalWebRtcPocPanel.tsx'
import TelehealthFinalClinicalReviewPanel from './TelehealthFinalClinicalReviewPanel.tsx'
import TelehealthProfessionalClaimPreparationPanel from './TelehealthProfessionalClaimPreparationPanel.tsx'
import TelehealthEncounterFinalizationPanel from './TelehealthEncounterFinalizationPanel.tsx'
import TelehealthSyntheticVisitClosurePanel from './TelehealthSyntheticVisitClosurePanel.tsx'
import ClinicianIdleShiftEndControl, { type ClinicianIdleShiftEndConfirmations } from './ClinicianIdleShiftEndControl.tsx'
import ClinicianReservationReleaseControl, { type ClinicianReservationReleaseConfirmations } from './ClinicianReservationReleaseControl.tsx'
import ClinicianConnectionAbandonControl, { type ClinicianConnectionAbandonConfirmations } from './ClinicianConnectionAbandonControl.tsx'
import './telehealth.css'

const TelehealthInternetCallingPocPanel = lazy(() => import('./TelehealthInternetCallingPocPanel.tsx'))

export default function ClinicianTelehealthQueue() {
  type DraftFields = { subjective: string; objective: string; assessment: string; plan: string }
  const emptyDraft: DraftFields = { subjective: '', objective: '', assessment: '', plan: '' }
  const [items, setItems] = useState<TelehealthQueueItem[]>([])
  const [shift, setShift] = useState<TelehealthShift | null>(null)
  const [reservation, setReservation] = useState<TelehealthReservation | null>(null)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [connectionWorking, setConnectionWorking] = useState(false)
  const [deviceEvidence, setDeviceEvidence] = useState<TelehealthDevicePreflight | null>(null)
  const [waitingRoom, setWaitingRoom] = useState<TelehealthConnectionGrant | null>(null)
  const [localMediaConnected, setLocalMediaConnected] = useState(false)
  const [consultation, setConsultation] = useState<{ consultationId: string; limitations: string[] } | null>(null)
  const [workspace, setWorkspace] = useState<TelehealthConsultationWorkspace | null>(null)
  const [encounterLocked, setEncounterLocked] = useState(false)
  const [closureStatus, setClosureStatus] = useState<string | null>(null)
  const [workspaceLoading, setWorkspaceLoading] = useState(false)
  const [workspaceError, setWorkspaceError] = useState<string | null>(null)
  const [draft, setDraft] = useState<DraftFields>(emptyDraft)
  const [draftVersion, setDraftVersion] = useState(0)
  const [draftDirty, setDraftDirty] = useState(false)
  const [draftSaving, setDraftSaving] = useState(false)
  const [draftStatus, setDraftStatus] = useState<string | null>(null)
  const [draftError, setDraftError] = useState<string | null>(null)
  const [draftReloadPending, setDraftReloadPending] = useState(false)
  const [wrapUpChecks, setWrapUpChecks] = useState({
    syntheticSessionEndedConfirmed: false,
    documentationStillIncompleteAcknowledged: false,
    wrapUpResponsibilityAcknowledged: false,
  })
  const [wrapUpWorking, setWrapUpWorking] = useState(false)
  const [wrapUpStatus, setWrapUpStatus] = useState<string | null>(null)
  const [wrapUpError, setWrapUpError] = useState<string | null>(null)
  const [startChecklist, setStartChecklist] = useState<Omit<TelehealthConsultationStartInput, 'expectedVersion' | 'syntheticDataConfirmed'>>({
    patientLocationState: 'GA', patientIdentityDiscussed: false, callbackConfirmed: false,
    privacyConfirmed: false, consentDiscussed: false, noConcerningSymptomChange: false,
    emergencyPlanConfirmed: false, communicationSufficient: false,
  })
  const generation = useRef(0)
  const connectionCommandKey = useRef<string | null>(null)
  const consultationCommandKey = useRef<string | null>(null)
  const wrapUpCommandKey = useRef<string | null>(null)
  const startReady = startChecklist.patientIdentityDiscussed && startChecklist.callbackConfirmed
    && startChecklist.privacyConfirmed && startChecklist.consentDiscussed
    && startChecklist.noConcerningSymptomChange && startChecklist.emergencyPlanConfirmed
    && startChecklist.communicationSufficient
  const wrapUpReady = Object.values(wrapUpChecks).every(Boolean)

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const current = ++generation.current
    setLoading(true)
    setError(null)
    try {
      const [result, activeWork] = await Promise.all([
        listClinicianQueue(signal),
        getClinicianActiveWork(signal),
      ])
      if (current !== generation.current) return
      setItems(result)
      setShift(activeWork.shift)
      setReservation(activeWork.reservation)
    } catch (caught) {
      if (isRequestCancellation(caught) || current !== generation.current) return
      setItems([])
      setError(caught instanceof Error ? caught.message : 'The clinician queue could not be loaded.')
    } finally {
      if (current === generation.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void refresh(controller.signal)
    const timer = window.setInterval(() => void refresh(controller.signal), 10_000)
    return () => { controller.abort(); window.clearInterval(timer) }
  }, [refresh])

  async function start() {
    setWorking(true); setError(null)
    try { setShift(await startClinicianShift()) }
    catch (caught) { setShift(null); setError(caught instanceof Error ? caught.message : 'The telehealth shift could not start.') }
    finally { setWorking(false) }
  }

  async function reserve() {
    setWorking(true); setError(null)
    try {
      setReservation(await reserveNextRequest()); setConsultation(null); setWorkspace(null); setWorkspaceError(null); setEncounterLocked(false); setClosureStatus(null); resetDraft()
      setWrapUpChecks({ syntheticSessionEndedConfirmed: false, documentationStillIncompleteAcknowledged: false, wrapUpResponsibilityAcknowledged: false })
      setWrapUpStatus(null); setWrapUpError(null); wrapUpCommandKey.current = null
      await refresh()
    }
    catch (caught) { setReservation(null); setItems([]); setError(caught instanceof Error ? caught.message : 'The next request could not be reserved.') }
    finally { setWorking(false) }
  }

  async function endShift(confirmations: ClinicianIdleShiftEndConfirmations) {
    if (!shift || reservation || consultation || working || !confirmations.noActiveWorkConfirmed || !confirmations.syntheticEndConfirmed) return
    setWorking(true); setError(null)
    try { await endIdleClinicianShift(shift.shiftId, shift.version, confirmations.noActiveWorkConfirmed, confirmations.syntheticEndConfirmed); setShift(null); setClosureStatus('Synthetic telehealth shift ended. No patient, appointment, encounter, clinical, billing, claim, media, integration, or external state changed.') }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'The idle shift could not be ended.') }
    finally { setWorking(false) }
  }

  async function releaseReservation(confirmations: ClinicianReservationReleaseConfirmations) {
    if (!reservation || waitingRoom || consultation || working || connectionWorking
      || !confirmations.noConnectionOrConsultationConfirmed || !confirmations.syntheticReleaseConfirmed) return
    setWorking(true); setError(null)
    try {
      await releaseTelehealthReservation(
        reservation.reservationId,
        reservation.requestVersion,
        confirmations.noConnectionOrConsultationConfirmed,
        confirmations.syntheticReleaseConfirmed,
      )
      setReservation(null); setDeviceEvidence(null); setWaitingRoom(null); setLocalMediaConnected(false)
      setClosureStatus('Synthetic reservation released back to the existing queue. No connection, consultation, clinical, billing, claim, media, integration, or external state changed.')
      await refresh()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The synthetic reservation could not be released. Refresh before retrying.')
    } finally { setWorking(false) }
  }

  async function abandonConnection(confirmations: ClinicianConnectionAbandonConfirmations) {
    if (!reservation || !waitingRoom || consultation || working || connectionWorking
      || !confirmations.noConsultationConfirmed || !confirmations.syntheticConnectionAbandonConfirmed) return
    setWorking(true); setError(null)
    try {
      await abandonTelehealthConnection(
        reservation.reservationId,
        reservation.requestVersion,
        confirmations.noConsultationConfirmed,
        confirmations.syntheticConnectionAbandonConfirmed,
      )
      setReservation(null); setDeviceEvidence(null); setWaitingRoom(null); setLocalMediaConnected(false)
      connectionCommandKey.current = null
      setClosureStatus('Synthetic connection attempt abandoned and the request returned to the existing queue. Pending local grants and the synthetic session ended; no consultation, clinical, billing, claim, integration, or external state changed.')
      await refresh()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The synthetic connection attempt could not be abandoned. Refresh before retrying.')
    } finally { setWorking(false) }
  }

  async function beginSyntheticConsultation() {
    if (!reservation || !waitingRoom || connectionWorking) return
    setConnectionWorking(true); setError(null)
    consultationCommandKey.current ??= crypto.randomUUID()
    try {
      const result = await startTelehealthConsultation(reservation.reservationId, {
        expectedVersion: reservation.requestVersion,
        ...startChecklist,
        syntheticDataConfirmed: true,
      }, consultationCommandKey.current)
      setConsultation({ consultationId: result.consultationId, limitations: result.limitations })
      setReservation((current) => current ? { ...current, requestVersion: result.requestVersion, status: 'Released' } : current)
      consultationCommandKey.current = null
      await loadWorkspace(result.consultationId)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The synthetic consultation lifecycle could not start.')
    } finally { setConnectionWorking(false) }
  }

  async function loadWorkspace(consultationId: string) {
    setWorkspaceLoading(true); setWorkspaceError(null)
    try {
      const result = await getTelehealthConsultationWorkspace(consultationId)
      setWorkspace(result)
      replaceDraft(result)
    }
    catch (caught) {
      setWorkspace(null)
      setWorkspaceError(caught instanceof Error ? caught.message : 'The read-only consultation workspace could not be loaded.')
    } finally { setWorkspaceLoading(false) }
  }

  function resetDraft() {
    setDraft(emptyDraft)
    setDraftVersion(0)
    setDraftDirty(false)
    setDraftSaving(false)
    setDraftStatus(null)
    setDraftError(null)
    setDraftReloadPending(false)
  }

  function replaceDraft(current: TelehealthConsultationWorkspace) {
    setDraft({
      subjective: current.documentation.subjective ?? '',
      objective: current.documentation.objective ?? '',
      assessment: current.documentation.assessment ?? '',
      plan: current.documentation.plan ?? '',
    })
    setDraftVersion(current.documentation.version)
    setDraftDirty(false)
    setDraftError(null)
    setDraftStatus(current.documentation.version > 0 ? `Current saved draft version ${current.documentation.version} loaded.` : 'No saved draft. Start with blank fields.')
    setDraftReloadPending(false)
  }

  function updateDraft(name: keyof DraftFields, value: string) {
    setDraft((current) => ({ ...current, [name]: value }))
    setDraftDirty(true)
    setDraftStatus('Unsaved changes. Nothing is saved automatically.')
    setDraftError(null)
  }

  function requestWorkspaceReload() {
    if (!consultation) return
    if (draftDirty) {
      setDraftReloadPending(true)
      return
    }
    void loadWorkspace(consultation.consultationId)
  }

  async function saveDraft() {
    if (!consultation || !workspace || draftSaving || workspace.documentation.isLocked) return
    setDraftSaving(true); setDraftError(null); setDraftStatus('Saving unsigned synthetic draft…')
    try {
      const saved = await saveTelehealthConsultationDocumentationDraft(consultation.consultationId, {
        expectedVersion: draftVersion,
        subjective: draft.subjective.trim() || null,
        objective: draft.objective.trim() || null,
        assessment: draft.assessment.trim() || null,
        plan: draft.plan.trim() || null,
      })
      setWorkspace((current) => current ? { ...current, documentation: saved } : current)
      setDraft({ subjective: saved.subjective ?? '', objective: saved.objective ?? '', assessment: saved.assessment ?? '', plan: saved.plan ?? '' })
      setDraftVersion(saved.version)
      setDraftDirty(false)
      setDraftStatus(`Unsigned synthetic draft version ${saved.version} saved. It is not final or patient-visible.`)
    } catch (caught) {
      setDraftError(caught instanceof Error ? caught.message : 'The unsigned synthetic draft could not be saved. Reload the current draft before retrying a version conflict.')
      setDraftStatus('Draft not saved. Your typed changes remain in this page only.')
    } finally { setDraftSaving(false) }
  }

  async function enterWrapUp() {
    if (!consultation || !workspace || workspace.consultationStatus !== 'InConsultation' || wrapUpWorking || !wrapUpReady) return
    setWrapUpWorking(true); setWrapUpError(null); setWrapUpStatus('Moving the unfinished synthetic visit into physician-owned wrap-up…')
    wrapUpCommandKey.current ??= crypto.randomUUID()
    try {
      const result = await enterTelehealthConsultationWrapUp(consultation.consultationId, {
        expectedVersion: workspace.consultationVersion,
        syntheticSessionEndedConfirmed: true,
        documentationStillIncompleteAcknowledged: true,
        wrapUpResponsibilityAcknowledged: true,
      }, wrapUpCommandKey.current)
      setWorkspace((current) => current ? {
        ...current,
        consultationStatus: 'WrapUp',
        consultationVersion: result.version,
        mediaEndedAt: result.mediaEndedAt,
        asOf: result.mediaEndedAt,
      } : current)
      setReservation((current) => current ? { ...current, requestVersion: result.requestVersion } : current)
      setShift((current) => current ? { ...current, status: result.shiftStatus } : current)
      setConsultation((current) => current ? { ...current, limitations: result.limitations } : current)
      setWrapUpStatus('Wrap-up entered. This synthetic visit is still unfinished, and you remain responsible and unavailable for new work.')
      wrapUpCommandKey.current = null
    } catch (caught) {
      setWrapUpError(caught instanceof Error ? caught.message : 'The consultation could not enter wrap-up. Reload the workspace before retrying a conflict.')
      setWrapUpStatus('Wrap-up not entered. No completion or clinician release occurred.')
    } finally { setWrapUpWorking(false) }
  }

  function setStartCheck(name: keyof typeof startChecklist, value: boolean | 'GA' | 'CA' | 'FL') {
    setStartChecklist((current) => ({ ...current, [name]: value }))
  }

  async function checkDevices() {
    setConnectionWorking(true); setError(null); setWaitingRoom(null); setLocalMediaConnected(false)
    try {
      const result = await runTelehealthDevicePreflight()
      if (result.status === 'failed') { setDeviceEvidence(null); setError(result.message); return }
      setDeviceEvidence(result.evidence)
    } finally { setConnectionWorking(false) }
  }

  async function enterWaitingRoom() {
    if (!reservation || !deviceEvidence || connectionWorking) return
    setConnectionWorking(true); setError(null)
    connectionCommandKey.current ??= crypto.randomUUID()
    try {
      const result = await preparePhysicianConnection(
        reservation.reservationId,
        reservation.requestVersion,
        deviceEvidence,
        connectionCommandKey.current,
      )
      setWaitingRoom(result); setLocalMediaConnected(false)
      setReservation((current) => current ? { ...current, requestVersion: result.requestVersion } : current)
      connectionCommandKey.current = null
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The physician connection room could not be prepared.')
    } finally { setConnectionWorking(false) }
  }

  return (
    <main className="telehealth-page" aria-labelledby="clinician-telehealth-title">
      <header className="telehealth-heading"><div><p className="telehealth-kicker">Physician workspace</p><h1 id="clinician-telehealth-title">Telehealth clinician queue</h1></div><button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void refresh()} disabled={loading}>Refresh</button></header>
      <div className="telehealth-synthetic" role="note">Synthetic lifecycle demonstration only. No real consultation, media, prescribing, transmission, claims, completion, or patient care is enabled. Bounded chart, pharmacy, disposition, completion-review, and unsigned prescription-preparation drafts are available without legal effect.</div>
      {error ? <p className="telehealth-error" role="alert">{error}</p> : null}
      <section className="telehealth-card telehealth-actions" aria-live="polite">
        <div><h2>Telehealth shift</h2><p>{shift ? `${shift.status} at facility ${shift.facilityId}` : 'Start a shift before reserving a request.'}</p></div>
        <button className="telehealth-button" type="button" disabled={working || shift !== null} onClick={() => void start()}>{shift ? 'Shift active' : 'Start telehealth shift'}</button>
        <button className="telehealth-button" type="button" disabled={working || shift === null || reservation !== null} onClick={() => void reserve()}>Reserve next request</button>
        {shift ? <ClinicianIdleShiftEndControl shift={shift} reservationActive={reservation !== null} consultationActive={consultation !== null} working={working} onEnd={(confirmations) => void endShift(confirmations)} /> : null}
      </section>
      {closureStatus ? <p role="status">{closureStatus}</p> : null}
      {reservation ? (
        <section className="telehealth-card" aria-labelledby="reserved-title">
          <h2 id="reserved-title">Reserved synthetic request</h2>
          <p>Request {reservation.requestId.slice(0, 8)}</p>
          {reservation.applicantOriginated ? <p><strong>New-patient applicant request.</strong> This reservation matched the exact current synthetic rendering-candidate evidence. It is not real credentialing, network confirmation, consent, or care authorization.</p> : null}
          <p>Lease expires {new Date(reservation.leaseExpiresAt).toLocaleTimeString()}.</p>
          <p>The connection room is provider-neutral and transports no media. After the start handoff, only an audited, bounded chart projection and unsigned SOAP draft are available; general chart navigation and all other clinical actions remain unavailable.</p>
          {!waitingRoom && !consultation ? <ClinicianReservationReleaseControl reservation={reservation} disabled={working || connectionWorking} onRelease={(confirmations) => void releaseReservation(confirmations)} /> : null}
          <section className="telehealth-connection-room" aria-labelledby="physician-device-check-title">
            <h3 id="physician-device-check-title">Physician device check</h3>
            <p>The user-initiated test requests camera and microphone access and immediately stops all test tracks. No device names or media are retained.</p>
            <div className="telehealth-actions">
              <button className="telehealth-button telehealth-button-secondary" type="button" disabled={connectionWorking} onClick={() => void checkDevices()}>{deviceEvidence ? 'Run device check again' : 'Check camera and microphone'}</button>
              <button className="telehealth-button" type="button" disabled={connectionWorking || !deviceEvidence} onClick={() => void enterWaitingRoom()}>Enter physician waiting room</button>
            </div>
            {deviceEvidence ? <p className="telehealth-preflight-passed" role="status">Device check passed. Network indication: {deviceEvidence.networkQuality}.</p> : null}
            {waitingRoom ? <div className="telehealth-waiting-room" role="status"><h4>Physician grant ready</h4><p>{waitingRoom.waitingRoomMessage}</p><p><small>Local grant expires {new Date(waitingRoom.expiresAt).toLocaleTimeString()}.</small></p><ul>{waitingRoom.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul></div> : null}
            {waitingRoom?.mediaTransportEnabled && waitingRoom.mediaTransportMode === 'NON_PRODUCTION_INTERNET_ACS_CALLING_POC' ? <Suspense fallback={<p role="status">Loading the synthetic internet calling controls…</p>}><TelehealthInternetCallingPocPanel grant={waitingRoom} role="physician" getCallingConfiguration={() => getPhysicianInternetCallingConfiguration(waitingRoom)} onConnectionStateChange={setLocalMediaConnected} /></Suspense> : null}
            {waitingRoom?.mediaTransportEnabled && waitingRoom.mediaTransportMode === 'NON_PRODUCTION_LOCAL_WEBRTC_POC' ? <TelehealthLocalWebRtcPocPanel grant={waitingRoom} role="physician" writeSignal={(kind, payload) => writePhysicianLocalWebRtcSignal(waitingRoom, kind, payload)} readSignals={(afterSequence, signal) => readPhysicianLocalWebRtcSignals(waitingRoom, afterSequence, signal)} onConnectionStateChange={setLocalMediaConnected} /> : null}
          </section>
          {waitingRoom && !consultation ? <ClinicianConnectionAbandonControl reservation={reservation} disabled={working || connectionWorking} onAbandon={(confirmations) => void abandonConnection(confirmations)} /> : null}
          {waitingRoom && !consultation ? (
            <form className="telehealth-consultation-start" onSubmit={(event) => { event.preventDefault(); void beginSyntheticConsultation() }}>
              <fieldset>
                <legend>Synthetic consultation-start checklist</legend>
                <p>This records coarse lifecycle evidence only. It is not identity proofing, legal consent, a clinical assessment, or proof that media is connected.</p>
                {reservation.applicantOriginated ? <p role="note"><strong>New-patient financial evidence:</strong> start requires the still-current exact synthetic eligibility and rendering-candidate chain. This is not real coverage verification or a payment guarantee.</p> : null}
                <label>Reconfirmed patient state<select value={startChecklist.patientLocationState} onChange={(event) => setStartCheck('patientLocationState', event.target.value as 'GA' | 'CA' | 'FL')}><option value="GA">Georgia</option><option value="CA">California</option><option value="FL">Florida</option></select></label>
                {([
                  ['patientIdentityDiscussed', 'Patient identity discussion completed'],
                  ['callbackConfirmed', 'Callback number reconfirmed'],
                  ['privacyConfirmed', 'Privacy and other participants discussed'],
                  ['consentDiscussed', 'Telehealth consent discussion completed (no legal effect here)'],
                  ['noConcerningSymptomChange', 'No concerning symptom change or red flag reported'],
                  ['emergencyPlanConfirmed', 'Emergency plan reviewed'],
                  ['communicationSufficient', 'Synthetic communication check is sufficient'],
                ] as const).map(([name, label]) => <label className="telehealth-check" key={name}><input type="checkbox" checked={startChecklist[name]} onChange={(event) => setStartCheck(name, event.target.checked)} />{label}</label>)}
              </fieldset>
              {waitingRoom.mediaTransportEnabled && !localMediaConnected ? <p className="telehealth-inline-warning" role="note">Connect the synthetic browser media POC before starting this synthetic lifecycle.</p> : null}
              <button className="telehealth-button" type="submit" disabled={connectionWorking || !startReady || (waitingRoom.mediaTransportEnabled && !localMediaConnected)}>Start synthetic lifecycle</button>
            </form>
          ) : null}
          {consultation ? <section className="telehealth-consultation-started" role="status"><h3>Synthetic consultation lifecycle started</h3><p>Opaque consultation {consultation.consultationId.slice(0, 8)}. The sequential encounter key is not exposed.</p><ul>{consultation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul></section> : null}
          {consultation ? (
            <section className="telehealth-consultation-workspace" aria-labelledby="consultation-workspace-title" aria-busy={workspaceLoading}>
              <div className="telehealth-heading"><div><p className="telehealth-kicker">Audited bounded workspace</p><h3 id="consultation-workspace-title">Consultation workspace</h3></div><button className="telehealth-button telehealth-button-secondary" type="button" disabled={workspaceLoading} onClick={requestWorkspaceReload}>Reload workspace</button></div>
              <p role="note">Verify every projected item with the patient. Empty lists are not confirmed negative histories. Only the unsigned SOAP draft below can write to this synthetic encounter.</p>
              {workspaceLoading ? <p role="status">Loading the read-only projection…</p> : null}
              {workspaceError ? <p className="telehealth-error" role="alert">{workspaceError}</p> : null}
              {workspace ? <>
                <p className="telehealth-workspace-state" role="status"><strong>Lifecycle: {workspace.consultationStatus === 'WrapUp' ? 'unfinished wrap-up' : 'synthetic consultation'}.</strong> Version {workspace.consultationVersion}.{workspace.mediaEndedAt ? ` Synthetic session ended ${new Date(workspace.mediaEndedAt).toLocaleString()}.` : ''}</p>
                <div className="telehealth-workspace-grid">
                  <section aria-labelledby="workspace-patient-title"><h4 id="workspace-patient-title">Patient and callback</h4><dl><div><dt>Name</dt><dd>{workspace.patient.displayName}</dd></div><div><dt>Date of birth</dt><dd>{workspace.patient.dateOfBirth} (age {workspace.patient.age})</dd></div><div><dt>Recorded sex</dt><dd>{workspace.patient.recordedSex || 'Not returned'}</dd></div><div><dt>Callback</dt><dd>{workspace.patient.callbackPhone || 'Not returned — verify verbally'}</dd></div></dl></section>
                  <section aria-labelledby="workspace-visit-title"><h4 id="workspace-visit-title">Current visit</h4><dl><div><dt>Physical location</dt><dd>{workspace.visit.patientLocationState}</dd></div><div><dt>Purpose</dt><dd>{workspace.visit.complaintCategory}</dd></div><div><dt>Patient summary</dt><dd>{workspace.visit.complaintSummary}</dd></div><div><dt>Duration</dt><dd>{workspace.visit.symptomDuration}</dd></div><div><dt>Triage</dt><dd>{workspace.visit.triageOutcome}</dd></div></dl></section>
                </div>
                <div className="telehealth-workspace-lists">
                  <section aria-labelledby="workspace-allergies-title"><h4 id="workspace-allergies-title">Active allergies</h4>{workspace.allergies.length ? <ul>{workspace.allergies.map((item) => <li key={`${item.title}-${item.reaction ?? ''}`}><strong>{item.title}</strong>{item.reaction ? ` — ${item.reaction}` : ''}{item.severity ? ` (${item.severity})` : ''}</li>)}</ul> : <p>No active entry returned — verify verbally.</p>}</section>
                  <section aria-labelledby="workspace-medications-title"><h4 id="workspace-medications-title">Active medications</h4>{workspace.medications.length ? <ul>{workspace.medications.map((item) => <li key={item.title}>{item.title}</li>)}</ul> : <p>No active entry returned — verify verbally.</p>}</section>
                  <section aria-labelledby="workspace-problems-title"><h4 id="workspace-problems-title">Active problems</h4>{workspace.problems.length ? <ul>{workspace.problems.map((item) => <li key={`${item.title}-${item.diagnosis ?? ''}`}><strong>{item.title}</strong>{item.diagnosis && item.diagnosis !== item.title ? ` — ${item.diagnosis}` : ''}</li>)}</ul> : <p>No active entry returned — verify verbally.</p>}</section>
                </div>
                {workspace.consultationStatus === 'InConsultation' ? <TelehealthConversationPanel participant="physician" consultationId={consultation.consultationId} /> : null}
                <form className="telehealth-documentation-draft" aria-labelledby="documentation-draft-title" onSubmit={(event) => { event.preventDefault(); void saveDraft() }}>
                  <div><p className="telehealth-kicker">Explicit save only</p><h4 id="documentation-draft-title">Unsigned SOAP draft</h4></div>
                  <p role="note">Synthetic and incomplete. Nothing is saved automatically. Enter only facts you actually obtained; blank fields make no clinical assertion. This draft is not signed, final, coded, prescribed, billed, or patient-visible.</p>
                  <div className="telehealth-documentation-grid">
                    <label>Subjective<textarea maxLength={10000} value={draft.subjective} onChange={(event) => updateDraft('subjective', event.target.value)} disabled={workspace.documentation.isLocked || draftSaving} /></label>
                    <label>Objective<textarea maxLength={10000} value={draft.objective} onChange={(event) => updateDraft('objective', event.target.value)} disabled={workspace.documentation.isLocked || draftSaving} /></label>
                    <label>Assessment<textarea maxLength={10000} value={draft.assessment} onChange={(event) => updateDraft('assessment', event.target.value)} disabled={workspace.documentation.isLocked || draftSaving} /></label>
                    <label>Plan<textarea maxLength={10000} value={draft.plan} onChange={(event) => updateDraft('plan', event.target.value)} disabled={workspace.documentation.isLocked || draftSaving} /></label>
                  </div>
                  <p><small>Current server version: {draftVersion}. {workspace.documentation.savedAt ? `Saved ${new Date(workspace.documentation.savedAt).toLocaleString()} by ${workspace.documentation.savedBy || 'authenticated physician'}.` : 'No saved version.'}</small></p>
                  {workspace.documentation.isLocked ? <p className="telehealth-error" role="alert">This encounter has a locking signature. Ordinary draft changes are unavailable.</p> : null}
                  {draftStatus ? <p role="status">{draftStatus}</p> : null}
                  {draftError ? <p className="telehealth-error" role="alert">{draftError}</p> : null}
                  {draftReloadPending ? <section className="telehealth-draft-reload" role="alert" aria-labelledby="draft-reload-title"><h5 id="draft-reload-title">Unsaved changes</h5><p>Reloading will replace the text currently in these fields with the last server version.</p><div className="telehealth-actions"><button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => setDraftReloadPending(false)}>Keep editing</button><button className="telehealth-button" type="button" onClick={() => void loadWorkspace(consultation.consultationId)}>Replace with saved draft</button></div></section> : null}
                  <button className="telehealth-button" type="submit" disabled={draftSaving || !draftDirty || workspace.documentation.isLocked || !Object.values(draft).some((value) => value.trim().length > 0)}>{draftSaving ? 'Saving draft…' : 'Save unsigned draft'}</button>
                </form>
                {workspace.consultationStatus === 'InConsultation' ? (
                  <form className="telehealth-wrap-up-handoff" aria-labelledby="wrap-up-handoff-title" onSubmit={(event) => { event.preventDefault(); void enterWrapUp() }}>
                    <div><p className="telehealth-kicker">Consequential lifecycle action</p><h4 id="wrap-up-handoff-title">Enter unfinished wrap-up</h4></div>
                    <p role="note">This ends only the synthetic session state. It does not document a final clinical disposition, complete the appointment or encounter, sign the draft, release you to new work, notify the patient, or create downstream work.</p>
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check"><input type="checkbox" checked={wrapUpChecks.syntheticSessionEndedConfirmed} onChange={(event) => setWrapUpChecks((current) => ({ ...current, syntheticSessionEndedConfirmed: event.target.checked }))} />The synthetic session is ended; this makes no claim about real media.</label>
                      <label className="telehealth-check"><input type="checkbox" checked={wrapUpChecks.documentationStillIncompleteAcknowledged} onChange={(event) => setWrapUpChecks((current) => ({ ...current, documentationStillIncompleteAcknowledged: event.target.checked }))} />Documentation and any future safety disposition remain incomplete.</label>
                      <label className="telehealth-check"><input type="checkbox" checked={wrapUpChecks.wrapUpResponsibilityAcknowledged} onChange={(event) => setWrapUpChecks((current) => ({ ...current, wrapUpResponsibilityAcknowledged: event.target.checked }))} />I remain responsible for this unfinished synthetic visit and unavailable for new work.</label>
                    </fieldset>
                    {wrapUpStatus ? <p role="status">{wrapUpStatus}</p> : null}
                    {wrapUpError ? <p className="telehealth-error" role="alert">{wrapUpError}</p> : null}
                    <button className="telehealth-button" type="submit" disabled={wrapUpWorking || !wrapUpReady}>{wrapUpWorking ? 'Entering wrap-up…' : 'End synthetic session and enter wrap-up'}</button>
                  </form>
                ) : (
                  <section className="telehealth-wrap-up-handoff" role="status" aria-labelledby="wrap-up-active-title">
                    <p className="telehealth-kicker">Physician-owned unfinished work</p><h4 id="wrap-up-active-title">Wrap-up is active</h4>
                    <p>This visit is not complete. Continue the unsigned SOAP note as needed. The pharmacy, prescription-preparation, and safety-disposition sections below record planning drafts only; safety checking, signing, transmission, patient delivery, finalization, and clinician release remain unavailable.</p>
                    {wrapUpStatus ? <p>{wrapUpStatus}</p> : null}
                  </section>
                )}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthPharmacyChoicePanel consultationId={consultation.consultationId} patientState={workspace.visit.patientLocationState} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthPrescriptionPreparationPanel consultationId={consultation.consultationId} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthSafetyDispositionPanel consultationId={consultation.consultationId} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthFinalClinicalReviewPanel consultationId={consultation.consultationId} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthEncounterFinalizationPanel consultationId={consultation.consultationId} onFinalized={() => setEncounterLocked(true)} /> : null}
                {workspace.consultationStatus === 'WrapUp' && encounterLocked ? <TelehealthSyntheticVisitClosurePanel consultationId={consultation.consultationId} expectedVersion={workspace.consultationVersion} onClosed={(result) => {
                  setReservation(null); setConsultation(null); setWorkspace(null); setEncounterLocked(false); setDeviceEvidence(null); setWaitingRoom(null); setLocalMediaConnected(false); resetDraft()
                  setShift((current) => current ? { ...current, status: result.clinicianAvailableForNewWork ? 'Active' : current.status } : current)
                  setClosureStatus(result.clinicianAvailableForNewWork
                    ? 'Synthetic visit lifecycle closed. You are available for new work; the appointment and encounter remain incomplete, and no delivery, billing, claim, integration, or external action was created.'
                    : 'Synthetic visit lifecycle closed. The appointment and encounter remain incomplete, and no delivery, billing, claim, integration, or external action was created.')
                  void refresh()
                }} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthProfessionalClaimPreparationPanel consultationId={consultation.consultationId} /> : null}
                {workspace.consultationStatus === 'WrapUp' ? <TelehealthCompletionPrerequisitesPanel consultationId={consultation.consultationId} /> : null}
                <p><small>Projection as of {new Date(workspace.asOf).toLocaleString()}.</small></p>
                <ul>{workspace.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
              </> : null}
            </section>
          ) : null}
        </section>
      ) : null}
      <section className="telehealth-card" aria-busy={loading}><h2>Ready queue</h2>{loading ? <p aria-live="polite">Refreshing queue…</p> : null}{!loading && items.length === 0 ? <p>No eligible requests are ready.</p> : null}<ol className="telehealth-queue">{items.map((item, index) => <li key={item.requestId}><div><strong>Position {index + 1}: {item.complaintCategory}</strong><span>{item.status}</span><small>{item.applicantOriginated ? 'New-patient applicant · exact synthetic candidate match' : 'Established patient'} · Request {item.requestId.slice(0, 8)}</small></div></li>)}</ol></section>
    </main>
  )
}
