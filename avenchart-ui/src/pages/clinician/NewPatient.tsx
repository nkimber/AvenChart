// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useRef, useState } from "react"
import { Link, useNavigate, useOutletContext } from "react-router-dom"
import {
  AlertTriangle,
  CheckCircle,
  ChevronLeft,
  UserPlus,
} from "lucide-react"
import {
  createPatient,
  findPatientDuplicateCandidates,
  type PatientDuplicateSearchResponse,
  type PatientRegistrationInput,
} from "../../api.ts"
import { showToast } from "../../components/Toast.tsx"
import type { ClinicianOutletContext } from "./ClinicianShell.tsx"

const BLANK: PatientRegistrationInput = {
  pubpid: "", firstName: "", lastName: "", preferredName: "", sex: "",
  dateOfBirth: "", street: "", city: "", state: "", postalCode: "",
  phoneHome: "", phoneCell: "", email: "", maritalStatus: "",
  occupation: "", race: "", ethnicity: "", hipaaAllowSms: "NO", hipaaAllowEmail: "NO",
}

type DuplicateCheckState =
  | { status: "idle" }
  | { status: "loading" }
  | {
      status: "ready"
      fingerprint: string
      data: PatientDuplicateSearchResponse
    }
  | { status: "error"; message: string }

const duplicateIdentityFields: Array<keyof PatientRegistrationInput> = [
  "firstName",
  "lastName",
  "dateOfBirth",
  "phoneHome",
  "phoneCell",
  "email",
]

function duplicateFingerprint(form: PatientRegistrationInput) {
  return JSON.stringify({
    firstName: form.firstName.trim().toLocaleLowerCase(),
    lastName: form.lastName.trim().toLocaleLowerCase(),
    dateOfBirth: form.dateOfBirth.trim(),
    phone: (form.phoneHome || form.phoneCell).replace(/\D/g, ""),
    email: form.email.trim().toLocaleLowerCase(),
  })
}

