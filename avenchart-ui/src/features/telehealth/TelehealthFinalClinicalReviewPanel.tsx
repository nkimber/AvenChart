// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import { getTelehealthFinalClinicalReview, recordTelehealthFinalClinicalReview, type TelehealthFinalClinicalReviewWorkspace } from './api.ts'

type Props = { consultationId: string }

export default function TelehealthFinalClinicalReviewPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const commandKey = useRef<string | null>(null)
  const [workspace, setWorkspace] = useState<TelehealthFinalClinicalReviewWorkspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<string | null>(null)
  const [documentationReviewed, setDocumentationReviewed] = useState(false)
  const [physicianResponsibility, setPhysicianResponsibility] = useState(false)
  const [noAutomaticClaimOrDelivery, setNoAutomaticClaimOrDelivery] = useState(false)
  const [syntheticConfirmed, setSyntheticConfirmed] = useState(false)

  useEffect(() => { if (error) errorRef.current?.focus() }, [error])
  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true); setError(null)
    try {
      const result = await getTelehealthFinalClinicalReview(consultationId, signal)
      setWorkspace(result)
      setStatus(result.currentReview
        ? `Current review evidence version ${result.currentReview.version} is recorded. No signature, completion, or claim was created.`
        : 'No current final clinical-review evidence is recorded. Nothing is saved automatically.')
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setError(caught instanceof Error ? caught.message : 'The final clinical-review workspace could not be loaded.')
    } finally { setLoading(false) }
  }, [consultationId])
  useEffect(() => { const controller = new AbortController(); void load(controller.signal); return () => controller.abort() }, [load])
  const ready = Boolean(workspace?.reviewEnabled && documentationReviewed && physicianResponsibility && noAutomaticClaimOrDelivery && syntheticConfirmed)
  async function record() {
    if (!workspace || !ready || saving) return
    setSaving(true); setError(null); setStatus('Recording immutable synthetic final clinical-review evidence…')
    try {
      commandKey.current ??= crypto.randomUUID()
      const review = await recordTelehealthFinalClinicalReview(consultationId, {
        expectedDocumentationVersion: workspace.documentation.version,
        expectedDispositionVersion: workspace.safetyDisposition!.version,
        documentationReviewed: true, physicianResponsibilityConfirmed: true,
        noAutomaticClaimOrDeliveryConfirmed: true, syntheticDataConfirmed: true,
      }, commandKey.current)
      setWorkspace((current) => current ? { ...current, currentReview: review } : current)
      setStatus(`Final clinical-review evidence version ${review.version} was recorded. It has no legal, delivery, billing, or claim effect.`)
    } catch (caught) {
      commandKey.current = null
      setError(caught instanceof Error ? caught.message : 'The final clinical-review evidence could not be recorded.')
      setStatus('No signature, completion, delivery, billing, or claim action occurred.')
    } finally { setSaving(false) }
  }
  function changed() { commandKey.current = null; setError(null) }

  return <section className="telehealth-completion-review" aria-labelledby={headingId} aria-busy={loading}>
    <div className="telehealth-heading"><div><p className="telehealth-kicker">Synthetic review evidence</p><h4 id={headingId}>Final clinical-review affirmation</h4></div><button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading || saving} onClick={() => void load()}>{loading ? 'Refreshing…' : 'Reload review'}</button></div>
    <p role="note">This records review of current draft versions only. It is not a legal encounter signature or a statement that care, billing, or claim requirements are complete.</p>
    {status ? <p role="status">{status}</p> : null}{error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
    {workspace ? <>
      <p>Current SOAP version {workspace.documentation.version || 'none'}; safety-disposition version {workspace.safetyDisposition?.version ?? 'none'}; signed synthetic prescription {workspace.currentPrescriptionOrderId ? 'is bound if reviewed' : 'is not recorded'}.</p>
      {workspace.currentReview ? <p><strong>Current review:</strong> version {workspace.currentReview.version} matches these source versions. No signature, completion, delivery, billing, or claim was created.</p> : null}
      <fieldset disabled={!workspace.reviewEnabled || saving}><legend>Required acknowledgments</legend>
        <label className="telehealth-check"><input type="checkbox" checked={documentationReviewed} onChange={(event) => { setDocumentationReviewed(event.target.checked); changed() }} />I reviewed the current synthetic SOAP and safety-disposition draft versions.</label>
        <label className="telehealth-check"><input type="checkbox" checked={physicianResponsibility} onChange={(event) => { setPhysicianResponsibility(event.target.checked); changed() }} />I retain responsibility for clinical review; this application made no adequacy, diagnosis, or treatment decision.</label>
        <label className="telehealth-check"><input type="checkbox" checked={noAutomaticClaimOrDelivery} onChange={(event) => { setNoAutomaticClaimOrDelivery(event.target.checked); changed() }} />I understand this does not create an automatic patient delivery, bill, or claim.</label>
        <label className="telehealth-check"><input type="checkbox" checked={syntheticConfirmed} onChange={(event) => { setSyntheticConfirmed(event.target.checked); changed() }} />I confirm this is synthetic demonstration evidence with no legal effect.</label>
      </fieldset>
      {!workspace.reviewEnabled ? <p className="telehealth-capability-stop">All four SOAP sections and a safety-disposition draft must be present before this limited review evidence can be recorded.</p> : null}
      <button className="telehealth-button" type="button" disabled={!ready || saving} onClick={() => void record()}>{saving ? 'Recording review…' : 'Record final clinical-review evidence'}</button>
      <ul>{workspace.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
    </> : null}
  </section>
}
