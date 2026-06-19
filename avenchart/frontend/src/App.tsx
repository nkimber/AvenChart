import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  Activity,
  Building2,
  CalendarDays,
  ClipboardList,
  Clock,
  FileText,
  FlaskConical,
  HeartPulse,
  Mail,
  MapPin,
  Search,
  ShieldCheck,
  Stethoscope,
  UserRound,
  WalletCards,
  type LucideIcon,
} from 'lucide-react'
import {
  getAppointmentDetail,
  getAdministrationDirectory,
  getClinicalLists,
  getEncounterDetail,
  getPatientChart,
  getPatientBilling,
  getPatientMessages,
  getProcedureResults,
  getOperationalReports,
  searchAppointments,
  searchEncounters,
  searchPatients,
  type AdministrationDirectoryResponse,
  type AdministrationFacilityItem,
  type AdministrationUserItem,
  type AppointmentDetail,
  type AppointmentListItem,
  type AppointmentSearchResponse,
  type AllergyListItem,
  type BillingEncounterItem,
  type BillingLineItem,
  type ClinicalListsResponse,
  type EncounterDetail,
  type EncounterListItem,
  type EncounterSearchResponse,
  type MedicationListItem,
  type PatientChartSummary,
  type PatientListItem,
  type PatientBillingResponse,
  type PatientMessageItem,
  type PatientMessagesResponse,
  type PatientSearchResponse,
  type OperationalReportsResponse,
  type ProviderActivityReportItem,
  type FacilityActivityReportItem,
  type ClinicalConditionReportItem,
  type ProcedureOrderItem,
  type ProcedureReportItem,
  type ProcedureResultItem,
  type ProcedureResultsResponse,
  type PrescriptionListItem,
  type ProblemListItem,
} from './api'
import './App.css'

type ModuleId =
  | 'patients'
  | 'calendar'
  | 'encounters'
  | 'lists'
  | 'fees'
  | 'procedures'
  | 'messages'
  | 'reports'
  | 'admin'

const moduleItems: Array<{ id: string; label: string; icon: LucideIcon; implemented?: ModuleId }> = [
  { id: 'patients', label: 'Patient/Client', icon: UserRound, implemented: 'patients' },
  { id: 'calendar', label: 'Calendar', icon: CalendarDays, implemented: 'calendar' },
  { id: 'encounters', label: 'Encounters', icon: Stethoscope, implemented: 'encounters' },
  { id: 'lists', label: 'Lists', icon: ClipboardList, implemented: 'lists' },
  { id: 'fees', label: 'Fees', icon: WalletCards, implemented: 'fees' },
  { id: 'procedures', label: 'Procedures', icon: FlaskConical, implemented: 'procedures' },
  { id: 'messages', label: 'Messages', icon: Mail, implemented: 'messages' },
  { id: 'reports', label: 'Reports', icon: FileText, implemented: 'reports' },
  { id: 'admin', label: 'Admin', icon: ShieldCheck, implemented: 'admin' },
]

