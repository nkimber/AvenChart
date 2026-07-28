import { useMemo, useState } from 'react'
import {
  createInventoryCountReconciliation,
  createInventoryExpiryDisposition,
  createInventoryLotDestruction,
  createInventoryTransaction,
  createInventoryTransfer,
  type InventoryCountReconciliation,
  type InventoryExpiryDisposition,
  type InventoryItem,
  type InventoryLot,
  type InventoryLotDestruction,
  type InventoryMutationResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  facilities: { facilityId: number; code: string; name: string }[]
  items: InventoryItem[]
  onChanged: () => Promise<void>
  sessionId: string
}

type LotWithItem = {
  item: InventoryItem
  lot: InventoryLot
}

type Workflow = 'movement' | 'count' | 'expiry' | 'destruction'

type Result =
  | { kind: 'movement'; unit: string; value: InventoryMutationResponse }
  | { kind: 'count'; unit: string; value: InventoryCountReconciliation }
  | { kind: 'expiry'; unit: string; value: InventoryExpiryDisposition }
  | { kind: 'destruction'; unit: string; value: InventoryLotDestruction }

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

function lotLabel({ item, lot }: LotWithItem) {
  return `${item.itemCode} / ${lot.facilityCode} / ${lot.lotNumber} / ${lot.quantityOnHand} ${item.unit}`
}

