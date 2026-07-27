import { useEffect, useEffectEvent, useMemo, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  createInventoryTransaction,
  createInventoryTransfer,
  downloadInventoryActivityCsv,
  getInventory,
  getInventoryActivityReport,
  type InventoryActivityReport,
  type InventoryResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

export default function InventoryWorkspace() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [data, setData] = useState<InventoryResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [lotId, setLotId] = useState('')
  const [kind, setKind] = useState('consumption')
  const [destination, setDestination] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [activity, setActivity] = useState<InventoryActivityReport | null>(
    null,
  )
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [activityFacility, setActivityFacility] = useState('')

  const lots = useMemo(
    () =>
      data?.items.flatMap((item) =>
        item.lots.map((lot) => ({ item, lot })),
      ) ?? [],
    [data],
  )
  const selected = lots.find((entry) => String(entry.lot.lotId) === lotId)

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

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!selected || !Number(quantity)) return
    setSaving(true)
    try {
      if (kind === 'transfer') {
        await createInventoryTransfer(session.sessionId, {
          sourceLotId: selected.lot.lotId,
          destinationFacilityId: Number(destination),
          quantity: Number(quantity),
          reason: reason || null,
        })
      } else {
        await createInventoryTransaction(session.sessionId, {
          lotId: selected.lot.lotId,
          transactionType: kind,
          quantity: Number(quantity),
          reason: reason || null,
        })
      }
      showToast(
        kind === 'transfer'
          ? 'Inventory transferred.'
          : 'Inventory activity recorded.',
        'success',
      )
      setReason('')
      await load()
    } catch {
      showToast('Could not record inventory activity.', 'error')
    } finally {
      setSaving(false)
    }
  }

  async function loadActivity() {
    try {
      setActivity(
        await getInventoryActivityReport(session.sessionId, {
          from: from || undefined,
          to: to || undefined,
          facilityId: activityFacility
            ? Number(activityFacility)
            : undefined,
        }),
      )
    } catch {
      showToast('Could not load inventory activity.', 'error')
    }
  }

  async function exportActivity() {
    try {
      const blob = await downloadInventoryActivityCsv(session.sessionId, {
        from: from || undefined,
        to: to || undefined,
        facilityId: activityFacility ? Number(activityFacility) : undefined,
      })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'legacy-ehr-inventory-activity.csv'
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      showToast('Could not export inventory activity.', 'error')
    }
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

          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">Record activity</h2>
            </div>
            <form className="inventory-record-form" onSubmit={submit}>
              <label className="cl-admin-field">
                <span>Inventory lot</span>
                <select
                  value={lotId}
                  onChange={(event) => setLotId(event.target.value)}
                  required
                >
                  <option value="">Select a lot</option>
                  {lots.map(({ item, lot }) => (
                    <option key={lot.lotId} value={lot.lotId}>
                      {item.itemCode} / {lot.facilityCode} / {lot.lotNumber}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Activity</span>
                <select
                  value={kind}
                  onChange={(event) => setKind(event.target.value)}
                >
                  <option value="consumption">Consumption</option>
                  <option value="purchase">Purchase receipt</option>
                  <option value="adjustment">Count adjustment</option>
                  <option value="destruction">Destruction</option>
                  <option value="transfer">Transfer</option>
                </select>
              </label>
              {kind === 'transfer' && (
                <label className="cl-admin-field">
                  <span>Destination facility</span>
                  <select
                    value={destination}
                    onChange={(event) => setDestination(event.target.value)}
                    required
                  >
                    <option value="">Select a destination</option>
                    {data.facilities
                      .filter(
                        (facility) =>
                          facility.code !== selected?.lot.facilityCode,
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
                  step="0.01"
                  value={quantity}
                  onChange={(event) => setQuantity(event.target.value)}
                  required
                />
              </label>
              <label className="cl-admin-field">
                <span>Reason</span>
                <input
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </label>
              <button type="submit" className="cl-btn-primary" disabled={saving}>
                {saving ? 'Recording...' : 'Record'}
              </button>
            </form>
          </section>

          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">Activity report</h2>
            </div>
            <div className="cl-inline-form">
              <label className="cl-admin-field">
                <span>From date</span>
                <input
                  className="ne-input"
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>To date</span>
                <input
                  className="ne-input"
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Facility</span>
                <select
                  value={activityFacility}
                  onChange={(event) =>
                    setActivityFacility(event.target.value)
                  }
                >
                  <option value="">All facilities</option>
                  {data.facilities.map((facility) => (
                    <option
                      key={facility.facilityId}
                      value={facility.facilityId}
                    >
                      {facility.code}
                    </option>
                  ))}
                </select>
              </label>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={loadActivity}
              >
                Run report
              </button>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={exportActivity}
              >
                CSV export
              </button>
            </div>
            {activity && (
              <>
                <p className="cl-empty-text">
                  {activity.totalEntries} matching entries
                </p>
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>Item</th>
                      <th>Facility</th>
                      <th>Type</th>
                      <th>Delta</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activity.entries.map((entry) => (
                      <tr key={entry.transactionId}>
                        <td>{new Date(entry.occurredAt).toLocaleString()}</td>
                        <td>{entry.itemCode}</td>
                        <td>{entry.facilityCode}</td>
                        <td>{entry.transactionType}</td>
                        <td>{entry.quantityDelta}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}
          </section>

          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">Stock ledger</h2>
            </div>
            <div className="inventory-modern-list">
              {data.items.map((item) => (
                <article
                  key={item.itemId}
                  className={`inventory-modern-item${
                    item.belowReorderPoint ? ' inventory-modern-low' : ''
                  }`}
                >
                  <div>
                    <strong>{item.name}</strong>
                    <p>
                      {item.itemCode} / {item.category}
                    </p>
                  </div>
                  <div>
                    <strong>
                      {item.quantityOnHand} {item.unit}
                    </strong>
                    <p>Reorder at {item.reorderPoint}</p>
                  </div>
                  <div className="inventory-modern-lots">
                    {item.lots.map((lot) => (
                      <span key={lot.lotId}>
                        {lot.facilityCode} / {lot.lotNumber} /{' '}
                        {lot.quantityOnHand}
                      </span>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </div>
  )
}
