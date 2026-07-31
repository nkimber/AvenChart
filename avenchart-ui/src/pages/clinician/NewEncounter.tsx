// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { useNavigate, useOutletContext, useParams } from 'react-router-dom'
import { BookTemplate, ChevronLeft, Save } from 'lucide-react'
import {
  createEncounterVitals,
  createEncounterSoapNote,
  getAppointmentSchedulingOptions,
  searchPatients,
  type AppointmentSchedulingOptionsResponse,
  type EncounterDetail,
} from '../../api.ts'
import { createCompleteEncounter } from '../../api/encounterCoding.ts'
import { signEncounterUnderLocalPolicy } from '../../api/encounterLifecycle.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type Template = {
  name: string
  subjective: string
  objective: string
  assessment: string
  plan: string
}

function loadTemplates(): Template[] {
  try {
    return JSON.parse(localStorage.getItem('encounter-templates') ?? '[]')
  } catch {
    return []
  }
}

function saveTemplate(tpl: Template) {
  const existing = loadTemplates().filter((t) => t.name !== tpl.name)
  localStorage.setItem(
    'encounter-templates',
    JSON.stringify([tpl, ...existing].slice(0, 20)),
  )
}

function isoNow() {
  const now = new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
}

function optionalPositiveId(value: string) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}

type Step = 'basic' | 'vitals' | 'soap' | 'done'

