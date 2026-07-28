import { useEffect, useMemo, useState } from 'react'
import type { InventoryItem } from '../../api.ts'

type Props = {
  asOfDate: string
  datasetId: string
  datasetVersion: string
  items: InventoryItem[]
}

const ITEMS_PER_PAGE = 6

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

export default function InventoryReplenishmentPanel({
  asOfDate,
  datasetId,
  datasetVersion,
  items,
}: Props) {
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)
  const candidates = useMemo(
    () =>
      items
        .filter((item) => item.belowReorderPoint)
        .map((item) => ({
          ...item,
          recommendedQuantity: Math.max(
            0,
            item.preferredQuantity - item.quantityOnHand,
          ),
        }))
        .sort(
          (left, right) =>
            right.recommendedQuantity - left.recommendedQuantity ||
            left.itemCode.localeCompare(right.itemCode),
        ),
    [items],
  )
  const filteredCandidates = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return candidates.filter(
      (item) =>
        !normalizedQuery ||
        [item.itemCode, item.name, item.category, item.unit].some((value) =>
          value.toLowerCase().includes(normalizedQuery),
        ),
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
          <h2 className="cl-card-title">Replenishment planning</h2>
          <p className="cl-card-subtitle">
            Review active items at or below their configured reorder point and
            the quantity needed to reach their preferred target.
          </p>
        </div>
        <span className="cl-badge cl-badge-muted">
          {candidates.length} candidate{candidates.length === 1 ? '' : 's'}
        </span>
      </div>

      <div className="hint-banner">
        This is a read-only aggregate planning projection. Reorder points,
        preferred targets, facility allocation, valuation method, approvals, and
        decision evidence are not owner-approved production policy. Requisition
        creation is intentionally unavailable here.
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
          <dt>Candidate rule</dt>
          <dd>Aggregate on hand ≤ reorder point</dd>
        </div>
      </dl>

      <div className="inventory-replenishment-filter">
        <label className="cl-admin-field">
          <span>Search replenishment candidates</span>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Item code, name, category, or stocking unit"
          />
        </label>
        <span className="cl-badge cl-badge-muted">
          {filteredCandidates.length} of {candidates.length} candidates
        </span>
      </div>

      {visibleCandidates.length === 0 ? (
        <div className="empty-state">
          <p className="empty-state-text">
            {candidates.length === 0
              ? 'No active items are at or below their configured reorder point.'
              : 'No replenishment candidates match this search.'}
          </p>
        </div>
      ) : (
        <div className="cl-table-scroll">
          <table className="cl-table inventory-replenishment-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Aggregate on hand</th>
                <th>Reorder point</th>
                <th>Preferred target</th>
                <th>Projected need</th>
                <th>Current lot value</th>
                <th>Planning status</th>
              </tr>
            </thead>
            <tbody>
              {visibleCandidates.map((item) => (
                <tr key={item.itemId}>
                  <td>
                    <strong>{item.itemCode}</strong>
                    <span className="inventory-table-secondary">
                      {item.name} / {item.category}
                    </span>
                  </td>
                  <td>
                    {item.quantityOnHand} {item.unit}
                  </td>
                  <td>
                    {item.reorderPoint} {item.unit}
                  </td>
                  <td>
                    {item.preferredQuantity} {item.unit}
                  </td>
                  <td>
                    <strong>
                      {item.recommendedQuantity} {item.unit}
                    </strong>
                    <span className="inventory-table-secondary">
                      Preferred target minus aggregate on hand
                    </span>
                  </td>
                  <td>
                    {formatCurrency(item.inventoryValue)}
                    <span className="inventory-table-secondary">
                      Current local lot carrying value
                    </span>
                  </td>
                  <td>
                    <span className="cl-badge cl-badge-amber">
                      At or below reorder
                    </span>
                    <span className="inventory-table-secondary">
                      No approved decision recorded
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
        aria-label="Replenishment candidate pages"
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

      <div className="inventory-replenishment-decision">
        <strong>Requisition creation is policy-gated</strong>
        <span>
          Use the separate Purchase requisitions workflow for an authorized
          request. This projection does not create, prefill, approve, reserve,
          receive, value, or post inventory.
        </span>
      </div>
    </section>
  )
}
