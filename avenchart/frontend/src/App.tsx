import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import {
  Activity,
  Building2,
  Ban,
  CalendarDays,
  CalendarPlus,
  Check,
  ClipboardList,
  Clock,
  Download,
  FileText,
  FlaskConical,
  FolderOpen,
  HeartPulse,
  Mail,
  MapPin,
  Pencil,
  Search,
  ShieldCheck,
  Stethoscope,
  Trash2,
  UserRound,
  WalletCards,
  X,
  type LucideIcon,
} from 'lucide-react'
import {
  getAppointmentDetail,
  getAdministrationDirectory,
  getClinicalLists,
  getEncounterDetail,
  getPatientChart,
  getPatientBilling,
  getPatientDocumentContent,
  getPatientDocumentDownloadUrl,
  getPatientDocuments,
  getPatientMessages,
  getProcedureResults,
  getOperationalReports,
  getOperationalReportsCsvUrl,
  createAppointment,
  createBillingLine,
  createClinicalAllergy,
  createClinicalPrescription,
  createAdministrationFacility,
  createAdministrationUser,
  createPatientDocument,
  createEncounter,
  createEncounterSoapNote,
  createEncounterVitals,
  createPatientMessage,
  createProcedureOrder,
  createProcedureReport,
  createProcedureResult,
  deleteAppointment,
  deleteAdministrationFacility,
  deleteAdministrationUser,
  deleteBillingLine,
  deleteClinicalAllergy,
  deleteClinicalPrescription,
  deleteEncounter,
  deletePatientDocument,
  deletePatientMessage,
  deleteProcedureOrder,
  deactivateClinicalAllergy,
  deactivateClinicalPrescription,
  grantAdministrationAccessPermission,
  grantAdministrationAccessUserMembership,
  revokeAdministrationAccessPermission,
  revokeAdministrationAccessUserMembership,
  searchAppointments,
  searchEncounters,
  searchPatients,
  softDeletePatientDocument,
  softDeletePatientMessage,
  updateAppointmentStatus,
  updateAdministrationFacility,
  updateAdministrationUser,
  updateBillingLineStatus,
  updateEncounter,
  updatePatientMessageStatus,
  updatePatientContact,
  updateProcedureOrderStatus,
  type AdministrationDirectoryResponse,
  type AdministrationFacilityItem,
  type AdministrationFacilityMutationInput,
  type AdministrationAccessGroupItem,
  type AdministrationAccessPermissionMutationInput,
  type AdministrationAccessGroupPermissionItem,
  type AdministrationAccessUserMembershipItem,
  type AdministrationAccessUserMembershipMutationInput,
  type AdministrationUserItem,
  type AdministrationUserMutationInput,
  type AppointmentDetail,
  type AppointmentCreateInput,
  type AppointmentListItem,
  type AppointmentSearchResponse,
  type AllergyListItem,
  type BillingEncounterItem,
  type BillingLineCreateInput,
  type BillingLineItem,
  type ClinicalListsResponse,
  type ClinicalAllergyCreateInput,
  type ClinicalPrescriptionCreateInput,
  type EncounterCreateInput,
  type EncounterDetail,
  type EncounterSoapNoteCreateInput,
  type EncounterListItem,
  type EncounterSearchResponse,
  type EncounterUpdateInput,
  type EncounterVitalsCreateInput,
  type MedicationListItem,
  type PatientChartSummary,
  type PatientListItem,
  type PatientBillingResponse,
  type PatientContactUpdate,
  type PatientDocumentCreateInput,
  type PatientDocumentContentResponse,
  type PatientDocumentItem,
  type PatientDocumentsResponse,
  type PatientMessageCreateInput,
  type PatientMessageItem,
  type PatientMessagesResponse,
  type PatientSearchResponse,
  type OperationalReportsResponse,
  type ProviderActivityReportItem,
  type FacilityActivityReportItem,
  type ClinicalConditionReportItem,
  type ProcedureOrderCreateInput,
  type ProcedureOrderItem,
  type ProcedureReportCreateInput,
  type ProcedureReportItem,
  type ProcedureResultCreateInput,
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
  | 'documents'
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
  { id: 'documents', label: 'Documents', icon: FolderOpen, implemented: 'documents' },
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
  const [appointmentRefreshKey, setAppointmentRefreshKey] = useState(0)

  const [encounterPatientId, setEncounterPatientId] = useState('MOD-PAT-0001')
  const [encounterFromDate, setEncounterFromDate] = useState('2026-01-01')
  const [encounterResult, setEncounterResult] = useState<EncounterSearchResponse | null>(null)
  const [selectedEncounter, setSelectedEncounter] = useState<number | null>(null)
  const [encounterDetail, setEncounterDetail] = useState<EncounterDetail | null>(null)
  const [encounterStatus, setEncounterStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [encounterDetailStatus, setEncounterDetailStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [encounterError, setEncounterError] = useState<string | null>(null)
  const [encounterRefreshKey, setEncounterRefreshKey] = useState(0)

  const [clinicalPatientId, setClinicalPatientId] = useState('MOD-PAT-0001')
  const [clinicalLists, setClinicalLists] = useState<ClinicalListsResponse | null>(null)
  const [clinicalStatus, setClinicalStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [clinicalError, setClinicalError] = useState<string | null>(null)
  const [clinicalRefreshKey, setClinicalRefreshKey] = useState(0)

  const [messagePatientId, setMessagePatientId] = useState('MOD-PAT-0004')
  const [patientMessages, setPatientMessages] = useState<PatientMessagesResponse | null>(null)
  const [messageStatus, setMessageStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [messageError, setMessageError] = useState<string | null>(null)
  const [messageRefreshKey, setMessageRefreshKey] = useState(0)

  const [documentPatientId, setDocumentPatientId] = useState('MOD-PAT-0001')
  const [patientDocuments, setPatientDocuments] = useState<PatientDocumentsResponse | null>(null)
  const [documentStatus, setDocumentStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [documentError, setDocumentError] = useState<string | null>(null)
  const [documentRefreshKey, setDocumentRefreshKey] = useState(0)

  const [procedurePatientId, setProcedurePatientId] = useState('MOD-PAT-0009')
  const [procedureResults, setProcedureResults] = useState<ProcedureResultsResponse | null>(null)
  const [procedureStatus, setProcedureStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [procedureError, setProcedureError] = useState<string | null>(null)
  const [procedureRefreshKey, setProcedureRefreshKey] = useState(0)

  const [billingPatientId, setBillingPatientId] = useState('MOD-PAT-0001')
  const [patientBilling, setPatientBilling] = useState<PatientBillingResponse | null>(null)
  const [billingStatus, setBillingStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [billingError, setBillingError] = useState<string | null>(null)

  const [administrationDirectory, setAdministrationDirectory] = useState<AdministrationDirectoryResponse | null>(null)
  const [administrationStatus, setAdministrationStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [administrationError, setAdministrationError] = useState<string | null>(null)
  const [administrationRefreshKey, setAdministrationRefreshKey] = useState(0)

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
  }, [activeModule, appointmentPatientId, appointmentFromDate, appointmentRefreshKey])

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
  }, [activeModule, encounterPatientId, encounterFromDate, encounterRefreshKey])

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
  }, [activeModule, clinicalPatientId, clinicalRefreshKey])

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
  }, [activeModule, messagePatientId, messageRefreshKey])

  useEffect(() => {
    if (activeModule !== 'documents') {
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setDocumentStatus('loading')
      setDocumentError(null)

      try {
        const result = await getPatientDocuments(documentPatientId, controller.signal)
        setPatientDocuments(result)
        setDocumentStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setDocumentStatus('error')
          setDocumentError(loadError instanceof Error ? loadError.message : 'Patient documents failed')
        }
      }
    }, 180)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [activeModule, documentPatientId, documentRefreshKey])

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
  }, [activeModule, procedurePatientId, procedureRefreshKey])

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
  }, [activeModule, administrationRefreshKey])

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

  async function handlePatientContactSave(patientId: string, contact: PatientContactUpdate) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const updated = await updatePatientContact(patientId, contact)
      setChart(updated)
      setChartStatus('ready')
      setSearchResult((current) => {
        if (!current) {
          return current
        }

        return {
          ...current,
          patients: current.patients.map((patient) =>
            patient.canonicalId === updated.canonicalId
              ? {
                  ...patient,
                  phone: updated.phone,
                  phoneHome: updated.phoneHome,
                  phoneCell: updated.phoneCell,
                  email: updated.email,
                }
              : patient,
          ),
        }
      })
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient contact save failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handleAppointmentCreate(input: AppointmentCreateInput) {
    setAppointmentStatus('loading')
    setAppointmentError(null)

    try {
      const created = await createAppointment(input)
      setAppointmentPatientId(created.patientId)
      setAppointmentFromDate(created.date)
      setSelectedAppointmentId(created.id)
      setAppointmentDetail(created)
      setAppointmentDetailStatus('ready')
      setAppointmentStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return created
    } catch (createError) {
      setAppointmentStatus('error')
      const message = createError instanceof Error ? createError.message : 'Appointment create failed'
      setAppointmentError(message)
      throw createError
    }
  }

  async function handleAppointmentCancel(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const updated = await updateAppointmentStatus(appointment.id, {
        status: 'x',
        title: appointment.title.endsWith('Cancelled') ? appointment.title : `${appointment.title} Cancelled`,
      })
      setAppointmentDetail(updated)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (cancelError) {
      setAppointmentDetailStatus('error')
      const message = cancelError instanceof Error ? cancelError.message : 'Appointment cancel failed'
      setAppointmentError(message)
      throw cancelError
    }
  }

  async function handleAppointmentDelete(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      await deleteAppointment(appointment.id)
      setSelectedAppointmentId(null)
      setAppointmentDetail(null)
      setAppointmentDetailStatus('idle')
      setAppointmentRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setAppointmentDetailStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Appointment delete failed'
      setAppointmentError(message)
      throw deleteError
    }
  }

  async function handleEncounterCreate(input: EncounterCreateInput) {
    setEncounterStatus('loading')
    setEncounterError(null)

    try {
      const created = await createEncounter(input)
      setEncounterPatientId(created.patientId)
      setEncounterFromDate(created.date)
      setSelectedEncounter(created.encounter)
      setEncounterDetail(created)
      setEncounterDetailStatus('ready')
      setEncounterStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return created
    } catch (createError) {
      setEncounterStatus('error')
      const message = createError instanceof Error ? createError.message : 'Encounter create failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterUpdate(encounter: EncounterDetail, update: EncounterUpdateInput) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const updated = await updateEncounter(encounter.encounter, update)
      setEncounterDetail(updated)
      setSelectedEncounter(updated.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return updated
    } catch (updateError) {
      setEncounterDetailStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Encounter update failed'
      setEncounterError(message)
      throw updateError
    }
  }

  async function handleEncounterDelete(encounter: EncounterDetail) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      await deleteEncounter(encounter.encounter)
      setSelectedEncounter(null)
      setEncounterDetail(null)
      setEncounterDetailStatus('idle')
      setEncounterRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setEncounterDetailStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Encounter delete failed'
      setEncounterError(message)
      throw deleteError
    }
  }

  async function handleEncounterVitalsCreate(
    encounter: EncounterDetail,
    input: EncounterVitalsCreateInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const response = await createEncounterVitals(encounter.encounter, input)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (vitalsError) {
      setEncounterDetailStatus('error')
      const message = vitalsError instanceof Error ? vitalsError.message : 'Encounter vitals save failed'
      setEncounterError(message)
      throw vitalsError
    }
  }

  async function handleEncounterSoapCreate(
    encounter: EncounterDetail,
    input: EncounterSoapNoteCreateInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const response = await createEncounterSoapNote(encounter.encounter, input)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (soapError) {
      setEncounterDetailStatus('error')
      const message = soapError instanceof Error ? soapError.message : 'Encounter SOAP note save failed'
      setEncounterError(message)
      throw soapError
    }
  }

  async function handleClinicalAllergyCreate(input: ClinicalAllergyCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const response = await createClinicalAllergy(input)
      setClinicalPatientId(response.detail.patientId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setClinicalStatus('error')
      const message = createError instanceof Error ? createError.message : 'Clinical allergy create failed'
      setClinicalError(message)
      throw createError
    }
  }

  async function handleClinicalAllergyDeactivate(allergy: AllergyListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const response = await deactivateClinicalAllergy(allergy.id, {
        comments: 'Deactivated from the modernized Lists workspace.',
      })
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (deactivateError) {
      setClinicalStatus('error')
      const message = deactivateError instanceof Error ? deactivateError.message : 'Clinical allergy deactivate failed'
      setClinicalError(message)
      throw deactivateError
    }
  }

  async function handleClinicalAllergyDelete(allergy: AllergyListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      await deleteClinicalAllergy(allergy.id)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId)
      setClinicalLists(refreshed)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setClinicalStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Clinical allergy delete failed'
      setClinicalError(message)
      throw deleteError
    }
  }

  async function handleClinicalPrescriptionCreate(input: ClinicalPrescriptionCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const response = await createClinicalPrescription(input)
      setClinicalPatientId(response.detail.patientId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setClinicalStatus('error')
      const message = createError instanceof Error ? createError.message : 'Clinical prescription create failed'
      setClinicalError(message)
      throw createError
    }
  }

  async function handleClinicalPrescriptionDeactivate(prescription: PrescriptionListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const response = await deactivateClinicalPrescription(prescription.id, {
        endDate: '2026-08-15',
        note: 'Deactivated from the modernized Lists workspace.',
      })
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (deactivateError) {
      setClinicalStatus('error')
      const message = deactivateError instanceof Error ? deactivateError.message : 'Clinical prescription deactivate failed'
      setClinicalError(message)
      throw deactivateError
    }
  }

  async function handleClinicalPrescriptionDelete(prescription: PrescriptionListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      await deleteClinicalPrescription(prescription.id)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId)
      setClinicalLists(refreshed)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setClinicalStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Clinical prescription delete failed'
      setClinicalError(message)
      throw deleteError
    }
  }

  async function handleBillingLineCreate(input: BillingLineCreateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const response = await createBillingLine(input)
      setBillingPatientId(response.detail.patientId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (createError) {
      setBillingStatus('error')
      const message = createError instanceof Error ? createError.message : 'Billing line create failed'
      setBillingError(message)
      throw createError
    }
  }

  async function handleBillingLineDeactivate(line: BillingLineItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const response = await updateBillingLineStatus(line.id, {
        billed: 1,
        activity: 0,
      })
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (statusError) {
      setBillingStatus('error')
      const message = statusError instanceof Error ? statusError.message : 'Billing line status update failed'
      setBillingError(message)
      throw statusError
    }
  }

  async function handleBillingLineDelete(line: BillingLineItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      await deleteBillingLine(line.id)
      const refreshed = await getPatientBilling(patientBilling?.patientId ?? billingPatientId)
      setPatientBilling(refreshed)
      setBillingStatus('ready')
    } catch (deleteError) {
      setBillingStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Billing line delete failed'
      setBillingError(message)
      throw deleteError
    }
  }

  async function handleProcedureOrderCreate(input: ProcedureOrderCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const response = await createProcedureOrder(input)
      setProcedurePatientId(response.detail.patientId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setProcedureStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure order create failed'
      setProcedureError(message)
      throw createError
    }
  }

  async function handleProcedureOrderComplete(order: ProcedureOrderItem) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const response = await updateProcedureOrderStatus(order.id, { status: 'complete' })
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (statusError) {
      setProcedureStatus('error')
      const message = statusError instanceof Error ? statusError.message : 'Procedure order status update failed'
      setProcedureError(message)
      throw statusError
    }
  }

  async function handleProcedureReportCreate(input: ProcedureReportCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const response = await createProcedureReport(input)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setProcedureStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure report create failed'
      setProcedureError(message)
      throw createError
    }
  }

  async function handleProcedureResultCreate(input: ProcedureResultCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const response = await createProcedureResult(input)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setProcedureStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure result create failed'
      setProcedureError(message)
      throw createError
    }
  }

  async function handleProcedureOrderDelete(order: ProcedureOrderItem) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      await deleteProcedureOrder(order.id)
      const refreshed = await getProcedureResults(procedureResults?.patientId ?? procedurePatientId)
      setProcedureResults(refreshed)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setProcedureStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Procedure order delete failed'
      setProcedureError(message)
      throw deleteError
    }
  }

  async function handleAdministrationUserCreate(input: AdministrationUserMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await createAdministrationUser(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setAdministrationStatus('error')
      const message = createError instanceof Error ? createError.message : 'Administration user create failed'
      setAdministrationError(message)
      throw createError
    }
  }

  async function handleAdministrationUserUpdate(
    user: AdministrationUserItem,
    input: AdministrationUserMutationInput,
  ) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await updateAdministrationUser(user.id, input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setAdministrationStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Administration user update failed'
      setAdministrationError(message)
      throw updateError
    }
  }

  async function handleAdministrationUserDelete(user: AdministrationUserItem) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      await deleteAdministrationUser(user.id)
      const refreshed = await getAdministrationDirectory()
      setAdministrationDirectory(refreshed)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setAdministrationStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Administration user delete failed'
      setAdministrationError(message)
      throw deleteError
    }
  }

  async function handleAdministrationFacilityCreate(input: AdministrationFacilityMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await createAdministrationFacility(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setAdministrationStatus('error')
      const message = createError instanceof Error ? createError.message : 'Administration facility create failed'
      setAdministrationError(message)
      throw createError
    }
  }

  async function handleAdministrationFacilityUpdate(
    facility: AdministrationFacilityItem,
    input: AdministrationFacilityMutationInput,
  ) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await updateAdministrationFacility(facility.id, input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setAdministrationStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Administration facility update failed'
      setAdministrationError(message)
      throw updateError
    }
  }

  async function handleAdministrationFacilityDelete(facility: AdministrationFacilityItem) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      await deleteAdministrationFacility(facility.id)
      const refreshed = await getAdministrationDirectory()
      setAdministrationDirectory(refreshed)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setAdministrationStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Administration facility delete failed'
      setAdministrationError(message)
      throw deleteError
    }
  }

  async function handleAdministrationAccessPermissionGrant(input: AdministrationAccessPermissionMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await grantAdministrationAccessPermission(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (grantError) {
      setAdministrationStatus('error')
      const message = grantError instanceof Error ? grantError.message : 'Administration access permission grant failed'
      setAdministrationError(message)
      throw grantError
    }
  }

  async function handleAdministrationAccessPermissionRevoke(input: AdministrationAccessPermissionMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await revokeAdministrationAccessPermission(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (revokeError) {
      setAdministrationStatus('error')
      const message = revokeError instanceof Error ? revokeError.message : 'Administration access permission revoke failed'
      setAdministrationError(message)
      throw revokeError
    }
  }

  async function handleAdministrationAccessUserMembershipGrant(input: AdministrationAccessUserMembershipMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await grantAdministrationAccessUserMembership(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (grantError) {
      setAdministrationStatus('error')
      const message = grantError instanceof Error ? grantError.message : 'Administration access membership grant failed'
      setAdministrationError(message)
      throw grantError
    }
  }

  async function handleAdministrationAccessUserMembershipRevoke(input: AdministrationAccessUserMembershipMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const response = await revokeAdministrationAccessUserMembership(input)
      setAdministrationDirectory(response.detail)
      setAdministrationStatus('ready')
      setAdministrationRefreshKey((current) => current + 1)
      return response
    } catch (revokeError) {
      setAdministrationStatus('error')
      const message = revokeError instanceof Error ? revokeError.message : 'Administration access membership revoke failed'
      setAdministrationError(message)
      throw revokeError
    }
  }

  async function handlePatientMessageCreate(input: PatientMessageCreateInput) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const response = await createPatientMessage(input)
      setMessagePatientId(response.detail.patientId)
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setMessageStatus('error')
      const message = createError instanceof Error ? createError.message : 'Patient message create failed'
      setMessageError(message)
      throw createError
    }
  }

  async function handlePatientMessageClose(message: PatientMessageItem) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const response = await updatePatientMessageStatus(message.id, {
        status: 'Done',
        body: message.body?.startsWith('Closed from')
          ? message.body
          : 'Closed from the modernized Messages workspace.',
      })
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (closeError) {
      setMessageStatus('error')
      const messageText = closeError instanceof Error ? closeError.message : 'Patient message close failed'
      setMessageError(messageText)
      throw closeError
    }
  }

  async function handlePatientMessageArchive(message: PatientMessageItem) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const response = await softDeletePatientMessage(message.id)
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (archiveError) {
      setMessageStatus('error')
      const messageText = archiveError instanceof Error ? archiveError.message : 'Patient message archive failed'
      setMessageError(messageText)
      throw archiveError
    }
  }

  async function handlePatientMessageDelete(message: PatientMessageItem) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      await deletePatientMessage(message.id)
      const refreshed = await getPatientMessages(patientMessages?.patientId ?? messagePatientId)
      setPatientMessages(refreshed)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setMessageStatus('error')
      const messageText = deleteError instanceof Error ? deleteError.message : 'Patient message delete failed'
      setMessageError(messageText)
      throw deleteError
    }
  }

  async function handlePatientDocumentCreate(input: PatientDocumentCreateInput) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const response = await createPatientDocument(input)
      setDocumentPatientId(response.detail.patientId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setDocumentStatus('error')
      const message = createError instanceof Error ? createError.message : 'Patient document create failed'
      setDocumentError(message)
      throw createError
    }
  }

  async function handlePatientDocumentArchive(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const response = await softDeletePatientDocument(document.id)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (archiveError) {
      setDocumentStatus('error')
      const message = archiveError instanceof Error ? archiveError.message : 'Patient document archive failed'
      setDocumentError(message)
      throw archiveError
    }
  }

  async function handlePatientDocumentDelete(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      await deletePatientDocument(document.id)
      const refreshed = await getPatientDocuments(patientDocuments?.patientId ?? documentPatientId)
      setPatientDocuments(refreshed)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setDocumentStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Patient document delete failed'
      setDocumentError(message)
      throw deleteError
    }
  }

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
            onSaveContact={handlePatientContactSave}
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
            onCreateAppointment={handleAppointmentCreate}
            onCancelAppointment={handleAppointmentCancel}
            onDeleteAppointment={handleAppointmentDelete}
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
            onCreateEncounter={handleEncounterCreate}
            onUpdateEncounter={handleEncounterUpdate}
            onDeleteEncounter={handleEncounterDelete}
            onCreateVitals={handleEncounterVitalsCreate}
            onCreateSoapNote={handleEncounterSoapCreate}
          />
        )}
        {activeModule === 'lists' && (
          <ClinicalListsWorkspace
            patientId={clinicalPatientId}
            clinicalLists={clinicalLists}
            status={clinicalStatus}
            error={clinicalError}
            onPatientIdChange={setClinicalPatientId}
            onCreateAllergy={handleClinicalAllergyCreate}
            onDeactivateAllergy={handleClinicalAllergyDeactivate}
            onDeleteAllergy={handleClinicalAllergyDelete}
            onCreatePrescription={handleClinicalPrescriptionCreate}
            onDeactivatePrescription={handleClinicalPrescriptionDeactivate}
            onDeletePrescription={handleClinicalPrescriptionDelete}
          />
        )}
        {activeModule === 'fees' && (
          <FeesWorkspace
            patientId={billingPatientId}
            patientBilling={patientBilling}
            status={billingStatus}
            error={billingError}
            onPatientIdChange={setBillingPatientId}
            onCreateLine={handleBillingLineCreate}
            onDeactivateLine={handleBillingLineDeactivate}
            onDeleteLine={handleBillingLineDelete}
          />
        )}
        {activeModule === 'procedures' && (
          <ProceduresWorkspace
            patientId={procedurePatientId}
            procedureResults={procedureResults}
            status={procedureStatus}
            error={procedureError}
            onPatientIdChange={setProcedurePatientId}
            onCreateOrder={handleProcedureOrderCreate}
            onCompleteOrder={handleProcedureOrderComplete}
            onCreateReport={handleProcedureReportCreate}
            onCreateResult={handleProcedureResultCreate}
            onDeleteOrder={handleProcedureOrderDelete}
          />
        )}
        {activeModule === 'messages' && (
          <MessagesWorkspace
            patientId={messagePatientId}
            patientMessages={patientMessages}
            status={messageStatus}
            error={messageError}
            onPatientIdChange={setMessagePatientId}
            onCreateMessage={handlePatientMessageCreate}
            onCloseMessage={handlePatientMessageClose}
            onArchiveMessage={handlePatientMessageArchive}
            onDeleteMessage={handlePatientMessageDelete}
          />
        )}
        {activeModule === 'documents' && (
          <DocumentsWorkspace
            patientId={documentPatientId}
            patientDocuments={patientDocuments}
            status={documentStatus}
            error={documentError}
            onPatientIdChange={setDocumentPatientId}
            onCreateDocument={handlePatientDocumentCreate}
            onArchiveDocument={handlePatientDocumentArchive}
            onDeleteDocument={handlePatientDocumentDelete}
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
            onCreateUser={handleAdministrationUserCreate}
            onUpdateUser={handleAdministrationUserUpdate}
            onDeleteUser={handleAdministrationUserDelete}
            onCreateFacility={handleAdministrationFacilityCreate}
            onUpdateFacility={handleAdministrationFacilityUpdate}
            onDeleteFacility={handleAdministrationFacilityDelete}
            onGrantAccessPermission={handleAdministrationAccessPermissionGrant}
            onRevokeAccessPermission={handleAdministrationAccessPermissionRevoke}
            onGrantAccessMembership={handleAdministrationAccessUserMembershipGrant}
            onRevokeAccessMembership={handleAdministrationAccessUserMembershipRevoke}
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
  if (moduleId === 'documents') {
    return 'Patient Files'
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
  if (moduleId === 'documents') {
    return 'Documents'
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
  onSaveContact,
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
  onSaveContact: (canonicalId: string, contact: PatientContactUpdate) => Promise<void>
}) {
  const [isEditingContact, setIsEditingContact] = useState(false)
  const [contactDraft, setContactDraft] = useState<PatientContactUpdate>(() => buildContactDraft(null))
  const [contactSaveStatus, setContactSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    setContactDraft(buildContactDraft(chart ?? activePatient))
    setIsEditingContact(false)
    setContactSaveStatus('idle')
  }, [activePatient, chart])

  function updateDraft(field: keyof PatientContactUpdate, value: string) {
    setContactDraft((current) => ({ ...current, [field]: value }))
  }

  async function handleContactSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!chart) {
      return
    }

    setContactSaveStatus('saving')
    try {
      await onSaveContact(chart.canonicalId, contactDraft)
      setIsEditingContact(false)
      setContactSaveStatus('saved')
    } catch {
      setContactSaveStatus('error')
    }
  }

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
                {isEditingContact && chart ? (
                  <form className="contact-form" onSubmit={handleContactSubmit}>
                    <label className="contact-field">
                      <span>Home phone</span>
                      <input
                        value={contactDraft.phoneHome}
                        onChange={(event) => updateDraft('phoneHome', event.target.value)}
                        aria-label="Home phone"
                      />
                    </label>
                    <label className="contact-field">
                      <span>Cell phone</span>
                      <input
                        value={contactDraft.phoneCell}
                        onChange={(event) => updateDraft('phoneCell', event.target.value)}
                        aria-label="Cell phone"
                      />
                    </label>
                    <label className="contact-field">
                      <span>Email</span>
                      <input
                        value={contactDraft.email}
                        onChange={(event) => updateDraft('email', event.target.value)}
                        aria-label="Email"
                      />
                    </label>
                    <div className="contact-toggle-row">
                      <label className="contact-toggle">
                        <input
                          type="checkbox"
                          checked={contactDraft.hipaaAllowSms === 'YES'}
                          onChange={(event) => updateDraft('hipaaAllowSms', event.target.checked ? 'YES' : 'NO')}
                        />
                        <span>SMS allowed</span>
                      </label>
                      <label className="contact-toggle">
                        <input
                          type="checkbox"
                          checked={contactDraft.hipaaAllowEmail === 'YES'}
                          onChange={(event) => updateDraft('hipaaAllowEmail', event.target.checked ? 'YES' : 'NO')}
                        />
                        <span>Email allowed</span>
                      </label>
                    </div>
                    <div className="contact-actions">
                      <button className="icon-text-button primary" type="submit" disabled={contactSaveStatus === 'saving'}>
                        <Check size={15} />
                        <span>{contactSaveStatus === 'saving' ? 'Saving' : 'Save'}</span>
                      </button>
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setContactDraft(buildContactDraft(chart))
                          setIsEditingContact(false)
                          setContactSaveStatus('idle')
                        }}
                      >
                        <X size={15} />
                        <span>Cancel</span>
                      </button>
                    </div>
                  </form>
                ) : (
                  <>
                    <Field label="Home phone" value={chart?.phoneHome ?? activePatient.phone} />
                    <Field label="Cell phone" value={chart?.phoneCell ?? activePatient.phoneCell} />
                    <Field label="Email" value={activePatient.email} />
                    <Field label="Address" value={formatAddress(chart)} />
                    <Field label="Facility" value={activePatient.facilityName} />
                    <Field label="SMS permission" value={chart?.hipaaAllowSms} />
                    <Field label="Email permission" value={chart?.hipaaAllowEmail} />
                    <div className="contact-actions">
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setContactDraft(buildContactDraft(chart ?? activePatient))
                          setIsEditingContact(true)
                          setContactSaveStatus('idle')
                        }}
                        disabled={!chart}
                      >
                        <Pencil size={15} />
                        <span>Edit contact</span>
                      </button>
                      {contactSaveStatus === 'saved' && <span className="save-note">Saved</span>}
                      {contactSaveStatus === 'error' && <span className="save-note error">Save failed</span>}
                    </div>
                  </>
                )}
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
  onCreateAppointment,
  onCancelAppointment,
  onDeleteAppointment,
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
  onCreateAppointment: (input: AppointmentCreateInput) => Promise<AppointmentDetail>
  onCancelAppointment: (appointment: AppointmentDetail) => Promise<AppointmentDetail>
  onDeleteAppointment: (appointment: AppointmentDetail) => Promise<void>
}) {
  const [draftTitle, setDraftTitle] = useState('Parity Appointment')
  const [draftDate, setDraftDate] = useState('2026-10-15')
  const [draftStartTime, setDraftStartTime] = useState('10:30')
  const [draftDuration, setDraftDuration] = useState('30')
  const [draftRoom, setDraftRoom] = useState('Parity')
  const [mutationStatus, setMutationStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  async function handleCreateSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationStatus('saving')

    try {
      await onCreateAppointment({
        patientId,
        title: draftTitle,
        date: draftDate,
        startTime: draftStartTime,
        durationMinutes: Number(draftDuration),
        room: draftRoom,
        categoryId: 9,
      })
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleCancelSelected() {
    if (!appointmentDetail) {
      return
    }

    setMutationStatus('saving')
    try {
      await onCancelAppointment(appointmentDetail)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleDeleteSelected() {
    if (!appointmentDetail) {
      return
    }

    setMutationStatus('saving')
    try {
      await onDeleteAppointment(appointmentDetail)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

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

        <form className="appointment-mutation-panel" onSubmit={handleCreateSubmit} aria-label="Create appointment">
          <div className="panel-heading compact-heading">
            <CalendarPlus size={17} />
            <h3>Create Appointment</h3>
          </div>
          <label className="contact-field">
            <span>Title</span>
            <input
              value={draftTitle}
              onChange={(event) => setDraftTitle(event.target.value)}
              aria-label="Appointment title"
            />
          </label>
          <div className="mutation-grid two-column">
            <label className="contact-field">
              <span>Date</span>
              <input
                type="date"
                value={draftDate}
                onChange={(event) => setDraftDate(event.target.value)}
                aria-label="New appointment date"
              />
            </label>
            <label className="contact-field">
              <span>Start</span>
              <input
                type="time"
                value={draftStartTime}
                onChange={(event) => setDraftStartTime(event.target.value)}
                aria-label="New appointment start time"
              />
            </label>
          </div>
          <div className="mutation-grid two-column">
            <label className="contact-field">
              <span>Minutes</span>
              <input
                type="number"
                min="5"
                step="5"
                value={draftDuration}
                onChange={(event) => setDraftDuration(event.target.value)}
                aria-label="New appointment duration"
              />
            </label>
            <label className="contact-field">
              <span>Room</span>
              <input value={draftRoom} onChange={(event) => setDraftRoom(event.target.value)} aria-label="New appointment room" />
            </label>
          </div>
          <div className="contact-actions">
            <button className="icon-text-button primary" type="submit" disabled={mutationStatus === 'saving'}>
              <CalendarPlus size={15} />
              <span>{mutationStatus === 'saving' ? 'Saving' : 'Create'}</span>
            </button>
            {mutationStatus === 'saved' && <span className="save-note">Saved</span>}
            {mutationStatus === 'error' && <span className="save-note error">Action failed</span>}
          </div>
        </form>

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

            <div className="detail-actions">
              <button
                className="icon-text-button"
                type="button"
                onClick={handleCancelSelected}
                disabled={detailStatus === 'loading' || appointmentDetail.status === 'x'}
              >
                <Ban size={15} />
                <span>Cancel appointment</span>
              </button>
              <button
                className="icon-text-button danger"
                type="button"
                onClick={handleDeleteSelected}
                disabled={detailStatus === 'loading'}
              >
                <Trash2 size={15} />
                <span>Delete appointment</span>
              </button>
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
  onCreateEncounter,
  onUpdateEncounter,
  onDeleteEncounter,
  onCreateVitals,
  onCreateSoapNote,
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
  onCreateEncounter: (input: EncounterCreateInput) => Promise<EncounterDetail>
  onUpdateEncounter: (encounter: EncounterDetail, update: EncounterUpdateInput) => Promise<EncounterDetail>
  onDeleteEncounter: (encounter: EncounterDetail) => Promise<void>
  onCreateVitals: (encounter: EncounterDetail, input: EncounterVitalsCreateInput) => Promise<unknown>
  onCreateSoapNote: (encounter: EncounterDetail, input: EncounterSoapNoteCreateInput) => Promise<unknown>
}) {
  const [createPatientId, setCreatePatientId] = useState(patientId)
  const [createDateTime, setCreateDateTime] = useState('2026-06-18T10:00')
  const [createReason, setCreateReason] = useState('Follow-up encounter')
  const [createProviderId, setCreateProviderId] = useState('')
  const [createFacilityId, setCreateFacilityId] = useState('10')
  const [createBillingFacilityId, setCreateBillingFacilityId] = useState('10')
  const [createBillingNote, setCreateBillingNote] = useState('Created from modernized encounter workspace.')
  const [createStatus, setCreateStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const [summaryReason, setSummaryReason] = useState('')
  const [summaryBillingNote, setSummaryBillingNote] = useState('')
  const [summaryStatus, setSummaryStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const [vitalsDateTime, setVitalsDateTime] = useState('2026-06-18T10:05')
  const [vitalsSystolic, setVitalsSystolic] = useState('128')
  const [vitalsDiastolic, setVitalsDiastolic] = useState('76')
  const [vitalsWeight, setVitalsWeight] = useState('186')
  const [vitalsHeight, setVitalsHeight] = useState('70')
  const [vitalsPulse, setVitalsPulse] = useState('72')
  const [vitalsOxygen, setVitalsOxygen] = useState('98')
  const [vitalsNote, setVitalsNote] = useState('Parity vitals detail.')
  const [vitalsStatus, setVitalsStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const [soapDateTime, setSoapDateTime] = useState('2026-06-18T10:10')
  const [soapSubjective, setSoapSubjective] = useState('Patient reports symptoms are stable.')
  const [soapObjective, setSoapObjective] = useState('Vitals reviewed.')
  const [soapAssessment, setSoapAssessment] = useState('Stable clinical condition.')
  const [soapPlan, setSoapPlan] = useState('Continue validation plan.')
  const [soapStatus, setSoapStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    setCreatePatientId(patientId)
  }, [patientId])

  useEffect(() => {
    if (!encounterDetail) {
      setSummaryReason('')
      setSummaryBillingNote('')
      return
    }

    setSummaryReason(encounterDetail.reason ?? '')
    setSummaryBillingNote(encounterDetail.billingNote ?? '')
    setVitalsDateTime(`${encounterDetail.date}T10:05`)
    setSoapDateTime(`${encounterDetail.date}T10:10`)
  }, [encounterDetail])

  async function handleCreateSubmit(event: FormEvent) {
    event.preventDefault()
    setCreateStatus('saving')

    try {
      await onCreateEncounter({
        patientId: createPatientId,
        providerId: numberOrNull(createProviderId),
        dateTime: createDateTime,
        reason: createReason,
        facilityId: numberOrNull(createFacilityId),
        billingFacilityId: numberOrNull(createBillingFacilityId),
        billingNote: createBillingNote,
      })
      setCreateStatus('saved')
    } catch {
      setCreateStatus('error')
    }
  }

  async function handleSummarySubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setSummaryStatus('saving')
    try {
      await onUpdateEncounter(encounterDetail, {
        reason: summaryReason,
        billingNote: summaryBillingNote,
      })
      setSummaryStatus('saved')
    } catch {
      setSummaryStatus('error')
    }
  }

  async function handleDeleteClick() {
    if (!encounterDetail) {
      return
    }

    setSummaryStatus('saving')
    try {
      await onDeleteEncounter(encounterDetail)
      setSummaryStatus('saved')
    } catch {
      setSummaryStatus('error')
    }
  }

  async function handleVitalsSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setVitalsStatus('saving')
    try {
      await onCreateVitals(encounterDetail, {
        dateTime: vitalsDateTime,
        systolic: numberOrNull(vitalsSystolic),
        diastolic: numberOrNull(vitalsDiastolic),
        weight: numberOrNull(vitalsWeight),
        height: numberOrNull(vitalsHeight),
        pulse: numberOrNull(vitalsPulse),
        oxygenSaturation: numberOrNull(vitalsOxygen),
        note: vitalsNote,
      })
      setVitalsStatus('saved')
    } catch {
      setVitalsStatus('error')
    }
  }

  async function handleSoapSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setSoapStatus('saving')
    try {
      await onCreateSoapNote(encounterDetail, {
        dateTime: soapDateTime,
        subjective: soapSubjective,
        objective: soapObjective,
        assessment: soapAssessment,
        plan: soapPlan,
      })
      setSoapStatus('saved')
    } catch {
      setSoapStatus('error')
    }
  }

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

        <form className="appointment-mutation-panel" onSubmit={handleCreateSubmit} aria-label="Create encounter">
          <div className="panel-heading compact-heading">
            <Stethoscope size={16} />
            <h3>New Encounter</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Patient ID</span>
              <input
                value={createPatientId}
                onChange={(event) => setCreatePatientId(event.target.value)}
                aria-label="New visit patient ID"
              />
            </label>
            <label className="filter-field">
              <span>Date Time</span>
              <input
                value={createDateTime}
                onChange={(event) => setCreateDateTime(event.target.value)}
                aria-label="New encounter date time"
                type="datetime-local"
              />
            </label>
            <label className="filter-field">
              <span>Reason</span>
              <input
                value={createReason}
                onChange={(event) => setCreateReason(event.target.value)}
                aria-label="New encounter reason"
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Provider</span>
                <input
                  value={createProviderId}
                  onChange={(event) => setCreateProviderId(event.target.value)}
                  aria-label="New encounter provider ID"
                  inputMode="numeric"
                />
              </label>
              <label className="filter-field">
                <span>Facility</span>
                <input
                  value={createFacilityId}
                  onChange={(event) => setCreateFacilityId(event.target.value)}
                  aria-label="New encounter facility ID"
                  inputMode="numeric"
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Billing Facility</span>
              <input
                value={createBillingFacilityId}
                onChange={(event) => setCreateBillingFacilityId(event.target.value)}
                aria-label="New encounter billing facility ID"
                inputMode="numeric"
              />
            </label>
            <label className="filter-field">
              <span>Billing Note</span>
              <input
                value={createBillingNote}
                onChange={(event) => setCreateBillingNote(event.target.value)}
                aria-label="New encounter billing note"
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={createStatus === 'saving'}>
              <CalendarPlus size={16} />
              <span>{createStatus === 'saving' ? 'Saving' : 'Create'}</span>
            </button>
            {createStatus === 'saved' && <span className="save-note">Saved</span>}
            {createStatus === 'error' && <span className="save-note error">Action failed</span>}
          </div>
        </form>
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
                <Field label="Billing note" value={encounterDetail.billingNote} />
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

            <form className="appointment-mutation-panel" onSubmit={handleSummarySubmit} aria-label="Update encounter">
              <div className="panel-heading compact-heading">
                <Pencil size={16} />
                <h3>Encounter Summary</h3>
              </div>
              <div className="mutation-grid">
                <label className="filter-field">
                  <span>Reason</span>
                  <input
                    value={summaryReason}
                    onChange={(event) => setSummaryReason(event.target.value)}
                    aria-label="Encounter reason"
                  />
                </label>
                <label className="filter-field">
                  <span>Billing Note</span>
                  <input
                    value={summaryBillingNote}
                    onChange={(event) => setSummaryBillingNote(event.target.value)}
                    aria-label="Encounter billing note"
                  />
                </label>
              </div>
              <div className="detail-actions">
                <button className="icon-text-button primary" type="submit" disabled={summaryStatus === 'saving'}>
                  <Check size={16} />
                  <span>{summaryStatus === 'saving' ? 'Saving' : 'Update'}</span>
                </button>
                <button
                  className="icon-text-button danger"
                  type="button"
                  onClick={handleDeleteClick}
                  disabled={summaryStatus === 'saving'}
                >
                  <Trash2 size={16} />
                  <span>Delete</span>
                </button>
                {summaryStatus === 'saved' && <span className="save-note">Saved</span>}
                {summaryStatus === 'error' && <span className="save-note error">Action failed</span>}
              </div>
            </form>

            <form className="appointment-mutation-panel" onSubmit={handleVitalsSubmit} aria-label="Record vitals">
              <div className="panel-heading compact-heading">
                <HeartPulse size={16} />
                <h3>Vitals</h3>
              </div>
              <div className="mutation-grid">
                <label className="filter-field">
                  <span>Date Time</span>
                  <input
                    value={vitalsDateTime}
                    onChange={(event) => setVitalsDateTime(event.target.value)}
                    aria-label="Encounter vitals date time"
                    type="datetime-local"
                  />
                </label>
                <div className="mutation-grid two-column">
                  <label className="filter-field">
                    <span>Systolic</span>
                    <input
                      value={vitalsSystolic}
                      onChange={(event) => setVitalsSystolic(event.target.value)}
                      aria-label="Encounter systolic"
                      inputMode="numeric"
                    />
                  </label>
                  <label className="filter-field">
                    <span>Diastolic</span>
                    <input
                      value={vitalsDiastolic}
                      onChange={(event) => setVitalsDiastolic(event.target.value)}
                      aria-label="Encounter diastolic"
                      inputMode="numeric"
                    />
                  </label>
                </div>
                <div className="mutation-grid two-column">
                  <label className="filter-field">
                    <span>Weight</span>
                    <input
                      value={vitalsWeight}
                      onChange={(event) => setVitalsWeight(event.target.value)}
                      aria-label="Encounter weight"
                      inputMode="decimal"
                    />
                  </label>
                  <label className="filter-field">
                    <span>Height</span>
                    <input
                      value={vitalsHeight}
                      onChange={(event) => setVitalsHeight(event.target.value)}
                      aria-label="Encounter height"
                      inputMode="decimal"
                    />
                  </label>
                </div>
                <div className="mutation-grid two-column">
                  <label className="filter-field">
                    <span>Pulse</span>
                    <input
                      value={vitalsPulse}
                      onChange={(event) => setVitalsPulse(event.target.value)}
                      aria-label="Encounter pulse"
                      inputMode="numeric"
                    />
                  </label>
                  <label className="filter-field">
                    <span>Oxygen</span>
                    <input
                      value={vitalsOxygen}
                      onChange={(event) => setVitalsOxygen(event.target.value)}
                      aria-label="Encounter oxygen"
                      inputMode="numeric"
                    />
                  </label>
                </div>
                <label className="filter-field">
                  <span>Note</span>
                  <input
                    value={vitalsNote}
                    onChange={(event) => setVitalsNote(event.target.value)}
                    aria-label="Encounter vitals note"
                  />
                </label>
              </div>
              <div className="detail-actions">
                <button className="icon-text-button primary" type="submit" disabled={vitalsStatus === 'saving'}>
                  <HeartPulse size={16} />
                  <span>{vitalsStatus === 'saving' ? 'Saving' : 'Record'}</span>
                </button>
                {vitalsStatus === 'saved' && <span className="save-note">Saved</span>}
                {vitalsStatus === 'error' && <span className="save-note error">Action failed</span>}
              </div>
            </form>

            <form className="appointment-mutation-panel" onSubmit={handleSoapSubmit} aria-label="Record SOAP note">
              <div className="panel-heading compact-heading">
                <FileText size={16} />
                <h3>SOAP Entry</h3>
              </div>
              <div className="mutation-grid">
                <label className="filter-field">
                  <span>Date Time</span>
                  <input
                    value={soapDateTime}
                    onChange={(event) => setSoapDateTime(event.target.value)}
                    aria-label="SOAP date time"
                    type="datetime-local"
                  />
                </label>
                <label className="filter-field">
                  <span>Subjective</span>
                  <textarea
                    value={soapSubjective}
                    onChange={(event) => setSoapSubjective(event.target.value)}
                    aria-label="SOAP subjective"
                    rows={2}
                  />
                </label>
                <label className="filter-field">
                  <span>Objective</span>
                  <textarea
                    value={soapObjective}
                    onChange={(event) => setSoapObjective(event.target.value)}
                    aria-label="SOAP objective"
                    rows={2}
                  />
                </label>
                <label className="filter-field">
                  <span>Assessment</span>
                  <textarea
                    value={soapAssessment}
                    onChange={(event) => setSoapAssessment(event.target.value)}
                    aria-label="SOAP assessment"
                    rows={2}
                  />
                </label>
                <label className="filter-field">
                  <span>Plan</span>
                  <textarea
                    value={soapPlan}
                    onChange={(event) => setSoapPlan(event.target.value)}
                    aria-label="SOAP plan"
                    rows={2}
                  />
                </label>
              </div>
              <div className="detail-actions">
                <button className="icon-text-button primary" type="submit" disabled={soapStatus === 'saving'}>
                  <FileText size={16} />
                  <span>{soapStatus === 'saving' ? 'Saving' : 'Record'}</span>
                </button>
                {soapStatus === 'saved' && <span className="save-note">Saved</span>}
                {soapStatus === 'error' && <span className="save-note error">Action failed</span>}
              </div>
            </form>
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
  onCreateAllergy,
  onDeactivateAllergy,
  onDeleteAllergy,
  onCreatePrescription,
  onDeactivatePrescription,
  onDeletePrescription,
}: {
  patientId: string
  clinicalLists: ClinicalListsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onCreateAllergy: (input: ClinicalAllergyCreateInput) => Promise<unknown>
  onDeactivateAllergy: (allergy: AllergyListItem) => Promise<unknown>
  onDeleteAllergy: (allergy: AllergyListItem) => Promise<void>
  onCreatePrescription: (input: ClinicalPrescriptionCreateInput) => Promise<unknown>
  onDeactivatePrescription: (prescription: PrescriptionListItem) => Promise<unknown>
  onDeletePrescription: (prescription: PrescriptionListItem) => Promise<void>
}) {
  const [allergyTitle, setAllergyTitle] = useState('Parity Allergy')
  const [allergyDate, setAllergyDate] = useState('2026-06-18 09:00:00')
  const [allergyReaction, setAllergyReaction] = useState('Rash')
  const [allergySeverity, setAllergySeverity] = useState('mild')
  const [allergyComments, setAllergyComments] = useState('Created from the modernized Lists workspace.')
  const [prescriptionDrug, setPrescriptionDrug] = useState('Parity Medication')
  const [prescriptionStartDate, setPrescriptionStartDate] = useState('2026-07-15')
  const [prescriptionDosage, setPrescriptionDosage] = useState('1 tablet daily')
  const [prescriptionQuantity, setPrescriptionQuantity] = useState('30')
  const [prescriptionRefills, setPrescriptionRefills] = useState('1')
  const [prescriptionDiagnosis, setPrescriptionDiagnosis] = useState('Z00.00')
  const [prescriptionNote, setPrescriptionNote] = useState('Created from the modernized Lists workspace.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const isLoading = status === 'loading'

  async function handleAllergySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateAllergy({
      patientId,
      title: allergyTitle,
      dateTime: allergyDate,
      comments: allergyComments,
      reaction: allergyReaction,
      severity: allergySeverity,
      listOptionId: 'parity-allergy',
    })

    setMutationMessage('Allergy saved')
  }

  async function handlePrescriptionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreatePrescription({
      patientId,
      startDate: prescriptionStartDate,
      drug: prescriptionDrug,
      rxNormCode: '1049502',
      dosage: prescriptionDosage,
      quantity: prescriptionQuantity,
      route: 'oral',
      refills: Number(prescriptionRefills || 0),
      note: prescriptionNote,
      diagnosis: prescriptionDiagnosis,
    })

    setMutationMessage('Prescription saved')
  }

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
          <span>Allergy and Rx lifecycles</span>
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

        <form className="appointment-mutation-panel" onSubmit={handleAllergySubmit}>
          <div className="panel-heading compact-heading">
            <ShieldCheck size={16} />
            <h3>New Allergy</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Title</span>
              <input
                value={allergyTitle}
                onChange={(event) => setAllergyTitle(event.target.value)}
                aria-label="New allergy title"
                required
              />
            </label>
            <label className="filter-field">
              <span>Date</span>
              <input
                value={allergyDate}
                onChange={(event) => setAllergyDate(event.target.value)}
                aria-label="New allergy date"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Reaction</span>
                <input
                  value={allergyReaction}
                  onChange={(event) => setAllergyReaction(event.target.value)}
                  aria-label="New allergy reaction"
                />
              </label>
              <label className="filter-field">
                <span>Severity</span>
                <input
                  value={allergySeverity}
                  onChange={(event) => setAllergySeverity(event.target.value)}
                  aria-label="New allergy severity"
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Comments</span>
              <textarea
                value={allergyComments}
                onChange={(event) => setAllergyComments(event.target.value)}
                aria-label="New allergy comments"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading}>
              <Check size={15} />
              Save Allergy
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handlePrescriptionSubmit}>
          <div className="panel-heading compact-heading">
            <FileText size={16} />
            <h3>New Prescription</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Drug</span>
              <input
                value={prescriptionDrug}
                onChange={(event) => setPrescriptionDrug(event.target.value)}
                aria-label="New prescription drug"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Start</span>
                <input
                  value={prescriptionStartDate}
                  onChange={(event) => setPrescriptionStartDate(event.target.value)}
                  aria-label="New prescription start date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Refills</span>
                <input
                  value={prescriptionRefills}
                  onChange={(event) => setPrescriptionRefills(event.target.value)}
                  aria-label="New prescription refills"
                  inputMode="numeric"
                  required
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Dosage</span>
                <input
                  value={prescriptionDosage}
                  onChange={(event) => setPrescriptionDosage(event.target.value)}
                  aria-label="New prescription dosage"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Quantity</span>
                <input
                  value={prescriptionQuantity}
                  onChange={(event) => setPrescriptionQuantity(event.target.value)}
                  aria-label="New prescription quantity"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Diagnosis</span>
              <input
                value={prescriptionDiagnosis}
                onChange={(event) => setPrescriptionDiagnosis(event.target.value)}
                aria-label="New prescription diagnosis"
              />
            </label>
            <label className="filter-field">
              <span>Note</span>
              <textarea
                value={prescriptionNote}
                onChange={(event) => setPrescriptionNote(event.target.value)}
                aria-label="New prescription note"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading}>
              <Check size={15} />
              Save Rx
            </button>
          </div>
        </form>
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
              <AllergyPanel
                items={clinicalLists.allergies}
                onDeactivate={onDeactivateAllergy}
                onDelete={onDeleteAllergy}
                disabled={isLoading}
              />
              <MedicationPanel items={clinicalLists.medications} />
              <PrescriptionPanel
                items={clinicalLists.prescriptions}
                onDeactivate={onDeactivatePrescription}
                onDelete={onDeletePrescription}
                disabled={isLoading}
              />
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
  onCreateLine,
  onDeactivateLine,
  onDeleteLine,
}: {
  patientId: string
  patientBilling: PatientBillingResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onCreateLine: (input: BillingLineCreateInput) => Promise<unknown>
  onDeactivateLine: (line: BillingLineItem) => Promise<unknown>
  onDeleteLine: (line: BillingLineItem) => Promise<void>
}) {
  const [billingEncounter, setBillingEncounter] = useState('')
  const [billingDate, setBillingDate] = useState('2026-06-18')
  const [billingCode, setBillingCode] = useState('99213')
  const [billingCodeText, setBillingCodeText] = useState('Established patient office visit')
  const [billingFee, setBillingFee] = useState('125.00')
  const [billingUnits, setBillingUnits] = useState('1')
  const [billingJustify, setBillingJustify] = useState('Z00.00')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const lineCount = countBillingLines(patientBilling?.encounters)
  const totalFee = patientBilling?.encounters.reduce((sum, encounter) => sum + encounter.totalFee, 0) ?? 0
  const isLoading = status === 'loading'

  useEffect(() => {
    if (!patientBilling || patientBilling.encounters.length === 0) {
      return
    }

    const currentEncounterExists = patientBilling.encounters.some(
      (encounter) => String(encounter.encounter) === billingEncounter,
    )
    if (!currentEncounterExists) {
      setBillingEncounter(String(patientBilling.encounters[0].encounter))
    }
  }, [billingEncounter, patientBilling])

  async function handleBillingLineSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateLine({
      patientId,
      encounter: Number(billingEncounter),
      billingDate,
      codeType: 'CPT4',
      code: billingCode,
      codeText: billingCodeText,
      fee: Number(billingFee),
      units: Number(billingUnits),
      justify: billingJustify,
    })

    setMutationMessage('Billing line saved')
  }

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
          <span>Billing lifecycle</span>
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

        <form className="appointment-mutation-panel" onSubmit={handleBillingLineSubmit}>
          <div className="panel-heading compact-heading">
            <WalletCards size={16} />
            <h3>New CPT Line</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={billingEncounter}
                onChange={(event) => setBillingEncounter(event.target.value)}
                aria-label="New billing encounter"
                inputMode="numeric"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Date</span>
                <input
                  value={billingDate}
                  onChange={(event) => setBillingDate(event.target.value)}
                  aria-label="New billing date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Units</span>
                <input
                  value={billingUnits}
                  onChange={(event) => setBillingUnits(event.target.value)}
                  aria-label="New billing units"
                  inputMode="numeric"
                  required
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>CPT</span>
                <input
                  value={billingCode}
                  onChange={(event) => setBillingCode(event.target.value)}
                  aria-label="New billing CPT code"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Fee</span>
                <input
                  value={billingFee}
                  onChange={(event) => setBillingFee(event.target.value)}
                  aria-label="New billing fee"
                  inputMode="decimal"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Description</span>
              <input
                value={billingCodeText}
                onChange={(event) => setBillingCodeText(event.target.value)}
                aria-label="New billing description"
                required
              />
            </label>
            <label className="filter-field">
              <span>Justify</span>
              <input
                value={billingJustify}
                onChange={(event) => setBillingJustify(event.target.value)}
                aria-label="New billing justification"
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={isLoading || !patientBilling || patientBilling.encounters.length === 0}
            >
              <Check size={15} />
              Save CPT
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>
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
                    <BillingEncounterCard
                      key={encounter.encounter}
                      encounter={encounter}
                      disabled={isLoading}
                      onDeactivateLine={onDeactivateLine}
                      onDeleteLine={onDeleteLine}
                    />
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
  onCreateOrder,
  onCompleteOrder,
  onCreateReport,
  onCreateResult,
  onDeleteOrder,
}: {
  patientId: string
  procedureResults: ProcedureResultsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onCreateOrder: (input: ProcedureOrderCreateInput) => Promise<unknown>
  onCompleteOrder: (order: ProcedureOrderItem) => Promise<unknown>
  onCreateReport: (input: ProcedureReportCreateInput) => Promise<unknown>
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
  onDeleteOrder: (order: ProcedureOrderItem) => Promise<void>
}) {
  const [procedureEncounter, setProcedureEncounter] = useState('')
  const [procedureDate, setProcedureDate] = useState('2026-06-18')
  const [procedureCode, setProcedureCode] = useState('80053')
  const [procedureName, setProcedureName] = useState('Comprehensive metabolic panel')
  const [procedureDiagnosis, setProcedureDiagnosis] = useState('Z00.00')
  const [procedureInstructions, setProcedureInstructions] = useState('Collect fasting sample.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const procedureCounts = procedureResults?.counts
  const scheduledOrders = procedureResults?.orders.filter(isScheduledProcedureOrder) ?? []
  const reportlessOrders = procedureResults?.orders.filter((order) => order.reports.length === 0) ?? []
  const reportCount = procedureCounts?.reports ?? countProcedureReports(procedureResults?.orders)
  const resultCount = procedureCounts?.results ?? countProcedureResults(procedureResults?.orders)
  const finalCount = procedureCounts?.finalResults ?? countProcedureResultsByStatus(procedureResults?.orders, 'final')
  const scheduledCount = procedureCounts?.scheduledOrders ?? scheduledOrders.length
  const reportlessCount = procedureCounts?.reportlessOrders ?? reportlessOrders.length
  const futureScheduledCount = procedureCounts?.futureScheduledOrders ?? scheduledOrders.length
  const isLoading = status === 'loading'

  useEffect(() => {
    if (!procedureResults || procedureResults.orders.length === 0) {
      return
    }

    const encounters = new Set(
      procedureResults.orders
        .map((order) => order.encounter)
        .filter((encounter): encounter is number => typeof encounter === 'number'),
    )
    if (!procedureEncounter || !encounters.has(Number(procedureEncounter))) {
      const firstEncounter = procedureResults.orders.find((order) => order.encounter)?.encounter
      if (firstEncounter) {
        setProcedureEncounter(String(firstEncounter))
      }
    }
  }, [procedureEncounter, procedureResults])

  async function handleOrderSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateOrder({
      patientId,
      encounterId: Number(procedureEncounter),
      dateOrdered: procedureDate,
      priority: 'routine',
      status: 'pending',
      procedureCode,
      procedureName,
      procedureType: 'laboratory',
      diagnosis: procedureDiagnosis,
      instructions: procedureInstructions,
    })

    setMutationMessage('Procedure order saved')
  }

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
          <span>Lab lifecycle</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {procedureResults ? (
          <div className="list-counts">
            <MetricRow label="Orders" value={procedureCounts?.orders ?? procedureResults.orders.length} />
            <MetricRow label="Scheduled" value={scheduledCount} />
            <MetricRow label="Reportless" value={reportlessCount} />
            <MetricRow label="Reports" value={reportCount} />
            <MetricRow label="Results" value={resultCount} />
            <MetricRow label="Final" value={finalCount} />
          </div>
        ) : (
          <div className="empty-state">No procedure results loaded</div>
        )}

        <form className="appointment-mutation-panel" onSubmit={handleOrderSubmit}>
          <div className="panel-heading compact-heading">
            <FlaskConical size={16} />
            <h3>New Lab Order</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={procedureEncounter}
                onChange={(event) => setProcedureEncounter(event.target.value)}
                aria-label="New procedure encounter"
                inputMode="numeric"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Date</span>
                <input
                  value={procedureDate}
                  onChange={(event) => setProcedureDate(event.target.value)}
                  aria-label="New procedure date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Code</span>
                <input
                  value={procedureCode}
                  onChange={(event) => setProcedureCode(event.target.value)}
                  aria-label="New procedure code"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Name</span>
              <input
                value={procedureName}
                onChange={(event) => setProcedureName(event.target.value)}
                aria-label="New procedure name"
                required
              />
            </label>
            <label className="filter-field">
              <span>Diagnosis</span>
              <input
                value={procedureDiagnosis}
                onChange={(event) => setProcedureDiagnosis(event.target.value)}
                aria-label="New procedure diagnosis"
                required
              />
            </label>
            <label className="filter-field">
              <span>Instructions</span>
              <input
                value={procedureInstructions}
                onChange={(event) => setProcedureInstructions(event.target.value)}
                aria-label="New procedure instructions"
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={isLoading || !procedureResults || !procedureEncounter}
            >
              <Check size={15} />
              Save Order
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>
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
                <Field label="Orders" value={procedureCounts?.orders ?? procedureResults.orders.length} />
                <Field label="Completed orders" value={procedureCounts?.completedOrders ?? '-'} />
                <Field label="Scheduled orders" value={scheduledCount} />
                <Field label="Future scheduled" value={futureScheduledCount} />
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
                    <ProcedureOrderCard
                      key={order.id}
                      order={order}
                      disabled={isLoading}
                      onComplete={onCompleteOrder}
                      onCreateReport={onCreateReport}
                      onDelete={onDeleteOrder}
                    />
                  ))}
                  {procedureResults.orders.length === 0 && (
                    <div className="timeline-placeholder">No procedure orders recorded</div>
                  )}
                </div>
              </section>

              <section className="info-panel procedure-orders-panel">
                <div className="panel-heading">
                  <Clock size={17} />
                  <h3>Pending/Scheduled Orders</h3>
                </div>
                <div className="procedure-order-list">
                  {reportlessOrders.map((order) => (
                    <ProcedureScheduledOrderCard key={order.id} order={order} />
                  ))}
                  {reportlessOrders.length === 0 && (
                    <div className="timeline-placeholder">No pending or scheduled orders without reports</div>
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
                  <ProcedureReportGroup
                    key={order.id}
                    order={order}
                    disabled={isLoading}
                    onCreateResult={onCreateResult}
                  />
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
  onCreateMessage,
  onCloseMessage,
  onArchiveMessage,
  onDeleteMessage,
}: {
  patientId: string
  patientMessages: PatientMessagesResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onCreateMessage: (input: PatientMessageCreateInput) => Promise<unknown>
  onCloseMessage: (message: PatientMessageItem) => Promise<unknown>
  onArchiveMessage: (message: PatientMessageItem) => Promise<unknown>
  onDeleteMessage: (message: PatientMessageItem) => Promise<void>
}) {
  const [messageTitle, setMessageTitle] = useState('Parity Message')
  const [messageBody, setMessageBody] = useState('Created from the modernized Messages workspace.')
  const [assignedTo, setAssignedTo] = useState('admin')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const newCount = countMessagesByStatus(patientMessages?.messages, 'New')
  const doneCount = countMessagesByStatus(patientMessages?.messages, 'Done')
  const isLoading = status === 'loading'

  async function handleMessageSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateMessage({
      patientId,
      title: messageTitle,
      body: messageBody,
      assignedTo,
    })

    setMutationMessage('Message saved')
  }

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
          <span>Message lifecycle</span>
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

        <form className="appointment-mutation-panel" onSubmit={handleMessageSubmit}>
          <div className="panel-heading compact-heading">
            <Mail size={16} />
            <h3>New Message</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Title</span>
              <input
                value={messageTitle}
                onChange={(event) => setMessageTitle(event.target.value)}
                aria-label="New message title"
                required
              />
            </label>
            <label className="filter-field">
              <span>Assigned To</span>
              <input
                value={assignedTo}
                onChange={(event) => setAssignedTo(event.target.value)}
                aria-label="New message assigned to"
                required
              />
            </label>
            <label className="filter-field">
              <span>Body</span>
              <textarea
                value={messageBody}
                onChange={(event) => setMessageBody(event.target.value)}
                aria-label="New message body"
                rows={4}
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading}>
              <Check size={15} />
              Save Message
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>
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
                    <MessageItem
                      key={message.id}
                      message={message}
                      disabled={isLoading}
                      onClose={onCloseMessage}
                      onArchive={onArchiveMessage}
                      onDelete={onDeleteMessage}
                    />
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

