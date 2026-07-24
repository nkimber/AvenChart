import { useEffect, useState } from 'react'
import { Send, WalletCards } from 'lucide-react'
import { createBillingCollectionsFollowUp, dispatchBillingStatementBatch, getBillingCollectionsWorkQueue, getBillingStatementBatch, type CollectionsWorkQueueResponse, type StatementBatchResponse } from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'
import { useOutletContext } from 'react-router-dom'

function money(value: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value)
}

export default function BillingWorkspace() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [batch, setBatch] = useState<StatementBatchResponse | null>(null)
  const [collections, setCollections] = useState<CollectionsWorkQueueResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [dispatching, setDispatching] = useState(false)
  const [followUpPatientId, setFollowUpPatientId] = useState<string | null>(null)
  const [followUpNote, setFollowUpNote] = useState('')
  const [savingFollowUp, setSavingFollowUp] = useState(false)

  function load() {
    setError(null)
    getBillingStatementBatch(session.sessionId, 10)
      .then(setBatch)
      .catch(() => setError('Could not load statement candidates.'))
    getBillingCollectionsWorkQueue(session.sessionId, 10).then(setCollections).catch(() => {})
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

  async function createFollowUp() {
    if (!followUpPatientId || savingFollowUp || !followUpNote.trim() || !window.confirm('Create this local collections follow-up?')) return
    setSavingFollowUp(true)
    try {
      await createBillingCollectionsFollowUp(session.sessionId, { patientId: followUpPatientId, assignedTo: session.username, action: 'follow-up', note: followUpNote.trim() })
      showToast('Collections follow-up created.', 'success')
      setFollowUpPatientId(null); setFollowUpNote(''); load()
    } catch { showToast('Could not create the collections follow-up.', 'error') } finally { setSavingFollowUp(false) }
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
      {collections && <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}><div className="cl-card-header" style={{ padding: '16px 20px 12px' }}><h2 className="cl-card-title">Collections queue</h2><span className="cl-badge cl-badge-muted">{collections.highPriorityCount} high priority</span></div><table className="cl-table"><thead><tr><th>Patient</th><th>Tier</th><th>Recommended action</th><th>Past due</th><th>Over 90</th><th></th></tr></thead><tbody>{collections.items.map((item) => <tr key={item.patientId}><td>{item.patientDisplayName}<p className="cl-table-sub">{item.pubpid}</p></td><td>{item.collectionTier}</td><td className="cl-td-muted">{item.recommendedAction}</td><td>{money(item.pastDueAmount)}</td><td>{money(item.over90Amount)}</td><td><button className="cl-btn-secondary" type="button" onClick={() => { setFollowUpPatientId(item.patientId); setFollowUpNote(item.recommendedAction) }}>Follow up</button></td></tr>)}</tbody></table>{followUpPatientId && <div className="cl-inline-form" style={{ margin: 16 }}><label className="cl-admin-field"><span>Follow-up note</span><textarea className="ne-input" value={followUpNote} onChange={(event) => setFollowUpNote(event.target.value)} required /></label><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="button" disabled={savingFollowUp || !followUpNote.trim()} onClick={createFollowUp}>{savingFollowUp ? 'Saving...' : 'Create follow-up'}</button><button className="cl-btn-secondary" type="button" onClick={() => setFollowUpPatientId(null)}>Cancel</button></div></div>}{collections.items.length === 0 && <p className="cl-empty-text">No accounts need collections follow-up.</p>}</section>}
    </>}
  </div>
}
