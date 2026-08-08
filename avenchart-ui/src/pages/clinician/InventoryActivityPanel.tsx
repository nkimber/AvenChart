// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useMemo, useState } from 'react'
import {
  downloadInventoryActivityCsv,
  getInventoryActivityReport,
  type InventoryActivityReport,
  type InventoryFacility,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  facilities: InventoryFacility[]
  refreshToken: number
  sessionId: string
}

type ReportFilters = {
  from?: string
  to?: string
  facilityId?: number
}

type ReportRun = {
  filters: ReportFilters
  loadedAt: string
  refreshToken: number
  value: InventoryActivityReport
}

type ExportEvidence = {
  downloadedAt: string
  filters: ReportFilters
  filename: string
}

const ENTRIES_PER_PAGE = 8
const REPORT_ENTRY_LIMIT = 500

function caughtMessage(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function formatFilterSummary(
  filters: ReportFilters,
  facilities: InventoryFacility[],
) {
  const facility = facilities.find(
    (candidate) => candidate.facilityId === filters.facilityId,
  )
  return [
    filters.from ? `from ${filters.from}` : 'all start dates',
    filters.to ? `through ${filters.to}` : 'all end dates',
    facility ? `${facility.code} / ${facility.name}` : 'all facilities',
  ].join(' / ')
}

export default function InventoryActivityPanel({
  facilities,
  refreshToken,
  sessionId,
}: Props) {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [query, setQuery] = useState('')
  const [transactionType, setTransactionType] = useState('')
  const [page, setPage] = useState(1)
  const [run, setRun] = useState<ReportRun | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)
  const [exportEvidence, setExportEvidence] = useState<ExportEvidence | null>(
    null,
  )

  const filters = useMemo<ReportFilters>(
    () => ({
      from: from || undefined,
      to: to || undefined,
      facilityId: facilityId ? Number(facilityId) : undefined,
    }),
    [facilityId, from, to],
  )
  const transactionTypes = useMemo(
    () =>
      [...new Set(run?.value.entries.map((entry) => entry.transactionType))]
        .filter(Boolean)
        .sort(),
    [run],
  )
  const filteredEntries = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return (
      run?.value.entries.filter(
        (entry) =>
          (!transactionType || entry.transactionType === transactionType) &&
          (!normalizedQuery ||
            [
              entry.itemCode,
              entry.itemName,
              entry.facilityCode,
              entry.transactionType,
              entry.reason,
              entry.performedBy,
              entry.transactionId,
              entry.transferId,
              entry.receiptId,
              entry.receiptReference,
              entry.reconciliationId,
            ].some((value) =>
              String(value ?? '')
                .toLowerCase()
                .includes(normalizedQuery),
            )),
      ) ?? []
    )
  }, [query, run, transactionType])
  const pageCount = Math.max(
    1,
    Math.ceil(filteredEntries.length / ENTRIES_PER_PAGE),
  )
  const visibleEntries = filteredEntries.slice(
    (page - 1) * ENTRIES_PER_PAGE,
    page * ENTRIES_PER_PAGE,
  )
  const stale = run !== null && run.refreshToken !== refreshToken

  useEffect(() => {
    setPage(1)
  }, [query, run, transactionType])

  function filterValidationError() {
    return from && to && from > to
      ? 'The activity report start date cannot be after its end date.'
      : null
  }

  async function loadReport() {
    const validationError = filterValidationError()
    if (validationError) {
      setError(validationError)
      return
    }
    setLoading(true)
    setError(null)
    try {
      const value = await getInventoryActivityReport(sessionId, filters)
      setRun({
        filters,
        loadedAt: new Date().toISOString(),
        refreshToken,
        value,
      })
    } catch (caught) {
      setError(caughtMessage(caught, 'Could not load inventory activity.'))
    } finally {
      setLoading(false)
    }
  }

  async function exportReport() {
    const validationError = filterValidationError()
    if (validationError) {
      setExportError(validationError)
      return
    }
    setExporting(true)
    setExportError(null)
    setExportEvidence(null)
    try {
      const blob = await downloadInventoryActivityCsv(sessionId, filters)
      const filename = 'avenchart-inventory-activity.csv'
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
      setExportEvidence({
        downloadedAt: new Date().toISOString(),
        filters,
        filename,
      })
      showToast('Inventory activity CSV downloaded.', 'success')
    } catch (caught) {
      setExportError(
        caughtMessage(caught, 'Could not export inventory activity.'),
      )
    } finally {
      setExporting(false)
    }
  }

  return (
    <section className="cl-card inventory-activity">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Activity report</h2>
          <p className="cl-card-subtitle">
            Run the protected date/facility report, inspect immutable stock
            movements, and export the same bounded server result as CSV.
          </p>
        </div>
        {run && (
          <span className="cl-badge cl-badge-muted">
            {run.value.totalEntries} matching
          </span>
        )}
      </div>

      <form
        className="inventory-activity-filters"
        onSubmit={(event) => {
          event.preventDefault()
          void loadReport()
        }}
      >
        <label className="cl-admin-field">
          <span>Activity from date</span>
          <input
            type="date"
            max={to || undefined}
            value={from}
            onChange={(event) => setFrom(event.target.value)}
          />
        </label>
        <label className="cl-admin-field">
          <span>Activity to date</span>
          <input
            type="date"
            min={from || undefined}
            value={to}
            onChange={(event) => setTo(event.target.value)}
          />
        </label>
        <label className="cl-admin-field">
          <span>Activity facility</span>
          <select
            value={facilityId}
            onChange={(event) => setFacilityId(event.target.value)}
          >
            <option value="">All facilities</option>
            {facilities.map((facility) => (
              <option key={facility.facilityId} value={facility.facilityId}>
                {facility.code} / {facility.name}
              </option>
            ))}
          </select>
        </label>
        <div className="inventory-activity-actions">
          <button className="cl-btn-secondary" type="submit" disabled={loading}>
            {loading ? 'Running…' : 'Run report'}
          </button>
          <button
            className="cl-btn-secondary"
            type="button"
            disabled={exporting}
            onClick={() => void exportReport()}
          >
            {exporting ? 'Exporting…' : 'CSV export'}
          </button>
        </div>
      </form>

      {error && (
        <div className="error-banner" role="alert">
          <span>{error}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void loadReport()}
          >
            Retry report
          </button>
        </div>
      )}
      {exportError && (
        <div className="error-banner" role="alert">
          <span>{exportError}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void exportReport()}
          >
            Retry CSV export
          </button>
        </div>
      )}
      {loading && !run && (
        <p className="cl-empty-text" aria-live="polite">
          Loading inventory activity…
        </p>
      )}
      {!run && !loading && !error && (
        <div className="empty-state">
          <p className="empty-state-text">
            Run the report to load protected inventory activity and output
            metadata.
          </p>
        </div>
      )}

      {run && (
        <>
          {stale && (
            <div className="hint-banner">
              Inventory changed after this result was loaded. Run the report
              again for an authoritative refresh.
            </div>
          )}

          <dl className="inventory-activity-facts">
            <div>
              <dt>Dataset</dt>
              <dd>
                <code>{run.value.datasetId}</code>
              </dd>
            </div>
            <div>
              <dt>Dataset version</dt>
              <dd>{run.value.datasetVersion}</dd>
            </div>
            <div>
              <dt>Server filters</dt>
              <dd>{formatFilterSummary(run.filters, facilities)}</dd>
            </div>
            <div>
              <dt>Result size</dt>
              <dd>
                {run.value.totalEntries} matching / {run.value.entries.length}{' '}
                returned
              </dd>
            </div>
            <div>
              <dt>Retrieved</dt>
              <dd>{new Date(run.loadedAt).toLocaleString()}</dd>
            </div>
          </dl>

          {run.value.totalEntries > run.value.entries.length && (
            <div className="hint-banner">
              The target contract returned the newest {run.value.entries.length}{' '}
              of {run.value.totalEntries} matching entries. Narrow the date or
              facility filters before relying on complete detail. The CSV uses
              the same {REPORT_ENTRY_LIMIT}-entry limit.
            </div>
          )}

          <div className="inventory-activity-view-filters">
            <label className="cl-admin-field">
              <span>Search returned activity</span>
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Item, reason, actor, reference, or stable ID"
              />
            </label>
            <label className="cl-admin-field">
              <span>Returned transaction type</span>
              <select
                value={transactionType}
                onChange={(event) => setTransactionType(event.target.value)}
              >
                <option value="">All returned types</option>
                {transactionTypes.map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </select>
            </label>
            <span className="cl-badge cl-badge-muted">
              {filteredEntries.length} of {run.value.entries.length} returned
            </span>
          </div>

          {visibleEntries.length === 0 ? (
            <div className="empty-state">
              <p className="empty-state-text">
                No returned activity matches the local view filters.
              </p>
            </div>
          ) : (
            <div
              className="cl-table-scroll"
              role="region"
              aria-label="Inventory activity results"
              tabIndex={0}
            >
              <table className="cl-table inventory-activity-table">
                <thead>
                  <tr>
                    <th>When</th>
                    <th>Item</th>
                    <th>Facility</th>
                    <th>Type</th>
                    <th>Delta</th>
                    <th>Reason / actor</th>
                    <th>Provenance</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleEntries.map((entry) => (
                    <tr key={entry.transactionId}>
                      <td>{new Date(entry.occurredAt).toLocaleString()}</td>
                      <td>
                        <strong>{entry.itemCode}</strong>
                        <span className="inventory-table-secondary">
                          {entry.itemName} / lot {entry.lotId}
                        </span>
                      </td>
                      <td>
                        {entry.facilityCode}
                        {entry.counterpartyFacilityCode && (
                          <span className="inventory-table-secondary">
                            Counterparty {entry.counterpartyFacilityCode}
                          </span>
                        )}
                      </td>
                      <td>{entry.transactionType}</td>
                      <td>
                        {entry.quantityDelta > 0 ? '+' : ''}
                        {entry.quantityDelta}
                      </td>
                      <td>
                        {entry.reason ?? 'No reason recorded'}
                        <span className="inventory-table-secondary">
                          {entry.performedBy}
                        </span>
                      </td>
                      <td>
                        <code>{entry.transactionId}</code>
                        {entry.transferId && (
                          <span className="inventory-table-secondary">
                            Transfer {entry.transferId}
                          </span>
                        )}
                        {entry.receiptId && (
                          <span className="inventory-table-secondary">
                            Receipt {entry.receiptId}
                            {entry.receiptReference
                              ? ` / ${entry.receiptReference}`
                              : ''}
                          </span>
                        )}
                        {entry.reconciliationId && (
                          <span className="inventory-table-secondary">
                            Reconciliation {entry.reconciliationId}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div
            className="inventory-lot-pagination"
            aria-label="Activity report pages"
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
        </>
      )}

      {exportEvidence && (
        <div className="inventory-activity-export" aria-live="polite">
          <strong>CSV downloaded</strong>
          <span>{exportEvidence.filename}</span>
          <span>
            {formatFilterSummary(exportEvidence.filters, facilities)} /{' '}
            {new Date(exportEvidence.downloadedAt).toLocaleString()}
          </span>
          <span>
            The CSV uses the protected server report and includes at most the
            newest {REPORT_ENTRY_LIMIT} matching entries.
          </span>
        </div>
      )}
    </section>
  )
}
