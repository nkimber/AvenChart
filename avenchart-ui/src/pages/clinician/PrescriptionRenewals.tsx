import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { CheckCircle, Pill, XCircle } from 'lucide-react'
import {
  getClinicalLists,
  searchPatients,
  deactivatePrescription,
  createPrescription,
  type PrescriptionListItem,
  type PatientListItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type RxEntry = {
  patient: PatientListItem
  rx: PrescriptionListItem
  daysUntilExpiry: number | null
}

type AsyncState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; entries: RxEntry[] }
  | { status: 'error'; message: string }

function daysUntil(dateStr?: string | null): number | null {
  if (!dateStr) return null
  const diff = new Date(dateStr).getTime() - Date.now()
  return Math.ceil(diff / (1000 * 60 * 60 * 24))
}

function urgencyClass(days: number | null): string {
  if (days === null) return 'rx-urgency-unknown'
  if (days < 0) return 'rx-urgency-expired'
  if (days <= 7) return 'rx-urgency-critical'
  if (days <= 30) return 'rx-urgency-soon'
  return 'rx-urgency-ok'
}

function urgencyLabel(days: number | null): string {
  if (days === null) return 'No end date'
  if (days < 0) return `Expired ${Math.abs(days)}d ago`
  if (days === 0) return 'Expires today'
  return `${days}d remaining`
}

