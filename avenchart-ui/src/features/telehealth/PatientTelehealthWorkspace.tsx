// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import {
  cancelPatientTelehealthRequest,
  completePatientReadiness,
  confirmPatientLocation,
  createPatientRequest,
  evaluatePatientTriage,
  fastTrackPatientRequestToQueue,
  getPatientQueueStatus,
  getPatientRequestHistory,
  getPatientSyntheticAfterVisitPlanPreview,
  getPatientSyntheticPostVisitReceipt,
  getPatientReadiness,
  getPatientInternetCallingConfiguration,
  readPatientLocalWebRtcSignals,
  listPatientRequests,
  preparePatientConnection,
  verifyPatientCoverage,
  writePatientLocalWebRtcSignal,
  type TelehealthConnectionGrant,
  type TelehealthDevicePreflight,
  type TelehealthPatientQueueStatus,
  type TelehealthReadiness,
  type TelehealthRequest,
  type TelehealthRequestHistory,
  type TelehealthSyntheticAfterVisitPlanPreview,
  type TelehealthSyntheticPostVisitReceipt,
} from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import { connectionReturnedToQueueMessage, connectionWasReturnedToQueue } from './connectionRecovery.ts'
import { canCancelPatientTelehealthRequest } from './requestCancellation.ts'
import { isRequestCancellation } from '../../api/transport.ts'
import { queuePollDelayMilliseconds, shouldPollPatientQueueStatus } from './polling.ts'
import TelehealthConversationPanel from './TelehealthConversationPanel.tsx'
import TelehealthLocalWebRtcPocPanel from './TelehealthLocalWebRtcPocPanel.tsx'
import './telehealth.css'

const TelehealthInternetCallingPocPanel = lazy(() => import('./TelehealthInternetCallingPocPanel.tsx'))

