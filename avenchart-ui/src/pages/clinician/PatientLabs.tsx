import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { getProcedureReportQueue, type ProcedureReportQueueItem } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

export default function PatientLabs() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<ProcedureReportQueueItem[]>>({ status: 'loading' })

  useEffect(() => {
    getProcedureReportQueue(session.sessionId, { limit: 50 })
      .then((data) => {
        const filtered = data.reports.filter((r) => r.patientId === patientId)
        setState({ status: 'ready', data: filtered })
      })
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  return (
    <div className="clinician-page">
      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 60 }} />)}
          </div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && state.data.length === 0 && (
        <div className="cl-card"><p className="cl-empty-text">No lab reports on file.</p></div>
      )}
      {state.status === 'ready' && state.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="cl-table">
            <thead>
              <tr>
                <th>Report date</th>
                <th>Test</th>
                <th>Lab</th>
                <th>Provider</th>
                <th>Status</th>
                <th>Reviewed</th>
              </tr>
            </thead>
            <tbody>
              {state.data.map((r) => (
                <tr key={r.reportId}>
                  <td>{r.reportDate}</td>
                  <td>{r.procedureName ?? r.procedureCode ?? '—'}</td>
                  <td className="cl-td-muted">{r.labName ?? '—'}</td>
                  <td className="cl-td-muted">{r.providerName ?? '—'}</td>
                  <td>
                    <span className={`cl-badge ${r.reportStatus === 'final' ? 'cl-badge-green' : 'cl-badge-amber'}`}>
                      {r.reportStatus ?? 'pending'}
                    </span>
                  </td>
                  <td>
                    {r.reviewedBy ? (
                      <span className="cl-td-muted">{r.reviewedBy}</span>
                    ) : (
                      <span className="cl-badge cl-badge-amber">unreviewed</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  )
}
