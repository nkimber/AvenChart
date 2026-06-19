import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  Activity,
  Building2,
  CalendarDays,
  ClipboardList,
  FileText,
  FlaskConical,
  HeartPulse,
  Mail,
  Search,
  ShieldCheck,
  Stethoscope,
  UserRound,
  WalletCards,
  type LucideIcon,
} from 'lucide-react'
import {
  getPatientChart,
  searchPatients,
  type PatientChartSummary,
  type PatientListItem,
  type PatientSearchResponse,
} from './api'
import './App.css'

const moduleItems = [
  { label: 'Patient/Client', icon: UserRound, active: true },
  { label: 'Calendar', icon: CalendarDays },
  { label: 'Encounters', icon: Stethoscope },
  { label: 'Lists', icon: ClipboardList },
  { label: 'Fees', icon: WalletCards },
  { label: 'Procedures', icon: FlaskConical },
  { label: 'Messages', icon: Mail },
  { label: 'Reports', icon: FileText },
  { label: 'Admin', icon: ShieldCheck },
]

function App() {
  const [query, setQuery] = useState('Avery')
  const [searchResult, setSearchResult] = useState<PatientSearchResponse | null>(null)
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null)
  const [chart, setChart] = useState<PatientChartSummary | null>(null)
  const [searchStatus, setSearchStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [chartStatus, setChartStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setSearchStatus('loading')
      setError(null)

      try {
        const result = await searchPatients(query, controller.signal)
        setSearchResult(result)
        setSearchStatus('ready')

        if (result.patients.length > 0) {
          setSelectedPatientId((current) => {
            const currentStillVisible = result.patients.some((patient) => patient.canonicalId === current)
            return currentStillVisible ? current : result.patients[0].canonicalId
          })
        } else {
          setSelectedPatientId(null)
          setChart(null)
        }
      } catch (searchError) {
        if (!controller.signal.aborted) {
          setSearchStatus('error')
          setError(searchError instanceof Error ? searchError.message : 'Patient search failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [query])

  useEffect(() => {
    if (!selectedPatientId) {
      setChartStatus('idle')
      setChart(null)
      return
    }

    const controller = new AbortController()
    async function loadChart() {
      setChartStatus('loading')
      try {
        const patient = await getPatientChart(selectedPatientId!, controller.signal)
        setChart(patient)
        setChartStatus('ready')
      } catch (chartError) {
        if (!controller.signal.aborted) {
          setChartStatus('error')
          setError(chartError instanceof Error ? chartError.message : 'Patient chart failed')
        }
      }
    }

    loadChart()
    return () => controller.abort()
  }, [selectedPatientId])

  const selectedFromList = useMemo(
    () => searchResult?.patients.find((patient) => patient.canonicalId === selectedPatientId) ?? null,
    [searchResult, selectedPatientId],
  )

  const activePatient = chart ?? selectedFromList

  return (
    <div className="app-shell">
      <aside className="sidebar" aria-label="Main modules">
        <div className="brand-block">
          <div className="brand-mark">OE</div>
          <div>
            <div className="brand-title">Legacy EHR</div>
            <div className="brand-subtitle">Modernized</div>
          </div>
        </div>

        <nav className="module-nav">
          {moduleItems.map((item) => {
            const Icon = item.icon
            return (
              <button
                className={item.active ? 'module-button active' : 'module-button'}
                type="button"
                key={item.label}
                aria-current={item.active ? 'page' : undefined}
                title={item.label}
              >
                <Icon size={18} strokeWidth={2} />
                <span>{item.label}</span>
              </button>
            )
          })}
        </nav>
      </aside>

      <main className="workspace">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Patient Finder</p>
            <h1>Patient/Client</h1>
          </div>
          <div className="dataset-chip">
            <Activity size={16} />
            <span>{searchResult?.datasetVersion ?? 'v1'} gold dataset</span>
          </div>
        </header>

        <section className="split-layout">
          <section className="finder-panel" aria-label="Patient search">
            <div className="search-box">
              <Search size={18} />
              <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                aria-label="Search patients"
                placeholder="Name, ID, phone, or email"
              />
            </div>

            <div className="result-meta">
              <span>{searchStatus === 'loading' ? 'Searching' : `${searchResult?.totalMatches ?? 0} matches`}</span>
              <span>Limit {searchResult?.limit ?? 25}</span>
            </div>

            {searchStatus === 'error' && <div className="status-banner error">{error}</div>}

            <div className="patient-list">
              {searchResult?.patients.map((patient) => (
                <PatientResult
                  key={patient.canonicalId}
                  patient={patient}
                  selected={patient.canonicalId === selectedPatientId}
                  onSelect={() => setSelectedPatientId(patient.canonicalId)}
                />
              ))}

              {searchStatus === 'ready' && searchResult?.patients.length === 0 && (
                <div className="empty-state">No matching patients</div>
              )}
            </div>
          </section>

          <section className="chart-panel" aria-label="Patient chart summary">
            {activePatient ? (
              <>
                <div className="chart-banner">
                  <div>
                    <p className="eyebrow">Chart Summary</p>
                    <h2>{activePatient.displayName}</h2>
                    <p className="patient-line">
                      {activePatient.pubpid} · PID {activePatient.legacyPid} · {activePatient.sex ?? 'Unknown'} ·{' '}
                      {activePatient.age} years
                    </p>
                  </div>
                  <div className="portal-pill">{chart?.portalEnabled ? 'Portal enabled' : 'Portal pending'}</div>
                </div>

                <div className="chart-grid">
                  <InfoPanel title="Demographics" icon={UserRound}>
                    <Field label="Date of birth" value={activePatient.dateOfBirth} />
                    <Field label="Marital status" value={chart?.maritalStatus} />
                    <Field label="Occupation" value={chart?.occupation} />
                    <Field label="Registered" value={chart?.registrationDate} />
                  </InfoPanel>

                  <InfoPanel title="Contact" icon={Mail}>
                    <Field label="Phone" value={activePatient.phone} />
                    <Field label="Email" value={activePatient.email} />
                    <Field label="Address" value={formatAddress(chart)} />
                    <Field label="Facility" value={activePatient.facilityName} />
                  </InfoPanel>

                  <InfoPanel title="Clinical Activity" icon={HeartPulse}>
                    <MetricRow label="Appointments" value={activePatient.counts.appointments} />
                    <MetricRow label="Encounters" value={activePatient.counts.encounters} />
                    <MetricRow label="Prescriptions" value={activePatient.counts.prescriptions} />
                    <MetricRow label="Lab orders" value={activePatient.counts.labOrders} />
                  </InfoPanel>

                  <InfoPanel title="Chart Signals" icon={ClipboardList}>
                    <MetricRow label="Problems" value={activePatient.counts.problems} />
                    <MetricRow label="Allergies" value={activePatient.counts.allergies} />
                    <MetricRow label="Medications" value={activePatient.counts.medications} />
                    <MetricRow label="Messages" value={activePatient.counts.messages} />
                  </InfoPanel>
                </div>

                <div className="timeline-row">
                  <TimelinePanel
                    title="Next Appointment"
                    icon={CalendarDays}
                    item={chart?.nextAppointment}
                    loading={chartStatus === 'loading'}
                  />
                  <TimelinePanel
                    title="Latest Encounter"
                    icon={Stethoscope}
                    item={chart?.latestEncounter}
                    loading={chartStatus === 'loading'}
                  />
                  <InfoPanel title="Care Team" icon={Building2}>
                    <Field label="Primary provider" value={activePatient.primaryProviderName} />
                    <Field label="Cohort" value={activePatient.cohort} />
                    <Field label="Test purpose" value={activePatient.purpose} />
                  </InfoPanel>
                </div>
              </>
            ) : (
              <div className="empty-chart">Select a patient to open the chart summary</div>
            )}
          </section>
        </section>
      </main>
    </div>
  )
}

function PatientResult({
  patient,
  selected,
  onSelect,
}: {
  patient: PatientListItem
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button type="button" className={selected ? 'patient-result selected' : 'patient-result'} onClick={onSelect}>
      <div className="patient-result-main">
        <span className="patient-name">{patient.displayName}</span>
        <span className="patient-id">{patient.pubpid}</span>
      </div>
      <div className="patient-result-sub">
        <span>{patient.dateOfBirth}</span>
        <span>{patient.phone ?? 'No phone'}</span>
      </div>
      <div className="patient-mini-metrics">
        <span>{patient.counts.encounters} enc</span>
        <span>{patient.counts.appointments} appt</span>
        <span>{patient.counts.prescriptions} rx</span>
      </div>
    </button>
  )
}

function InfoPanel({
  title,
  icon: Icon,
  children,
}: {
  title: string
  icon: LucideIcon
  children: ReactNode
}) {
  return (
    <section className="info-panel">
      <div className="panel-heading">
        <Icon size={17} />
        <h3>{title}</h3>
      </div>
      <div className="panel-body">{children}</div>
    </section>
  )
}

function Field({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="field-row">
      <span>{label}</span>
      <strong>{value || 'Not recorded'}</strong>
    </div>
  )
}

function MetricRow({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function TimelinePanel({
  title,
  icon,
  item,
  loading,
}: {
  title: string
  icon: LucideIcon
  item?: PatientChartSummary['nextAppointment']
  loading: boolean
}) {
  return (
    <InfoPanel title={title} icon={icon}>
      {loading ? (
        <div className="timeline-placeholder">Loading chart detail</div>
      ) : item ? (
        <div className="timeline-item">
          <strong>{item.title}</strong>
          <span>
            {item.date}
            {item.time ? ` at ${item.time}` : ''}
          </span>
          <span>{item.providerName ?? 'Provider not recorded'}</span>
          <span>{item.facilityName ?? item.status ?? 'Status not recorded'}</span>
        </div>
      ) : (
        <div className="timeline-placeholder">No record in this slice</div>
      )}
    </InfoPanel>
  )
}

function formatAddress(chart: PatientChartSummary | null) {
  if (!chart?.street) {
    return null
  }

  return [chart.street, [chart.city, chart.state, chart.postalCode].filter(Boolean).join(' ')]
    .filter(Boolean)
    .join(', ')
}

export default App
