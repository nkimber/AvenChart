// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useState } from 'react'
import {
  createInventoryReplenishmentPolicyChangeRequest,
  getInventoryReplenishmentPolicies,
  getInventoryReplenishmentPolicyChangeRequest,
  getInventoryVendors,
  transitionInventoryReplenishmentPolicyChangeRequest,
  type InventoryItem,
  type InventoryReplenishmentPolicyChangeRequest,
  type InventoryReplenishmentPolicyChangeRequestDetailResponse,
  type InventoryReplenishmentPolicyDefinition,
  type InventoryVendor,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  facilities: { facilityId: number; code: string; name: string }[]
  items: InventoryItem[]
  onChanged: () => Promise<void>
  sessionId: string
}

const today = new Date().toISOString().slice(0, 10)

function initialDefinition(): InventoryReplenishmentPolicyDefinition {
  return {
    itemId: 0,
    facilityId: 0,
    reorderPoint: 0,
    targetQuantity: 0,
    leadTimeDays: 0,
    safetyStock: 0,
    preferredVendorId: null,
    packSize: 1,
    approvalThreshold: 0,
    effectiveDate: today,
    approvalReference: '',
    rationale: '',
  }
}

function messageOf(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

export default function InventoryReplenishmentPolicyGovernancePanel({
  facilities,
  items,
  onChanged,
  sessionId,
}: Props) {
  const [catalog, setCatalog] = useState<Awaited<ReturnType<typeof getInventoryReplenishmentPolicies>> | null>(null)
  const [vendors, setVendors] = useState<InventoryVendor[]>([])
  const [definition, setDefinition] = useState(initialDefinition)
  const [reason, setReason] = useState('')
  const [selected, setSelected] = useState<InventoryReplenishmentPolicyChangeRequestDetailResponse | null>(null)
  const [decisionNote, setDecisionNote] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [nextCatalog, vendorResponse] = await Promise.all([
        getInventoryReplenishmentPolicies(sessionId),
        getInventoryVendors(sessionId),
      ])
      setCatalog(nextCatalog)
      setVendors(vendorResponse.vendors.filter((vendor) => vendor.active))
    } catch (caught) {
      setError(messageOf(caught, 'Could not load replenishment-policy governance.'))
    } finally {
      setLoading(false)
    }
  }, [sessionId])

  useEffect(() => {
    void load()
  }, [load])

  async function openRequest(request: InventoryReplenishmentPolicyChangeRequest) {
    setBusy(true)
    setError(null)
    try {
      setSelected(await getInventoryReplenishmentPolicyChangeRequest(sessionId, request.requestId))
      setDecisionNote('')
    } catch (caught) {
      setError(messageOf(caught, 'Could not open this replenishment-policy proposal.'))
    } finally {
      setBusy(false)
    }
  }

  async function create(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const created = await createInventoryReplenishmentPolicyChangeRequest(sessionId, {
        proposedDefinition: definition,
        reason,
      })
      setSelected(created)
      setDefinition(initialDefinition())
      setReason('')
      await load()
      showToast('Replenishment-policy proposal created.', 'success')
    } catch (caught) {
      setError(messageOf(caught, 'Could not create the replenishment-policy proposal.'))
    } finally {
      setBusy(false)
    }
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
      const next = await transitionInventoryReplenishmentPolicyChangeRequest(
        sessionId,
        selected.request.requestId,
        action,
        {
          expectedVersion: selected.request.version,
          ...(decisionNote.trim() ? { note: decisionNote.trim() } : {}),
        },
      )
      setSelected(next)
      setDecisionNote('')
      await Promise.all([load(), onChanged()])
      showToast(
        `Replenishment-policy proposal ${action === 'activate' ? 'activated' : `${action}ed`}.`,
        'success',
      )
    } catch (caught) {
      setError(messageOf(caught, `Could not ${action} the replenishment-policy proposal.`))
    } finally {
      setBusy(false)
    }
  }

  const activeCount = catalog?.activePolicies.length ?? 0
  const selectedRequest = selected?.request

  return (
    <section className="cl-card">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Replenishment-policy governance</h2>
          <p className="cl-card-subtitle">
            Controlled, item-and-facility policy versions drive read-only planning recommendations.
          </p>
        </div>
        <span className={activeCount ? 'cl-badge cl-badge-green' : 'cl-badge cl-badge-muted'}>
          {activeCount ? `${activeCount} active ${activeCount === 1 ? 'policy' : 'policies'}` : 'No active policies'}
        </span>
      </div>
      <div className="hint-banner">
        Policies must be proposed, submitted, approved, and explicitly activated. Activation supersedes only the prior policy for the same item and facility. It does not create a purchase requisition or order.
      </div>
      {error && <div className="error-banner" role="alert">{error}</div>}
      {loading ? <p className="cl-empty-text">Loading replenishment-policy governance…</p> : <>
        <form className="inventory-medication-link-form" onSubmit={create}>
          <label className="cl-admin-field"><span>Inventory item</span><select value={definition.itemId || ''} onChange={(event) => setDefinition((current) => ({ ...current, itemId: Number(event.target.value) }))} required><option value="">Select active item</option>{items.map((item) => <option key={item.itemId} value={item.itemId}>{item.itemCode} / {item.name}</option>)}</select></label>
          <label className="cl-admin-field"><span>Facility</span><select value={definition.facilityId || ''} onChange={(event) => setDefinition((current) => ({ ...current, facilityId: Number(event.target.value) }))} required><option value="">Select facility</option>{facilities.map((facility) => <option key={facility.facilityId} value={facility.facilityId}>{facility.code} / {facility.name}</option>)}</select></label>
          <label className="cl-admin-field"><span>Reorder point</span><input type="number" min="0" step="0.01" value={definition.reorderPoint} onChange={(event) => setDefinition((current) => ({ ...current, reorderPoint: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Target quantity</span><input type="number" min="0" step="0.01" value={definition.targetQuantity} onChange={(event) => setDefinition((current) => ({ ...current, targetQuantity: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Safety stock</span><input type="number" min="0" step="0.01" value={definition.safetyStock} onChange={(event) => setDefinition((current) => ({ ...current, safetyStock: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Lead time (days)</span><input type="number" min="0" step="1" value={definition.leadTimeDays} onChange={(event) => setDefinition((current) => ({ ...current, leadTimeDays: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Pack size</span><input type="number" min="0.01" step="0.01" value={definition.packSize} onChange={(event) => setDefinition((current) => ({ ...current, packSize: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Approval threshold</span><input type="number" min="0" step="0.01" value={definition.approvalThreshold} onChange={(event) => setDefinition((current) => ({ ...current, approvalThreshold: Number(event.target.value) }))} required /></label>
          <label className="cl-admin-field"><span>Preferred vendor (optional)</span><select value={definition.preferredVendorId ?? ''} onChange={(event) => setDefinition((current) => ({ ...current, preferredVendorId: event.target.value || null }))}><option value="">No preferred vendor</option>{vendors.map((vendor) => <option key={vendor.vendorId} value={vendor.vendorId}>{vendor.name}</option>)}</select></label>
          <label className="cl-admin-field"><span>Effective date</span><input type="date" value={definition.effectiveDate} onChange={(event) => setDefinition((current) => ({ ...current, effectiveDate: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Approval reference</span><input value={definition.approvalReference} maxLength={160} onChange={(event) => setDefinition((current) => ({ ...current, approvalReference: event.target.value }))} placeholder="Committee record or owner sign-off" required /></label>
          <label className="cl-admin-field"><span>Policy rationale</span><input value={definition.rationale} maxLength={1000} onChange={(event) => setDefinition((current) => ({ ...current, rationale: event.target.value }))} required /></label>
          <label className="cl-admin-field"><span>Proposal reason</span><input value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)} placeholder="Why this proposal is needed" required /></label>
          <button className="cl-btn-primary" disabled={busy} type="submit">{busy ? 'Working…' : 'Create proposal'}</button>
        </form>
        <div className="cl-table-scroll" role="region" aria-label="Replenishment-policy proposals" tabIndex={0}><table className="cl-table"><thead><tr><th>Scope</th><th>Policy inputs</th><th>Status</th><th>Version</th><th>Updated</th><th /></tr></thead><tbody>{catalog?.requests.length ? catalog.requests.map((request) => <tr key={request.requestId}><td><strong>Item {request.proposedDefinition.itemId}</strong><span className="inventory-table-secondary">Facility {request.proposedDefinition.facilityId}</span></td><td>Reorder {request.proposedDefinition.reorderPoint} / target {request.proposedDefinition.targetQuantity}<span className="inventory-table-secondary">Lead {request.proposedDefinition.leadTimeDays} days / safety {request.proposedDefinition.safetyStock} / pack {request.proposedDefinition.packSize}</span></td><td>{request.status}</td><td>{request.version}</td><td>{formatDate(request.updatedAt)}</td><td><button className="link-button" type="button" disabled={busy} onClick={() => void openRequest(request)}>Review</button></td></tr>) : <tr><td colSpan={6}>No replenishment-policy proposals have been recorded.</td></tr>}</tbody></table></div>
      </>}
      {selectedRequest && <section className="inventory-medication-result"><strong>Replenishment proposal / {selectedRequest.status}</strong><span>Created by {selectedRequest.createdBy}; baseline revision {selectedRequest.baselineRevision ?? 'none'}; proposal reason: {selectedRequest.reason}</span><label className="cl-admin-field"><span>Decision note (required for reject/cancel)</span><input value={decisionNote} maxLength={1000} onChange={(event) => setDecisionNote(event.target.value)} placeholder="Approval evidence or decision note" /></label><div className="inventory-medication-current">{selectedRequest.status === 'draft' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('submit')}>Submit for approval</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'submitted' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('approve')}>Approve</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('reject')}>Reject</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}{selectedRequest.status === 'approved' && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transition('activate')}>Activate policy</button><button className="link-button" type="button" disabled={busy} onClick={() => void transition('cancel')}>Cancel</button></>}</div><div className="cl-table-scroll" role="region" aria-label="Replenishment-policy proposal audit history" tabIndex={0}><table className="cl-table"><thead><tr><th>Action</th><th>By</th><th>When</th><th>Note</th></tr></thead><tbody>{selected.events.map((event) => <tr key={event.eventId}><td>{event.action}</td><td>{event.username}</td><td>{formatDate(event.occurredAt)}</td><td>{event.note ?? '—'}</td></tr>)}</tbody></table></div></section>}
    </section>
  )
}
