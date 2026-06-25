import { useEffect, useState } from 'react'
import { Link, Navigate, Outlet, useLocation, useNavigate, useOutletContext, useParams } from 'react-router-dom'
import { Activity, CalendarClock, FileText, FlaskConical, FolderOpen, Mail, UserCircle, X } from 'lucide-react'
import { getPatientChartSummary, type PatientChartSummary } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

export type PatientOutletContext = {
  session: import('../../auth/session.ts').ClinicianSession
  patient: PatientChartSummary
  patientId: string
  reload: () => void
  signOut: () => void
}

const CHART_TABS = [
  { path: 'summary', label: 'Summary', icon: UserCircle },
  { path: 'chart', label: 'Chart', icon: Activity },
  { path: 'encounters', label: 'Encounters', icon: FileText },
  { path: 'documents', label: 'Documents', icon: FolderOpen },
  { path: 'labs', label: 'Labs', icon: FlaskConical },
  { path: 'appointments', label: 'Appointments', icon: CalendarClock },
  { path: 'messages', label: 'Messages', icon: Mail },
]

export default function PatientShell() {
  const { session, signOut } = useOutletContext<ClinicianOutletContext>()
  const { patientId } = useParams<{ patientId: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const [patient, setPatient] = useState<PatientChartSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  function load() {
    if (!patientId) return
    setLoading(true)
    setError(null)
    getPatientChartSummary(session.sessionId, patientId)
      .then((data) => { setPatient(data); setLoading(false) })
      .catch((err) => { setError(err instanceof Error ? err.message : 'Could not load patient.'); setLoading(false) })
  }

  useEffect(() => { load() }, [patientId]) // eslint-disable-line react-hooks/exhaustive-deps

  if (!patientId) return <Navigate to="/clinician/patients" replace />

  // Determine active tab
  const pathParts = location.pathname.split('/')
  const activeTab = pathParts[pathParts.length - 1]

  // Redirect /clinician/patients/:id → /clinician/patients/:id/summary
  const isPatientRoot = location.pathname === `/clinician/patients/${patientId}`
  if (isPatientRoot) return <Navigate to={`/clinician/patients/${patientId}/summary`} replace />

  const context: PatientOutletContext | null = patient
    ? { session, patient, patientId, reload: load, signOut }
    : null

  return (
    <div className="patient-shell">
      {/* Patient header bar */}
      <div className="patient-header">
        <div className="patient-header-inner">
          <div className="patient-header-identity">
            {loading ? (
              <div className="skeleton-row" style={{ width: 220, height: 18 }} />
            ) : error ? (
              <p className="patient-header-error">{error}</p>
            ) : patient ? (
              <>
                <div className="patient-header-avatar" aria-hidden="true">
                  {patient.firstName[0]?.toUpperCase()}{patient.lastName[0]?.toUpperCase()}
                </div>
                <div>
                  <h2 className="patient-header-name">{patient.displayName}</h2>
                  <p className="patient-header-meta">
                    DOB {patient.dateOfBirth} · {patient.age}y
                    {patient.sex ? ` · ${patient.sex}` : ''}
                    {patient.pubpid ? ` · #${patient.pubpid}` : ''}
                    {patient.primaryProviderName ? ` · ${patient.primaryProviderName}` : ''}
                  </p>
                </div>
              </>
            ) : null}
          </div>
          <button
            className="patient-header-close"
            type="button"
            onClick={() => navigate('/clinician/patients')}
            aria-label="Close patient chart"
          >
            <X size={16} />
          </button>
        </div>

        {/* Chart tab nav */}
        <nav className="patient-tab-nav" aria-label="Chart sections">
          <div className="patient-tab-inner">
            {CHART_TABS.map((tab) => {
              const Icon = tab.icon
              const isActive = activeTab === tab.path
              return (
                <Link
                  key={tab.path}
                  to={`/clinician/patients/${patientId}/${tab.path}`}
                  className={`patient-tab${isActive ? ' patient-tab-active' : ''}`}
                  aria-current={isActive ? 'page' : undefined}
                >
                  <Icon size={15} aria-hidden="true" />
                  {tab.label}
                </Link>
              )
            })}
          </div>
        </nav>
      </div>

      {/* Chart content */}
      <div className="patient-content">
        {loading ? (
          <div className="clinician-page">
            <div className="cl-card">
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 60 }} />)}
              </div>
            </div>
          </div>
        ) : error ? (
          <div className="clinician-page">
            <div className="error-banner">{error}</div>
            <button className="cl-btn-secondary" type="button" onClick={load} style={{ marginTop: 12, width: 'auto' }}>
              Retry
            </button>
          </div>
        ) : context ? (
          <Outlet context={context} />
        ) : null}
      </div>
    </div>
  )
}
