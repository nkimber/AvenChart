import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { Download, LogOut } from 'lucide-react'
import {
  getPatientPortalProfile,
  submitPatientPortalProfileChange,
  type PatientPortalProfileChangeInput,
  type PatientPortalProfileDemographics,
  type PatientPortalProfileResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PortalOutletContext } from './PortalShell.tsx'

const emptyContactForm: PatientPortalProfileChangeInput = {
  phoneHome: null,
  phoneCell: null,
  email: null,
  hipaaAllowSms: null,
  hipaaAllowEmail: null,
  street: null,
  city: null,
  state: null,
  postalCode: null,
}

function contactFormFrom(demographics: PatientPortalProfileDemographics): PatientPortalProfileChangeInput {
  return {
    phoneHome: demographics.phoneHome ?? null,
    phoneCell: demographics.phoneCell ?? null,
    email: demographics.email ?? null,
    hipaaAllowSms: demographics.hipaaAllowSms ?? 'NO',
    hipaaAllowEmail: demographics.hipaaAllowEmail ?? 'NO',
    street: demographics.street ?? null,
    city: demographics.city ?? null,
    state: demographics.state ?? null,
    postalCode: demographics.postalCode ?? null,
  }
}

export default function PortalAccount() {
  const { session, home, signOut } = useOutletContext<PortalOutletContext>()
  const [profile, setProfile] = useState<PatientPortalProfileResponse | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [contactForm, setContactForm] = useState<PatientPortalProfileChangeInput>(emptyContactForm)

  function loadProfile() {
    setProfileError(null)
    getPatientPortalProfile(session.sessionId)
      .then((result) => {
        if (!result.authenticated) throw new Error(result.failureReason ?? 'Your profile is unavailable.')
        setProfile(result)
        setContactForm(contactFormFrom(result.demographics))
      })
      .catch((error) => setProfileError(error instanceof Error ? error.message : 'Could not load account details.'))
  }

  useEffect(() => {
    loadProfile()
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function openEdit() {
    if (profile) setContactForm(contactFormFrom(profile.demographics))
    setEditOpen(true)
  }

  async function handleSaveContact(event: React.FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      const result = await submitPatientPortalProfileChange(session.sessionId, contactForm)
      if (!result.authenticated) throw new Error(result.failureReason ?? 'Could not submit this request.')
      setProfile(result)
      setEditOpen(false)
      showToast('Your contact-change request is awaiting review.', 'success')
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not submit contact changes.', 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="portal-page">
      <section className="portal-section">
        <div className="account-avatar-row">
          <div className="account-avatar">
            {session.displayName
              .split(' ')
              .filter(Boolean)
              .slice(0, 2)
              .map((part) => part[0]?.toUpperCase())
              .join('')}
          </div>
          <div>
            <p className="account-name">{session.displayName}</p>
            <p className="muted">{session.portalUsername}</p>
          </div>
        </div>
      </section>

      <section className="portal-section">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 14 }}>
          <div>
            <h2 className="portal-section-title">Contact details</h2>
            <p className="muted" style={{ marginTop: 4, fontSize: 13 }}>Changes are reviewed before they update your chart.</p>
          </div>
          {!editOpen && <button className="toggle-button" type="button" onClick={openEdit} disabled={!profile}>Request changes</button>}
        </div>

        {profileError && <div className="error-banner">{profileError}</div>}
        {!profile && !profileError && <div className="skeleton-list"><div className="skeleton-row" style={{ height: 84 }} /></div>}
        {profile?.pendingChange && (
          <div className="portal-profile-pending">
            <strong>Update awaiting review</strong>
            <span>Your latest contact and communication-preference request is pending staff review.</span>
          </div>
        )}

        {editOpen ? (
          <form onSubmit={handleSaveContact}>
            <div className="portal-contact-grid">
              <div className="field">
                <label className="label" htmlFor="pa-phone">Home phone</label>
                <input id="pa-phone" type="tel" className="input" value={contactForm.phoneHome ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, phoneHome: event.target.value || null }))} />
              </div>
              <div className="field">
                <label className="label" htmlFor="pa-cell">Cell phone</label>
                <input id="pa-cell" type="tel" className="input" value={contactForm.phoneCell ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, phoneCell: event.target.value || null }))} />
              </div>
              <div className="field portal-contact-grid-wide">
                <label className="label" htmlFor="pa-email">Email</label>
                <input id="pa-email" type="email" className="input" value={contactForm.email ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, email: event.target.value || null }))} />
              </div>
              <div className="field portal-contact-grid-wide">
                <label className="label" htmlFor="pa-street">Street address</label>
                <input id="pa-street" className="input" value={contactForm.street ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, street: event.target.value || null }))} />
              </div>
              <div className="field">
                <label className="label" htmlFor="pa-city">City</label>
                <input id="pa-city" className="input" value={contactForm.city ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, city: event.target.value || null }))} />
              </div>
              <div className="field">
                <label className="label" htmlFor="pa-state">State</label>
                <input id="pa-state" className="input" value={contactForm.state ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, state: event.target.value || null }))} />
              </div>
              <div className="field">
                <label className="label" htmlFor="pa-postal">Postal code</label>
                <input id="pa-postal" className="input" value={contactForm.postalCode ?? ''} onChange={(event) => setContactForm((form) => ({ ...form, postalCode: event.target.value || null }))} />
              </div>
            </div>
            <div className="portal-consent-row">
              <label><input type="checkbox" checked={contactForm.hipaaAllowSms === 'YES'} onChange={(event) => setContactForm((form) => ({ ...form, hipaaAllowSms: event.target.checked ? 'YES' : 'NO' }))} /> Allow SMS reminders</label>
              <label><input type="checkbox" checked={contactForm.hipaaAllowEmail === 'YES'} onChange={(event) => setContactForm((form) => ({ ...form, hipaaAllowEmail: event.target.checked ? 'YES' : 'NO' }))} /> Allow email reminders</label>
            </div>
            <div className="button-row">
              <button className="button-primary" type="submit" disabled={saving}>{saving ? 'Submitting...' : 'Submit for review'}</button>
              <button className="button-secondary" type="button" onClick={() => setEditOpen(false)} disabled={saving}>Cancel</button>
            </div>
          </form>
        ) : profile && (
          <ul className="fact-list" style={{ marginBottom: 0 }}>
            <li className="fact-row"><span>Name</span><span>{profile.displayName}</span></li>
            <li className="fact-row"><span>Home phone</span><span>{profile.demographics.phoneHome ?? 'Not recorded'}</span></li>
            <li className="fact-row"><span>Cell phone</span><span>{profile.demographics.phoneCell ?? 'Not recorded'}</span></li>
            <li className="fact-row"><span>Email</span><span>{profile.demographics.email ?? 'Not recorded'}</span></li>
            <li className="fact-row"><span>Address</span><span>{[profile.demographics.street, profile.demographics.city, profile.demographics.state, profile.demographics.postalCode].filter(Boolean).join(', ') || 'Not recorded'}</span></li>
            <li className="fact-row"><span>SMS reminders</span><span>{profile.demographics.hipaaAllowSms === 'YES' ? 'Allowed' : 'Not allowed'}</span></li>
            <li className="fact-row"><span>Email reminders</span><span>{profile.demographics.hipaaAllowEmail === 'YES' ? 'Allowed' : 'Not allowed'}</span></li>
          </ul>
        )}
      </section>

      <section className="portal-section">
        <h2 className="portal-section-title" style={{ marginBottom: 14 }}>Account activity</h2>
        <ul className="fact-list" style={{ marginBottom: 0 }}>
          <li className="fact-row"><span>Portal username</span><span>{session.portalUsername}</span></li>
          {home && <><li className="fact-row"><span>Upcoming appointments</span><span>{home.upcomingAppointmentCount}</span></li><li className="fact-row"><span>Unread messages</span><span>{home.messages.newMessages}</span></li></>}
        </ul>
      </section>

      <section className="portal-section">
        <h2 className="portal-section-title" style={{ marginBottom: 14 }}>Quick actions</h2>
        <Link to="/portal/records" state={{ tab: 'report' }} className="account-quick-link">
          <div className="account-quick-link-icon"><Download size={16} /></div>
          <div><p className="account-quick-link-title">Download medical report</p><p className="account-quick-link-desc">Generate a PDF summary of your full record</p></div>
        </Link>
      </section>

      <section className="portal-section">
        <h2 className="portal-section-title" style={{ marginBottom: 14 }}>Session</h2>
        <p className="muted" style={{ marginBottom: 16, fontSize: 13 }}>You are currently signed in to the patient portal. Sign out to end your session on this device.</p>
        <button className="button-sign-out" type="button" onClick={signOut}><LogOut size={16} />Sign out</button>
      </section>
    </div>
  )
}
