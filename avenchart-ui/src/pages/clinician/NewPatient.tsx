import { useState } from "react"
import { useNavigate, useOutletContext } from "react-router-dom"
import { ChevronLeft, UserPlus } from "lucide-react"
import { createPatient, type PatientRegistrationInput } from "../../api.ts"
import { showToast } from "../../components/Toast.tsx"
import type { ClinicianOutletContext } from "./ClinicianShell.tsx"

const BLANK: PatientRegistrationInput = {
  pubpid: "", firstName: "", lastName: "", preferredName: "", sex: "",
  dateOfBirth: "", street: "", city: "", state: "", postalCode: "",
  phoneHome: "", phoneCell: "", email: "", maritalStatus: "",
  occupation: "", race: "", ethnicity: "", hipaaAllowSms: "NO", hipaaAllowEmail: "NO",
}

export default function NewPatient() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [form, setForm] = useState<PatientRegistrationInput>(BLANK)
  const [saving, setSaving] = useState(false)

  function set(patch: Partial<PatientRegistrationInput>) {
    setForm((f) => ({ ...f, ...patch }))
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    try {
      const patient = await createPatient(session.sessionId, form)
      showToast("Patient registered.", "success")
      navigate("/clinician/patients/" + patient.canonicalId + "/summary")
    } catch {
      showToast("Could not register patient.", "error")
    } finally {
      setSaving(false)
    }
  }

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
              <label className="label" htmlFor="np-pubpid">Chart number (optional)</label>
              <input id="np-pubpid" className="input" value={form.pubpid} onChange={(e) => set({ pubpid: e.target.value })} />
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

        <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
          <button className="cl-btn-primary" type="submit" disabled={saving}>
            {saving ? "Registering..." : "Register patient"}
          </button>
          <button className="cl-btn-secondary" type="button" onClick={() => navigate(-1)}>Cancel</button>
        </div>
      </form>
    </div>
  )
}
