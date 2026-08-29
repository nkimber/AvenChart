// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { ApiRequestError, isRequestCancellation } from '../../api/transport.ts'
import {
  getApplicantTelehealthRequestQueueStatus,
  type TelehealthApplicantRequestQueueStatus,
} from './api.ts'
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
  const generation = useRef(0)

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
            <div><dt>Exact queue position assigned</dt><dd>No</dd></div>
            <div><dt>Wait estimate available</dt><dd>No</dd></div>
          </dl>
          <p><small>Last confirmed <time dateTime={status.snapshotAt}>{new Date(status.snapshotAt).toLocaleTimeString()}</time>. Authoritative HTTP polling; realtime delivery is not enabled.</small></p>
          <ul className="telehealth-safety-actions">{(status.safetyActions ?? []).map((action) => <li key={action}>{action}</li>)}</ul>
          <ul>{(status.limitations ?? []).map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
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
