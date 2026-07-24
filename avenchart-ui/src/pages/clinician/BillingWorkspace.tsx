import { useEffect, useState } from 'react'
import { Send, WalletCards } from 'lucide-react'
import { dispatchBillingStatementBatch, getBillingStatementBatch, type StatementBatchResponse } from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'
import { useOutletContext } from 'react-router-dom'

function money(value: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value)
}

export default function BillingWorkspace() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [batch, setBatch] = useState<StatementBatchResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [dispatching, setDispatching] = useState(false)

  function load() {
    setError(null)
    getBillingStatementBatch(session.sessionId, 10)
      .then(setBatch)
      .catch(() => setError('Could not load statement candidates.'))
  }

  useEffect(() => { load() // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function dispatch() {
    if (!batch || dispatching || !window.confirm(`Dispatch ${batch.candidateCount} statement candidates?`)) return
    setDispatching(true)
    try {
      const result = await dispatchBillingStatementBatch(session.sessionId, 10)
      showToast(`${result.dispatchedStatementCount} statements dispatched (${result.emailQueueCount} email, ${result.printQueueCount} print).`, 'success')
      load()
    } catch {
      showToast('Could not dispatch the statement batch.', 'error')
    } finally {
      setDispatching(false)
    }
  }

  return <div className="clinician-page">
    <div className="clinician-page-header">
      <div><h1 className="clinician-page-title">Billing</h1><p className="clinician-page-subtitle">Statement candidates and local dispatch readiness.</p></div>
      <button className="cl-btn-primary" type="button" disabled={!batch || dispatching} onClick={dispatch}><Send size={15} /> {dispatching ? 'Dispatching...' : 'Dispatch statements'}</button>
    </div>
    {error && <div className="error-banner">{error}</div>}
    {!batch && !error && <div className="cl-card"><div className="skeleton-list">{[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 62 }} />)}</div></div>}
    {batch && <>
      <section className="cl-card"><div className="cl-card-header"><h2 className="cl-card-title"><WalletCards size={16} /> Statement batch</h2><span className="cl-badge cl-badge-muted">As of {batch.asOfDate}</span></div><div className="cl-stats-grid"><div className="cl-stat-tile"><span className="cl-stat-tile-value">{batch.candidateCount}</span><span className="cl-stat-tile-label">Candidates</span></div><div className="cl-stat-tile"><span className="cl-stat-tile-value">{money(batch.totalBalanceAmount)}</span><span className="cl-stat-tile-label">Balance due</span></div><div className="cl-stat-tile"><span className="cl-stat-tile-value">{money(batch.totalPastDueAmount)}</span><span className="cl-stat-tile-label">Past due</span></div></div></section>
      <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}><table className="cl-table"><thead><tr><th>Patient</th><th>Statement</th><th>Due</th><th>Balance</th><th>Delivery</th></tr></thead><tbody>{batch.candidates.map((candidate) => <tr key={candidate.patientId}><td>{candidate.patientDisplayName}<p className="cl-table-sub">{candidate.pubpid}</p></td><td>{candidate.statementNumber}<p className="cl-table-sub">{candidate.statementStatus}</p></td><td>{candidate.dueDate}<p className="cl-table-sub">{candidate.oldestOpenAgeDays} days open</p></td><td>{money(candidate.balanceDueAmount)}</td><td className="cl-td-muted">{candidate.deliveryMethod}</td></tr>)}</tbody></table>{batch.candidates.length === 0 && <p className="cl-empty-text">No statement candidates are ready.</p>}</section>
    </>}
  </div>
}
