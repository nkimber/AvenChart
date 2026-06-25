import { useEffect, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { getProcedureOrderQueue, getProcedureReportQueue, type ProcedureOrderQueueItem, type ProcedureReportQueueItem } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

export default function LabQueue() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [tab, setTab] = useState<'reports' | 'orders'>('reports')
  const [reportState, setReportState] = useState<AsyncState<ProcedureReportQueueItem[]>>({ status: 'loading' })
  const [orderState, setOrderState] = useState<AsyncState<ProcedureOrderQueueItem[]>>({ status: 'loading' })
  const [unreviewedCount, setUnreviewedCount] = useState(0)
  const [readyCount, setReadyCount] = useState(0)

  useEffect(() => {
    getProcedureReportQueue(session.sessionId, { limit: 100 })
      .then((data) => {
        setReportState({ status: 'ready', data: data.reports })
        setUnreviewedCount(data.unreviewedReports)
      })
      .catch((err) => setReportState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))

    getProcedureOrderQueue(session.sessionId, { limit: 100 })
      .then((data) => {
        setOrderState({ status: 'ready', data: data.reports })
        setReadyCount(data.readyToSendOrders)
      })
      .catch((err) => setOrderState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

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
          {reportState.status === 'ready' && reportState.data.length === 0 && <div className="cl-card"><p className="cl-empty-text">No reports.</p></div>}
          {reportState.status === 'ready' && reportState.data.length > 0 && (
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
                  </tr>
                </thead>
                <tbody>
                  {reportState.data.map((r) => (
                    <tr key={r.reportId}>
                      <td>{r.reportDate}</td>
                      <td>
                        <button className="cl-table-link" type="button" onClick={() => navigate(`/clinician/patients/${r.patientId}/labs`)}>
                          {r.patientDisplayName}
                        </button>
                        <p className="cl-table-sub">{r.pubpid}</p>
                      </td>
                      <td>{r.procedureName ?? r.procedureCode ?? '—'}</td>
                      <td className="cl-td-muted">{r.labName ?? '—'}</td>
                      <td className="cl-td-muted">{r.providerName ?? '—'}</td>
                      <td>
                        <span className={`cl-badge ${r.reportStatus === 'final' ? 'cl-badge-green' : 'cl-badge-amber'}`}>
                          {r.reportStatus ?? 'pending'}
                        </span>
                      </td>
                      <td>
                        {r.reviewedBy
                          ? <span className="cl-td-muted">{r.reviewedBy}</span>
                          : <span className="cl-badge cl-badge-amber">unreviewed</span>
                        }
                      </td>
                    </tr>
                  ))}
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
