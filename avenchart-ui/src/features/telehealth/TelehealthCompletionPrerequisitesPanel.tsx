// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import { getTelehealthCompletionPrerequisites, type TelehealthCompletionPrerequisites } from './api.ts'

type Props = { consultationId: string }

export default function TelehealthCompletionPrerequisitesPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const [review, setReview] = useState<TelehealthCompletionPrerequisites | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<string | null>(null)

  useEffect(() => { if (error) errorRef.current?.focus() }, [error])

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    setError(null)
    setStatus('Refreshing the structural evidence review…')
    try {
      const result = await getTelehealthCompletionPrerequisites(consultationId, signal)
      setReview(result)
      setStatus('Structural evidence review refreshed. No signing or completion action occurred.')
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setError(caught instanceof Error ? caught.message : 'The structural evidence review could not be loaded.')
      setStatus('The prior review is unchanged. No signing or completion action occurred.')
    } finally {
      setLoading(false)
    }
  }, [consultationId])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  return (
    <section className="telehealth-completion-review" aria-labelledby={headingId} aria-busy={loading}>
      <div className="telehealth-heading">
        <div><p className="telehealth-kicker">Read-only structural review</p><h4 id={headingId}>Pre-finalization evidence review</h4></div>
        <button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading} onClick={() => void load()}>{loading ? 'Refreshing…' : 'Reload review'}</button>
      </div>
      <p role="note">This view reports whether limited draft fields exist. It does not judge clinical completeness, accuracy, applicability, safety, or readiness to sign.</p>
      {status ? <p role="status">{status}</p> : null}
      {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
      {review ? <>
        <p><strong>Lifecycle remains unfinished:</strong> consultation {humanize(review.consultationStatus)}, request {humanize(review.requestStatus)}, physician shift {humanize(review.shiftStatus)}.</p>
        <div className="telehealth-completion-review-grid">
          <section aria-labelledby={`${headingId}-documentation`}>
            <h5 id={`${headingId}-documentation`}>Unsigned SOAP draft</h5>
            <p>Current version: {review.documentation.version || 'none recorded'}.</p>
            <ul className="telehealth-presence-list">
              <Presence label="Subjective field has content" value={review.documentation.subjectivePresent} />
              <Presence label="Objective field has content" value={review.documentation.objectivePresent} />
              <Presence label="Assessment field has content" value={review.documentation.assessmentPresent} />
              <Presence label="Plan field has content" value={review.documentation.planPresent} />
            </ul>
          </section>
          <section aria-labelledby={`${headingId}-disposition`}>
            <h5 id={`${headingId}-disposition`}>Unsigned safety disposition</h5>
            {review.safetyDisposition ? <>
              <p>{humanize(review.safetyDisposition.dispositionCode)} · version {review.safetyDisposition.version}.</p>
              <ul className="telehealth-presence-list">
                <Presence label="Follow-up owner recorded" value={review.safetyDisposition.followUpOwnerPresent} />
                <Presence label="Follow-up timeframe has content" value={review.safetyDisposition.followUpTimeframePresent} />
                <Presence label="Next-step instructions have content" value={review.safetyDisposition.nextStepInstructionsPresent} />
                <Presence label="Warning and escalation instructions have content" value={review.safetyDisposition.warningEscalationInstructionsPresent} />
                <Presence label="Selected communication completed" value={review.safetyDisposition.communicationCompleted} />
              </ul>
            </> : <p>No safety-disposition draft recorded.</p>}
          </section>
          <section aria-labelledby={`${headingId}-pharmacy`}>
            <h5 id={`${headingId}-pharmacy`}>Optional pharmacy destination</h5>
            {review.pharmacyChoice
              ? <p>Patient-confirmed destination draft version {review.pharmacyChoice.version} is recorded. This is not a prescription.</p>
              : <p>None recorded — this is optional and is not a blocker.</p>}
          </section>
          <section aria-labelledby={`${headingId}-review`}>
            <h5 id={`${headingId}-review`}>Synthetic final clinical review</h5>
            {review.currentFinalClinicalReview
              ? <p>Current source-bound review version {review.currentFinalClinicalReview.version} is recorded. It is not a signature, completion, bill, or claim.</p>
              : <p>No current source-bound final clinical-review evidence is recorded.</p>}
          </section>
          <section aria-labelledby={`${headingId}-blockers`}>
            <h5 id={`${headingId}-blockers`}>Product blockers</h5>
            <ul className="telehealth-blocker-list">{review.productBlockers.map((blocker) => <li key={blocker}>{humanize(blocker)}</li>)}</ul>
          </section>
        </div>
        <p className="telehealth-capability-stop"><strong>Unavailable:</strong> signing, encounter completion, patient delivery, and downstream creation. All four capabilities remain disabled even when structural drafts or synthetic review evidence are present.</p>
        <p><small>Structural drafts recorded: {review.structuralEvidencePresent ? 'yes' : 'no'}. This is not a clinical readiness result. Projection as of {new Date(review.asOf).toLocaleString()}.</small></p>
        <ul>{review.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
      </> : null}
    </section>
  )
}

function Presence({ label, value }: { label: string, value: boolean }) {
  return <li>{label}: <strong>{value ? 'yes' : 'no'}</strong>.</li>
}

function humanize(value: string) {
  return value
    .replaceAll('_', ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .toLowerCase()
}
