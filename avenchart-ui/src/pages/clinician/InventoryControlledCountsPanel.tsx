// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  closeInventoryControlledDiscrepancy,
  correctInventoryControlledDiscrepancy,
  createInventoryControlledCountSession,
  getInventoryControlledCountSession,
  getInventoryControlledCountSessions,
  getInventoryControlledSubstanceCatalog,
  investigateInventoryControlledDiscrepancy,
  submitInventoryControlledCountSession,
  type InventoryControlledCountSession,
  type InventoryControlledCountSessionSummary,
  type InventoryControlledSubstanceCatalogResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = { sessionId: string; onChanged: () => Promise<void> }

function messageOf(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function idempotencyKey(prefix: string) {
  return `${prefix}-${globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`}`
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'Not submitted'
}

export default function InventoryControlledCountsPanel({
  sessionId,
  onChanged,
}: Props) {
  const [catalog, setCatalog] = useState<InventoryControlledSubstanceCatalogResponse | null>(null)
  const [sessions, setSessions] = useState<InventoryControlledCountSessionSummary[]>([])
  const [selected, setSelected] = useState<InventoryControlledCountSession | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [locationId, setLocationId] = useState('')
  const [countType, setCountType] = useState('cycle')
  const [movementLockActive, setMovementLockActive] = useState(true)
  const [createReason, setCreateReason] = useState('')
  const [counterSessionId, setCounterSessionId] = useState('')
  const [actionNotes, setActionNotes] = useState('')
  const [witnessSessionId, setWitnessSessionId] = useState('')
  const [observations, setObservations] = useState<Record<number, string>>({})

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [nextCatalog, nextSessions] = await Promise.all([
        getInventoryControlledSubstanceCatalog(sessionId),
        getInventoryControlledCountSessions(sessionId),
      ])
      setCatalog(nextCatalog)
      setSessions(nextSessions)
      if (!locationId && nextCatalog.locations[0]) setLocationId(nextCatalog.locations[0].locationId)
    } catch (caught) {
      setError(messageOf(caught, 'Could not load controlled-count operations.'))
    } finally {
      setLoading(false)
    }
  }, [locationId, sessionId])

  useEffect(() => { void load() }, [load])

  const lockedSessions = useMemo(
    () => sessions.filter((session) => session.movementLockActive),
    [sessions],
  )
  const unresolvedSessions = useMemo(
    () => sessions.filter((session) => session.openDiscrepancyCount > 0),
    [sessions],
  )

  async function selectSession(countSessionId: string) {
    setError(null)
    setBusy(true)
    try {
      const detail = await getInventoryControlledCountSession(sessionId, countSessionId)
      setSelected(detail)
      setObservations(Object.fromEntries(detail.lines.map((line) => [line.lotId, String(line.expectedQuantity)])))
      setActionNotes('')
      setCounterSessionId('')
      setWitnessSessionId('')
    } catch (caught) {
      setError(messageOf(caught, 'Could not open this controlled count.'))
    } finally {
      setBusy(false)
    }
  }

  async function refreshSelected(session: InventoryControlledCountSession | null) {
    await load()
    if (!session) return
    const refreshed = await getInventoryControlledCountSession(sessionId, session.sessionId)
    setSelected(refreshed)
  }

  async function createCount(event: React.FormEvent) {
    event.preventDefault()
    if (!locationId || !createReason.trim()) {
      setError('Choose a secure location and record why this count is needed.')
      return
    }
    setBusy(true); setError(null)
    try {
      const count = await createInventoryControlledCountSession(sessionId, {
        locationId, countType, movementLockActive, reason: createReason.trim(), idempotencyKey: idempotencyKey('ui-count'),
      })
      setCreateReason('')
      setSelected(count)
      setObservations(Object.fromEntries(count.lines.map((line) => [line.lotId, String(line.expectedQuantity)])))
      await load()
      showToast('Controlled count opened.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not open the controlled count.')) } finally { setBusy(false) }
  }

  async function submitCount(event: React.FormEvent) {
    event.preventDefault()
    if (!selected || !counterSessionId.trim() || !actionNotes.trim()) {
      setError('A different authenticated counter session and submission reason are required.')
      return
    }
    const nextObservations = selected.lines.map((line) => ({ lotId: line.lotId, observedQuantity: Number(observations[line.lotId]) }))
    if (nextObservations.some((line) => !Number.isFinite(line.observedQuantity) || line.observedQuantity < 0)) {
      setError('Every observed quantity must be zero or greater.')
      return
    }
    setBusy(true); setError(null)
    try {
      const count = await submitInventoryControlledCountSession(sessionId, selected.sessionId, { counterSessionId: counterSessionId.trim(), reason: actionNotes.trim(), idempotencyKey: idempotencyKey('ui-count-submit'), observations: nextObservations })
      setSelected(count); setActionNotes(''); setCounterSessionId('')
      await load(); await onChanged()
      showToast('Independent controlled count submitted.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not submit the controlled count.')) } finally { setBusy(false) }
  }

  async function investigate(discrepancyId: string) {
    if (!actionNotes.trim()) { setError('Investigation notes are required.'); return }
    setBusy(true); setError(null)
    try {
      const count = await investigateInventoryControlledDiscrepancy(sessionId, discrepancyId, actionNotes.trim())
      setSelected(count); setActionNotes(''); await load()
      showToast('Discrepancy investigation recorded.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not start the discrepancy investigation.')) } finally { setBusy(false) }
  }

  async function correct(discrepancyId: string) {
    if (!actionNotes.trim()) { setError('Correction notes are required.'); return }
    setBusy(true); setError(null)
    try {
      await correctInventoryControlledDiscrepancy(sessionId, discrepancyId, { notes: actionNotes.trim(), idempotencyKey: idempotencyKey('ui-count-correction'), ...(witnessSessionId.trim() ? { witnessSessionId: witnessSessionId.trim() } : {}) })
      setActionNotes(''); setWitnessSessionId(''); await refreshSelected(selected); await onChanged()
      showToast('Compensating custody correction recorded.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not record the discrepancy correction.')) } finally { setBusy(false) }
  }

  async function close(discrepancyId: string) {
    if (!actionNotes.trim()) { setError('Closure notes are required.'); return }
    setBusy(true); setError(null)
    try {
      const count = await closeInventoryControlledDiscrepancy(sessionId, discrepancyId, actionNotes.trim())
      setSelected(count); setActionNotes(''); await load()
      showToast('Controlled discrepancy closed.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not close the controlled discrepancy.')) } finally { setBusy(false) }
  }

  return (
    <section className="cl-card">
      <div className="cl-card-header">
        <div><h2 className="cl-card-title">Controlled count operations</h2><p className="cl-card-subtitle">Secure-location count evidence, movement locks, and discrepancy resolution.</p></div>
        <span className={lockedSessions.length ? 'cl-badge cl-badge-red' : 'cl-badge cl-badge-green'}>{lockedSessions.length ? `${lockedSessions.length} movement lock${lockedSessions.length === 1 ? '' : 's'}` : 'No movement locks'}</span>
      </div>
      <div className="hint-banner">A movement lock cannot be bypassed here. Submit the independent count to release it; corrections remain separately witnessed where the secure location requires it.</div>
      {error && <div className="error-banner" role="alert">{error}</div>}
      {loading ? <p className="cl-empty-text">Loading controlled count operations…</p> : <>
        <section className="cl-stats-grid" aria-label="Controlled count status"><div className="cl-stat-tile"><strong>{lockedSessions.length}</strong><span>Locked locations</span></div><div className="cl-stat-tile"><strong>{unresolvedSessions.length}</strong><span>Open discrepancies</span></div><div className="cl-stat-tile"><strong>{catalog?.locations.length ?? 0}</strong><span>Secure locations</span></div></section>
        <form className="inventory-medication-link-form" onSubmit={createCount}>
          <label className="cl-admin-field"><span>Secure location</span><select value={locationId} onChange={(event) => setLocationId(event.target.value)} required>{catalog?.locations.map((location) => <option key={location.locationId} value={location.locationId}>{location.locationCode} / {location.displayName}{location.dualAttestationRequired ? ' / witness required' : ''}</option>)}</select></label>
          <label className="cl-admin-field"><span>Count type</span><select value={countType} onChange={(event) => setCountType(event.target.value)}><option value="opening">Opening</option><option value="shift">Shift</option><option value="cycle">Cycle</option><option value="closing">Closing</option></select></label>
          <label className="cl-admin-field"><span>Reason</span><input value={createReason} maxLength={500} onChange={(event) => setCreateReason(event.target.value)} placeholder="Why is this count required?" required /></label>
          <label className="cl-admin-field"><span><input type="checkbox" checked={movementLockActive} onChange={(event) => setMovementLockActive(event.target.checked)} /> Lock custody movements until submitted</span></label>
          <button className="cl-btn-primary" disabled={busy} type="submit">{busy ? 'Working…' : 'Open controlled count'}</button>
        </form>
        <div className="cl-table-scroll" role="region" aria-label="Controlled count sessions" tabIndex={0}><table className="cl-table"><thead><tr><th>Location</th><th>Status</th><th>Lock</th><th>Lines</th><th>Discrepancies</th><th>Started</th><th /></tr></thead><tbody>{sessions.length === 0 ? <tr><td colSpan={7}>No controlled count sessions have been recorded.</td></tr> : sessions.map((session) => <tr key={session.sessionId}><td><strong>{session.locationCode}</strong><span className="inventory-table-secondary">{session.locationName} / {session.countType}</span></td><td>{session.status}</td><td>{session.movementLockActive ? 'Movement locked' : 'Released'}</td><td>{session.lineCount}</td><td>{session.openDiscrepancyCount} open / {session.discrepancyCount} total</td><td>{formatDate(session.startedAt)}</td><td><button className="link-button" type="button" disabled={busy} onClick={() => void selectSession(session.sessionId)}>Review</button></td></tr>)}</tbody></table></div>
      </>}
      {selected && <section className="inventory-medication-result"><strong>{selected.locationCode} / {selected.countType} count / {selected.status}</strong><span>Started by {selected.startedBy} / {formatDate(selected.startedAt)} / {selected.movementLockActive ? 'movements locked' : 'movement lock released'}</span><span>{selected.reason}</span>
        {selected.status === 'in_progress' && <form className="inventory-medication-link-form" onSubmit={submitCount}><label className="cl-admin-field"><span>Independent counter session ID</span><input value={counterSessionId} onChange={(event) => setCounterSessionId(event.target.value)} placeholder="Authenticated second user session UUID" required /></label><label className="cl-admin-field"><span>Submission reason</span><input value={actionNotes} maxLength={500} onChange={(event) => setActionNotes(event.target.value)} required /></label><button className="cl-btn-primary" disabled={busy} type="submit">Submit independent count</button></form>}
        {selected.status !== 'in_progress' && <div className="inventory-medication-current"><label className="cl-admin-field"><span>Resolution notes</span><input value={actionNotes} maxLength={1000} onChange={(event) => setActionNotes(event.target.value)} placeholder="Required for investigation, correction, or closure" /></label><label className="cl-admin-field"><span>Witness session ID (if required)</span><input value={witnessSessionId} onChange={(event) => setWitnessSessionId(event.target.value)} placeholder="Authenticated different user session UUID" /></label></div>}
        <div className="cl-table-scroll" role="region" aria-label="Controlled count lines" tabIndex={0}><table className="cl-table"><thead><tr><th>Item / lot</th><th>Expected</th><th>Observed</th><th>Variance</th><th>Discrepancy</th><th /></tr></thead><tbody>{selected.lines.map((line) => <tr key={line.lineId}><td><strong>{line.itemCode}</strong><span className="inventory-table-secondary">Lot {line.lotNumber}</span></td><td>{line.expectedQuantity}</td><td>{selected.status === 'in_progress' ? <input aria-label={`Observed quantity for ${line.itemCode} lot ${line.lotNumber}`} inputMode="decimal" value={observations[line.lotId] ?? ''} onChange={(event) => setObservations((current) => ({ ...current, [line.lotId]: event.target.value }))} /> : line.observedQuantity ?? '—'}</td><td>{line.varianceQuantity ?? '—'}</td><td>{line.discrepancyStatus ?? 'None'}</td><td>{line.discrepancyId && line.discrepancyStatus === 'open' && <button className="link-button" type="button" disabled={busy} onClick={() => void investigate(line.discrepancyId!)}>Investigate</button>}{line.discrepancyId && line.discrepancyStatus === 'investigating' && <button className="link-button" type="button" disabled={busy} onClick={() => void correct(line.discrepancyId!)}>Correct</button>}{line.discrepancyId && line.discrepancyStatus === 'corrected' && <button className="link-button" type="button" disabled={busy} onClick={() => void close(line.discrepancyId!)}>Close</button>}</td></tr>)}</tbody></table></div>
      </section>}
    </section>
  )
}
