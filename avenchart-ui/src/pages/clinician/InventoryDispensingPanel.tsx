import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  allocateInventoryPatientSale,
  createInventoryPatientSale,
  dispenseInventoryPrescription,
  getClinicalLists,
  searchEncounters,
  searchPatients,
  type EncounterListItem,
  type InventoryItem,
  type InventoryLot,
  type InventoryPatientSale,
  type InventoryPatientSaleAllocation,
  type InventoryPrescriptionDispense,
  type PatientListItem,
  type PrescriptionListItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  items: InventoryItem[]
  onChanged: () => Promise<void>
  sessionId: string
}

type PatientSearchState =
  | { status: 'loading' }
  | { status: 'ready'; patients: PatientListItem[]; total: number }
  | { status: 'error'; message: string }

type PatientContextState =
  | { status: 'idle' }
  | { status: 'loading'; patient: PatientListItem }
  | {
      status: 'ready'
      patient: PatientListItem
      encounters: EncounterListItem[]
      prescriptions: PrescriptionListItem[]
    }
  | { status: 'error'; patient: PatientListItem; message: string }

type LotWithItem = {
  item: InventoryItem
  lot: InventoryLot
}

type Result =
  | {
      kind: 'sale'
      itemName: string
      unit: string
      value: InventoryPatientSale
    }
  | {
      kind: 'allocation'
      itemName: string
      unit: string
      value: InventoryPatientSaleAllocation
    }
  | {
      kind: 'prescription'
      itemName: string
      unit: string
      value: InventoryPrescriptionDispense
    }

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

function formatDate(value?: string | null) {
  if (!value) return 'No expiry tracked'
  return new Date(`${value}T00:00:00`).toLocaleDateString()
}

