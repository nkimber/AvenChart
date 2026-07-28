import { useEffect, useEffectEvent, useMemo, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  getInventory,
  getInventoryActivityReport,
  getInventoryLotMetadataHistory,
  type InventoryItem,
  type InventoryLot,
  type InventoryLotMetadataAuditItem,
  type InventoryResponse,
  type InventoryTransactionItem,
} from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'
import InventoryActivityPanel from './InventoryActivityPanel.tsx'
import InventoryDispensingPanel from './InventoryDispensingPanel.tsx'
import InventoryMedicationLinksPanel from './InventoryMedicationLinksPanel.tsx'
import InventoryReceivingPanel from './InventoryReceivingPanel.tsx'
import InventoryReplenishmentPanel from './InventoryReplenishmentPanel.tsx'
import InventoryRequisitionsPanel from './InventoryRequisitionsPanel.tsx'
import InventoryStockActionsPanel from './InventoryStockActionsPanel.tsx'

type LotWithItem = {
  item: InventoryItem
  lot: InventoryLot
}

type LotDetailState =
  | { status: 'idle' }
  | { status: 'loading' }
  | {
      status: 'ready'
      metadataHistory: InventoryLotMetadataAuditItem[]
      ledger: InventoryTransactionItem[]
      ledgerTotal: number
    }
  | { status: 'error'; message: string }

const LOTS_PER_PAGE = 6

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

function formatDate(value?: string | null) {
  if (!value) return 'Not tracked'
  return new Date(`${value}T00:00:00`).toLocaleDateString()
}

