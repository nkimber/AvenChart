// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import {
  getTelehealthPrescriptionPreparationDraft,
  recordTelehealthPrescriptionPreparationDraft,
  type TelehealthPrescriptionPreparationDraft,
  type TelehealthPrescriptionPreparationWorkspace,
} from './api.ts'

type Props = { consultationId: string }

type DraftFields = {
  rxNormCode: string
  doseAmount: string
  doseUnit: string
  frequency: string
  quantityValue: string
  quantityUnit: string
  durationDays: string
  refills: string
  indication: string
  directions: string
}

const emptyFields: DraftFields = {
  rxNormCode: '', doseAmount: '', doseUnit: '', frequency: '', quantityValue: '',
  quantityUnit: '', durationDays: '', refills: '', indication: '', directions: '',
}

export default function TelehealthPrescriptionPreparationPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const commandKey = useRef<string | null>(null)
  const [workspace, setWorkspace] = useState<TelehealthPrescriptionPreparationWorkspace | null>(null)
  const [fields, setFields] = useState<DraftFields>(emptyFields)
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [searching, setSearching] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<string | null>(null)
  const [medicationListReviewed, setMedicationListReviewed] = useState(false)
  const [allergyListReviewed, setAllergyListReviewed] = useState(false)
  const [adequateEvaluationCompleted, setAdequateEvaluationCompleted] = useState(false)
  const [syntheticDataConfirmed, setSyntheticDataConfirmed] = useState(false)

  useEffect(() => { if (error) errorRef.current?.focus() }, [error])

  const applyDraft = useCallback((draft: TelehealthPrescriptionPreparationDraft | null) => {
    setFields(draft ? {
      rxNormCode: draft.rxNormCode,
      doseAmount: String(draft.doseAmount),
      doseUnit: draft.doseUnit,
      frequency: draft.frequency,
      quantityValue: String(draft.quantityValue),
      quantityUnit: draft.quantityUnit,
      durationDays: String(draft.durationDays),
      refills: String(draft.refills),
      indication: draft.indication,
      directions: draft.directions,
    } : emptyFields)
    setMedicationListReviewed(draft?.medicationListReviewed ?? false)
    setAllergyListReviewed(draft?.allergyListReviewed ?? false)
    setAdequateEvaluationCompleted(draft?.adequateEvaluationCompleted ?? false)
    setSyntheticDataConfirmed(false)
    commandKey.current = null
  }, [])

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    setError(null)
    try {
      const result = await getTelehealthPrescriptionPreparationDraft(consultationId, undefined, signal)
      setWorkspace(result)
      applyDraft(result.currentDraft)
      setStatus(result.currentDraft
        ? `Unsigned prescription-preparation draft version ${result.currentDraft.version} loaded.`
        : 'No prescription-preparation draft recorded. No medication is selected by default.')
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setError(caught instanceof Error ? caught.message : 'The prescription-preparation workspace could not be loaded.')
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
    setStatus('Unsaved preparation changes. Nothing is saved automatically.')
  }

  function updateField(name: keyof DraftFields, value: string) {
    setFields((current) => ({ ...current, [name]: value }))
    markChanged()
  }

  async function search() {
    const normalized = query.trim()
    if (normalized.length < 2 || searching) return
    setSearching(true)
    setError(null)
    setStatus('Searching the deterministic non-controlled synthetic catalog…')
    try {
      const result = await getTelehealthPrescriptionPreparationDraft(consultationId, normalized)
      setWorkspace(result)
      setStatus(result.catalogResults.length
        ? `${result.catalogResults.length} non-controlled synthetic catalog result${result.catalogResults.length === 1 ? '' : 's'} returned. No medication or dose was selected.`
        : 'No permitted synthetic catalog result matched. Controlled and unknown entries fail closed.')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The synthetic medication catalog search failed.')
      setStatus('No selection or prescription-preparation change occurred.')
    } finally {
      setSearching(false)
    }
  }

  const numbersValid = Number(fields.doseAmount) > 0
    && Number(fields.doseAmount) <= 100000
    && Number(fields.quantityValue) > 0
    && Number(fields.quantityValue) <= 100000
    && Number.isInteger(Number(fields.durationDays))
    && Number(fields.durationDays) >= 1
    && Number(fields.durationDays) <= 365
    && Number.isInteger(Number(fields.refills))
    && Number(fields.refills) >= 0
    && Number(fields.refills) <= 5
  const complete = Boolean(
    workspace?.currentPharmacyChoiceVersion
    && fields.rxNormCode
    && numbersValid
    && fields.doseUnit.trim()
    && fields.frequency.trim()
    && fields.quantityUnit.trim()
    && fields.indication.trim()
    && fields.directions.trim()
    && medicationListReviewed
    && allergyListReviewed
    && adequateEvaluationCompleted
    && syntheticDataConfirmed,
  )

  async function save() {
    if (!workspace || !complete || saving) return
    setSaving(true)
    setError(null)
    setStatus('Recording an unsigned, unchecked NON_PRODUCTION preparation draft…')
    commandKey.current ??= crypto.randomUUID()
    try {
      const draft = await recordTelehealthPrescriptionPreparationDraft(consultationId, {
        expectedVersion: workspace.currentDraft?.version ?? 0,
        rxNormCode: fields.rxNormCode,
        doseAmount: Number(fields.doseAmount),
        doseUnit: fields.doseUnit.trim(),
        frequency: fields.frequency.trim(),
        quantityValue: Number(fields.quantityValue),
        quantityUnit: fields.quantityUnit.trim(),
        durationDays: Number(fields.durationDays),
        refills: Number(fields.refills),
        indication: fields.indication.trim(),
        directions: fields.directions.trim(),
        medicationListReviewed: true,
        allergyListReviewed: true,
        adequateEvaluationCompleted: true,
        syntheticDataConfirmed: true,
      }, commandKey.current)
      setWorkspace((current) => current ? { ...current, currentDraft: draft } : current)
      setSyntheticDataConfirmed(false)
      commandKey.current = null
      setStatus(`Preparation draft version ${draft.version} recorded. It was not safety checked, signed, created as a prescription, or transmitted.`)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The preparation draft was not recorded. Reload before retrying a conflict.')
      setStatus('No canonical prescription, transmission, or lifecycle action occurred. You may retry the unchanged command.')
    } finally {
      setSaving(false)
    }
  }

  const selected = workspace?.catalogResults.find((item) => item.rxNormCode === fields.rxNormCode)
    ?? (workspace?.currentDraft?.rxNormCode === fields.rxNormCode ? workspace.currentDraft : null)

  return (
    <section className="telehealth-prescription-preparation" aria-labelledby={headingId} aria-busy={loading || searching || saving}>
      <div className="telehealth-heading"><div><p className="telehealth-kicker">Unsigned NON_PRODUCTION planning draft</p><h4 id={headingId}>Prescription preparation</h4></div><button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading || saving} onClick={() => void load()}>{loading ? 'Loading…' : 'Reload saved draft'}</button></div>
      <p role="note">This is not a prescription or drug recommendation. No interaction or contraindication service is active. Nothing can be signed, sent, delivered, or used to complete the visit.</p>
      {status ? <p role="status">{status}</p> : null}
      {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
      {workspace ? <>
        <form className="telehealth-prescription-search" role="search" onSubmit={(event) => { event.preventDefault(); void search() }}>
          <label>Search the synthetic medication catalog<input value={query} maxLength={100} autoComplete="off" onChange={(event) => setQuery(event.target.value)} /></label>
          <button className="telehealth-button telehealth-button-secondary" type="submit" disabled={searching || query.trim().length < 2}>{searching ? 'Searching…' : 'Search catalog'}</button>
        </form>
        {workspace.catalogResults.length ? <fieldset className="telehealth-prescription-catalog"><legend>Reference facts — choose explicitly</legend>{workspace.catalogResults.map((item) => <label key={item.rxNormCode} className="telehealth-catalog-choice"><input type="radio" name={`${headingId}-catalog`} value={item.rxNormCode} checked={fields.rxNormCode === item.rxNormCode} onChange={() => updateField('rxNormCode', item.rxNormCode)} /><span><strong>{item.displayName}</strong><small>{item.form} · {item.strength} · catalog route {item.route} · RxNorm {item.rxNormCode}</small></span></label>)}</fieldset> : null}
        <form onSubmit={(event) => { event.preventDefault(); void save() }}>
          <fieldset disabled={saving}>
            <legend>Physician-entered preparation fields</legend>
            <p>{selected ? <><strong>Selected catalog fact:</strong> {selected.displayName} ({selected.rxNormCode}).</> : 'No medication selected.'} Catalog metadata does not populate or recommend the fields below.</p>
            <div className="telehealth-form-grid">
              <label>Dose amount<input inputMode="decimal" value={fields.doseAmount} onChange={(event) => updateField('doseAmount', event.target.value)} /></label>
              <label>Dose unit<input maxLength={40} value={fields.doseUnit} onChange={(event) => updateField('doseUnit', event.target.value)} /></label>
              <label>Frequency<input maxLength={160} value={fields.frequency} onChange={(event) => updateField('frequency', event.target.value)} /></label>
              <label>Quantity<input inputMode="decimal" value={fields.quantityValue} onChange={(event) => updateField('quantityValue', event.target.value)} /></label>
              <label>Quantity unit<input maxLength={40} value={fields.quantityUnit} onChange={(event) => updateField('quantityUnit', event.target.value)} /></label>
              <label>Duration in days<input inputMode="numeric" value={fields.durationDays} onChange={(event) => updateField('durationDays', event.target.value)} /></label>
              <label>Refills (0–5)<input inputMode="numeric" value={fields.refills} onChange={(event) => updateField('refills', event.target.value)} /></label>
            </div>
            <label>Physician-entered indication<textarea rows={3} maxLength={500} value={fields.indication} onChange={(event) => updateField('indication', event.target.value)} /></label>
            <label>Physician-entered directions<textarea rows={4} maxLength={1000} value={fields.directions} onChange={(event) => updateField('directions', event.target.value)} /></label>
            <fieldset><legend>Required acknowledgments</legend>
              <label className="telehealth-check"><input type="checkbox" checked={medicationListReviewed} onChange={(event) => { setMedicationListReviewed(event.target.checked); markChanged() }} />I reviewed the current medication information with the patient.</label>
              <label className="telehealth-check"><input type="checkbox" checked={allergyListReviewed} onChange={(event) => { setAllergyListReviewed(event.target.checked); markChanged() }} />I reviewed the current allergy information with the patient.</label>
              <label className="telehealth-check"><input type="checkbox" checked={adequateEvaluationCompleted} onChange={(event) => { setAdequateEvaluationCompleted(event.target.checked); markChanged() }} />The available evaluation was adequate to prepare this physician-authored draft.</label>
              <label className="telehealth-check"><input type="checkbox" checked={syntheticDataConfirmed} onChange={(event) => { setSyntheticDataConfirmed(event.target.checked); markChanged() }} />I confirm this contains synthetic demonstration data only and has no legal or patient-care effect.</label>
            </fieldset>
            <p><small>{workspace.currentPharmacyChoiceVersion ? `Current patient-confirmed pharmacy draft version ${workspace.currentPharmacyChoiceVersion} will be referenced without exposing its identity here.` : 'A current patient-confirmed pharmacy choice is required before this preparation draft can be recorded.'}</small></p>
            <button className="telehealth-button" type="submit" disabled={!complete || saving}>{saving ? 'Recording preparation…' : workspace.currentDraft ? 'Record revised preparation draft' : 'Record preparation draft'}</button>
          </fieldset>
        </form>
        {workspace.currentDraft ? <section aria-labelledby={`${headingId}-current`}><h5 id={`${headingId}-current`}>Current preparation draft</h5><p><strong>{workspace.currentDraft.displayName}</strong> · version {workspace.currentDraft.version} · pharmacy-choice version {workspace.currentDraft.pharmacyChoiceVersion}.</p><p>Safety checked: no. Signed: no. Canonical prescription created: no. Transmission queued: no. Patient delivered: no. Legal effect: no.</p></section> : null}
        <p className="telehealth-capability-stop"><strong>Unavailable:</strong> interaction/contraindication checking, signing, canonical prescription creation, NCPDP mapping, transmission, patient delivery, and consultation completion.</p>
        <ul>{workspace.limitations.map((item) => <li key={item}>{item}</li>)}</ul>
      </> : null}
    </section>
  )
}
