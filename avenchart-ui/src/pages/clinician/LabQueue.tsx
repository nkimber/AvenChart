import { useEffect, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { CheckCircle } from 'lucide-react'
import {
  getProcedureOrderQueue,
  getProcedureReportQueue,
  signLabReport,
  type ProcedureOrderQueueItem,
  type ProcedureReportQueueItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function abnormalClass(abnormal?: string | null): string {
  if (!abnormal) return ''
  const a = abnormal.toLowerCase()
  if (a.includes('critical') || a === 'panic') return 'lab-flag-critical'
  if (a.includes('high') || a === 'h' || a === 'hh') return 'lab-flag-high'
  if (a.includes('low') || a === 'l' || a === 'll') return 'lab-flag-low'
  return 'lab-flag-abnormal'
}

function abnormalLabel(abnormal?: string | null): string | null {
  if (!abnormal) return null
  const a = abnormal.toLowerCase()
  if (a.includes('critical') || a === 'panic') return 'CRITICAL'
  if (a.includes('high') || a === 'hh') return 'HIGH ↑'
  if (a === 'h') return '↑'
  if (a.includes('low') || a === 'll') return 'LOW ↓'
  if (a === 'l') return '↓'
  return abnormal.toUpperCase()
}

export default function LabQueue() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [tab, setTab] = useState<'reports' | 'orders'>('reports')
  const [reportState, setReportState] = useState<AsyncState<ProcedureReportQueueItem[]>>({ status: 'loading' })
  const [orderState, setOrderState] = useState<AsyncState<ProcedureOrderQueueItem[]>>({ status: 'loading' })
  const [unreviewedCount, setUnreviewedCount] = useState(0)
  const [readyCount, setReadyCount] = useState(0)
  const [reviewing, setReviewing] = useState<Set<number>>(new Set())
  const [reviewed, setReviewed] = useState<Set<number>>(new Set())

  function loadReports() {
    getProcedureReportQueue(session.sessionId, { limit: 100 })
      .then((data) => {
        setReportState({ status: 'ready', data: data.reports })
        setUnreviewedCount(data.unreviewedReports)
      })
      .catch((err) => setReportState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  }

  useEffect(() => {
    loadReports()
    getProcedureOrderQueue(session.sessionId, { limit: 100 })
      .then((data) => {
        setOrderState({ status: 'ready', data: data.reports })
        setReadyCount(data.readyToSendOrders)
      })
      .catch((err) => setOrderState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function handleReview(reportId: number) {
    setReviewing((s) => new Set([...s, reportId]))
    try {
      await signLabReport(session.sessionId, reportId, {
        reviewedBy: session.username,
        reviewedAt: new Date().toISOString(),
      })
      setReviewed((s) => new Set([...s, reportId]))
      setUnreviewedCount((n) => Math.max(0, n - 1))
      showToast('Report marked reviewed.', 'success')
    } catch {
      showToast('Could not mark report reviewed. Please try again.', 'error')
    } finally {
      setReviewing((s) => { const next = new Set(s); next.delete(reportId); return next })
    }
  }

  const reportRows = reportState.status === 'ready'
    ? reportState.data.filter((r) => !reviewed.has(r.reportId))
    : []

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Lab queue</h1>
          <p className="clinician-page-subtitle">
            {unreviewedCount > 0 ? `${unreviewedCount} reports pending review` : 'All reports reviewed'}
            {readyCount > 0 ? ` · ${readyCount} orders ready to send` : ''}
          </p>
        </div>
      </div>

      <div className="cl-tab-bar">
        <button
          className={`cl-tab-btn${tab === 'reports' ? ' cl-tab-btn-active' : ''}`}
          type="button"
          onClick={() => setTab('reports')}
        >
          Report review{unreviewedCount > 0 && <span className="cl-tab-badge">{unreviewedCount}</span>}
        </button>
        <button
          className={`cl-tab-btn${tab === 'orders' ? ' cl-tab-btn-active' : ''}`}
          type="button"
          onClick={() => setTab('orders')}
        >
          Order queue{readyCount > 0 && <span className="cl-tab-badge">{readyCount}</span>}
        </button>
      </div>

      {tab === 'reports' && (
        <>
          {reportState.status === 'loading' && <div className="cl-card"><div className="skeleton-list">{[0,1,2,3].map((i)=><div key={i} className="skeleton-row" style={{height:56}} />)}</div></div>}
          {reportState.status === 'error' && <div className="error-banner">{reportState.message}</div>}
          {reportState.status === 'ready' && reportRows.length === 0 && <div className="cl-card"><p className="cl-empty-text">No reports.</p></div>}
          {reportState.status === 'ready' && reportRows.length > 0 && (
            <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
              <table className="cl-table">
                <thead>
                  <tr>
                    <th>Report date</th>
                    <th>Patient</th>
                    <th>Test</th>
                    <th>Lab</th>
                    <th>Provider</th>
                    <th>Status</th>
                    <th>Review</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {reportRows.map((r) => {
                    const isReviewing = reviewing.has(r.reportId)
                    const isReviewed = !!r.reviewedBy
                    return (
                      <tr key={r.reportId}>
                        <td>{r.reportDate}</td>
                        <td>
                          <button className="cl-table-link" type="button" onClick={() => navigate(`/clinician/patients/${r.patientId}/labs`)}>
                            {r.patientDisplayName}
                          </button>
                          <p className="cl-table-sub">{r.pubpid}</p>
                        </td>
                        <td>
                          <span>{r.procedureName ?? r.procedureCode ?? '—'}</span>
                        </td>
                        <td className="cl-td-muted">{r.labName ?? '—'}</td>
                        <td className="cl-td-muted">{r.providerName ?? '—'}</td>
                        <td>
                          <span className={`cl-badge ${r.reportStatus === 'final' ? 'cl-badge-green' : 'cl-badge-amber'}`}>
                            {r.reportStatus ?? 'pending'}
                          </span>
                        </td>
                        <td>
                          {isReviewed
                            ? <span className="cl-td-muted">{r.reviewedBy}</span>
                            : <span className="cl-badge cl-badge-amber">unreviewed</span>
                          }
                        </td>
                        <td>
                          {!isReviewed && (
                            <button
                              className="cl-btn-icon cl-btn-icon-teal"
                              type="button"
                              aria-label="Mark reviewed"
                              disabled={isReviewing}
                              onClick={() => handleReview(r.reportId)}
                            >
                              <CheckCircle size={15} />
                            </button>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </section>
          )}
        </>
      )}

      {tab === 'orders' && (
        <>
          {orderState.status === 'loading' && <div className="cl-card"><div className="skeleton-list">{[0,1,2,3].map((i)=><div key={i} className="skeleton-row" style={{height:56}} />)}</div></div>}
          {orderState.status === 'error' && <div className="error-banner">{orderState.message}</div>}
          {orderState.status === 'ready' && orderState.data.length === 0 && <div className="cl-card"><p className="cl-empty-text">No orders.</p></div>}
          {orderState.status === 'ready' && orderState.data.length > 0 && (
            <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
              <table className="cl-table">
                <thead>
                  <tr>
                    <th>Order date</th>
                    <th>Patient</th>
                    <th>Test</th>
                    <th>Lab</th>
                    <th>Provider</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {orderState.data.map((o) => (
                    <tr key={o.orderId}>
                      <td>{o.orderDate}</td>
                      <td>
                        <button className="cl-table-link" type="button" onClick={() => navigate(`/clinician/patients/${o.patientId}/labs`)}>
                          {o.patientDisplayName}
                        </button>
                        <p className="cl-table-sub">{o.pubpid}</p>
                      </td>
                      <td>{o.procedureName ?? o.procedureCode ?? '—'}</td>
                      <td className="cl-td-muted">{o.labName ?? '—'}</td>
                      <td className="cl-td-muted">{o.providerName ?? '—'}</td>
                      <td>
                        <span className={`cl-badge ${o.orderStatus === 'completed' ? 'cl-badge-green' : o.orderStatus === 'ready' ? 'cl-badge-amber' : 'cl-badge-muted'}`}>
                          {o.orderStatus ?? 'pending'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}
        </>
      )}
    </div>
  )
}

// Exported so PatientLabs can reuse abnormal flag rendering
export { abnormalClass, abnormalLabel }
