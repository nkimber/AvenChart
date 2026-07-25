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
  ['employment_status', 'Employment status'], ['education_level', 'Education level'],
  ['caregiver_status', 'Caregiver status'], ['veteran_status', 'Veteran status'],
] as const

const STATUS_OPTIONS = ['', 'no', 'yes', 'at_risk', 'sometimes', 'often', 'none', 'unknown']

function blankInput(): PatientSdohAssessmentInput {
  return { assessmentDate: new Date().toISOString().slice(0, 10), screeningTool: '', assessor: '', domains: {}, hungerQuestionOne: '', hungerQuestionTwo: '', pregnancyStatus: '', pregnancyEdd: '', pregnancyIntent: '', postpartumStatus: '', postpartumEnd: '', disabilityStatus: '', disabilityStatusNotes: '', disabilityScale: {}, interventions: '' }
}

function toInput(assessment: PatientSdohAssessment): PatientSdohAssessmentInput {
  return {
    assessmentDate: assessment.assessmentDate,
    screeningTool: assessment.screeningTool ?? '',
    assessor: assessment.assessor,
    domains: assessment.domains,
    hungerQuestionOne: assessment.hungerQuestionOne ?? '',
    hungerQuestionTwo: assessment.hungerQuestionTwo ?? '',
    pregnancyStatus: assessment.pregnancyStatus ?? '',
    pregnancyEdd: assessment.pregnancyEdd ?? '',
    pregnancyIntent: assessment.pregnancyIntent ?? '',
    postpartumStatus: assessment.postpartumStatus ?? '',
    postpartumEnd: assessment.postpartumEnd ?? '',
    disabilityStatus: assessment.disabilityStatus ?? '',
    disabilityStatusNotes: assessment.disabilityStatusNotes ?? '',
    disabilityScale: assessment.disabilityScale,
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
  function setDisabilityQuestion(key: string, answer: string) { setForm((current) => ({ ...current, disabilityScale: { ...current.disabilityScale, [key]: answer } })) }

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
      <section className="cl-soap-section"><p className="cl-soap-label">Hunger Vital Signs</p><p className="cl-empty-text">Food insecurity is calculated from these two legacy questions: Often or sometimes true marks the assessment at risk.</p><div className="cl-admin-form-grid"><label className="cl-admin-field"><span>Food worry in the last 12 months</span><select className="ne-input" value={form.hungerQuestionOne ?? ''} onChange={(event) => setForm((current) => ({ ...current, hungerQuestionOne: event.target.value }))}><option value="">Not recorded</option><option value="LA28397-0">Often true</option><option value="LA28398-8">Sometimes true</option><option value="LA6729-3">Never true</option></select></label><label className="cl-admin-field"><span>Food did not last in the last 12 months</span><select className="ne-input" value={form.hungerQuestionTwo ?? ''} onChange={(event) => setForm((current) => ({ ...current, hungerQuestionTwo: event.target.value }))}><option value="">Not recorded</option><option value="LA28397-0">Often true</option><option value="LA28398-8">Sometimes true</option><option value="LA6729-3">Never true</option></select></label></div></section>
      <section className="cl-soap-section"><p className="cl-soap-label">Pregnancy / postpartum status</p><p className="cl-empty-text">Legacy SDOH fields are recorded independently and retain their dates.</p><div className="cl-admin-form-grid"><label className="cl-admin-field"><span>Pregnancy status</span><select className="ne-input" value={form.pregnancyStatus ?? ''} onChange={(event) => setForm((current) => ({ ...current, pregnancyStatus: event.target.value }))}><option value="">Not recorded</option><option value="pregnant">Pregnant</option><option value="not_pregnant">Not pregnant</option><option value="possible">Possible pregnancy</option><option value="unconfirmed">Pregnancy not yet confirmed</option></select></label><label className="cl-admin-field"><span>Estimated due date</span><input className="ne-input" type="date" value={form.pregnancyEdd ?? ''} onChange={(event) => setForm((current) => ({ ...current, pregnancyEdd: event.target.value }))} /></label><label className="cl-admin-field"><span>Pregnancy intention in the next year</span><select className="ne-input" value={form.pregnancyIntent ?? ''} onChange={(event) => setForm((current) => ({ ...current, pregnancyIntent: event.target.value }))}><option value="">Not recorded</option><option value="not_sure">Not sure</option><option value="ambivalent">Ambivalent</option><option value="no_desire">No desire</option><option value="wants_pregnancy">Wants pregnancy</option></select></label><label className="cl-admin-field"><span>Postpartum status</span><select className="ne-input" value={form.postpartumStatus ?? ''} onChange={(event) => setForm((current) => ({ ...current, postpartumStatus: event.target.value }))}><option value="">Not recorded</option><option value="postpartum">Postpartum (≤6 weeks)</option></select></label><label className="cl-admin-field"><span>Postpartum end date</span><input className="ne-input" type="date" value={form.postpartumEnd ?? ''} onChange={(event) => setForm((current) => ({ ...current, postpartumEnd: event.target.value }))} /></label></div></section>
      <section className="cl-soap-section"><p className="cl-soap-label">Disability status (ACS six-item set)</p><div className="cl-admin-form-grid"><label className="cl-admin-field"><span>Overall disability status</span><select className="ne-input" value={form.disabilityStatus ?? ''} onChange={(event) => setForm((current) => ({ ...current, disabilityStatus: event.target.value }))}><option value="">Not recorded</option><option value="im_safe">I'm Safe</option><option value="im_vulnerable">I'm Vulnerable</option><option value="im_at_risk">I'm at risk</option><option value="im_in_crisis">I'm in crisis</option></select></label><label className="cl-admin-field"><span>Additional notes</span><textarea className="ne-input" rows={2} value={form.disabilityStatusNotes ?? ''} onChange={(event) => setForm((current) => ({ ...current, disabilityStatusNotes: event.target.value }))} maxLength={2000} /></label></div><div className="cl-admin-form-grid">{[['walk_climb', 'Serious difficulty walking or climbing stairs'], ['seeing', 'Serious difficulty seeing, even with glasses'], ['hearing', 'Serious difficulty hearing'], ['cognitive', 'Serious difficulty concentrating, remembering, or deciding'], ['dressing_bathing', 'Difficulty dressing or bathing'], ['errands', 'Difficulty doing errands alone']].map(([key, label]) => <label className="cl-admin-field" key={key}><span>{label}</span><select className="ne-input" value={form.disabilityScale?.[key] ?? ''} onChange={(event) => setDisabilityQuestion(key, event.target.value)}><option value="">Not recorded</option><option value="yes">Yes</option><option value="no">No</option><option value="declined">Declined</option></select></label>)}</div></section>
      <div className="cl-grid-two">{DOMAINS.map(([key, label]) => <section className="cl-soap-section" key={key}><p className="cl-soap-label">{label}</p><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={form.domains[key]?.status ?? ''} onChange={(event) => setDomain(key, { status: event.target.value })}>{STATUS_OPTIONS.map((status) => <option key={status} value={status}>{statusLabel(status)}</option>)}</select></label><label className="cl-admin-field"><span>Notes</span><textarea className="ne-input" rows={2} value={form.domains[key]?.notes ?? ''} onChange={(event) => setDomain(key, { notes: event.target.value })} maxLength={2000} /></label></section>)}</div>
      <label className="cl-admin-field"><span>Additional interventions</span><textarea className="ne-input" rows={3} value={form.interventions ?? ''} onChange={(event) => setForm((current) => ({ ...current, interventions: event.target.value }))} maxLength={4000} /></label>
      <div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save assessment'}</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => setEditing(null)}>Cancel</button></div>
    </form>}
    {loading ? <div className="cl-card"><p className="cl-empty-text">Loading assessments…</p></div> : assessments.length === 0 ? <div className="cl-card"><p className="cl-empty-text">No SDOH assessments have been recorded.</p></div> : <div className="cl-clinical-list">{assessments.map((assessment) => <article className="cl-card" key={assessment.assessmentId}><div className="cl-card-header"><div><h2 className="cl-card-title"><ClipboardList size={15} /> {assessment.assessmentDate}</h2><p className="cl-empty-text">{assessment.screeningTool ?? 'No screening tool recorded'} · Assessor: {assessment.assessor} · {assessment.instrumentScore} positive domain{assessment.instrumentScore === 1 ? '' : 's'} · Hunger score: {assessment.hungerScore}</p></div><button className="cl-btn-secondary" type="button" onClick={() => beginEdit(assessment)}>Edit</button></div><div className="cl-grid-two">{DOMAINS.filter(([key]) => assessment.domains[key]?.status || assessment.domains[key]?.notes).map(([key, label]) => <div className="fact-row" key={key}><span>{label}</span><span>{statusLabel(assessment.domains[key]?.status ?? '')}{assessment.domains[key]?.notes ? ` · ${assessment.domains[key].notes}` : ''}</span></div>)}{(assessment.pregnancyStatus || assessment.pregnancyEdd || assessment.pregnancyIntent || assessment.postpartumStatus || assessment.postpartumEnd) && <div className="fact-row"><span>Pregnancy / postpartum</span><span>{[assessment.pregnancyStatus && `Pregnancy: ${statusLabel(assessment.pregnancyStatus)}`, assessment.pregnancyEdd && `Due: ${assessment.pregnancyEdd}`, assessment.pregnancyIntent && `Intent: ${statusLabel(assessment.pregnancyIntent)}`, assessment.postpartumStatus && statusLabel(assessment.postpartumStatus), assessment.postpartumEnd && `Postpartum end: ${assessment.postpartumEnd}`].filter(Boolean).join(' · ')}</span></div>}</div>{assessment.interventions && <p className="cl-empty-text" style={{ marginTop: 12 }}>Interventions: {assessment.interventions}</p>}<p className="cl-empty-text" style={{ marginTop: 10 }}>Updated by {assessment.updatedBy} on {new Date(assessment.updatedAt).toLocaleString()}.</p></article>)}</div>}
  </div>
}