function caughtMessage(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

export default function InventoryStockActionsPanel({
  facilities,
  items,
  onChanged,
  sessionId,
}: Props) {
  const [workflow, setWorkflow] = useState<Workflow>('movement')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<Result | null>(null)

  const [movementLotId, setMovementLotId] = useState('')
  const [movementKind, setMovementKind] = useState<'consumption' | 'transfer'>(
    'consumption',
  )
  const [destinationFacilityId, setDestinationFacilityId] = useState('')
  const [movementQuantity, setMovementQuantity] = useState('1')
  const [movementReason, setMovementReason] = useState('')

  const [countLotId, setCountLotId] = useState('')
  const [countedQuantity, setCountedQuantity] = useState('')
  const [countNotes, setCountNotes] = useState('')

  const [expiryLotId, setExpiryLotId] = useState('')
  const [expiryDisposition, setExpiryDisposition] = useState<
    'quarantine' | 'return' | 'destroy'
  >('quarantine')
  const [expiryNotes, setExpiryNotes] = useState('')
  const [expiryMethod, setExpiryMethod] = useState('')
  const [expiryWitness, setExpiryWitness] = useState('')

  const [destructionLotId, setDestructionLotId] = useState('')
  const [destructionDate, setDestructionDate] = useState(
    new Date().toISOString().slice(0, 10),
  )
  const [destructionMethod, setDestructionMethod] = useState('')
  const [destructionWitness, setDestructionWitness] = useState('')
  const [destructionNotes, setDestructionNotes] = useState('')
  const [destructionConfirmed, setDestructionConfirmed] = useState(false)

  const lots = useMemo(
    () => items.flatMap((item) => item.lots.map((lot) => ({ item, lot }))),
    [items],
  )
  const activeLots = lots.filter(
    ({ lot }) => lot.status === 'active' && lot.quantityOnHand > 0,
  )
  const countableLots = lots.filter(({ lot }) => lot.status === 'active')
  const expiredLots = lots.filter(
    ({ lot }) =>
      lot.expiryStatus === 'expired' &&
      (lot.status === 'active' || lot.status === 'quarantined'),
  )
  const movementLot = activeLots.find(
    ({ lot }) => String(lot.lotId) === movementLotId,
  )
  const countLot = countableLots.find(
    ({ lot }) => String(lot.lotId) === countLotId,
  )
  const expiryLot = expiredLots.find(
    ({ lot }) => String(lot.lotId) === expiryLotId,
  )
  const destructionLot = activeLots.find(
    ({ lot }) => String(lot.lotId) === destructionLotId,
  )
  const movementAmount = Number(movementQuantity)
  const countedAmount = Number(countedQuantity)
  const countVariance =
    countLot && countedQuantity !== ''
      ? countedAmount - countLot.lot.quantityOnHand
      : null

  function chooseWorkflow(next: Workflow) {
    setWorkflow(next)
    setError(null)
    setResult(null)
  }

  async function finish(nextResult: Result, message: string) {
    setResult(nextResult)
    showToast(message, 'success')
    await onChanged()
  }

  async function submitMovement(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (!movementLot) {
      setError('Select an active lot with quantity on hand.')
      return
    }
    if (
      !Number.isFinite(movementAmount) ||
      movementAmount <= 0 ||
      movementAmount > movementLot.lot.quantityOnHand
    ) {
      setError(
        `Quantity must be greater than zero and no more than ${movementLot.lot.quantityOnHand} ${movementLot.item.unit}.`,
      )
      return
    }
    if (!movementReason.trim()) {
      setError('A reason is required for this stock movement.')
      return
    }
    if (
      movementKind === 'transfer' &&
      (!destinationFacilityId ||
        Number(destinationFacilityId) <= 0 ||
        facilities.find(
          (facility) => facility.facilityId === Number(destinationFacilityId),
        )?.code === movementLot.lot.facilityCode)
    ) {
      setError('Select a different destination facility.')
      return
    }

    setBusy(true)
    try {
      const value =
        movementKind === 'transfer'
          ? await createInventoryTransfer(sessionId, {
              sourceLotId: movementLot.lot.lotId,
              destinationFacilityId: Number(destinationFacilityId),
              quantity: movementAmount,
              reason: movementReason.trim(),
            })
          : await createInventoryTransaction(sessionId, {
              lotId: movementLot.lot.lotId,
              transactionType: 'consumption',
              quantity: movementAmount,
              reason: movementReason.trim(),
            })
      await finish(
        { kind: 'movement', unit: movementLot.item.unit, value },
        movementKind === 'transfer'
          ? 'Inventory transfer recorded.'
          : 'Inventory consumption recorded.',
      )
      setMovementQuantity('1')
      setMovementReason('')
    } catch (caught) {
      setError(caughtMessage(caught, 'Could not record the stock movement.'))
    } finally {
      setBusy(false)
    }
  }

  async function submitCount(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (!countLot) {
      setError('Select an active lot to count.')
      return
    }
    if (
      countedQuantity === '' ||
      !Number.isFinite(countedAmount) ||
      countedAmount < 0
    ) {
      setError('Counted quantity must be zero or greater.')
      return
    }
    if (!countNotes.trim()) {
      setError('Count notes are required.')
      return
    }

    setBusy(true)
    try {
      const value = await createInventoryCountReconciliation(sessionId, {
        lotId: countLot.lot.lotId,
        countedQuantity: countedAmount,
        notes: countNotes.trim(),
      })
      await finish(
        { kind: 'count', unit: countLot.item.unit, value },
        'Inventory count reconciled.',
      )
      setCountedQuantity('')
      setCountNotes('')
    } catch (caught) {
      setError(
        caughtMessage(caught, 'Could not reconcile the inventory count.'),
      )
    } finally {
      setBusy(false)
    }
  }

  async function submitExpiry(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (!expiryLot) {
      setError('Select an expired active or quarantined lot.')
      return
    }
    if (
      expiryLot.lot.status === 'quarantined' &&
      expiryDisposition === 'quarantine'
    ) {
      setError('A quarantined lot must be returned or destroyed.')
      return
    }
    if (!expiryNotes.trim()) {
      setError('Disposition notes are required.')
      return
    }
    if (
      expiryDisposition === 'destroy' &&
      (!expiryMethod.trim() || !expiryWitness.trim())
    ) {
      setError('Destruction method and witness are required.')
      return
    }

    setBusy(true)
    try {
      const value = await createInventoryExpiryDisposition(
        sessionId,
        expiryLot.lot.lotId,
        {
          disposition: expiryDisposition,
          notes: expiryNotes.trim(),
          method: expiryDisposition === 'destroy' ? expiryMethod.trim() : null,
          witness:
            expiryDisposition === 'destroy' ? expiryWitness.trim() : null,
        },
      )
      await finish(
        { kind: 'expiry', unit: expiryLot.item.unit, value },
        'Expired-lot disposition recorded.',
      )
      setExpiryNotes('')
      setExpiryMethod('')
      setExpiryWitness('')
    } catch (caught) {
      setError(
        caughtMessage(caught, 'Could not record the expiry disposition.'),
      )
    } finally {
      setBusy(false)
    }
  }

  async function submitDestruction(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (!destructionLot) {
      setError('Select an active lot with quantity on hand.')
      return
    }
    if (
      !destructionDate ||
      destructionDate > new Date().toISOString().slice(0, 10)
    ) {
      setError('Destruction date is required and cannot be in the future.')
      return
    }
    if (
      !destructionMethod.trim() ||
      !destructionWitness.trim() ||
      !destructionNotes.trim()
    ) {
      setError('Destruction method, witness, and notes are required.')
      return
    }
    if (!destructionConfirmed) {
      setError('Confirm the full-lot destruction before recording it.')
      return
    }

    setBusy(true)
    try {
      const value = await createInventoryLotDestruction(
        sessionId,
        destructionLot.lot.lotId,
        {
          destructionDate,
          method: destructionMethod.trim(),
          witness: destructionWitness.trim(),
          notes: destructionNotes.trim(),
        },
      )
      await finish(
        { kind: 'destruction', unit: destructionLot.item.unit, value },
        'Lot destruction recorded.',
      )
      setDestructionMethod('')
      setDestructionWitness('')
      setDestructionNotes('')
      setDestructionConfirmed(false)
    } catch (caught) {
      setError(caughtMessage(caught, 'Could not record the lot destruction.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="cl-card inventory-stock-actions">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Authoritative stock actions</h2>
          <p className="cl-card-subtitle">
            Use the named workflow so stock and its immutable evidence change
            together. These controls apply to general inventory; controlled
            custody follows its separate governed workflow.
          </p>
        </div>
      </div>

      <label className="cl-admin-field inventory-workflow-picker">
        <span>Stock workflow</span>
        <select
          value={workflow}
          onChange={(event) => chooseWorkflow(event.target.value as Workflow)}
        >
          <option value="movement">Consume or transfer stock</option>
          <option value="count">Reconcile a physical count</option>
          <option value="expiry">Disposition an expired lot</option>
          <option value="destruction">Witness full-lot destruction</option>
        </select>
      </label>

      {workflow === 'movement' && (
        <form className="inventory-action-form" onSubmit={submitMovement}>
          <div className="inventory-action-grid">
            <label className="cl-admin-field inventory-action-lot">
              <span>Active inventory lot</span>
              <select
                value={movementLotId}
                onChange={(event) => setMovementLotId(event.target.value)}
                required
              >
                <option value="">Select a lot</option>
                {activeLots.map((entry) => (
                  <option key={entry.lot.lotId} value={entry.lot.lotId}>
                    {lotLabel(entry)}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Movement</span>
              <select
                value={movementKind}
                onChange={(event) =>
                  setMovementKind(
                    event.target.value as 'consumption' | 'transfer',
                  )
                }
              >
                <option value="consumption">Consume from stock</option>
                <option value="transfer">Transfer between facilities</option>
              </select>
            </label>
            {movementKind === 'transfer' && (
              <label className="cl-admin-field">
                <span>Destination facility</span>
                <select
                  value={destinationFacilityId}
                  onChange={(event) =>
                    setDestinationFacilityId(event.target.value)
                  }
                  required
                >
                  <option value="">Select a destination</option>
                  {facilities
                    .filter(
                      (facility) =>
                        facility.code !== movementLot?.lot.facilityCode,
                    )
                    .map((facility) => (
                      <option
                        key={facility.facilityId}
                        value={facility.facilityId}
                      >
                        {facility.code} / {facility.name}
                      </option>
                    ))}
                </select>
              </label>
            )}
            <label className="cl-admin-field">
              <span>Quantity</span>
              <input
                type="number"
                min="0.01"
                max={movementLot?.lot.quantityOnHand}
                step="0.01"
                value={movementQuantity}
                onChange={(event) => setMovementQuantity(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field inventory-action-notes">
              <span>Reason</span>
              <textarea
                maxLength={500}
                value={movementReason}
                onChange={(event) => setMovementReason(event.target.value)}
                required
              />
            </label>
          </div>
          {movementLot &&
            Number.isFinite(movementAmount) &&
            movementAmount > 0 && (
              <p className="inventory-action-preview">
                Preview: {movementLot.lot.quantityOnHand}{' '}
                {movementLot.item.unit} at {movementLot.lot.facilityCode} →{' '}
                {Math.max(0, movementLot.lot.quantityOnHand - movementAmount)}{' '}
                {movementLot.item.unit}.
              </p>
            )}
          <button className="cl-btn-primary" type="submit" disabled={busy}>
            {busy ? 'Recording…' : 'Record stock movement'}
          </button>
        </form>
      )}

      {workflow === 'count' && (
        <form className="inventory-action-form" onSubmit={submitCount}>
          <div className="inventory-action-grid">
            <label className="cl-admin-field inventory-action-lot">
              <span>Lot to count</span>
              <select
                value={countLotId}
                onChange={(event) => {
                  const next = countableLots.find(
                    ({ lot }) => String(lot.lotId) === event.target.value,
                  )
                  setCountLotId(event.target.value)
                  setCountedQuantity(
                    next ? String(next.lot.quantityOnHand) : '',
                  )
                }}
                required
              >
                <option value="">Select a lot</option>
                {countableLots.map((entry) => (
                  <option key={entry.lot.lotId} value={entry.lot.lotId}>
                    {lotLabel(entry)}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Counted quantity</span>
              <input
                type="number"
                min="0"
                step="0.01"
                value={countedQuantity}
                onChange={(event) => setCountedQuantity(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field inventory-action-notes">
              <span>Count notes</span>
              <textarea
                maxLength={500}
                value={countNotes}
                onChange={(event) => setCountNotes(event.target.value)}
                required
              />
            </label>
          </div>
          {countLot && countVariance !== null && (
            <p className="inventory-action-preview" aria-live="polite">
              Preview: expected {countLot.lot.quantityOnHand}{' '}
              {countLot.item.unit}; counted {countedAmount} {countLot.item.unit}
              ; variance {countVariance > 0 ? '+' : ''}
              {countVariance} {countLot.item.unit}. The count atomically sets
              the lot to the observed quantity and writes the variance ledger
              entry.
            </p>
          )}
          <button className="cl-btn-primary" type="submit" disabled={busy}>
            {busy ? 'Reconciling…' : 'Reconcile physical count'}
          </button>
        </form>
      )}

      {workflow === 'expiry' && (
        <form className="inventory-action-form" onSubmit={submitExpiry}>
          <div className="inventory-action-grid">
            <label className="cl-admin-field inventory-action-lot">
              <span>Expired lot</span>
              <select
                value={expiryLotId}
                onChange={(event) => {
                  const next = expiredLots.find(
                    ({ lot }) => String(lot.lotId) === event.target.value,
                  )
                  setExpiryLotId(event.target.value)
                  if (
                    next?.lot.status === 'quarantined' &&
                    expiryDisposition === 'quarantine'
                  ) {
                    setExpiryDisposition('return')
                  }
                }}
                required
              >
                <option value="">Select an expired lot</option>
                {expiredLots.map((entry) => (
                  <option key={entry.lot.lotId} value={entry.lot.lotId}>
                    {lotLabel(entry)} / {entry.lot.status}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Disposition</span>
              <select
                value={expiryDisposition}
                onChange={(event) =>
                  setExpiryDisposition(
                    event.target.value as 'quarantine' | 'return' | 'destroy',
                  )
                }
              >
                <option
                  value="quarantine"
                  disabled={expiryLot?.lot.status === 'quarantined'}
                >
                  Quarantine pending decision
                </option>
                <option value="return">Return expired stock</option>
                <option value="destroy">Destroy expired stock</option>
              </select>
            </label>
            {expiryDisposition === 'destroy' && (
              <>
                <label className="cl-admin-field">
                  <span>Destruction method</span>
                  <input
                    maxLength={250}
                    value={expiryMethod}
                    onChange={(event) => setExpiryMethod(event.target.value)}
                    required
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Witness</span>
                  <input
                    maxLength={250}
                    value={expiryWitness}
                    onChange={(event) => setExpiryWitness(event.target.value)}
                    required
                  />
                </label>
              </>
            )}
            <label className="cl-admin-field inventory-action-notes">
              <span>Disposition notes</span>
              <textarea
                maxLength={500}
                value={expiryNotes}
                onChange={(event) => setExpiryNotes(event.target.value)}
                required
              />
            </label>
          </div>
          {expiryLot && (
            <p className="inventory-action-preview">
              Preview: {expiryDisposition} the full{' '}
              {expiryLot.lot.quantityOnHand} {expiryLot.item.unit} in expired
              lot {expiryLot.lot.lotNumber}. Return or destruction removes the
              lot quantity and writes a matching ledger delta; quarantine
              retains quantity under a non-active status.
            </p>
          )}
          <button className="cl-btn-primary" type="submit" disabled={busy}>
            {busy ? 'Recording…' : 'Record expiry disposition'}
          </button>
        </form>
      )}

      {workflow === 'destruction' && (
        <form className="inventory-action-form" onSubmit={submitDestruction}>
          <div className="inventory-action-grid">
            <label className="cl-admin-field inventory-action-lot">
              <span>Lot to destroy</span>
              <select
                value={destructionLotId}
                onChange={(event) => {
                  setDestructionLotId(event.target.value)
                  setDestructionConfirmed(false)
                }}
                required
              >
                <option value="">Select an active lot</option>
                {activeLots.map((entry) => (
                  <option key={entry.lot.lotId} value={entry.lot.lotId}>
                    {lotLabel(entry)}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Destruction date</span>
              <input
                type="date"
                max={new Date().toISOString().slice(0, 10)}
                value={destructionDate}
                onChange={(event) => setDestructionDate(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field">
              <span>Destruction method</span>
              <input
                maxLength={250}
                value={destructionMethod}
                onChange={(event) => setDestructionMethod(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field">
              <span>Witness</span>
              <input
                maxLength={250}
                value={destructionWitness}
                onChange={(event) => setDestructionWitness(event.target.value)}
                required
              />
            </label>
            <label className="cl-admin-field inventory-action-notes">
              <span>Destruction notes</span>
              <textarea
                maxLength={250}
                value={destructionNotes}
                onChange={(event) => setDestructionNotes(event.target.value)}
                required
              />
            </label>
          </div>
          {destructionLot && (
            <div className="inventory-destruction-confirmation">
              <p className="inventory-action-preview">
                This irreversibly removes all{' '}
                {destructionLot.lot.quantityOnHand} {destructionLot.item.unit}{' '}
                from lot {destructionLot.lot.lotNumber} (
                {formatCurrency(
                  destructionLot.lot.quantityOnHand *
                    destructionLot.lot.unitCost,
                )}
                ) and records matching destruction and ledger evidence.
              </p>
              <label>
                <input
                  type="checkbox"
                  checked={destructionConfirmed}
                  onChange={(event) =>
                    setDestructionConfirmed(event.target.checked)
                  }
                />{' '}
                I confirm the selected full lot was destroyed using the method
                and witness recorded above.
              </label>
            </div>
          )}
          <button
            className="cl-btn-danger"
            type="submit"
            disabled={busy || !destructionConfirmed}
          >
            {busy ? 'Recording…' : 'Record witnessed destruction'}
          </button>
        </form>
      )}

      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}

      {result && (
        <div className="inventory-action-result" aria-live="polite">
          {result.kind === 'movement' && (
            <>
              <strong>Stock movement recorded</strong>
              <span>
                Transaction{' '}
                <code>{result.value.transaction.transactionId}</code> /{' '}
                {result.value.transaction.performedBy} /{' '}
                {new Date(result.value.transaction.occurredAt).toLocaleString()}
              </span>
              <span>
                Lot on hand: {result.value.lot.quantityOnHand} {result.unit};
                item on hand: {result.value.itemQuantityOnHand} {result.unit}.
              </span>
              {result.value.transferId && (
                <span>
                  Transfer <code>{result.value.transferId}</code>; destination
                  lot <code>{result.value.counterpartyLot?.lotId}</code>.
                </span>
              )}
            </>
          )}
          {result.kind === 'count' && (
            <>
              <strong>Physical count reconciled</strong>
              <span>
                Reconciliation <code>{result.value.reconciliationId}</code> /
                transaction{' '}
                <code>{result.value.transaction.transactionId}</code>
              </span>
              <span>
                Expected {result.value.expectedQuantity}; counted{' '}
                {result.value.countedQuantity}; variance{' '}
                {result.value.quantityDelta > 0 ? '+' : ''}
                {result.value.quantityDelta} {result.unit}.
              </span>
              <span>
                {result.value.countedBy} /{' '}
                {new Date(result.value.countedAt).toLocaleString()}
              </span>
            </>
          )}
          {result.kind === 'expiry' && (
            <>
              <strong>Expired-lot disposition recorded</strong>
              <span>
                Disposition <code>{result.value.dispositionId}</code> /{' '}
                {result.value.disposition} / {result.value.quantityAffected}{' '}
                {result.unit} affected
              </span>
              {result.value.transaction && (
                <span>
                  Ledger transaction{' '}
                  <code>{result.value.transaction.transactionId}</code> / delta{' '}
                  {result.value.transaction.quantityDelta}.
                </span>
              )}
              {result.value.destructionId && (
                <span>
                  Destruction <code>{result.value.destructionId}</code>.
                </span>
              )}
              <span>
                {result.value.disposedBy} /{' '}
                {new Date(result.value.disposedAt).toLocaleString()}
              </span>
            </>
          )}
          {result.kind === 'destruction' && (
            <>
              <strong>Witnessed destruction recorded</strong>
              <span>
                Destruction <code>{result.value.destructionId}</code> / ledger{' '}
                <code>{result.value.transaction.transactionId}</code>
              </span>
              <span>
                {result.value.quantityAffected} {result.unit} removed; resulting
                lot quantity {result.value.lot.quantityOnHand} {result.unit}.
              </span>
              <span>
                {result.value.destroyedBy} /{' '}
                {new Date(result.value.recordedAt).toLocaleString()} / witness{' '}
                {result.value.witness}
              </span>
            </>
          )}
        </div>
      )}
    </section>
  )
}