export default function NewPatient() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [form, setForm] = useState<PatientRegistrationInput>(BLANK)
  const [saving, setSaving] = useState(false)
  const [duplicateCheck, setDuplicateCheck] =
    useState<DuplicateCheckState>({ status: "idle" })
  const [separatePatientConfirmed, setSeparatePatientConfirmed] =
    useState(false)
  const [duplicateReviewReason, setDuplicateReviewReason] = useState("")
  const duplicateRequestId = useRef(0)

  function set(patch: Partial<PatientRegistrationInput>) {
    if (duplicateIdentityFields.some((field) => field in patch)) {
      duplicateRequestId.current += 1
      setDuplicateCheck({ status: "idle" })
      setSeparatePatientConfirmed(false)
      setDuplicateReviewReason("")
    }
    setForm((f) => ({ ...f, ...patch }))
  }

  async function registerPatient() {
    setSaving(true)
    try {
      const patient = await createPatient(session.sessionId, {
        ...form,
        duplicateReviewAcknowledged: hasDuplicateCandidates,
        duplicateReviewReason: hasDuplicateCandidates
          ? duplicateReviewReason.trim()
          : undefined,
      })
      showToast("Patient registered.", "success")
      navigate("/clinician/patients/" + patient.canonicalId + "/summary")
    } catch {
      showToast("Could not register patient.", "error")
    } finally {
      setSaving(false)
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const fingerprint = duplicateFingerprint(form)
    const reviewedCurrentValues =
      duplicateCheck.status === "ready" &&
      duplicateCheck.fingerprint === fingerprint

    if (!reviewedCurrentValues) {
      const requestId = ++duplicateRequestId.current
      setDuplicateCheck({ status: "loading" })
      setSeparatePatientConfirmed(false)
      setDuplicateReviewReason("")
      try {
        const data = await findPatientDuplicateCandidates(
          session.sessionId,
          {
            firstName: form.firstName,
            lastName: form.lastName,
            dateOfBirth: form.dateOfBirth,
            phone: form.phoneHome || form.phoneCell,
            email: form.email,
            limit: 10,
          },
        )
        if (requestId !== duplicateRequestId.current) return
        setDuplicateCheck({ status: "ready", fingerprint, data })
        if (data.candidates.length > 0) return
      } catch (error) {
        if (requestId !== duplicateRequestId.current) return
        setDuplicateCheck({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Duplicate detection is unavailable.",
        })
        return
      }
    } else if (
      duplicateCheck.data.candidates.length > 0 &&
      (!separatePatientConfirmed || duplicateReviewReason.trim().length < 10)
    ) {
      return
    }

    await registerPatient()
  }

  const hasDuplicateCandidates =
    duplicateCheck.status === "ready" &&
    duplicateCheck.data.candidates.length > 0

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <button className="cl-btn-secondary" type="button" style={{ marginBottom: 8 }} onClick={() => navigate(-1)}>
            <ChevronLeft size={14} /> Back
          </button>
          <h1 className="clinician-page-title" style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <UserPlus size={20} /> Register new patient
          </h1>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="cl-grid-two">
          <section className="cl-card">
            <div className="cl-card-header"><h2 className="cl-card-title">Identity</h2></div>
            <div className="field">
              <label className="label" htmlFor="np-pubpid">Chart number *</label>
              <input id="np-pubpid" className="input" value={form.pubpid} onChange={(e) => set({ pubpid: e.target.value })} required />
            </div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-first">First name *</label>
                <input id="np-first" className="input" value={form.firstName} onChange={(e) => set({ firstName: e.target.value })} required />
              </div>
              <div className="field">
                <label className="label" htmlFor="np-last">Last name *</label>
                <input id="np-last" className="input" value={form.lastName} onChange={(e) => set({ lastName: e.target.value })} required />
              </div>
            </div>
            <div className="field">
              <label className="label" htmlFor="np-pref">Preferred name</label>
              <input id="np-pref" className="input" value={form.preferredName} onChange={(e) => set({ preferredName: e.target.value })} />
            </div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-sex">Sex *</label>
                <select id="np-sex" className="select" value={form.sex} onChange={(e) => set({ sex: e.target.value })} required>
                  <option value="">Select</option>
                  <option value="Male">Male</option>
                  <option value="Female">Female</option>
                  <option value="Unknown">Unknown</option>
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="np-dob">Date of birth *</label>
                <input id="np-dob" type="date" className="input" value={form.dateOfBirth} onChange={(e) => set({ dateOfBirth: e.target.value })} required />
              </div>
            </div>
          </section>

          <section className="cl-card">
            <div className="cl-card-header"><h2 className="cl-card-title">Contact</h2></div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-phone">Home phone</label>
                <input id="np-phone" type="tel" className="input" value={form.phoneHome} onChange={(e) => set({ phoneHome: e.target.value })} />
              </div>
              <div className="field">
                <label className="label" htmlFor="np-cell">Cell phone</label>
                <input id="np-cell" type="tel" className="input" value={form.phoneCell} onChange={(e) => set({ phoneCell: e.target.value })} />
              </div>
            </div>
            <div className="field">
              <label className="label" htmlFor="np-email">Email</label>
              <input id="np-email" type="email" className="input" value={form.email} onChange={(e) => set({ email: e.target.value })} />
            </div>
            <p className="cl-form-section-label" style={{ marginTop: 12 }}>Address</p>
            <div className="field">
              <label className="label" htmlFor="np-street">Street</label>
              <input id="np-street" className="input" value={form.street} onChange={(e) => set({ street: e.target.value })} />
            </div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-city">City</label>
                <input id="np-city" className="input" value={form.city} onChange={(e) => set({ city: e.target.value })} />
              </div>
              <div className="field">
                <label className="label" htmlFor="np-state">State</label>
                <input id="np-state" className="input" maxLength={2} value={form.state} onChange={(e) => set({ state: e.target.value })} />
              </div>
              <div className="field">
                <label className="label" htmlFor="np-zip">ZIP</label>
                <input id="np-zip" className="input" value={form.postalCode} onChange={(e) => set({ postalCode: e.target.value })} />
              </div>
            </div>
          </section>

          <section className="cl-card">
            <div className="cl-card-header"><h2 className="cl-card-title">Demographics</h2></div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-marital">Marital status</label>
                <select id="np-marital" className="select" value={form.maritalStatus} onChange={(e) => set({ maritalStatus: e.target.value })}>
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
                <label className="label" htmlFor="np-occ">Occupation</label>
                <input id="np-occ" className="input" value={form.occupation} onChange={(e) => set({ occupation: e.target.value })} />
              </div>
            </div>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-race">Race</label>
                <input id="np-race" className="input" value={form.race} onChange={(e) => set({ race: e.target.value })} />
              </div>
              <div className="field">
                <label className="label" htmlFor="np-ethnicity">Ethnicity</label>
                <input id="np-ethnicity" className="input" value={form.ethnicity} onChange={(e) => set({ ethnicity: e.target.value })} />
              </div>
            </div>
            <p className="cl-form-section-label" style={{ marginTop: 12 }}>Communication preferences</p>
            <div className="form-row">
              <div className="field">
                <label className="label" htmlFor="np-sms">Allow SMS</label>
                <select id="np-sms" className="select" value={form.hipaaAllowSms} onChange={(e) => set({ hipaaAllowSms: e.target.value })}>
                  <option value="NO">No</option>
                  <option value="YES">Yes</option>
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="np-email-opt">Allow Email</label>
                <select id="np-email-opt" className="select" value={form.hipaaAllowEmail} onChange={(e) => set({ hipaaAllowEmail: e.target.value })}>
                  <option value="NO">No</option>
                  <option value="YES">Yes</option>
                </select>
              </div>
            </div>
          </section>
        </div>

        <section
          className="cl-card patient-registration-duplicate-check"
          aria-labelledby="patient-registration-duplicate-heading"
          aria-live="polite"
        >
          <div className="cl-card-header">
            <div>
              <h2
                className="cl-card-title"
                id="patient-registration-duplicate-heading"
              >
                Duplicate record check
              </h2>
              <p className="cl-empty-text">
                Registration checks name, date of birth, phone, and email
                against active patient records before creating a chart.
              </p>
            </div>
            {duplicateCheck.status === "ready" && (
              <span className="cl-badge cl-badge-muted">
                {duplicateCheck.data.candidates.length} returned
              </span>
            )}
          </div>

          {duplicateCheck.status === "idle" && (
            <p className="cl-empty-text">
              Submit the form to run the required check. No chart is created
              until the check succeeds.
            </p>
          )}

          {duplicateCheck.status === "loading" && (
            <p className="cl-empty-text" role="status">
              Checking for possible duplicate records…
            </p>
          )}

          {duplicateCheck.status === "error" && (
            <div className="cl-inline-error" role="alert">
              <span>
                Duplicate detection is unavailable. Registration is paused.{" "}
                {duplicateCheck.message}
              </span>
              <button className="cl-link" type="submit">
                Retry
              </button>
            </div>
          )}

          {duplicateCheck.status === "ready" &&
            duplicateCheck.data.candidates.length === 0 && (
              <div className="patient-registration-duplicate-clear">
                <CheckCircle size={18} aria-hidden="true" />
                <div>
                  <strong>No possible duplicate was returned.</strong>
                  <p>
                    Checked dataset {duplicateCheck.data.datasetId} version{" "}
                    {duplicateCheck.data.datasetVersion}.
                  </p>
                </div>
              </div>
            )}

          {duplicateCheck.status === "ready" &&
            duplicateCheck.data.candidates.length > 0 && (
              <>
                <div className="patient-registration-duplicate-warning">
                  <AlertTriangle size={18} aria-hidden="true" />
                  <div>
                    <strong>
                      Review possible existing records before continuing.
                    </strong>
                    <p>
                      Match scores are review evidence, not identity proof.
                      Open an existing chart if it represents this patient.
                    </p>
                  </div>
                </div>
                <ul className="patient-registration-duplicate-list">
                  {duplicateCheck.data.candidates.map((candidate) => (
                    <li key={candidate.canonicalId}>
                      <div>
                        <strong>
                          {candidate.displayName}{" "}
                          <span className="cl-badge cl-badge-muted">
                            {candidate.matchScore}% match
                          </span>
                        </strong>
                        <p>
                          DOB {candidate.dateOfBirth} · Chart #
                          {candidate.pubpid} · {candidate.canonicalId}
                        </p>
                        <p>{candidate.matchReasons.join(" · ")}</p>
                        {(candidate.email ||
                          candidate.phoneHome ||
                          candidate.phoneCell ||
                          candidate.phone) && (
                          <p>
                            {[
                              candidate.email,
                              candidate.phoneHome ??
                                candidate.phoneCell ??
                                candidate.phone,
                            ]
                              .filter(Boolean)
                              .join(" · ")}
                          </p>
                        )}
                      </div>
                      <Link
                        className="cl-btn-secondary"
                        to={`/clinician/patients/${encodeURIComponent(candidate.canonicalId)}/summary`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Open existing chart
                      </Link>
                    </li>
                  ))}
                </ul>
                <label className="patient-registration-separate-confirmation">
                  <input
                    type="checkbox"
                    checked={separatePatientConfirmed}
                    onChange={(event) =>
                      setSeparatePatientConfirmed(event.target.checked)
                    }
                  />
                  <span>
                    I reviewed these records and intend to register a separate
                    patient. This does not mark candidates unique or merge
                    records.
                  </span>
                </label>
                <label className="field" htmlFor="np-duplicate-review-reason">
                  <span className="label">Reason for separate registration *</span>
                  <textarea
                    id="np-duplicate-review-reason"
                    className="input"
                    rows={3}
                    minLength={10}
                    maxLength={500}
                    value={duplicateReviewReason}
                    onChange={(event) => setDuplicateReviewReason(event.target.value)}
                    required
                  />
                  <span className="help-text">Record why these records do not represent the patient being registered (10–500 characters).</span>
                </label>
                <p className="cl-empty-text">
                  {duplicateCheck.data.candidates.length} of at most{" "}
                  {duplicateCheck.data.limit} candidates are shown from dataset{" "}
                  {duplicateCheck.data.datasetId} version{" "}
                  {duplicateCheck.data.datasetVersion}.
                </p>
              </>
            )}

        <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
          <button
            className="cl-btn-primary"
            type="submit"
            disabled={
              saving ||
              duplicateCheck.status === "loading" ||
              (hasDuplicateCandidates &&
                (!separatePatientConfirmed || duplicateReviewReason.trim().length < 10))
            }
          >
            {saving
              ? "Registering…"
              : duplicateCheck.status === "loading"
                ? "Checking…"
                : hasDuplicateCandidates
                  ? "Register separate patient"
                  : "Review and register"}
          </button>
          <button className="cl-btn-secondary" type="button" onClick={() => navigate(-1)}>Cancel</button>
        </div>
        </section>
      </form>
    </div>
  )
}
