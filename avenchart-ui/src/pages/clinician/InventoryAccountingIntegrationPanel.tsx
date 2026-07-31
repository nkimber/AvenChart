// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useState } from 'react'
import {
  createInventoryAccountingIntegrationChangeRequest,
  getInventoryAccountingIntegrationChangeRequest,
  getInventoryAccountingIntegrationDecision,
  transitionInventoryAccountingIntegrationChangeRequest,
  type InventoryAccountingIntegrationChangeRequest,
  type InventoryAccountingIntegrationChangeRequestDetailResponse,
  type InventoryAccountingIntegrationDecisionDefinition,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = { sessionId: string }

const initialDefinition: InventoryAccountingIntegrationDecisionDefinition = {
  mode: 'external',
  financeOwner: '',
  effectiveDate: new Date().toISOString().slice(0, 10),
  mappingReference: null,
  reconciliationReference: null,
  rationale: '',
}

function messageOf(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

export default function InventoryAccountingIntegrationPanel({ sessionId }: Props) {
  const [catalog, setCatalog] = useState<Awaited<ReturnType<typeof getInventoryAccountingIntegrationDecision>> | null>(null)
  const [definition, setDefinition] = useState(initialDefinition)
  const [reason, setReason] = useState('')
  const [selected, setSelected] = useState<InventoryAccountingIntegrationChangeRequestDetailResponse | null>(null)
  const [decisionNote, setDecisionNote] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try { setCatalog(await getInventoryAccountingIntegrationDecision(sessionId)) }
    catch (caught) { setError(messageOf(caught, 'Could not load the accounting-integration decision.')) }
    finally { setLoading(false) }
  }, [sessionId])

  useEffect(() => { void load() }, [load])

  async function openRequest(request: InventoryAccountingIntegrationChangeRequest) {
    setBusy(true)
    setError(null)
    try { setSelected(await getInventoryAccountingIntegrationChangeRequest(sessionId, request.requestId)); setDecisionNote('') }
    catch (caught) { setError(messageOf(caught, 'Could not open this accounting-integration proposal.')) }
    finally { setBusy(false) }
  }

  async function create(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const created = await createInventoryAccountingIntegrationChangeRequest(sessionId, { proposedDefinition: definition, reason })
      setSelected(created)
      setDefinition(initialDefinition)
      setReason('')
      await load()
      showToast('Accounting-integration decision proposal created.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not create the accounting-integration proposal.')) }
    finally { setBusy(false) }
  }

  async function transition(action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') {
    if (!selected) return
    if ((action === 'reject' || action === 'cancel') && !decisionNote.trim()) {
      setError('A decision note is required for rejection or cancellation.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const next = await transitionInventoryAccountingIntegrationChangeRequest(sessionId, selected.request.requestId, action, { expectedVersion: selected.request.version, ...(decisionNote.trim() ? { note: decisionNote.trim() } : {}) })
      setSelected(next)
      setDecisionNote('')
      await load()
      showToast(`Accounting-integration proposal ${action === 'activate' ? 'activated' : `${action}ed`}.`, 'success')
    } catch (caught) { setError(messageOf(caught, `Could not ${action} the accounting-integration proposal.`)) }
    finally { setBusy(false) }
  }

  const active = catalog?.activeDecision
  const selectedRequest = selected?.request
  const acceptedIntegration = definition.mode === 'integration_accepted'

  return (
    <section className="cl-card">
      <div className="cl-card-header"><div><h2 className="cl-card-title">Inventory accounting-integration decision</h2><p className="cl-card-subtitle">Finance ownership decision for external accounting or an accepted future mapping/reconciliation contract.</p></div><span className={active ? 'cl-badge cl-badge-green' : 'cl-badge cl-badge-muted'}>{active ? `Active revision ${active.revision}` : 'No active decision'}</span></div>
      <div className="hint-banner">No general-ledger or accounts-payable posting is implemented. Selecting an accepted contract records only its approval evidence; it does not configure credentials, connect a vendor, transmit data, or create exports.</div>
      {error && <div className="error-banner" role="alert">{error}</div>}
      {loading ? <p className="cl-empty-text">Loading accounting-integration governance…</p> : <>
        {active ? <div className="inventory-medication-current"><strong>{active.definition.mode === 'external' ? 'Accounting export remains external' : 'Integration contract accepted'}</strong><span>Finance owner: {active.definition.financeOwner} / effective {active.definition.effectiveDate} / activated by {active.activatedBy}</span><span>{active.definition.mode === 'external' ? 'No mapping or reconciliation reference is retained because the product does not post inventory accounting.' : `Mapping: ${active.definition.mappingReference}; reconciliation: ${active.definition.reconciliationReference}.`}</span></div> : <div className="inventory-medication-current"><strong>No accounting-integration decision</strong><span>Financial posting remains unavailable until finance ownership records a governed decision.</span></div>}
        <form className="inventory-medication-link-form" onSubmit={create}>
          <label className="cl-admin-field"><span>Decision</span><select value={definition.mode} onChange={(event) => setDefinition((current) => ({ ...current, mode: event.target.value as InventoryAccountingIntegrationDecisionDefinition['mode'], ...(event.target.value === 'external' ? { mappingReference: null, reconciliationReference: null } : {}) }))}><option value="external">Keep accounting export external</option><option value="integration_accepted">Accept mapping/reconciliation contract</option></select></label>
          <label className="cl-admin-field"><span>Finance owner</span><input value={definition.financeOwner} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, financeOwner: event.target.value }))} placeholder="Named accountable owner" required /></label>
          <label className="cl-admin-field"><span>Effective date</span><input type="date" value={definition.effectiveDate} onChange={(event) => setDefinition((current) => ({ ...current, effectiveDate: event.target.value }))} required /></label>
          {acceptedIntegration && <><label className="cl-admin-field"><span>Mapping reference</span><input value={definition.mappingReference ?? ''} maxLength={500} onChange={(event) => setDefinition((current) => ({ ...current, mappingReference: event.target.value || null }))} required /></label><label className="cl-admin-field"><span>Reconciliation reference</span><input value={definition.reconciliationReference ?? ''} maxLength={500} onChange={(event) => setDefinition((current) => ({ ...current, reconciliationReference: event.target.value || null }))} required /></label></>}
          <label className="cl-admin-field"><span>Decision rationale</span><input value={definition.rationale} maxLength={1000} onChange={(event) => setDefinition((current) => ({ ...current, rationale: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Proposal reason</span><input value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)} placeholder="Why this decision is needed" required /></label>
          <button className="cl-btn-primary" disabled={busy} type="submit">{busy ? 'Working…' : 'Create proposal'}</button>
        </form>
        <div className="cl-table-scroll" role="region" aria-label="Accounting-integration proposals" tabIndex={0}><table className="cl-table"><thead><tr><th>Decision</th><th>Finance owner</th><th>Status</th><th>Version</th><th>Updated</th><th /></tr></thead><tbody>{catalog?.requests.length ? catalog.requests.map((request) => <tr key={request.requestId}><td>{request.proposedDefinition.mode === 'external' ? 'External' : 'Integration accepted'}<span className="inventory-table-secondary">Effective {request.proposedDefinition.effectiveDate}</span></td><td>{request.proposedDefinition.financeOwner}</td><td>{request.status}</td><td>{request.version}</td><td>{formatDate(request.updatedAt)}</td><td><button className="link-button" type="button" disabled={busy} onClick={() => void openRequest(request)}>Review</button></td></tr>) : <tr><td colSpan={6}>No accounting-integration proposals have been recorded.</td></tr>}</tbody></table></div>
      </>}
      {selectedRequest && <section className="inventory-medication-result"><strong>Accounting-integration proposal / {selectedRequest.status}</strong><span>Created by {selectedRequest.createdBy}; baseline revision {selectedRequest.baselineRevision ?? 'none'}; proposal reason: {selectedRequest.reason}</span><label className="cl-admin-field"><span>Decision note (required for reject/cancel)</span><input value={decisionNote} maxLength={1000} onChange={(event) => setDecisionNote(event.target.value)} placeholder="Approval evidence or decision note" /></label><div className="inventory-medication-current">{selectedRequest.status === 'draft' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('submit')}>Submit for approval</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'submitted' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('approve')}>Approve</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('reject')}>Reject</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'approved' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('activate')}>Activate decision</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}</div><div className="cl-table-scroll" role="region" aria-label="Accounting-integration decision audit history" tabIndex={0}><table className="cl-table"><thead><tr><th>Action</th><th>By</th><th>When</th><th>Note</th></tr></thead><tbody>{selected.events.map((event) => <tr key={event.eventId}><td>{event.action}</td><td>{event.username}</td><td>{formatDate(event.occurredAt)}</td><td>{event.note ?? '—'}</td></tr>)}</tbody></table></div></section>}
    </section>
  )
}
