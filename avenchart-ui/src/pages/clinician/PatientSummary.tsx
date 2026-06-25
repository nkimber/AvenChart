import { useNavigate, useOutletContext } from 'react-router-dom'
import { CalendarClock, FileText, Phone, Printer, Shield } from 'lucide-react'
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

export default function PatientSummary() {
  const { patient } = useOutletContext<PatientOutletContext>()
  const navigate = useNavigate()
  const patientId = patient.canonicalId

  return (
    <div className="clinician-page">
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button
          className="cl-btn-secondary"
          type="button"
          onClick={() => window.print()}
        >
          <Printer size={14} /> Print summary
        </button>
      </div>
      <div className="cl-grid-two print-summary">
        {/* Demographics */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><Phone size={15} /> Contact & demographics</h2>
            <button
              className="cl-link"
              type="button"
              onClick={() => navigate(`/clinician/patients/${patientId}/chart`)}
            >
              Edit
            </button>
          </div>
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
        </section>

        {/* Insurance */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title"><Shield size={15} /> Insurance</h2>
          </div>
          {patient.insurance.length === 0 ? (
            <p className="cl-empty-text">No insurance on file.</p>
          ) : (
            <ul className="fact-list">
              {patient.insurance.map((ins) => (
                <li key={ins.id} className="cl-insurance-item">
                  <p className="cl-insurance-type">{ins.type ?? 'Primary'}</p>
                  <p className="cl-insurance-plan">{ins.provider ?? '—'}{ins.planName ? ` · ${ins.planName}` : ''}</p>
                  {ins.policyNumber && <p className="cl-insurance-meta">Policy: {ins.policyNumber}{ins.groupNumber ? ` · Group: ${ins.groupNumber}` : ''}</p>}
                  {ins.relationship && <p className="cl-insurance-meta">Relationship: {ins.relationship}</p>}
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

