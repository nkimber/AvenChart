import { useCallback, useEffect, useState } from 'react'
import {
  createInventoryCostPolicyChangeRequest,
  getInventoryCostPolicies,
  getInventoryCostPolicyChangeRequest,
  transitionInventoryCostPolicyChangeRequest,
  type InventoryCostPolicyChangeRequest,
  type InventoryCostPolicyChangeRequestDetailResponse,
  type InventoryCostPolicyDefinition,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = { sessionId: string }

const initialDefinition: InventoryCostPolicyDefinition = {
  method: 'weighted_average',
  currency: 'USD',
  taxTreatment: 'Exclude recoverable tax',
  freightTreatment: 'Capitalize receipt freight',
  landedCostTreatment: 'No landed-cost allocation',
  roundingRule: 'half_even',
  backdatedEntryRule: 'restatement',
  effectiveDate: new Date().toISOString().slice(0, 10),
  approvalReference: '',
  rationale: '',
}

function messageOf(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function labelForMethod(value: InventoryCostPolicyDefinition['method']) {
  return ({ fifo: 'FIFO', weighted_average: 'Weighted average', specific_identification: 'Specific identification', practice_specific: 'Practice-specific (documented)' })[value]
}

export default function InventoryCostPolicyGovernancePanel({ sessionId }: Props) {
  const [catalog, setCatalog] = useState<Awaited<ReturnType<typeof getInventoryCostPolicies>> | null>(null)
  const [definition, setDefinition] = useState(initialDefinition)
  const [reason, setReason] = useState('')
  const [selected, setSelected] = useState<InventoryCostPolicyChangeRequestDetailResponse | null>(null)
  const [decisionNote, setDecisionNote] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try { setCatalog(await getInventoryCostPolicies(sessionId)) }
    catch (caught) { setError(messageOf(caught, 'Could not load cost-policy governance.')) }
    finally { setLoading(false) }
  }, [sessionId])

  useEffect(() => { void load() }, [load])

  async function openRequest(request: InventoryCostPolicyChangeRequest) {
    setBusy(true); setError(null)
    try { setSelected(await getInventoryCostPolicyChangeRequest(sessionId, request.requestId)); setDecisionNote('') }
    catch (caught) { setError(messageOf(caught, 'Could not open this cost-policy proposal.')) }
    finally { setBusy(false) }
  }

  async function create(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true); setError(null)
    try {
      const created = await createInventoryCostPolicyChangeRequest(sessionId, { proposedDefinition: definition, reason })
      setSelected(created); setReason(''); setDefinition(initialDefinition)
      await load(); showToast('Inventory cost-policy proposal created.', 'success')
    } catch (caught) { setError(messageOf(caught, 'Could not create the cost-policy proposal.')) }
    finally { setBusy(false) }
  }

  async function transition(action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') {
    if (!selected) return
    if ((action === 'reject' || action === 'cancel') && !decisionNote.trim()) {
      setError('A decision note is required for rejection or cancellation.')
      return
    }
    setBusy(true); setError(null)
    try {
      const next = await transitionInventoryCostPolicyChangeRequest(sessionId, selected.request.requestId, action, { expectedVersion: selected.request.version, ...(decisionNote.trim() ? { note: decisionNote.trim() } : {}) })
      setSelected(next); setDecisionNote(''); await load()
      showToast(`Cost-policy proposal ${action === 'activate' ? 'activated' : `${action}ed`}.`, 'success')
    } catch (caught) { setError(messageOf(caught, `Could not ${action} the cost-policy proposal.`)) }
    finally { setBusy(false) }
  }

  const active = catalog?.activePolicy
  const selectedRequest = selected?.request

  return (
    <section className="cl-card">
      <div className="cl-card-header">
        <div><h2 className="cl-card-title">Inventory cost-policy governance</h2><p className="cl-card-subtitle">Explicit approval and effective-date control for the valuation method; not a ledger or general-ledger posting workflow.</p></div>
        <span className={active ? 'cl-badge cl-badge-green' : 'cl-badge cl-badge-muted'}>{active ? `Active revision ${active.revision}` : 'No active policy'}</span>
      </div>
      <div className="hint-banner">No valuation method is assumed. A proposal must be submitted, approved, and explicitly activated before it becomes the organization policy. Existing lot unit costs are operational stock data, not accounting valuation evidence.</div>
      {error && <div className="error-banner" role="alert">{error}</div>}
      {loading ? <p className="cl-empty-text">Loading cost-policy governance…</p> : <>
        {active ? <div className="inventory-medication-current"><strong>{labelForMethod(active.definition.method)} / {active.definition.currency}</strong><span>Effective {active.definition.effectiveDate} / approval {active.definition.approvalReference} / activated by {active.activatedBy}</span><span>Tax: {active.definition.taxTreatment}. Freight: {active.definition.freightTreatment}. Landed cost: {active.definition.landedCostTreatment}. Rounding: {active.definition.roundingRule}. Backdated entries: {active.definition.backdatedEntryRule}.</span></div> : <div className="inventory-medication-current"><strong>Cost policy not selected</strong><span>Finance/product ownership must select, approve, and activate a policy before valuation implementation can begin.</span></div>}
        <form className="inventory-medication-link-form" onSubmit={create}>
          <label className="cl-admin-field"><span>Method</span><select value={definition.method} onChange={(event) => setDefinition((current) => ({ ...current, method: event.target.value as InventoryCostPolicyDefinition['method'] }))}><option value="fifo">FIFO</option><option value="weighted_average">Weighted average</option><option value="specific_identification">Specific identification</option><option value="practice_specific">Practice-specific (documented)</option></select></label>
          <label className="cl-admin-field"><span>Currency</span><input value={definition.currency} maxLength={3} onChange={(event) => setDefinition((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} required /></label>
          <label className="cl-admin-field"><span>Effective date</span><input type="date" value={definition.effectiveDate} onChange={(event) => setDefinition((current) => ({ ...current, effectiveDate: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Rounding</span><select value={definition.roundingRule} onChange={(event) => setDefinition((current) => ({ ...current, roundingRule: event.target.value as InventoryCostPolicyDefinition['roundingRule'] }))}><option value="half_even">Half even</option><option value="half_up">Half up</option><option value="truncate">Truncate</option></select></label>
          <label className="cl-admin-field"><span>Backdated entries</span><select value={definition.backdatedEntryRule} onChange={(event) => setDefinition((current) => ({ ...current, backdatedEntryRule: event.target.value as InventoryCostPolicyDefinition['backdatedEntryRule'] }))}><option value="restatement">Restatement</option><option value="prohibited">Prohibited</option></select></label>
          <label className="cl-admin-field"><span>Tax treatment</span><input value={definition.taxTreatment} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, taxTreatment: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Freight treatment</span><input value={definition.freightTreatment} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, freightTreatment: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Landed-cost treatment</span><input value={definition.landedCostTreatment} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, landedCostTreatment: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Approval reference</span><input value={definition.approvalReference} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, approvalReference: event.target.value }))} placeholder="Committee record or owner sign-off" required /></label>
          <label className="cl-admin-field"><span>Policy rationale</span><input value={definition.rationale} maxLength={1000} onChange={(event) => setDefinition((current) => ({ ...current, rationale: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Proposal reason</span><input value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)} placeholder="Why this proposal is needed" required /></label>
          <button className="cl-btn-primary" disabled={busy} type="submit">{busy ? 'Working…' : 'Create proposal'}</button>
        </form>
        <div className="cl-table-scroll" role="region" aria-label="Inventory cost-policy proposals" tabIndex={0}><table className="cl-table"><thead><tr><th>Method</th><th>Status</th><th>Version</th><th>Effective</th><th>Updated</th><th /></tr></thead><tbody>{catalog?.requests.length ? catalog.requests.map((request) => <tr key={request.requestId}><td><strong>{labelForMethod(request.proposedDefinition.method)}</strong><span className="inventory-table-secondary">{request.proposedDefinition.currency} / {request.proposedDefinition.approvalReference}</span></td><td>{request.status}</td><td>{request.version}</td><td>{request.proposedDefinition.effectiveDate}</td><td>{formatDate(request.updatedAt)}</td><td><button className="link-button" type="button" disabled={busy} onClick={() => void openRequest(request)}>Review</button></td></tr>) : <tr><td colSpan={6}>No cost-policy proposals have been recorded.</td></tr>}</tbody></table></div>
      </>}
      {selectedRequest && <section className="inventory-medication-result"><strong>{labelForMethod(selectedRequest.proposedDefinition.method)} proposal / {selectedRequest.status}</strong><span>Created by {selectedRequest.createdBy}; baseline revision {selectedRequest.baselineRevision ?? 'none'}; proposal reason: {selectedRequest.reason}</span><label className="cl-admin-field"><span>Decision note {selectedRequest.status === 'draft' || selectedRequest.status === 'submitted' || selectedRequest.status === 'approved' ? '(required for reject/cancel)' : ''}</span><input value={decisionNote} maxLength={1000} onChange={(event) => setDecisionNote(event.target.value)} placeholder="Approval evidence or decision note" /></label><div className="inventory-medication-current">{selectedRequest.status === 'draft' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('submit')}>Submit for approval</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'submitted' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('approve')}>Approve</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('reject')}>Reject</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'approved' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('activate')}>Activate policy</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}</div><div className="cl-table-scroll" role="region" aria-label="Cost-policy proposal audit history" tabIndex={0}><table className="cl-table"><thead><tr><th>Action</th><th>By</th><th>When</th><th>Note</th></tr></thead><tbody>{selected.events.map((event) => <tr key={event.eventId}><td>{event.action}</td><td>{event.username}</td><td>{formatDate(event.occurredAt)}</td><td>{event.note ?? '—'}</td></tr>)}</tbody></table></div></section>}
    </section>
  )
}
