import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { getClinicalLists, type ClinicalListsResponse } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function statusDot(activity: number) {
  return <span className={`cl-activity-dot ${activity === 1 ? 'cl-activity-active' : 'cl-activity-inactive'}`} aria-label={activity === 1 ? 'Active' : 'Inactive'} />
}

export default function PatientChart() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<ClinicalListsResponse>>({ status: 'loading' })

  useEffect(() => {
    setState({ status: 'loading' })
    getClinicalLists(session.sessionId, patientId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Could not load chart.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  if (state.status === 'loading') return (
    <div className="clinician-page">
      <div className="cl-grid-two">
        {[0, 1, 2, 3].map((i) => (
          <section key={i} className="cl-card">
            <div className="skeleton-list">
              {[0, 1, 2].map((j) => <div key={j} className="skeleton-row" />)}
            </div>
          </section>
        ))}
      </div>
    </div>
  )

  if (state.status === 'error') return (
    <div className="clinician-page"><div className="error-banner">{state.message}</div></div>
  )

  const { data } = state

  return (
    <div className="clinician-page">
      <div className="cl-grid-two">
        {/* Problems */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Problems ({data.problems.length})</h2>
          </div>
          {data.problems.length === 0 ? (
            <p className="cl-empty-text">No problems on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.problems.map((p) => (
                <li key={p.id} className="cl-clinical-row">
                  {statusDot(p.activity)}
                  <div>
                    <p className="cl-clinical-title">{p.title}</p>
                    {(p.diagnosis ?? p.date) && (
                      <p className="cl-clinical-meta">
                        {p.diagnosis ?? ''}{p.date ? ` · ${p.date}` : ''}
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Allergies */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Allergies ({data.allergies.length})</h2>
          </div>
          {data.allergies.length === 0 ? (
            <p className="cl-empty-text">No known allergies on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.allergies.map((a) => (
                <li key={a.id} className="cl-clinical-row">
                  {statusDot(a.activity)}
                  <div>
                    <p className="cl-clinical-title">{a.title}</p>
                    {(a.reaction ?? a.severity) && (
                      <p className="cl-clinical-meta">
                        {a.reaction ?? ''}
                        {a.severity ? ` · ${a.severity}` : ''}
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Medications */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Medications ({data.medications.length})</h2>
          </div>
          {data.medications.length === 0 ? (
            <p className="cl-empty-text">No medications on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.medications.map((m) => (
                <li key={m.id} className="cl-clinical-row">
                  {statusDot(m.activity)}
                  <div>
                    <p className="cl-clinical-title">{m.title}</p>
                    {m.date && <p className="cl-clinical-meta">{m.date}</p>}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Prescriptions */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Prescriptions ({data.prescriptions.length})</h2>
          </div>
          {data.prescriptions.length === 0 ? (
            <p className="cl-empty-text">No prescriptions on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.prescriptions.map((rx) => (
                <li key={rx.id} className="cl-clinical-row">
                  {statusDot(rx.active)}
                  <div>
                    <p className="cl-clinical-title">{rx.drug}</p>
                    <p className="cl-clinical-meta">
                      {[rx.dosage, rx.quantity ? `Qty ${rx.quantity}` : null, rx.route].filter(Boolean).join(' · ')}
                      {rx.providerName ? ` · ${rx.providerName}` : ''}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Immunizations */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Immunizations ({data.immunizations.length})</h2>
          </div>
          {data.immunizations.length === 0 ? (
            <p className="cl-empty-text">No immunizations on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.immunizations.map((imm) => (
                <li key={imm.id} className="cl-clinical-row">
                  <div className="cl-activity-dot cl-activity-active" aria-hidden="true" />
                  <div>
                    <p className="cl-clinical-title">{imm.vaccine}</p>
                    <p className="cl-clinical-meta">
                      {imm.administeredAt ?? ''}
                      {imm.manufacturer ? ` · ${imm.manufacturer}` : ''}
                      {imm.lotNumber ? ` · Lot: ${imm.lotNumber}` : ''}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
