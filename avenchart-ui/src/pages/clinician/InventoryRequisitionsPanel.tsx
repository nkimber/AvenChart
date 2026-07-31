// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  createInventoryPurchaseRequisition,
  decideInventoryPurchaseRequisition,
  getInventoryPurchaseRequisitions,
  getInventoryVendors,
  submitInventoryPurchaseRequisition,
  type InventoryItem,
  type InventoryPurchaseRequisition,
  type InventoryVendor,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  facilities: { facilityId: number; code: string; name: string }[]
  items: InventoryItem[]
  refreshToken: number
  sessionId: string
}

type RequisitionState =
  | { status: 'loading' }
  | {
      status: 'ready'
      requisitions: InventoryPurchaseRequisition[]
      vendors: InventoryVendor[]
    }
  | { status: 'error'; message: string }

type DraftLine = {
  key: string
  itemId: string
  quantity: string
}

const PAGE_SIZE = 5

function replaceRequisition(
  requisitions: InventoryPurchaseRequisition[],
  updated: InventoryPurchaseRequisition,
) {
  return requisitions.map((item) =>
    item.requisitionId === updated.requisitionId ? updated : item,
  )
}

function lifecycleBadge(status: string) {
  if (status === 'approved') return 'cl-badge-green'
  if (status === 'rejected') return 'cl-badge-red'
  if (status === 'submitted') return 'cl-badge-amber'
  return 'cl-badge-muted'
}

