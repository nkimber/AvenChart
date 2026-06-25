import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { ChevronRight, FileText } from 'lucide-react'
import { getEncounterDetail, searchEncounters, type EncounterDetail, type EncounterListItem } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

type ListState =
  | { status: 'loading' }
  | { status: 'ready'; data: EncounterListItem[] }
  | { status: 'error'; message: string }

type DetailState =
  | { status: 'idle' }
  | { status: 'loading'; id: number }
  | { status: 'ready'; data: EncounterDetail }
  | { status: 'error'; message: string }

function vitalRow(label: string, value?: string | number | null, unit?: string) {
  if (value === null || value === undefined) return null
  return (
    <div className="cl-vital-item">
      <span className="cl-vital-value">{value}{unit ? ` ${unit}` : ''}</span>
      <span className="cl-vital-label">{label}</span>
    </div>
  )
}

export default function PatientEncounters() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [listState, setListState] = useState<ListState>({ status: 'loading' })
  const [detailState, setDetailState] = useState<DetailState>({ status: 'idle' })
  const [selectedId, setSelectedId] = useState<number | null>(null)

  useEffect(() => {
    searchEncounters(session.sessionId, { patientId, limit: 50 })
      .then((data) => setListState({ status: 'ready', data: data.encounters }))
      .catch((err) => setListState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  function openEncounter(id: number) {
    setSelectedId(id)
    setDetailState({ status: 'loading', id })
    getEncounterDetail(session.sessionId, id)
      .then((data) => setDetailState({ status: 'ready', data }))
      .catch((err) => setDetailState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  return (
    <div className="clinician-page">
      <div className="cl-encounter-layout">
        {/* Encounter list */}
        <aside className="cl-encounter-list">
          {listState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 64 }} />)}
            </div>
          )}
          {listState.status === 'error' && <p className="cl-empty-text">{listState.message}</p>}
          {listState.status === 'ready' && listState.data.length === 0 && (
            <p className="cl-empty-text">No encounters on file.</p>
          )}
          {listState.status === 'ready' && listState.data.map((enc) => (
            <button
              key={enc.id}
              className={`cl-encounter-item${selectedId === enc.id ? ' cl-encounter-item-active' : ''}`}
              type="button"
              onClick={() => openEncounter(enc.id)}
            >
              <div className="cl-encounter-item-inner">
                <div>
                  <p className="cl-encounter-date">{enc.date}</p>
                  <p className="cl-encounter-reason">{enc.reason ?? 'Visit'}</p>
                  {enc.diagnosisText && <p className="cl-encounter-dx">{enc.diagnosisText}</p>}
                </div>
                <ChevronRight size={14} />
              </div>
              <div className="cl-encounter-badges">
                {enc.hasSoapNote && <span className="cl-badge cl-badge-teal">SOAP</span>}
                {enc.hasVitals && <span className="cl-badge cl-badge-blue">Vitals</span>}
                {enc.billingLineCount > 0 && <span className="cl-badge cl-badge-muted">{enc.billingLineCount} billing</span>}
              </div>
            </button>
          ))}
        </aside>

        {/* Encounter detail */}
        <section className="cl-encounter-detail">
          {detailState.status === 'idle' && (
            <div className="cl-encounter-empty">
              <FileText size={40} />
              <p>Select an encounter to view details.</p>
            </div>
          )}
          {detailState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 80 }} />)}
            </div>
          )}
          {detailState.status === 'error' && <p className="cl-empty-text">{detailState.message}</p>}
          {detailState.status === 'ready' && (() => {
            const { data: enc } = detailState
            return (
              <>
                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">
                      {enc.date} — {enc.reason ?? 'Visit'}
                    </h2>
                    <span className="cl-badge cl-badge-muted">Enc #{enc.encounter}</span>
                  </div>
                  <ul className="fact-list">
                    {enc.providerName && <li className="fact-row"><span>Provider</span><span>{enc.providerName}</span></li>}
                    {enc.facilityName && <li className="fact-row"><span>Facility</span><span>{enc.facilityName}</span></li>}
                    {enc.diagnosisText && <li className="fact-row"><span>Diagnosis</span><span>{enc.diagnosisCode} — {enc.diagnosisText}</span></li>}
                  </ul>
                </div>

                {enc.vitals && (
                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">Vitals</h2>
                    </div>
                    <div className="cl-vitals-grid">
                      {vitalRow('BP', enc.vitals.bloodPressure ?? (enc.vitals.systolic ? `${enc.vitals.systolic}/${enc.vitals.diastolic}` : null))}
                      {vitalRow('Pulse', enc.vitals.pulse, 'bpm')}
                      {vitalRow('Temp', enc.vitals.temperature, '°F')}
                      {vitalRow('Resp', enc.vitals.respiration, '/min')}
                      {vitalRow('O₂ Sat', enc.vitals.oxygenSaturation, '%')}
                      {vitalRow('Weight', enc.vitals.weight, 'lbs')}
                      {vitalRow('Height', enc.vitals.height, 'in')}
                      {vitalRow('BMI', enc.vitals.bmi)}
                    </div>
                  </div>
                )}

                {enc.soapNote && (enc.soapNote.subjective ?? enc.soapNote.objective ?? enc.soapNote.assessment ?? enc.soapNote.plan) && (
                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">SOAP note</h2>
                    </div>
                    {[
                      { label: 'Subjective', text: enc.soapNote.subjective },
                      { label: 'Objective', text: enc.soapNote.objective },
                      { label: 'Assessment', text: enc.soapNote.assessment },
                      { label: 'Plan', text: enc.soapNote.plan },
                    ].filter((s) => s.text).map((s) => (
                      <div key={s.label} className="cl-soap-section">
                        <p className="cl-soap-label">{s.label}</p>
                        <p className="cl-soap-text">{s.text}</p>
                      </div>
                    ))}
                  </div>
                )}

                {enc.diagnosisCodes.length > 0 && (
                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">Diagnosis codes</h2>
                    </div>
                    <ul className="cl-clinical-list">
                      {enc.diagnosisCodes.map((dx) => (
                        <li key={dx.code} className="cl-clinical-row">
                          <span className="cl-dx-code">{dx.code}</span>
                          <span>{dx.description ?? ''}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )
          })()}
        </section>
      </div>
    </div>
  )
}
