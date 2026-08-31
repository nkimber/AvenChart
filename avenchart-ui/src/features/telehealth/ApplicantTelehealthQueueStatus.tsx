// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { ApiRequestError, isRequestCancellation } from '../../api/transport.ts'
import {
  getApplicantSyntheticPostVisitReceipt,
  getApplicantTelehealthRequestQueueStatus,
  prepareApplicantConnection,
  type TelehealthApplicantRequestQueueStatus,
  type TelehealthDevicePreflight,
  type TelehealthSyntheticPostVisitReceipt,
} from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import { queuePollDelayMilliseconds, shouldPollPatientQueueStatus } from './polling.ts'

type Props = {
  applicantId: string
  applicantAccessKey: string
  enabled: boolean
}

export default function ApplicantTelehealthQueueStatus({ applicantId, applicantAccessKey, enabled }: Props) {
  const [status, setStatus] = useState<TelehealthApplicantRequestQueueStatus | null>(null)
  const [connection, setConnection] = useState<'idle' | 'checking' | 'connected' | 'paused' | 'retrying'>('idle')
  const [issue, setIssue] = useState<string | null>(null)
  const [retryAttempt, setRetryAttempt] = useState(0)
  const [connectionWorking, setConnectionWorking] = useState(false)
  const [connectionIssue, setConnectionIssue] = useState<string | null>(null)
  const [deviceEvidence, setDeviceEvidence] = useState<TelehealthDevicePreflight | null>(null)
  const [waitingRoom, setWaitingRoom] = useState<{ expiresAt: string; message: string; limitations: string[] } | null>(null)
  const [postVisitReceipt, setPostVisitReceipt] = useState<TelehealthSyntheticPostVisitReceipt | null>(null)
  const generation = useRef(0)
  const connectionCommandKey = useRef<string | null>(null)

  useEffect(() => {
    setConnectionWorking(false)
    setConnectionIssue(null)
    setDeviceEvidence(null)
    setWaitingRoom(null)
    setPostVisitReceipt(null)
    connectionCommandKey.current = null
  }, [applicantAccessKey, applicantId, enabled])

  useEffect(() => {
    if (!enabled || status?.requestStatus !== 'Closed') return
    const controller = new AbortController()
    void getApplicantSyntheticPostVisitReceipt(applicantId, applicantAccessKey, status.requestId, controller.signal)
      .then((receipt) => setPostVisitReceipt(receipt))
      .catch((caught) => {
        if (!isRequestCancellation(caught)) setPostVisitReceipt(null)
      })
    return () => controller.abort()
  }, [applicantAccessKey, applicantId, enabled, status?.requestId, status?.requestStatus])

  useEffect(() => {
    const currentGeneration = ++generation.current
    if (!enabled) {
      setConnection('idle')
      setStatus(null)
      setIssue(null)
      return
    }

    let stopped = false
    let timerId: number | undefined
    let inFlight: AbortController | null = null
    let consecutiveFailures = 0
    let hasConfirmedStatus = status !== null

    const schedule = (refreshAfterSeconds: number) => {
      if (stopped) return
      timerId = window.setTimeout(
        () => void poll(),
        queuePollDelayMilliseconds(refreshAfterSeconds, consecutiveFailures),
      )
    }

    const poll = async () => {
      if (stopped || currentGeneration !== generation.current) return
      if (document.visibilityState === 'hidden') {
        setConnection('paused')
        schedule(5)
        return
      }

      inFlight?.abort()
      const controller = new AbortController()
      inFlight = controller
      setConnection(consecutiveFailures > 0 ? 'retrying' : 'checking')
      try {
        const result = await getApplicantTelehealthRequestQueueStatus(
          applicantId,
          applicantAccessKey,
          controller.signal,
        )
        if (stopped || currentGeneration !== generation.current) return
        hasConfirmedStatus = true
        consecutiveFailures = 0
        setStatus(result)
        setIssue(null)
        setConnection('connected')
        if (shouldPollPatientQueueStatus(result.requestStatus)) {
          schedule(result.refreshAfterSeconds)
        }
      } catch (caught) {
        if (stopped || isRequestCancellation(caught) || currentGeneration !== generation.current) return
        if (caught instanceof ApiRequestError && caught.status === 409 && !hasConfirmedStatus) {
          setConnection('idle')
          schedule(5)
          return
        }
        consecutiveFailures += 1
        setIssue(caught instanceof ApiRequestError && caught.status === 410
          ? 'This synthetic applicant session expired. Start again to view request status.'
          : 'Queue status is temporarily unavailable. The last confirmed status remains shown; retry now or keep this page open.')
        setConnection('retrying')
        if (!(caught instanceof ApiRequestError && caught.status && caught.status < 500)) schedule(5)
      }
    }

    const handleVisibility = () => {
      if (document.visibilityState === 'hidden') {
        inFlight?.abort()
        setConnection('paused')
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
    // A manual retry intentionally starts a fresh polling generation.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [applicantAccessKey, applicantId, enabled, retryAttempt])

  async function checkDevices() {
    setConnectionWorking(true)
    setConnectionIssue(null)
    setWaitingRoom(null)
    try {
      const result = await runTelehealthDevicePreflight()
      if (result.status === 'failed') {
        setDeviceEvidence(null)
        setConnectionIssue(result.message)
        return
      }
      setDeviceEvidence(result.evidence)
    } finally {
      setConnectionWorking(false)
    }
  }

  async function enterWaitingRoom() {
    if (!status || !deviceEvidence || connectionWorking) return
    setConnectionWorking(true)
    setConnectionIssue(null)
    connectionCommandKey.current ??= crypto.randomUUID()
    try {
      const result = await prepareApplicantConnection(
        applicantId,
        applicantAccessKey,
        status.requestId,
        status.requestVersion,
        deviceEvidence,
        connectionCommandKey.current,
      )
      setWaitingRoom({
        expiresAt: result.expiresAt,
        message: result.waitingRoomMessage,
        limitations: result.limitations,
      })
      connectionCommandKey.current = null
      setRetryAttempt((value) => value + 1)
    } catch (caught) {
      setConnectionIssue(caught instanceof Error
        ? caught.message
        : 'The private synthetic connection room could not be prepared.')
    } finally {
      setConnectionWorking(false)
    }
  }

  if (!status && !issue) return null

  return (
    <section className="telehealth-queue-status" aria-labelledby="applicant-queue-status-title" aria-busy={connection === 'checking' || connection === 'retrying'}>
      <div className="telehealth-queue-status-heading">
        <h3 id="applicant-queue-status-title">{status?.headline ?? 'Request status temporarily unavailable'}</h3>
        <span className="telehealth-status-chip">{connection === 'paused' ? 'Updates paused while hidden' : connection === 'connected' ? 'Status current' : 'Checking status'}</span>
      </div>
      {issue ? (
        <div className="telehealth-inline-warning" role="alert">
          <p>{issue}</p>
          <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => setRetryAttempt((value) => value + 1)}>Retry queue status</button>
        </div>
      ) : null}
      {status ? (
        <div aria-live="polite">
          <p>{status.detail}</p>
          {status.positionIsApproximate && status.approximateRequestsAhead !== null ? (
            <p className="telehealth-position"><strong>Approximate requests ahead:</strong> {status.approximateRequestsAhead}</p>
          ) : null}
          <p>{status.waitEstimateMessage}</p>
          <dl className="telehealth-details">
            <div><dt>Request status</dt><dd>{status.requestStatus}</dd></div>
            <div><dt>Practice accepted for synthetic queue</dt><dd>{status.practiceAccepted ? 'Yes' : 'Not yet'}</dd></div>
            <div><dt>Doctor search started</dt><dd>{status.doctorSearchStarted ? 'Yes' : 'No'}</dd></div>
            <div><dt>Physician assigned</dt><dd>{status.renderingPhysicianAssigned ? 'Yes — identity not disclosed here' : 'No'}</dd></div>
            <div><dt>Exact synthetic candidate matched</dt><dd>{status.syntheticRenderingCandidateMatched ? 'Yes' : 'Not yet'}</dd></div>
            <div><dt>Real physician network confirmed</dt><dd>No</dd></div>
            <div><dt>Private synthetic waiting room entered</dt><dd>{status.patientWaitingRoomEntered ? 'Yes' : 'No'}</dd></div>
            <div><dt>Media session created</dt><dd>No</dd></div>
            <div><dt>Communication started</dt><dd>No</dd></div>
            <div><dt>Exact queue position assigned</dt><dd>No</dd></div>
            <div><dt>Wait estimate available</dt><dd>No</dd></div>
          </dl>
          <p><small>Last confirmed <time dateTime={status.snapshotAt}>{new Date(status.snapshotAt).toLocaleTimeString()}</time>. Authoritative HTTP polling; realtime delivery is not enabled.</small></p>
          <ul className="telehealth-safety-actions">{(status.safetyActions ?? []).map((action) => <li key={action}>{action}</li>)}</ul>
          <ul>{(status.limitations ?? []).map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
          {status.requestStatus === 'Closed' ? (
            <section className="telehealth-post-visit-receipt" aria-labelledby="applicant-post-visit-receipt-title">
              <h4 id="applicant-post-visit-receipt-title">Synthetic post-visit receipt</h4>
              {postVisitReceipt ? (
                <>
                  <p>{postVisitReceipt.receiptState}. This is a minimized lifecycle receipt, not an after-visit summary.</p>
                  <dl className="telehealth-details">
                    <div><dt>Source mode</dt><dd>{postVisitReceipt.sourceMode}</dd></div>
                    <div><dt>Appointment completed</dt><dd>{postVisitReceipt.appointmentCompleted ? 'Yes' : 'No'}</dd></div>
                    <div><dt>Encounter completed</dt><dd>{postVisitReceipt.encounterCompleted ? 'Yes' : 'No'}</dd></div>
                    <div><dt>Clinical record delivered</dt><dd>{postVisitReceipt.clinicalRecordDelivered ? 'Yes' : 'No'}</dd></div>
                    <div><dt>Prescription delivered</dt><dd>{postVisitReceipt.prescriptionDelivered ? 'Yes' : 'No'}</dd></div>
                    <div><dt>Billing or claim created</dt><dd>{postVisitReceipt.billingCreated || postVisitReceipt.claimCreated ? 'Yes' : 'No'}</dd></div>
                  </dl>
                  <ul>{postVisitReceipt.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                </>
              ) : <p>Loading the minimized synthetic lifecycle receipt.</p>}
            </section>
          ) : null}
          {status.requestStatus === 'Reserved' || status.requestStatus === 'Connecting' ? (
            <section className="telehealth-connection-room" aria-labelledby="applicant-device-check-title" aria-busy={connectionWorking}>
              <h4 id="applicant-device-check-title">Private synthetic connection room</h4>
              <p>Run the local camera, microphone, speaker, and secure-browser check when you are ready. Temporary test tracks stop immediately. No media, device name, device ID, browser details, IP address, or recording is sent or stored.</p>
              <div className="telehealth-actions">
                <button className="telehealth-button telehealth-button-secondary" type="button" disabled={connectionWorking} onClick={() => void checkDevices()}>
                  {connectionWorking ? 'Checking…' : 'Check this device'}
                </button>
                <button className="telehealth-button" type="button" disabled={connectionWorking || !deviceEvidence} onClick={() => void enterWaitingRoom()}>
                  Enter private synthetic waiting room
                </button>
              </div>
              {deviceEvidence ? <p className="telehealth-preflight-passed" role="status">Device check passed. Camera, microphone, speaker, and secure browser capability are available. Connection indication: {deviceEvidence.networkQuality}.</p> : null}
              {connectionIssue ? <p className="telehealth-error" role="alert">{connectionIssue}</p> : null}
              {waitingRoom ? (
                <div className="telehealth-waiting-room" role="status">
                  <h5>Waiting room ready</h5>
                  <p>{waitingRoom.message}</p>
                  <p><small>Local grant expires {new Date(waitingRoom.expiresAt).toLocaleTimeString()}.</small></p>
                  <ul>{waitingRoom.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                </div>
              ) : null}
            </section>
          ) : null}
          {shouldPollPatientQueueStatus(status.requestStatus) ? (
            <button
              className="telehealth-button telehealth-button-secondary"
              type="button"
              aria-disabled={connection === 'checking'}
              onClick={() => {
                if (connection !== 'checking') setRetryAttempt((value) => value + 1)
              }}
            >
              Refresh queue status now
            </button>
          ) : null}
        </div>
      ) : null}
    </section>
  )
}