function caughtMessage(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function sortLots(left: LotWithItem, right: LotWithItem) {
  if (!left.lot.expirationDate && right.lot.expirationDate) return 1
  if (left.lot.expirationDate && !right.lot.expirationDate) return -1
  const dateOrder = (left.lot.expirationDate ?? '').localeCompare(
    right.lot.expirationDate ?? '',
  )
  if (dateOrder !== 0) return dateOrder
  return left.lot.lotNumber.localeCompare(right.lot.lotNumber)
}

export default function InventoryDispensingPanel({
  items,
  onChanged,
  sessionId,
}: Props) {
  const [workflow, setWorkflow] = useState<'sale' | 'prescription'>('sale')
  const [patientQuery, setPatientQuery] = useState('')
  const [patientSearch, setPatientSearch] = useState<PatientSearchState>({
    status: 'loading',
  })
  const [patientContext, setPatientContext] = useState<PatientContextState>({
    status: 'idle',
  })
  const [selectedPatientId, setSelectedPatientId] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<Result | null>(null)
  const patientSearchRequest = useRef(0)
  const patientContextRequest = useRef(0)

  const [encounter, setEncounter] = useState('')
  const [saleMode, setSaleMode] = useState<'lot' | 'fefo'>('lot')
  const [saleLotId, setSaleLotId] = useState('')
  const [saleItemId, setSaleItemId] = useState('')
  const [saleDate, setSaleDate] = useState(
    new Date().toISOString().slice(0, 10),
  )
  const [saleQuantity, setSaleQuantity] = useState('1')
  const [saleFee, setSaleFee] = useState('0')
  const [saleNotes, setSaleNotes] = useState('')

  const [prescriptionId, setPrescriptionId] = useState('')
  const [prescriptionDate, setPrescriptionDate] = useState(
    new Date().toISOString().slice(0, 10),
  )
  const [prescriptionQuantity, setPrescriptionQuantity] = useState('1')
  const [prescriptionFee, setPrescriptionFee] = useState('0')
  const [prescriptionNotes, setPrescriptionNotes] = useState('')

  const eligibleLots = useMemo(
    () =>
      items
        .flatMap((item) => item.lots.map((lot) => ({ item, lot })))
        .filter(
          ({ lot }) =>
            lot.status === 'active' &&
            lot.quantityOnHand > 0 &&
            lot.expiryStatus !== 'expired',
        ),
    [items],
  )
  const saleItems = useMemo(
    () =>
      items.filter((item) =>
        eligibleLots.some(
          ({ item: candidate }) => candidate.itemId === item.itemId,
        ),
      ),
    [eligibleLots, items],
  )
  const selectedLot = eligibleLots.find(
    ({ lot }) => String(lot.lotId) === saleLotId,
  )
  const selectedItem = saleItems.find(
    (item) => String(item.itemId) === saleItemId,
  )
  const selectedItemLots = useMemo(
    () =>
      eligibleLots
        .filter(({ item }) => item.itemId === selectedItem?.itemId)
        .sort(sortLots),
    [eligibleLots, selectedItem?.itemId],
  )
  const selectedPrescription =
    patientContext.status === 'ready'
      ? patientContext.prescriptions.find(
          (prescription) => prescription.id === prescriptionId,
        )
      : undefined
  const quantity = Number(saleQuantity)
  const fee = Number(saleFee)
  const prescriptionAmount = Number(prescriptionQuantity)
  const prescriptionFeeAmount = Number(prescriptionFee)
  const itemAvailable = selectedItemLots.reduce(
    (total, entry) => total + entry.lot.quantityOnHand,
    0,
  )
  const allocationPreview = useMemo(() => {
    if (!Number.isFinite(quantity) || quantity <= 0) return []
    let remaining = quantity
    return selectedItemLots.flatMap((entry) => {
      if (remaining <= 0) return []
      const allocated = Math.min(remaining, entry.lot.quantityOnHand)
      remaining -= allocated
      return [{ ...entry, allocated }]
    })
  }, [quantity, selectedItemLots])

  const runPatientSearch = useCallback(
    async (query: string) => {
      const requestId = ++patientSearchRequest.current
      patientContextRequest.current += 1
      setPatientSearch({ status: 'loading' })
      setSelectedPatientId('')
      setPatientContext({ status: 'idle' })
      setEncounter('')
      setPrescriptionId('')
      setError(null)
      setResult(null)
      try {
        const response = await searchPatients(sessionId, {
          search: query.trim() || undefined,
          limit: 20,
        })
        if (requestId !== patientSearchRequest.current) return
        setPatientSearch({
          status: 'ready',
          patients: response.patients,
          total: response.totalMatches,
        })
      } catch (caught) {
        if (requestId !== patientSearchRequest.current) return
        setPatientSearch({
          status: 'error',
          message: caughtMessage(caught, 'Could not search patients.'),
        })
      }
    },
    [sessionId],
  )

  useEffect(() => {
    void runPatientSearch('')
  }, [runPatientSearch])

  async function loadPatientContext(patient: PatientListItem) {
    const requestId = ++patientContextRequest.current
    setPatientContext({ status: 'loading', patient })
    setEncounter('')
    setPrescriptionId('')
    setError(null)
    setResult(null)
    try {
      const [encounterResponse, clinicalLists] = await Promise.all([
        searchEncounters(sessionId, {
          patientId: patient.canonicalId,
          limit: 50,
        }),
        getClinicalLists(sessionId, patient.canonicalId),
      ])
      const encounters = [...encounterResponse.encounters].sort((left, right) =>
        right.date.localeCompare(left.date),
      )
      const prescriptions = clinicalLists.prescriptions.filter(
        (prescription) => prescription.active === 1,
      )
      if (requestId !== patientContextRequest.current) return
      setPatientContext({
        status: 'ready',
        patient,
        encounters,
        prescriptions,
      })
      if (encounters[0]) setEncounter(String(encounters[0].encounter))
      if (prescriptions[0]) setPrescriptionId(prescriptions[0].id)
    } catch (caught) {
      if (requestId !== patientContextRequest.current) return
      setPatientContext({
        status: 'error',
        patient,
        message: caughtMessage(
          caught,
          'Could not load patient encounters and prescriptions.',
        ),
      })
    }
  }

  function choosePatient(patientId: string) {
    setSelectedPatientId(patientId)
    const patient =
      patientSearch.status === 'ready'
        ? patientSearch.patients.find(
            (candidate) => candidate.canonicalId === patientId,
          )
        : undefined
    if (patient) {
      void loadPatientContext(patient)
    } else {
      patientContextRequest.current += 1
      setPatientContext({ status: 'idle' })
    }
  }

  async function submitSale(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (patientContext.status !== 'ready') {
      setError('Select a patient and wait for encounter context to load.')
      return
    }
    const encounterNumber = Number(encounter)
    if (
      !Number.isInteger(encounterNumber) ||
      !patientContext.encounters.some(
        (candidate) => candidate.encounter === encounterNumber,
      )
    ) {
      setError('Select an encounter that belongs to this patient.')
      return
    }
    if (
      !saleDate ||
      saleDate > new Date().toISOString().slice(0, 10) ||
      !Number.isFinite(quantity) ||
      quantity <= 0 ||
      !Number.isFinite(fee) ||
      fee < 0
    ) {
      setError(
        'Sale date cannot be in the future; quantity must be positive and fee cannot be negative.',
      )
      return
    }
    if (
      saleMode === 'lot' &&
      (!selectedLot || quantity > selectedLot.lot.quantityOnHand)
    ) {
      setError('Select an eligible lot with enough quantity for this sale.')
      return
    }
    if (saleMode === 'fefo' && (!selectedItem || quantity > itemAvailable)) {
      setError(
        `Eligible lots cannot fulfill this quantity. Available: ${itemAvailable} ${selectedItem?.unit ?? 'units'}.`,
      )
      return
    }

    setBusy(true)
    try {
      if (saleMode === 'lot' && selectedLot) {
        const value = await createInventoryPatientSale(sessionId, {
          lotId: selectedLot.lot.lotId,
          patientId: patientContext.patient.canonicalId,
          encounter: encounterNumber,
          saleDate,
          quantity,
          fee,
          notes: saleNotes.trim() || null,
        })
        setResult({
          kind: 'sale',
          itemName: selectedLot.item.name,
          unit: selectedLot.item.unit,
          value,
        })
      } else if (selectedItem) {
        const value = await allocateInventoryPatientSale(sessionId, {
          itemId: selectedItem.itemId,
          patientId: patientContext.patient.canonicalId,
          encounter: encounterNumber,
          saleDate,
          quantity,
          fee,
          notes: saleNotes.trim() || null,
        })
        setResult({
          kind: 'allocation',
          itemName: selectedItem.name,
          unit: selectedItem.unit,
          value,
        })
      }
      showToast('Patient-linked inventory sale recorded.', 'success')
      await onChanged()
      setSaleQuantity('1')
      setSaleNotes('')
    } catch (caught) {
      setError(
        caughtMessage(
          caught,
          'Could not record the patient-linked inventory sale.',
        ),
      )
    } finally {
      setBusy(false)
    }
  }

  async function submitPrescription(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setResult(null)
    if (patientContext.status !== 'ready' || !selectedPrescription) {
      setError('Select a patient with an active prescription.')
      return
    }
    if (
      !prescriptionDate ||
      prescriptionDate > new Date().toISOString().slice(0, 10) ||
      !Number.isFinite(prescriptionAmount) ||
      prescriptionAmount <= 0 ||
      !Number.isFinite(prescriptionFeeAmount) ||
      prescriptionFeeAmount < 0
    ) {
      setError(
        'Dispense date cannot be in the future; quantity must be positive and fee cannot be negative.',
      )
      return
    }

    setBusy(true)
    try {
      const value = await dispenseInventoryPrescription(sessionId, {
        prescriptionId: selectedPrescription.id,
        saleDate: prescriptionDate,
        quantity: prescriptionAmount,
        fee: prescriptionFeeAmount,
        notes: prescriptionNotes.trim() || null,
      })
      const item = items.find((candidate) => candidate.itemId === value.itemId)
      setResult({
        kind: 'prescription',
        itemName: item?.name ?? `Inventory item ${value.itemId}`,
        unit: item?.unit ?? 'units',
        value,
      })
      showToast('Prescription-linked inventory dispense recorded.', 'success')
      await onChanged()
      setPrescriptionQuantity('1')
      setPrescriptionNotes('')
    } catch (caught) {
      setError(
        caughtMessage(
          caught,
          'Could not dispense this prescription from one eligible lot.',
        ),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="cl-card inventory-dispensing">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Patient sales and dispensing</h2>
          <p className="cl-card-subtitle">
            Bind every stock debit to a selected patient and encounter. Fees
            below are local inventory-sale evidence and do not create a billing
            charge.
          </p>
        </div>
      </div>

      <div
        className="inventory-dispensing-workflow"
        role="group"
        aria-label="Dispensing workflow"
      >
        <button
          className={
            workflow === 'sale' ? 'cl-btn-primary' : 'cl-btn-secondary'
          }
          type="button"
          aria-pressed={workflow === 'sale'}
          onClick={() => {
            setWorkflow('sale')
            setError(null)
            setResult(null)
          }}
        >
          Patient inventory sale
        </button>
        <button
          className={
            workflow === 'prescription' ? 'cl-btn-primary' : 'cl-btn-secondary'
          }
          type="button"
          aria-pressed={workflow === 'prescription'}
          onClick={() => {
            setWorkflow('prescription')
            setError(null)
            setResult(null)
          }}
        >
          Prescription dispense
        </button>
      </div>

      <form
        className="inventory-patient-search"
        onSubmit={(event) => {
          event.preventDefault()
          void runPatientSearch(patientQuery)
        }}
      >
        <label className="cl-admin-field">
          <span>Find patient</span>
          <input
            type="search"
            value={patientQuery}
            onChange={(event) => setPatientQuery(event.target.value)}
            placeholder="Name, public ID, or canonical ID"
          />
        </label>
        <button className="cl-btn-secondary" type="submit">
          Search patients
        </button>
      </form>

      {patientSearch.status === 'loading' && (
        <p className="cl-empty-text" aria-live="polite">
          Loading patient choices…
        </p>
      )}
      {patientSearch.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{patientSearch.message}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void runPatientSearch(patientQuery)}
          >
            Retry
          </button>
        </div>
      )}
      {patientSearch.status === 'ready' && (
        <label className="cl-admin-field inventory-patient-picker">
          <span>Patient</span>
          <select
            aria-label="Patient"
            value={selectedPatientId}
            onChange={(event) => choosePatient(event.target.value)}
          >
            <option value="">Select a patient</option>
            {patientSearch.patients.map((patient) => (
              <option key={patient.canonicalId} value={patient.canonicalId}>
                {patient.displayName} / {patient.pubpid} / {patient.canonicalId}
              </option>
            ))}
          </select>
          <small>
            Showing {patientSearch.patients.length} of {patientSearch.total}{' '}
            matches. Search to narrow the list.
          </small>
        </label>
      )}

      {patientContext.status === 'loading' && (
        <p className="cl-empty-text" aria-live="polite">
          Loading encounters and prescriptions for{' '}
          {patientContext.patient.displayName}…
        </p>
      )}
      {patientContext.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{patientContext.message}</span>
          <button
            className="link-button"
            type="button"
            onClick={() => void loadPatientContext(patientContext.patient)}
          >
            Retry patient context
          </button>
        </div>
      )}

      {patientContext.status === 'ready' && (
        <>
          <div className="inventory-patient-context">
            <strong>{patientContext.patient.displayName}</strong>
            <span>
              Public ID {patientContext.patient.pubpid} / canonical ID{' '}
              <code>{patientContext.patient.canonicalId}</code>
            </span>
            <span>
              {patientContext.encounters.length} recent encounters /{' '}
              {patientContext.prescriptions.length} active prescriptions
            </span>
          </div>

          {workflow === 'sale' && (
            <form className="inventory-dispensing-form" onSubmit={submitSale}>
              <fieldset className="inventory-receiving-mode">
                <legend>Sale allocation</legend>
                <label>
                  <input
                    type="radio"
                    checked={saleMode === 'lot'}
                    onChange={() => setSaleMode('lot')}
                  />{' '}
                  Debit one selected lot
                </label>
                <label>
                  <input
                    type="radio"
                    checked={saleMode === 'fefo'}
                    onChange={() => setSaleMode('fefo')}
                  />{' '}
                  Allocate earliest expiry first
                </label>
              </fieldset>
              <div className="inventory-dispensing-grid">
                <label className="cl-admin-field">
                  <span>Patient encounter</span>
                  <select
                    aria-label="Patient encounter"
                    value={encounter}
                    onChange={(event) => setEncounter(event.target.value)}
                    required
                  >
                    <option value="">Select an encounter</option>
                    {patientContext.encounters.map((item) => (
                      <option key={item.encounter} value={item.encounter}>
                        {item.date} / {item.encounter} /{' '}
                        {item.reason ?? 'No reason recorded'}
                      </option>
                    ))}
                  </select>
                </label>
                {saleMode === 'lot' ? (
                  <label className="cl-admin-field inventory-dispensing-target">
                    <span>Sale inventory lot</span>
                    <select
                      aria-label="Sale inventory lot"
                      value={saleLotId}
                      onChange={(event) => setSaleLotId(event.target.value)}
                      required
                    >
                      <option value="">Select an eligible lot</option>
                      {eligibleLots.map(({ item, lot }) => (
                        <option key={lot.lotId} value={lot.lotId}>
                          {item.itemCode} / {lot.facilityCode} / {lot.lotNumber}{' '}
                          / {lot.quantityOnHand} {item.unit} /{' '}
                          {formatDate(lot.expirationDate)}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : (
                  <label className="cl-admin-field inventory-dispensing-target">
                    <span>Sale inventory item</span>
                    <select
                      aria-label="Sale inventory item"
                      value={saleItemId}
                      onChange={(event) => setSaleItemId(event.target.value)}
                      required
                    >
                      <option value="">Select an item</option>
                      {saleItems.map((item) => (
                        <option key={item.itemId} value={item.itemId}>
                          {item.itemCode} / {item.name} / {item.quantityOnHand}{' '}
                          {item.unit} total
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                <label className="cl-admin-field">
                  <span>Sale date</span>
                  <input
                    type="date"
                    max={new Date().toISOString().slice(0, 10)}
                    value={saleDate}
                    onChange={(event) => setSaleDate(event.target.value)}
                    required
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Sale quantity</span>
                  <input
                    type="number"
                    min="0.01"
                    max={
                      saleMode === 'lot'
                        ? selectedLot?.lot.quantityOnHand
                        : itemAvailable || undefined
                    }
                    step="0.01"
                    value={saleQuantity}
                    onChange={(event) => setSaleQuantity(event.target.value)}
                    required
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Inventory sale fee (USD)</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={saleFee}
                    onChange={(event) => setSaleFee(event.target.value)}
                    required
                  />
                </label>
                <label className="cl-admin-field inventory-dispensing-notes">
                  <span>Sale notes (optional)</span>
                  <textarea
                    maxLength={250}
                    value={saleNotes}
                    onChange={(event) => setSaleNotes(event.target.value)}
                  />
                </label>
              </div>

              {saleMode === 'lot' && selectedLot && quantity > 0 && (
                <p className="inventory-action-preview">
                  Preview: debit {quantity} {selectedLot.item.unit} from lot{' '}
                  {selectedLot.lot.lotNumber}; on hand{' '}
                  {selectedLot.lot.quantityOnHand} →{' '}
                  {Math.max(0, selectedLot.lot.quantityOnHand - quantity)}{' '}
                  {selectedLot.item.unit}. Fee evidence:{' '}
                  {formatCurrency(Number.isFinite(fee) ? fee : 0)}.
                </p>
              )}
              {saleMode === 'fefo' && selectedItem && (
                <div className="inventory-action-preview">
                  <strong>
                    FEFO preview: {itemAvailable} {selectedItem.unit} eligible
                  </strong>
                  {quantity > itemAvailable ? (
                    <span>
                      Requested {quantity} {selectedItem.unit} cannot be
                      fulfilled.
                    </span>
                  ) : (
                    allocationPreview.map((entry) => (
                      <span key={entry.lot.lotId}>
                        Lot {entry.lot.lotNumber}: {entry.allocated}{' '}
                        {entry.item.unit} / expires{' '}
                        {formatDate(entry.lot.expirationDate)}
                      </span>
                    ))
                  )}
                </div>
              )}

              {patientContext.encounters.length === 0 && (
                <div className="hint-banner">
                  This patient has no selectable encounter. A patient inventory
                  sale requires an encounter owned by the patient.
                </div>
              )}
              <button
                className="cl-btn-primary"
                type="submit"
                disabled={busy || patientContext.encounters.length === 0}
              >
                {busy ? 'Recording…' : 'Record patient inventory sale'}
              </button>
            </form>
          )}

          {workflow === 'prescription' && (
            <form
              className="inventory-dispensing-form"
              onSubmit={submitPrescription}
            >
              <div className="hint-banner">
                The backend derives patient, encounter, and RXCUI from the
                active prescription, then chooses the earliest-expiring single
                eligible lot. It never combines lots; unavailable mapping or
                insufficient single-lot stock is reported without mutation.
              </div>
              <div className="inventory-dispensing-grid">
                <label className="cl-admin-field inventory-dispensing-target">
                  <span>Active prescription</span>
                  <select
                    aria-label="Active prescription"
                    value={prescriptionId}
                    onChange={(event) => setPrescriptionId(event.target.value)}
                    required
                  >
                    <option value="">Select a prescription</option>
                    {patientContext.prescriptions.map((prescription) => (
                      <option key={prescription.id} value={prescription.id}>
                        {prescription.drug} / {prescription.id} /{' '}
                        {prescription.dosage ?? 'dose not recorded'}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>Dispense date</span>
                  <input
                    type="date"
                    max={new Date().toISOString().slice(0, 10)}
                    value={prescriptionDate}
                    onChange={(event) =>
                      setPrescriptionDate(event.target.value)
                    }
                    required
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Dispense quantity</span>
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={prescriptionQuantity}
                    onChange={(event) =>
                      setPrescriptionQuantity(event.target.value)
                    }
                    required
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Inventory dispense fee (USD)</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={prescriptionFee}
                    onChange={(event) => setPrescriptionFee(event.target.value)}
                    required
                  />
                </label>
                <label className="cl-admin-field inventory-dispensing-notes">
                  <span>Dispense notes (optional)</span>
                  <textarea
                    maxLength={250}
                    value={prescriptionNotes}
                    onChange={(event) =>
                      setPrescriptionNotes(event.target.value)
                    }
                  />
                </label>
              </div>
              {selectedPrescription && (
                <p className="inventory-action-preview">
                  Prescription <code>{selectedPrescription.id}</code> /{' '}
                  {selectedPrescription.drug} /{' '}
                  {selectedPrescription.dosage ?? 'dose not recorded'} / source
                  quantity {selectedPrescription.quantity ?? 'not recorded'}.
                </p>
              )}
              {patientContext.prescriptions.length === 0 && (
                <div className="hint-banner">
                  This patient has no active prescription available for local
                  inventory dispensing.
                </div>
              )}
              <button
                className="cl-btn-primary"
                type="submit"
                disabled={busy || patientContext.prescriptions.length === 0}
              >
                {busy ? 'Dispensing…' : 'Dispense linked prescription'}
              </button>
            </form>
          )}
        </>
      )}

      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}

      {result && (
        <div className="inventory-dispensing-result" aria-live="polite">
          {result.kind === 'sale' && (
            <>
              <strong>Patient inventory sale recorded</strong>
              <span>
                Sale <code>{result.value.saleId}</code> / transaction{' '}
                <code>
                  {result.value.inventoryMutation.transaction.transactionId}
                </code>
              </span>
              <span>
                Patient <code>{result.value.patientId}</code> / encounter{' '}
                {result.value.encounter} / {result.itemName}
              </span>
              <span>
                {result.value.quantity} {result.unit} /{' '}
                {formatCurrency(result.value.fee)} / lot remaining{' '}
                {result.value.inventoryMutation.lot.quantityOnHand}{' '}
                {result.unit}
              </span>
              <span>
                {result.value.soldBy} /{' '}
                {new Date(result.value.soldAt).toLocaleString()}
              </span>
            </>
          )}
          {result.kind === 'allocation' && (
            <>
              <strong>FEFO patient inventory sale recorded</strong>
              <span>
                Batch <code>{result.value.saleBatchId}</code> / patient{' '}
                <code>{result.value.patientId}</code> / encounter{' '}
                {result.value.encounter}
              </span>
              <span>
                {result.value.quantity} {result.unit} of {result.itemName} /{' '}
                {formatCurrency(result.value.fee)} /{' '}
                {result.value.allocations.length} lot allocations
              </span>
              {result.value.allocations.map((allocation) => (
                <span key={allocation.saleId}>
                  Lot {allocation.lotNumber}: {allocation.quantity}{' '}
                  {result.unit} / {formatCurrency(allocation.fee)} / sale{' '}
                  <code>{allocation.saleId}</code> / transaction{' '}
                  <code>{allocation.transactionId}</code>
                </span>
              ))}
            </>
          )}
          {result.kind === 'prescription' && (
            <>
              <strong>Prescription-linked dispense recorded</strong>
              <span>
                Prescription <code>{result.value.prescriptionId}</code> / RXCUI{' '}
                <code>{result.value.rxNormCode}</code> / sale{' '}
                <code>{result.value.sale.saleId}</code>
              </span>
              <span>
                Patient <code>{result.value.patientId}</code> / encounter{' '}
                {result.value.encounter} / {result.itemName}
              </span>
              <span>
                Single lot {result.value.sale.inventoryMutation.lot.lotNumber} /{' '}
                {result.value.sale.quantity} {result.unit} /{' '}
                {formatCurrency(result.value.sale.fee)} / transaction{' '}
                <code>
                  {
                    result.value.sale.inventoryMutation.transaction
                      .transactionId
                  }
                </code>
              </span>
              <span>
                {result.value.sale.soldBy} /{' '}
                {new Date(result.value.sale.soldAt).toLocaleString()}
              </span>
            </>
          )}
        </div>
      )}
    </section>
  )
}