export default function InventoryRequisitionsPanel({
  facilities,
  items,
  refreshToken,
  sessionId,
}: Props) {
  const [state, setState] = useState<RequisitionState>({ status: 'loading' })
  const [query, setQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [facilityId, setFacilityId] = useState('')
  const [vendorId, setVendorId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([
    { key: 'initial', itemId: '', quantity: '1' },
  ])
  const [formError, setFormError] = useState<string | null>(null)
  const [decisionNotes, setDecisionNotes] = useState('')
  const [mutationError, setMutationError] = useState<string | null>(null)
  const [mutating, setMutating] = useState(false)

  const load = useCallback(async () => {
    setState({ status: 'loading' })
    try {
      const [requisitions, vendorResponse] = await Promise.all([
        getInventoryPurchaseRequisitions(sessionId),
        getInventoryVendors(sessionId),
      ])
      setState({
        status: 'ready',
        requisitions,
        vendors: vendorResponse.vendors,
      })
    } catch (caught) {
      setState({
        status: 'error',
        message:
          caught instanceof Error
            ? caught.message
            : 'Could not load purchase requisitions.',
      })
    }
  }, [sessionId])

  useEffect(() => {
    void load()
  }, [load, refreshToken])

  useEffect(() => {
    setPage(1)
  }, [query, statusFilter])

  const requisitions = useMemo(
    () => (state.status === 'ready' ? state.requisitions : []),
    [state],
  )
  const filtered = useMemo(() => {
    const normalized = query.trim().toLowerCase()
    return requisitions.filter((requisition) => {
      const matchesStatus = !statusFilter || requisition.status === statusFilter
      const matchesQuery =
        !normalized ||
        [
          requisition.requisitionId,
          requisition.facilityCode,
          requisition.facilityName,
          requisition.vendorName ?? '',
          requisition.requestedBy,
          requisition.notes ?? '',
          ...requisition.lines.flatMap((line) => [
            line.itemCode,
            line.itemName,
          ]),
        ].some((value) => value.toLowerCase().includes(normalized))
      return matchesStatus && matchesQuery
    })
  }, [query, requisitions, statusFilter])
  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const visible = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  const selected = requisitions.find(
    (item) => item.requisitionId === selectedId,
  )

  function updateReady(
    updater: (
      current: InventoryPurchaseRequisition[],
    ) => InventoryPurchaseRequisition[],
  ) {
    setState((current) =>
      current.status === 'ready'
        ? {
            ...current,
            requisitions: updater(current.requisitions),
          }
        : current,
    )
  }

  async function createRequisition(event: React.FormEvent) {
    event.preventDefault()
    const parsedLines = lines.map((line) => ({
      itemId: Number(line.itemId),
      quantity: Number(line.quantity),
    }))
    const distinctItems = new Set(parsedLines.map((line) => line.itemId))
    if (!facilityId) {
      setFormError('Select the requesting facility.')
      return
    }
    if (
      parsedLines.some(
        (line) =>
          !line.itemId || !Number.isFinite(line.quantity) || line.quantity <= 0,
      )
    ) {
      setFormError('Each line requires an active item and positive quantity.')
      return
    }
    if (distinctItems.size !== parsedLines.length) {
      setFormError('Each inventory item may appear only once.')
      return
    }
    setFormError(null)
    setMutating(true)
    try {
      const created = await createInventoryPurchaseRequisition(sessionId, {
        facilityId: Number(facilityId),
        vendorId: vendorId || null,
        notes: notes.trim() || null,
        lines: parsedLines,
      })
      updateReady((current) => [created, ...current])
      setSelectedId(created.requisitionId)
      setCreateOpen(false)
      setFacilityId('')
      setVendorId('')
      setNotes('')
      setLines([{ key: 'initial', itemId: '', quantity: '1' }])
      showToast('Draft purchase requisition created.', 'success')
    } catch (caught) {
      const message =
        caught instanceof Error
          ? caught.message
          : 'Could not create the purchase requisition.'
      setFormError(message)
      showToast(message, 'error')
    } finally {
      setMutating(false)
    }
  }

  async function submitSelected() {
    if (!selected) return
    setMutationError(null)
    setMutating(true)
    try {
      const updated = await submitInventoryPurchaseRequisition(
        sessionId,
        selected.requisitionId,
      )
      updateReady((current) => replaceRequisition(current, updated))
      showToast('Purchase requisition submitted.', 'success')
    } catch (caught) {
      setMutationError(
        caught instanceof Error
          ? caught.message
          : 'Could not submit the purchase requisition.',
      )
    } finally {
      setMutating(false)
    }
  }

  async function decideSelected(decision: 'approve' | 'reject') {
    if (!selected) return
    if (decision === 'reject' && !decisionNotes.trim()) {
      setMutationError('A rejection reason is required.')
      return
    }
    setMutationError(null)
    setMutating(true)
    try {
      const updated = await decideInventoryPurchaseRequisition(
        sessionId,
        selected.requisitionId,
        decision,
        decisionNotes,
      )
      updateReady((current) => replaceRequisition(current, updated))
      setDecisionNotes('')
      showToast(
        `Purchase requisition ${decision === 'approve' ? 'approved' : 'rejected'}.`,
        'success',
      )
    } catch (caught) {
      setMutationError(
        caught instanceof Error
          ? caught.message
          : `Could not ${decision} the purchase requisition.`,
      )
    } finally {
      setMutating(false)
    }
  }

  return (
    <section className="cl-card" aria-labelledby="inventory-requisitions-title">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title" id="inventory-requisitions-title">
            Purchase requisitions
          </h2>
          <p className="cl-card-subtitle">
            Draft, submit, approve, or reject requests with immutable lifecycle
            evidence. Backend authorization remains authoritative for every
            action. Search and paging cover the target&apos;s 100 most recent
            requisitions.
          </p>
        </div>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() => setCreateOpen((open) => !open)}
        >
          {createOpen ? 'Close form' : 'New requisition'}
        </button>
      </div>

      {createOpen && state.status === 'ready' && (
        <form
          className="inventory-requisition-form"
          onSubmit={createRequisition}
        >
          <div className="inventory-lot-filters">
            <label className="cl-admin-field">
              <span>Requesting facility</span>
              <select
                value={facilityId}
                onChange={(event) => setFacilityId(event.target.value)}
                required
              >
                <option value="">Select a facility</option>
                {facilities.map((facility) => (
                  <option key={facility.facilityId} value={facility.facilityId}>
                    {facility.code} / {facility.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Preferred vendor (optional)</span>
              <select
                value={vendorId}
                onChange={(event) => setVendorId(event.target.value)}
              >
                <option value="">No vendor selected</option>
                {state.vendors.map((vendor) => (
                  <option key={vendor.vendorId} value={vendor.vendorId}>
                    {vendor.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Notes (optional)</span>
              <input
                value={notes}
                maxLength={500}
                onChange={(event) => setNotes(event.target.value)}
              />
            </label>
          </div>
          <fieldset className="inventory-requisition-lines">
            <legend>Requested items</legend>
            {lines.map((line, index) => (
              <div className="inventory-requisition-line" key={line.key}>
                <label className="cl-admin-field">
                  <span>Item {index + 1}</span>
                  <select
                    value={line.itemId}
                    onChange={(event) =>
                      setLines((current) =>
                        current.map((item) =>
                          item.key === line.key
                            ? { ...item, itemId: event.target.value }
                            : item,
                        ),
                      )
                    }
                    required
                  >
                    <option value="">Select an item</option>
                    {items.map((item) => (
                      <option key={item.itemId} value={item.itemId}>
                        {item.itemCode} / {item.name} ({item.unit})
                      </option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>Quantity</span>
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={line.quantity}
                    onChange={(event) =>
                      setLines((current) =>
                        current.map((item) =>
                          item.key === line.key
                            ? { ...item, quantity: event.target.value }
                            : item,
                        ),
                      )
                    }
                    required
                  />
                </label>
                {lines.length > 1 && (
                  <button
                    className="cl-link"
                    type="button"
                    onClick={() =>
                      setLines((current) =>
                        current.filter((item) => item.key !== line.key),
                      )
                    }
                  >
                    Remove line
                  </button>
                )}
              </div>
            ))}
          </fieldset>
          <div className="inventory-requisition-form-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={lines.length >= 25}
              onClick={() =>
                setLines((current) => [
                  ...current,
                  {
                    key: crypto.randomUUID(),
                    itemId: '',
                    quantity: '1',
                  },
                ])
              }
            >
              Add line
            </button>
            <button
              className="cl-btn-primary"
              type="submit"
              disabled={mutating}
            >
              {mutating ? 'Creating...' : 'Create draft'}
            </button>
          </div>
          {formError && (
            <div className="error-banner" role="alert">
              {formError}
            </div>
          )}
        </form>
      )}

      {state.status === 'loading' && (
        <div className="skeleton-list" aria-live="polite">
          <span className="sr-only">Loading purchase requisitions</span>
          <div className="skeleton-row" />
          <div className="skeleton-row" />
        </div>
      )}
      {state.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{state.message}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void load()}
          >
            Retry
          </button>
        </div>
      )}
      {state.status === 'ready' && (
        <>
          <div className="inventory-requisition-filters">
            <label className="cl-admin-field">
              <span>Search requisitions</span>
              <input
                type="search"
                value={query}
                placeholder="ID, item, facility, vendor, or requester"
                onChange={(event) => setQuery(event.target.value)}
              />
            </label>
            <label className="cl-admin-field">
              <span>Lifecycle status</span>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
              >
                <option value="">All statuses</option>
                <option value="draft">Draft</option>
                <option value="submitted">Submitted</option>
                <option value="approved">Approved</option>
                <option value="rejected">Rejected</option>
              </select>
            </label>
            <span className="cl-badge cl-badge-muted" aria-live="polite">
              {filtered.length} of {requisitions.length} requisitions
            </span>
          </div>

          {visible.length === 0 ? (
            <p className="cl-empty-text">
              No purchase requisitions match the active filters.
            </p>
          ) : (
            <div
              className="cl-table-scroll"
              role="region"
              aria-label="Purchase requisitions"
              tabIndex={0}
            >
              <table className="cl-table inventory-requisition-table">
                <thead>
                  <tr>
                    <th>Requested</th>
                    <th>Facility/vendor</th>
                    <th>Status</th>
                    <th>Lines</th>
                    <th>Receipt state</th>
                    <th>
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((requisition) => (
                    <tr key={requisition.requisitionId}>
                      <td>
                        {new Date(requisition.requestedAt).toLocaleString()}
                        <span className="inventory-table-secondary">
                          {requisition.requestedBy} /{' '}
                          {requisition.requisitionId}
                        </span>
                      </td>
                      <td>
                        {requisition.facilityCode}
                        <span className="inventory-table-secondary">
                          {requisition.vendorName ?? 'No preferred vendor'}
                        </span>
                      </td>
                      <td>
                        <span
                          className={`cl-badge ${lifecycleBadge(requisition.status)}`}
                        >
                          {requisition.status}
                        </span>
                      </td>
                      <td>{requisition.lines.length}</td>
                      <td>{requisition.receiptStatus}</td>
                      <td>
                        <button
                          className="cl-link"
                          type="button"
                          aria-label={`Open requisition ${requisition.requisitionId}`}
                          onClick={() => {
                            setSelectedId(requisition.requisitionId)
                            setMutationError(null)
                            setDecisionNotes('')
                          }}
                        >
                          Open
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div
            className="inventory-lot-pagination"
            aria-label="Requisition pages"
          >
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={page === 1}
              onClick={() => setPage((current) => current - 1)}
            >
              Previous
            </button>
            <span>
              Page {page} of {pageCount}
            </span>
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={page === pageCount}
              onClick={() => setPage((current) => current + 1)}
            >
              Next
            </button>
          </div>
        </>
      )}

      {selected && (
        <div className="inventory-requisition-detail">
          <div className="cl-card-header">
            <div>
              <h3 className="inventory-detail-heading">
                Requisition {selected.requisitionId}
              </h3>
              <p className="cl-card-subtitle">
                {selected.facilityCode} / {selected.vendorName ?? 'No vendor'} /
                requested by {selected.requestedBy}
              </p>
            </div>
            <button
              className="cl-link"
              type="button"
              onClick={() => setSelectedId(null)}
            >
              Close detail
            </button>
          </div>

          <div
            className="cl-table-scroll"
            role="region"
            aria-label="Purchase requisition lines"
            tabIndex={0}
          >
            <table className="cl-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Requested</th>
                  <th>Received</th>
                  <th>Outstanding</th>
                </tr>
              </thead>
              <tbody>
                {selected.lines.map((line) => (
                  <tr key={line.requisitionLineId}>
                    <td>
                      {line.itemCode} / {line.itemName}
                    </td>
                    <td>
                      {line.requestedQuantity} {line.unit}
                    </td>
                    <td>
                      {line.receivedQuantity} {line.unit}
                    </td>
                    <td>
                      {line.outstandingQuantity} {line.unit}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <h4 className="inventory-detail-heading">
            Immutable lifecycle events
          </h4>
          <ol className="inventory-requisition-events">
            {selected.events.map((event) => (
              <li key={event.eventId}>
                <strong>{event.action}</strong>
                <span>
                  {new Date(event.occurredAt).toLocaleString()} by {event.actor}
                </span>
                {event.note && <span>{event.note}</span>}
                <code>{event.eventId}</code>
              </li>
            ))}
          </ol>

          {selected.status === 'draft' && (
            <button
              className="cl-btn-primary"
              type="button"
              disabled={mutating}
              onClick={() => void submitSelected()}
            >
              Submit for decision
            </button>
          )}
          {selected.status === 'submitted' && (
            <div className="inventory-requisition-decision">
              <label className="cl-admin-field">
                <span>Decision note (required for rejection)</span>
                <textarea
                  rows={3}
                  maxLength={500}
                  value={decisionNotes}
                  onChange={(event) => setDecisionNotes(event.target.value)}
                />
              </label>
              <div className="inventory-requisition-form-actions">
                <button
                  className="cl-btn-primary"
                  type="button"
                  disabled={mutating}
                  onClick={() => void decideSelected('approve')}
                >
                  Approve
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  disabled={mutating || !decisionNotes.trim()}
                  onClick={() => void decideSelected('reject')}
                >
                  Reject
                </button>
              </div>
            </div>
          )}
          {selected.status !== 'draft' && selected.status !== 'submitted' && (
            <p className="cl-empty-text">
              This requisition is {selected.status}; no further lifecycle action
              is available.
            </p>
          )}
          {mutationError && (
            <div className="error-banner" role="alert">
              {mutationError}
            </div>
          )}
        </div>
      )}
    </section>
  )
}