export default function PatientTelehealthWorkspace() {
  const [requests, setRequests] = useState<TelehealthRequest[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [stateCode, setStateCode] = useState('GA')
  const [answers, setAnswers] = useState({ emergency: false, severe: false, handsOn: false, unsure: false })
  const [readiness, setReadiness] = useState<TelehealthReadiness | null>(null)
  const [readinessLoading, setReadinessLoading] = useState(false)
  const [coverageToken, setCoverageToken] = useState('')
  const [complaintSummary, setComplaintSummary] = useState('Synthetic recurring migraine demonstration')
  const [symptomDuration, setSymptomDuration] = useState('1-3-days')
  const [confirmations, setConfirmations] = useState({ details: false, clinical: false, coverage: false, acknowledgment: false, synthetic: false })
  const [cancellationConfirmed, setCancellationConfirmed] = useState(false)
  const [queueStatus, setQueueStatus] = useState<TelehealthPatientQueueStatus | null>(null)
  const [requestHistory, setRequestHistory] = useState<TelehealthRequestHistory | null>(null)
  const [postVisitReceipt, setPostVisitReceipt] = useState<TelehealthSyntheticPostVisitReceipt | null>(null)
  const [afterVisitPlanPreview, setAfterVisitPlanPreview] = useState<TelehealthSyntheticAfterVisitPlanPreview | null>(null)
  const [queueConnection, setQueueConnection] = useState<'idle' | 'checking' | 'connected' | 'paused' | 'retrying'>('idle')
  const [queueIssue, setQueueIssue] = useState<string | null>(null)
  const [queueRefreshNonce, setQueueRefreshNonce] = useState(0)
  const [connectionWorking, setConnectionWorking] = useState(false)
  const [deviceEvidence, setDeviceEvidence] = useState<TelehealthDevicePreflight | null>(null)
  const [waitingRoom, setWaitingRoom] = useState<TelehealthConnectionGrant | null>(null)
  const [connectionRecoveryNotice, setConnectionRecoveryNotice] = useState<string | null>(null)
  const connectionCommandKey = useRef<string | null>(null)
  const requestGeneration = useRef(0)
  const readinessGeneration = useRef(0)
  const historyGeneration = useRef(0)
  const postVisitReceiptGeneration = useRef(0)
  const afterVisitPlanPreviewGeneration = useRef(0)
  const queueStatusGeneration = useRef(0)
  const selected = requests.find((item) => item.requestId === selectedId) ?? null
  // A polling response may replace the selected request object even though the
  // patient is still on exactly the same readiness step. Keep draft input
  // scoped to meaningful workflow identity, not object identity.
  const selectedReadinessRequestId = selected?.requestId ?? null
  const selectedReadinessStatus = selected?.status ?? null
  const selectedReadinessComplaintCategory = selected?.complaintCategory ?? null
  const selectedQueueRequestId = selected?.requestId ?? null
  const selectedQueueRequestStatus = selected?.status ?? null
  const visibleQueueStatus = queueStatus?.requestId === selectedQueueRequestId ? queueStatus : null

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const generation = ++requestGeneration.current
    setLoading(true)
    setError(null)
    try {
      const result = await listPatientRequests(signal)
      if (generation !== requestGeneration.current) return
      setRequests(result)
      setSelectedId((current) => result.some((item) => item.requestId === current) ? current : result[0]?.requestId ?? null)
    } catch (caught) {
      if (isRequestCancellation(caught) || generation !== requestGeneration.current) return
      setRequests([])
      setSelectedId(null)
      setError(caught instanceof Error ? caught.message : 'Requests could not be loaded.')
    } finally {
      if (generation === requestGeneration.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void refresh(controller.signal)
    return () => controller.abort()
  }, [refresh])

  useEffect(() => {
    const generation = ++readinessGeneration.current
    setReadiness(null)
    setCoverageToken('')
    setComplaintSummary(selectedReadinessComplaintCategory === 'sleep'
      ? 'Synthetic sleep difficulty demonstration'
      : 'Synthetic recurring migraine demonstration')
    setConfirmations({ details: false, clinical: false, coverage: false, acknowledgment: false, synthetic: false })
    setCancellationConfirmed(false)
    if (!selectedReadinessRequestId || !selectedReadinessStatus || !['Intake', 'Verification', 'OperationalReview'].includes(selectedReadinessStatus)) {
      setReadinessLoading(false)
      return
    }
    const controller = new AbortController()
    setReadinessLoading(true)
    void getPatientReadiness(selectedReadinessRequestId, controller.signal)
      .then((result) => {
        if (generation !== readinessGeneration.current) return
        setReadiness(result)
        setCoverageToken(result.coverageOptions[0]?.coverageToken ?? '')
      })
      .catch((caught) => {
        if (isRequestCancellation(caught) || generation !== readinessGeneration.current) return
        setError(caught instanceof Error ? caught.message : 'Readiness details could not be loaded.')
      })
      .finally(() => {
        if (generation === readinessGeneration.current) setReadinessLoading(false)
      })
    return () => controller.abort()
  }, [selectedReadinessComplaintCategory, selectedReadinessRequestId, selectedReadinessStatus])

  useEffect(() => {
    const generation = ++afterVisitPlanPreviewGeneration.current
    setAfterVisitPlanPreview(null)
    if (!selected || selected.status !== 'Closed') return
    const controller = new AbortController()
    void getPatientSyntheticAfterVisitPlanPreview(selected.requestId, controller.signal)
      .then((result) => {
        if (generation === afterVisitPlanPreviewGeneration.current) setAfterVisitPlanPreview(result)
      })
      .catch((caught) => {
        if (!isRequestCancellation(caught) && generation === afterVisitPlanPreviewGeneration.current) {
          setError(caught instanceof Error ? caught.message : 'After-visit plan preview could not be loaded.')
        }
      })
    return () => controller.abort()
  }, [selected])

  useEffect(() => {
    const generation = ++postVisitReceiptGeneration.current
    setPostVisitReceipt(null)
    if (!selected || selected.status !== 'Closed') return
    const controller = new AbortController()
    void getPatientSyntheticPostVisitReceipt(selected.requestId, controller.signal)
      .then((result) => {
        if (generation === postVisitReceiptGeneration.current) setPostVisitReceipt(result)
      })
      .catch((caught) => {
        if (!isRequestCancellation(caught) && generation === postVisitReceiptGeneration.current) {
          setError(caught instanceof Error ? caught.message : 'Post-visit receipt could not be loaded.')
        }
      })
    return () => controller.abort()
  }, [selected])

  useEffect(() => {
    const generation = ++historyGeneration.current
    setRequestHistory(null)
    if (!selected) return
    const controller = new AbortController()
    void getPatientRequestHistory(selected.requestId, controller.signal)
      .then((result) => {
        if (generation === historyGeneration.current) setRequestHistory(result)
      })
      .catch((caught) => {
        if (!isRequestCancellation(caught) && generation === historyGeneration.current) {
          setError(caught instanceof Error ? caught.message : 'Request history could not be loaded.')
        }
      })
    return () => controller.abort()
  }, [selected])

  useEffect(() => {
    setDeviceEvidence(null)
    setWaitingRoom(null)
    setConnectionRecoveryNotice(null)
    connectionCommandKey.current = null
  }, [selectedId])

  useEffect(() => {
    const generation = ++queueStatusGeneration.current
    setQueueIssue(null)
    if (!selectedQueueRequestId || !selectedQueueRequestStatus || !shouldPollPatientQueueStatus(selectedQueueRequestStatus)) {
      setQueueConnection('idle')
      return
    }

    let stopped = false
    let timerId: number | undefined
    let inFlight: AbortController | null = null
    let consecutiveFailures = 0

    const schedule = (refreshAfterSeconds: number) => {
      if (stopped) return
      timerId = window.setTimeout(
        () => void poll(),
        queuePollDelayMilliseconds(refreshAfterSeconds, consecutiveFailures),
      )
    }

    const poll = async () => {
      if (stopped || generation !== queueStatusGeneration.current) return
      if (document.visibilityState === 'hidden') {
        setQueueConnection('paused')
        schedule(5)
        return
      }

      inFlight?.abort()
      const controller = new AbortController()
      inFlight = controller
      setQueueConnection(consecutiveFailures > 0 ? 'retrying' : 'checking')
      try {
        const result = await getPatientQueueStatus(selectedQueueRequestId, controller.signal)
        if (stopped || generation !== queueStatusGeneration.current) return
        consecutiveFailures = 0
        if (connectionWasReturnedToQueue(selectedQueueRequestStatus, result.requestStatus)) {
          setConnectionWorking(false)
          setDeviceEvidence(null)
          setWaitingRoom(null)
          connectionCommandKey.current = null
          setConnectionRecoveryNotice(connectionReturnedToQueueMessage)
        }
        setQueueStatus(result)
        setQueueIssue(null)
        setQueueConnection('connected')
        setRequests((current) => {
          const existing = current.find((item) => item.requestId === result.requestId)
          if (!existing
            || existing.version > result.requestVersion
            || (existing.status === result.requestStatus
              && existing.version === result.requestVersion)) {
            return current
          }

          return current.map((item) => item.requestId === result.requestId
            ? { ...item, status: result.requestStatus, version: result.requestVersion, updatedAt: result.requestUpdatedAt }
            : item)
        })
        schedule(result.refreshAfterSeconds)
      } catch (caught) {
        if (stopped || isRequestCancellation(caught) || generation !== queueStatusGeneration.current) return
        consecutiveFailures += 1
        setQueueIssue('Live status is temporarily unavailable. The last confirmed status remains shown; retry now or keep this page open.')
        setQueueConnection('retrying')
        schedule(5)
      }
    }

    const handleVisibility = () => {
      if (document.visibilityState === 'hidden') {
        inFlight?.abort()
        setQueueConnection('paused')
        return
      }
      if (timerId !== undefined) window.clearTimeout(timerId)
      void poll()
    }

    document.addEventListener('visibilitychange', handleVisibility)
    void poll()
    return () => {
      stopped = true
      if (timerId !== undefined) window.clearTimeout(timerId)
      inFlight?.abort()
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [queueRefreshNonce, selectedQueueRequestId, selectedQueueRequestStatus])

  async function run(action: () => Promise<TelehealthRequest>) {
    if (working) return
    setWorking(true)
    setError(null)
    try {
      const result = await action()
      setRequests((current) => [result, ...current.filter((item) => item.requestId !== result.requestId)])
      setSelectedId(result.requestId)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The request could not be updated.')
      await refresh()
    } finally {
      setWorking(false)
    }
  }

  async function checkDevices() {
    setConnectionWorking(true)
    setError(null)
    setWaitingRoom(null)
    try {
      const result = await runTelehealthDevicePreflight()
      if (result.status === 'failed') {
        setDeviceEvidence(null)
        setError(result.message)
        return
      }
      setDeviceEvidence(result.evidence)
    } finally {
      setConnectionWorking(false)
    }
  }

  async function enterWaitingRoom() {
    if (!selected || !deviceEvidence || connectionWorking) return
    setConnectionWorking(true)
    setError(null)
    connectionCommandKey.current ??= crypto.randomUUID()
    try {
      const result = await preparePatientConnection(
        selected.requestId,
        selected.version,
        deviceEvidence,
        connectionCommandKey.current,
      )
      setWaitingRoom(result)
      setRequests((current) => current.map((item) => item.requestId === result.requestId
        ? { ...item, status: result.requestStatus, version: result.requestVersion }
        : item))
      connectionCommandKey.current = null
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The private connection room could not be prepared.')
    } finally {
      setConnectionWorking(false)
    }
  }

  return (
    <main className="telehealth-page" aria-labelledby="patient-telehealth-title">
      <header className="telehealth-heading">
        <div><p className="telehealth-kicker">Synthetic foundation</p><h1 id="patient-telehealth-title">Immediate telehealth request</h1></div>
        <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void refresh()} disabled={loading}>Refresh</button>
      </header>
      <div className="telehealth-synthetic" role="note">Demonstration data only. Do not enter real symptoms or personal information.</div>
      <section className="telehealth-emergency" aria-labelledby="patient-emergency-title">
        <h2 id="patient-emergency-title">Emergency symptoms?</h2>
        <p>Call 911 now. An emergency result cannot enter this queue.</p>
        <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
      </section>
      {error ? <p className="telehealth-error" role="alert">{error}</p> : null}

      <div className="telehealth-workspace">
        <section className="telehealth-card" aria-labelledby="request-list-title" aria-busy={loading}>
          <h2 id="request-list-title">Your synthetic requests</h2>
          <div className="telehealth-actions">
            <button className="telehealth-button" type="button" disabled={working} onClick={() => void run(() => createPatientRequest('migraine'))}>Start migraine demo</button>
            <button className="telehealth-button" type="button" disabled={working} onClick={() => void run(() => createPatientRequest('sleep'))}>Start sleep demo</button>
          </div>
          {loading ? <p aria-live="polite">Loading requests…</p> : null}
          {!loading && requests.length === 0 ? <p>No active synthetic request.</p> : null}
          <ul className="telehealth-list">
            {requests.map((item) => (
              <li key={item.requestId}>
                <button type="button" className={item.requestId === selectedId ? 'is-selected' : ''} onClick={() => setSelectedId(item.requestId)}>
                  <span>{item.complaintCategory}</span><strong>{item.status}</strong><small>Version {item.version}</small>
                </button>
              </li>
            ))}
          </ul>
        </section>

        <section className="telehealth-card" aria-labelledby="request-action-title">
          <h2 id="request-action-title">Next step</h2>
          {!selected ? <p>Select or start a request.</p> : null}
          {selected?.status === 'Draft' ? (
            <form onSubmit={(event) => { event.preventDefault(); void run(() => confirmPatientLocation(selected.requestId, stateCode, selected.version)) }}>
              <label htmlFor="telehealth-state">Where are you physically located now?</label>
              <select id="telehealth-state" value={stateCode} onChange={(event) => setStateCode(event.target.value)}>
                <option value="GA">Georgia</option><option value="CA">California</option><option value="FL">Florida</option>
              </select>
              <button className="telehealth-button" type="submit" disabled={working}>Confirm current location</button>
            </form>
          ) : null}
          {selected?.status === 'LocationConfirmed' ? (
            <form onSubmit={(event) => {
              event.preventDefault()
              void run(() => evaluatePatientTriage(selected.requestId, {
                hasEmergencyWarning: answers.emergency,
                severeOrWorsening: answers.severe,
                requiresHandsOnExam: answers.handsOn,
                unsure: answers.unsure,
                expectedVersion: selected.version,
              }))
            }}>
              <fieldset><legend>Synthetic safety answers</legend>
                {([
                  ['emergency', 'Possible emergency warning'],
                  ['severe', 'Severe or rapidly worsening'],
                  ['handsOn', 'May require an in-person examination'],
                  ['unsure', 'Unsure about any answer'],
                ] as const).map(([key, label]) => (
                  <label className="telehealth-check" key={key}><input type="checkbox" checked={answers[key]} onChange={(event) => setAnswers((current) => ({ ...current, [key]: event.target.checked }))} />{label}</label>
                ))}
              </fieldset>
              <button className="telehealth-button" type="submit" disabled={working}>Evaluate synthetic triage</button>
            </form>
          ) : null}
          {selected?.status === 'OperationalReview' ? (
            <section className="telehealth-queue-status" aria-labelledby="patient-demo-queue-title">
              <h3 id="patient-demo-queue-title">Ready for the physician demo</h3>
              <p>Your synthetic eligibility, readiness, and coverage checks have already passed. Join the ready physician queue now so the physician can reserve this request.</p>
              <p><small>This records a patient-initiated synthetic demonstration handoff. It is not acceptance for care, an appointment confirmation, or a payment guarantee.</small></p>
              <button className="telehealth-button" type="button" disabled={working} onClick={() => void run(() => fastTrackPatientRequestToQueue(selected.requestId, selected.version))}>Join physician demo queue</button>
            </section>
          ) : null}
          {selected && ['Intake', 'Verification', 'OperationalReview'].includes(selected.status) ? (
            <form onSubmit={(event) => {
              event.preventDefault()
              if (!readiness || !Object.values(confirmations).every(Boolean)) return
              void run(() => completePatientReadiness(selected.requestId, readiness, {
                complaintSummary,
                symptomDuration,
                coverageToken,
              }))
            }}>
              {readinessLoading ? <p aria-live="polite">Loading current details…</p> : null}
              {readiness ? (
                <>
                  <fieldset>
                    <legend>Current patient details</legend>
                    <dl className="telehealth-details">
                      <div><dt>Name</dt><dd>{readiness.patientDetails.displayName}</dd></div>
                      <div><dt>Date of birth</dt><dd>{readiness.patientDetails.dateOfBirth}</dd></div>
                      <div><dt>Email</dt><dd>{readiness.patientDetails.email ?? 'Missing'}</dd></div>
                      <div><dt>Callback phone</dt><dd>{readiness.patientDetails.phone ?? 'Missing'}</dd></div>
                      <div><dt>Home address</dt><dd>{readiness.patientDetails.address || 'Missing'}</dd></div>
                    </dl>
                    <label className="telehealth-check"><input type="checkbox" checked={confirmations.details} onChange={(event) => setConfirmations((current) => ({ ...current, details: event.target.checked }))} />I confirm these current demographic and contact details.</label>
                  </fieldset>
                  <fieldset>
                    <legend>Clinical list summary</legend>
                    <p>{readiness.clinicalSummary.activeMedicationCount} active medications · {readiness.clinicalSummary.activeAllergyCount} active allergies · history {readiness.clinicalSummary.historyAvailable ? 'available' : 'not available'}</p>
                    <label className="telehealth-check"><input type="checkbox" checked={confirmations.clinical} onChange={(event) => setConfirmations((current) => ({ ...current, clinical: event.target.checked }))} />I reviewed this synthetic clinical-list summary.</label>
                  </fieldset>
                  <fieldset>
                    <legend>Purpose of this synthetic request</legend>
                    <label htmlFor="telehealth-complaint-summary">Synthetic complaint summary</label>
                    <textarea id="telehealth-complaint-summary" rows={4} minLength={10} maxLength={500} required value={complaintSummary} onChange={(event) => setComplaintSummary(event.target.value)} />
                    <label htmlFor="telehealth-duration">Synthetic symptom duration</label>
                    <select id="telehealth-duration" value={symptomDuration} onChange={(event) => setSymptomDuration(event.target.value)}>
                      <option value="less-than-day">Less than one day</option>
                      <option value="1-3-days">1–3 days</option>
                      <option value="4-14-days">4–14 days</option>
                      <option value="more-than-14-days">More than 14 days</option>
                    </select>
                    <label className="telehealth-check"><input type="checkbox" checked={confirmations.synthetic} onChange={(event) => setConfirmations((current) => ({ ...current, synthetic: event.target.checked }))} />I confirm I entered synthetic demonstration data only.</label>
                  </fieldset>
                  <fieldset>
                    <legend>Existing coverage record</legend>
                    {readiness.coverageOptions.map((coverage) => (
                      <label className="telehealth-coverage-option" key={coverage.coverageToken}>
                        <input type="radio" name="telehealth-coverage" value={coverage.coverageToken} checked={coverageToken === coverage.coverageToken} onChange={(event) => setCoverageToken(event.target.value)} />
                        <span><strong>{coverage.provider} — {coverage.planName}</strong><small>{coverage.coverageType} · policy {coverage.maskedPolicyNumber} · group {coverage.maskedGroupNumber}</small></span>
                      </label>
                    ))}
                    <label className="telehealth-check"><input type="checkbox" checked={confirmations.coverage} onChange={(event) => setConfirmations((current) => ({ ...current, coverage: event.target.checked }))} />I selected and confirmed this existing synthetic coverage record.</label>
                  </fieldset>
                  <fieldset>
                    <legend>{readiness.acknowledgment.title}</legend>
                    <ul>{readiness.acknowledgment.statements.map((statement) => <li key={statement}>{statement}</li>)}</ul>
                    <label className="telehealth-check"><input type="checkbox" checked={confirmations.acknowledgment} onChange={(event) => setConfirmations((current) => ({ ...current, acknowledgment: event.target.checked }))} />I affirmatively accept this exact synthetic acknowledgment.</label>
                  </fieldset>
                  {readiness.blockingReasons.length > 0 ? <div className="telehealth-error" role="alert"><strong>Cannot continue</strong><ul>{readiness.blockingReasons.map((reason) => <li key={reason}>{reason}</li>)}</ul></div> : null}
                  <button className="telehealth-button" type="submit" disabled={working || readiness.blockingReasons.length > 0 || !coverageToken || !Object.values(confirmations).every(Boolean)}>Submit readiness for synthetic verification</button>
                </>
              ) : null}
            </form>
          ) : null}
          {selected?.status === 'Verification' ? (
            <div aria-live="polite">
              <p><strong>Status:</strong> Verification</p>
              <p>Coverage eligibility and exact network participation are checked separately. This deterministic fixture never contacts a payer.</p>
              {selected.coverage ? (
                <div className="telehealth-coverage-result" role="status">
                  <p><strong>Eligibility:</strong> {selected.coverage.eligibilityStatus}</p>
                  <p><strong>Exact network:</strong> {selected.coverage.networkStatus}</p>
                  <p><strong>Financial route:</strong> {selected.coverage.financialRoute}</p>
                  <ul>{selected.coverage.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                </div>
              ) : null}
              <button className="telehealth-button" type="button" disabled={working} onClick={() => void run(() => verifyPatientCoverage(selected.requestId, selected.version))}>{selected.coverage ? 'Run synthetic verification again' : 'Run synthetic coverage verification'}</button>
            </div>
          ) : null}
          {selected && canCancelPatientTelehealthRequest(selected.status) ? (
            <section className="telehealth-request-cancellation" aria-labelledby="telehealth-cancel-title">
              <h3 id="telehealth-cancel-title">Cancel this synthetic request</h3>
              <p>You may cancel before a physician reserves the request or a connection starts. If practice queue authorization already occurred, this removes the request from the ready queue and cancels its provisional synthetic appointment. It does not cancel a reservation, connection, consultation, prescription, billing item, claim, or external action.</p>
              <label className="telehealth-check"><input type="checkbox" checked={cancellationConfirmed} onChange={(event) => setCancellationConfirmed(event.target.checked)} />I confirm I want to cancel this synthetic request.</label>
              <button className="telehealth-button telehealth-button-secondary" type="button" disabled={working || !cancellationConfirmed} onClick={() => void run(() => cancelPatientTelehealthRequest(selected.requestId, selected.version))}>Cancel synthetic request</button>
            </section>
          ) : null}
          {selected && !['Draft', 'LocationConfirmed', 'Intake', 'Verification'].includes(selected.status) ? (
            <div aria-live="polite">
              <p><strong>Status:</strong> {selected.status}</p>
              <p><strong>Synthetic result:</strong> {selected.triageOutcome ?? 'Pending'}</p>
              {selected.status === 'OperationalReview' ? <><p>This synthetic request is ready for the physician-demo handoff above.</p><button className="telehealth-button telehealth-button-secondary" type="button" disabled={working} onClick={() => void run(() => verifyPatientCoverage(selected.requestId, selected.version))}>Refresh synthetic coverage evidence</button></> : null}
              {selected.coverage ? <p><strong>Synthetic coverage:</strong> eligibility {selected.coverage.eligibilityStatus}; exact network {selected.coverage.networkStatus}. This is not a guarantee of payment.</p> : null}
              {shouldPollPatientQueueStatus(selected.status) ? (
                <section className="telehealth-queue-status" aria-labelledby="patient-queue-status-title" aria-busy={queueConnection === 'checking'}>
                  <div className="telehealth-queue-status-heading">
                    <h3 id="patient-queue-status-title">{visibleQueueStatus?.headline ?? 'Checking your request status'}</h3>
                    <span className={`telehealth-connection telehealth-connection-${queueConnection}`}>
                      {queueConnection === 'connected' ? 'Status connected' : null}
                      {queueConnection === 'checking' ? 'Checking now' : null}
                      {queueConnection === 'paused' ? 'Paused while this page is hidden' : null}
                      {queueConnection === 'retrying' ? 'Connection interrupted' : null}
                      {queueConnection === 'idle' ? 'Status idle' : null}
                    </span>
                  </div>
                  {visibleQueueStatus ? (
                    <>
                      <p>{visibleQueueStatus.detail}</p>
                      {visibleQueueStatus.positionIsApproximate && visibleQueueStatus.approximateRequestsAhead !== null ? (
                        <p className="telehealth-position"><strong>Approximate requests ahead:</strong> {visibleQueueStatus.approximateRequestsAhead}</p>
                      ) : null}
                      <p>{visibleQueueStatus.waitEstimateMessage}</p>
                      <p><small>Last confirmed <time dateTime={visibleQueueStatus.snapshotAt}>{new Date(visibleQueueStatus.snapshotAt).toLocaleTimeString()}</time>. Authoritative HTTP polling; realtime delivery is not enabled.</small></p>
                      <ul className="telehealth-safety-actions">{visibleQueueStatus.safetyActions.map((action) => <li key={action}>{action}</li>)}</ul>
                    </>
                  ) : <p>The server is checking the authoritative request state.</p>}
                  {queueIssue ? <p className="telehealth-inline-warning" role="status">{queueIssue}</p> : null}
                  {connectionRecoveryNotice ? <p className="telehealth-inline-warning" role="status">{connectionRecoveryNotice}</p> : null}
                  <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => setQueueRefreshNonce((current) => current + 1)}>Refresh status now</button>
                </section>
              ) : null}
              {requestHistory?.requestId === selected.requestId ? (
                <section className="telehealth-request-history" aria-labelledby="telehealth-history-title">
                  <h3 id="telehealth-history-title">Synthetic request history</h3>
                  <p>This is a read-only POC status history. It does not show clinician identity, clinical notes, prescriptions, billing, claims, communications, or external activity.</p>
                  <ol>
                    {requestHistory.entries.map((entry) => <li key={entry.aggregateVersion}><strong>{entry.status}</strong> — {entry.message} <time dateTime={entry.occurredAt}>{new Date(entry.occurredAt).toLocaleString()}</time></li>)}
                  </ol>
                </section>
              ) : null}
              {selected.status === 'Closed' && postVisitReceipt?.requestId === selected.requestId ? (
                <section className="telehealth-post-visit-receipt" aria-labelledby="telehealth-post-visit-receipt-title">
                  <h3 id="telehealth-post-visit-receipt-title">Synthetic post-visit receipt</h3>
                  <p><strong>State:</strong> {postVisitReceipt.receiptState} · version {postVisitReceipt.receiptVersion}</p>
                  <p>Created <time dateTime={postVisitReceipt.createdAt}>{new Date(postVisitReceipt.createdAt).toLocaleString()}</time>. This is an immutable non-production lifecycle receipt, not an after-visit summary.</p>
                  <ul>{postVisitReceipt.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                </section>
              ) : null}
              {selected.status === 'Closed' && afterVisitPlanPreview?.requestId === selected.requestId ? (
                <section className="telehealth-post-visit-receipt" aria-labelledby="telehealth-after-visit-plan-preview-title">
                  <h3 id="telehealth-after-visit-plan-preview-title">Synthetic after-visit plan preview</h3>
                  <p><strong>{afterVisitPlanPreview.previewState}</strong> · version {afterVisitPlanPreview.previewVersion} · {afterVisitPlanPreview.sourceMode}</p>
                  <p>This immutable synthetic preview is not medical advice, a delivered after-visit summary, or a completed visit.</p>
                  <dl className="telehealth-details">
                    <div><dt>Disposition</dt><dd>{afterVisitPlanPreview.dispositionCode}</dd></div>
                    <div><dt>Follow-up owner</dt><dd>{afterVisitPlanPreview.followUpOwner}</dd></div>
                    <div><dt>Follow-up timeframe</dt><dd>{afterVisitPlanPreview.followUpTimeframe}</dd></div>
                    <div><dt>Communication recorded</dt><dd>{afterVisitPlanPreview.communicationCompleted ? 'Yes' : 'No'}</dd></div>
                    <div><dt>Appointment completed</dt><dd>No</dd></div>
                    <div><dt>Encounter completed</dt><dd>No</dd></div>
                  </dl>
                  <h4>Physician-authored synthetic next steps</h4>
                  <p>{afterVisitPlanPreview.nextStepInstructions}</p>
                  <h4>Physician-authored synthetic warning text</h4>
                  <p>{afterVisitPlanPreview.warningEscalationInstructions}</p>
                  <ul>{afterVisitPlanPreview.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                </section>
              ) : null}
              {['Reserved', 'Connecting'].includes(selected.status) ? (
                <section className="telehealth-connection-room" aria-labelledby="patient-device-check-title">
                  <h3 id="patient-device-check-title">Private synthetic connection room</h3>
                  <p>The check requests camera and microphone access, confirms coarse browser capability, and immediately stops every test track. No device names, media, recording, or transcript are stored.</p>
                  <div className="telehealth-actions">
                    <button className="telehealth-button telehealth-button-secondary" type="button" disabled={connectionWorking} onClick={() => void checkDevices()}>
                      {deviceEvidence ? 'Run device check again' : 'Check camera and microphone'}
                    </button>
                    <button className="telehealth-button" type="button" disabled={connectionWorking || !deviceEvidence} onClick={() => void enterWaitingRoom()}>Enter synthetic waiting room</button>
                  </div>
                  {deviceEvidence ? <p className="telehealth-preflight-passed" role="status">Device check passed. Camera, microphone, speaker, and secure browser capability are available. Network indication: {deviceEvidence.networkQuality}.</p> : null}
                  {waitingRoom ? (
                    <div className="telehealth-waiting-room" role="status">
                      <h4>Waiting room ready</h4>
                      <p>{waitingRoom.waitingRoomMessage}</p>
                      <p><small>Local grant expires {new Date(waitingRoom.expiresAt).toLocaleTimeString()}.</small></p>
                      <ul>{waitingRoom.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    </div>
                  ) : null}
                </section>
              ) : null}
              {waitingRoom?.mediaTransportEnabled && ['Connecting', 'InConsultation'].includes(selected.status) && waitingRoom.mediaTransportMode === 'NON_PRODUCTION_INTERNET_ACS_CALLING_POC' ? <Suspense fallback={<p role="status">Loading the synthetic internet calling controls…</p>}><TelehealthInternetCallingPocPanel grant={waitingRoom} role="patient" getCallingConfiguration={() => getPatientInternetCallingConfiguration(waitingRoom)} /></Suspense> : null}
              {waitingRoom?.mediaTransportEnabled && ['Connecting', 'InConsultation'].includes(selected.status) && waitingRoom.mediaTransportMode === 'NON_PRODUCTION_LOCAL_WEBRTC_POC' ? <TelehealthLocalWebRtcPocPanel grant={waitingRoom} role="patient" writeSignal={(kind, payload) => writePatientLocalWebRtcSignal(waitingRoom, kind, payload)} readSignals={(afterSequence, signal) => readPatientLocalWebRtcSignals(waitingRoom, afterSequence, signal)} /> : null}
              {selected.status === 'InConsultation' ? <section className="telehealth-consultation-started" role="status"><h3>Synthetic consultation lifecycle started</h3><p>This is lifecycle demonstration data only. A synthetic browser media POC may remain connected, but no recording, transcription, clinician identity, chart, diagnosis, prescription, completion, or claim is available.</p><TelehealthConversationPanel participant="patient" requestId={selected.requestId} /><ul className="telehealth-safety-actions"><li>If symptoms worsen or you are unsure it is safe to continue, contact the practice or seek in-person care.</li><li>Call 911 now for an emergency.</li></ul></section> : null}
              {selected.status === 'WrapUp' ? <section className="telehealth-consultation-started" role="status"><h3>Your physician is finishing the synthetic visit record</h3><p>This visit is not complete. No signed record, after-visit summary, prescription, or claim is available. Follow the practice guidance you received.</p><ul className="telehealth-safety-actions"><li>If symptoms worsen or you are unsure it is safe to wait, contact the practice or seek in-person care.</li><li>Call 911 now for an emergency.</li></ul></section> : null}
              {selected.status === 'Closed' && !postVisitReceipt ? <section className="telehealth-consultation-started" role="status"><h3>Synthetic lifecycle closed</h3><p>The post-visit receipt is loading. This does not mean your appointment, encounter, clinical record, prescription, billing, or claim is complete.</p></section> : null}
              {selected.status === 'Redirected' ? <p>This request cannot enter the telehealth queue. Follow urgent or in-person guidance.</p> : null}
              {selected.status === 'Cancelled' ? <p>This synthetic request was cancelled. If practice queue authorization had already occurred, its provisional synthetic appointment was also cancelled. No reservation, connection, consultation, prescription, billing item, claim, integration, or external action occurred.</p> : null}
            </div>
          ) : null}
        </section>
      </div>
    </main>
  )
}
