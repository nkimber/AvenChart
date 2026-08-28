// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import { getTelehealthPharmacyChoices, recordTelehealthPharmacyChoice, type TelehealthPharmacyChoiceWorkspace } from './api.ts'

type Props = {
  consultationId: string
  patientState: 'GA' | 'CA' | 'FL'
}

export default function TelehealthPharmacyChoicePanel({ consultationId, patientState }: Props) {
  const headingId = useId()
  const [query, setQuery] = useState('')
  const [state, setState] = useState<'GA' | 'CA' | 'FL'>(patientState)
  const [postalCode, setPostalCode] = useState('')
  const [originPostalCode, setOriginPostalCode] = useState('')
  const [locationSearchAcknowledged, setLocationSearchAcknowledged] = useState(false)
  const [workspace, setWorkspace] = useState<TelehealthPharmacyChoiceWorkspace | null>(null)
  const [selectedEntryId, setSelectedEntryId] = useState('')
  const [patientChoiceConfirmed, setPatientChoiceConfirmed] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const commandKey = useRef<string | null>(null)
  const errorRef = useRef<HTMLParagraphElement>(null)

  useEffect(() => {
    if (error) errorRef.current?.focus()
  }, [error])

  const load = useCallback(async (signal?: AbortSignal, includeForm = false) => {
    setLoading(true)
    setError(null)
    try {
      const result = await getTelehealthPharmacyChoices(consultationId, includeForm ? {
        query: query.trim() || undefined,
        state,
        postalCode: postalCode.trim() || undefined,
        originPostalCode: originPostalCode.trim() || undefined,
        locationSearchAcknowledged,
        limit: 25,
      } : { state: patientState, limit: 25 }, signal)
      setWorkspace(result)
      setSelectedEntryId(result.currentChoice?.directoryEntryId ?? '')
      setPatientChoiceConfirmed(false)
      setStatus(result.pharmacies.length
        ? `${result.pharmacies.length} neutral synthetic choice${result.pharmacies.length === 1 ? '' : 's'} loaded.`
        : 'No synthetic directory choices matched. Change the search; manual pharmacy resolution is not available in this slice.')
    } catch (caught) {
      if (isRequestCancellation(caught)) return
      setWorkspace(null)
      setError(caught instanceof Error ? caught.message : 'The synthetic pharmacy choices could not be loaded.')
    } finally {
      setLoading(false)
    }
  }, [consultationId, locationSearchAcknowledged, originPostalCode, patientState, postalCode, query, state])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void getTelehealthPharmacyChoices(consultationId, { state: patientState, limit: 25 }, controller.signal)
      .then((result) => {
        setWorkspace(result)
        setSelectedEntryId(result.currentChoice?.directoryEntryId ?? '')
        setPatientChoiceConfirmed(false)
        setStatus(result.pharmacies.length
          ? `${result.pharmacies.length} neutral synthetic choice${result.pharmacies.length === 1 ? '' : 's'} loaded.`
          : 'No synthetic directory choices matched. Change the search; manual pharmacy resolution is not available in this slice.')
      })
      .catch((caught) => {
        if (isRequestCancellation(caught)) return
        setWorkspace(null)
        setError(caught instanceof Error ? caught.message : 'The synthetic pharmacy choices could not be loaded.')
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [consultationId, patientState])

  async function recordChoice() {
    if (!workspace || !selectedEntryId || !patientChoiceConfirmed || saving) return
    setSaving(true)
    setError(null)
    setStatus('Recording the unsigned patient-confirmed destination draft…')
    commandKey.current ??= crypto.randomUUID()
    try {
      const choice = await recordTelehealthPharmacyChoice(
        consultationId,
        workspace.currentChoice?.version ?? 0,
        selectedEntryId,
        commandKey.current,
      )
      setWorkspace((current) => current ? { ...current, currentChoice: choice } : current)
      setPatientChoiceConfirmed(false)
      setStatus(`Destination draft version ${choice.version} recorded. No prescription was created or transmitted.`)
      commandKey.current = null
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The destination draft was not recorded. Reload before retrying a conflict.')
      setStatus('No destination change was recorded. No prescription or transmission occurred.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="telehealth-pharmacy-choice" aria-labelledby={headingId} aria-busy={loading}>
      <div><p className="telehealth-kicker">Neutral synthetic directory</p><h4 id={headingId}>Patient-confirmed pharmacy destination draft</h4></div>
      <p role="note">This records only where the patient says a future prescription should go if you later decide to prescribe. It does not create, sign, route, or transmit a prescription, and it does not endorse a pharmacy.</p>
      <form onSubmit={(event) => { event.preventDefault(); void load(undefined, true) }}>
        <fieldset>
          <legend>Search synthetic directory facts</legend>
          <div className="telehealth-form-grid">
            <label>Name or city<input maxLength={64} value={query} onChange={(event) => setQuery(event.target.value)} autoComplete="off" /></label>
            <label>State<select value={state} onChange={(event) => setState(event.target.value as 'GA' | 'CA' | 'FL')}><option value="GA">Georgia</option><option value="CA">California</option><option value="FL">Florida</option></select></label>
            <label>Filter postal code<input inputMode="numeric" pattern="[0-9]{0,5}" maxLength={5} value={postalCode} onChange={(event) => setPostalCode(event.target.value.replace(/\D/g, '').slice(0, 5))} autoComplete="postal-code" /></label>
            <label>Approximate-distance postal origin<input inputMode="numeric" pattern="[0-9]{5}" maxLength={5} value={originPostalCode} onChange={(event) => setOriginPostalCode(event.target.value.replace(/\D/g, '').slice(0, 5))} aria-describedby={`${headingId}-origin-help`} autoComplete="off" /></label>
          </div>
          <p id={`${headingId}-origin-help`}><small>Optional. This is an address entered for directory search—not automatic home/current location. The server uses a deterministic synthetic postal centroid and exposes no coordinates.</small></p>
          <label className="telehealth-check"><input type="checkbox" checked={locationSearchAcknowledged} onChange={(event) => setLocationSearchAcknowledged(event.target.checked)} />The patient authorized use of this entered postal origin for this directory search.</label>
          <button className="telehealth-button telehealth-button-secondary" type="submit" disabled={loading || (originPostalCode.length > 0 && (!locationSearchAcknowledged || originPostalCode.length !== 5))}>{loading ? 'Searching…' : 'Search neutral choices'}</button>
        </fieldset>
      </form>
      {status ? <p role="status">{status}</p> : null}
      {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
      {workspace ? <>
        <p><small>{workspace.adapterMode} · {workspace.datasetId} · {workspace.datasetVersion}. {workspace.chartPreferenceCount} active synthetic chart preference{workspace.chartPreferenceCount === 1 ? '' : 's'} returned.</small></p>
        {workspace.currentChoice ? <section className="telehealth-current-pharmacy" aria-labelledby={`${headingId}-current`}><h5 id={`${headingId}-current`}>Current unsigned destination draft</h5><p><strong>{workspace.currentChoice.name}</strong><br />{formatAddress(workspace.currentChoice.address)}</p><p><small>Version {workspace.currentChoice.version}, recorded {new Date(workspace.currentChoice.selectedAt).toLocaleString()}. Prescription created: no. Transmitted: no.</small></p></section> : <p>No destination draft has been recorded.</p>}
        {workspace.pharmacies.length ? <form onSubmit={(event) => { event.preventDefault(); void recordChoice() }}>
          <fieldset>
            <legend>Choose the destination the patient confirmed</legend>
            <div className="telehealth-pharmacy-results">
              {workspace.pharmacies.map((pharmacy) => <label className="telehealth-pharmacy-option" key={pharmacy.directoryEntryId}>
                <input type="radio" name={`${headingId}-pharmacy`} value={pharmacy.directoryEntryId} checked={selectedEntryId === pharmacy.directoryEntryId} onChange={() => { setSelectedEntryId(pharmacy.directoryEntryId); setPatientChoiceConfirmed(false); setError(null); commandKey.current = null }} />
                <span><strong>{pharmacy.name}</strong>{pharmacy.isChartPreferred ? <em>Chart preference</em> : null}<span>{formatAddress(pharmacy.address)}</span><small>{pharmacy.phone}{pharmacy.approximateDistanceMiles === null ? '' : ` · approximately ${pharmacy.approximateDistanceMiles.toFixed(1)} miles from entered postal origin`} · electronic routing: synthetic only</small></span>
              </label>)}
            </div>
            <label className="telehealth-check"><input type="checkbox" checked={patientChoiceConfirmed} onChange={(event) => setPatientChoiceConfirmed(event.target.checked)} />The patient chose or confirmed this destination; I understand this is not a prescription or transmission.</label>
            <button className="telehealth-button" type="submit" disabled={saving || !selectedEntryId || !patientChoiceConfirmed}>{saving ? 'Recording draft…' : workspace.currentChoice ? 'Record changed destination draft' : 'Record destination draft'}</button>
          </fieldset>
        </form> : null}
        <ul>{workspace.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
      </> : null}
    </section>
  )
}

function formatAddress(address: { line1: string; line2: string | null; city: string; state: string; postalCode: string }) {
  return `${address.line1}${address.line2 ? `, ${address.line2}` : ''}, ${address.city}, ${address.state} ${address.postalCode}`
}
