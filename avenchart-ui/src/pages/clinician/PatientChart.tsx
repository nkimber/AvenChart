import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Plus, X } from 'lucide-react'
import {
  getClinicalLists,
  createProblem,
  deactivateProblem,
  createAllergy,
  deactivateAllergy,
  createMedication,
  deactivateMedication,
  createImmunization,
  markImmunizationEnteredInError,
  type ClinicalListsResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function statusDot(activity: number) {
  return <span className={`cl-activity-dot ${activity === 1 ? 'cl-activity-active' : 'cl-activity-inactive'}`} aria-label={activity === 1 ? 'Active' : 'Inactive'} />
}

function isoNow() { return new Date().toISOString().replace('T', ' ').slice(0, 19) }

type AddMode = 'problem' | 'allergy' | 'medication' | 'immunization' | null

export default function PatientChart() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<ClinicalListsResponse>>({ status: 'loading' })
  const [addMode, setAddMode] = useState<AddMode>(null)
  const [working, setWorking] = useState(false)

  // Add-problem form state
  const [newProbTitle, setNewProbTitle] = useState('')
  const [newProbDx, setNewProbDx] = useState('')

  // Add-allergy form state
  const [newAllergyTitle, setNewAllergyTitle] = useState('')
  const [newAllergyReaction, setNewAllergyReaction] = useState('')
  const [newAllergySeverity, setNewAllergySeverity] = useState('mild')

  // Add-medication form state
  const [newMedTitle, setNewMedTitle] = useState('')
  const [newMedDx, setNewMedDx] = useState('')

  // Add-immunization form state
  const [newImmVaccine, setNewImmVaccine] = useState('')
  const [newImmDate, setNewImmDate] = useState('')
  const [newImmManufacturer, setNewImmManufacturer] = useState('')
  const [newImmLot, setNewImmLot] = useState('')

  function load() {
    setState({ status: 'loading' })
    getClinicalLists(session.sessionId, patientId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Could not load chart.' }))
  }

  useEffect(() => { load() }, [patientId]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleAddProblem(e: React.FormEvent) {
    e.preventDefault()
    if (!newProbTitle) return
    setWorking(true)
    try {
      const result = await createProblem(session.sessionId, {
        patientId, title: newProbTitle, dateTime: isoNow(), diagnosis: newProbDx || null, comments: '',
      })
      setState({ status: 'ready', data: result.detail })
      setAddMode(null); setNewProbTitle(''); setNewProbDx('')
      showToast('Problem added.', 'success')
    } catch { showToast('Could not add problem.', 'error') }
    finally { setWorking(false) }
  }

  async function handleDeactivateProblem(id: string) {
    setWorking(true)
    try {
      const result = await deactivateProblem(session.sessionId, id, 'Marked inactive by clinician')
      setState({ status: 'ready', data: result.detail })
      showToast('Problem marked inactive.', 'success')
    } catch { showToast('Could not update problem.', 'error') }
    finally { setWorking(false) }
  }

  async function handleAddAllergy(e: React.FormEvent) {
    e.preventDefault()
    if (!newAllergyTitle) return
    setWorking(true)
    try {
      const result = await createAllergy(session.sessionId, {
        patientId, title: newAllergyTitle, dateTime: isoNow(),
        reaction: newAllergyReaction, severity: newAllergySeverity, comments: '',
      })
      setState({ status: 'ready', data: result.detail })
      setAddMode(null); setNewAllergyTitle(''); setNewAllergyReaction(''); setNewAllergySeverity('mild')
      showToast('Allergy added.', 'success')
    } catch { showToast('Could not add allergy.', 'error') }
    finally { setWorking(false) }
  }

  async function handleDeactivateAllergy(id: string) {
    setWorking(true)
    try {
      const result = await deactivateAllergy(session.sessionId, id, 'Marked inactive by clinician')
      setState({ status: 'ready', data: result.detail })
      showToast('Allergy marked inactive.', 'success')
    } catch { showToast('Could not update allergy.', 'error') }
    finally { setWorking(false) }
  }

  async function handleAddMedication(e: React.FormEvent) {
    e.preventDefault()
    if (!newMedTitle) return
    setWorking(true)
    try {
      const result = await createMedication(session.sessionId, {
        patientId, title: newMedTitle, dateTime: isoNow(), diagnosis: newMedDx || null, comments: '',
      })
      setState({ status: 'ready', data: result.detail })
      setAddMode(null); setNewMedTitle(''); setNewMedDx('')
      showToast('Medication added.', 'success')
    } catch { showToast('Could not add medication.', 'error') }
    finally { setWorking(false) }
  }

  async function handleDeactivateMedication(id: string) {
    setWorking(true)
    try {
      const result = await deactivateMedication(session.sessionId, id, 'Marked inactive by clinician')
      setState({ status: 'ready', data: result.detail })
      showToast('Medication marked inactive.', 'success')
    } catch { showToast('Could not update medication.', 'error') }
    finally { setWorking(false) }
  }

  async function handleAddImmunization(e: React.FormEvent) {
    e.preventDefault()
    if (!newImmVaccine || !newImmDate) return
    setWorking(true)
    try {
      const result = await createImmunization(session.sessionId, {
        patientId,
        vaccine: newImmVaccine,
        administeredAt: newImmDate,
        manufacturer: newImmManufacturer || null,
        lotNumber: newImmLot || null,
      })
      setState({ status: 'ready', data: result.detail })
      setAddMode(null); setNewImmVaccine(''); setNewImmDate(''); setNewImmManufacturer(''); setNewImmLot('')
      showToast('Immunization recorded.', 'success')
    } catch { showToast('Could not add immunization.', 'error') }
    finally { setWorking(false) }
  }

  async function handleMarkImmunizationError(id: number) {
    setWorking(true)
    try {
      const result = await markImmunizationEnteredInError(session.sessionId, id)
      setState({ status: 'ready', data: result.detail })
      showToast('Immunization marked entered-in-error.', 'success')
    } catch { showToast('Could not update immunization.', 'error') }
    finally { setWorking(false) }
  }

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
            <button className="cl-btn-icon" type="button" onClick={() => setAddMode(addMode === 'problem' ? null : 'problem')} title="Add problem">
              <Plus size={15} />
            </button>
          </div>
          {addMode === 'problem' && (
            <form className="cl-inline-form" onSubmit={handleAddProblem}>
              <input className="ne-input" placeholder="Problem title…" value={newProbTitle} onChange={(e) => setNewProbTitle(e.target.value)} required />
              <input className="ne-input" placeholder="Diagnosis code (optional)" value={newProbDx} onChange={(e) => setNewProbDx(e.target.value)} />
              <div className="cl-inline-form-actions">
                <button className="cl-btn-primary" type="submit" disabled={working || !newProbTitle}>Add</button>
                <button className="cl-btn-secondary" type="button" onClick={() => setAddMode(null)}>Cancel</button>
              </div>
            </form>
          )}
          {data.problems.length === 0 ? (
            <p className="cl-empty-text">No problems on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.problems.map((p) => (
                <li key={p.id} className="cl-clinical-row cl-clinical-row-interactive">
                  {statusDot(p.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{p.title}</p>
                    {(p.diagnosis ?? p.date) && (
                      <p className="cl-clinical-meta">
                        {p.diagnosis ?? ''}{p.date ? ` · ${p.date}` : ''}
                      </p>
                    )}
                  </div>
                  {p.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      title="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateProblem(p.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Allergies */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Allergies ({data.allergies.length})</h2>
            <button className="cl-btn-icon" type="button" onClick={() => setAddMode(addMode === 'allergy' ? null : 'allergy')} title="Add allergy">
              <Plus size={15} />
            </button>
          </div>
          {addMode === 'allergy' && (
            <form className="cl-inline-form" onSubmit={handleAddAllergy}>
              <input className="ne-input" placeholder="Allergen name…" value={newAllergyTitle} onChange={(e) => setNewAllergyTitle(e.target.value)} required />
              <input className="ne-input" placeholder="Reaction (optional)" value={newAllergyReaction} onChange={(e) => setNewAllergyReaction(e.target.value)} />
              <select className="ne-input" value={newAllergySeverity} onChange={(e) => setNewAllergySeverity(e.target.value)}>
                <option value="mild">Mild</option>
                <option value="moderate">Moderate</option>
                <option value="severe">Severe</option>
                <option value="life-threatening">Life-threatening</option>
              </select>
              <div className="cl-inline-form-actions">
                <button className="cl-btn-primary" type="submit" disabled={working || !newAllergyTitle}>Add</button>
                <button className="cl-btn-secondary" type="button" onClick={() => setAddMode(null)}>Cancel</button>
              </div>
            </form>
          )}
          {data.allergies.length === 0 ? (
            <p className="cl-empty-text">No known allergies on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.allergies.map((a) => (
                <li key={a.id} className="cl-clinical-row cl-clinical-row-interactive">
                  {statusDot(a.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{a.title}</p>
                    {(a.reaction ?? a.severity) && (
                      <p className="cl-clinical-meta">
                        {a.reaction ?? ''}{a.severity ? ` · ${a.severity}` : ''}
                      </p>
                    )}
                  </div>
                  {a.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      title="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateAllergy(a.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Medications */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Medications ({data.medications.length})</h2>
            <button className="cl-btn-icon" type="button" onClick={() => setAddMode(addMode === 'medication' ? null : 'medication')} title="Add medication">
              <Plus size={15} />
            </button>
          </div>
          {addMode === 'medication' && (
            <form className="cl-inline-form" onSubmit={handleAddMedication}>
              <input className="ne-input" placeholder="Medication name…" value={newMedTitle} onChange={(e) => setNewMedTitle(e.target.value)} required />
              <input className="ne-input" placeholder="Diagnosis code (optional)" value={newMedDx} onChange={(e) => setNewMedDx(e.target.value)} />
              <div className="cl-inline-form-actions">
                <button className="cl-btn-primary" type="submit" disabled={working || !newMedTitle}>Add</button>
                <button className="cl-btn-secondary" type="button" onClick={() => setAddMode(null)}>Cancel</button>
              </div>
            </form>
          )}
          {data.medications.length === 0 ? (
            <p className="cl-empty-text">No medications on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.medications.map((m) => (
                <li key={m.id} className="cl-clinical-row cl-clinical-row-interactive">
                  {statusDot(m.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{m.title}</p>
                    {m.date && <p className="cl-clinical-meta">{m.date}</p>}
                  </div>
                  {m.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      title="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateMedication(m.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
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
            <button className="cl-btn-icon" type="button" onClick={() => setAddMode(addMode === 'immunization' ? null : 'immunization')} title="Add immunization">
              <Plus size={15} />
            </button>
          </div>
          {addMode === 'immunization' && (
            <form className="cl-inline-form" onSubmit={handleAddImmunization}>
              <div className="form-row">
                <input className="ne-input" placeholder="Vaccine name…" value={newImmVaccine} onChange={(e) => setNewImmVaccine(e.target.value)} required style={{ flex: 2 }} />
                <input className="ne-input" type="date" placeholder="Date administered" value={newImmDate} onChange={(e) => setNewImmDate(e.target.value)} required />
              </div>
              <div className="form-row">
                <input className="ne-input" placeholder="Manufacturer (optional)" value={newImmManufacturer} onChange={(e) => setNewImmManufacturer(e.target.value)} />
                <input className="ne-input" placeholder="Lot number (optional)" value={newImmLot} onChange={(e) => setNewImmLot(e.target.value)} />
              </div>
              <div className="cl-inline-form-actions">
                <button className="cl-btn-primary" type="submit" disabled={working || !newImmVaccine || !newImmDate}>Record</button>
                <button className="cl-btn-secondary" type="button" onClick={() => setAddMode(null)}>Cancel</button>
              </div>
            </form>
          )}
          {data.immunizations.length === 0 ? (
            <p className="cl-empty-text">No immunizations on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.immunizations.map((imm) => (
                <li key={imm.id} className="cl-clinical-row cl-clinical-row-interactive">
                  <div className="cl-activity-dot cl-activity-active" aria-hidden="true" />
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{imm.vaccine}</p>
                    <p className="cl-clinical-meta">
                      {imm.administeredAt ?? ''}
                      {imm.manufacturer ? ` · ${imm.manufacturer}` : ''}
                      {imm.lotNumber ? ` · Lot: ${imm.lotNumber}` : ''}
                    </p>
                  </div>
                  <button
                    className="cl-clinical-action"
                    type="button"
                    title="Mark entered in error"
                    disabled={working}
                    onClick={() => handleMarkImmunizationError(imm.id)}
                  >
                    <X size={12} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
