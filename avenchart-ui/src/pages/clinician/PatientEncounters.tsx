import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { ChevronRight, FileText, Plus, TrendingUp } from 'lucide-react'
import { getEncounterDetail, searchEncounters, type EncounterDetail, type EncounterListItem, type EncounterVitals } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

// Simple SVG sparkline for a series of numeric values
function Sparkline({ values, color = '#0f6e56' }: { values: number[]; color?: string }) {
  if (values.length < 2) return null
  const w = 80, h = 28
  const min = Math.min(...values), max = Math.max(...values)
  const range = max - min || 1
  const pts = values.map((v, i) => {
    const x = (i / (values.length - 1)) * (w - 4) + 2
    const y = h - 2 - ((v - min) / range) * (h - 4)
    return `${x},${y}`
  }).join(' ')
  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} aria-hidden="true" className="vital-sparkline">
      <polyline points={pts} fill="none" stroke={color} strokeWidth="1.5" strokeLinejoin="round" strokeLinecap="round" />
      {values.map((v, i) => {
        const x = (i / (values.length - 1)) * (w - 4) + 2
        const y = h - 2 - ((v - min) / range) * (h - 4)
        return <circle key={i} cx={x} cy={y} r={i === values.length - 1 ? 2.5 : 1.5} fill={color} />
      })}
    </svg>
  )
}

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

function extractVitalSeries(encounters: EncounterListItem[], details: Map<number, EncounterDetail>) {
  const series: { date: string; vitals: EncounterVitals }[] = []
  for (const enc of [...encounters].reverse()) {
    const d = details.get(enc.id)
    if (d?.vitals) series.push({ date: enc.date, vitals: d.vitals })
  }
  return series
}

export default function PatientEncounters() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const navigate = useNavigate()
  const [listState, setListState] = useState<ListState>({ status: 'loading' })
  const [detailState, setDetailState] = useState<DetailState>({ status: 'idle' })
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detailCache, setDetailCache] = useState<Map<number, EncounterDetail>>(new Map())
  const [showTrends, setShowTrends] = useState(false)

  useEffect(() => {
    setDetailCache(new Map())
    searchEncounters(session.sessionId, { patientId, limit: 50 })
      .then((data) => setListState({ status: 'ready', data: data.encounters }))
      .catch((err) => setListState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  const vitalSeries = useMemo(() => {
    if (listState.status !== 'ready') return []
    return extractVitalSeries(listState.data, detailCache)
  }, [listState, detailCache])

  function openEncounter(id: number) {
    setSelectedId(id)
    setDetailState({ status: 'loading', id })
    getEncounterDetail(session.sessionId, id)
      .then((data) => {
        setDetailState({ status: 'ready', data })
        setDetailCache((prev) => new Map(prev).set(id, data))
      })
      .catch((err) => setDetailState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  return (
    <div className="clinician-page">
      {/* Vitals trend panel */}
      {vitalSeries.length >= 2 && (
        <section className="cl-card" style={{ marginBottom: 16 }}>
          <div className="cl-card-header">
            <h2 className="cl-card-title"><TrendingUp size={15} /> Vital trends ({vitalSeries.length} visits)</h2>
            <button className="cl-link" type="button" onClick={() => setShowTrends((s) => !s)}>
              {showTrends ? 'Hide' : 'Show'}
            </button>
          </div>
          {showTrends && (
            <div className="vital-trends-grid">
              {[
                { label: 'Systolic BP', key: 'systolic' as const, color: '#993c1d' },
                { label: 'Diastolic BP', key: 'diastolic' as const, color: '#d97706' },
                { label: 'Pulse', key: 'pulse' as const, color: '#0f6e56' },
                { label: 'Weight (lbs)', key: 'weight' as const, color: '#7c3aed' },
                { label: 'O₂ Sat (%)', key: 'oxygenSaturation' as const, color: '#0891b2' },
                { label: 'Temp (°F)', key: 'temperature' as const, color: '#db2777' },
              ].map(({ label, key, color }) => {
                const vals = vitalSeries
                  .map((s) => s.vitals[key])
                  .filter((v): v is number => v != null)
                if (vals.length < 2) return null
                const latest = vals[vals.length - 1]
                return (
                  <div key={key} className="vital-trend-item">
                    <div className="vital-trend-top">
                      <span className="vital-trend-label">{label}</span>
                      <span className="vital-trend-value">{latest}</span>
                    </div>
                    <Sparkline values={vals} color={color} />
                  </div>
                )
              })}
            </div>
          )}
        </section>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() => navigate(`/clinician/patients/${patientId}/encounters/new`)}
        >
          <Plus size={14} /> New encounter
        </button>
      </div>

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