function DocumentsWorkspace({
  patientId,
  patientDocuments,
  status,
  error,
  onPatientIdChange,
  onCreateDocument,
  onArchiveDocument,
  onDeleteDocument,
}: {
  patientId: string
  patientDocuments: PatientDocumentsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onPatientIdChange: (value: string) => void
  onCreateDocument: (input: PatientDocumentCreateInput) => Promise<unknown>
  onArchiveDocument: (document: PatientDocumentItem) => Promise<unknown>
  onDeleteDocument: (document: PatientDocumentItem) => Promise<void>
}) {
  const [documentName, setDocumentName] = useState('Parity Document')
  const [documentCategoryId, setDocumentCategoryId] = useState('3')
  const [documentDate, setDocumentDate] = useState('2026-06-18')
  const [documentEncounter, setDocumentEncounter] = useState('1000013')
  const [documentContent, setDocumentContent] = useState('Created from the modernized Documents workspace.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const [viewedDocument, setViewedDocument] = useState<PatientDocumentContentResponse | null>(null)
  const [documentContentStatus, setDocumentContentStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [documentContentError, setDocumentContentError] = useState<string | null>(null)
  const documents = patientDocuments?.documents ?? []
  const categories = useMemo(
    () => Array.from(new Set(documents.map((document) => document.categoryName))).sort(),
    [documents],
  )
  const linkedEncounterCount = documents.filter((document) => document.encounter).length
  const totalPages = documents.reduce((total, document) => total + (document.pages ?? 0), 0)
  const latestDocument = documents[0]
  const isLoading = status === 'loading'

  useEffect(() => {
    if (viewedDocument && !documents.some((document) => document.id === viewedDocument.id)) {
      setViewedDocument(null)
      setDocumentContentStatus('idle')
      setDocumentContentError(null)
    }
  }, [documents, viewedDocument])

  async function handleDocumentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    const categoryId = Number(documentCategoryId)
    const encounter = documentEncounter.trim().length > 0 ? Number(documentEncounter) : null
    if (!Number.isInteger(categoryId) || (encounter !== null && !Number.isInteger(encounter))) {
      setMutationMessage('Check numeric fields')
      return
    }

    await onCreateDocument({
      patientId,
      categoryId,
      name: documentName,
      docDate: documentDate,
      encounter,
      content: documentContent,
      notes: 'Created from the modernized Documents workspace.',
    })

    setMutationMessage('Document saved')
  }

  async function handleDocumentView(document: PatientDocumentItem) {
    setDocumentContentStatus('loading')
    setDocumentContentError(null)

    try {
      const content = await getPatientDocumentContent(document.id)
      setViewedDocument(content)
      setDocumentContentStatus('ready')
    } catch (error) {
      setViewedDocument(null)
      setDocumentContentStatus('error')
      setDocumentContentError(error instanceof Error ? error.message : 'Patient document content load failed')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Documents search">
        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Documents patient ID"
              placeholder="MOD-PAT-0001"
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Patient documents'}</span>
          <span>Document lifecycle</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {patientDocuments ? (
          <>
            <div className="list-counts">
              <MetricRow label="Documents" value={patientDocuments.count} />
              <MetricRow label="Categories" value={categories.length} />
              <MetricRow label="Linked encounters" value={linkedEncounterCount} />
              <MetricRow label="Pages" value={totalPages} />
            </div>

            <div className="access-scope-panel">
              <div className="panel-heading">
                <FolderOpen size={17} />
                <h3>Document Scope</h3>
              </div>
              <Field label="Patient" value={patientDocuments.patientDisplayName} />
              <Field label="Latest document" value={latestDocument?.docDate} />
              <Field label="Storage" value="Synthetic database payloads" />
              <div className="document-category-stack">
                {categories.map((category) => (
                  <span className="status-tag" key={category}>
                    {category}
                  </span>
                ))}
              </div>
            </div>
          </>
        ) : (
          <div className="empty-state">No patient documents loaded</div>
        )}

        <form className="appointment-mutation-panel" onSubmit={handleDocumentSubmit}>
          <div className="panel-heading compact-heading">
            <FolderOpen size={16} />
            <h3>New Document</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Name</span>
              <input
                value={documentName}
                onChange={(event) => setDocumentName(event.target.value)}
                aria-label="New document name"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Category</span>
                <select
                  value={documentCategoryId}
                  onChange={(event) => setDocumentCategoryId(event.target.value)}
                  aria-label="New document category"
                >
                  <option value="3">Medical Record</option>
                  <option value="6">Advance Directive</option>
                  <option value="2">Lab Report</option>
                  <option value="4">Patient Information</option>
                </select>
              </label>
              <label className="filter-field">
                <span>Document Date</span>
                <input
                  type="date"
                  value={documentDate}
                  onChange={(event) => setDocumentDate(event.target.value)}
                  aria-label="New document date"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={documentEncounter}
                onChange={(event) => setDocumentEncounter(event.target.value)}
                aria-label="New document encounter"
                inputMode="numeric"
              />
            </label>
            <label className="filter-field">
              <span>Body</span>
              <textarea
                value={documentContent}
                onChange={(event) => setDocumentContent(event.target.value)}
                aria-label="New document body"
                rows={4}
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading}>
              <Check size={15} />
              Save Document
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>
      </section>

      <section className="appointment-detail-panel" aria-label="Documents detail">
        {patientDocuments ? (
          <>
            <div className="appointment-banner">
              <div>
                <p className="eyebrow">Patient Documents</p>
                <h2>{patientDocuments.patientDisplayName}</h2>
                <p className="patient-line">
                  {patientDocuments.pubpid} / PID {patientDocuments.legacyPid}
                </p>
              </div>
              <div className="portal-pill">{patientDocuments.count} documents</div>
            </div>

            <div className="documents-detail-grid">
              <InfoPanel title="Document Summary" icon={FolderOpen}>
                <MetricRow label="Documents" value={patientDocuments.count} />
                <MetricRow label="Categories" value={categories.length} />
                <MetricRow label="Linked encounters" value={linkedEncounterCount} />
                <MetricRow label="Pages" value={totalPages} />
              </InfoPanel>

              <InfoPanel title="Latest Filing" icon={FileText}>
                <Field label="Name" value={latestDocument?.name} />
                <Field label="Category" value={latestDocument?.categoryName} />
                <Field label="Document date" value={latestDocument?.docDate} />
                <Field label="Uploaded" value={latestDocument?.uploadedAt} />
              </InfoPanel>

              <section className="info-panel document-viewer-panel" aria-label="Document viewer">
                <div className="panel-heading">
                  <FileText size={17} />
                  <h3>Document Viewer</h3>
                </div>
                {documentContentStatus === 'loading' ? (
                  <div className="timeline-placeholder">Loading document content</div>
                ) : viewedDocument ? (
                  <>
                    <div className="document-viewer-meta">
                      <Field label="Name" value={viewedDocument.name} />
                      <Field label="File" value={viewedDocument.fileName} />
                      <Field label="MIME" value={viewedDocument.mimetype} />
                      <Field label="Hash" value={viewedDocument.hash} />
                    </div>
                    <pre className="document-content-block">{viewedDocument.content}</pre>
                    <div className="document-item-actions">
                      <a
                        className="icon-text-button secondary"
                        href={getPatientDocumentDownloadUrl(viewedDocument.id)}
                        download={viewedDocument.fileName}
                      >
                        <Download size={14} />
                        Download
                      </a>
                    </div>
                  </>
                ) : (
                  <div className="timeline-placeholder">
                    {documentContentStatus === 'error' ? documentContentError : 'No document selected'}
                  </div>
                )}
              </section>

              <section className="info-panel documents-panel">
                <div className="panel-heading">
                  <FolderOpen size={17} />
                  <h3>Filed Documents</h3>
                </div>
                <div className="document-list-body">
                  {documents.map((document) => (
                    <DocumentItem
                      key={document.id}
                      document={document}
                      disabled={isLoading}
                      onView={handleDocumentView}
                      onArchive={onArchiveDocument}
                      onDelete={onDeleteDocument}
                    />
                  ))}
                  {documents.length === 0 && <div className="timeline-placeholder">No documents recorded</div>}
                </div>
              </section>
            </div>
          </>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading patient documents</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load documents</div>
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
              <Field label="Exports" value="CSV ready" />
              <a className="icon-text-button secondary" href={getOperationalReportsCsvUrl()} download>
                <Download size={15} />
                CSV Export
              </a>
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
                <MetricRow label="Documents" value={reports.counts.patientDocuments} />
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
  onCreateUser,
  onUpdateUser,
  onDeleteUser,
  onCreateFacility,
  onUpdateFacility,
  onDeleteFacility,
  onGrantAccessPermission,
  onRevokeAccessPermission,
  onGrantAccessMembership,
  onRevokeAccessMembership,
}: {
  directory: AdministrationDirectoryResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onCreateUser: (input: AdministrationUserMutationInput) => Promise<unknown>
  onUpdateUser: (
    user: AdministrationUserItem,
    input: AdministrationUserMutationInput,
  ) => Promise<unknown>
  onDeleteUser: (user: AdministrationUserItem) => Promise<void>
  onCreateFacility: (input: AdministrationFacilityMutationInput) => Promise<unknown>
  onUpdateFacility: (
    facility: AdministrationFacilityItem,
    input: AdministrationFacilityMutationInput,
  ) => Promise<unknown>
  onDeleteFacility: (facility: AdministrationFacilityItem) => Promise<void>
  onGrantAccessPermission: (input: AdministrationAccessPermissionMutationInput) => Promise<unknown>
  onRevokeAccessPermission: (input: AdministrationAccessPermissionMutationInput) => Promise<unknown>
  onGrantAccessMembership: (input: AdministrationAccessUserMembershipMutationInput) => Promise<unknown>
  onRevokeAccessMembership: (input: AdministrationAccessUserMembershipMutationInput) => Promise<unknown>
}) {
  const billingUsers = countUsersByRole(directory?.users, 'billing')
  const frontDeskUsers = countUsersByRole(directory?.users, 'frontdesk')
  const visibleUsers = directory?.users.filter((user) => user.active) ?? []
  const visibleFacilities = directory?.facilities.filter((facility) => facility.active) ?? []
  const leafAccessGroups = directory?.accessControl.groups.filter((group) => group.parentId !== null) ?? []
  const accessPermissionOptions = directory?.accessControl.permissions ?? []
  const accessMembershipOptions = directory?.accessControl.userMemberships ?? []
  const accessPermissionAnchors = directory?.accessControl.groupPermissions.filter((permission) =>
    ['admin', 'doc', 'clin', 'front', 'back', 'breakglass'].includes(permission.groupValue),
  ) ?? []
  const [userDraft, setUserDraft] = useState<AdministrationUserMutationInput>({
    username: 'slice19-user',
    firstName: 'Morgan',
    lastName: 'Parity',
    role: 'frontdesk',
    calendar: false,
    facilityId: 10,
    email: 'slice19-user@example.test',
    npi: '',
    active: true,
  })
  const [facilityDraft, setFacilityDraft] = useState<AdministrationFacilityMutationInput>({
    code: 'WEST',
    name: 'West County Health Center',
    phone: '(619) 555-0180',
    street: '440 Mission Road',
    city: 'San Diego',
    state: 'CA',
    postalCode: '92111',
    color: '#356f9f',
    active: true,
  })
  const [accessDraft, setAccessDraft] = useState<AdministrationAccessPermissionMutationInput>({
    groupValue: 'front',
    sectionValue: 'patients',
    permissionValue: 'demo',
    returnValue: 'write',
  })
  const [accessMembershipDraft, setAccessMembershipDraft] = useState<AdministrationAccessUserMembershipMutationInput>({
    userValue: 'gold-frontdesk-01',
    groupValue: 'front',
  })
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)

  async function handleUserCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)
    await onCreateUser(userDraft)
    setMutationMessage(`Created ${userDraft.username}`)
  }

  async function handleUserDeactivate(user: AdministrationUserItem) {
    setMutationMessage(null)
    await onUpdateUser(user, {
      username: user.username,
      firstName: user.firstName,
      lastName: user.lastName.endsWith('Inactive') ? user.lastName : `${user.lastName} Inactive`,
      role: user.role,
      calendar: user.calendar,
      facilityId: user.facilityId ?? null,
      email: user.email,
      npi: user.npi,
      active: false,
    })
    setMutationMessage(`Updated ${user.username} to inactive`)
  }

  async function handleUserDelete(user: AdministrationUserItem) {
    setMutationMessage(null)
    await onDeleteUser(user)
    setMutationMessage(`Deleted ${user.username}`)
  }

  async function handleFacilityCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)
    await onCreateFacility(facilityDraft)
    setMutationMessage(`Created ${facilityDraft.name}`)
  }

  async function handleFacilityDeactivate(facility: AdministrationFacilityItem) {
    setMutationMessage(null)
    await onUpdateFacility(facility, {
      code: facility.code,
      name: facility.name.endsWith('Inactive') ? facility.name : `${facility.name} Inactive`,
      phone: facility.phone,
      street: facility.street,
      city: facility.city,
      state: facility.state,
      postalCode: facility.postalCode,
      color: facility.color,
      active: false,
    })
    setMutationMessage(`Updated ${facility.code} to inactive`)
  }

  async function handleFacilityDelete(facility: AdministrationFacilityItem) {
    setMutationMessage(null)
    await onDeleteFacility(facility)
    setMutationMessage(`Deleted ${facility.code}`)
  }

  async function handleAccessGrant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)
    await onGrantAccessPermission(accessDraft)
    setMutationMessage(`Granted ${accessDraft.groupValue} ${accessDraft.sectionValue}:${accessDraft.permissionValue}`)
  }

  async function handleAccessRevoke() {
    setMutationMessage(null)
    await onRevokeAccessPermission(accessDraft)
    setMutationMessage(`Revoked ${accessDraft.groupValue} ${accessDraft.sectionValue}:${accessDraft.permissionValue}`)
  }

  async function handleAccessMembershipGrant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)
    await onGrantAccessMembership(accessMembershipDraft)
    setMutationMessage(`Assigned ${accessMembershipDraft.userValue} to ${accessMembershipDraft.groupValue}`)
  }

  async function handleAccessMembershipRevoke() {
    setMutationMessage(null)
    await onRevokeAccessMembership(accessMembershipDraft)
    setMutationMessage(`Revoked ${accessMembershipDraft.userValue} from ${accessMembershipDraft.groupValue}`)
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Administration summary">
        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Administration directory'}</span>
          <span>User and facility lifecycle</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}
        {mutationMessage && <div className="status-banner success">{mutationMessage}</div>}

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
          <Field label="Authorization" value="Default ACL model mirrored" />
          <Field label="Audit logging" value="Planned" />
          <Field label="Directory mode" value="User/facility mutation and ACL read model" />
          {directory && (
            <>
              <Field label="Access groups" value={String(directory.counts.accessGroups)} />
              <Field label="Permission entries" value={String(directory.counts.accessGroupPermissions)} />
              <Field label="Access memberships" value={String(directory.counts.accessUserMemberships)} />
            </>
          )}
        </div>

        <form className="appointment-mutation-panel" onSubmit={handleAccessGrant}>
          <div className="panel-heading">
            <ShieldCheck size={17} />
            <h3>Permission Assignment</h3>
          </div>
          <label className="form-field">
            <span>Group</span>
            <select
              value={accessDraft.groupValue}
              onChange={(event) => setAccessDraft((current) => ({ ...current, groupValue: event.target.value }))}
            >
              {leafAccessGroups.map((group) => (
                <option key={group.value} value={group.value}>
                  {group.name}
                </option>
              ))}
            </select>
          </label>
          <label className="form-field">
            <span>Permission</span>
            <select
              value={`${accessDraft.sectionValue}:${accessDraft.permissionValue}`}
              onChange={(event) => {
                const [sectionValue, permissionValue] = event.target.value.split(':')
                setAccessDraft((current) => ({ ...current, sectionValue, permissionValue }))
              }}
            >
              {accessPermissionOptions.map((permission) => (
                <option key={`${permission.sectionValue}:${permission.value}`} value={`${permission.sectionValue}:${permission.value}`}>
                  {permission.name}
                </option>
              ))}
            </select>
          </label>
          <label className="form-field">
            <span>Return value</span>
            <select
              value={accessDraft.returnValue}
              onChange={(event) => setAccessDraft((current) => ({ ...current, returnValue: event.target.value }))}
            >
              <option value="write">write</option>
              <option value="view">view</option>
              <option value="addonly">addonly</option>
              <option value="wsome">wsome</option>
            </select>
          </label>
          <div className="button-row">
            <button type="submit" className="icon-text-button primary" disabled={status === 'loading'}>
              <Check size={16} />
              Grant
            </button>
            <button
              type="button"
              className="icon-text-button"
              disabled={status === 'loading'}
              onClick={handleAccessRevoke}
            >
              <Ban size={16} />
              Revoke
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleAccessMembershipGrant}>
          <div className="panel-heading">
            <ShieldCheck size={17} />
            <h3>User Group Membership</h3>
          </div>
          <label className="form-field">
            <span>User</span>
            <select
              value={accessMembershipDraft.userValue}
              onChange={(event) =>
                setAccessMembershipDraft((current) => ({ ...current, userValue: event.target.value }))
              }
            >
              {visibleUsers.map((user) => (
                <option key={user.id} value={user.username}>
                  {user.displayName} ({user.username})
                </option>
              ))}
            </select>
          </label>
          <label className="form-field">
            <span>Group</span>
            <select
              value={accessMembershipDraft.groupValue}
              onChange={(event) =>
                setAccessMembershipDraft((current) => ({ ...current, groupValue: event.target.value }))
              }
            >
              {leafAccessGroups.map((group) => (
                <option key={group.value} value={group.value}>
                  {group.name}
                </option>
              ))}
            </select>
          </label>
          <div className="button-row">
            <button type="submit" className="icon-text-button primary" disabled={status === 'loading'}>
              <Check size={16} />
              Assign
            </button>
            <button
              type="button"
              className="icon-text-button"
              disabled={status === 'loading'}
              onClick={handleAccessMembershipRevoke}
            >
              <Ban size={16} />
              Revoke
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleUserCreate}>
          <div className="panel-heading">
            <UserRound size={17} />
            <h3>Create User</h3>
          </div>
          <label className="form-field">
            <span>Username</span>
            <input
              value={userDraft.username}
              onChange={(event) => setUserDraft((current) => ({ ...current, username: event.target.value }))}
            />
          </label>
          <div className="mutation-grid two-column">
            <label className="form-field">
              <span>First name</span>
              <input
                value={userDraft.firstName}
                onChange={(event) => setUserDraft((current) => ({ ...current, firstName: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>Last name</span>
              <input
                value={userDraft.lastName}
                onChange={(event) => setUserDraft((current) => ({ ...current, lastName: event.target.value }))}
              />
            </label>
          </div>
          <div className="mutation-grid two-column">
            <label className="form-field">
              <span>Role</span>
              <select
                value={userDraft.role}
                onChange={(event) => setUserDraft((current) => ({ ...current, role: event.target.value }))}
              >
                <option value="frontdesk">frontdesk</option>
                <option value="billing">billing</option>
                <option value="nurse">nurse</option>
                <option value="provider">provider</option>
              </select>
            </label>
            <label className="form-field">
              <span>Facility ID</span>
              <input
                type="number"
                value={userDraft.facilityId ?? ''}
                onChange={(event) => setUserDraft((current) => ({
                  ...current,
                  facilityId: event.target.value ? Number(event.target.value) : null,
                }))}
              />
            </label>
          </div>
          <label className="form-field">
            <span>Email</span>
            <input
              value={userDraft.email ?? ''}
              onChange={(event) => setUserDraft((current) => ({ ...current, email: event.target.value }))}
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={Boolean(userDraft.calendar)}
              onChange={(event) => setUserDraft((current) => ({ ...current, calendar: event.target.checked }))}
            />
            <span>Calendar enabled</span>
          </label>
          <button type="submit" className="icon-text-button primary" disabled={status === 'loading'}>
            <Check size={16} />
            Create user
          </button>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleFacilityCreate}>
          <div className="panel-heading">
            <Building2 size={17} />
            <h3>Create Facility</h3>
          </div>
          <label className="form-field">
            <span>Facility code</span>
            <input
              value={facilityDraft.code}
              onChange={(event) => setFacilityDraft((current) => ({ ...current, code: event.target.value }))}
            />
          </label>
          <label className="form-field">
            <span>Facility name</span>
            <input
              value={facilityDraft.name}
              onChange={(event) => setFacilityDraft((current) => ({ ...current, name: event.target.value }))}
            />
          </label>
          <label className="form-field">
            <span>Phone</span>
            <input
              value={facilityDraft.phone ?? ''}
              onChange={(event) => setFacilityDraft((current) => ({ ...current, phone: event.target.value }))}
            />
          </label>
          <label className="form-field">
            <span>Street</span>
            <input
              value={facilityDraft.street ?? ''}
              onChange={(event) => setFacilityDraft((current) => ({ ...current, street: event.target.value }))}
            />
          </label>
          <div className="mutation-grid two-column">
            <label className="form-field">
              <span>City</span>
              <input
                value={facilityDraft.city ?? ''}
                onChange={(event) => setFacilityDraft((current) => ({ ...current, city: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>State</span>
              <input
                value={facilityDraft.state ?? ''}
                onChange={(event) => setFacilityDraft((current) => ({ ...current, state: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>ZIP</span>
              <input
                value={facilityDraft.postalCode ?? ''}
                onChange={(event) => setFacilityDraft((current) => ({ ...current, postalCode: event.target.value }))}
              />
            </label>
          </div>
          <label className="form-field">
            <span>Color</span>
            <input
              value={facilityDraft.color ?? ''}
              onChange={(event) => setFacilityDraft((current) => ({ ...current, color: event.target.value }))}
            />
          </label>
          <button type="submit" className="icon-text-button primary" disabled={status === 'loading'}>
            <Check size={16} />
            Create facility
          </button>
        </form>
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

              <InfoPanel title="Access Control Matrix" icon={ShieldCheck}>
                <MetricRow label="Groups" value={directory.counts.accessGroups} />
                <MetricRow label="Leaf groups" value={leafAccessGroups.length} />
                <MetricRow label="Permissions" value={directory.counts.accessPermissions} />
                <MetricRow label="Assignments" value={directory.counts.accessGroupPermissions} />
                <MetricRow label="Memberships" value={directory.counts.accessUserMemberships} />
              </InfoPanel>

              <section className="info-panel admin-users-panel">
                <div className="panel-heading">
                  <ShieldCheck size={17} />
                  <h3>Users</h3>
                </div>
                <div className="admin-directory-list">
                  {visibleUsers.map((user) => (
                    <AdministrationUserCard
                      key={user.id}
                      user={user}
                      memberships={accessMembershipOptions.filter(
                        (membership) => membership.staffId === user.id || membership.userValue === user.username,
                      )}
                      onDeactivate={handleUserDeactivate}
                      onDelete={handleUserDelete}
                    />
                  ))}
                </div>
              </section>

              <section className="info-panel admin-access-panel">
                <div className="panel-heading">
                  <ShieldCheck size={17} />
                  <h3>Access Groups</h3>
                </div>
                <div className="admin-directory-list">
                  {leafAccessGroups.map((group) => (
                    <AdministrationAccessGroupCard
                      key={group.value}
                      group={group}
                      permissions={accessPermissionAnchors.filter((permission) => permission.groupValue === group.value)}
                    />
                  ))}
                </div>
              </section>

              <section className="info-panel admin-facilities-panel">
                <div className="panel-heading">
                  <Building2 size={17} />
                  <h3>Facilities</h3>
                </div>
                <div className="admin-directory-list">
                  {visibleFacilities.map((facility) => (
                    <AdministrationFacilityCard
                      key={facility.id}
                      facility={facility}
                      onDeactivate={handleFacilityDeactivate}
                      onDelete={handleFacilityDelete}
                    />
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

function DocumentItem({
  document,
  disabled,
  onView,
  onArchive,
  onDelete,
}: {
  document: PatientDocumentItem
  disabled: boolean
  onView: (document: PatientDocumentItem) => Promise<void>
  onArchive: (document: PatientDocumentItem) => Promise<unknown>
  onDelete: (document: PatientDocumentItem) => Promise<void>
}) {
  return (
    <article className="document-card">
      <div className="message-item-header">
        <strong>{document.name}</strong>
        <span className="status-tag">{document.categoryName}</span>
      </div>
      <div className="document-meta-grid">
        <span>{document.docDate}</span>
        <span>{document.mimetype || 'No mimetype'}</span>
        <span>{formatBytes(document.sizeBytes)}</span>
        <span>{document.pages ? `${document.pages} pages` : 'No page count'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{document.encounter ? `Encounter ${document.encounter}` : 'No linked encounter'}</span>
        <span>{document.storageMethod || 'Storage not recorded'}</span>
      </div>
      <p className="document-preview">{document.contentPreview || document.notes || 'No preview available'}</p>
      <div className="document-footnote">
        <span>{document.documentKey}</span>
        <span>{document.hash || document.url || 'No document reference'}</span>
      </div>
      <div className="document-item-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => void onView(document)}
        >
          <FileText size={14} />
          View
        </button>
        <a
          className="icon-text-button secondary"
          href={getPatientDocumentDownloadUrl(document.id)}
          download
          aria-disabled={disabled}
        >
          <Download size={14} />
          Download
        </a>
        <button
          className="icon-text-button danger"
          type="button"
          disabled={disabled}
          onClick={() => void onArchive(document)}
        >
          <Ban size={14} />
          Archive
        </button>
        <button
          className="icon-text-button"
          type="button"
          disabled={disabled}
          onClick={() => void onDelete(document)}
        >
          <Trash2 size={14} />
          Delete
        </button>
      </div>
    </article>
  )
}

const preferredAccessPermissionKeys = new Set([
  'admin:acl',
  'admin:super',
  'patients:demo',
  'patients:rx',
  'patients:pat_rep',
  'acct:eob',
])

function getAccessPermissionRank(permission: AdministrationAccessGroupPermissionItem) {
  return preferredAccessPermissionKeys.has(`${permission.sectionValue}:${permission.permissionValue}`) ? 0 : 1
}

function AdministrationAccessGroupCard({
  group,
  permissions,
}: {
  group: AdministrationAccessGroupItem
  permissions: AdministrationAccessGroupPermissionItem[]
}) {
  const visiblePermissions = [...permissions]
    .sort(
      (left, right) =>
        getAccessPermissionRank(left) - getAccessPermissionRank(right) ||
        left.sectionValue.localeCompare(right.sectionValue) ||
        left.permissionValue.localeCompare(right.permissionValue) ||
        left.returnValue.localeCompare(right.returnValue),
    )
    .slice(0, 4)
  const remainingCount = Math.max(0, permissions.length - visiblePermissions.length)

  return (
    <article className="admin-user-card">
      <div className="message-item-header">
        <strong>{group.name}</strong>
        <span className="status-tag">{group.value}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{group.permissionCount} permissions</span>
        <span>{group.parentId ? `Parent ${group.parentId}` : 'Root group'}</span>
      </div>
      <div className="access-permission-list">
        {visiblePermissions.map((permission) => (
          <span key={`${permission.sectionValue}-${permission.permissionValue}-${permission.returnValue}`}>
            {permission.permissionName} ({permission.sectionValue}:{permission.permissionValue} {permission.returnValue})
          </span>
        ))}
        {remainingCount > 0 && <span>{remainingCount} more permissions</span>}
      </div>
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

function AdministrationUserCard({
  user,
  memberships,
  onDeactivate,
  onDelete,
}: {
  user: AdministrationUserItem
  memberships?: AdministrationAccessUserMembershipItem[]
  onDeactivate?: (user: AdministrationUserItem) => Promise<void>
  onDelete?: (user: AdministrationUserItem) => Promise<void>
}) {
  const visibleMemberships = memberships ?? []

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
      {visibleMemberships.length > 0 && (
        <div className="access-permission-list">
          {visibleMemberships.map((membership) => (
            <span key={`${membership.userValue}-${membership.groupValue}`}>
              {membership.groupName} membership
            </span>
          ))}
        </div>
      )}
      {(onDeactivate || onDelete) && (
        <div className="detail-actions compact-actions">
          {onDeactivate && user.active && (
            <button type="button" className="icon-text-button" onClick={() => onDeactivate(user)}>
              <Ban size={15} />
              Deactivate
            </button>
          )}
          {onDelete && (
            <button type="button" className="icon-text-button danger" onClick={() => onDelete(user)}>
              <Trash2 size={15} />
              Delete
            </button>
          )}
        </div>
      )}
    </article>
  )
}

function AdministrationFacilityCard({
  facility,
  onDeactivate,
  onDelete,
}: {
  facility: AdministrationFacilityItem
  onDeactivate?: (facility: AdministrationFacilityItem) => Promise<void>
  onDelete?: (facility: AdministrationFacilityItem) => Promise<void>
}) {
  return (
    <article className="facility-card">
      <div className="message-item-header">
        <strong>{facility.name}</strong>
        <span className="status-tag">{facility.active ? facility.code : 'Inactive'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{facility.phone || 'No phone'}</span>
        <span>{facility.color || 'No color'}</span>
      </div>
      <p>{formatFacilityAddress(facility)}</p>
      {(onDeactivate || onDelete) && (
        <div className="detail-actions compact-actions">
          {onDeactivate && facility.active && (
            <button type="button" className="icon-text-button" onClick={() => onDeactivate(facility)}>
              <Ban size={15} />
              Deactivate
            </button>
          )}
          {onDelete && (
            <button type="button" className="icon-text-button danger" onClick={() => onDelete(facility)}>
              <Trash2 size={15} />
              Delete
            </button>
          )}
        </div>
      )}
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

function MessageItem({
  message,
  disabled,
  onClose,
  onArchive,
  onDelete,
}: {
  message: PatientMessageItem
  disabled: boolean
  onClose: (message: PatientMessageItem) => Promise<unknown>
  onArchive: (message: PatientMessageItem) => Promise<unknown>
  onDelete: (message: PatientMessageItem) => Promise<void>
}) {
  return (
    <article className="message-item">
      <div className="message-item-header">
        <strong>{message.title || 'Patient message'}</strong>
        <span className="status-tag">{message.status || 'Status pending'}</span>
      </div>
      <p>{message.body || 'No message body recorded'}</p>
      <span>{[message.date || 'No date', message.assignedTo ? `Assigned to ${message.assignedTo}` : null].filter(Boolean).join(' / ')}</span>
      <div className="message-item-actions">
        <button
          className="icon-text-button primary"
          type="button"
          disabled={disabled || message.status === 'Done'}
          onClick={() => void onClose(message)}
        >
          <Check size={14} />
          Close
        </button>
        <button
          className="icon-text-button danger"
          type="button"
          disabled={disabled}
          onClick={() => void onArchive(message)}
        >
          <Ban size={14} />
          Archive
        </button>
        <button
          className="icon-text-button"
          type="button"
          disabled={disabled}
          onClick={() => void onDelete(message)}
        >
          <Trash2 size={14} />
          Delete
        </button>
      </div>
    </article>
  )
}

function BillingEncounterCard({
  encounter,
  disabled,
  onDeactivateLine,
  onDeleteLine,
}: {
  encounter: BillingEncounterItem
  disabled: boolean
  onDeactivateLine: (line: BillingLineItem) => Promise<unknown>
  onDeleteLine: (line: BillingLineItem) => Promise<void>
}) {
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
          <BillingLineCard
            key={line.id}
            line={line}
            disabled={disabled}
            onDeactivate={onDeactivateLine}
            onDelete={onDeleteLine}
          />
        ))}
        {encounter.lines.length === 0 && <div className="timeline-placeholder">No fee sheet codes recorded</div>}
      </div>
    </article>
  )
}

function BillingLineCard({
  line,
  disabled,
  onDeactivate,
  onDelete,
}: {
  line: BillingLineItem
  disabled: boolean
  onDeactivate: (line: BillingLineItem) => Promise<unknown>
  onDelete: (line: BillingLineItem) => Promise<void>
}) {
  return (
    <article className="billing-line-card">
      <div className="message-item-header">
        <strong>{line.code || 'Billing code'}</strong>
        <span className="status-tag">{line.codeType || 'Code type'}</span>
      </div>
      <p>{line.codeText || 'No description recorded'}</p>
      <div className="procedure-order-meta">
        <span>{line.justify ? `Justify ${line.justify}` : 'No justification'}</span>
        <span>{line.units} unit{line.units === 1 ? '' : 's'}</span>
        <span>{line.billed === 1 ? 'Billed' : 'Unbilled'}</span>
        <span>{formatCurrency(line.fee)}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => onDeactivate(line)}
        >
          <Ban size={15} />
          Bill/Deactivate
        </button>
        <button
          className="icon-text-button danger"
          type="button"
          disabled={disabled}
          onClick={() => onDelete(line)}
        >
          <Trash2 size={15} />
          Delete
        </button>
      </div>
    </article>
  )
}

function ProcedureOrderCard({
  order,
  disabled,
  onComplete,
  onCreateReport,
  onDelete,
}: {
  order: ProcedureOrderItem
  disabled: boolean
  onComplete: (order: ProcedureOrderItem) => Promise<unknown>
  onCreateReport: (input: ProcedureReportCreateInput) => Promise<unknown>
  onDelete: (order: ProcedureOrderItem) => Promise<void>
}) {
  async function handleCreateReport() {
    await onCreateReport({
      orderId: order.id,
      dateCollected: '2026-06-18 12:30:00',
      dateReport: '2026-06-18 13:00:00',
      specimenNumber: `MOD-${order.id}`,
      reportStatus: 'final',
      reviewStatus: 'reviewed',
      notes: 'Created from the modernized Procedures workspace.',
    })
  }

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
      <div className="procedure-order-meta">
        <span>{order.orderPriority || 'No priority'}</span>
        <span>{order.procedureType || 'No procedure type'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || order.orderStatus === 'complete'}
          onClick={() => onComplete(order)}
        >
          <Check size={15} />
          Complete
        </button>
        <button className="icon-text-button secondary" type="button" disabled={disabled} onClick={handleCreateReport}>
          <FileText size={15} />
          Add Report
        </button>
        <button className="icon-text-button danger" type="button" disabled={disabled} onClick={() => onDelete(order)}>
          <Trash2 size={15} />
          Delete
        </button>
      </div>
    </article>
  )
}

function ProcedureScheduledOrderCard({ order }: { order: ProcedureOrderItem }) {
  return (
    <article className="procedure-order-card scheduled-order-card">
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
      <div className="procedure-order-meta">
        <span>{order.orderPriority || 'No priority'}</span>
        <span>{order.procedureType || 'No procedure type'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{order.diagnosis || 'No diagnosis'}</span>
        <span>{order.reports.length === 0 ? 'No report has been filed' : `${order.reports.length} reports recorded`}</span>
      </div>
      {order.instructions && <p className="procedure-scheduled-note">{order.instructions}</p>}
    </article>
  )
}

function ProcedureReportGroup({
  order,
  disabled,
  onCreateResult,
}: {
  order: ProcedureOrderItem
  disabled: boolean
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
}) {
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
        <ProcedureReportCard key={report.id} report={report} disabled={disabled} onCreateResult={onCreateResult} />
      ))}
      {order.reports.length === 0 && <div className="timeline-placeholder">No reports recorded for this order</div>}
    </article>
  )
}

function ProcedureReportCard({
  report,
  disabled,
  onCreateResult,
}: {
  report: ProcedureReportItem
  disabled: boolean
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
}) {
  async function handleCreateResult() {
    await onCreateResult({
      reportId: report.id,
      resultCode: '2345-7',
      resultText: 'Glucose',
      dateTime: '2026-06-18 13:05:00',
      facility: 'Modernization Family Medicine',
      units: 'mg/dL',
      result: '104',
      range: '70-99',
      abnormal: 'high',
      comments: 'Created from the modernized Procedures workspace.',
      status: 'final',
    })
  }

  return (
    <section className="procedure-report-card">
      <div className="procedure-report-title">
        <div>
          <strong>Report {report.id}</strong>
          <span>{[report.reportDate, report.reviewStatus, report.notes].filter(Boolean).join(' / ')}</span>
        </div>
        <span className="status-tag">{report.status || 'Status pending'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button className="icon-text-button secondary" type="button" disabled={disabled} onClick={handleCreateResult}>
          <Activity size={15} />
          Add Result
        </button>
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

function AllergyPanel({
  items,
  onDeactivate,
  onDelete,
  disabled,
}: {
  items: AllergyListItem[]
  onDeactivate: (allergy: AllergyListItem) => Promise<unknown>
  onDelete: (allergy: AllergyListItem) => Promise<void>
  disabled: boolean
}) {
  return (
    <ClinicalSection title="Allergies" icon={ShieldCheck} emptyText="No allergies recorded">
      {items.map((item) => (
        <ClinicalItem
          key={item.id}
          title={item.title}
          meta={[item.reaction, item.severity].filter(Boolean).join(' / ')}
          date={item.date}
          note={item.comments}
        >
          <div className="clinical-item-actions">
            <button
              className="icon-text-button danger"
              type="button"
              disabled={disabled}
              onClick={() => void onDeactivate(item)}
            >
              <Ban size={14} />
              Deactivate
            </button>
            <button
              className="icon-text-button"
              type="button"
              disabled={disabled}
              onClick={() => void onDelete(item)}
            >
              <Trash2 size={14} />
              Delete
            </button>
          </div>
        </ClinicalItem>
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

function PrescriptionPanel({
  items,
  onDeactivate,
  onDelete,
  disabled,
}: {
  items: PrescriptionListItem[]
  onDeactivate: (prescription: PrescriptionListItem) => Promise<unknown>
  onDelete: (prescription: PrescriptionListItem) => Promise<void>
  disabled: boolean
}) {
  return (
    <ClinicalSection title="Prescriptions" icon={FileText} emptyText="No prescriptions">
      {items.map((item) => (
        <ClinicalItem
          key={item.id}
          title={item.drug}
          meta={[item.dosage, item.route, item.diagnosis].filter(Boolean).join(' / ')}
          date={item.startDate}
          note={[item.providerName, item.quantity ? `Qty ${item.quantity}` : null, `${item.refills} refill${item.refills === 1 ? '' : 's'}`]
            .filter(Boolean)
            .join(' / ')}
        >
          {item.note && <p className="clinical-item-note">{item.note}</p>}
          <div className="clinical-item-actions">
            <button
              className="icon-text-button danger"
              type="button"
              disabled={disabled}
              onClick={() => void onDeactivate(item)}
            >
              <Ban size={14} />
              Deactivate
            </button>
            <button
              className="icon-text-button"
              type="button"
              disabled={disabled}
              onClick={() => void onDelete(item)}
            >
              <Trash2 size={14} />
              Delete
            </button>
          </div>
        </ClinicalItem>
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
  children,
}: {
  title: string
  meta?: string | null
  date?: string | null
  note?: string | null
  children?: ReactNode
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
      {children}
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

function isScheduledProcedureOrder(order: ProcedureOrderItem) {
  return order.orderStatus?.toLowerCase() === 'scheduled'
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

function buildContactDraft(patient: PatientListItem | PatientChartSummary | null): PatientContactUpdate {
  return {
    phoneHome: patient?.phoneHome ?? patient?.phone ?? '',
    phoneCell: patient?.phoneCell ?? patient?.phone ?? '',
    email: patient?.email ?? '',
    hipaaAllowSms: readContactPermission(patient, 'hipaaAllowSms'),
    hipaaAllowEmail: readContactPermission(patient, 'hipaaAllowEmail'),
  }
}

function readContactPermission(
  patient: PatientListItem | PatientChartSummary | null,
  field: 'hipaaAllowSms' | 'hipaaAllowEmail',
) {
  if (!hasContactPermissions(patient)) {
    return 'YES'
  }

  return patient[field] || 'YES'
}

function hasContactPermissions(patient: PatientListItem | PatientChartSummary | null): patient is PatientChartSummary {
  return Boolean(patient && 'hipaaAllowSms' in patient && 'hipaaAllowEmail' in patient)
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

function numberOrNull(value: string) {
  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

function formatCurrency(value?: number | null) {
  return `$${(value ?? 0).toFixed(2)}`
}

function formatBytes(value?: number | null) {
  if (!value) {
    return 'No size'
  }

  if (value < 1024) {
    return `${value} bytes`
  }

  return `${(value / 1024).toFixed(1)} KB`
}

export default App