function App() {
  const [activeModule, setActiveModule] = useState<ModuleId>('patients')

  const [query, setQuery] = useState('Avery')
  const [searchResult, setSearchResult] = useState<PatientSearchResponse | null>(null)
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null)
  const [chart, setChart] = useState<PatientChartSummary | null>(null)
  const [searchStatus, setSearchStatus] = useState<'loading' | 'ready' | 'error'>('loading')
  const [chartStatus, setChartStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [patientError, setPatientError] = useState<string | null>(null)

  const [appointmentPatientId, setAppointmentPatientId] = useState('MOD-PAT-0003')
  const [appointmentFromDate, setAppointmentFromDate] = useState('2026-06-18')
  const [appointmentResult, setAppointmentResult] = useState<AppointmentSearchResponse | null>(null)
  const [selectedAppointmentId, setSelectedAppointmentId] = useState<string | null>(null)
  const [appointmentDetail, setAppointmentDetail] = useState<AppointmentDetail | null>(null)
  const [appointmentStatus, setAppointmentStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [appointmentDetailStatus, setAppointmentDetailStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [appointmentError, setAppointmentError] = useState<string | null>(null)

  const [encounterPatientId, setEncounterPatientId] = useState('MOD-PAT-0001')
  const [encounterFromDate, setEncounterFromDate] = useState('2026-01-01')
  const [encounterResult, setEncounterResult] = useState<EncounterSearchResponse | null>(null)
  const [selectedEncounter, setSelectedEncounter] = useState<number | null>(null)
  const [encounterDetail, setEncounterDetail] = useState<EncounterDetail | null>(null)
  const [encounterStatus, setEncounterStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [encounterDetailStatus, setEncounterDetailStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [encounterError, setEncounterError] = useState<string | null>(null)

  const [clinicalPatientId, setClinicalPatientId] = useState('MOD-PAT-0001')
  const [clinicalLists, setClinicalLists] = useState<ClinicalListsResponse | null>(null)
  const [clinicalStatus, setClinicalStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [clinicalError, setClinicalError] = useState<string | null>(null)

  const [messagePatientId, setMessagePatientId] = useState('MOD-PAT-0004')
  const [patientMessages, setPatientMessages] = useState<PatientMessagesResponse | null>(null)
  const [messageStatus, setMessageStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [messageError, setMessageError] = useState<string | null>(null)

  const [procedurePatientId, setProcedurePatientId] = useState('MOD-PAT-0009')
  const [procedureResults, setProcedureResults] = useState<ProcedureResultsResponse | null>(null)
  const [procedureStatus, setProcedureStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [procedureError, setProcedureError] = useState<string | null>(null)

  const [billingPatientId, setBillingPatientId] = useState('MOD-PAT-0001')
  const [patientBilling, setPatientBilling] = useState<PatientBillingResponse | null>(null)
  const [billingStatus, setBillingStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [billingError, setBillingError] = useState<string | null>(null)

  const [administrationDirectory, setAdministrationDirectory] = useState<AdministrationDirectoryResponse | null>(null)
  const [administrationStatus, setAdministrationStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [administrationError, setAdministrationError] = useState<string | null>(null)

  const [operationalReports, setOperationalReports] = useState<OperationalReportsResponse | null>(null)
  const [reportsStatus, setReportsStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [reportsError, setReportsError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setSearchStatus('loading')
      setPatientError(null)

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
          setPatientError(searchError instanceof Error ? searchError.message : 'Patient search failed')
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
          setPatientError(chartError instanceof Error ? chartError.message : 'Patient chart failed')
        }
      }
    }

    loadChart()
    return () => controller.abort()
  }, [selectedPatientId])

  useEffect(() => {
    if (activeModule !== 'calendar') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setAppointmentStatus('loading')
      setAppointmentError(null)

      try {
        const result = await searchAppointments(appointmentPatientId, appointmentFromDate, controller.signal)
        setAppointmentResult(result)
        setAppointmentStatus('ready')

        if (result.appointments.length > 0) {
          setSelectedAppointmentId((current) => {
            const currentStillVisible = result.appointments.some((appointment) => appointment.id === current)
            return currentStillVisible ? current : result.appointments[0].id
          })
        } else {
          setSelectedAppointmentId(null)
          setAppointmentDetail(null)
        }
      } catch (searchError) {
        if (!controller.signal.aborted) {
          setAppointmentStatus('error')
          setAppointmentError(searchError instanceof Error ? searchError.message : 'Appointment search failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, appointmentPatientId, appointmentFromDate])

  useEffect(() => {
    if (activeModule !== 'calendar' || !selectedAppointmentId) {
      setAppointmentDetailStatus('idle')
      setAppointmentDetail(null)
      return
    }

    const controller = new AbortController()
    async function loadAppointmentDetail() {
      setAppointmentDetailStatus('loading')
      try {
        const detail = await getAppointmentDetail(selectedAppointmentId!, controller.signal)
        setAppointmentDetail(detail)
        setAppointmentDetailStatus('ready')
      } catch (detailError) {
        if (!controller.signal.aborted) {
          setAppointmentDetailStatus('error')
          setAppointmentError(detailError instanceof Error ? detailError.message : 'Appointment detail failed')
        }
      }
    }

    loadAppointmentDetail()
    return () => controller.abort()
  }, [activeModule, selectedAppointmentId])

  useEffect(() => {
    if (activeModule !== 'encounters') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setEncounterStatus('loading')
      setEncounterError(null)

      try {
        const result = await searchEncounters(encounterPatientId, encounterFromDate, controller.signal)
        setEncounterResult(result)
        setEncounterStatus('ready')

        if (result.encounters.length > 0) {
          setSelectedEncounter((current) => {
            const currentStillVisible = result.encounters.some((encounter) => encounter.encounter === current)
            return currentStillVisible ? current : result.encounters[0].encounter
          })
        } else {
          setSelectedEncounter(null)
          setEncounterDetail(null)
        }
      } catch (searchError) {
        if (!controller.signal.aborted) {
          setEncounterStatus('error')
          setEncounterError(searchError instanceof Error ? searchError.message : 'Encounter search failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, encounterPatientId, encounterFromDate])

  useEffect(() => {
    if (activeModule !== 'encounters' || selectedEncounter === null) {
      setEncounterDetailStatus('idle')
      setEncounterDetail(null)
      return
    }

    const controller = new AbortController()
    async function loadEncounterDetail() {
      setEncounterDetailStatus('loading')
      try {
        const detail = await getEncounterDetail(selectedEncounter!, controller.signal)
        setEncounterDetail(detail)
        setEncounterDetailStatus('ready')
      } catch (detailError) {
        if (!controller.signal.aborted) {
          setEncounterDetailStatus('error')
          setEncounterError(detailError instanceof Error ? detailError.message : 'Encounter detail failed')
        }
      }
    }

    loadEncounterDetail()
    return () => controller.abort()
  }, [activeModule, selectedEncounter])

  useEffect(() => {
    if (activeModule !== 'lists') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setClinicalStatus('loading')
      setClinicalError(null)

      try {
        const result = await getClinicalLists(clinicalPatientId, controller.signal)
        setClinicalLists(result)
        setClinicalStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setClinicalStatus('error')
          setClinicalError(loadError instanceof Error ? loadError.message : 'Clinical lists failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, clinicalPatientId])

  useEffect(() => {
    if (activeModule !== 'messages') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setMessageStatus('loading')
      setMessageError(null)

      try {
        const result = await getPatientMessages(messagePatientId, controller.signal)
        setPatientMessages(result)
        setMessageStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setMessageStatus('error')
          setMessageError(loadError instanceof Error ? loadError.message : 'Patient messages failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, messagePatientId])

  useEffect(() => {
    if (activeModule !== 'procedures') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setProcedureStatus('loading')
      setProcedureError(null)

      try {
        const result = await getProcedureResults(procedurePatientId, controller.signal)
        setProcedureResults(result)
        setProcedureStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setProcedureStatus('error')
          setProcedureError(loadError instanceof Error ? loadError.message : 'Procedure results failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, procedurePatientId])

  useEffect(() => {
    if (activeModule !== 'fees') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setBillingStatus('loading')
      setBillingError(null)

      try {
        const result = await getPatientBilling(billingPatientId, controller.signal)
        setPatientBilling(result)
        setBillingStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setBillingStatus('error')
          setBillingError(loadError instanceof Error ? loadError.message : 'Patient billing failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, billingPatientId])

  useEffect(() => {
    if (activeModule !== 'admin') {
      return
    }

    const controller = new AbortController()
    async function loadAdministrationDirectory() {
      setAdministrationStatus('loading')
      setAdministrationError(null)

      try {
        const result = await getAdministrationDirectory(controller.signal)
        setAdministrationDirectory(result)
        setAdministrationStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setAdministrationStatus('error')
          setAdministrationError(loadError instanceof Error ? loadError.message : 'Administration directory failed')
        }
      }
    }

    loadAdministrationDirectory()
    return () => controller.abort()
  }, [activeModule])

  useEffect(() => {
    if (activeModule !== 'reports') {
      return
    }

    const controller = new AbortController()

    async function loadOperationalReports() {
      setReportsStatus('loading')
      setReportsError(null)

      try {
        const result = await getOperationalReports(controller.signal)
        setOperationalReports(result)
        setReportsStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setReportsStatus('error')
          setReportsError(loadError instanceof Error ? loadError.message : 'Operational reports failed')
        }
      }
    }

    loadOperationalReports()
    return () => controller.abort()
  }, [activeModule])

  const selectedFromList = useMemo(
    () => searchResult?.patients.find((patient) => patient.canonicalId === selectedPatientId) ?? null,
    [searchResult, selectedPatientId],
  )

  const activePatient = chart ?? selectedFromList
  const datasetVersion =
    activeModule === 'calendar'
      ? appointmentResult?.datasetVersion ?? searchResult?.datasetVersion
      : activeModule === 'encounters'
        ? encounterResult?.datasetVersion ?? searchResult?.datasetVersion
        : activeModule === 'lists'
          ? clinicalLists?.datasetVersion ?? searchResult?.datasetVersion
          : activeModule === 'fees'
            ? patientBilling?.datasetVersion ?? searchResult?.datasetVersion
          : activeModule === 'procedures'
            ? procedureResults?.datasetVersion ?? searchResult?.datasetVersion
          : activeModule === 'messages'
            ? patientMessages?.datasetVersion ?? searchResult?.datasetVersion
          : activeModule === 'reports'
            ? operationalReports?.datasetVersion ?? searchResult?.datasetVersion
            : activeModule === 'admin'
              ? administrationDirectory?.datasetVersion ?? searchResult?.datasetVersion
              : searchResult?.datasetVersion

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
            const active = item.implemented === activeModule
            return (
              <button
                className={active ? 'module-button active' : item.implemented ? 'module-button' : 'module-button muted'}
                type="button"
                key={item.id}
                aria-current={active ? 'page' : undefined}
                title={item.label}
                disabled={!item.implemented}
                onClick={() => item.implemented && setActiveModule(item.implemented)}
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
            <p className="eyebrow">{moduleEyebrow(activeModule)}</p>
            <h1>{moduleTitle(activeModule)}</h1>
          </div>
          <div className="dataset-chip">
            <Activity size={16} />
            <span>{datasetVersion ?? 'v1'} gold dataset</span>
          </div>
        </header>

        {activeModule === 'patients' && (
          <PatientWorkspace
            query={query}
            searchResult={searchResult}
            selectedPatientId={selectedPatientId}
            activePatient={activePatient}
            chart={chart}
            searchStatus={searchStatus}
            chartStatus={chartStatus}
            error={patientError}
            onQueryChange={setQuery}
            onSelectPatient={setSelectedPatientId}
          />
        )}
        {activeModule === 'calendar' && (
          <CalendarWorkspace
            patientId={appointmentPatientId}
            fromDate={appointmentFromDate}
            searchResult={appointmentResult}
            selectedAppointmentId={selectedAppointmentId}
            appointmentDetail={appointmentDetail}
            searchStatus={appointmentStatus}
            detailStatus={appointmentDetailStatus}
            error={appointmentError}
            onPatientIdChange={setAppointmentPatientId}
            onFromDateChange={setAppointmentFromDate}
            onSelectAppointment={setSelectedAppointmentId}
          />
        )}
        {activeModule === 'encounters' && (
          <EncounterWorkspace
            patientId={encounterPatientId}
            fromDate={encounterFromDate}
            searchResult={encounterResult}
            selectedEncounter={selectedEncounter}
            encounterDetail={encounterDetail}
            searchStatus={encounterStatus}
            detailStatus={encounterDetailStatus}
            error={encounterError}
            onPatientIdChange={setEncounterPatientId}
            onFromDateChange={setEncounterFromDate}
            onSelectEncounter={setSelectedEncounter}
          />
        )}
        {activeModule === 'lists' && (
          <ClinicalListsWorkspace
            patientId={clinicalPatientId}
            clinicalLists={clinicalLists}
            status={clinicalStatus}
            error={clinicalError}
            onPatientIdChange={setClinicalPatientId}
          />
        )}
        {activeModule === 'fees' && (
          <FeesWorkspace
            patientId={billingPatientId}
            patientBilling={patientBilling}
            status={billingStatus}
            error={billingError}
            onPatientIdChange={setBillingPatientId}
          />
        )}
        {activeModule === 'procedures' && (
          <ProceduresWorkspace
            patientId={procedurePatientId}
            procedureResults={procedureResults}
            status={procedureStatus}
            error={procedureError}
            onPatientIdChange={setProcedurePatientId}
          />
        )}
        {activeModule === 'messages' && (
          <MessagesWorkspace
            patientId={messagePatientId}
            patientMessages={patientMessages}
            status={messageStatus}
            error={messageError}
            onPatientIdChange={setMessagePatientId}
          />
        )}
        {activeModule === 'reports' && (
          <ReportsWorkspace
            reports={operationalReports}
            status={reportsStatus}
            error={reportsError}
          />
        )}
        {activeModule === 'admin' && (
          <AdministrationWorkspace
            directory={administrationDirectory}
            status={administrationStatus}
            error={administrationError}
          />
        )}
      </main>
    </div>
  )
}

function moduleEyebrow(moduleId: ModuleId) {
  if (moduleId === 'calendar') {
    return 'Scheduling'
  }
  if (moduleId === 'encounters') {
    return 'Clinical Visits'
  }
  if (moduleId === 'lists') {
    return 'Clinical Lists'
  }
  if (moduleId === 'fees') {
    return 'Billing'
  }
  if (moduleId === 'procedures') {
    return 'Labs And Orders'
  }
  if (moduleId === 'messages') {
    return 'Patient Communications'
  }
  if (moduleId === 'reports') {
    return 'Reports And Exports'
  }
  if (moduleId === 'admin') {
    return 'Administration'
  }
  return 'Patient Finder'
}

function moduleTitle(moduleId: ModuleId) {
  if (moduleId === 'calendar') {
    return 'Calendar'
  }
  if (moduleId === 'encounters') {
    return 'Encounters'
  }
  if (moduleId === 'lists') {
    return 'Lists'
  }
  if (moduleId === 'fees') {
    return 'Fees'
  }
  if (moduleId === 'procedures') {
    return 'Procedures'
  }
  if (moduleId === 'messages') {
    return 'Messages'
  }
  if (moduleId === 'reports') {
    return 'Reports'
  }
  if (moduleId === 'admin') {
    return 'Admin'
  }
  return 'Patient/Client'
}

function PatientWorkspace({
  query,
  searchResult,
  selectedPatientId,
  activePatient,
  chart,
  searchStatus,
  chartStatus,
  error,
  onQueryChange,
  onSelectPatient,
}: {
  query: string
  searchResult: PatientSearchResponse | null
  selectedPatientId: string | null
  activePatient: PatientListItem | PatientChartSummary | null
  chart: PatientChartSummary | null
  searchStatus: 'loading' | 'ready' | 'error'
  chartStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onQueryChange: (value: string) => void
  onSelectPatient: (canonicalId: string) => void
}) {
  return (
    <section className="split-layout">
      <section className="finder-panel" aria-label="Patient search">
        <div className="search-box">
          <Search size={18} />
          <input
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
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
              onSelect={() => onSelectPatient(patient.canonicalId)}
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
                  {activePatient.pubpid} / PID {activePatient.legacyPid} / {activePatient.sex ?? 'Unknown'} /{' '}
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
  )
}

function CalendarWorkspace({
  patientId,
  fromDate,
  searchResult,
  selectedAppointmentId,
  appointmentDetail,
  searchStatus,
  detailStatus,
  error,
  onPatientIdChange,
  onFromDateChange,
  onSelectAppointment,
}: {
  patientId: string
  fromDate: string
  searchResult: AppointmentSearchResponse | null
  selectedAppointmentId: string | null
  appointmentDetail: AppointmentDetail | null
  searchStatus: 'idle' | 'loading' | 'ready' | 'error'
  detailStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onFromDateChange: (value: string) => void
  onSelectAppointment: (appointmentId: string) => void
}) {
  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Appointment search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Appointment patient ID"
              placeholder="MOD-PAT-0003"
            />
          </label>
          <label className="filter-field">
            <span>From</span>
            <input
              value={fromDate}
              onChange={(event) => onFromDateChange(event.target.value)}
              aria-label="Appointment from date"
              type="date"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{searchStatus === 'loading' ? 'Searching' : `${searchResult?.totalMatches ?? 0} appointments`}</span>
          <span>Future schedule</span>
        </div>

        {searchStatus === 'error' && <div className="status-banner error">{error}</div>}

        <div className="appointment-list">
          {searchResult?.appointments.map((appointment) => (
            <AppointmentResult
              key={appointment.id}
              appointment={appointment}
              selected={appointment.id === selectedAppointmentId}
              onSelect={() => onSelectAppointment(appointment.id)}
            />
          ))}

          {searchStatus === 'ready' && searchResult?.appointments.length === 0 && (
            <div className="empty-state">No matching appointments</div>
          )}
        </div>
      </section>

      <section className="appointment-detail-panel" aria-label="Appointment detail">
        {appointmentDetail ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Appointment Detail</p>
                <h2>{appointmentDetail.title}</h2>
                <p className="patient-line">
                  {appointmentDetail.patientDisplayName} / {appointmentDetail.pubpid} / PID {appointmentDetail.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{appointmentDetail.status ?? 'Status pending'}</div>
            </div>

            <div className="appointment-detail-grid">
              <InfoPanel title="Schedule" icon={Clock}>
                <Field label="Date" value={appointmentDetail.date} />
                <Field label="Start time" value={appointmentDetail.startTime} />
                <Field label="Duration" value={`${appointmentDetail.durationMinutes} minutes`} />
                <Field label="Room" value={appointmentDetail.room} />
              </InfoPanel>

              <InfoPanel title="Patient" icon={UserRound}>
                <Field label="Patient ID" value={appointmentDetail.pubpid} />
                <Field label="Date of birth" value={appointmentDetail.dateOfBirth} />
                <Field label="Sex" value={appointmentDetail.sex} />
                <Field label="Test purpose" value={appointmentDetail.patientPurpose} />
              </InfoPanel>

              <InfoPanel title="Care Location" icon={MapPin}>
                <Field label="Provider" value={appointmentDetail.providerName} />
                <Field label="Facility" value={appointmentDetail.facilityName} />
                <Field label="Category" value={appointmentDetail.categoryId} />
                <Field label="Appointment ID" value={appointmentDetail.id} />
              </InfoPanel>
            </div>
          </>
        ) : detailStatus === 'loading' ? (
          <div className="empty-chart">Loading appointment detail</div>
        ) : (
          <div className="empty-chart">Select an appointment to open the schedule detail</div>
        )}
      </section>
    </section>
  )
}

function EncounterWorkspace({
  patientId,
  fromDate,
  searchResult,
  selectedEncounter,
  encounterDetail,
  searchStatus,
  detailStatus,
  error,
  onPatientIdChange,
  onFromDateChange,
  onSelectEncounter,
}: {
  patientId: string
  fromDate: string
  searchResult: EncounterSearchResponse | null
  selectedEncounter: number | null
  encounterDetail: EncounterDetail | null
  searchStatus: 'idle' | 'loading' | 'ready' | 'error'
  detailStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onFromDateChange: (value: string) => void
  onSelectEncounter: (encounter: number) => void
}) {
  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Encounter search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Encounter patient ID"
              placeholder="MOD-PAT-0001"
            />
          </label>
          <label className="filter-field">
            <span>From</span>
            <input
              value={fromDate}
              onChange={(event) => onFromDateChange(event.target.value)}
              aria-label="Encounter from date"
              type="date"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{searchStatus === 'loading' ? 'Searching' : `${searchResult?.totalMatches ?? 0} encounters`}</span>
          <span>Clinical history</span>
        </div>

        {searchStatus === 'error' && <div className="status-banner error">{error}</div>}

        <div className="appointment-list">
          {searchResult?.encounters.map((encounter) => (
            <EncounterResult
              key={encounter.encounter}
              encounter={encounter}
              selected={encounter.encounter === selectedEncounter}
              onSelect={() => onSelectEncounter(encounter.encounter)}
            />
          ))}

          {searchStatus === 'ready' && searchResult?.encounters.length === 0 && (
            <div className="empty-state">No matching encounters</div>
          )}
        </div>
      </section>

      <section className="appointment-detail-panel" aria-label="Encounter detail">
        {encounterDetail ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Encounter Detail</p>
                <h2>{encounterDetail.reason ?? 'Clinical encounter'}</h2>
                <p className="patient-line">
                  {encounterDetail.patientDisplayName} / {encounterDetail.pubpid} / PID {encounterDetail.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{encounterDetail.diagnosisCode ?? 'No code'}</div>
            </div>

            <div className="encounter-detail-grid">
              <InfoPanel title="Visit" icon={Stethoscope}>
                <Field label="Date" value={encounterDetail.date} />
                <Field label="Encounter" value={encounterDetail.encounter} />
                <Field label="Provider" value={encounterDetail.providerName} />
                <Field label="Facility" value={encounterDetail.facilityName} />
              </InfoPanel>

              <InfoPanel title="Assessment" icon={ClipboardList}>
                <Field label="Diagnosis" value={encounterDetail.diagnosisText} />
                <Field label="Billing lines" value={encounterDetail.billingLineCount} />
                <Field label="SOAP note" value={encounterDetail.soapNote ? 'Recorded' : 'Not recorded'} />
                <Field label="Vitals" value={encounterDetail.vitals ? 'Recorded' : 'Not recorded'} />
              </InfoPanel>

              <InfoPanel title="Vitals" icon={HeartPulse}>
                <Field label="Blood Pressure" value={encounterDetail.vitals?.bloodPressure} />
                <Field label="Pulse" value={encounterDetail.vitals?.pulse} />
                <Field label="Respiration" value={encounterDetail.vitals?.respiration} />
                <Field label="Oxygen" value={formatPercent(encounterDetail.vitals?.oxygenSaturation)} />
                <Field label="BMI" value={encounterDetail.vitals?.bmi} />
              </InfoPanel>
            </div>

            <section className="soap-panel" aria-label="SOAP note">
              <div className="panel-heading">
                <FileText size={17} />
                <h3>SOAP Note</h3>
              </div>
              <div className="soap-grid">
                <NoteBlock label="Subjective:" value={encounterDetail.soapNote?.subjective} />
                <NoteBlock label="Objective:" value={encounterDetail.soapNote?.objective} />
                <NoteBlock label="Assessment:" value={encounterDetail.soapNote?.assessment} />
                <NoteBlock label="Plan:" value={encounterDetail.soapNote?.plan} />
              </div>
            </section>
          </>
        ) : detailStatus === 'loading' ? (
          <div className="empty-chart">Loading encounter detail</div>
        ) : (
          <div className="empty-chart">Select an encounter to open the clinical detail</div>
        )}
      </section>
    </section>
  )
}

function ClinicalListsWorkspace({
  patientId,
  clinicalLists,
  status,
  error,
  onPatientIdChange,
}: {
  patientId: string
  clinicalLists: ClinicalListsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
}) {
  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Clinical lists search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Clinical lists patient ID"
              placeholder="MOD-PAT-0001"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Chart lists'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {clinicalLists ? (
          <div className="list-counts">
            <MetricRow label="Problems" value={clinicalLists.problems.length} />
            <MetricRow label="Allergies" value={clinicalLists.allergies.length} />
            <MetricRow label="Medications" value={clinicalLists.medications.length} />
            <MetricRow label="Prescriptions" value={clinicalLists.prescriptions.length} />
          </div>
        ) : (
          <div className="empty-state">No clinical lists loaded</div>
        )}
      </section>

      <section className="appointment-detail-panel" aria-label="Clinical lists detail">
        {clinicalLists ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Clinical Lists</p>
                <h2>{clinicalLists.patientDisplayName}</h2>
                <p className="patient-line">
                  {clinicalLists.pubpid} / PID {clinicalLists.legacyPid}
                </p>
              </div>
              <div className="portal-pill">
                {clinicalLists.problems.length + clinicalLists.allergies.length + clinicalLists.medications.length} active
              </div>
            </div>

            <div className="clinical-list-grid">
              <ProblemPanel items={clinicalLists.problems} />
              <AllergyPanel items={clinicalLists.allergies} />
              <MedicationPanel items={clinicalLists.medications} />
              <PrescriptionPanel items={clinicalLists.prescriptions} />
            </div>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading clinical lists</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load clinical lists</div>
        )}
      </section>
    </section>
  )
}

function FeesWorkspace({
  patientId,
  patientBilling,
  status,
  error,
  onPatientIdChange,
}: {
  patientId: string
  patientBilling: PatientBillingResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
}) {
  const lineCount = countBillingLines(patientBilling?.encounters)
  const totalFee = patientBilling?.encounters.reduce((sum, encounter) => sum + encounter.totalFee, 0) ?? 0

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Fees search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Fees patient ID"
              placeholder="MOD-PAT-0001"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Fee sheet lines'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {patientBilling ? (
          <div className="list-counts">
            <MetricRow label="Encounters" value={patientBilling.encounters.length} />
            <MetricRow label="Billing lines" value={lineCount} />
            <MetricRow label="CPT lines" value={countBillingLinesByType(patientBilling.encounters, 'CPT4')} />
            <MetricRow label="Total fee" value={Math.round(totalFee)} />
          </div>
        ) : (
          <div className="empty-state">No fee sheet loaded</div>
        )}
      </section>

      <section className="appointment-detail-panel" aria-label="Fees detail">
        {patientBilling ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Fee Sheet</p>
                <h2>{patientBilling.patientDisplayName}</h2>
                <p className="patient-line">
                  {patientBilling.pubpid} / PID {patientBilling.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{formatCurrency(totalFee)}</div>
            </div>

            <div className="billing-detail-grid">
              <InfoPanel title="Billing Summary" icon={WalletCards}>
                <Field label="Patient ID" value={patientBilling.pubpid} />
                <Field label="Encounters" value={patientBilling.encounters.length} />
                <Field label="Billing lines" value={lineCount} />
                <Field label="Total fee" value={formatCurrency(totalFee)} />
              </InfoPanel>

              <section className="info-panel billing-lines-panel">
                <div className="panel-heading">
                  <ClipboardList size={17} />
                  <h3>Selected Fee Sheet Codes and Charges</h3>
                </div>
                <div className="billing-encounter-list">
                  {patientBilling.encounters.map((encounter) => (
                    <BillingEncounterCard key={encounter.encounter} encounter={encounter} />
                  ))}
                  {patientBilling.encounters.length === 0 && (
                    <div className="timeline-placeholder">No billing lines recorded</div>
                  )}
                </div>
              </section>
            </div>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading fee sheet</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load fee sheet lines</div>
        )}
      </section>
    </section>
  )
}

function ProceduresWorkspace({
  patientId,
  procedureResults,
  status,
  error,
  onPatientIdChange,
}: {
  patientId: string
  procedureResults: ProcedureResultsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
}) {
  const reportCount = countProcedureReports(procedureResults?.orders)
  const resultCount = countProcedureResults(procedureResults?.orders)
  const finalCount = countProcedureResultsByStatus(procedureResults?.orders, 'final')

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Procedure results search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Procedure patient ID"
              placeholder="MOD-PAT-0009"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Procedure results'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {procedureResults ? (
          <div className="list-counts">
            <MetricRow label="Orders" value={procedureResults.orders.length} />
            <MetricRow label="Reports" value={reportCount} />
            <MetricRow label="Results" value={resultCount} />
            <MetricRow label="Final" value={finalCount} />
          </div>
        ) : (
          <div className="empty-state">No procedure results loaded</div>
        )}
      </section>

      <section className="appointment-detail-panel" aria-label="Procedure results detail">
        {procedureResults ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Procedure Results</p>
                <h2>{procedureResults.patientDisplayName}</h2>
                <p className="patient-line">
                  {procedureResults.pubpid} / PID {procedureResults.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{resultCount} results</div>
            </div>

            <div className="procedure-detail-grid">
              <InfoPanel title="Order Summary" icon={FlaskConical}>
                <Field label="Patient ID" value={procedureResults.pubpid} />
                <Field label="Orders" value={procedureResults.orders.length} />
                <Field label="Reports" value={reportCount} />
                <Field label="Final results" value={finalCount} />
              </InfoPanel>

              <section className="info-panel procedure-orders-panel">
                <div className="panel-heading">
                  <ClipboardList size={17} />
                  <h3>Orders</h3>
                </div>
                <div className="procedure-order-list">
                  {procedureResults.orders.map((order) => (
                    <ProcedureOrderCard key={order.id} order={order} />
                  ))}
                  {procedureResults.orders.length === 0 && (
                    <div className="timeline-placeholder">No procedure orders recorded</div>
                  )}
                </div>
              </section>
            </div>

            <section className="info-panel procedure-results-panel">
              <div className="panel-heading">
                <Activity size={17} />
                <h3>Order Report Results</h3>
              </div>
              <div className="procedure-result-body">
                {procedureResults.orders.map((order) => (
                  <ProcedureReportGroup key={order.id} order={order} />
                ))}
                {procedureResults.orders.length === 0 && (
                  <div className="timeline-placeholder">No report results recorded</div>
                )}
              </div>
            </section>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading procedure results</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load procedure results</div>
        )}
      </section>
    </section>
  )
}

function MessagesWorkspace({
  patientId,
  patientMessages,
  status,
  error,
  onPatientIdChange,
}: {
  patientId: string
  patientMessages: PatientMessagesResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
}) {
  const newCount = countMessagesByStatus(patientMessages?.messages, 'New')
  const doneCount = countMessagesByStatus(patientMessages?.messages, 'Done')

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Messages search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Messages patient ID"
              placeholder="MOD-PAT-0004"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Patient messages'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {patientMessages ? (
          <div className="list-counts">
            <MetricRow label="Messages" value={patientMessages.messages.length} />
            <MetricRow label="New" value={newCount} />
            <MetricRow label="Done" value={doneCount} />
            <MetricRow label="Portal" value={patientMessages.portalEnabled ? 1 : 0} />
          </div>
        ) : (
          <div className="empty-state">No patient messages loaded</div>
        )}
      </section>

      <section className="appointment-detail-panel" aria-label="Messages detail">
        {patientMessages ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Patient Messages</p>
                <h2>{patientMessages.patientDisplayName}</h2>
                <p className="patient-line">
                  {patientMessages.pubpid} / PID {patientMessages.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{patientMessages.portalEnabled ? 'Portal enabled' : 'Portal pending'}</div>
            </div>

            <div className="message-detail-grid">
              <InfoPanel title="Portal Status" icon={Mail}>
                <Field label="Patient ID" value={patientMessages.pubpid} />
                <Field label="Portal access" value={patientMessages.portalEnabled ? 'Enabled' : 'Pending'} />
                <Field label="Open messages" value={newCount} />
                <Field label="Closed messages" value={doneCount} />
              </InfoPanel>

              <section className="info-panel messages-panel">
                <div className="panel-heading">
                  <Mail size={17} />
                  <h3>Messages</h3>
                </div>
                <div className="message-list-body">
                  {patientMessages.messages.map((message) => (
                    <MessageItem key={message.id} message={message} />
                  ))}
                  {patientMessages.messages.length === 0 && (
                    <div className="timeline-placeholder">No messages recorded</div>
                  )}
                </div>
              </section>
            </div>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading patient messages</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load messages</div>
        )}
      </section>
    </section>
  )
}

function ReportsWorkspace({
  reports,
  status,
  error,
}: {
  reports: OperationalReportsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
}) {
  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Reports summary">
        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Operational reports'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {reports ? (
          <>
            <div className="list-counts">
              <MetricRow label="Patients" value={reports.counts.patients} />
              <MetricRow label="Encounters" value={reports.counts.encounters} />
              <MetricRow label="Appointments" value={reports.counts.appointments} />
              <MetricRow label="Billing lines" value={reports.counts.billingLines} />
            </div>

            <div className="access-scope-panel">
              <div className="panel-heading">
                <FileText size={17} />
                <h3>Report Scope</h3>
              </div>
              <Field label="As of" value={reports.asOfDate} />
              <Field label="Current year" value={reports.currentYear} />
              <Field label="Dataset" value={reports.datasetVersion} />
              <Field label="Exports" value="Deferred" />
            </div>
          </>
        ) : (
          <div className="empty-state">No operational reports loaded</div>
        )}
      </section>

      <section className="appointment-detail-panel" aria-label="Operational reports">
        {reports ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Operational Reports</p>
                <h2>Gold Data Snapshot</h2>
                <p className="patient-line">
                  As of {reports.asOfDate} / {reports.counts.providers} providers / {reports.counts.facilities}{' '}
                  facilities
                </p>
              </div>
              <div className="portal-pill">{formatCurrency(reports.counts.billingTotal)}</div>
            </div>

            <div className="reports-detail-grid">
              <InfoPanel title="Activity Summary" icon={Activity}>
                <MetricRow label="Portal patients" value={reports.counts.portalPatients} />
                <MetricRow label="Future appointments" value={reports.counts.futureAppointments} />
                <MetricRow label="Current year appointments" value={reports.counts.currentYearAppointments} />
                <MetricRow label="Current year encounters" value={reports.counts.currentYearEncounters} />
              </InfoPanel>

              <InfoPanel title="Communication And Labs" icon={Mail}>
                <MetricRow label="Messages" value={reports.counts.messages} />
                <MetricRow label="New messages" value={reports.counts.newMessages} />
                <MetricRow label="Done messages" value={reports.counts.doneMessages} />
                <MetricRow label="Lab reports" value={reports.counts.labReports} />
              </InfoPanel>

              <section className="info-panel report-list-panel">
                <div className="panel-heading">
                  <UserRound size={17} />
                  <h3>Provider Activity</h3>
                </div>
                <div className="report-card-list">
                  {reports.providerActivity.map((provider) => (
                    <ProviderReportCard key={provider.username} provider={provider} />
                  ))}
                </div>
              </section>

              <section className="info-panel report-list-panel">
                <div className="panel-heading">
                  <Building2 size={17} />
                  <h3>Facility Activity</h3>
                </div>
                <div className="report-card-list">
                  {reports.facilityActivity.map((facility) => (
                    <FacilityReportCard key={facility.code} facility={facility} />
                  ))}
                </div>
              </section>
            </div>

            <section className="info-panel report-conditions-panel">
              <div className="panel-heading">
                <HeartPulse size={17} />
                <h3>Clinical Conditions</h3>
              </div>
              <div className="condition-report-grid">
                {reports.clinicalConditions.map((condition) => (
                  <ConditionReportCard key={`${condition.diagnosis}-${condition.title}`} condition={condition} />
                ))}
              </div>
            </section>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading operational reports</div>
        ) : (
          <div className="empty-chart">Open Reports to load the operational report dashboard</div>
        )}
      </section>
    </section>
  )
}

function AdministrationWorkspace({
  directory,
  status,
  error,
}: {
  directory: AdministrationDirectoryResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
}) {
  const billingUsers = countUsersByRole(directory?.users, 'billing')
  const frontDeskUsers = countUsersByRole(directory?.users, 'frontdesk')

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Administration summary">
        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Administration directory'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {directory ? (
          <div className="list-counts">
            <MetricRow label="Users" value={directory.counts.users} />
            <MetricRow label="Providers" value={directory.counts.providers} />
            <MetricRow label="Calendar users" value={directory.counts.calendarUsers} />
            <MetricRow label="Facilities" value={directory.counts.facilities} />
          </div>
        ) : (
          <div className="empty-state">No administration directory loaded</div>
        )}

        <div className="access-scope-panel">
          <div className="panel-heading">
            <ShieldCheck size={17} />
            <h3>Access Control Status</h3>
          </div>
          <Field label="Authentication" value="Deferred" />
          <Field label="Authorization" value="Deferred" />
          <Field label="Audit logging" value="Planned" />
          <Field label="Directory mode" value="Read only" />
        </div>
      </section>

      <section className="appointment-detail-panel" aria-label="Administration directory">
        {directory ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Administration Directory</p>
                <h2>Users And Facilities</h2>
                <p className="patient-line">
                  {directory.counts.users} users / {directory.counts.facilities} facilities / {directory.datasetVersion}
                </p>
              </div>
              <div className="portal-pill">{directory.counts.providers} providers</div>
            </div>

            <div className="admin-detail-grid">
              <InfoPanel title="Role Mix" icon={UserRound}>
                <MetricRow label="Providers" value={directory.counts.providers} />
                <MetricRow label="Billing" value={billingUsers} />
                <MetricRow label="Front desk" value={frontDeskUsers} />
                <MetricRow label="Calendar" value={directory.counts.calendarUsers} />
              </InfoPanel>

              <section className="info-panel admin-users-panel">
                <div className="panel-heading">
                  <ShieldCheck size={17} />
                  <h3>Users</h3>
                </div>
                <div className="admin-directory-list">
                  {directory.users.map((user) => (
                    <AdministrationUserCard key={user.id} user={user} />
                  ))}
                </div>
              </section>

              <section className="info-panel admin-facilities-panel">
                <div className="panel-heading">
                  <Building2 size={17} />
                  <h3>Facilities</h3>
                </div>
                <div className="admin-directory-list">
                  {directory.facilities.map((facility) => (
                    <AdministrationFacilityCard key={facility.id} facility={facility} />
                  ))}
                </div>
              </section>
            </div>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading administration directory</div>
        ) : (
          <div className="empty-chart">Open Admin to load the users and facilities directory</div>
        )}
      </section>
    </section>
  )
}

function ProviderReportCard({ provider }: { provider: ProviderActivityReportItem }) {
  return (
    <article className="report-row-card">
      <div className="message-item-header">
        <strong>{provider.displayName}</strong>
        <span className="status-tag">{provider.username}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{provider.encounters} encounters</span>
        <span>{provider.billingLines} billing lines</span>
      </div>
      <div className="procedure-order-meta">
        <span>{formatCurrency(provider.billingTotal)}</span>
        <span>Seeded provider activity</span>
      </div>
    </article>
  )
}

function FacilityReportCard({ facility }: { facility: FacilityActivityReportItem }) {
  return (
    <article className="report-row-card">
      <div className="message-item-header">
        <strong>{facility.name}</strong>
        <span className="status-tag">{facility.code}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{facility.appointments} appointments</span>
        <span>{facility.encounters} encounters</span>
      </div>
      <div className="procedure-order-meta">
        <span>{facility.billingLines} billing lines</span>
        <span>{formatCurrency(facility.billingTotal)}</span>
      </div>
    </article>
  )
}

function ConditionReportCard({ condition }: { condition: ClinicalConditionReportItem }) {
  return (
    <article className="condition-report-card">
      <div>
        <strong>{condition.title}</strong>
        <span>{condition.diagnosis}</span>
      </div>
      <div className="portal-pill">{condition.patients} patients</div>
    </article>
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

function AdministrationUserCard({ user }: { user: AdministrationUserItem }) {
  return (
    <article className="admin-user-card">
      <div className="message-item-header">
        <strong>{user.displayName}</strong>
        <span className="status-tag">{user.role}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{user.username}</span>
        <span>{user.active ? 'Active' : 'Inactive'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{user.facilityName || 'Facility not assigned'}</span>
        <span>{user.calendar ? 'Calendar enabled' : 'No calendar'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{user.email || 'No email'}</span>
        <span>{user.authorized ? 'Authorized provider' : 'Operational user'}</span>
      </div>
    </article>
  )
}

function AdministrationFacilityCard({ facility }: { facility: AdministrationFacilityItem }) {
  return (
    <article className="facility-card">
      <div className="message-item-header">
        <strong>{facility.name}</strong>
        <span className="status-tag">{facility.code}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{facility.phone || 'No phone'}</span>
        <span>{facility.color || 'No color'}</span>
      </div>
      <p>{formatFacilityAddress(facility)}</p>
    </article>
  )
}

function AppointmentResult({
  appointment,
  selected,
  onSelect,
}: {
  appointment: AppointmentListItem
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button type="button" className={selected ? 'appointment-result selected' : 'appointment-result'} onClick={onSelect}>
      <div className="appointment-result-main">
        <span className="patient-name">{appointment.title}</span>
        <span className="status-tag">{appointment.status ?? 'Open'}</span>
      </div>
      <div className="patient-result-sub">
        <span>
          {appointment.date} at {appointment.startTime}
        </span>
        <span>{appointment.durationMinutes} min</span>
      </div>
      <div className="patient-result-sub">
        <span>{appointment.patientDisplayName}</span>
        <span>{appointment.providerName ?? 'Provider not recorded'}</span>
      </div>
    </button>
  )
}

function EncounterResult({
  encounter,
  selected,
  onSelect,
}: {
  encounter: EncounterListItem
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button type="button" className={selected ? 'appointment-result selected' : 'appointment-result'} onClick={onSelect}>
      <div className="appointment-result-main">
        <span className="patient-name">{encounter.reason ?? 'Clinical encounter'}</span>
        <span className="status-tag">{encounter.diagnosisCode ?? 'Dx'}</span>
      </div>
      <div className="patient-result-sub">
        <span>{encounter.date}</span>
        <span>Encounter {encounter.encounter}</span>
      </div>
      <div className="patient-result-sub">
        <span>{encounter.patientDisplayName}</span>
        <span>
          {encounter.hasSoapNote ? 'SOAP' : 'No SOAP'} / {encounter.hasVitals ? 'Vitals' : 'No vitals'}
        </span>
      </div>
    </button>
  )
}

function MessageItem({ message }: { message: PatientMessageItem }) {
  return (
    <article className="message-item">
      <div className="message-item-header">
        <strong>{message.title || 'Patient message'}</strong>
        <span className="status-tag">{message.status || 'Status pending'}</span>
      </div>
      <p>{message.body || 'No message body recorded'}</p>
      <span>{message.date || 'No date'}</span>
    </article>
  )
}

function BillingEncounterCard({ encounter }: { encounter: BillingEncounterItem }) {
  return (
    <article className="billing-encounter-card">
      <div className="procedure-report-title">
        <div>
          <strong>{encounter.reason || 'Billing encounter'}</strong>
          <span>
            {encounter.date} / Encounter {encounter.encounter}
          </span>
        </div>
        <span className="status-tag">{formatCurrency(encounter.totalFee)}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{encounter.providerName || 'Provider not recorded'}</span>
        <span>{encounter.facilityName || 'Facility not recorded'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{encounter.diagnosisCode || 'No diagnosis'}</span>
        <span>{encounter.diagnosisText || 'No diagnosis text'}</span>
      </div>
      <div className="billing-line-list">
        {encounter.lines.map((line) => (
          <BillingLineCard key={line.id} line={line} />
        ))}
        {encounter.lines.length === 0 && <div className="timeline-placeholder">No fee sheet codes recorded</div>}
      </div>
    </article>
  )
}

function BillingLineCard({ line }: { line: BillingLineItem }) {
  return (
    <article className="billing-line-card">
      <div className="message-item-header">
        <strong>{line.code || 'Billing code'}</strong>
        <span className="status-tag">{line.codeType || 'Code type'}</span>
      </div>
      <p>{line.codeText || 'No description recorded'}</p>
      <div className="procedure-order-meta">
        <span>{line.justify ? `Justify ${line.justify}` : 'No justification'}</span>
        <span>{formatCurrency(line.fee)}</span>
      </div>
    </article>
  )
}

function ProcedureOrderCard({ order }: { order: ProcedureOrderItem }) {
  return (
    <article className="procedure-order-card">
      <div className="message-item-header">
        <strong>{order.name || 'Procedure order'}</strong>
        <span className="status-tag">{order.orderStatus || 'Status pending'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{order.code || 'No code'}</span>
        <span>{order.orderDate}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{order.providerName || 'Provider not recorded'}</span>
        <span>{order.encounter ? `Encounter ${order.encounter}` : 'No encounter'}</span>
      </div>
    </article>
  )
}

function ProcedureReportGroup({ order }: { order: ProcedureOrderItem }) {
  return (
    <article className="procedure-report-group">
      <div className="procedure-report-title">
        <div>
          <strong>{order.name || 'Procedure order'}</strong>
          <span>{[order.code, order.diagnosis, order.orderDate].filter(Boolean).join(' / ')}</span>
        </div>
        <span className="status-tag">{order.orderStatus || 'Status pending'}</span>
      </div>

      {order.reports.map((report) => (
        <ProcedureReportCard key={report.id} report={report} />
      ))}
      {order.reports.length === 0 && <div className="timeline-placeholder">No reports recorded for this order</div>}
    </article>
  )
}

function ProcedureReportCard({ report }: { report: ProcedureReportItem }) {
  return (
    <section className="procedure-report-card">
      <div className="procedure-report-title">
        <div>
          <strong>Report {report.id}</strong>
          <span>{report.reportDate}</span>
        </div>
        <span className="status-tag">{report.status || 'Status pending'}</span>
      </div>
      <div className="procedure-result-grid">
        {report.results.map((result) => (
          <ProcedureResultCard key={result.id} result={result} />
        ))}
        {report.results.length === 0 && <div className="timeline-placeholder">No result rows recorded</div>}
      </div>
    </section>
  )
}

function ProcedureResultCard({ result }: { result: ProcedureResultItem }) {
  return (
    <article className="procedure-result-card">
      <div>
        <strong>{result.text || 'Result'}</strong>
        <span className="status-tag">{result.resultStatus || 'Status pending'}</span>
      </div>
      <div className="procedure-result-value">
        <span>{result.result || 'No value'}</span>
        <span>{result.units || 'No units'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{result.code || 'No code'}</span>
        <span>{result.range ? `Range ${result.range}` : 'No range'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{result.abnormal || 'No flag'}</span>
        <span>{result.resultDate}</span>
      </div>
    </article>
  )
}

function ProblemPanel({ items }: { items: ProblemListItem[] }) {
  return (
    <ClinicalSection title="Problems" icon={ClipboardList} emptyText="No active problems">
      {items.map((item) => (
        <ClinicalItem key={item.id} title={item.title} meta={item.diagnosis} date={item.date} note={item.comments} />
      ))}
      {items.length === 0 && <div className="timeline-placeholder">No active problems</div>}
    </ClinicalSection>
  )
}

function AllergyPanel({ items }: { items: AllergyListItem[] }) {
  return (
    <ClinicalSection title="Allergies" icon={ShieldCheck} emptyText="No allergies recorded">
      {items.map((item) => (
        <ClinicalItem
          key={item.id}
          title={item.title}
          meta={[item.reaction, item.severity].filter(Boolean).join(' / ')}
          date={item.date}
          note={item.comments}
        />
      ))}
      {items.length === 0 && <div className="timeline-placeholder">No allergies recorded</div>}
    </ClinicalSection>
  )
}

function MedicationPanel({ items }: { items: MedicationListItem[] }) {
  return (
    <ClinicalSection title="Medication List" icon={HeartPulse} emptyText="No active medications">
      {items.map((item) => (
        <ClinicalItem key={item.id} title={item.title} meta={item.diagnosis} date={item.date} note={item.comments} />
      ))}
      {items.length === 0 && <div className="timeline-placeholder">No active medications</div>}
    </ClinicalSection>
  )
}

function PrescriptionPanel({ items }: { items: PrescriptionListItem[] }) {
  return (
    <ClinicalSection title="Prescriptions" icon={FileText} emptyText="No prescriptions">
      {items.map((item) => (
        <ClinicalItem
          key={item.id}
          title={item.drug}
          meta={[item.dosage, item.route, item.diagnosis].filter(Boolean).join(' / ')}
          date={item.startDate}
          note={item.providerName}
        />
      ))}
      {items.length === 0 && <div className="timeline-placeholder">No prescriptions</div>}
    </ClinicalSection>
  )
}

function ClinicalSection({
  title,
  icon: Icon,
  children,
}: {
  title: string
  icon: LucideIcon
  emptyText: string
  children: ReactNode
}) {
  return (
    <section className="info-panel clinical-section">
      <div className="panel-heading">
        <Icon size={17} />
        <h3>{title}</h3>
      </div>
      <div className="clinical-list-body">{children}</div>
    </section>
  )
}

function ClinicalItem({
  title,
  meta,
  date,
  note,
}: {
  title: string
  meta?: string | null
  date?: string | null
  note?: string | null
}) {
  return (
    <article className="clinical-item">
      <div>
        <strong>{title}</strong>
        <span>{meta || 'No coded detail'}</span>
      </div>
      <div>
        <span>{date || 'No date'}</span>
        <span>{note || 'No note'}</span>
      </div>
    </article>
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

function NoteBlock({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="note-block">
      <strong>{label}</strong>
      <p>{value || 'Not recorded'}</p>
    </div>
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

function formatPercent(value?: number | null) {
  return value === null || value === undefined ? null : `${value}%`
}

function MetricRow({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function countMessagesByStatus(messages: PatientMessageItem[] | undefined, status: string) {
  return messages?.filter((message) => message.status === status).length ?? 0
}

function countBillingLines(encounters: BillingEncounterItem[] | undefined) {
  return encounters?.reduce((count, encounter) => count + encounter.lines.length, 0) ?? 0
}

function countBillingLinesByType(encounters: BillingEncounterItem[] | undefined, codeType: string) {
  return (
    encounters?.reduce(
      (count, encounter) => count + encounter.lines.filter((line) => line.codeType === codeType).length,
      0,
    ) ?? 0
  )
}

function countUsersByRole(users: AdministrationUserItem[] | undefined, role: string) {
  return users?.filter((user) => user.role === role).length ?? 0
}

function countProcedureReports(orders: ProcedureOrderItem[] | undefined) {
  return orders?.reduce((count, order) => count + order.reports.length, 0) ?? 0
}

function countProcedureResults(orders: ProcedureOrderItem[] | undefined) {
  return orders?.reduce((count, order) => count + countReportResults(order.reports), 0) ?? 0
}

function countProcedureResultsByStatus(orders: ProcedureOrderItem[] | undefined, status: string) {
  return (
    orders?.reduce(
      (count, order) =>
        count +
        order.reports.reduce(
          (reportCount, report) =>
            reportCount + report.results.filter((result) => result.resultStatus?.toLowerCase() === status).length,
          0,
        ),
      0,
    ) ?? 0
  )
}

function countReportResults(reports: ProcedureReportItem[]) {
  return reports.reduce((count, report) => count + report.results.length, 0)
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

function formatFacilityAddress(facility: AdministrationFacilityItem) {
  const cityLine = [facility.city, facility.state, facility.postalCode].filter(Boolean).join(' ')
  return [facility.street, cityLine].filter(Boolean).join(', ') || 'No address recorded'
}

function formatCurrency(value?: number | null) {
  return `$${(value ?? 0).toFixed(2)}`
}

export default App
