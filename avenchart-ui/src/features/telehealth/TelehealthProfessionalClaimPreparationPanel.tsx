// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import { getTelehealthProfessionalClaimPreparation, prepareTelehealthProfessionalClaim, type TelehealthProfessionalClaimPreparationWorkspace } from './api.ts'

type Props = { consultationId: string }

export default function TelehealthProfessionalClaimPreparationPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const [workspace, setWorkspace] = useState<TelehealthProfessionalClaimPreparationWorkspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmed, setConfirmed] = useState({ source: false, synthetic: false, noSubmission: false })
  useEffect(() => { if (error) errorRef.current?.focus() }, [error])
  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true); setError(null)
    try { setWorkspace(await getTelehealthProfessionalClaimPreparation(consultationId, signal)) }
    catch (caught) { if (!isRequestCancellation(caught)) setError(caught instanceof Error ? caught.message : 'Claim-preparation blockers could not be loaded.') }
    finally { setLoading(false) }
  }, [consultationId])
  useEffect(() => { const controller = new AbortController(); void load(controller.signal); return () => controller.abort() }, [load])
  const prepare = async () => {
    if (!workspace?.claimPreparationEnabled || saving || !workspace.currentFinalClinicalReviewRecorded || !workspace.encounterSignatureRecorded || !confirmed.source || !confirmed.synthetic || !confirmed.noSubmission) return
    setSaving(true); setError(null)
    try {
      await prepareTelehealthProfessionalClaim(consultationId, {
        expectedDocumentationVersion: workspace.currentDocumentationVersion ?? 0,
        expectedDispositionVersion: workspace.currentDispositionVersion ?? 0,
        expectedFinalClinicalReviewVersion: workspace.currentFinalClinicalReviewVersion ?? 0,
        sourceEvidenceReviewed: confirmed.source,
        syntheticOnlyConfirmed: confirmed.synthetic,
        noSubmissionConfirmed: confirmed.noSubmission,
      }, crypto.randomUUID())
      await load()
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'The synthetic claim-preparation receipt could not be recorded.') }
    finally { setSaving(false) }
  }
  return <section className="telehealth-completion-review" aria-labelledby={headingId} aria-busy={loading}>
    <div className="telehealth-heading"><div><p className="telehealth-kicker">Professional claim preparation</p><h4 id={headingId}>Claim-preparation blockers</h4></div><button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading} onClick={() => void load()}>{loading ? 'Refreshing…' : 'Reload blockers'}</button></div>
    <p role="note">This is a structural, non-production assessment. It cannot create or submit a claim, and it does not promise payer coverage or payment.</p>
    {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
    {workspace ? <><p role="status">Target: {workspace.targetStandard}. Claim submission remains unavailable.</p>
      {workspace.currentPreparation ? <div role="status"><strong>PreparedOnly receipt recorded.</strong> {workspace.currentPreparation.adapterName} recorded a non-transmitting synthetic receipt at <time dateTime={workspace.currentPreparation.preparedAt}>{new Date(workspace.currentPreparation.preparedAt).toLocaleString()}</time>. No transaction or external action occurred.</div> : null}
      {workspace.claimPreparationEnabled ? <fieldset disabled={saving}><legend>Structural receipt confirmations</legend><label className="telehealth-check"><input type="checkbox" checked={confirmed.source} onChange={(event) => setConfirmed((current) => ({ ...current, source: event.target.checked }))} />I reviewed the exact locked source-evidence versions.</label><label className="telehealth-check"><input type="checkbox" checked={confirmed.synthetic} onChange={(event) => setConfirmed((current) => ({ ...current, synthetic: event.target.checked }))} />I confirm this is synthetic-only structural evidence.</label><label className="telehealth-check"><input type="checkbox" checked={confirmed.noSubmission} onChange={(event) => setConfirmed((current) => ({ ...current, noSubmission: event.target.checked }))} />I confirm this action cannot submit a claim or contact an external destination.</label><button className="telehealth-button" type="button" disabled={saving || !confirmed.source || !confirmed.synthetic || !confirmed.noSubmission} onClick={() => void prepare()}>{saving ? 'Preparing receipt…' : 'Record PreparedOnly claim receipt'}</button></fieldset> : null}
      <ul>{workspace.blockers.map((blocker) => <li key={blocker}>{blocker}</li>)}</ul><ul>{workspace.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul></> : null}
  </section>
}
