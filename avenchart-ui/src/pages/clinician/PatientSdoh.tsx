import { useEffect, useState } from 'react'
import { ClipboardList, Plus } from 'lucide-react'
import {
  createPatientSdohAssessment,
  getPatientSdohAssessments,
  updatePatientSdohAssessment,
  type PatientSdohAssessment,
  type PatientSdohAssessmentInput,
  type PatientSdohDomainValue,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'
import { useOutletContext } from 'react-router-dom'

const DOMAINS = [
  ['food_insecurity', 'Food insecurity'], ['housing_instability', 'Housing instability'],
  ['transportation_insecurity', 'Transportation insecurity'], ['utilities_insecurity', 'Utilities insecurity'],
  ['interpersonal_safety', 'Interpersonal safety'], ['financial_strain', 'Financial strain'],
  ['social_isolation', 'Social isolation'], ['childcare_needs', 'Childcare needs'], ['digital_access', 'Digital access'],
  ['disability_status', 'Disability status'], ['employment_status', 'Employment status'], ['education_level', 'Education level'],
  ['caregiver_status', 'Caregiver status'], ['veteran_status', 'Veteran status'], ['pregnancy_status', 'Pregnancy status'], ['postpartum_status', 'Postpartum status'],
] as const

const STATUS_OPTIONS = ['', 'no', 'yes', 'at_risk', 'sometimes', 'often', 'none', 'unknown']

function blankInput(): PatientSdohAssessmentInput {
  return { assessmentDate: new Date().toISOString().slice(0, 10), screeningTool: '', assessor: '', domains: {}, interventions: '' }
}

function toInput(assessment: PatientSdohAssessment): PatientSdohAssessmentInput {
  return {
    assessmentDate: assessment.assessmentDate,
    screeningTool: assessment.screeningTool ?? '',
    assessor: assessment.assessor,
    domains: assessment.domains,
    interventions: assessment.interventions ?? '',
  }
}

function statusLabel(status: string) {
  return status ? status.replaceAll('_', ' ') : 'Not recorded'
}

export default function PatientSdoh() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [assessments, setAssessments] = useState<PatientSdohAssessment[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<PatientSdohAssessment | 'new' | null>(null)
  const [form, setForm] = useState<PatientSdohAssessmentInput>(blankInput)
  const [saving, setSaving] = useState(false)

  async function load() {
    setLoading(true)
    try { setAssessments(await getPatientSdohAssessments(session.sessionId, patientId)) }
    catch { showToast('Could not load SDOH assessments.', 'error') }
    finally { setLoading(false) }
  }

  useEffect(() => { void load() }, [session.sessionId, patientId])

  function beginNew() { setForm(blankInput()); setEditing('new') }
  function beginEdit(assessment: PatientSdohAssessment) { setForm(toInput(assessment)); setEditing(assessment) }
  function setDomain(key: string, patch: Partial<PatientSdohDomainValue>) {
    setForm((current) => ({ ...current, domains: { ...current.domains, [key]: { status: current.domains[key]?.status ?? '', notes: current.domains[key]?.notes ?? '', ...patch } } }))
  }

  async function save(event: React.FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      if (editing === 'new') await createPatientSdohAssessment(session.sessionId, patientId, form)
      else if (editing) await updatePatientSdohAssessment(session.sessionId, patientId, editing.assessmentId, form)
      showToast(editing === 'new' ? 'SDOH assessment recorded.' : 'SDOH assessment updated.', 'success')
      setEditing(null)
      await load()
    } catch { showToast('Could not save the SDOH assessment.', 'error') }
    finally { setSaving(false) }
  }

  return <div className="clinician-page">
    <div className="clinician-page-header"><div><h1 className="clinician-page-title">Social determinants of health</h1><p className="clinician-page-subtitle">Dated SDOH assessments preserve screening evidence, domain status, and notes.</p></div>{editing === null && <button className="cl-btn-primary" type="button" onClick={beginNew}><Plus size={15} /> New assessment</button>}</div>
    {editing !== null && <form className="cl-card" onSubmit={save}>
      <div className="cl-card-header"><div><h2 className="cl-card-title">{editing === 'new' ? 'New SDOH assessment' : 'Edit SDOH assessment'}</h2><p className="cl-empty-text">Legacy-compatible domains are saved together under the assessment date.</p></div></div>
      <div className="cl-admin-form-grid"><label className="cl-admin-field"><span>Assessment date <em>*</em></span><input className="ne-input" type="date" value={form.assessmentDate} onChange={(event) => setForm((current) => ({ ...current, assessmentDate: event.target.value }))} required /></label><label className="cl-admin-field"><span>Screening tool</span><input className="ne-input" value={form.screeningTool ?? ''} onChange={(event) => setForm((current) => ({ ...current, screeningTool: event.target.value }))} placeholder="e.g. Hunger Vital Signs" maxLength={120} /></label><label className="cl-admin-field"><span>Assessor</span><input className="ne-input" value={form.assessor ?? ''} onChange={(event) => setForm((current) => ({ ...current, assessor: event.target.value }))} placeholder="Defaults to signed-in user" maxLength={120} /></label></div>
      <div className="cl-grid-two">{DOMAINS.map(([key, label]) => <section className="cl-soap-section" key={key}><p className="cl-soap-label">{label}</p><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={form.domains[key]?.status ?? ''} onChange={(event) => setDomain(key, { status: event.target.value })}>{STATUS_OPTIONS.map((status) => <option key={status} value={status}>{statusLabel(status)}</option>)}</select></label><label className="cl-admin-field"><span>Notes</span><textarea className="ne-input" rows={2} value={form.domains[key]?.notes ?? ''} onChange={(event) => setDomain(key, { notes: event.target.value })} maxLength={2000} /></label></section>)}</div>
      <label className="cl-admin-field"><span>Additional interventions</span><textarea className="ne-input" rows={3} value={form.interventions ?? ''} onChange={(event) => setForm((current) => ({ ...current, interventions: event.target.value }))} maxLength={4000} /></label>
      <div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save assessment'}</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => setEditing(null)}>Cancel</button></div>
    </form>}
    {loading ? <div className="cl-card"><p className="cl-empty-text">Loading assessments…</p></div> : assessments.length === 0 ? <div className="cl-card"><p className="cl-empty-text">No SDOH assessments have been recorded.</p></div> : <div className="cl-clinical-list">{assessments.map((assessment) => <article className="cl-card" key={assessment.assessmentId}><div className="cl-card-header"><div><h2 className="cl-card-title"><ClipboardList size={15} /> {assessment.assessmentDate}</h2><p className="cl-empty-text">{assessment.screeningTool ?? 'No screening tool recorded'} · Assessor: {assessment.assessor} · {assessment.instrumentScore} positive domain{assessment.instrumentScore === 1 ? '' : 's'}</p></div><button className="cl-btn-secondary" type="button" onClick={() => beginEdit(assessment)}>Edit</button></div><div className="cl-grid-two">{DOMAINS.filter(([key]) => assessment.domains[key]?.status || assessment.domains[key]?.notes).map(([key, label]) => <div className="fact-row" key={key}><span>{label}</span><span>{statusLabel(assessment.domains[key]?.status ?? '')}{assessment.domains[key]?.notes ? ` · ${assessment.domains[key].notes}` : ''}</span></div>)}</div>{assessment.interventions && <p className="cl-empty-text" style={{ marginTop: 12 }}>Interventions: {assessment.interventions}</p>}<p className="cl-empty-text" style={{ marginTop: 10 }}>Updated by {assessment.updatedBy} on {new Date(assessment.updatedAt).toLocaleString()}.</p></article>)}</div>}
  </div>
}