export default function InventoryWorkspace() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [data, setData] = useState<InventoryResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [lotQuery, setLotQuery] = useState('')
  const [lotFacility, setLotFacility] = useState('')
  const [lotStatus, setLotStatus] = useState('')
  const [lotPage, setLotPage] = useState(1)
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null)
  const [workflowRefreshToken, setWorkflowRefreshToken] = useState(0)
  const [lotDetail, setLotDetail] = useState<LotDetailState>({
    status: 'idle',
  })

  const lots = useMemo(
    () =>
      data?.items.flatMap((item) => item.lots.map((lot) => ({ item, lot }))) ??
      [],
    [data],
  )
  const filteredLots = useMemo(() => {
    const query = lotQuery.trim().toLowerCase()
    return lots.filter(({ item, lot }) => {
      const matchesQuery =
        !query ||
        [
          item.itemCode,
          item.name,
          item.category,
          lot.lotNumber,
          lot.facilityCode,
          lot.facilityName,
          String(lot.lotId),
        ].some((value) => value.toLowerCase().includes(query))
      const matchesFacility = !lotFacility || lot.facilityCode === lotFacility
      const matchesStatus =
        !lotStatus || lot.status === lotStatus || lot.expiryStatus === lotStatus
      return matchesQuery && matchesFacility && matchesStatus
    })
  }, [lotFacility, lotQuery, lots, lotStatus])
  const lotPageCount = Math.max(
    1,
    Math.ceil(filteredLots.length / LOTS_PER_PAGE),
  )
  const visibleLots = filteredLots.slice(
    (lotPage - 1) * LOTS_PER_PAGE,
    lotPage * LOTS_PER_PAGE,
  )
  const selectedLot = lots.find(({ lot }) => lot.lotId === selectedLotId)

  useEffect(() => {
    setLotPage(1)
  }, [lotFacility, lotQuery, lotStatus])

  function load() {
    setError(null)
    return getInventory(session.sessionId)
      .then(setData)
      .catch(() => setError('Could not load inventory.'))
  }

  const loadOnSessionChange = useEffectEvent(load)
  useEffect(() => {
    void loadOnSessionChange()
  }, [session.sessionId])

  async function loadLotDetail(selectedLot: LotWithItem) {
    setSelectedLotId(selectedLot.lot.lotId)
    setLotDetail({ status: 'loading' })
    try {
      const [metadataHistory, report] = await Promise.all([
        getInventoryLotMetadataHistory(
          session.sessionId,
          selectedLot.lot.lotId,
        ),
        getInventoryActivityReport(session.sessionId, {}),
      ])
      setLotDetail({
        status: 'ready',
        metadataHistory,
        ledger: report.entries.filter(
          (entry) => entry.lotId === selectedLot.lot.lotId,
        ),
        ledgerTotal: report.totalEntries,
      })
    } catch (caught) {
      setLotDetail({
        status: 'error',
        message:
          caught instanceof Error
            ? caught.message
            : 'Could not load the lot history.',
      })
    }
  }

  async function handleInventoryWorkflowChanged() {
    await load()
    setWorkflowRefreshToken((current) => current + 1)
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Inventory</h1>
          <p className="clinician-page-subtitle">
            Stock levels, lot pressure, and recorded handling activity.
          </p>
        </div>
      </div>

      {error && (
        <div className="error-banner" role="alert">
          <span>{error}</span>{' '}
          <button type="button" className="link-button" onClick={load}>
            Retry
          </button>
        </div>
      )}
      {!data && !error && (
        <div className="cl-card" aria-live="polite">
          <span className="sr-only">Loading inventory</span>
          <div className="skeleton-list">
            <div className="skeleton-row" />
          </div>
        </div>
      )}

      {data && (
        <>
          <section className="cl-stats-grid" aria-label="Inventory summary">
            <div className="cl-stat-tile">
              <strong>{data.summary.activeItems}</strong>
              <span>Active items</span>
            </div>
            <div className="cl-stat-tile">
              <strong>{data.summary.belowReorderPoint}</strong>
              <span>Below reorder point</span>
            </div>
            <div className="cl-stat-tile">
              <strong>{data.summary.expiringWithin90Days}</strong>
              <span>Expiry watch</span>
            </div>
          </section>

          <InventoryStockActionsPanel
            facilities={data.facilities}
            items={data.items}
            onChanged={handleInventoryWorkflowChanged}
            sessionId={session.sessionId}
          />

          <InventoryMedicationLinksPanel
            items={data.items}
            onChanged={handleInventoryWorkflowChanged}
            sessionId={session.sessionId}
          />

          <InventoryDispensingPanel
            items={data.items}
            onChanged={handleInventoryWorkflowChanged}
            sessionId={session.sessionId}
          />

          <InventoryReceivingPanel
            facilities={data.facilities}
            items={data.items}
            onChanged={handleInventoryWorkflowChanged}
            sessionId={session.sessionId}
          />

          <InventoryRequisitionsPanel
            facilities={data.facilities}
            items={data.items}
            refreshToken={workflowRefreshToken}
            sessionId={session.sessionId}
          />

          <InventoryReplenishmentPanel
            asOfDate={data.asOfDate}
            datasetId={data.datasetId}
            datasetVersion={data.datasetVersion}
            items={data.items}
          />

          <InventoryActivityPanel
            facilities={data.facilities}
            refreshToken={workflowRefreshToken}
            sessionId={session.sessionId}
          />

          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title">Lots and immutable ledger</h2>
                <p className="cl-card-subtitle">
                  Search the authoritative lot register, then open metadata and
                  transaction evidence. Quantities use each item&apos;s named
                  stocking unit; costs are USD per stocking unit.
                </p>
              </div>
              <span className="cl-badge cl-badge-muted">
                {filteredLots.length} of {lots.length} lots
              </span>
            </div>

            <div className="inventory-lot-filters">
              <label className="cl-admin-field">
                <span>Search lots</span>
                <input
                  type="search"
                  value={lotQuery}
                  onChange={(event) => setLotQuery(event.target.value)}
                  placeholder="Item, lot, facility, or ID"
                />
              </label>
              <label className="cl-admin-field">
                <span>Lot facility</span>
                <select
                  value={lotFacility}
                  onChange={(event) => setLotFacility(event.target.value)}
                >
                  <option value="">All facilities</option>
                  {data.facilities.map((facility) => (
                    <option key={facility.facilityId} value={facility.code}>
                      {facility.code} / {facility.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Lot or expiry status</span>
                <select
                  value={lotStatus}
                  onChange={(event) => setLotStatus(event.target.value)}
                >
                  <option value="">All statuses</option>
                  <option value="active">Active</option>
                  <option value="current">Current expiry</option>
                  <option value="expiring">Expiring within 90 days</option>
                  <option value="expired">Expired</option>
                  <option value="not-tracked">Expiry not tracked</option>
                  <option value="destroyed">Destroyed</option>
                </select>
              </label>
            </div>

            {visibleLots.length === 0 ? (
              <div className="empty-state">
                <p className="empty-state-text">
                  No lots match the active search and filters.
                </p>
              </div>
            ) : (
              <div className="cl-table-scroll">
                <table className="cl-table inventory-lot-table">
                  <thead>
                    <tr>
                      <th>Item</th>
                      <th>Lot</th>
                      <th>Facility</th>
                      <th>Status</th>
                      <th>Expiration</th>
                      <th>On hand</th>
                      <th>Unit cost</th>
                      <th>Lot value</th>
                      <th>
                        <span className="sr-only">Actions</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleLots.map(({ item, lot }) => (
                      <tr key={lot.lotId}>
                        <td>
                          <strong>{item.itemCode}</strong>
                          <span className="inventory-table-secondary">
                            {item.name}
                          </span>
                        </td>
                        <td>
                          <strong>{lot.lotNumber}</strong>
                          <span className="inventory-table-secondary">
                            ID {lot.lotId}
                          </span>
                        </td>
                        <td>
                          {lot.facilityCode}
                          <span className="inventory-table-secondary">
                            {lot.facilityName}
                          </span>
                        </td>
                        <td>
                          <span className="cl-badge cl-badge-muted">
                            {lot.status}
                          </span>
                          <span className="inventory-table-secondary">
                            Expiry: {lot.expiryStatus ?? 'unknown'}
                          </span>
                        </td>
                        <td>{formatDate(lot.expirationDate)}</td>
                        <td>
                          {lot.quantityOnHand} {item.unit}
                        </td>
                        <td>
                          {formatCurrency(lot.unitCost)} per {item.unit}
                        </td>
                        <td>
                          {formatCurrency(lot.quantityOnHand * lot.unitCost)}
                        </td>
                        <td>
                          <button
                            className="cl-link"
                            type="button"
                            aria-label={`Open lot ${lot.lotNumber}`}
                            onClick={() => void loadLotDetail({ item, lot })}
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

            <div className="inventory-lot-pagination" aria-label="Lot pages">
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={lotPage === 1}
                onClick={() => setLotPage((current) => current - 1)}
              >
                Previous
              </button>
              <span aria-live="polite">
                Page {lotPage} of {lotPageCount}
              </span>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={lotPage === lotPageCount}
                onClick={() => setLotPage((current) => current + 1)}
              >
                Next
              </button>
            </div>
          </section>

          {selectedLot && (
            <section className="cl-card" aria-labelledby="inventory-lot-detail">
              <div className="cl-card-header">
                <div>
                  <h2 className="cl-card-title" id="inventory-lot-detail">
                    Lot {selectedLot.lot.lotNumber}
                  </h2>
                  <p className="cl-card-subtitle">
                    {selectedLot.item.itemCode} / {selectedLot.item.name} /
                    stable lot ID {selectedLot.lot.lotId}
                  </p>
                </div>
                <button
                  className="cl-link"
                  type="button"
                  onClick={() => {
                    setSelectedLotId(null)
                    setLotDetail({ status: 'idle' })
                  }}
                >
                  Close detail
                </button>
              </div>

              <dl className="inventory-lot-facts">
                <div>
                  <dt>Facility</dt>
                  <dd>
                    {selectedLot.lot.facilityCode} /{' '}
                    {selectedLot.lot.facilityName}
                  </dd>
                </div>
                <div>
                  <dt>Lifecycle</dt>
                  <dd>
                    {selectedLot.lot.status}; expiry{' '}
                    {selectedLot.lot.expiryStatus ?? 'unknown'}
                  </dd>
                </div>
                <div>
                  <dt>Expiration</dt>
                  <dd>{formatDate(selectedLot.lot.expirationDate)}</dd>
                </div>
                <div>
                  <dt>Quantity on hand</dt>
                  <dd>
                    {selectedLot.lot.quantityOnHand} {selectedLot.item.unit}
                  </dd>
                </div>
                <div>
                  <dt>Unit cost</dt>
                  <dd>
                    {formatCurrency(selectedLot.lot.unitCost)} per{' '}
                    {selectedLot.item.unit}
                  </dd>
                </div>
                <div>
                  <dt>Extended lot value</dt>
                  <dd>
                    {formatCurrency(
                      selectedLot.lot.quantityOnHand * selectedLot.lot.unitCost,
                    )}{' '}
                    USD
                  </dd>
                </div>
              </dl>

              {lotDetail.status === 'loading' && (
                <div className="skeleton-list" aria-live="polite">
                  <span className="sr-only">Loading lot history</span>
                  <div className="skeleton-row" />
                  <div className="skeleton-row" />
                </div>
              )}
              {lotDetail.status === 'error' && (
                <div className="error-banner" role="alert">
                  <span>{lotDetail.message}</span>
                  <button
                    className="link-button"
                    type="button"
                    onClick={() => void loadLotDetail(selectedLot)}
                  >
                    Retry
                  </button>
                </div>
              )}
              {lotDetail.status === 'ready' && (
                <div className="inventory-lot-history-grid">
                  <section aria-labelledby="inventory-metadata-history">
                    <h3
                      className="inventory-detail-heading"
                      id="inventory-metadata-history"
                    >
                      Metadata history
                    </h3>
                    {lotDetail.metadataHistory.length === 0 ? (
                      <p className="cl-empty-text">
                        No metadata changes recorded.
                      </p>
                    ) : (
                      <div className="cl-table-scroll">
                        <table className="cl-table">
                          <thead>
                            <tr>
                              <th>Changed</th>
                              <th>Lot number</th>
                              <th>Expiration</th>
                              <th>Actor</th>
                              <th>Audit ID</th>
                            </tr>
                          </thead>
                          <tbody>
                            {lotDetail.metadataHistory.map((entry) => (
                              <tr key={entry.auditId}>
                                <td>
                                  {new Date(entry.changedAt).toLocaleString()}
                                </td>
                                <td>
                                  {entry.priorLotNumber} → {entry.newLotNumber}
                                </td>
                                <td>
                                  {formatDate(entry.priorExpirationDate)} →{' '}
                                  {formatDate(entry.newExpirationDate)}
                                </td>
                                <td>{entry.changedBy}</td>
                                <td>
                                  <code>{entry.auditId}</code>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </section>

                  <section aria-labelledby="inventory-transaction-ledger">
                    <h3
                      className="inventory-detail-heading"
                      id="inventory-transaction-ledger"
                    >
                      Immutable transaction ledger
                    </h3>
                    <p className="cl-card-subtitle">
                      Recorded quantity deltas are expressed in{' '}
                      {selectedLot.item.unit}. Transactions cannot be edited
                      from this workspace.
                    </p>
                    {lotDetail.ledgerTotal > 500 && (
                      <div className="hint-banner">
                        This lot is filtered from the API&apos;s 500 most recent
                        transactions; the unfiltered activity query matched{' '}
                        {lotDetail.ledgerTotal}.
                      </div>
                    )}
                    {lotDetail.ledger.length === 0 ? (
                      <p className="cl-empty-text">
                        No ledger entries are recorded for this lot.
                      </p>
                    ) : (
                      <div className="cl-table-scroll">
                        <table className="cl-table">
                          <thead>
                            <tr>
                              <th>When</th>
                              <th>Type</th>
                              <th>Quantity delta</th>
                              <th>Reason/evidence</th>
                              <th>Actor</th>
                              <th>Transaction ID</th>
                            </tr>
                          </thead>
                          <tbody>
                            {lotDetail.ledger.map((entry) => (
                              <tr key={entry.transactionId}>
                                <td>
                                  {new Date(entry.occurredAt).toLocaleString()}
                                </td>
                                <td>{entry.transactionType}</td>
                                <td>
                                  {entry.quantityDelta > 0 ? '+' : ''}
                                  {entry.quantityDelta} {selectedLot.item.unit}
                                </td>
                                <td>
                                  {entry.reason ?? 'No reason recorded'}
                                  {entry.counterpartyFacilityCode
                                    ? ` / counterparty ${entry.counterpartyFacilityCode}`
                                    : ''}
                                  {entry.receiptReference
                                    ? ` / receipt ${entry.receiptReference}`
                                    : ''}
                                  {entry.reconciliationId
                                    ? ` / reconciliation ${entry.reconciliationId}`
                                    : ''}
                                </td>
                                <td>{entry.performedBy}</td>
                                <td>
                                  <code>{entry.transactionId}</code>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </section>
                </div>
              )}
            </section>
          )}
        </>
      )}
    </div>
  )
}
