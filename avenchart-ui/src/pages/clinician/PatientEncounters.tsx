import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { ChevronRight, FileText, Plus, TrendingUp } from 'lucide-react'
import {
  createEncounterSoapNote,
  createEncounterVitals,
  getEncounterDetail,
  searchEncounters,
  type EncounterDetail,
  type EncounterListItem,
  type EncounterVitals,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
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

const BLANK_VITALS = {
  systolic: '', diastolic: '', pulse: '', temperature: '', respiration: '', oxygenSaturation: '', weight: '', height: '',
}
const BLANK_SOAP = { subjective: '', objective: '', assessment: '', plan: '' }

export default function PatientEncounters() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const navigate = useNavigate()
  const [listState, setListState] = useState<ListState>({ status: 'loading' })
  const [detailState, setDetailState] = useState<DetailState>({ status: 'idle' })
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detailCache, setDetailCache] = useState<Map<number, EncounterDetail>>(new Map())
  const [showTrends, setShowTrends] = useState(false)
  const [addVitalsOpen, setAddVitalsOpen] = useState(false)
  const [addSoapOpen, setAddSoapOpen] = useState(false)
  const [vitalsForm, setVitalsForm] = useState(BLANK_VITALS)
  const [soapForm, setSoapForm] = useState(BLANK_SOAP)
  const [saving, setSaving] = useState(false)

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
    setAddVitalsOpen(false)
    setAddSoapOpen(false)
    setVitalsForm(BLANK_VITALS)
    setSoapForm(BLANK_SOAP)
    setDetailState({ status: 'loading', id })
    getEncounterDetail(session.sessionId, id)
      .then((data) => {
        setDetailState({ status: 'ready', data })
        setDetailCache((prev) => new Map(prev).set(id, data))
      })
      .catch((err) => setDetailState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  async function handleAddVitals(e: React.FormEvent) {
    e.preventDefault()
    if (selectedId == null) return
    setSaving(true)
    try {
      await createEncounterVitals(session.sessionId, selectedId, {
        dateTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
        systolic: vitalsForm.systolic ? Number(vitalsForm.systolic) : undefined,
        diastolic: vitalsForm.diastolic ? Number(vitalsForm.diastolic) : undefined,
        pulse: vitalsForm.pulse ? Number(vitalsForm.pulse) : undefined,
        temperature: vitalsForm.temperature ? Number(vitalsForm.temperature) : undefined,
        respiration: vitalsForm.respiration ? Number(vitalsForm.respiration) : undefined,
        oxygenSaturation: vitalsForm.oxygenSaturation ? Number(vitalsForm.oxygenSaturation) : undefined,
        weight: vitalsForm.weight ? Number(vitalsForm.weight) : undefined,
        height: vitalsForm.height ? Number(vitalsForm.height) : undefined,
      })
      showToast('Vitals recorded.', 'success')
      setAddVitalsOpen(false)
      setVitalsForm(BLANK_VITALS)
      openEncounter(selectedId)
    } catch { showToast('Could not record vitals.', 'error') }
    finally { setSaving(false) }
  }

  async function handleAddSoap(e: React.FormEvent) {
    e.preventDefault()
    if (selectedId == null) return
    setSaving(true)
    try {
      await createEncounterSoapNote(session.sessionId, selectedId, {
        dateTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
        ...soapForm,
      })
      showToast('SOAP note saved.', 'success')
      setAddSoapOpen(false)
      setSoapForm(BLANK_SOAP)
      openEncounter(selectedId)
    } catch { showToast('Could not save SOAP note.', 'error') }
    finally { setSaving(false) }
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

                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">Vitals</h2>
                    <button className="cl-btn-icon" type="button" aria-label="Record vitals" onClick={() => { setAddVitalsOpen((o) => !o); setAddSoapOpen(false) }}>
                      <Plus size={14} />
                    </button>
                  </div>
                  {addVitalsOpen && (
                    <form className="cl-vitals-form" onSubmit={handleAddVitals}>
                      <div className="cl-vitals-input-grid">
                        {[
                          { id: 'v-sys', label: 'Systolic', key: 'systolic' as const, placeholder: '120' },
                          { id: 'v-dia', label: 'Diastolic', key: 'diastolic' as const, placeholder: '80' },
                          { id: 'v-pulse', label: 'Pulse (bpm)', key: 'pulse' as const, placeholder: '72' },
                          { id: 'v-temp', label: 'Temp (°F)', key: 'temperature' as const, placeholder: '98.6' },
                          { id: 'v-resp', label: 'Resp (/min)', key: 'respiration' as const, placeholder: '16' },
                          { id: 'v-o2', label: 'O₂ Sat (%)', key: 'oxygenSaturation' as const, placeholder: '99' },
                          { id: 'v-wt', label: 'Weight (lbs)', key: 'weight' as const, placeholder: '150' },
                          { id: 'v-ht', label: 'Height (in)', key: 'height' as const, placeholder: '68' },
                        ].map(({ id, label, key, placeholder }) => (
                          <div key={key} className="field">
                            <label className="label" htmlFor={id}>{label}</label>
                            <input id={id} type="number" step="0.1" className="input" placeholder={placeholder}
                              value={vitalsForm[key]} onChange={(e) => setVitalsForm((f) => ({ ...f, [key]: e.target.value }))} />
                          </div>
                        ))}
                      </div>
                      <div className="cl-inline-form-actions">
                        <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Record vitals'}</button>
                        <button className="cl-btn-secondary" type="button" onClick={() => setAddVitalsOpen(false)}>Cancel</button>
                      </div>
                    </form>
                  )}
                  {enc.vitals ? (
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
                  ) : !addVitalsOpen && (
                    <p className="cl-empty-text">No vitals recorded. <button className="cl-link" type="button" onClick={() => setAddVitalsOpen(true)}>Add vitals</button></p>
                  )}
                </div>

                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">SOAP note</h2>
                    <button className="cl-btn-icon" type="button" aria-label="Add SOAP note" onClick={() => {
                      setAddSoapOpen((o) => !o); setAddVitalsOpen(false)
                      if (enc.soapNote) setSoapForm({
                        subjective: enc.soapNote.subjective ?? '',
                        objective: enc.soapNote.objective ?? '',
                        assessment: enc.soapNote.assessment ?? '',
                        plan: enc.soapNote.plan ?? '',
                      })
                    }}>
                      <Plus size={14} />
                    </button>
                  </div>
                  {addSoapOpen && (
                    <form onSubmit={handleAddSoap}>
                      {(['subjective', 'objective', 'assessment', 'plan'] as const).map((field) => (
                        <div key={field} className="field" style={{ marginBottom: 10 }}>
                          <label className="label" htmlFor={`soap-${field}`} style={{ textTransform: 'capitalize' }}>{field}</label>
                          <textarea id={`soap-${field}`} className="textarea" rows={3}
                            value={soapForm[field]} onChange={(e) => setSoapForm((f) => ({ ...f, [field]: e.target.value }))} />
                        </div>
                      ))}
                      <div className="cl-inline-form-actions">
                        <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save SOAP note'}</button>
                        <button className="cl-btn-secondary" type="button" onClick={() => setAddSoapOpen(false)}>Cancel</button>
                      </div>
                    </form>
                  )}
                  {enc.soapNote && (enc.soapNote.subjective ?? enc.soapNote.objective ?? enc.soapNote.assessment ?? enc.soapNote.plan) ? (
                    [
                      { label: 'Subjective', text: enc.soapNote.subjective },
                      { label: 'Objective', text: enc.soapNote.objective },
                      { label: 'Assessment', text: enc.soapNote.assessment },
                      { label: 'Plan', text: enc.soapNote.plan },
                    ].filter((s) => s.text).map((s) => (
                      <div key={s.label} className="cl-soap-section">
                        <p className="cl-soap-label">{s.label}</p>
                        <p className="cl-soap-text">{s.text}</p>
                      </div>
                    ))
                  ) : !addSoapOpen && (
                    <p className="cl-empty-text">No SOAP note. <button className="cl-link" type="button" onClick={() => setAddSoapOpen(true)}>Add note</button></p>
                  )}
                </div>

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