export default function PrescriptionRenewals() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<AsyncState>({ status: 'idle' })
  const [renewingId, setRenewingId] = useState<string | null>(null)
  const [renewDays, setRenewDays] = useState('90')
  const [processed, setProcessed] = useState<Set<string>>(new Set())
  const [filter, setFilter] = useState<'expiring' | 'expired' | 'all'>('expiring')

  useEffect(() => {
    setState({ status: 'loading' })
    searchPatients(session.sessionId, { limit: 50 })
      .then(async (data) => {
        const entries: RxEntry[] = []
        await Promise.allSettled(
          data.patients.slice(0, 30).map(async (patient) => {
            try {
              const lists = await getClinicalLists(session.sessionId, patient.canonicalId)
              for (const rx of lists.prescriptions) {
                if (!rx.active) continue
                const days = daysUntil(rx.endDate)
                if (days === null || days > 60) continue
                entries.push({ patient, rx, daysUntilExpiry: days })
              }
            } catch { /* skip failed patients */ }
          }),
        )
        entries.sort((a, b) => {
          const ad = a.daysUntilExpiry ?? 999
          const bd = b.daysUntilExpiry ?? 999
          return ad - bd
        })
        setState({ status: 'ready', entries })
      })
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function handleDeactivate(entry: RxEntry) {
    try {
      await deactivatePrescription(session.sessionId, entry.rx.id, {
        endDate: new Date().toISOString().slice(0, 10),
        note: 'Discontinued via renewal queue',
      })
      setProcessed((p) => new Set([...p, entry.rx.id]))
      showToast(`${entry.rx.drug} discontinued.`, 'success')
    } catch {
      showToast('Could not discontinue. Please try again.', 'error')
    }
  }

  async function handleRenew(entry: RxEntry) {
    if (!renewDays || isNaN(Number(renewDays))) return
    const days = Number(renewDays)
    const startDate = new Date().toISOString().slice(0, 10)
    const endDate = new Date(Date.now() + days * 86400000).toISOString().slice(0, 10)
    try {
      await createPrescription(session.sessionId, {
        patientId: entry.patient.canonicalId,
        drug: entry.rx.drug,
        dosage: entry.rx.dosage ?? '',
        quantity: entry.rx.quantity ?? '',
        route: entry.rx.route ?? null,
        startDate,
        refills: 0,
        note: `Renewed for ${days} days`,
        diagnosis: '',
      })
      await deactivatePrescription(session.sessionId, entry.rx.id, {
        endDate,
        note: `Renewed — new prescription created`,
      })
      setProcessed((p) => new Set([...p, entry.rx.id]))
      showToast(`${entry.rx.drug} renewed for ${days} days.`, 'success')
      setRenewingId(null)
    } catch {
      showToast('Renewal failed. Please try again.', 'error')
    }
  }

  const allEntries = state.status === 'ready' ? state.entries : []
  const visible = allEntries.filter((e) => {
    if (processed.has(e.rx.id)) return false
    if (filter === 'expired') return e.daysUntilExpiry !== null && e.daysUntilExpiry < 0
    if (filter === 'expiring') return e.daysUntilExpiry !== null && e.daysUntilExpiry >= 0
    return true
  })

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Prescription renewals</h1>
          <p className="clinician-page-subtitle">
            {state.status === 'ready'
              ? `${allEntries.length - processed.size} prescriptions expiring within 60 days`
              : 'Loading…'}
          </p>
        </div>
      </div>

      <div className="cl-tab-bar">
        {(['expiring', 'expired', 'all'] as const).map((f) => (
          <button
            key={f}
            className={`cl-tab-btn${filter === f ? ' cl-tab-btn-active' : ''}`}
            type="button"
            onClick={() => setFilter(f)}
          >
            {f === 'expiring' ? 'Expiring soon' : f === 'expired' ? 'Expired' : 'All'}
          </button>
        ))}
      </div>

      {state.status === 'loading' && (
        <div className="cl-card">
          <p className="cl-empty-text">Scanning patient prescriptions…</p>
          <div className="skeleton-list" style={{ marginTop: 12 }}>
            {[0, 1, 2, 3, 4].map((i) => <div key={i} className="skeleton-row" style={{ height: 68 }} />)}
          </div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && visible.length === 0 && (
        <div className="cl-card"><p className="cl-empty-text">No prescriptions to show for this filter.</p></div>
      )}

      {state.status === 'ready' && visible.length > 0 && (
        <div className="rx-renew-list">
          {visible.map((entry) => (
            <div key={`${entry.patient.canonicalId}-${entry.rx.id}`} className="rx-renew-item cl-card">
              <div className="rx-renew-left">
                <div className="rx-renew-patient">
                  <Pill size={14} />
                  <span className="rx-renew-patient-name">{entry.patient.displayName}</span>
                  <span className="cl-badge cl-badge-muted">DOB {entry.patient.dateOfBirth}</span>
                </div>
                <p className="rx-renew-drug">{entry.rx.drug}</p>
                <p className="rx-renew-meta">
                  {[entry.rx.dosage, entry.rx.quantity ? `Qty ${entry.rx.quantity}` : null, entry.rx.route].filter(Boolean).join(' · ')}
                  {entry.rx.endDate ? ` · Ends ${entry.rx.endDate}` : ''}
                </p>
              </div>
              <div className="rx-renew-right">
                <span className={`rx-urgency-badge ${urgencyClass(entry.daysUntilExpiry)}`}>
                  {urgencyLabel(entry.daysUntilExpiry)}
                </span>
                {renewingId === entry.rx.id ? (
                  <div className="rx-renew-form">
                    <div className="ne-input-unit" style={{ width: 100 }}>
                      <input
                        className="ne-input"
                        type="number"
                        value={renewDays}
                        onChange={(e) => setRenewDays(e.target.value)}
                        min={1}
                        max={365}
                      />
                      <span>days</span>
                    </div>
                    <button className="cl-btn-primary" type="button" onClick={() => handleRenew(entry)}>
                      <CheckCircle size={13} /> Confirm
                    </button>
                    <button className="cl-btn-secondary" type="button" onClick={() => setRenewingId(null)}>
                      Cancel
                    </button>
                  </div>
                ) : (
                  <div className="rx-renew-actions">
                    <button
                      className="cl-btn-primary"
                      type="button"
                      onClick={() => setRenewingId(entry.rx.id)}
                    >
                      <CheckCircle size={13} /> Renew
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => handleDeactivate(entry)}
                    >
                      <XCircle size={13} /> Discontinue
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
