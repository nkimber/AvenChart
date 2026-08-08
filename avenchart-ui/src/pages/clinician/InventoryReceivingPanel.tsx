// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  createInventoryPurchaseReceipt,
  getInventoryPurchaseRequisitions,
  getInventoryVendors,
  type InventoryItem,
  type InventoryPurchaseReceipt,
  type InventoryPurchaseRequisition,
  type InventoryVendor,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  facilities: { facilityId: number; code: string; name: string }[]
  items: InventoryItem[]
  onChanged: () => Promise<void>
  sessionId: string
}

type ReceivingOptionsState =
  | { status: 'loading' }
  | {
      status: 'ready'
      vendors: InventoryVendor[]
      requisitions: InventoryPurchaseRequisition[]
    }
  | { status: 'error'; message: string }

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

export default function InventoryReceivingPanel({
  facilities,
  items,
  onChanged,
  sessionId,
}: Props) {
  const [state, setState] = useState<ReceivingOptionsState>({
    status: 'loading',
  })
  const [mode, setMode] = useState<'direct' | 'requisition'>('direct')
  const [requisitionId, setRequisitionId] = useState('')
  const [requisitionLineId, setRequisitionLineId] = useState('')
  const [vendorId, setVendorId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [itemId, setItemId] = useState('')
  const [lotNumber, setLotNumber] = useState('')
  const [expirationDate, setExpirationDate] = useState('')
  const [quantity, setQuantity] = useState('')
  const [unitCost, setUnitCost] = useState('')
  const [referenceNumber, setReferenceNumber] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [receipt, setReceipt] = useState<InventoryPurchaseReceipt | null>(null)

  const loadOptions = useCallback(async () => {
    setState({ status: 'loading' })
    try {
      const [vendorResponse, requisitions] = await Promise.all([
        getInventoryVendors(sessionId),
        getInventoryPurchaseRequisitions(sessionId),
      ])
      setState({
        status: 'ready',
        vendors: vendorResponse.vendors,
        requisitions,
      })
    } catch (caught) {
      setState({
        status: 'error',
        message:
          caught instanceof Error
            ? caught.message
            : 'Could not load receiving options.',
      })
    }
  }, [sessionId])

  useEffect(() => {
    void loadOptions()
  }, [loadOptions])

  const approvedRequisitions = useMemo(
    () =>
      state.status === 'ready'
        ? state.requisitions.filter(
            (requisition) =>
              requisition.status === 'approved' &&
              requisition.lines.some((line) => line.outstandingQuantity > 0),
          )
        : [],
    [state],
  )
  const selectedRequisition = approvedRequisitions.find(
    (requisition) => requisition.requisitionId === requisitionId,
  )
  const selectedLine = selectedRequisition?.lines.find(
    (line) => line.requisitionLineId === requisitionLineId,
  )
  const selectedItem = items.find((item) => String(item.itemId) === itemId)
  const receiptItem = items.find(
    (item) => item.itemCode === receipt?.transaction.itemCode,
  )

  function chooseRequisition(nextId: string) {
    setRequisitionId(nextId)
    setError(null)
    const requisition = approvedRequisitions.find(
      (candidate) => candidate.requisitionId === nextId,
    )
    if (!requisition) {
      setRequisitionLineId('')
      setVendorId('')
      setFacilityId('')
      setItemId('')
      return
    }
    const firstOutstanding = requisition.lines.find(
      (line) => line.outstandingQuantity > 0,
    )
    setVendorId(requisition.vendorId ?? '')
    setFacilityId(String(requisition.facilityId))
    setRequisitionLineId(firstOutstanding?.requisitionLineId ?? '')
    setItemId(firstOutstanding ? String(firstOutstanding.itemId) : '')
    setQuantity(
      firstOutstanding ? String(firstOutstanding.outstandingQuantity) : '',
    )
  }

  function chooseRequisitionLine(lineId: string) {
    setRequisitionLineId(lineId)
    const line = selectedRequisition?.lines.find(
      (candidate) => candidate.requisitionLineId === lineId,
    )
    setItemId(line ? String(line.itemId) : '')
    setQuantity(line ? String(line.outstandingQuantity) : '')
    setError(null)
  }

  function switchMode(nextMode: 'direct' | 'requisition') {
    setMode(nextMode)
    setRequisitionId('')
    setRequisitionLineId('')
    setVendorId('')
    setFacilityId('')
    setItemId('')
    setQuantity('')
    setError(null)
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const parsedQuantity = Number(quantity)
    const parsedCost = Number(unitCost)
    if (!vendorId || !facilityId || !itemId) {
      setError('Vendor, facility, and inventory item are required.')
      return
    }
    if (
      !lotNumber.trim() ||
      !notes.trim() ||
      !Number.isFinite(parsedQuantity) ||
      parsedQuantity <= 0 ||
      !Number.isFinite(parsedCost) ||
      parsedCost < 0
    ) {
      setError(
        'Lot number, positive quantity, non-negative unit cost, and receipt notes are required.',
      )
      return
    }
    if (mode === 'requisition' && (!selectedRequisition || !selectedLine)) {
      setError('Select an approved requisition and outstanding line.')
      return
    }
    if (
      mode === 'requisition' &&
      selectedLine &&
      parsedQuantity > selectedLine.outstandingQuantity
    ) {
      setError(
        `Quantity cannot exceed the ${selectedLine.outstandingQuantity} ${selectedLine.unit} outstanding on the selected line.`,
      )
      return
    }

    setError(null)
    setSaving(true)
    try {
      const created = await createInventoryPurchaseReceipt(sessionId, {
        vendorId,
        facilityId: Number(facilityId),
        itemId: Number(itemId),
        lotNumber: lotNumber.trim(),
        expirationDate: expirationDate || null,
        quantity: parsedQuantity,
        unitCost: parsedCost,
        referenceNumber: referenceNumber.trim() || null,
        notes: notes.trim(),
        requisitionId:
          mode === 'requisition' ? selectedRequisition?.requisitionId : null,
      })
      setReceipt(created)
      setLotNumber('')
      setExpirationDate('')
      setQuantity('')
      setUnitCost('')
      setReferenceNumber('')
      setNotes('')
      setRequisitionId('')
      setRequisitionLineId('')
      await Promise.all([onChanged(), loadOptions()])
      showToast('Inventory receipt recorded.', 'success')
    } catch (caught) {
      const message =
        caught instanceof Error
          ? caught.message
          : 'Could not record the inventory receipt.'
      setError(message)
      showToast(message, 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="cl-card" aria-labelledby="inventory-receiving-title">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title" id="inventory-receiving-title">
            Receive inventory
          </h2>
          <p className="cl-card-subtitle">
            Record a direct vendor receipt or reconcile an approved requisition
            line. The target rejects duplicate vendor references and
            over-receipt before stock changes.
          </p>
        </div>
      </div>

      {state.status === 'loading' && (
        <div className="skeleton-list" aria-live="polite">
          <span className="sr-only">Loading receiving options</span>
          <div className="skeleton-row" />
        </div>
      )}
      {state.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{state.message}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void loadOptions()}
          >
            Retry
          </button>
        </div>
      )}
      {state.status === 'ready' && (
        <form className="inventory-receiving-form" onSubmit={submit}>
          <fieldset className="inventory-receiving-mode">
            <legend>Receipt source</legend>
            <label>
              <input
                type="radio"
                name="inventory-receipt-source"
                checked={mode === 'direct'}
                onChange={() => switchMode('direct')}
              />{' '}
              Direct vendor receipt
            </label>
            <label>
              <input
                type="radio"
                name="inventory-receipt-source"
                checked={mode === 'requisition'}
                onChange={() => switchMode('requisition')}
              />{' '}
              Reconcile approved requisition
            </label>
          </fieldset>

          {mode === 'requisition' && (
            <div className="inventory-receiving-grid">
              <label className="cl-admin-field">
                <span>Approved requisition request</span>
                <select
                  value={requisitionId}
                  onChange={(event) => chooseRequisition(event.target.value)}
                  required
                >
                  <option value="">Select a requisition</option>
                  {approvedRequisitions.map((requisition) => (
                    <option
                      key={requisition.requisitionId}
                      value={requisition.requisitionId}
                    >
                      {requisition.facilityCode} /{' '}
                      {requisition.vendorName ?? 'No vendor'} /{' '}
                      {requisition.requisitionId}
                    </option>
                  ))}
                </select>
                {approvedRequisitions.length === 0 && (
                  <small>
                    No approved requisitions have outstanding quantities.
                  </small>
                )}
              </label>
              <label className="cl-admin-field">
                <span>Outstanding line</span>
                <select
                  value={requisitionLineId}
                  onChange={(event) =>
                    chooseRequisitionLine(event.target.value)
                  }
                  disabled={!selectedRequisition}
                  required
                >
                  <option value="">Select a line</option>
                  {selectedRequisition?.lines
                    .filter((line) => line.outstandingQuantity > 0)
                    .map((line) => (
                      <option
                        key={line.requisitionLineId}
                        value={line.requisitionLineId}
                      >
                        {line.itemCode} / {line.outstandingQuantity} {line.unit}{' '}
                        outstanding
                      </option>
                    ))}
                </select>
              </label>
            </div>
          )}

          <div className="inventory-receiving-grid">
            <label className="cl-admin-field">
              <span>Vendor</span>
              <select
                aria-label="Vendor"
                value={vendorId}
                onChange={(event) => setVendorId(event.target.value)}
                disabled={mode === 'requisition'}
                required
              >
                <option value="">Select a vendor</option>
                {state.vendors.map((vendor) => (
                  <option key={vendor.vendorId} value={vendor.vendorId}>
                    {vendor.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Receiving facility</span>
              <select
                value={facilityId}
                onChange={(event) => setFacilityId(event.target.value)}
                disabled={mode === 'requisition'}
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
              <span>Inventory item</span>
              <select
                value={itemId}
                onChange={(event) => setItemId(event.target.value)}
                disabled={mode === 'requisition'}
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
          </div>

          <div className="inventory-receiving-grid">
            <label className="cl-admin-field">
              <span>Lot number</span>
              <input
                value={lotNumber}
                maxLength={80}
                onChange={(event) => setLotNumber(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field">
              <span>Expiration date (optional)</span>
              <input
                type="date"
                value={expirationDate}
                onChange={(event) => setExpirationDate(event.target.value)}
              />
            </label>
            <label className="cl-admin-field">
              <span>Quantity received</span>
              <input
                type="number"
                min="0.01"
                step="0.01"
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
                required
              />
              {selectedLine && (
                <small>
                  Maximum {selectedLine.outstandingQuantity} {selectedLine.unit}{' '}
                  outstanding
                </small>
              )}
            </label>
            <label className="cl-admin-field">
              <span>
                Unit cost (USD
                {selectedItem ? ` per ${selectedItem.unit}` : ''})
              </span>
              <input
                type="number"
                min="0"
                step="0.01"
                value={unitCost}
                onChange={(event) => setUnitCost(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field">
              <span>Vendor reference (optional)</span>
              <input
                value={referenceNumber}
                maxLength={120}
                onChange={(event) => setReferenceNumber(event.target.value)}
              />
            </label>
            <label className="cl-admin-field">
              <span>Receipt notes</span>
              <input
                value={notes}
                maxLength={500}
                onChange={(event) => setNotes(event.target.value)}
                required
              />
            </label>
          </div>

          {selectedLine && quantity && Number(quantity) > 0 && (
            <div className="hint-banner" role="status">
              Receive {quantity} {selectedLine.unit} against{' '}
              {selectedLine.outstandingQuantity} {selectedLine.unit}{' '}
              outstanding. Resulting outstanding:{' '}
              {Math.max(0, selectedLine.outstandingQuantity - Number(quantity))}{' '}
              {selectedLine.unit}.
            </div>
          )}
          {error && (
            <div className="error-banner" role="alert">
              {error}
            </div>
          )}
          <button className="cl-btn-primary" type="submit" disabled={saving}>
            {saving ? 'Recording receipt...' : 'Record receipt'}
          </button>
        </form>
      )}

      {receipt && (
        <div className="inventory-receipt-result" role="status">
          <strong>Receipt {receipt.receiptId} recorded</strong>
          <span>
            {receipt.receivedAt} by {receipt.receivedBy} / {receipt.vendor.name}{' '}
            / {receipt.facilityCode}
          </span>
          <span>
            Lot {receipt.lot.lotNumber} ({receipt.lot.lotId}) /{' '}
            {receipt.lot.quantityOnHand} {receiptItem?.unit ?? 'stocking units'}{' '}
            on hand / {formatCurrency(receipt.lot.unitCost)} per{' '}
            {receiptItem?.unit ?? 'stocking unit'}
          </span>
          <span>
            Ledger transaction {receipt.transaction.transactionId}; item total{' '}
            {receipt.itemQuantityOnHand} {receiptItem?.unit ?? 'stocking units'}
          </span>
          {receipt.requisitionReconciliation && (
            <span>
              Reconciled {receipt.requisitionReconciliation.receivedQuantity} to
              requisition {receipt.requisitionReconciliation.requisitionId};
              evidence {receipt.requisitionReconciliation.reconciliationId}
            </span>
          )}
        </div>
      )}
    </section>
  )
}
