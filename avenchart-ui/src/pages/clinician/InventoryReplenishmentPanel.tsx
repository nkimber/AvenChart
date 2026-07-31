// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useMemo, useState } from 'react'
import {
  getInventoryReplenishmentRecommendations,
  type InventoryReplenishmentRecommendation,
} from '../../api.ts'

type Props = {
  asOfDate: string
  datasetId: string
  datasetVersion: string
  refreshToken: number
  sessionId: string
}

type LoadState =
  | { status: 'loading' }
  | { status: 'ready'; recommendations: InventoryReplenishmentRecommendation[] }
  | { status: 'error' }

const ITEMS_PER_PAGE = 6

export default function InventoryReplenishmentPanel({
  asOfDate,
  datasetId,
  datasetVersion,
  refreshToken,
  sessionId,
}: Props) {
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)
  const [state, setState] = useState<LoadState>({ status: 'loading' })

  useEffect(() => {
    const controller = new AbortController()
    setState({ status: 'loading' })
    void getInventoryReplenishmentRecommendations(sessionId, controller.signal)
      .then((recommendations) => setState({ status: 'ready', recommendations }))
      .catch(() => {
        if (!controller.signal.aborted) setState({ status: 'error' })
      })
    return () => controller.abort()
  }, [refreshToken, sessionId])

  const candidates = useMemo(
    () => (state.status === 'ready' ? state.recommendations : []),
    [state],
  )
  const filteredCandidates = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return candidates.filter(
      (item) =>
        !normalizedQuery ||
        [
          item.itemCode,
          item.itemName,
          item.facilityCode,
          item.facilityName,
          item.unit,
          item.preferredVendorName ?? '',
        ].some((value) => value.toLowerCase().includes(normalizedQuery)),
    )
  }, [candidates, query])
  const pageCount = Math.max(
    1,
    Math.ceil(filteredCandidates.length / ITEMS_PER_PAGE),
  )
  const visibleCandidates = filteredCandidates.slice(
    (page - 1) * ITEMS_PER_PAGE,
    page * ITEMS_PER_PAGE,
  )

  useEffect(() => {
    setPage(1)
  }, [candidates, query])

  return (
    <section className="cl-card inventory-replenishment">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Governed replenishment planning</h2>
          <p className="cl-card-subtitle">
            Approved, effective item-and-facility policies identify stock at or
            below its reorder point and calculate a pack-rounded request.
          </p>
        </div>
        <span className="cl-badge cl-badge-muted">
          {state.status === 'loading'
            ? 'Loading'
            : `${candidates.length} candidate${candidates.length === 1 ? '' : 's'}`}
        </span>
      </div>

      <div className="hint-banner">
        A recommendation is not an order. It cannot reserve stock, create or
        prefill a requisition, approve purchasing, receive goods, value
        inventory, or post accounting activity.
      </div>

      <dl className="inventory-replenishment-facts">
        <div>
          <dt>Dataset</dt>
          <dd>
            <code>{datasetId}</code>
          </dd>
        </div>
        <div>
          <dt>Dataset version</dt>
          <dd>{datasetVersion}</dd>
        </div>
        <div>
          <dt>Inventory as-of date</dt>
          <dd>{new Date(`${asOfDate}T00:00:00`).toLocaleDateString()}</dd>
        </div>
        <div>
          <dt>Recommendation formula</dt>
          <dd>Round up policy target or reorder plus safety stock to pack size</dd>
        </div>
      </dl>

      <div className="inventory-replenishment-filter">
        <label className="cl-admin-field">
          <span>Search governed candidates</span>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Item, facility, stocking unit, or preferred vendor"
          />
        </label>
        <span className="cl-badge cl-badge-muted">
          {filteredCandidates.length} of {candidates.length} candidates
        </span>
      </div>

      {state.status === 'loading' && (
        <div className="empty-state" aria-live="polite">
          <p className="empty-state-text">Loading approved replenishment policy evidence.</p>
        </div>
      )}
      {state.status === 'error' && (
        <div className="error-banner" role="alert">
          Could not load governed replenishment recommendations. Try refreshing
          the Inventory workspace.
        </div>
      )}
      {state.status === 'ready' && visibleCandidates.length === 0 && (
        <div className="empty-state">
          <p className="empty-state-text">
            {candidates.length === 0
              ? 'No approved, effective replenishment policy is currently at or below its reorder point.'
              : 'No governed replenishment candidates match this search.'}
          </p>
        </div>
      )}
      {state.status === 'ready' && visibleCandidates.length > 0 && (
        <div
          className="cl-table-scroll"
          role="region"
          aria-label="Governed replenishment candidates"
          tabIndex={0}
        >
          <table className="cl-table inventory-replenishment-table">
            <thead>
              <tr>
                <th>Item / facility</th>
                <th>On hand / reorder</th>
                <th>Target / safety / lead time</th>
                <th>Pack-rounded recommendation</th>
                <th>Policy evidence</th>
              </tr>
            </thead>
            <tbody>
              {visibleCandidates.map((item) => (
                <tr key={item.policyId}>
                  <td>
                    <strong>{item.itemCode}</strong>
                    <span className="inventory-table-secondary">
                      {item.itemName} · {item.facilityCode} / {item.facilityName}
                    </span>
                  </td>
                  <td>
                    {item.onHand} / {item.reorderPoint} {item.unit}
                    <span className="inventory-table-secondary">
                      Aggregate active-lot quantity
                    </span>
                  </td>
                  <td>
                    {item.targetQuantity} / {item.safetyStock} {item.unit} · {item.leadTimeDays} days
                    <span className="inventory-table-secondary">
                      Preferred vendor: {item.preferredVendorName ?? 'Not assigned'}
                    </span>
                  </td>
                  <td>
                    <strong>{item.recommendedQuantity} {item.unit}</strong>
                    <span className="inventory-table-secondary">
                      Pack size {item.packSize}; approval threshold {item.approvalThreshold}
                    </span>
                  </td>
                  <td>
                    <span className="cl-badge cl-badge-amber">
                      Policy rev. {item.policyRevision}
                    </span>
                    <span className="inventory-table-secondary">
                      Effective {new Date(`${item.effectiveDate}T00:00:00`).toLocaleDateString()} · {item.approvalReference}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div
        className="inventory-lot-pagination"
        aria-label="Governed replenishment candidate pages"
      >
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={page === 1}
          onClick={() => setPage((current) => current - 1)}
        >
          Previous
        </button>
        <span aria-live="polite">
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
    </section>
  )
}
