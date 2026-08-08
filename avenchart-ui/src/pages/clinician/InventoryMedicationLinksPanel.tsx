// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  getInventoryMedicationLinkHistory,
  getInventoryMedicationCatalog,
  unlinkInventoryMedicationLink,
  updateInventoryMedicationLink,
  type InventoryItem,
  type InventoryMedicationLinkHistoryResponse,
  type InventoryMedicationCatalogItem,
  type InventoryMedicationLink,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  items: InventoryItem[]
  onChanged: () => Promise<void>
  sessionId: string
}

type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; medications: InventoryMedicationCatalogItem[] }
  | { status: 'error'; message: string }

const ITEMS_PER_PAGE = 6

function caughtMessage(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

export default function InventoryMedicationLinksPanel({
  items,
  onChanged,
  sessionId,
}: Props) {
  const [catalog, setCatalog] = useState<CatalogState>({ status: 'loading' })
  const [selectedItemId, setSelectedItemId] = useState('')
  const [selectedRxNormCode, setSelectedRxNormCode] = useState('')
  const [itemQuery, setItemQuery] = useState('')
  const [medicationQuery, setMedicationQuery] = useState('')
  const [itemPage, setItemPage] = useState(1)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<InventoryMedicationLink | null>(null)
  const [history, setHistory] =
    useState<InventoryMedicationLinkHistoryResponse | null>(null)
  const [unlinkReason, setUnlinkReason] = useState('')

  const selectedItem = items.find(
    (item) => String(item.itemId) === selectedItemId,
  )
  const medications = useMemo(
    () => (catalog.status === 'ready' ? catalog.medications : []),
    [catalog],
  )
  const selectedMedication = medications.find(
    (medication) => medication.rxNormCode === selectedRxNormCode,
  )
  const linkedByCode = useMemo(
    () =>
      new Map(
        items.flatMap((item) =>
          item.medicationLink
            ? [[item.medicationLink.rxNormCode, item] as const]
            : [],
        ),
      ),
    [items],
  )
  const visibleMedications = useMemo(() => {
    const query = medicationQuery.trim().toLowerCase()
    return medications.filter(
      (medication) =>
        medication.rxNormCode === selectedRxNormCode ||
        !query ||
        [
          medication.rxNormCode,
          medication.drugName,
          medication.displayName,
          medication.form,
          medication.strength,
          medication.route,
        ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [medicationQuery, medications, selectedRxNormCode])
  const filteredItems = useMemo(() => {
    const query = itemQuery.trim().toLowerCase()
    return items.filter(
      (item) =>
        !query ||
        [
          item.itemCode,
          item.name,
          item.category,
          item.medicationLink?.rxNormCode,
          item.medicationLink?.displayName,
        ].some((value) =>
          String(value ?? '')
            .toLowerCase()
            .includes(query),
        ),
    )
  }, [itemQuery, items])
  const itemPageCount = Math.max(
    1,
    Math.ceil(filteredItems.length / ITEMS_PER_PAGE),
  )
  const visibleItems = filteredItems.slice(
    (itemPage - 1) * ITEMS_PER_PAGE,
    itemPage * ITEMS_PER_PAGE,
  )
  const linkedCount = items.filter((item) => item.medicationLink).length
  const conflictingItem = selectedRxNormCode
    ? linkedByCode.get(selectedRxNormCode)
    : undefined
  const hasConflict =
    conflictingItem !== undefined &&
    conflictingItem.itemId !== selectedItem?.itemId
  const unchanged =
    selectedItem?.medicationLink?.rxNormCode === selectedRxNormCode

  const loadCatalog = useCallback(
    async (signal?: AbortSignal) => {
      setCatalog({ status: 'loading' })
      try {
        setCatalog({
          status: 'ready',
          medications: await getInventoryMedicationCatalog(sessionId, signal),
        })
      } catch (caught) {
        if (signal?.aborted) return
        setCatalog({
          status: 'error',
          message: caughtMessage(
            caught,
            'Could not load the local medication catalog.',
          ),
        })
      }
    },
    [sessionId],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadCatalog(controller.signal)
    return () => controller.abort()
  }, [loadCatalog])

  useEffect(() => {
    setItemPage(1)
  }, [itemQuery, items])

  function chooseItem(itemId: string) {
    const item = items.find((candidate) => String(candidate.itemId) === itemId)
    setSelectedItemId(itemId)
    setSelectedRxNormCode(item?.medicationLink?.rxNormCode ?? '')
    setMedicationQuery('')
    setError(null)
    setResult(null)
    setHistory(null)
    setUnlinkReason('')
  }

  async function saveLink(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (!selectedItem || !selectedMedication) {
      setError('Select an inventory item and a known local RXCUI medication.')
      return
    }
    if (hasConflict) {
      setError(
        `RXCUI ${selectedMedication.rxNormCode} is already linked to ${conflictingItem?.itemCode}.`,
      )
      return
    }
    if (unchanged) {
      setError('The selected medication link is already current.')
      return
    }

    setBusy(true)
    try {
      const link = await updateInventoryMedicationLink(
        sessionId,
        selectedItem.itemId,
        selectedMedication.rxNormCode,
      )
      setResult(link)
      setHistory(await getInventoryMedicationLinkHistory(sessionId, selectedItem.itemId))
      showToast('Inventory medication link saved.', 'success')
      await onChanged()
    } catch (caught) {
      setError(
        caughtMessage(caught, 'Could not save the inventory medication link.'),
      )
    } finally {
      setBusy(false)
    }
  }

  async function reviewHistory() {
    if (!selectedItem) return
    setError(null)
    setBusy(true)
    try {
      setHistory(await getInventoryMedicationLinkHistory(sessionId, selectedItem.itemId))
    } catch (caught) {
      setError(caughtMessage(caught, 'Could not load medication link history.'))
    } finally {
      setBusy(false)
    }
  }

  async function unlinkMedication() {
    if (!selectedItem?.medicationLink) return
    const reason = unlinkReason.trim()
    if (!reason) {
      setError('Provide a reason before unlinking this medication mapping.')
      return
    }
    setError(null)
    setBusy(true)
    try {
      const unlinkHistory = await unlinkInventoryMedicationLink(sessionId, selectedItem.itemId, reason)
      setHistory(unlinkHistory)
      setSelectedRxNormCode('')
      setUnlinkReason('')
      setResult(null)
      showToast('Inventory medication link removed with audit evidence.', 'success')
      await onChanged()
    } catch (caught) {
      setError(caughtMessage(caught, 'Could not unlink the medication mapping.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="cl-card inventory-medication-links">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Medication inventory links</h2>
          <p className="cl-card-subtitle">
            Map an active stocked item to one known local RXCUI so an active
            prescription can use the single-lot dispensing workflow.
          </p>
        </div>
        <span className="cl-badge cl-badge-muted">
          {linkedCount} linked / {items.length - linkedCount} unmapped
        </span>
      </div>

      <div className="hint-banner">
        This is a local mapping catalog, not a production drug-knowledge source,
        pharmacy network, or eRx/EPCS workflow. The target records immutable
        link audit rows. Active catalog entries can be mapped, and existing
        links can be reviewed or removed only with a recorded reason.
      </div>

      {catalog.status === 'loading' && (
        <p className="cl-empty-text" aria-live="polite">
          Loading the local medication catalog…
        </p>
      )}
      {catalog.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{catalog.message}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void loadCatalog()}
          >
            Retry medication catalog
          </button>
        </div>
      )}

      {catalog.status === 'ready' && (
        <form className="inventory-medication-link-form" onSubmit={saveLink}>
          <label className="cl-admin-field">
            <span>Link inventory item</span>
            <select
              value={selectedItemId}
              onChange={(event) => chooseItem(event.target.value)}
              required
            >
              <option value="">Select an active item</option>
              {items.map((item) => (
                <option key={item.itemId} value={item.itemId}>
                  {item.itemCode} / {item.name} /{' '}
                  {item.medicationLink
                    ? `RXCUI ${item.medicationLink.rxNormCode}`
                    : 'unmapped'}
                </option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Search local medications</span>
            <input
              type="search"
              value={medicationQuery}
              onChange={(event) => setMedicationQuery(event.target.value)}
              placeholder="RXCUI, drug, form, strength, or route"
            />
          </label>
          <label className="cl-admin-field inventory-medication-target">
            <span>Local RXCUI medication</span>
            <select
              value={selectedRxNormCode}
              onChange={(event) => {
                setSelectedRxNormCode(event.target.value)
                setError(null)
                setResult(null)
              }}
              required
            >
              <option value="">Select a catalog medication</option>
              {visibleMedications.map((medication) => {
                const linkedItem = linkedByCode.get(medication.rxNormCode)
                const linkedElsewhere =
                  linkedItem !== undefined &&
                  linkedItem.itemId !== selectedItem?.itemId
                return (
                  <option
                    key={medication.rxNormCode}
                    value={medication.rxNormCode}
                    disabled={linkedElsewhere}
                  >
                    {medication.rxNormCode} / {medication.displayName}
                    {linkedElsewhere
                      ? ` / linked to ${linkedItem.itemCode}`
                      : ''}
                  </option>
                )
              })}
            </select>
            <small>
              Showing {visibleMedications.length} of {medications.length} local
              catalog entries. An RXCUI can belong to only one inventory item.
            </small>
          </label>

          {selectedItem && (
            <div className="inventory-medication-current">
              <strong>
                {selectedItem.itemCode} / {selectedItem.name}
              </strong>
              {selectedItem.medicationLink ? (
                <>
                  <span>
                    Current: RXCUI {selectedItem.medicationLink.rxNormCode} /{' '}
                    {selectedItem.medicationLink.displayName}
                  </span>
                  <span>
                    Linked by {selectedItem.medicationLink.linkedBy} /{' '}
                    {new Date(
                      selectedItem.medicationLink.linkedAt,
                    ).toLocaleString()}
                  </span>
                </>
              ) : (
                <span>
                  Unmapped: this item is not eligible for prescription-linked
                  inventory dispensing.
                </span>
              )}
            </div>
          )}

          {selectedItem?.medicationLink && (
            <div className="inventory-medication-current">
              <label className="cl-admin-field">
                <span>Reason to unlink this mapping</span>
                <input
                  value={unlinkReason}
                  maxLength={500}
                  onChange={(event) => setUnlinkReason(event.target.value)}
                  placeholder="Why is this inventory-to-RXCUI mapping no longer valid?"
                />
              </label>
              <button className="cl-btn-secondary" type="button" disabled={busy || !unlinkReason.trim()} onClick={() => void unlinkMedication()}>
                {busy ? 'Working…' : 'Unlink with reason'}
              </button>
            </div>
          )}

          {selectedMedication && (
            <dl className="inventory-medication-facts">
              <div>
                <dt>RXCUI</dt>
                <dd>{selectedMedication.rxNormCode}</dd>
              </div>
              <div>
                <dt>Medication</dt>
                <dd>{selectedMedication.drugName}</dd>
              </div>
              <div>
                <dt>Form</dt>
                <dd>{selectedMedication.form}</dd>
              </div>
              <div>
                <dt>Strength</dt>
                <dd>{selectedMedication.strength}</dd>
              </div>
              <div>
                <dt>Route</dt>
                <dd>{selectedMedication.route}</dd>
              </div>
            </dl>
          )}

          {hasConflict && (
            <div className="hint-banner">
              RXCUI {selectedRxNormCode} is already linked to{' '}
              {conflictingItem?.itemCode} / {conflictingItem?.name}. Choose a
              different catalog entry.
            </div>
          )}
          {error && (
            <div className="error-banner" role="alert">
              {error}
            </div>
          )}
          <button
            className="cl-btn-primary"
            type="submit"
            disabled={
              busy ||
              !selectedItem ||
              !selectedMedication ||
              hasConflict ||
              unchanged
            }
          >
            {busy ? 'Saving…' : 'Save medication link'}
          </button>
          {selectedItem && (
            <button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void reviewHistory()}>
              Review link history
            </button>
          )}
        </form>
      )}

      {history && (
        <div className="inventory-medication-result" aria-live="polite">
          <strong>Medication link history</strong>
          {history.events.length === 0 ? (
            <span>No medication-link changes have been recorded for this item.</span>
          ) : (
            history.events.map((entry) => (
              <span key={entry.auditId}>
                {entry.action} / {entry.priorRxNormCode ?? 'none'} → {entry.newRxNormCode ?? 'none'} / {entry.changedBy} / {new Date(entry.changedAt).toLocaleString()}{entry.reason ? ` / ${entry.reason}` : ''}
              </span>
            ))
          )}
        </div>
      )}

      {result && (
        <div className="inventory-medication-result" aria-live="polite">
          <strong>Medication link saved</strong>
          <span>
            Inventory item {result.itemId} / RXCUI {result.rxNormCode} /{' '}
            {result.displayName}
          </span>
          <span>
            Linked by {result.linkedBy} /{' '}
            {new Date(result.linkedAt).toLocaleString()}
          </span>
        </div>
      )}

      <div className="inventory-medication-list-heading">
        <label className="cl-admin-field">
          <span>Search inventory mappings</span>
          <input
            type="search"
            value={itemQuery}
            onChange={(event) => setItemQuery(event.target.value)}
            placeholder="Item, category, RXCUI, or medication"
          />
        </label>
        <span className="cl-badge cl-badge-muted">
          {filteredItems.length} of {items.length} items
        </span>
      </div>

      {visibleItems.length === 0 ? (
        <div className="empty-state">
          <p className="empty-state-text">
            No inventory items match this mapping search.
          </p>
        </div>
      ) : (
        <div
          className="cl-table-scroll"
          role="region"
          aria-label="Inventory medication mappings"
          tabIndex={0}
        >
          <table className="cl-table inventory-medication-table">
            <thead>
              <tr>
                <th>Inventory item</th>
                <th>Mapping status</th>
                <th>RXCUI</th>
                <th>Medication</th>
                <th>Current link evidence</th>
              </tr>
            </thead>
            <tbody>
              {visibleItems.map((item) => (
                <tr key={item.itemId}>
                  <td>
                    <strong>{item.itemCode}</strong>
                    <span className="inventory-table-secondary">
                      {item.name} / {item.category}
                    </span>
                  </td>
                  <td>
                    <span
                      className={
                        item.medicationLink
                          ? 'cl-badge cl-badge-green'
                          : 'cl-badge cl-badge-muted'
                      }
                    >
                      {item.medicationLink ? 'Linked' : 'Unmapped'}
                    </span>
                  </td>
                  <td>{item.medicationLink?.rxNormCode ?? '—'}</td>
                  <td>
                    {item.medicationLink?.displayName ??
                      'Not eligible for prescription dispense'}
                  </td>
                  <td>
                    {item.medicationLink ? (
                      <>
                        {item.medicationLink.linkedBy}
                        <span className="inventory-table-secondary">
                          {new Date(
                            item.medicationLink.linkedAt,
                          ).toLocaleString()}
                        </span>
                      </>
                    ) : (
                      'No current link'
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
        aria-label="Medication mapping pages"
      >
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={itemPage === 1}
          onClick={() => setItemPage((current) => current - 1)}
        >
          Previous
        </button>
        <span aria-live="polite">
          Page {itemPage} of {itemPageCount}
        </span>
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={itemPage === itemPageCount}
          onClick={() => setItemPage((current) => current + 1)}
        >
          Next
        </button>
      </div>
    </section>
  )
}