export default function NewEncounter() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const { patientId: paramPatientId } = useParams<{ patientId?: string }>()

  const [step, setStep] = useState<Step>('basic')
  const [encounter, setEncounter] = useState<EncounterDetail | null>(null)
  const [saving, setSaving] = useState(false)
  const [templateOpen, setTemplateOpen] = useState(false)
  const [templates, setTemplates] = useState<Template[]>(() => loadTemplates())
  const [tplName, setTplName] = useState('')
  const templateRef = useRef<HTMLDivElement>(null)

  // Basic form
  const [patientSearch, setPatientSearch] = useState('')
  const [patientId, setPatientId] = useState(paramPatientId ?? '')
  const [patientLabel, setPatientLabel] = useState('')
  const [searchResults, setSearchResults] = useState<
    { canonicalId: string; displayName: string; dateOfBirth: string }[]
  >([])
  const [dateTime, setDateTime] = useState(isoNow())
  const [reason, setReason] = useState('')
  const [providerId, setProviderId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [billingFacilityId, setBillingFacilityId] = useState('')
  const [sensitivity, setSensitivity] = useState('')
  const [referralSource, setReferralSource] = useState('')
  const [externalId, setExternalId] = useState('')
  const [posCode, setPosCode] = useState('')
  const [billingNote, setBillingNote] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [schedulingOptions, setSchedulingOptions] =
    useState<AppointmentSchedulingOptionsResponse | null>(null)
  const [schedulingOptionsUnavailable, setSchedulingOptionsUnavailable] =
    useState(false)

  // Vitals
  const [systolic, setSystolic] = useState('')
  const [diastolic, setDiastolic] = useState('')
  const [pulse, setPulse] = useState('')
  const [temp, setTemp] = useState('')
  const [weight, setWeight] = useState('')
  const [height, setHeight] = useState('')
  const [o2, setO2] = useState('')
  const [resp, setResp] = useState('')

  // SOAP
  const [subjective, setSubjective] = useState('')
  const [objective, setObjective] = useState('')
  const [assessment, setAssessment] = useState('')
  const [plan, setPlan] = useState('')

  useEffect(() => {
    let cancelled = false
    getAppointmentSchedulingOptions(session.sessionId)
      .then((options) => {
        if (!cancelled) setSchedulingOptions(options)
      })
      .catch(() => {
        if (!cancelled) setSchedulingOptionsUnavailable(true)
      })
    return () => {
      cancelled = true
    }
  }, [session.sessionId])

  useEffect(() => {
    if (!patientSearch || patientId) return
    const t = setTimeout(() => {
      searchPatients(session.sessionId, { search: patientSearch, limit: 6 })
        .then((data) =>
          setSearchResults(
            data.patients.map((p) => ({
              canonicalId: p.canonicalId,
              displayName: p.displayName,
              dateOfBirth: p.dateOfBirth,
            })),
          ),
        )
        .catch(() => {})
    }, 300)
    return () => clearTimeout(t)
  }, [patientSearch, patientId, session.sessionId])

  async function submitBasic(e: React.FormEvent) {
    e.preventDefault()
    const normalizedReason = reason.trim()
    const parsedPosCode = posCode ? Number(posCode) : null
    if (!patientId) {
      setFormError('Select a patient before creating the encounter.')
      return
    }
    if (!normalizedReason) {
      setFormError('Enter the reason for the encounter.')
      return
    }
    if (!dateTime || Number.isNaN(new Date(dateTime).getTime())) {
      setFormError('Enter a valid encounter date and time.')
      return
    }
    if (
      parsedPosCode !== null &&
      (!Number.isInteger(parsedPosCode) ||
        parsedPosCode < 1 ||
        parsedPosCode > 99)
    ) {
      setFormError('Place of service must be a whole number from 1 through 99.')
      return
    }
    setFormError(null)
    setSaving(true)
    try {
      const result = await createCompleteEncounter(session.sessionId, {
        patientId,
        dateTime: dateTime.length === 16 ? `${dateTime}:00` : dateTime,
        reason: normalizedReason,
        providerId: optionalPositiveId(providerId),
        facilityId: optionalPositiveId(facilityId),
        billingFacilityId: optionalPositiveId(billingFacilityId),
        sensitivity: sensitivity.trim() || null,
        referralSource: referralSource.trim() || null,
        externalId: externalId.trim() || null,
        posCode: parsedPosCode,
        billingNote: billingNote.trim() || null,
      })
      setEncounter(result)
      setStep('vitals')
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Could not create the encounter from the supplied visit details.',
      )
    } finally {
      setSaving(false)
    }
  }

  async function submitVitals(e: React.FormEvent) {
    e.preventDefault()
    if (!encounter) return
    const hasAny = [
      systolic,
      diastolic,
      pulse,
      temp,
      weight,
      height,
      o2,
      resp,
    ].some(Boolean)
    if (!hasAny) {
      setStep('soap')
      return
    }
    setSaving(true)
    try {
      await createEncounterVitals(session.sessionId, encounter.encounter, {
        dateTime: dateTime.length === 16 ? `${dateTime}:00` : dateTime,
        systolic: systolic ? Number(systolic) : null,
        diastolic: diastolic ? Number(diastolic) : null,
        pulse: pulse ? Number(pulse) : null,
        temperature: temp ? Number(temp) : null,
        weight: weight ? Number(weight) : null,
        height: height ? Number(height) : null,
        oxygenSaturation: o2 ? Number(o2) : null,
        respiration: resp ? Number(resp) : null,
      })
      setStep('soap')
    } catch {
      showToast('Vitals save failed — you can still add them later.', 'error')
      setStep('soap')
    } finally {
      setSaving(false)
    }
  }

  async function submitSoap(e: React.FormEvent) {
    e.preventDefault()
    if (!encounter) return
    const hasAny = [subjective, objective, assessment, plan].some(Boolean)
    if (!hasAny) {
      setStep('done')
      return
    }
    setSaving(true)
    try {
      await createEncounterSoapNote(session.sessionId, encounter.encounter, {
        dateTime: dateTime.length === 16 ? `${dateTime}:00` : dateTime,
        subjective: subjective || null,
        objective: objective || null,
        assessment: assessment || null,
        plan: plan || null,
      })
      setStep('done')
    } catch {
      showToast('SOAP note save failed — you can add it later.', 'error')
      setStep('done')
    } finally {
      setSaving(false)
    }
  }

  async function handleSign() {
    if (!encounter) return
    setSaving(true)
    try {
      await signEncounterUnderLocalPolicy(
        session.sessionId,
        encounter.encounter,
        {
          isLock: false,
        },
      )
      showToast('Encounter signed.', 'success')
      navigateToEncounter()
    } catch {
      showToast('Sign failed — encounter was saved but not signed.', 'error')
      navigateToEncounter()
    } finally {
      setSaving(false)
    }
  }

  function navigateToEncounter() {
    if (paramPatientId) {
      navigate(`/clinician/patients/${paramPatientId}/encounters`)
    } else if (patientId) {
      navigate(`/clinician/patients/${patientId}/encounters`)
    } else {
      navigate('/clinician/dashboard')
    }
  }

  function applyTemplate(t: Template) {
    setSubjective(t.subjective)
    setObjective(t.objective)
    setAssessment(t.assessment)
    setPlan(t.plan)
    setTemplateOpen(false)
  }

  function handleSaveTemplate() {
    if (!tplName.trim()) return
    const tpl: Template = {
      name: tplName.trim(),
      subjective,
      objective,
      assessment,
      plan,
    }
    saveTemplate(tpl)
    setTemplates(loadTemplates())
    setTplName('')
    showToast('Template saved.', 'success')
  }

  const STEPS: Step[] = ['basic', 'vitals', 'soap', 'done']
  const stepIndex = STEPS.indexOf(step)

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <button
            className="cl-back-btn"
            type="button"
            onClick={() => navigate(-1)}
          >
            <ChevronLeft size={15} /> Back
          </button>
          <h1 className="clinician-page-title">New encounter</h1>
          {patientLabel && (
            <p className="clinician-page-subtitle">{patientLabel}</p>
          )}
        </div>
      </div>

      {/* Step progress */}
      <div className="ne-steps">
        {['Visit info', 'Vitals', 'SOAP note', 'Complete'].map((label, i) => (
          <div
            key={label}
            className={`ne-step${i === stepIndex ? ' ne-step-active' : i < stepIndex ? ' ne-step-done' : ''}`}
          >
            <div className="ne-step-dot">{i < stepIndex ? '✓' : i + 1}</div>
            <span>{label}</span>
          </div>
        ))}
      </div>

      {/* Step: Basic */}
      {step === 'basic' && (
        <form className="cl-card ne-form" onSubmit={submitBasic}>
          <h2 className="ne-section-title">Visit information</h2>
          <p className="ne-section-help">
            Required visit context is followed by optional operational and
            billing metadata. The selected patient cannot be changed after the
            encounter is created.
          </p>
          {formError && (
            <p className="ne-form-error" role="alert">
              {formError}
            </p>
          )}

          {!paramPatientId && (
            <div className="ne-field">
              {patientId ? (
                <>
                  <span className="ne-label">Patient</span>
                  <div className="ne-patient-selected">
                    <span>{patientLabel}</span>
                    <button
                      type="button"
                      className="cl-link"
                      onClick={() => {
                        setPatientId('')
                        setPatientLabel('')
                        setPatientSearch('')
                      }}
                    >
                      Change
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <label className="ne-label" htmlFor="ne-patient-search">
                    Patient
                  </label>
                  <div className="ne-patient-search">
                    <input
                      id="ne-patient-search"
                      className="ne-input"
                      type="text"
                      placeholder="Search by name or ID…"
                      value={patientSearch}
                      onChange={(e) => setPatientSearch(e.target.value)}
                    />
                    {searchResults.length > 0 && (
                      <div className="ne-patient-results">
                        {searchResults.map((p) => (
                          <button
                            key={p.canonicalId}
                            type="button"
                            className="ne-patient-result"
                            onClick={() => {
                              setPatientId(p.canonicalId)
                              setPatientLabel(
                                `${p.displayName} — DOB ${p.dateOfBirth}`,
                              )
                              setSearchResults([])
                              setPatientSearch('')
                            }}
                          >
                            <span>{p.displayName}</span>
                            <span className="ne-result-dob">
                              DOB {p.dateOfBirth}
                            </span>
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                </>
              )}
            </div>
          )}

          <div className="ne-field">
            <label className="ne-label" htmlFor="ne-datetime">
              Date &amp; time
            </label>
            <input
              id="ne-datetime"
              className="ne-input"
              type="datetime-local"
              value={dateTime}
              onChange={(e) => setDateTime(e.target.value)}
              required
            />
          </div>

          <div className="ne-field">
            <label className="ne-label" htmlFor="ne-reason">
              Chief complaint / reason for visit
            </label>
            <input
              id="ne-reason"
              className="ne-input"
              type="text"
              placeholder="e.g. Follow-up visit, Annual physical…"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              required
            />
          </div>

          <fieldset className="ne-metadata-fieldset">
            <legend>Visit metadata</legend>
            <p className="ne-section-help">
              Optional. Provider and facility defaults are still resolved by the
              server when left blank.
            </p>
            {schedulingOptionsUnavailable && (
              <p className="ne-form-notice" role="status">
                Provider and facility choices are temporarily unavailable. You
                can still create the encounter using server defaults.
              </p>
            )}
            <div className="ne-metadata-grid">
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-provider">
                  Provider
                </label>
                <select
                  id="ne-provider"
                  className="ne-input"
                  value={providerId}
                  onChange={(event) => {
                    const nextProviderId = event.target.value
                    setProviderId(nextProviderId)
                    const provider = schedulingOptions?.providers.find(
                      (item) => item.id === Number(nextProviderId),
                    )
                    if (provider?.facilityId && !facilityId) {
                      setFacilityId(String(provider.facilityId))
                    }
                  }}
                >
                  <option value="">Use patient or practice default</option>
                  {schedulingOptions?.providers.map((provider) => (
                    <option key={provider.id} value={provider.id}>
                      {provider.displayName}
                      {provider.facilityName
                        ? ` — ${provider.facilityName}`
                        : ''}
                    </option>
                  ))}
                </select>
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-facility">
                  Visit facility
                </label>
                <select
                  id="ne-facility"
                  className="ne-input"
                  value={facilityId}
                  onChange={(event) => setFacilityId(event.target.value)}
                >
                  <option value="">Use patient or practice default</option>
                  {schedulingOptions?.facilities.map((facility) => (
                    <option key={facility.id} value={facility.id}>
                      {facility.code ? `${facility.code} — ` : ''}
                      {facility.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-billing-facility">
                  Billing facility
                </label>
                <select
                  id="ne-billing-facility"
                  className="ne-input"
                  value={billingFacilityId}
                  onChange={(event) => setBillingFacilityId(event.target.value)}
                >
                  <option value="">Use visit facility</option>
                  {schedulingOptions?.facilities.map((facility) => (
                    <option key={facility.id} value={facility.id}>
                      {facility.code ? `${facility.code} — ` : ''}
                      {facility.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-pos-code">
                  Place of service
                </label>
                <input
                  id="ne-pos-code"
                  className="ne-input"
                  type="number"
                  min="1"
                  max="99"
                  step="1"
                  inputMode="numeric"
                  value={posCode}
                  onChange={(event) => setPosCode(event.target.value)}
                  placeholder="e.g. 11"
                />
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-sensitivity">
                  Sensitivity
                </label>
                <input
                  id="ne-sensitivity"
                  className="ne-input"
                  value={sensitivity}
                  onChange={(event) => setSensitivity(event.target.value)}
                  placeholder="Optional handling label"
                />
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-referral-source">
                  Referral source
                </label>
                <input
                  id="ne-referral-source"
                  className="ne-input"
                  value={referralSource}
                  onChange={(event) => setReferralSource(event.target.value)}
                />
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-external-id">
                  External reference
                </label>
                <input
                  id="ne-external-id"
                  className="ne-input"
                  value={externalId}
                  onChange={(event) => setExternalId(event.target.value)}
                />
              </div>
              <div className="ne-field">
                <label className="ne-label" htmlFor="ne-billing-note">
                  Billing note
                </label>
                <input
                  id="ne-billing-note"
                  className="ne-input"
                  value={billingNote}
                  onChange={(event) => setBillingNote(event.target.value)}
                />
              </div>
            </div>
          </fieldset>

          <div className="ne-actions">
            <button
              className="cl-btn-primary"
              type="submit"
              disabled={saving || !patientId || !reason}
            >
              {saving ? 'Creating…' : 'Create encounter & continue'}
            </button>
          </div>
        </form>
      )}

      {/* Step: Vitals */}
      {step === 'vitals' && (
        <form className="cl-card ne-form" onSubmit={submitVitals}>
          <h2 className="ne-section-title">
            Vitals <span className="ne-optional">(optional)</span>
          </h2>
          <div className="ne-vitals-grid">
            <div className="ne-field">
              <label className="ne-label">Systolic BP</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  placeholder="mmHg"
                  value={systolic}
                  onChange={(e) => setSystolic(e.target.value)}
                />
                <span>mmHg</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Diastolic BP</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  placeholder="mmHg"
                  value={diastolic}
                  onChange={(e) => setDiastolic(e.target.value)}
                />
                <span>mmHg</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Pulse</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  placeholder="bpm"
                  value={pulse}
                  onChange={(e) => setPulse(e.target.value)}
                />
                <span>bpm</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Temperature</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  step="0.1"
                  placeholder="°F"
                  value={temp}
                  onChange={(e) => setTemp(e.target.value)}
                />
                <span>°F</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Weight</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  step="0.1"
                  placeholder="lbs"
                  value={weight}
                  onChange={(e) => setWeight(e.target.value)}
                />
                <span>lbs</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Height</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  step="0.1"
                  placeholder="in"
                  value={height}
                  onChange={(e) => setHeight(e.target.value)}
                />
                <span>in</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">O₂ Saturation</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  placeholder="%"
                  value={o2}
                  onChange={(e) => setO2(e.target.value)}
                />
                <span>%</span>
              </div>
            </div>
            <div className="ne-field">
              <label className="ne-label">Respirations</label>
              <div className="ne-input-unit">
                <input
                  className="ne-input"
                  type="number"
                  placeholder="/min"
                  value={resp}
                  onChange={(e) => setResp(e.target.value)}
                />
                <span>/min</span>
              </div>
            </div>
          </div>
          <div className="ne-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setStep('soap')}
            >
              Skip vitals
            </button>
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Save vitals & continue'}
            </button>
          </div>
        </form>
      )}

      {/* Step: SOAP */}
      {step === 'soap' && (
        <form className="cl-card ne-form" onSubmit={submitSoap}>
          <div className="ne-soap-header">
            <h2 className="ne-section-title">
              SOAP note <span className="ne-optional">(optional)</span>
            </h2>
            <div className="ne-tpl-controls" ref={templateRef}>
              <button
                type="button"
                className="cl-btn-secondary ne-tpl-btn"
                onClick={() => setTemplateOpen((o) => !o)}
              >
                <BookTemplate size={14} /> Templates
              </button>
              {templateOpen && (
                <div className="ne-tpl-panel">
                  {templates.length === 0 ? (
                    <p className="ne-tpl-empty">No saved templates yet.</p>
                  ) : (
                    <ul className="ne-tpl-list">
                      {templates.map((t) => (
                        <li key={t.name}>
                          <button
                            type="button"
                            className="ne-tpl-item"
                            onClick={() => applyTemplate(t)}
                          >
                            {t.name}
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                  <div className="ne-tpl-save">
                    <input
                      className="ne-input ne-tpl-name"
                      placeholder="Template name…"
                      value={tplName}
                      onChange={(e) => setTplName(e.target.value)}
                    />
                    <button
                      type="button"
                      className="cl-btn-primary ne-tpl-save-btn"
                      onClick={handleSaveTemplate}
                    >
                      <Save size={13} /> Save current
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>

          {(['Subjective', 'Objective', 'Assessment', 'Plan'] as const).map(
            (section) => {
              const key = section.toLowerCase() as
                'subjective' | 'objective' | 'assessment' | 'plan'
              const val = { subjective, objective, assessment, plan }[key]
              const setter = {
                subjective: setSubjective,
                objective: setObjective,
                assessment: setAssessment,
                plan: setPlan,
              }[key]
              return (
                <div key={section} className="ne-soap-field">
                  <label className="ne-soap-label">
                    {section[0]}
                    <span className="ne-soap-label-full"> — {section}</span>
                  </label>
                  <textarea
                    className="ne-soap-textarea"
                    rows={3}
                    value={val}
                    onChange={(e) => setter(e.target.value)}
                    placeholder={`${section}…`}
                  />
                </div>
              )
            },
          )}

          <div className="ne-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setStep('done')}
            >
              Skip note
            </button>
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Save note & finish'}
            </button>
          </div>
        </form>
      )}

      {/* Step: Done */}
      {step === 'done' && encounter && (
        <div className="cl-card ne-done">
          <div className="ne-done-icon">✓</div>
          <h2 className="ne-done-title">Encounter created</h2>
          <p className="ne-done-sub">
            Encounter #{encounter.encounter} — {encounter.date}
          </p>
          <div className="ne-done-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={navigateToEncounter}
            >
              View in chart
            </button>
            <button
              className="cl-btn-primary"
              type="button"
              onClick={handleSign}
              disabled={saving}
            >
              {saving ? 'Signing…' : 'Sign & close'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
