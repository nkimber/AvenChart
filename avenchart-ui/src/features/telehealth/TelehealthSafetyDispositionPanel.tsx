// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import {
  getTelehealthSafetyDispositionDraft,
  recordTelehealthSafetyDispositionDraft,
  type TelehealthSafetyDispositionDraft,
  type TelehealthSafetyDispositionWorkspace,
} from './api.ts'

type Props = { consultationId: string }

export default function TelehealthSafetyDispositionPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const commandKey = useRef<string | null>(null)
  const [workspace, setWorkspace] = useState<TelehealthSafetyDispositionWorkspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<string | null>(null)
  const [dispositionCode, setDispositionCode] = useState('')
  const [adequateEvaluationCompleted, setAdequateEvaluationCompleted] = useState(false)
  const [followUpOwner, setFollowUpOwner] = useState('')
  const [followUpTimeframe, setFollowUpTimeframe] = useState('')
  const [nextStepInstructions, setNextStepInstructions] = useState('')
  const [warningEscalationInstructions, setWarningEscalationInstructions] = useState('')
  const [communicationMethod, setCommunicationMethod] = useState('')
  const [communicationCompleted, setCommunicationCompleted] = useState(false)
  const [locationCallbackReconfirmed, setLocationCallbackReconfirmed] = useState(false)
  const [emergencyInstructionProvided, setEmergencyInstructionProvided] = useState(false)
  const [emergencyHandoffStatus, setEmergencyHandoffStatus] = useState('')
  const [contactAttemptSummary, setContactAttemptSummary] = useState('')
  const [syntheticDataConfirmed, setSyntheticDataConfirmed] = useState(false)

  useEffect(() => { if (error) errorRef.current?.focus() }, [error])

  const applyDraft = useCallback((draft: TelehealthSafetyDispositionDraft | null) => {
    setDispositionCode(draft?.dispositionCode ?? '')
    setAdequateEvaluationCompleted(draft?.adequateEvaluationCompleted ?? false)
    setFollowUpOwner(draft?.followUpOwner ?? '')
    setFollowUpTimeframe(draft?.followUpTimeframe ?? '')
    setNextStepInstructions(draft?.nextStepInstructions ?? '')
    setWarningEscalationInstructions(draft?.warningEscalationInstructions ?? '')
    setCommunicationMethod(draft?.communicationMethod ?? '')
    setCommunicationCompleted(draft?.communicationCompleted ?? false)
    setLocationCallbackReconfirmed(draft?.locationCallbackReconfirmed ?? false)
    setEmergencyInstructionProvided(draft?.emergencyInstructionProvided ?? false)
    setEmergencyHandoffStatus(draft?.emergencyHandoffStatus ?? '')
    setContactAttemptSummary(draft?.contactAttemptSummary ?? '')
    setSyntheticDataConfirmed(false)
    commandKey.current = null
  }, [])

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    setError(null)
    try {
      const result = await getTelehealthSafetyDispositionDraft(consultationId, signal)
      setWorkspace(result)
      applyDraft(result.currentDraft)
      setStatus(result.currentDraft
        ? `Unsigned safety-disposition draft version ${result.currentDraft.version} loaded.`
        : 'No safety-disposition draft has been recorded.')
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setError(caught instanceof Error ? caught.message : 'The safety-disposition draft could not be loaded.')
    } finally {
      setLoading(false)
    }
  }, [applyDraft, consultationId])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  function markChanged() {
    commandKey.current = null
    setError(null)
    setStatus('Unsaved physician-authored safety-disposition changes. Nothing is saved automatically.')
  }

  function changeDisposition(value: string) {
    setDispositionCode(value)
    const option = workspace?.dispositions.find((item) => item.code === value)
    if (!option?.requiresLocationCallbackReconfirmation) setLocationCallbackReconfirmed(false)
    if (!option?.requiresEmergencyFacts) {
      setEmergencyInstructionProvided(false)
      setEmergencyHandoffStatus('')
    }
    if (!option?.requiresContactAttemptSummary) setContactAttemptSummary('')
    markChanged()
  }

  const selected = workspace?.dispositions.find((item) => item.code === dispositionCode)
  const communicationCoherent = communicationMethod === 'NotYetCommunicated'
    ? !communicationCompleted
    : communicationMethod.length > 0 && communicationCompleted
  const complete = Boolean(
    dispositionCode && followUpOwner && followUpTimeframe.trim() && nextStepInstructions.trim()
    && warningEscalationInstructions.trim() && communicationCoherent && syntheticDataConfirmed
    && (!selected?.requiresAdequateEvaluation || adequateEvaluationCompleted)
    && (!selected?.requiresLocationCallbackReconfirmation || locationCallbackReconfirmed)
    && (!selected?.requiresEmergencyFacts || (emergencyInstructionProvided && emergencyHandoffStatus))
    && (!selected?.requiresContactAttemptSummary || contactAttemptSummary.trim()),
  )

  async function save() {
    if (!workspace || !complete || saving) return
    setSaving(true)
    setError(null)
    setStatus('Recording the unsigned, undelivered safety-disposition draft…')
    commandKey.current ??= crypto.randomUUID()
    try {
      const draft = await recordTelehealthSafetyDispositionDraft(consultationId, {
        expectedVersion: workspace.currentDraft?.version ?? 0,
        dispositionCode,
        adequateEvaluationCompleted,
        followUpOwner,
        followUpTimeframe: followUpTimeframe.trim(),
        nextStepInstructions: nextStepInstructions.trim(),
        warningEscalationInstructions: warningEscalationInstructions.trim(),
        communicationMethod,
        communicationCompleted,
        locationCallbackReconfirmed,
        emergencyInstructionProvided,
        emergencyHandoffStatus: emergencyHandoffStatus || null,
        contactAttemptSummary: contactAttemptSummary.trim() || null,
        syntheticDataConfirmed: true,
      }, commandKey.current)
      setWorkspace((current) => current ? { ...current, currentDraft: draft } : current)
      setSyntheticDataConfirmed(false)
      commandKey.current = null
      setStatus(`Unsigned safety-disposition draft version ${draft.version} recorded. It was not signed, finalized, or delivered.`)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The safety-disposition draft was not recorded. Reload before retrying a conflict.')
      setStatus('No safety-disposition change was recorded. No patient delivery or lifecycle action occurred.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="telehealth-disposition-draft" aria-labelledby={headingId} aria-busy={loading}>
      <p className="telehealth-kicker">Physician-authored synthetic draft</p>
      <h4 id={headingId}>Safety disposition and follow-up draft</h4>
      <p role="note">This is unsigned and not delivered to the patient. The application supplies no medical advice. Recording it does not finalize the chart, release you, create an AVS, order, referral, prescription, claim, message, task, or external handoff.</p>
      <button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading || saving} onClick={() => void load()}>{loading ? 'Loading…' : 'Reload saved draft'}</button>
      {status ? <p role="status">{status}</p> : null}
      {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
      {workspace ? <form onSubmit={(event) => { event.preventDefault(); void save() }}>
        <fieldset>
          <legend>Record only facts and instructions you authored</legend>
          <label>Disposition<select value={dispositionCode} onChange={(event) => changeDisposition(event.target.value)}><option value="">Select a disposition</option>{workspace.dispositions.map((option) => <option key={option.code} value={option.code}>{option.label}</option>)}</select></label>
          <label className="telehealth-check"><input type="checkbox" checked={adequateEvaluationCompleted} onChange={(event) => { setAdequateEvaluationCompleted(event.target.checked); markChanged() }} />The available evaluation was adequate for this selected disposition.</label>
          <div className="telehealth-form-grid">
            <label>Follow-up owner<select value={followUpOwner} onChange={(event) => { setFollowUpOwner(event.target.value); markChanged() }}><option value="">Select an owner</option>{workspace.followUpOwners.map((owner) => <option key={owner} value={owner}>{humanize(owner)}</option>)}</select></label>
            <label>Physician-authored follow-up timeframe<input maxLength={160} value={followUpTimeframe} onChange={(event) => { setFollowUpTimeframe(event.target.value); markChanged() }} autoComplete="off" /></label>
          </div>
          <label>Physician-authored next-step instructions<textarea maxLength={2000} rows={4} value={nextStepInstructions} onChange={(event) => { setNextStepInstructions(event.target.value); markChanged() }} /></label>
          <label>Physician-authored warning signs and escalation instructions<textarea maxLength={2000} rows={4} value={warningEscalationInstructions} onChange={(event) => { setWarningEscalationInstructions(event.target.value); markChanged() }} /></label>
          <label>Communication method<select value={communicationMethod} onChange={(event) => { const value = event.target.value; setCommunicationMethod(value); setCommunicationCompleted(false); markChanged() }}><option value="">Select communication state</option>{workspace.communicationMethods.map((method) => <option key={method} value={method}>{humanize(method)}</option>)}</select></label>
          {communicationMethod && communicationMethod !== 'NotYetCommunicated' ? <label className="telehealth-check"><input type="checkbox" checked={communicationCompleted} onChange={(event) => { setCommunicationCompleted(event.target.checked); markChanged() }} />I completed this selected synthetic communication method.</label> : null}
          {selected?.requiresLocationCallbackReconfirmation ? <label className="telehealth-check"><input type="checkbox" checked={locationCallbackReconfirmed} onChange={(event) => { setLocationCallbackReconfirmed(event.target.checked); markChanged() }} />I reconfirmed the current location and callback number for this disposition.</label> : null}
          {selected?.requiresEmergencyFacts ? <fieldset><legend>Emergency draft facts</legend><label className="telehealth-check"><input type="checkbox" checked={emergencyInstructionProvided} onChange={(event) => { setEmergencyInstructionProvided(event.target.checked); markChanged() }} />I provided physician-authored emergency instructions in this synthetic interaction.</label><label>Factual handoff state<select value={emergencyHandoffStatus} onChange={(event) => { setEmergencyHandoffStatus(event.target.value); markChanged() }}><option value="">Select a state</option>{workspace.emergencyHandoffStatuses.map((state) => <option key={state} value={state}>{humanize(state)}</option>)}</select></label><p><small>“Connected” is only your entered draft fact; AvenChart has not verified any external connection or transfer.</small></p></fieldset> : null}
          {selected?.requiresContactAttemptSummary ? <label>Contact and safety-attempt summary<textarea maxLength={2000} rows={4} value={contactAttemptSummary} onChange={(event) => { setContactAttemptSummary(event.target.value); markChanged() }} /></label> : null}
          <label className="telehealth-check"><input type="checkbox" checked={syntheticDataConfirmed} onChange={(event) => { setSyntheticDataConfirmed(event.target.checked); markChanged() }} />I confirm this draft contains synthetic demonstration data only and has no legal or patient-care effect.</label>
          <button className="telehealth-button" type="submit" disabled={!complete || saving}>{saving ? 'Recording draft…' : workspace.currentDraft ? 'Record revised safety draft' : 'Record safety draft'}</button>
        </fieldset>
      </form> : null}
      {workspace?.currentDraft ? <section aria-labelledby={`${headingId}-current`}><h5 id={`${headingId}-current`}>Current unsigned, undelivered draft</h5><p><strong>{humanize(workspace.currentDraft.dispositionCode)}</strong> · version {workspace.currentDraft.version}</p><p>Signed: no. Finalized: no. Patient delivered: no. Legal effect: no.</p></section> : null}
      {workspace ? <ul>{workspace.limitations.map((item) => <li key={item}>{item}</li>)}</ul> : null}
    </section>
  )
}

function humanize(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
}
