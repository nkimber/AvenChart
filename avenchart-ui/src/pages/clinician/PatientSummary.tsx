import { useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { CalendarClock, FileText, Phone, Plus, Printer, Shield, Trash2 } from 'lucide-react'
import {
  updatePatientContact,
  updatePatientDemographics,
  createPatientInsurance,
  updatePatientInsurance,
  deletePatientInsurance,
  type PatientInsuranceMutationInput,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

function fact(label: string, value?: string | null) {
  if (!value) return null
  return (
    <li className="fact-row">
      <span>{label}</span>
      <span>{value}</span>
    </li>
  )
}

const BLANK_INS: PatientInsuranceMutationInput = {
  type: 'primary', provider: '', planName: '', policyNumber: '', groupNumber: '',
  relationship: 'self', subscriberFirstName: '', subscriberLastName: '',
  subscriberDateOfBirth: '', subscriberSex: 'unknown',
}

type InsuranceMode = { kind: 'none' } | { kind: 'add' } | { kind: 'edit'; insuranceId: string }

export default function PatientSummary() {
  const { session, patient, patientId, reload } = useOutletContext<PatientOutletContext>()
  const navigate = useNavigate()

  const [editDemoOpen, setEditDemoOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [contactForm, setContactForm] = useState({
    phoneHome: patient.phone ?? '',
    phoneCell: patient.phoneCell ?? '',
    email: patient.email ?? '',
    hipaaAllowSms: patient.hipaaAllowSms ?? 'NO',
    hipaaAllowEmail: patient.hipaaAllowEmail ?? 'NO',
  })
  const [demoForm, setDemoForm] = useState({
    firstName: patient.firstName ?? '', lastName: patient.lastName ?? '',
    preferredName: '', sex: patient.sex ?? '', dateOfBirth: patient.dateOfBirth ?? '',
    street: patient.street ?? '', city: patient.city ?? '', state: patient.state ?? '',
    postalCode: patient.postalCode ?? '', maritalStatus: patient.maritalStatus ?? '',
    occupation: patient.occupation ?? '', race: patient.race ?? '',
    ethnicity: patient.ethnicity ?? '', interpreter: patient.interpreter ?? '',
    familySize: patient.familySize ?? '', monthlyIncome: patient.monthlyIncome ?? '',
    homeless: patient.homeless ?? 'NO', financialReviewDate: patient.financialReviewDate ?? '',
  })
  const [insMode, setInsMode] = useState<InsuranceMode>({ kind: 'none' })
  const [insForm, setInsForm] = useState<PatientInsuranceMutationInput>(BLANK_INS)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  function openAddInsurance() { setInsForm({ ...BLANK_INS }); setInsMode({ kind: 'add' }) }

  function openEditInsurance(id: string) {
    const ins = patient.insurance.find((i) => i.id === id)
    if (!ins) return
    setInsForm({
      type: ins.type ?? 'primary', provider: ins.provider ?? '', planName: ins.planName ?? '',
      policyNumber: ins.policyNumber ?? '', groupNumber: ins.groupNumber ?? '',
      relationship: ins.relationship ?? 'self', subscriberFirstName: '',
      subscriberLastName: '', subscriberDateOfBirth: '', subscriberSex: 'unknown',
    })
    setInsMode({ kind: 'edit', insuranceId: id })
  }

  async function handleSaveDemographics(e: React.FormEvent) {
    e.preventDefault(); setSaving(true)
    try {
      await updatePatientContact(session.sessionId, patientId, contactForm)
      await updatePatientDemographics(session.sessionId, patientId, demoForm)
      showToast('Demographics saved.', 'success')
      setEditDemoOpen(false); reload()
    } catch { showToast('Could not save demographics.', 'error') }
    finally { setSaving(false) }
  }

  async function handleSaveInsurance(e: React.FormEvent) {
    e.preventDefault(); setSaving(true)
    try {
      if (insMode.kind === 'add') {
        await createPatientInsurance(session.sessionId, patientId, insForm)
        showToast('Insurance added.', 'success')
      } else if (insMode.kind === 'edit') {
        await updatePatientInsurance(session.sessionId, insMode.insuranceId, insForm)
        showToast('Insurance updated.', 'success')
      }
      setInsMode({ kind: 'none' }); reload()
    } catch { showToast('Could not save insurance.', 'error') }
    finally { setSaving(false) }
  }

  async function handleDeleteInsurance(id: string) {
    if (!confirm('Remove this insurance record?')) return
    setDeletingId(id)
    try {
      await deletePatientInsurance(session.sessionId, id)
      showToast('Insurance removed.', 'success'); reload()
    } catch { showToast('Could not remove insurance.', 'error') }
    finally { setDeletingId(null) }
  }

  const setIns = (patch: Partial<PatientInsuranceMutationInput>) => setInsForm((f) => ({ ...f, ...patch }))

  return (
    <div className="clinician-page">
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="cl-btn-secondary" type="button" onClick={() => window.print()}>
          <Printer size={14} /> Print summary
        </button>
      </div>

      {/* Insurance modal */}
      {insMode.kind !== 'none' && (
        <div className="modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) setInsMode({ kind: 'none' }) }}>
          <div className="modal-panel" role="dialog" aria-modal="true">
            <div className="modal-header">
              <h2 className="modal-title">{insMode.kind === 'add' ? 'Add insurance' : 'Edit insurance'}</h2>
              <button className="modal-close" type="button" onClick={() => setInsMode({ kind: 'none' })} aria-label="Close">×</button>
            </div>
            <form onSubmit={handleSaveInsurance}>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-type">Coverage type</label>
                  <select id="ins-type" className="select" value={insForm.type} onChange={(e) => setIns({ type: e.target.value })}>
                    <option value="primary">Primary</option>
                    <option value="secondary">Secondary</option>
                    <option value="tertiary">Tertiary</option>
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-rel">Relationship</label>
                  <select id="ins-rel" className="select" value={insForm.relationship} onChange={(e) => setIns({ relationship: e.target.value })}>
                    <option value="self">Self</option>
                    <option value="spouse">Spouse</option>
                    <option value="child">Child</option>
                    <option value="other">Other</option>
                  </select>
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-provider">Insurance company</label>
                  <input id="ins-provider" className="input" value={insForm.provider} onChange={(e) => setIns({ provider: e.target.value })} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-plan">Plan name</label>
                  <input id="ins-plan" className="input" value={insForm.planName} onChange={(e) => setIns({ planName: e.target.value })} />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-policy">Policy number</label>
                  <input id="ins-policy" className="input" value={insForm.policyNumber} onChange={(e) => setIns({ policyNumber: e.target.value })} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-group">Group number</label>
                  <input id="ins-group" className="input" value={insForm.groupNumber} onChange={(e) => setIns({ groupNumber: e.target.value })} />
                </div>
              </div>
              <p className="cl-form-section-label">Subscriber</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-sub-first">First name</label>
                  <input id="ins-sub-first" className="input" value={insForm.subscriberFirstName} onChange={(e) => setIns({ subscriberFirstName: e.target.value })} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-sub-last">Last name</label>
                  <input id="ins-sub-last" className="input" value={insForm.subscriberLastName} onChange={(e) => setIns({ subscriberLastName: e.target.value })} />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-sub-dob">Date of birth</label>
                  <input id="ins-sub-dob" type="date" className="input" value={insForm.subscriberDateOfBirth} onChange={(e) => setIns({ subscriberDateOfBirth: e.target.value })} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-sub-sex">Sex</label>
                  <select id="ins-sub-sex" className="select" value={insForm.subscriberSex} onChange={(e) => setIns({ subscriberSex: e.target.value })}>
                    <option value="unknown">Unknown</option>
                    <option value="male">Male</option>
                    <option value="female">Female</option>
                  </select>
                </div>
              </div>
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={saving}>
                  {saving ? 'Saving…' : insMode.kind === 'add' ? 'Add insurance' : 'Save changes'}
                </button>
                <button className="button-secondary" type="button" onClick={() => setInsMode({ kind: 'none' })} style={{ flex: 'none', width: 'auto' }}>
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="cl-grid-two print-summary">
        {/* Demographics */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><Phone size={15} /> Contact & demographics</h2>
            <button className="cl-link" type="button" onClick={() => setEditDemoOpen((o) => !o)}>
              {editDemoOpen ? 'Cancel' : 'Edit'}
            </button>
          </div>
          {editDemoOpen ? (
            <form onSubmit={handleSaveDemographics}>
              <p className="cl-form-section-label">Name</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-first">First name</label>
                  <input id="demo-first" className="input" value={demoForm.firstName} onChange={(e) => setDemoForm((f) => ({ ...f, firstName: e.target.value }))} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-last">Last name</label>
                  <input id="demo-last" className="input" value={demoForm.lastName} onChange={(e) => setDemoForm((f) => ({ ...f, lastName: e.target.value }))} required />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-pref">Preferred name</label>
                  <input id="demo-pref" className="input" value={demoForm.preferredName} onChange={(e) => setDemoForm((f) => ({ ...f, preferredName: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-sex">Sex</label>
                  <select id="demo-sex" className="select" value={demoForm.sex} onChange={(e) => setDemoForm((f) => ({ ...f, sex: e.target.value }))}>
                    <option value="">Select</option>
                    <option value="Male">Male</option>
                    <option value="Female">Female</option>
                    <option value="Unknown">Unknown</option>
                  </select>
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="demo-dob">Date of birth</label>
                <input id="demo-dob" type="date" className="input" value={demoForm.dateOfBirth} onChange={(e) => setDemoForm((f) => ({ ...f, dateOfBirth: e.target.value }))} required />
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>Contact</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-phone">Home phone</label>
                  <input id="demo-phone" type="tel" className="input" value={contactForm.phoneHome} onChange={(e) => setContactForm((f) => ({ ...f, phoneHome: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-cell">Cell phone</label>
                  <input id="demo-cell" type="tel" className="input" value={contactForm.phoneCell} onChange={(e) => setContactForm((f) => ({ ...f, phoneCell: e.target.value }))} />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="demo-email">Email</label>
                <input id="demo-email" type="email" className="input" value={contactForm.email} onChange={(e) => setContactForm((f) => ({ ...f, email: e.target.value }))} />
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>Address</p>
              <div className="field">
                <label className="label" htmlFor="demo-street">Street</label>
                <input id="demo-street" className="input" value={demoForm.street} onChange={(e) => setDemoForm((f) => ({ ...f, street: e.target.value }))} />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-city">City</label>
                  <input id="demo-city" className="input" value={demoForm.city} onChange={(e) => setDemoForm((f) => ({ ...f, city: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-state">State</label>
                  <input id="demo-state" className="input" maxLength={2} value={demoForm.state} onChange={(e) => setDemoForm((f) => ({ ...f, state: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-zip">ZIP</label>
                  <input id="demo-zip" className="input" value={demoForm.postalCode} onChange={(e) => setDemoForm((f) => ({ ...f, postalCode: e.target.value }))} />
                </div>
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>Additional</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-marital">Marital status</label>
                  <select id="demo-marital" className="select" value={demoForm.maritalStatus} onChange={(e) => setDemoForm((f) => ({ ...f, maritalStatus: e.target.value }))}>
                    <option value="">Select</option>
                    <option value="Single">Single</option>
                    <option value="Married">Married</option>
                    <option value="Divorced">Divorced</option>
                    <option value="Widowed">Widowed</option>
                    <option value="Separated">Separated</option>
                    <option value="Partner">Partner</option>
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-race">Race</label>
                  <input id="demo-race" className="input" value={demoForm.race} onChange={(e) => setDemoForm((f) => ({ ...f, race: e.target.value }))} />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-ethnicity">Ethnicity</label>
                  <input id="demo-ethnicity" className="input" value={demoForm.ethnicity} onChange={(e) => setDemoForm((f) => ({ ...f, ethnicity: e.target.value }))} />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-occupation">Occupation</label>
                  <input id="demo-occupation" className="input" value={demoForm.occupation} onChange={(e) => setDemoForm((f) => ({ ...f, occupation: e.target.value }))} />
                </div>
              </div>
              <div className="cl-inline-form-actions" style={{ marginTop: 16 }}>
                <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save changes'}</button>
                <button className="cl-btn-secondary" type="button" onClick={() => setEditDemoOpen(false)}>Cancel</button>
              </div>
            </form>
          ) : (
            <ul className="fact-list">
              {fact('Date of birth', patient.dateOfBirth)}
              {fact('Age', `${patient.age}y`)}
              {fact('Sex', patient.sex)}
              {fact('Phone', patient.phone ?? patient.phoneCell)}
              {fact('Email', patient.email)}
              {fact('Address', [patient.street, patient.city, patient.state, patient.postalCode].filter(Boolean).join(', '))}
              {fact('Marital status', patient.maritalStatus)}
              {fact('Race / Ethnicity', [patient.race, patient.ethnicity].filter(Boolean).join(' / '))}
              {fact('Occupation', patient.occupation)}
              {fact('Primary provider', patient.primaryProviderName)}
              {fact('Facility', patient.facilityName)}
              {fact('Patient since', patient.registrationDate)}
              {patient.deceasedDate && fact('Deceased', patient.deceasedDate)}
            </ul>
          )}
        </section>

        {/* Insurance */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><Shield size={15} /> Insurance</h2>
            <button className="cl-btn-icon" type="button" aria-label="Add insurance" onClick={openAddInsurance}>
              <Plus size={15} />
            </button>
          </div>
          {patient.insurance.length === 0 ? (
            <p className="cl-empty-text">No insurance on file. <button className="cl-link" type="button" onClick={openAddInsurance}>Add insurance</button></p>
          ) : (
            <ul className="fact-list">
              {patient.insurance.map((ins) => (
                <li key={ins.id} className="cl-insurance-item cl-insurance-item-actions">
                  <div>
                    <p className="cl-insurance-type">{ins.type ?? 'Primary'}</p>
                    <p className="cl-insurance-plan">{ins.provider ?? '—'}{ins.planName ? ` · ${ins.planName}` : ''}</p>
                    {ins.policyNumber && <p className="cl-insurance-meta">Policy: {ins.policyNumber}{ins.groupNumber ? ` · Group: ${ins.groupNumber}` : ''}</p>}
                    {ins.relationship && <p className="cl-insurance-meta">Relationship: {ins.relationship}</p>}
                  </div>
                  <div className="cl-insurance-btns">
                    <button className="cl-link" type="button" onClick={() => openEditInsurance(ins.id)}>Edit</button>
                    <button className="cl-clinical-action" type="button" aria-label="Remove insurance" disabled={deletingId === ins.id} onClick={() => handleDeleteInsurance(ins.id)}>
                      <Trash2 size={13} />
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Timeline */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><CalendarClock size={15} /> Next appointment</h2>
            <button className="cl-link" type="button" onClick={() => navigate(`/clinician/patients/${patientId}/appointments`)}>
              All appointments
            </button>
          </div>
          {!patient.nextAppointment ? (
            <p className="cl-empty-text">No upcoming appointments.</p>
          ) : (
            <div className="cl-timeline-item">
              <p className="cl-timeline-title">{patient.nextAppointment.title}</p>
              <p className="cl-timeline-meta">
                {patient.nextAppointment.date}
                {patient.nextAppointment.time ? ` at ${patient.nextAppointment.time.slice(0, 5)}` : ''}
                {patient.nextAppointment.providerName ? ` · ${patient.nextAppointment.providerName}` : ''}
              </p>
            </div>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><FileText size={15} /> Latest encounter</h2>
            <button className="cl-link" type="button" onClick={() => navigate(`/clinician/patients/${patientId}/encounters`)}>
              All encounters
            </button>
          </div>
          {!patient.latestEncounter ? (
            <p className="cl-empty-text">No encounter history.</p>
          ) : (
            <div className="cl-timeline-item">
              <p className="cl-timeline-title">{patient.latestEncounter.title}</p>
              <p className="cl-timeline-meta">
                {patient.latestEncounter.date}
                {patient.latestEncounter.providerName ? ` · ${patient.latestEncounter.providerName}` : ''}
              </p>
            </div>
          )}
        </section>

        {/* Activity counts */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Activity summary</h2>
          </div>
          <div className="cl-counts-grid">
            {[
              { label: 'Appointments', value: patient.counts.appointments, path: 'appointments' },
              { label: 'Encounters', value: patient.counts.encounters, path: 'encounters' },
              { label: 'Lab orders', value: patient.counts.labOrders, path: 'labs' },
              { label: 'Messages', value: patient.counts.messages, path: 'messages' },
              { label: 'Problems', value: patient.counts.problems, path: 'chart' },
              { label: 'Medications', value: patient.counts.medications, path: 'chart' },
            ].map((c) => (
              <button
                key={c.label}
                className="cl-count-tile"
                type="button"
                onClick={() => navigate(`/clinician/patients/${patientId}/${c.path}`)}
              >
                <span className="cl-count-value">{c.value}</span>
                <span className="cl-count-label">{c.label}</span>
              </button>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}

