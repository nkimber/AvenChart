import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react'
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
  ExternalLink,
  FileCheck2,
  FileClock,
  FileText,
  FlaskConical,
  FolderOpen,
  HeartPulse,
  LogIn,
  Mail,
  MapPin,
  Pencil,
  Reply,
  Search,
  ShieldCheck,
  Stethoscope,
  RotateCcw,
  Syringe,
  Trash2,
  Upload,
  UserRound,
  UserPlus,
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
  getCollectionsWorkQueue,
  getStatementBatch,
  downloadBillingStatementPdf,
  downloadStatementBatchPackage,
  downloadPatientDocument,
  getPatientDocumentContent,
  getPatientDocuments,
  getPatientMessages,
  getProcedureLabProviders,
  getProcedureOrderCatalog,
  getProcedureOrderQueue,
  getProcedureReportReviewQueue,
  getProcedureResults,
  getOperationalReports,
  getOperationalReportsCsv,
  getLoginAudit,
  getCurrentSession,
  createAppointment,
  bulkSignProcedureReports,
  importProcedureOrderCatalogCompendium,
  login,
  logout,
  createBillingClaimStatus,
  createBillingLine,
  createBillingPaymentPosting,
  createClinicalAllergy,
  createClinicalImmunization,
  createClinicalMedication,
  createClinicalProblem,
  createClinicalPrescription,
  createAdministrationFacility,
  createAdministrationUser,
  createCollectionsFollowUp,
  createPatientInsurance,
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  createEncounter,
  createEncounterBinaryDocument,
  createEncounterDocument,
  createEncounterExternalLinkDocument,
  createEncounterSoapNote,
  createEncounterVitals,
  createPatientMessage,
  createProcedureOrder,
  createProcedureLabProvider,
  createProcedureOrderCatalogItem,
  createProcedureReport,
  createProcedureSpecimen,
  createProcedureResult,
  createPatient,
  findPatientDuplicates,
  PatientRegistrationValidationError,
  updateProcedureReport,
  updateProcedureResult,
  deleteAppointment,
  deleteAdministrationFacility,
  deleteAdministrationUser,
  deleteBillingClaimStatus,
  deleteBillingLine,
  deleteBillingPaymentPosting,
  deleteClinicalAllergy,
  deleteClinicalImmunization,
  deleteClinicalMedication,
  deleteClinicalProblem,
  deleteClinicalPrescription,
  deleteEncounter,
  deleteEncounterSignature,
  denyEncounterDocument,
  deletePatientInsurance,
  deletePatientDocument,
  deletePatientMessage,
  deleteProcedureOrder,
  deleteProcedureLabProvider,
  deleteProcedureOrderCatalogItem,
  deactivateClinicalAllergy,
  deactivateClinicalMedication,
  deactivateClinicalProblem,
  deactivateClinicalPrescription,
  markClinicalImmunizationEnteredInError,
  moveEncounterDocument,
  grantAdministrationAccessPermission,
  grantAdministrationAccessUserMembership,
  revokeAdministrationAccessPermission,
  revokeAdministrationAccessUserMembership,
  searchAppointments,
  searchEncounters,
  searchPatients,
  signEncounter,
  signEncounterDocument,
  signProcedureReport,
  reopenProcedureReportReview,
  replyToPatientMessage,
  softDeleteEncounterDocument,
  signPatientDocument,
  softDeletePatientDocument,
  softDeletePatientMessage,
  transmitProcedureOrder,
  rescheduleAppointmentOccurrence,
  restoreEncounterDocument,
  restoreAppointmentOccurrence,
  restorePatientDocument,
  replaceEncounterDocumentBinaryContent,
  replaceEncounterDocumentContent,
  replacePatientDocumentBinaryContent,
  replacePatientDocumentContent,
  updatePatientMessageAssignment,
  updatePatientMessageContent,
  updateAppointment,
  updateAppointmentStatus,
  updateAdministrationFacility,
  updateAdministrationUser,
  updateBillingClaimStatus,
  updateBillingLine,
  updateBillingLineStatus,
  voidBillingPaymentPosting,
  updateEncounter,
  updateEncounterDocumentMetadata,
  updatePatientDocumentMetadata,
  updatePatientInsurance,
  updatePatientMessageStatus,
  updatePatientContact,
  updatePatientDeceasedStatus,
  updatePatientDemographics,
  updatePatientGuardianContact,
  updateProcedureOrder,
  updateProcedureLabProvider,
  updateProcedureOrderCatalogItem,
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
  type AuthAuditResponse,
  type AuthLoginResponse,
  type AuthSessionResponse,
  type AppointmentDetail,
  type AppointmentCreateInput,
  type AppointmentListItem,
  type AppointmentOccurrenceRescheduleInput,
  type AppointmentSearchResponse,
  type AppointmentUpdateInput,
  type AllergyListItem,
  type BillingEncounterItem,
  type BillingClaimItem,
  type BillingClaimCreateInput,
  type BillingClaimStatusUpdateInput,
  type BillingLedgerEntry,
  type BillingStatementLineItem,
  type BillingPaymentItem,
  type BillingLineCreateInput,
  type BillingLineItem,
  type BillingLineUpdateInput,
  type BillingPaymentCreateInput,
  type ClinicalListsResponse,
  type ClinicalAllergyCreateInput,
  type ClinicalImmunizationCreateInput,
  type ClinicalMedicationCreateInput,
  type ClinicalProblemCreateInput,
  type ClinicalPrescriptionCreateInput,
  type CollectionsWorkQueueItem,
  type CollectionsWorkQueueResponse,
  type CollectionsFollowUpMutationResponse,
  type EncounterAmendmentHistoryItem,
  type EncounterCreateInput,
  type EncounterBinaryDocumentCreateInput,
  type EncounterDiagnosisCode,
  type EncounterDetail,
  type EncounterDocumentCreateInput,
  type EncounterDocumentAttachment,
  type EncounterExternalLinkDocumentCreateInput,
  type EncounterDocumentMoveResponse,
  type EncounterDocumentMutationResponse,
  type EncounterSoapNoteCreateInput,
  type EncounterListItem,
  type EncounterSearchResponse,
  type EncounterSignInput,
  type EncounterSignatureItem,
  type EncounterSignatureMutationResponse,
  type EncounterUpdateInput,
  type EncounterVitalsCreateInput,
  type ImmunizationListItem,
  type MedicationListItem,
  type PatientChartSummary,
  type PatientDuplicateCandidate,
  type PatientDuplicateSearchResponse,
  type PatientInsuranceItem,
  type PatientInsuranceMutationInput,
  type PatientListItem,
  type PatientBillingResponse,
  type PatientDocumentBinaryContentReplaceInput,
  type PatientDocumentBinaryCreateInput,
  type PatientContactUpdate,
  type PatientDemographicsUpdate,
  type PatientDocumentCreateInput,
  type PatientDocumentContentReplaceInput,
  type PatientDocumentContentResponse,
  type PatientDeceasedStatusUpdate,
  type PatientGuardianContactUpdate,
  type PatientDocumentExternalLinkCreateInput,
  type PatientDocumentItem,
  type PatientDocumentMetadataUpdateInput,
  type PatientDocumentsResponse,
  type PatientMessageAssignmentUpdateInput,
  type PatientMessageContentUpdateInput,
  type PatientMessageCreateInput,
  type PatientMessageItem,
  type PatientMessageReplyInput,
  type PatientMessagesResponse,
  type PatientSearchResponse,
  type PatientRegistrationInput,
  type StatementBatchCandidate,
  type StatementBatchResponse,
  type OperationalReportsResponse,
  type ProviderActivityReportItem,
  type FacilityActivityReportItem,
  type ClinicalConditionReportItem,
  type ProcedureOrderCreateInput,
  type ProcedureOrderItem,
  type ProcedureOrderQueueItem,
  type ProcedureOrderUpdateInput,
  type ProcedureReportCreateInput,
  type ProcedureReportItem,
  type ProcedureLabProviderDirectoryResponse,
  type ProcedureLabProviderItem,
  type ProcedureLabProviderMutationInput,
  type ProcedureOrderCatalogItem,
  type ProcedureOrderCatalogImportInput,
  type ProcedureOrderCatalogImportResponse,
  type ProcedureOrderCatalogMutationInput,
  type ProcedureOrderCatalogResponse,
  type ProcedureOrderQueueResponse,
  type ProcedureReportBulkSignResponse,
  type ProcedureReportReviewQueueResponse,
  type ProcedureReportSignInput,
  type ProcedureReportUpdateInput,
  type ProcedureSpecimenCreateInput,
  type ProcedureSpecimenItem,
  type ProcedureResultCreateInput,
  type ProcedureResultItem,
  type ProcedureResultUpdateInput,
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

type EncounterProcedureResultSetInput = {
  report: ProcedureReportCreateInput
  result: Omit<ProcedureResultCreateInput, 'reportId'>
}

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
  const [searchStatus, setSearchStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
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
  const [encounterIncludeArchivedDocuments, setEncounterIncludeArchivedDocuments] = useState(false)

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
  const [documentIncludeArchived, setDocumentIncludeArchived] = useState(false)

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
  const [openEmrSessionId, setLegacyEhrSessionId] = useState<string | null>(null)

  const [operationalReports, setOperationalReports] = useState<OperationalReportsResponse | null>(null)
  const [reportsStatus, setReportsStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [reportsError, setReportsError] = useState<string | null>(null)
  const [procedureLabProviders, setProcedureLabProviders] =
    useState<ProcedureLabProviderDirectoryResponse | null>(null)
  const [procedureLabProvidersStatus, setProcedureLabProvidersStatus] =
    useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [procedureLabProvidersError, setProcedureLabProvidersError] = useState<string | null>(null)
  const [procedureLabProvidersIncludeInactive, setProcedureLabProvidersIncludeInactive] = useState(false)
  const [procedureOrderCatalog, setProcedureOrderCatalog] = useState<ProcedureOrderCatalogResponse | null>(null)
  const [procedureOrderCatalogStatus, setProcedureOrderCatalogStatus] =
    useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [procedureOrderCatalogError, setProcedureOrderCatalogError] = useState<string | null>(null)
  const [procedureOrderQueue, setProcedureOrderQueue] = useState<ProcedureOrderQueueResponse | null>(null)
  const [procedureOrderQueueStatus, setProcedureOrderQueueStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>(
    'idle',
  )
  const [procedureOrderQueueError, setProcedureOrderQueueError] = useState<string | null>(null)
  const [procedureOrderQueueFilter, setProcedureOrderQueueFilter] = useState('ready-to-send')
  const [procedureOrderQueuePatientFilter, setProcedureOrderQueuePatientFilter] = useState('')
  const [procedureOrderQueueProviderFilter, setProcedureOrderQueueProviderFilter] = useState('')
  const [procedureOrderQueueLabFilter, setProcedureOrderQueueLabFilter] = useState('')
  const [procedureOrderQueueFromDate, setProcedureOrderQueueFromDate] = useState('')
  const [procedureOrderQueueToDate, setProcedureOrderQueueToDate] = useState('')
  const [procedureOrderQueueRefreshKey, setProcedureOrderQueueRefreshKey] = useState(0)
  const [procedureReportReviewQueue, setProcedureReportReviewQueue] =
    useState<ProcedureReportReviewQueueResponse | null>(null)
  const [procedureReportReviewQueueStatus, setProcedureReportReviewQueueStatus] =
    useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [procedureReportReviewQueueError, setProcedureReportReviewQueueError] = useState<string | null>(null)
  const [procedureReportReviewQueueFilter, setProcedureReportReviewQueueFilter] = useState('unreviewed')
  const [procedureReportReviewQueuePatientFilter, setProcedureReportReviewQueuePatientFilter] = useState('')
  const [procedureReportReviewQueueProviderFilter, setProcedureReportReviewQueueProviderFilter] = useState('')
  const [procedureReportReviewQueueLabFilter, setProcedureReportReviewQueueLabFilter] = useState('')
  const [procedureReportReviewQueueFromDate, setProcedureReportReviewQueueFromDate] = useState('')
  const [procedureReportReviewQueueToDate, setProcedureReportReviewQueueToDate] = useState('')
  const [procedureReportReviewQueueRefreshKey, setProcedureReportReviewQueueRefreshKey] = useState(0)

  useEffect(() => {
    if (!openEmrSessionId) {
      setSearchResult(null)
      setSelectedPatientId(null)
      setChart(null)
      setSearchStatus('idle')
      setChartStatus('idle')
      setPatientError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setSearchStatus('loading')
      setPatientError(null)

      try {
        const result = await searchPatients(query, openEmrSessionId, controller.signal)
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
  }, [query, openEmrSessionId])

  useEffect(() => {
    if (!selectedPatientId || !openEmrSessionId) {
      setChartStatus('idle')
      setChart(null)
      return
    }

    const controller = new AbortController()
    async function loadChart() {
      setChartStatus('loading')
      try {
        const patient = await getPatientChart(selectedPatientId!, openEmrSessionId, controller.signal)
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
  }, [selectedPatientId, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'calendar') {
      return
    }

    if (!openEmrSessionId) {
      setAppointmentStatus('idle')
      setAppointmentError(null)
      setAppointmentResult(null)
      setSelectedAppointmentId(null)
      setAppointmentDetail(null)
      setAppointmentDetailStatus('idle')
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setAppointmentStatus('loading')
      setAppointmentError(null)

      try {
        const result = await searchAppointments(
          appointmentPatientId,
          appointmentFromDate,
          openEmrSessionId,
          controller.signal,
        )
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
  }, [activeModule, appointmentPatientId, appointmentFromDate, appointmentRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'calendar' || !selectedAppointmentId || !openEmrSessionId) {
      setAppointmentDetailStatus('idle')
      setAppointmentDetail(null)
      return
    }

    const controller = new AbortController()
    async function loadAppointmentDetail() {
      setAppointmentDetailStatus('loading')
      try {
        const detail = await getAppointmentDetail(selectedAppointmentId!, openEmrSessionId, controller.signal)
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
  }, [activeModule, selectedAppointmentId, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'encounters') {
      return
    }

    if (!openEmrSessionId) {
      setEncounterResult(null)
      setEncounterStatus('idle')
      setEncounterError(null)
      setSelectedEncounter(null)
      setEncounterDetail(null)
      setEncounterDetailStatus('idle')
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setEncounterStatus('loading')
      setEncounterError(null)

      try {
        const result = await searchEncounters(encounterPatientId, encounterFromDate, openEmrSessionId, controller.signal)
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
  }, [activeModule, encounterPatientId, encounterFromDate, encounterRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'encounters' || selectedEncounter === null || !openEmrSessionId) {
      setEncounterDetailStatus('idle')
      setEncounterDetail(null)
      return
    }

    const controller = new AbortController()
    async function loadEncounterDetail() {
      setEncounterDetailStatus('loading')
      try {
        const detail = await getEncounterDetail(
          selectedEncounter!,
          openEmrSessionId,
          controller.signal,
          encounterIncludeArchivedDocuments,
        )
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
  }, [activeModule, selectedEncounter, encounterIncludeArchivedDocuments, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'lists') {
      return
    }

    if (!openEmrSessionId) {
      setClinicalLists(null)
      setClinicalStatus('idle')
      setClinicalError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setClinicalStatus('loading')
      setClinicalError(null)

      try {
        const result = await getClinicalLists(clinicalPatientId, openEmrSessionId, controller.signal)
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
  }, [activeModule, clinicalPatientId, clinicalRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'messages') {
      return
    }

    if (!openEmrSessionId) {
      setPatientMessages(null)
      setMessageStatus('idle')
      setMessageError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setMessageStatus('loading')
      setMessageError(null)

      try {
        const result = await getPatientMessages(messagePatientId, openEmrSessionId, controller.signal)
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
  }, [activeModule, messagePatientId, messageRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'documents') {
      return
    }

    if (!openEmrSessionId) {
      setPatientDocuments(null)
      setDocumentStatus('idle')
      setDocumentError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setDocumentStatus('loading')
      setDocumentError(null)

      try {
        const result = await getPatientDocuments(
          documentPatientId,
          documentIncludeArchived,
          openEmrSessionId,
          controller.signal,
        )
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
  }, [activeModule, documentPatientId, documentIncludeArchived, documentRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'procedures') {
      return
    }
    if (!openEmrSessionId) {
      setProcedureResults(null)
      setProcedureStatus('idle')
      setProcedureError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setProcedureStatus('loading')
      setProcedureError(null)

      try {
        const result = await getProcedureResults(procedurePatientId, openEmrSessionId, controller.signal)
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
  }, [activeModule, procedurePatientId, procedureRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'fees') {
      return
    }
    if (!openEmrSessionId) {
      setPatientBilling(null)
      setBillingStatus('idle')
      setBillingError(null)
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setBillingStatus('loading')
      setBillingError(null)

      try {
        const result = await getPatientBilling(billingPatientId, openEmrSessionId, controller.signal)
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
  }, [activeModule, billingPatientId, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'admin') {
      return
    }
    if (!openEmrSessionId) {
      setAdministrationDirectory(null)
      setAdministrationStatus('idle')
      setAdministrationError(null)
      return
    }

    const controller = new AbortController()
    async function loadAdministrationDirectory() {
      setAdministrationStatus('loading')
      setAdministrationError(null)

      try {
        const result = await getAdministrationDirectory(openEmrSessionId, controller.signal)
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
  }, [activeModule, administrationRefreshKey, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'reports') {
      return
    }

    if (!openEmrSessionId) {
      setOperationalReports(null)
      setReportsStatus('idle')
      setReportsError(null)
      return
    }

    const controller = new AbortController()

    async function loadOperationalReports() {
      setReportsStatus('loading')
      setReportsError(null)

      try {
        const result = await getOperationalReports(openEmrSessionId, controller.signal)
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
  }, [activeModule, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'reports') {
      return
    }
    if (!openEmrSessionId) {
      setProcedureLabProviders(null)
      setProcedureLabProvidersStatus('idle')
      setProcedureLabProvidersError(null)
      return
    }

    const controller = new AbortController()

    async function loadProcedureLabProviders() {
      setProcedureLabProvidersStatus('loading')
      setProcedureLabProvidersError(null)

      try {
        const result = await getProcedureLabProviders(
          procedureLabProvidersIncludeInactive,
          openEmrSessionId,
          controller.signal,
        )
        setProcedureLabProviders(result)
        setProcedureLabProvidersStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setProcedureLabProvidersStatus('error')
          setProcedureLabProvidersError(
            loadError instanceof Error ? loadError.message : 'Procedure lab provider directory failed',
          )
        }
      }
    }

    loadProcedureLabProviders()
    return () => controller.abort()
  }, [activeModule, procedureLabProvidersIncludeInactive, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'reports' && activeModule !== 'procedures') {
      return
    }
    if (!openEmrSessionId) {
      setProcedureOrderCatalog(null)
      setProcedureOrderCatalogStatus('idle')
      setProcedureOrderCatalogError(null)
      return
    }

    const controller = new AbortController()

    async function loadProcedureOrderCatalog() {
      setProcedureOrderCatalogStatus('loading')
      setProcedureOrderCatalogError(null)

      try {
        const result = await getProcedureOrderCatalog(openEmrSessionId, controller.signal)
        setProcedureOrderCatalog(result)
        setProcedureOrderCatalogStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setProcedureOrderCatalogStatus('error')
          setProcedureOrderCatalogError(
            loadError instanceof Error ? loadError.message : 'Procedure order catalog failed',
          )
        }
      }
    }

    loadProcedureOrderCatalog()
    return () => controller.abort()
  }, [activeModule, openEmrSessionId])

  useEffect(() => {
    if (activeModule !== 'reports') {
      return
    }
    if (!openEmrSessionId) {
      setProcedureOrderQueue(null)
      setProcedureOrderQueueStatus('idle')
      setProcedureOrderQueueError(null)
      return
    }

    const controller = new AbortController()

    async function loadProcedureOrderQueue() {
      setProcedureOrderQueueStatus('loading')
      setProcedureOrderQueueError(null)

      try {
        const result = await getProcedureOrderQueue(
          procedureOrderQueueFilter,
          {
            patientId: procedureOrderQueuePatientFilter,
            providerId: procedureOrderQueueProviderFilter,
            labId: procedureOrderQueueLabFilter,
            fromDate: procedureOrderQueueFromDate,
            toDate: procedureOrderQueueToDate,
          },
          openEmrSessionId,
          controller.signal,
        )
        setProcedureOrderQueue(result)
        setProcedureOrderQueueStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setProcedureOrderQueueStatus('error')
          setProcedureOrderQueueError(loadError instanceof Error ? loadError.message : 'Procedure order queue failed')
        }
      }
    }

    loadProcedureOrderQueue()
    return () => controller.abort()
  }, [
    activeModule,
    procedureOrderQueueFilter,
    procedureOrderQueuePatientFilter,
    procedureOrderQueueProviderFilter,
    procedureOrderQueueLabFilter,
    procedureOrderQueueFromDate,
    procedureOrderQueueToDate,
    procedureOrderQueueRefreshKey,
    openEmrSessionId,
  ])

  useEffect(() => {
    if (activeModule !== 'reports') {
      return
    }
    if (!openEmrSessionId) {
      setProcedureReportReviewQueue(null)
      setProcedureReportReviewQueueStatus('idle')
      setProcedureReportReviewQueueError(null)
      return
    }

    const controller = new AbortController()

    async function loadProcedureReportReviewQueue() {
      setProcedureReportReviewQueueStatus('loading')
      setProcedureReportReviewQueueError(null)

      try {
        const result = await getProcedureReportReviewQueue(
          procedureReportReviewQueueFilter,
          {
            patientId: procedureReportReviewQueuePatientFilter,
            providerId: procedureReportReviewQueueProviderFilter,
            labId: procedureReportReviewQueueLabFilter,
            fromDate: procedureReportReviewQueueFromDate,
            toDate: procedureReportReviewQueueToDate,
          },
          openEmrSessionId,
          controller.signal,
        )
        setProcedureReportReviewQueue(result)
        setProcedureReportReviewQueueStatus('ready')
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setProcedureReportReviewQueueStatus('error')
          setProcedureReportReviewQueueError(
            loadError instanceof Error ? loadError.message : 'Procedure report review queue failed',
          )
        }
      }
    }

    loadProcedureReportReviewQueue()
    return () => controller.abort()
  }, [
    activeModule,
    procedureReportReviewQueueFilter,
    procedureReportReviewQueuePatientFilter,
    procedureReportReviewQueueProviderFilter,
    procedureReportReviewQueueLabFilter,
    procedureReportReviewQueueFromDate,
    procedureReportReviewQueueToDate,
    procedureReportReviewQueueRefreshKey,
    openEmrSessionId,
  ])

  const selectedFromList = useMemo(
    () => searchResult?.patients.find((patient) => patient.canonicalId === selectedPatientId) ?? null,
    [searchResult, selectedPatientId],
  )

  const activePatient = chart ?? selectedFromList

  function getActiveLegacyEhrSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access patient data.')
    }

    return openEmrSessionId
  }

  function getActiveClinicalListSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access clinical lists.')
    }

    return openEmrSessionId
  }

  function getActiveMessageSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access patient messages.')
    }

    return openEmrSessionId
  }

  function getActiveAppointmentSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access appointment schedules.')
    }

    return openEmrSessionId
  }

  function getActiveEncounterSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access encounters.')
    }

    return openEmrSessionId
  }

  function getActiveDocumentSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access documents.')
    }

    return openEmrSessionId
  }

  function getActiveBillingSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access billing.')
    }

    return openEmrSessionId
  }

  function getActiveProcedureSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to access procedure data.')
    }

    return openEmrSessionId
  }

  async function handlePatientContactSave(patientId: string, contact: PatientContactUpdate) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await updatePatientContact(patientId, contact, sessionId)
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

  async function handlePatientDemographicsSave(patientId: string, demographics: PatientDemographicsUpdate) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await updatePatientDemographics(patientId, demographics, sessionId)
      setChart(updated)
      setSelectedPatientId(updated.canonicalId)
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
                  displayName: updated.displayName,
                  firstName: updated.firstName,
                  lastName: updated.lastName,
                  preferredName: updated.preferredName,
                  sex: updated.sex,
                  dateOfBirth: updated.dateOfBirth,
                  age: updated.age,
                  purpose: updated.purpose,
                  phone: updated.phone,
                  phoneHome: updated.phoneHome,
                  phoneCell: updated.phoneCell,
                  email: updated.email,
                  facilityName: updated.facilityName,
                  primaryProviderName: updated.primaryProviderName,
                }
              : patient,
          ),
        }
      })
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient demographics save failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientDeceasedStatusSave(patientId: string, status: PatientDeceasedStatusUpdate) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await updatePatientDeceasedStatus(patientId, status, sessionId)
      setChart(updated)
      setChartStatus('ready')
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient deceased status save failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientGuardianContactSave(patientId: string, guardianContact: PatientGuardianContactUpdate) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await updatePatientGuardianContact(patientId, guardianContact, sessionId)
      setChart(updated)
      setChartStatus('ready')
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient guardian contact save failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientCreate(input: PatientRegistrationInput) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const created = await createPatient(input, sessionId)
      setChart(created)
      setSelectedPatientId(created.canonicalId)
      setQuery(created.pubpid)
      setSearchStatus('ready')
      setChartStatus('ready')
      setSearchResult((current) => {
        if (!current) {
          return current
        }

        const withoutCreated = current.patients.filter((patient) => patient.canonicalId !== created.canonicalId)
        return {
          ...current,
          totalMatches: Math.max(current.totalMatches, 1),
          patients: [created, ...withoutCreated].slice(0, current.limit),
        }
      })
      return created
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient registration failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientInsuranceCreate(patientId: string, insurance: PatientInsuranceMutationInput) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await createPatientInsurance(patientId, insurance, sessionId)
      setChart(updated)
      setChartStatus('ready')
      return updated
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient insurance create failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientInsuranceUpdate(insuranceId: string, insurance: PatientInsuranceMutationInput) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await updatePatientInsurance(insuranceId, insurance, sessionId)
      setChart(updated)
      setChartStatus('ready')
      return updated
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient insurance update failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handlePatientInsuranceDelete(insuranceId: string) {
    setChartStatus('loading')
    setPatientError(null)

    try {
      const sessionId = getActiveLegacyEhrSessionId()
      const updated = await deletePatientInsurance(insuranceId, sessionId)
      setChart(updated)
      setChartStatus('ready')
      return updated
    } catch (saveError) {
      setChartStatus('error')
      const message = saveError instanceof Error ? saveError.message : 'Patient insurance delete failed'
      setPatientError(message)
      throw saveError
    }
  }

  async function handleAppointmentCreate(input: AppointmentCreateInput) {
    setAppointmentStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const created = await createAppointment(input, sessionId)
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

  async function handleAppointmentUpdate(appointment: AppointmentDetail, input: AppointmentUpdateInput) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await updateAppointment(appointment.id, input, sessionId)
      setAppointmentPatientId(updated.patientId)
      setAppointmentFromDate(updated.date)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetail(updated)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (updateError) {
      setAppointmentDetailStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Appointment update failed'
      setAppointmentError(message)
      throw updateError
    }
  }

  async function handleAppointmentOccurrenceReschedule(
    appointment: AppointmentDetail,
    input: AppointmentOccurrenceRescheduleInput,
  ) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await rescheduleAppointmentOccurrence(appointment.seriesRootId, appointment.date, input, sessionId)
      setAppointmentPatientId(updated.patientId)
      setAppointmentFromDate(updated.date)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetail(updated)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (updateError) {
      setAppointmentDetailStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Appointment occurrence reschedule failed'
      setAppointmentError(message)
      throw updateError
    }
  }

  async function handleAppointmentCancel(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await updateAppointmentStatus(appointment.id, {
        status: 'x',
        title: appointment.title.endsWith('Cancelled') ? appointment.title : `${appointment.title} Cancelled`,
      }, sessionId)
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

  async function handleAppointmentArrive(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await updateAppointmentStatus(appointment.id, {
        status: '@',
        title: appointment.title.endsWith('Arrived') ? appointment.title : `${appointment.title} Arrived`,
      }, sessionId)
      setAppointmentDetail(updated)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (arrivalError) {
      setAppointmentDetailStatus('error')
      const message = arrivalError instanceof Error ? arrivalError.message : 'Appointment arrival update failed'
      setAppointmentError(message)
      throw arrivalError
    }
  }

  async function handleAppointmentCheckOut(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const baseTitle = appointment.title.endsWith(' Arrived')
        ? appointment.title.slice(0, -' Arrived'.length)
        : appointment.title
      const sessionId = getActiveAppointmentSessionId()
      const updated = await updateAppointmentStatus(appointment.id, {
        status: '>',
        title: baseTitle.endsWith('Checked Out') ? baseTitle : `${baseTitle} Checked Out`,
      }, sessionId)
      setAppointmentDetail(updated)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (checkoutError) {
      setAppointmentDetailStatus('error')
      const message = checkoutError instanceof Error ? checkoutError.message : 'Appointment check-out update failed'
      setAppointmentError(message)
      throw checkoutError
    }
  }

  async function handleAppointmentNoShow(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await updateAppointmentStatus(appointment.id, {
        status: '?',
        title: appointment.title.endsWith('No Show') ? appointment.title : `${appointment.title} No Show`,
      }, sessionId)
      setAppointmentDetail(updated)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (noShowError) {
      setAppointmentDetailStatus('error')
      const message = noShowError instanceof Error ? noShowError.message : 'Appointment no-show update failed'
      setAppointmentError(message)
      throw noShowError
    }
  }

  async function handleAppointmentDelete(appointment: AppointmentDetail) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      await deleteAppointment(appointment.id, sessionId)
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

  async function handleAppointmentOccurrenceRestore(appointment: AppointmentDetail, occurrenceDate: string) {
    setAppointmentDetailStatus('loading')
    setAppointmentError(null)

    try {
      const sessionId = getActiveAppointmentSessionId()
      const updated = await restoreAppointmentOccurrence(appointment.seriesRootId, occurrenceDate, sessionId)
      setSelectedAppointmentId(updated.id)
      setAppointmentDetail(updated)
      setAppointmentDetailStatus('ready')
      setAppointmentRefreshKey((current) => current + 1)
      return updated
    } catch (restoreError) {
      setAppointmentDetailStatus('error')
      const message = restoreError instanceof Error ? restoreError.message : 'Appointment occurrence restore failed'
      setAppointmentError(message)
      throw restoreError
    }
  }

  async function handleEncounterCreate(input: EncounterCreateInput) {
    setEncounterStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const created = await createEncounter(input, sessionId)
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
      const sessionId = getActiveEncounterSessionId()
      const updated = await updateEncounter(encounter.encounter, update, sessionId)
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
      const sessionId = getActiveEncounterSessionId()
      await deleteEncounter(encounter.encounter, sessionId)
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
      const sessionId = getActiveEncounterSessionId()
      const response = await createEncounterVitals(encounter.encounter, input, sessionId)
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
      const sessionId = getActiveEncounterSessionId()
      const response = await createEncounterSoapNote(encounter.encounter, input, sessionId)
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

  async function handleEncounterSign(
    encounter: EncounterDetail,
    input: EncounterSignInput,
  ): Promise<EncounterSignatureMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await signEncounter(encounter.encounter, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (signError) {
      setEncounterDetailStatus('error')
      const message = signError instanceof Error ? signError.message : 'Encounter sign-off failed'
      setEncounterError(message)
      throw signError
    }
  }

  async function handleEncounterSignatureDelete(encounter: EncounterDetail, signature: EncounterSignatureItem) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      await deleteEncounterSignature(encounter.encounter, signature.id, sessionId)
      const refreshed = await getEncounterDetail(encounter.encounter, sessionId)
      setEncounterDetail(refreshed)
      setSelectedEncounter(refreshed.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setEncounterDetailStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Encounter signature delete failed'
      setEncounterError(message)
      throw deleteError
    }
  }

  async function handleEncounterDocumentCreate(
    encounter: EncounterDetail,
    input: EncounterDocumentCreateInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await createEncounterDocument(encounter.encounter, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'Encounter document attach failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterBinaryDocumentCreate(
    encounter: EncounterDetail,
    input: EncounterBinaryDocumentCreateInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await createEncounterBinaryDocument(encounter.encounter, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'Binary encounter document attach failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterExternalLinkDocumentCreate(
    encounter: EncounterDetail,
    input: EncounterExternalLinkDocumentCreateInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await createEncounterExternalLinkDocument(encounter.encounter, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'External-link encounter document attach failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterDocumentMetadataUpdate(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentMetadataUpdateInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await updateEncounterDocumentMetadata(encounter.encounter, document.id, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (metadataError) {
      setEncounterDetailStatus('error')
      const message = metadataError instanceof Error ? metadataError.message : 'Encounter document metadata update failed'
      setEncounterError(message)
      throw metadataError
    }
  }

  async function handleEncounterDocumentMove(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    targetEncounter: number,
  ): Promise<EncounterDocumentMoveResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await moveEncounterDocument(encounter.encounter, document.id, { targetEncounter }, sessionId)
      setEncounterDetail(response.targetDetail)
      setSelectedEncounter(response.targetDetail.encounter)
      setEncounterPatientId(response.targetDetail.pubpid)
      setEncounterFromDate(response.targetDetail.date)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (moveError) {
      setEncounterDetailStatus('error')
      const message = moveError instanceof Error ? moveError.message : 'Encounter document move failed'
      setEncounterError(message)
      throw moveError
    }
  }

  async function handleEncounterDocumentContentReplace(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentContentReplaceInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await replaceEncounterDocumentContent(encounter.encounter, document.id, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (replaceError) {
      setEncounterDetailStatus('error')
      const message =
        replaceError instanceof Error ? replaceError.message : 'Encounter document content replacement failed'
      setEncounterError(message)
      throw replaceError
    }
  }

  async function handleEncounterDocumentBinaryContentReplace(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentBinaryContentReplaceInput,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await replaceEncounterDocumentBinaryContent(encounter.encounter, document.id, input, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (replaceError) {
      setEncounterDetailStatus('error')
      const message =
        replaceError instanceof Error ? replaceError.message : 'Encounter binary document content replacement failed'
      setEncounterError(message)
      throw replaceError
    }
  }

  async function handleEncounterDocumentArchive(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await softDeleteEncounterDocument(encounter.encounter, document.id, sessionId)
      setEncounterIncludeArchivedDocuments(true)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (archiveError) {
      setEncounterDetailStatus('error')
      const message = archiveError instanceof Error ? archiveError.message : 'Encounter document archive failed'
      setEncounterError(message)
      throw archiveError
    }
  }

  async function handleEncounterDocumentRestore(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await restoreEncounterDocument(encounter.encounter, document.id, sessionId)
      setEncounterIncludeArchivedDocuments(true)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (restoreError) {
      setEncounterDetailStatus('error')
      const message = restoreError instanceof Error ? restoreError.message : 'Encounter document restore failed'
      setEncounterError(message)
      throw restoreError
    }
  }

  async function handleEncounterDocumentSign(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await signEncounterDocument(encounter.encounter, document.id, {
        reviewStatus: 'approved',
        reviewedBy: 'admin',
      }, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (signError) {
      setEncounterDetailStatus('error')
      const message = signError instanceof Error ? signError.message : 'Encounter document sign-off failed'
      setEncounterError(message)
      throw signError
    }
  }

  async function handleEncounterDocumentDeny(
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ): Promise<EncounterDocumentMutationResponse> {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await denyEncounterDocument(encounter.encounter, document.id, {
        reviewStatus: 'denied',
        reviewedBy: 'admin',
      }, sessionId)
      setEncounterDetail(response.detail)
      setSelectedEncounter(response.detail.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (denyError) {
      setEncounterDetailStatus('error')
      const message = denyError instanceof Error ? denyError.message : 'Encounter document denial failed'
      setEncounterError(message)
      throw denyError
    }
  }

  async function handleEncounterFeeSheetLineCreate(
    encounter: EncounterDetail,
    input: BillingLineCreateInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await createBillingLine(input, sessionId)
      const refreshed = await getEncounterDetail(encounter.encounter, sessionId)
      setEncounterDetail(refreshed)
      setSelectedEncounter(refreshed.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'Encounter fee sheet line create failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterProcedureOrderCreate(
    encounter: EncounterDetail,
    input: ProcedureOrderCreateInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await createProcedureOrder(input, sessionId)
      const refreshed = await getEncounterDetail(encounter.encounter, sessionId)
      setEncounterDetail(refreshed)
      setSelectedEncounter(refreshed.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'Encounter procedure order create failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterProcedureResultSetCreate(
    encounter: EncounterDetail,
    input: EncounterProcedureResultSetInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const reportResponse = await createProcedureReport(input.report, sessionId)
      const resultResponse = await createProcedureResult({
        ...input.result,
        reportId: reportResponse.id,
      }, sessionId)
      const refreshed = await getEncounterDetail(encounter.encounter, sessionId)
      setEncounterDetail(refreshed)
      setSelectedEncounter(refreshed.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return {
        reportId: reportResponse.id,
        resultId: resultResponse.id,
      }
    } catch (createError) {
      setEncounterDetailStatus('error')
      const message = createError instanceof Error ? createError.message : 'Encounter procedure result create failed'
      setEncounterError(message)
      throw createError
    }
  }

  async function handleEncounterProcedureResultUpdate(
    encounter: EncounterDetail,
    result: ProcedureResultItem,
    input: ProcedureResultUpdateInput,
  ) {
    setEncounterDetailStatus('loading')
    setEncounterError(null)

    try {
      const sessionId = getActiveEncounterSessionId()
      const response = await updateProcedureResult(result.id, input, sessionId)
      const refreshed = await getEncounterDetail(encounter.encounter, sessionId)
      setEncounterDetail(refreshed)
      setSelectedEncounter(refreshed.encounter)
      setEncounterDetailStatus('ready')
      setEncounterRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setEncounterDetailStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Encounter procedure result update failed'
      setEncounterError(message)
      throw updateError
    }
  }

  async function handleClinicalAllergyCreate(input: ClinicalAllergyCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await createClinicalAllergy(input, sessionId)
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
      const sessionId = getActiveClinicalListSessionId()
      const response = await deactivateClinicalAllergy(allergy.id, {
        comments: 'Deactivated from the modernized Lists workspace.',
      }, sessionId)
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
      const sessionId = getActiveClinicalListSessionId()
      await deleteClinicalAllergy(allergy.id, sessionId)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId, sessionId)
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

  async function handleClinicalProblemCreate(input: ClinicalProblemCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await createClinicalProblem(input, sessionId)
      setClinicalPatientId(response.detail.patientId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setClinicalStatus('error')
      const message = createError instanceof Error ? createError.message : 'Clinical problem create failed'
      setClinicalError(message)
      throw createError
    }
  }

  async function handleClinicalProblemDeactivate(problem: ProblemListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await deactivateClinicalProblem(problem.id, {
        comments: 'Deactivated from the modernized Lists workspace.',
      }, sessionId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (deactivateError) {
      setClinicalStatus('error')
      const message = deactivateError instanceof Error ? deactivateError.message : 'Clinical problem deactivate failed'
      setClinicalError(message)
      throw deactivateError
    }
  }

  async function handleClinicalProblemDelete(problem: ProblemListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      await deleteClinicalProblem(problem.id, sessionId)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId, sessionId)
      setClinicalLists(refreshed)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setClinicalStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Clinical problem delete failed'
      setClinicalError(message)
      throw deleteError
    }
  }

  async function handleClinicalMedicationCreate(input: ClinicalMedicationCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await createClinicalMedication(input, sessionId)
      setClinicalPatientId(response.detail.patientId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setClinicalStatus('error')
      const message = createError instanceof Error ? createError.message : 'Clinical medication create failed'
      setClinicalError(message)
      throw createError
    }
  }

  async function handleClinicalMedicationDeactivate(medication: MedicationListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await deactivateClinicalMedication(medication.id, {
        comments: 'Deactivated from the modernized Lists workspace.',
      }, sessionId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (deactivateError) {
      setClinicalStatus('error')
      const message = deactivateError instanceof Error ? deactivateError.message : 'Clinical medication deactivate failed'
      setClinicalError(message)
      throw deactivateError
    }
  }

  async function handleClinicalMedicationDelete(medication: MedicationListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      await deleteClinicalMedication(medication.id, sessionId)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId, sessionId)
      setClinicalLists(refreshed)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setClinicalStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Clinical medication delete failed'
      setClinicalError(message)
      throw deleteError
    }
  }

  async function handleClinicalPrescriptionCreate(input: ClinicalPrescriptionCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await createClinicalPrescription(input, sessionId)
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
      const sessionId = getActiveClinicalListSessionId()
      const response = await deactivateClinicalPrescription(prescription.id, {
        endDate: '2026-08-15',
        note: 'Deactivated from the modernized Lists workspace.',
      }, sessionId)
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
      const sessionId = getActiveClinicalListSessionId()
      await deleteClinicalPrescription(prescription.id, sessionId)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId, sessionId)
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

  async function handleClinicalImmunizationCreate(input: ClinicalImmunizationCreateInput) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await createClinicalImmunization(input, sessionId)
      setClinicalPatientId(response.detail.patientId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setClinicalStatus('error')
      const message = createError instanceof Error ? createError.message : 'Clinical immunization create failed'
      setClinicalError(message)
      throw createError
    }
  }

  async function handleClinicalImmunizationEnteredInError(immunization: ImmunizationListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      const response = await markClinicalImmunizationEnteredInError(immunization.id, {
        note: 'Marked entered in error from the modernized Lists workspace.',
      }, sessionId)
      setClinicalLists(response.detail)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setClinicalStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Clinical immunization update failed'
      setClinicalError(message)
      throw updateError
    }
  }

  async function handleClinicalImmunizationDelete(immunization: ImmunizationListItem) {
    setClinicalStatus('loading')
    setClinicalError(null)

    try {
      const sessionId = getActiveClinicalListSessionId()
      await deleteClinicalImmunization(immunization.id, sessionId)
      const refreshed = await getClinicalLists(clinicalLists?.patientId ?? clinicalPatientId, sessionId)
      setClinicalLists(refreshed)
      setClinicalStatus('ready')
      setClinicalRefreshKey((current) => current + 1)
    } catch (deleteError) {
      setClinicalStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Clinical immunization delete failed'
      setClinicalError(message)
      throw deleteError
    }
  }

  async function handleBillingLineCreate(input: BillingLineCreateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await createBillingLine(input, sessionId)
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

  async function handleBillingLineUpdate(lineId: string, input: BillingLineUpdateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await updateBillingLine(lineId, input, sessionId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (updateError) {
      setBillingStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Billing line update failed'
      setBillingError(message)
      throw updateError
    }
  }

  async function handleBillingLineDeactivate(line: BillingLineItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await updateBillingLineStatus(line.id, {
        billed: 1,
        activity: 0,
      }, sessionId)
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
      const sessionId = getActiveBillingSessionId()
      await deleteBillingLine(line.id, sessionId)
      const refreshed = await getPatientBilling(patientBilling?.patientId ?? billingPatientId, sessionId)
      setPatientBilling(refreshed)
      setBillingStatus('ready')
    } catch (deleteError) {
      setBillingStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Billing line delete failed'
      setBillingError(message)
      throw deleteError
    }
  }

  async function handleBillingClaimCreate(input: BillingClaimCreateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await createBillingClaimStatus(input, sessionId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (createError) {
      setBillingStatus('error')
      const message = createError instanceof Error ? createError.message : 'Billing claim status create failed'
      setBillingError(message)
      throw createError
    }
  }

  async function handleBillingClaimStatusUpdate(claim: BillingClaimItem, input: BillingClaimStatusUpdateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await updateBillingClaimStatus(claim.id, input, sessionId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (updateError) {
      setBillingStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Billing claim status update failed'
      setBillingError(message)
      throw updateError
    }
  }

  async function handleBillingClaimDelete(claim: BillingClaimItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      await deleteBillingClaimStatus(claim.id, sessionId)
      const refreshed = await getPatientBilling(patientBilling?.patientId ?? billingPatientId, sessionId)
      setPatientBilling(refreshed)
      setBillingStatus('ready')
    } catch (deleteError) {
      setBillingStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Billing claim status delete failed'
      setBillingError(message)
      throw deleteError
    }
  }

  async function handleBillingPaymentCreate(input: BillingPaymentCreateInput) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await createBillingPaymentPosting(input, sessionId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (createError) {
      setBillingStatus('error')
      const message = createError instanceof Error ? createError.message : 'Billing payment posting create failed'
      setBillingError(message)
      throw createError
    }
  }

  async function handleBillingPaymentVoid(payment: BillingPaymentItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      const response = await voidBillingPaymentPosting(payment.activityId, sessionId)
      setPatientBilling(response.detail)
      setBillingStatus('ready')
      return response
    } catch (voidError) {
      setBillingStatus('error')
      const message = voidError instanceof Error ? voidError.message : 'Billing payment posting void failed'
      setBillingError(message)
      throw voidError
    }
  }

  async function handleBillingPaymentDelete(payment: BillingPaymentItem) {
    setBillingStatus('loading')
    setBillingError(null)

    try {
      const sessionId = getActiveBillingSessionId()
      await deleteBillingPaymentPosting(payment.activityId, sessionId)
      const refreshed = await getPatientBilling(patientBilling?.patientId ?? billingPatientId, sessionId)
      setPatientBilling(refreshed)
      setBillingStatus('ready')
    } catch (deleteError) {
      setBillingStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Billing payment posting delete failed'
      setBillingError(message)
      throw deleteError
    }
  }

  async function handleProcedureOrderCreate(input: ProcedureOrderCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureOrder(input, sessionId)
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
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureOrderStatus(order.id, { status: 'complete' }, sessionId)
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

  async function handleProcedureOrderUpdate(order: ProcedureOrderItem, input: ProcedureOrderUpdateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureOrder(order.id, input, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setProcedureStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Procedure order update failed'
      setProcedureError(message)
      throw updateError
    }
  }

  async function handleProcedureOrderTransmit(order: ProcedureOrderQueueItem) {
    setProcedureOrderQueueStatus('loading')
    setProcedureOrderQueueError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      await transmitProcedureOrder(order.orderId, { transmittedAt: new Date().toISOString() }, sessionId)
      setProcedureOrderQueueFilter('transmitted-pending')
      setProcedureOrderQueueRefreshKey((current) => current + 1)
    } catch (transmitError) {
      setProcedureOrderQueueStatus('error')
      const message = transmitError instanceof Error ? transmitError.message : 'Procedure order transmit failed'
      setProcedureOrderQueueError(message)
      throw transmitError
    }
  }

  async function handleProcedureReportBulkSign(reportIds: number[]): Promise<ProcedureReportBulkSignResponse> {
    setProcedureReportReviewQueueStatus('loading')
    setProcedureReportReviewQueueError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await bulkSignProcedureReports({
        reportIds,
        reviewedBy: 'admin',
        reviewedAt: '2026-06-21 11:25:00',
      }, sessionId)
      setProcedureReportReviewQueueFilter('reviewed')
      setProcedureReportReviewQueueRefreshKey((current) => current + 1)
      return response
    } catch (bulkSignError) {
      setProcedureReportReviewQueueStatus('error')
      const message = bulkSignError instanceof Error ? bulkSignError.message : 'Procedure report bulk sign-off failed'
      setProcedureReportReviewQueueError(message)
      throw bulkSignError
    }
  }

  async function handleProcedureReportCreate(input: ProcedureReportCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureReport(input, sessionId)
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

  async function handleProcedureReportUpdate(report: ProcedureReportItem, input: ProcedureReportUpdateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureReport(report.id, input, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setProcedureStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Procedure report update failed'
      setProcedureError(message)
      throw updateError
    }
  }

  async function handleProcedureReportSign(report: ProcedureReportItem, input: ProcedureReportSignInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await signProcedureReport(report.id, input, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      setProcedureReportReviewQueueRefreshKey((current) => current + 1)
      return response
    } catch (signError) {
      setProcedureStatus('error')
      const message = signError instanceof Error ? signError.message : 'Procedure report sign-off failed'
      setProcedureError(message)
      throw signError
    }
  }

  async function handleProcedureReportReviewReopen(report: ProcedureReportItem) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await reopenProcedureReportReview(report.id, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      setProcedureReportReviewQueueRefreshKey((current) => current + 1)
      return response
    } catch (reopenError) {
      setProcedureStatus('error')
      const message = reopenError instanceof Error ? reopenError.message : 'Procedure report review reopen failed'
      setProcedureError(message)
      throw reopenError
    }
  }

  async function handleProcedureSpecimenCreate(input: ProcedureSpecimenCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureSpecimen(input, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setProcedureStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure specimen create failed'
      setProcedureError(message)
      throw createError
    }
  }

  async function handleProcedureResultCreate(input: ProcedureResultCreateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureResult(input, sessionId)
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

  async function handleProcedureResultUpdate(result: ProcedureResultItem, input: ProcedureResultUpdateInput) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureResult(result.id, input, sessionId)
      setProcedureResults(response.detail)
      setProcedureStatus('ready')
      setProcedureRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setProcedureStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Procedure result update failed'
      setProcedureError(message)
      throw updateError
    }
  }

  async function handleProcedureOrderDelete(order: ProcedureOrderItem) {
    setProcedureStatus('loading')
    setProcedureError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      await deleteProcedureOrder(order.id, sessionId)
      const refreshed = await getProcedureResults(procedureResults?.patientId ?? procedurePatientId, sessionId)
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

  async function handleProcedureLabProviderCreate(input: ProcedureLabProviderMutationInput) {
    setProcedureLabProvidersStatus('loading')
    setProcedureLabProvidersError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureLabProvider(input, sessionId)
      setProcedureLabProviders(response.directory)
      setProcedureLabProvidersIncludeInactive(response.directory.includeInactive)
      setProcedureLabProvidersStatus('ready')
      return response
    } catch (createError) {
      setProcedureLabProvidersStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure lab provider create failed'
      setProcedureLabProvidersError(message)
      throw createError
    }
  }

  async function handleProcedureLabProviderUpdate(
    provider: ProcedureLabProviderItem,
    input: ProcedureLabProviderMutationInput,
  ) {
    setProcedureLabProvidersStatus('loading')
    setProcedureLabProvidersError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureLabProvider(provider.id, input, sessionId)
      setProcedureLabProviders(response.directory)
      setProcedureLabProvidersIncludeInactive(response.directory.includeInactive)
      setProcedureLabProvidersStatus('ready')
      return response
    } catch (updateError) {
      setProcedureLabProvidersStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Procedure lab provider update failed'
      setProcedureLabProvidersError(message)
      throw updateError
    }
  }

  async function handleProcedureLabProviderDelete(provider: ProcedureLabProviderItem) {
    setProcedureLabProvidersStatus('loading')
    setProcedureLabProvidersError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      await deleteProcedureLabProvider(provider.id, sessionId)
      const refreshed = await getProcedureLabProviders(procedureLabProvidersIncludeInactive, sessionId)
      setProcedureLabProviders(refreshed)
      setProcedureLabProvidersStatus('ready')
    } catch (deleteError) {
      setProcedureLabProvidersStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Procedure lab provider delete failed'
      setProcedureLabProvidersError(message)
      throw deleteError
    }
  }

  async function handleProcedureOrderCatalogCreate(input: ProcedureOrderCatalogMutationInput) {
    setProcedureOrderCatalogStatus('loading')
    setProcedureOrderCatalogError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await createProcedureOrderCatalogItem(input, sessionId)
      setProcedureOrderCatalog(response.catalog)
      setProcedureOrderCatalogStatus('ready')
      return response
    } catch (createError) {
      setProcedureOrderCatalogStatus('error')
      const message = createError instanceof Error ? createError.message : 'Procedure order catalog create failed'
      setProcedureOrderCatalogError(message)
      throw createError
    }
  }

  async function handleProcedureOrderCatalogUpdate(
    item: ProcedureOrderCatalogItem,
    input: ProcedureOrderCatalogMutationInput,
  ) {
    setProcedureOrderCatalogStatus('loading')
    setProcedureOrderCatalogError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await updateProcedureOrderCatalogItem(item.id, input, sessionId)
      setProcedureOrderCatalog(response.catalog)
      setProcedureOrderCatalogStatus('ready')
      return response
    } catch (updateError) {
      setProcedureOrderCatalogStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Procedure order catalog update failed'
      setProcedureOrderCatalogError(message)
      throw updateError
    }
  }

  async function handleProcedureOrderCatalogDelete(item: ProcedureOrderCatalogItem) {
    setProcedureOrderCatalogStatus('loading')
    setProcedureOrderCatalogError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      await deleteProcedureOrderCatalogItem(item.id, sessionId)
      const refreshed = await getProcedureOrderCatalog(sessionId)
      setProcedureOrderCatalog(refreshed)
      setProcedureOrderCatalogStatus('ready')
    } catch (deleteError) {
      setProcedureOrderCatalogStatus('error')
      const message = deleteError instanceof Error ? deleteError.message : 'Procedure order catalog delete failed'
      setProcedureOrderCatalogError(message)
      throw deleteError
    }
  }

  async function handleProcedureOrderCatalogImport(input: ProcedureOrderCatalogImportInput) {
    setProcedureOrderCatalogStatus('loading')
    setProcedureOrderCatalogError(null)

    try {
      const sessionId = getActiveProcedureSessionId()
      const response = await importProcedureOrderCatalogCompendium(input, sessionId)
      setProcedureOrderCatalog(response.catalog)
      setProcedureOrderCatalogStatus('ready')
      return response
    } catch (importError) {
      setProcedureOrderCatalogStatus('error')
      const message = importError instanceof Error ? importError.message : 'Procedure order catalog compendium import failed'
      setProcedureOrderCatalogError(message)
      throw importError
    }
  }

  function getActiveAdministrationSessionId() {
    if (!openEmrSessionId) {
      throw new Error('Sign in to manage administration data.')
    }

    return openEmrSessionId
  }

  async function handleAdministrationUserCreate(input: AdministrationUserMutationInput) {
    setAdministrationStatus('loading')
    setAdministrationError(null)

    try {
      const sessionId = getActiveAdministrationSessionId()
      const response = await createAdministrationUser(input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await updateAdministrationUser(user.id, input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      await deleteAdministrationUser(user.id, sessionId)
      const refreshed = await getAdministrationDirectory(sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await createAdministrationFacility(input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await updateAdministrationFacility(facility.id, input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      await deleteAdministrationFacility(facility.id, sessionId)
      const refreshed = await getAdministrationDirectory(sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await grantAdministrationAccessPermission(input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await revokeAdministrationAccessPermission(input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await grantAdministrationAccessUserMembership(input, sessionId)
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
      const sessionId = getActiveAdministrationSessionId()
      const response = await revokeAdministrationAccessUserMembership(input, sessionId)
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
      const sessionId = getActiveMessageSessionId()
      const response = await createPatientMessage(input, sessionId)
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
      const sessionId = getActiveMessageSessionId()
      const response = await updatePatientMessageStatus(message.id, {
        status: 'Done',
        body: message.body?.startsWith('Closed from')
          ? message.body
          : 'Closed from the modernized Messages workspace.',
      }, sessionId)
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

  async function handlePatientMessageContent(message: PatientMessageItem, update: PatientMessageContentUpdateInput) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const sessionId = getActiveMessageSessionId()
      const response = await updatePatientMessageContent(message.id, update, sessionId)
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (contentError) {
      setMessageStatus('error')
      const messageText = contentError instanceof Error ? contentError.message : 'Patient message content update failed'
      setMessageError(messageText)
      throw contentError
    }
  }

  async function handlePatientMessageAssignment(message: PatientMessageItem, update: PatientMessageAssignmentUpdateInput) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const sessionId = getActiveMessageSessionId()
      const response = await updatePatientMessageAssignment(message.id, update, sessionId)
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (assignmentError) {
      setMessageStatus('error')
      const messageText = assignmentError instanceof Error ? assignmentError.message : 'Patient message assignment update failed'
      setMessageError(messageText)
      throw assignmentError
    }
  }

  async function handlePatientMessageReply(message: PatientMessageItem, reply: PatientMessageReplyInput) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const sessionId = getActiveMessageSessionId()
      const response = await replyToPatientMessage(message.id, reply, sessionId)
      setPatientMessages(response.detail)
      setMessageStatus('ready')
      setMessageRefreshKey((current) => current + 1)
      return response
    } catch (replyError) {
      setMessageStatus('error')
      const messageText = replyError instanceof Error ? replyError.message : 'Patient message reply failed'
      setMessageError(messageText)
      throw replyError
    }
  }

  async function handlePatientMessageArchive(message: PatientMessageItem) {
    setMessageStatus('loading')
    setMessageError(null)

    try {
      const sessionId = getActiveMessageSessionId()
      const response = await softDeletePatientMessage(message.id, sessionId)
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
      const sessionId = getActiveMessageSessionId()
      await deletePatientMessage(message.id, sessionId)
      const refreshed = await getPatientMessages(patientMessages?.patientId ?? messagePatientId, sessionId)
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
      const sessionId = getActiveDocumentSessionId()
      const response = await createPatientDocument(input, sessionId)
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

  async function handlePatientBinaryDocumentCreate(input: PatientDocumentBinaryCreateInput) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await createPatientBinaryDocument(input, sessionId)
      setDocumentPatientId(response.detail.patientId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setDocumentStatus('error')
      const message = createError instanceof Error ? createError.message : 'Binary patient document create failed'
      setDocumentError(message)
      throw createError
    }
  }

  async function handlePatientExternalLinkDocumentCreate(input: PatientDocumentExternalLinkCreateInput) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await createPatientExternalLinkDocument(input, sessionId)
      setDocumentPatientId(response.detail.patientId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (createError) {
      setDocumentStatus('error')
      const message = createError instanceof Error ? createError.message : 'External-link patient document create failed'
      setDocumentError(message)
      throw createError
    }
  }

  async function handlePatientDocumentMetadataUpdate(
    document: PatientDocumentItem,
    input: PatientDocumentMetadataUpdateInput,
  ) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await updatePatientDocumentMetadata(document.id, input, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (updateError) {
      setDocumentStatus('error')
      const message = updateError instanceof Error ? updateError.message : 'Patient document metadata update failed'
      setDocumentError(message)
      throw updateError
    }
  }

  async function handlePatientDocumentContentReplace(
    document: PatientDocumentItem,
    input: PatientDocumentContentReplaceInput,
  ) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await replacePatientDocumentContent(document.id, input, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (replaceError) {
      setDocumentStatus('error')
      const message =
        replaceError instanceof Error ? replaceError.message : 'Patient document content replacement failed'
      setDocumentError(message)
      throw replaceError
    }
  }

  async function handlePatientDocumentBinaryContentReplace(
    document: PatientDocumentItem,
    input: PatientDocumentBinaryContentReplaceInput,
  ) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await replacePatientDocumentBinaryContent(document.id, input, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (replaceError) {
      setDocumentStatus('error')
      const message =
        replaceError instanceof Error ? replaceError.message : 'Binary patient document content replacement failed'
      setDocumentError(message)
      throw replaceError
    }
  }

  async function handlePatientDocumentArchive(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await softDeletePatientDocument(document.id, sessionId)
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

  async function handlePatientDocumentRestore(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await restorePatientDocument(document.id, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (restoreError) {
      setDocumentStatus('error')
      const message = restoreError instanceof Error ? restoreError.message : 'Patient document restore failed'
      setDocumentError(message)
      throw restoreError
    }
  }

  async function handlePatientDocumentSign(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await signPatientDocument(document.id, {
        reviewStatus: 'approved',
        reviewedBy: 'admin',
      }, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (signError) {
      setDocumentStatus('error')
      const message = signError instanceof Error ? signError.message : 'Patient document sign-off failed'
      setDocumentError(message)
      throw signError
    }
  }

  async function handlePatientDocumentDeny(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      const response = await signPatientDocument(document.id, {
        reviewStatus: 'denied',
        reviewedBy: 'admin',
      }, sessionId)
      setPatientDocuments(response.detail)
      setDocumentStatus('ready')
      setDocumentRefreshKey((current) => current + 1)
      return response
    } catch (denyError) {
      setDocumentStatus('error')
      const message = denyError instanceof Error ? denyError.message : 'Patient document denial failed'
      setDocumentError(message)
      throw denyError
    }
  }

  async function handlePatientDocumentDelete(document: PatientDocumentItem) {
    setDocumentStatus('loading')
    setDocumentError(null)

    try {
      const sessionId = getActiveDocumentSessionId()
      await deletePatientDocument(document.id, sessionId)
      const refreshed = await getPatientDocuments(
        patientDocuments?.patientId ?? documentPatientId,
        documentIncludeArchived,
        sessionId,
      )
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
            sessionId={openEmrSessionId}
            onPatientSessionActive={setLegacyEhrSessionId}
            onQueryChange={setQuery}
            onSelectPatient={setSelectedPatientId}
            onCreatePatient={handlePatientCreate}
            onSaveContact={handlePatientContactSave}
            onSaveDemographics={handlePatientDemographicsSave}
            onSaveDeceasedStatus={handlePatientDeceasedStatusSave}
            onSaveGuardianContact={handlePatientGuardianContactSave}
            onCreateInsurance={handlePatientInsuranceCreate}
            onUpdateInsurance={handlePatientInsuranceUpdate}
            onDeleteInsurance={handlePatientInsuranceDelete}
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
            sessionId={openEmrSessionId}
            onCalendarSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setAppointmentRefreshKey((current) => current + 1)
            }}
            onPatientIdChange={setAppointmentPatientId}
            onFromDateChange={setAppointmentFromDate}
            onSelectAppointment={setSelectedAppointmentId}
            onCreateAppointment={handleAppointmentCreate}
            onUpdateAppointment={handleAppointmentUpdate}
            onRescheduleAppointmentOccurrence={handleAppointmentOccurrenceReschedule}
            onArriveAppointment={handleAppointmentArrive}
            onCheckOutAppointment={handleAppointmentCheckOut}
            onNoShowAppointment={handleAppointmentNoShow}
            onCancelAppointment={handleAppointmentCancel}
            onDeleteAppointment={handleAppointmentDelete}
            onRestoreAppointmentOccurrence={handleAppointmentOccurrenceRestore}
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
            includeArchivedDocuments={encounterIncludeArchivedDocuments}
            sessionId={openEmrSessionId}
            onEncounterSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setEncounterRefreshKey((current) => current + 1)
            }}
            onPatientIdChange={setEncounterPatientId}
            onFromDateChange={setEncounterFromDate}
            onSelectEncounter={setSelectedEncounter}
            onIncludeArchivedDocumentsChange={setEncounterIncludeArchivedDocuments}
            onCreateEncounter={handleEncounterCreate}
            onUpdateEncounter={handleEncounterUpdate}
            onDeleteEncounter={handleEncounterDelete}
            onCreateVitals={handleEncounterVitalsCreate}
            onCreateSoapNote={handleEncounterSoapCreate}
            onSignEncounter={handleEncounterSign}
            onDeleteEncounterSignature={handleEncounterSignatureDelete}
            onCreateEncounterDocument={handleEncounterDocumentCreate}
            onCreateEncounterBinaryDocument={handleEncounterBinaryDocumentCreate}
            onCreateEncounterExternalLinkDocument={handleEncounterExternalLinkDocumentCreate}
            onUpdateEncounterDocumentMetadata={handleEncounterDocumentMetadataUpdate}
            onMoveEncounterDocument={handleEncounterDocumentMove}
            onReplaceEncounterDocumentContent={handleEncounterDocumentContentReplace}
            onReplaceEncounterDocumentBinaryContent={handleEncounterDocumentBinaryContentReplace}
            onArchiveEncounterDocument={handleEncounterDocumentArchive}
            onRestoreEncounterDocument={handleEncounterDocumentRestore}
            onSignEncounterDocument={handleEncounterDocumentSign}
            onDenyEncounterDocument={handleEncounterDocumentDeny}
            onCreateFeeSheetLine={handleEncounterFeeSheetLineCreate}
            onCreateProcedureOrder={handleEncounterProcedureOrderCreate}
            onCreateProcedureResultSet={handleEncounterProcedureResultSetCreate}
            onUpdateProcedureResult={handleEncounterProcedureResultUpdate}
          />
        )}
        {activeModule === 'lists' && (
          <ClinicalListsWorkspace
            patientId={clinicalPatientId}
            clinicalLists={clinicalLists}
            status={clinicalStatus}
            error={clinicalError}
            sessionId={openEmrSessionId}
            onClinicalListsSessionActive={setLegacyEhrSessionId}
            onPatientIdChange={setClinicalPatientId}
            onCreateAllergy={handleClinicalAllergyCreate}
            onDeactivateAllergy={handleClinicalAllergyDeactivate}
            onDeleteAllergy={handleClinicalAllergyDelete}
            onCreateProblem={handleClinicalProblemCreate}
            onDeactivateProblem={handleClinicalProblemDeactivate}
            onDeleteProblem={handleClinicalProblemDelete}
            onCreateMedication={handleClinicalMedicationCreate}
            onDeactivateMedication={handleClinicalMedicationDeactivate}
            onDeleteMedication={handleClinicalMedicationDelete}
            onCreatePrescription={handleClinicalPrescriptionCreate}
            onDeactivatePrescription={handleClinicalPrescriptionDeactivate}
            onDeletePrescription={handleClinicalPrescriptionDelete}
            onCreateImmunization={handleClinicalImmunizationCreate}
            onMarkImmunizationEnteredInError={handleClinicalImmunizationEnteredInError}
            onDeleteImmunization={handleClinicalImmunizationDelete}
          />
        )}
        {activeModule === 'fees' && (
          <FeesWorkspace
            patientId={billingPatientId}
            patientBilling={patientBilling}
            status={billingStatus}
            error={billingError}
            sessionId={openEmrSessionId}
            onBillingSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
            }}
            onPatientIdChange={setBillingPatientId}
            onCreateLine={handleBillingLineCreate}
            onUpdateLine={handleBillingLineUpdate}
            onDeactivateLine={handleBillingLineDeactivate}
            onDeleteLine={handleBillingLineDelete}
            onCreateClaim={handleBillingClaimCreate}
            onUpdateClaimStatus={handleBillingClaimStatusUpdate}
            onDeleteClaim={handleBillingClaimDelete}
            onCreatePayment={handleBillingPaymentCreate}
            onVoidPayment={handleBillingPaymentVoid}
            onDeletePayment={handleBillingPaymentDelete}
          />
        )}
        {activeModule === 'procedures' && (
          <ProceduresWorkspace
            patientId={procedurePatientId}
            procedureResults={procedureResults}
            status={procedureStatus}
            error={procedureError}
            orderCatalog={procedureOrderCatalog}
            orderCatalogStatus={procedureOrderCatalogStatus}
            orderCatalogError={procedureOrderCatalogError}
            sessionId={openEmrSessionId}
            onProceduresSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setProcedureRefreshKey((current) => current + 1)
            }}
            onPatientIdChange={setProcedurePatientId}
            onCreateOrder={handleProcedureOrderCreate}
            onCompleteOrder={handleProcedureOrderComplete}
            onUpdateOrder={handleProcedureOrderUpdate}
            onCreateReport={handleProcedureReportCreate}
            onUpdateReport={handleProcedureReportUpdate}
            onSignReport={handleProcedureReportSign}
            onReopenReportReview={handleProcedureReportReviewReopen}
            onCreateSpecimen={handleProcedureSpecimenCreate}
            onCreateResult={handleProcedureResultCreate}
            onUpdateResult={handleProcedureResultUpdate}
            onDeleteOrder={handleProcedureOrderDelete}
          />
        )}
        {activeModule === 'messages' && (
          <MessagesWorkspace
            patientId={messagePatientId}
            patientMessages={patientMessages}
            status={messageStatus}
            error={messageError}
            sessionId={openEmrSessionId}
            onPatientIdChange={setMessagePatientId}
            onMessagesSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setMessageRefreshKey((current) => current + 1)
            }}
            onCreateMessage={handlePatientMessageCreate}
            onCloseMessage={handlePatientMessageClose}
            onUpdateMessageContent={handlePatientMessageContent}
            onAssignMessage={handlePatientMessageAssignment}
            onReplyMessage={handlePatientMessageReply}
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
            includeArchived={documentIncludeArchived}
            sessionId={openEmrSessionId}
            onPatientIdChange={setDocumentPatientId}
            onIncludeArchivedChange={setDocumentIncludeArchived}
            onDocumentsSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setDocumentRefreshKey((current) => current + 1)
            }}
            onCreateDocument={handlePatientDocumentCreate}
            onCreateBinaryDocument={handlePatientBinaryDocumentCreate}
            onCreateExternalLinkDocument={handlePatientExternalLinkDocumentCreate}
            onUpdateDocumentMetadata={handlePatientDocumentMetadataUpdate}
            onReplaceDocumentContent={handlePatientDocumentContentReplace}
            onReplaceDocumentBinaryContent={handlePatientDocumentBinaryContentReplace}
            onArchiveDocument={handlePatientDocumentArchive}
            onRestoreDocument={handlePatientDocumentRestore}
            onSignDocument={handlePatientDocumentSign}
            onDenyDocument={handlePatientDocumentDeny}
            onDeleteDocument={handlePatientDocumentDelete}
          />
        )}
        {activeModule === 'reports' && (
          <ReportsWorkspace
            reports={operationalReports}
            status={reportsStatus}
            error={reportsError}
            sessionId={openEmrSessionId}
            onReportsSessionActive={setLegacyEhrSessionId}
            labProviders={procedureLabProviders}
            labProvidersStatus={procedureLabProvidersStatus}
            labProvidersError={procedureLabProvidersError}
            labProvidersIncludeInactive={procedureLabProvidersIncludeInactive}
            orderCatalog={procedureOrderCatalog}
            orderCatalogStatus={procedureOrderCatalogStatus}
            orderCatalogError={procedureOrderCatalogError}
            onLabProvidersIncludeInactiveChange={setProcedureLabProvidersIncludeInactive}
            onCreateLabProvider={handleProcedureLabProviderCreate}
            onUpdateLabProvider={handleProcedureLabProviderUpdate}
            onDeleteLabProvider={handleProcedureLabProviderDelete}
            onCreateOrderCatalogItem={handleProcedureOrderCatalogCreate}
            onUpdateOrderCatalogItem={handleProcedureOrderCatalogUpdate}
            onDeleteOrderCatalogItem={handleProcedureOrderCatalogDelete}
            onImportOrderCatalogCompendium={handleProcedureOrderCatalogImport}
            orderQueue={procedureOrderQueue}
            orderQueueStatus={procedureOrderQueueStatus}
            orderQueueError={procedureOrderQueueError}
            orderQueueFilter={procedureOrderQueueFilter}
            orderQueuePatientFilter={procedureOrderQueuePatientFilter}
            orderQueueProviderFilter={procedureOrderQueueProviderFilter}
            orderQueueLabFilter={procedureOrderQueueLabFilter}
            orderQueueFromDate={procedureOrderQueueFromDate}
            orderQueueToDate={procedureOrderQueueToDate}
            onOrderQueueFilterChange={setProcedureOrderQueueFilter}
            onOrderQueuePatientFilterChange={setProcedureOrderQueuePatientFilter}
            onOrderQueueProviderFilterChange={setProcedureOrderQueueProviderFilter}
            onOrderQueueLabFilterChange={setProcedureOrderQueueLabFilter}
            onOrderQueueFromDateChange={setProcedureOrderQueueFromDate}
            onOrderQueueToDateChange={setProcedureOrderQueueToDate}
            onOrderQueueTransmit={handleProcedureOrderTransmit}
            reviewQueue={procedureReportReviewQueue}
            reviewQueueStatus={procedureReportReviewQueueStatus}
            reviewQueueError={procedureReportReviewQueueError}
            reviewQueueFilter={procedureReportReviewQueueFilter}
            reviewQueuePatientFilter={procedureReportReviewQueuePatientFilter}
            reviewQueueProviderFilter={procedureReportReviewQueueProviderFilter}
            reviewQueueLabFilter={procedureReportReviewQueueLabFilter}
            reviewQueueFromDate={procedureReportReviewQueueFromDate}
            reviewQueueToDate={procedureReportReviewQueueToDate}
            onReviewQueueFilterChange={setProcedureReportReviewQueueFilter}
            onReviewQueuePatientFilterChange={setProcedureReportReviewQueuePatientFilter}
            onReviewQueueProviderFilterChange={setProcedureReportReviewQueueProviderFilter}
            onReviewQueueLabFilterChange={setProcedureReportReviewQueueLabFilter}
            onReviewQueueFromDateChange={setProcedureReportReviewQueueFromDate}
            onReviewQueueToDateChange={setProcedureReportReviewQueueToDate}
            onReviewQueueBulkSign={handleProcedureReportBulkSign}
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
            onAdminSessionActive={(sessionId) => {
              setLegacyEhrSessionId(sessionId)
              setAdministrationRefreshKey((current) => current + 1)
            }}
            onAdminSessionEnded={() => {
              setLegacyEhrSessionId(null)
              setAdministrationDirectory(null)
              setAdministrationStatus('idle')
              setAdministrationError(null)
            }}
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
  sessionId,
  onPatientSessionActive,
  onQueryChange,
  onSelectPatient,
  onCreatePatient,
  onSaveContact,
  onSaveDemographics,
  onSaveDeceasedStatus,
  onSaveGuardianContact,
  onCreateInsurance,
  onUpdateInsurance,
  onDeleteInsurance,
}: {
  query: string
  searchResult: PatientSearchResponse | null
  selectedPatientId: string | null
  activePatient: PatientListItem | PatientChartSummary | null
  chart: PatientChartSummary | null
  searchStatus: 'idle' | 'loading' | 'ready' | 'error'
  chartStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onPatientSessionActive: (sessionId: string) => void
  onQueryChange: (value: string) => void
  onSelectPatient: (canonicalId: string) => void
  onCreatePatient: (patient: PatientRegistrationInput) => Promise<PatientChartSummary>
  onSaveContact: (canonicalId: string, contact: PatientContactUpdate) => Promise<void>
  onSaveDemographics: (canonicalId: string, demographics: PatientDemographicsUpdate) => Promise<void>
  onSaveDeceasedStatus: (canonicalId: string, status: PatientDeceasedStatusUpdate) => Promise<void>
  onSaveGuardianContact: (canonicalId: string, guardianContact: PatientGuardianContactUpdate) => Promise<void>
  onCreateInsurance: (canonicalId: string, insurance: PatientInsuranceMutationInput) => Promise<PatientChartSummary>
  onUpdateInsurance: (insuranceId: string, insurance: PatientInsuranceMutationInput) => Promise<PatientChartSummary>
  onDeleteInsurance: (insuranceId: string) => Promise<PatientChartSummary>
}) {
  const [isEditingDemographics, setIsEditingDemographics] = useState(false)
  const [demographicsDraft, setDemographicsDraft] = useState<PatientDemographicsUpdate>(() =>
    buildDemographicsDraft(null),
  )
  const [demographicsSaveStatus, setDemographicsSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [isEditingDeceasedStatus, setIsEditingDeceasedStatus] = useState(false)
  const [deceasedStatusDraft, setDeceasedStatusDraft] = useState<PatientDeceasedStatusUpdate>(() =>
    buildDeceasedStatusDraft(null),
  )
  const [deceasedStatusSaveStatus, setDeceasedStatusSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>(
    'idle',
  )
  const [isEditingGuardianContact, setIsEditingGuardianContact] = useState(false)
  const [guardianContactDraft, setGuardianContactDraft] = useState<PatientGuardianContactUpdate>(() =>
    buildGuardianContactDraft(null),
  )
  const [guardianContactSaveStatus, setGuardianContactSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>(
    'idle',
  )
  const [isEditingContact, setIsEditingContact] = useState(false)
  const [contactDraft, setContactDraft] = useState<PatientContactUpdate>(() => buildContactDraft(null))
  const [contactSaveStatus, setContactSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [editingInsuranceId, setEditingInsuranceId] = useState<string | null>(null)
  const [insuranceDraft, setInsuranceDraft] = useState<PatientInsuranceMutationInput>(() => buildInsuranceDraft())
  const [insuranceSaveStatus, setInsuranceSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [isRegistering, setIsRegistering] = useState(false)
  const [registrationDraft, setRegistrationDraft] = useState<PatientRegistrationInput>(() => buildRegistrationDraft())
  const [registrationSaveStatus, setRegistrationSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [registrationValidationMessages, setRegistrationValidationMessages] = useState<string[]>([])
  const [duplicateSearch, setDuplicateSearch] = useState<PatientDuplicateSearchResponse | null>(null)
  const [duplicateSearchStatus, setDuplicateSearchStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [duplicateSearchError, setDuplicateSearchError] = useState<string | null>(null)
  const [patientLoginUsername, setPatientLoginUsername] = useState('admin')
  const [patientLoginPassword, setPatientLoginPassword] = useState('pass')
  const [patientLoginStatus, setPatientLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [patientLoginMessage, setPatientLoginMessage] = useState<string | null>(null)

  useEffect(() => {
    setDemographicsDraft(buildDemographicsDraft(chart ?? activePatient))
    setIsEditingDemographics(false)
    setDemographicsSaveStatus('idle')
    setDeceasedStatusDraft(buildDeceasedStatusDraft(chart))
    setIsEditingDeceasedStatus(false)
    setDeceasedStatusSaveStatus('idle')
    setGuardianContactDraft(buildGuardianContactDraft(chart))
    setIsEditingGuardianContact(false)
    setGuardianContactSaveStatus('idle')
    setContactDraft(buildContactDraft(chart ?? activePatient))
    setIsEditingContact(false)
    setContactSaveStatus('idle')
    setEditingInsuranceId(null)
    setInsuranceDraft(buildInsuranceDraft())
    setInsuranceSaveStatus('idle')
  }, [activePatient, chart])

  function updateDraft(field: keyof PatientContactUpdate, value: string) {
    setContactDraft((current) => ({ ...current, [field]: value }))
  }

  function updateDemographicsDraft(field: keyof PatientDemographicsUpdate, value: string) {
    setDemographicsDraft((current) => ({ ...current, [field]: value }))
  }

  function updateDeceasedStatusDraft(field: keyof PatientDeceasedStatusUpdate, value: string) {
    setDeceasedStatusDraft((current) => ({ ...current, [field]: value }))
  }

  function updateGuardianContactDraft(field: keyof PatientGuardianContactUpdate, value: string) {
    setGuardianContactDraft((current) => ({ ...current, [field]: value }))
  }

  function updateInsuranceDraft(field: keyof PatientInsuranceMutationInput, value: string) {
    setInsuranceDraft((current) => ({ ...current, [field]: value }))
  }

  function updateRegistrationDraft(field: keyof PatientRegistrationInput, value: string) {
    setRegistrationDraft((current) => ({ ...current, [field]: value }))
    setRegistrationValidationMessages([])
    setDuplicateSearchStatus('idle')
    setDuplicateSearchError(null)
  }

  async function handlePatientLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPatientLoginStatus('checking')
    setPatientLoginMessage(null)

    try {
      const result = await login({ username: patientLoginUsername, password: patientLoginPassword })
      if (result.authenticated && result.sessionId) {
        onPatientSessionActive(result.sessionId)
        setPatientLoginStatus('authenticated')
        setPatientLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setPatientLoginStatus('rejected')
        setPatientLoginMessage(result.failureReason ?? 'Patient access was rejected.')
      }
    } catch (loginError) {
      setPatientLoginStatus('error')
      setPatientLoginMessage(loginError instanceof Error ? loginError.message : 'Patient access check failed')
    }
  }

  async function handleRegistrationSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    setRegistrationSaveStatus('saving')
    try {
      await onCreatePatient(registrationDraft)
      setRegistrationDraft(buildRegistrationDraft())
      setIsRegistering(false)
      setRegistrationSaveStatus('saved')
      setRegistrationValidationMessages([])
    } catch (registrationError) {
      if (registrationError instanceof PatientRegistrationValidationError) {
        setRegistrationValidationMessages(registrationError.messages)
      }
      setRegistrationSaveStatus('error')
    }
  }

  async function handleDuplicateCheck() {
    if (!sessionId) {
      return
    }

    setDuplicateSearchStatus('loading')
    setDuplicateSearchError(null)
    try {
      const result = await findPatientDuplicates(
        {
          firstName: registrationDraft.firstName,
          lastName: registrationDraft.lastName,
          dateOfBirth: registrationDraft.dateOfBirth,
          phone: registrationDraft.phoneHome || registrationDraft.phoneCell,
          email: registrationDraft.email,
          excludePatientId: registrationDraft.pubpid,
          limit: 5,
        },
        sessionId,
      )
      setDuplicateSearch(result)
      setDuplicateSearchStatus('ready')
    } catch (duplicateError) {
      setDuplicateSearch(null)
      setDuplicateSearchStatus('error')
      setDuplicateSearchError(duplicateError instanceof Error ? duplicateError.message : 'Duplicate check failed')
    }
  }

  async function handleDemographicsSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!chart) {
      return
    }

    setDemographicsSaveStatus('saving')
    try {
      await onSaveDemographics(chart.canonicalId, demographicsDraft)
      setIsEditingDemographics(false)
      setDemographicsSaveStatus('saved')
    } catch {
      setDemographicsSaveStatus('error')
    }
  }

  async function handleDeceasedStatusSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!chart) {
      return
    }

    setDeceasedStatusSaveStatus('saving')
    try {
      await onSaveDeceasedStatus(chart.canonicalId, deceasedStatusDraft)
      setIsEditingDeceasedStatus(false)
      setDeceasedStatusSaveStatus('saved')
    } catch {
      setDeceasedStatusSaveStatus('error')
    }
  }

  async function handleGuardianContactSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!chart) {
      return
    }

    setGuardianContactSaveStatus('saving')
    try {
      await onSaveGuardianContact(chart.canonicalId, guardianContactDraft)
      setIsEditingGuardianContact(false)
      setGuardianContactSaveStatus('saved')
    } catch {
      setGuardianContactSaveStatus('error')
    }
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

  async function handleInsuranceSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!chart) {
      return
    }

    setInsuranceSaveStatus('saving')
    try {
      if (editingInsuranceId) {
        await onUpdateInsurance(editingInsuranceId, insuranceDraft)
      } else {
        await onCreateInsurance(chart.canonicalId, insuranceDraft)
      }
      setEditingInsuranceId(null)
      setInsuranceDraft(buildInsuranceDraft())
      setInsuranceSaveStatus('saved')
    } catch {
      setInsuranceSaveStatus('error')
    }
  }

  function handleInsuranceEdit(item: PatientInsuranceItem) {
    setEditingInsuranceId(item.id)
    setInsuranceDraft(buildInsuranceDraft(item))
    setInsuranceSaveStatus('idle')
  }

  async function handleInsuranceDelete(item: PatientInsuranceItem) {
    setInsuranceSaveStatus('saving')
    try {
      await onDeleteInsurance(item.id)
      if (editingInsuranceId === item.id) {
        setEditingInsuranceId(null)
        setInsuranceDraft(buildInsuranceDraft())
      }
      setInsuranceSaveStatus('saved')
    } catch {
      setInsuranceSaveStatus('error')
    }
  }

  return (
    <section className="split-layout">
      <section className="finder-panel" aria-label="Patient search">
        {!sessionId && (
          <form className="mutation-form" aria-label="Patient access" onSubmit={handlePatientLogin}>
            <div className="panel-heading">
              <ShieldCheck size={17} />
              <h3>Patient Access</h3>
            </div>
            <label>
              Username
              <input value={patientLoginUsername} onChange={(event) => setPatientLoginUsername(event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={patientLoginPassword}
                onChange={(event) => setPatientLoginPassword(event.target.value)}
              />
            </label>
            <button type="submit" disabled={patientLoginStatus === 'checking'}>
              <LogIn size={15} />
              {patientLoginStatus === 'checking' ? 'Checking' : 'Verify Patient Access'}
            </button>
            {patientLoginMessage && (
              <div className={patientLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {patientLoginMessage}
              </div>
            )}
          </form>
        )}
        {sessionId && patientLoginMessage && patientLoginStatus === 'authenticated' && (
          <div className="status-banner">{patientLoginMessage}</div>
        )}

        <div className="search-box">
          <Search size={18} />
          <input
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
            aria-label="Search patients"
            placeholder="Name, ID, phone, or email"
            disabled={!sessionId}
          />
        </div>

        <div className="registration-panel">
          <button
            className="icon-text-button primary"
            type="button"
            disabled={!sessionId}
            onClick={() => {
              setIsRegistering((current) => !current)
              setRegistrationSaveStatus('idle')
              setRegistrationValidationMessages([])
            }}
          >
            <UserPlus size={15} />
            <span>Register patient</span>
          </button>
          {registrationSaveStatus === 'saved' && <span className="save-note">Registered</span>}
          {registrationSaveStatus === 'error' && <span className="save-note error">Registration failed</span>}
        </div>

        {isRegistering && (
          <form className="registration-form" onSubmit={handleRegistrationSubmit} aria-label="Patient registration form">
            <label className="contact-field">
              <span>Public ID</span>
              <input
                value={registrationDraft.pubpid}
                onChange={(event) => updateRegistrationDraft('pubpid', event.target.value)}
                aria-label="New patient public ID"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="contact-field">
                <span>First name</span>
                <input
                  value={registrationDraft.firstName}
                  onChange={(event) => updateRegistrationDraft('firstName', event.target.value)}
                  aria-label="New patient first name"
                  required
                />
              </label>
              <label className="contact-field">
                <span>Last name</span>
                <input
                  value={registrationDraft.lastName}
                  onChange={(event) => updateRegistrationDraft('lastName', event.target.value)}
                  aria-label="New patient last name"
                  required
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="contact-field">
                <span>Date of birth</span>
                <input
                  type="date"
                  value={registrationDraft.dateOfBirth}
                  onChange={(event) => updateRegistrationDraft('dateOfBirth', event.target.value)}
                  aria-label="New patient date of birth"
                  required
                />
              </label>
              <label className="contact-field">
                <span>Sex</span>
                <select
                  value={registrationDraft.sex}
                  onChange={(event) => updateRegistrationDraft('sex', event.target.value)}
                  aria-label="New patient sex"
                >
                  <option value="">Unspecified</option>
                  <option value="Female">Female</option>
                  <option value="Male">Male</option>
                  <option value="Other">Other</option>
                </select>
              </label>
            </div>
            <label className="contact-field">
              <span>Street</span>
              <input
                value={registrationDraft.street}
                onChange={(event) => updateRegistrationDraft('street', event.target.value)}
                aria-label="New patient street"
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="contact-field">
                <span>City</span>
                <input
                  value={registrationDraft.city}
                  onChange={(event) => updateRegistrationDraft('city', event.target.value)}
                  aria-label="New patient city"
                />
              </label>
              <label className="contact-field">
                <span>Postal code</span>
                <input
                  value={registrationDraft.postalCode}
                  onChange={(event) => updateRegistrationDraft('postalCode', event.target.value)}
                  aria-label="New patient postal code"
                />
              </label>
            </div>
            <label className="contact-field">
              <span>Email</span>
              <input
                value={registrationDraft.email}
                onChange={(event) => updateRegistrationDraft('email', event.target.value)}
                aria-label="New patient email"
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="contact-field">
                <span>Home phone</span>
                <input
                  value={registrationDraft.phoneHome}
                  onChange={(event) => updateRegistrationDraft('phoneHome', event.target.value)}
                  aria-label="New patient home phone"
                />
              </label>
              <label className="contact-field">
                <span>Cell phone</span>
                <input
                  value={registrationDraft.phoneCell}
                  onChange={(event) => updateRegistrationDraft('phoneCell', event.target.value)}
                  aria-label="New patient cell phone"
                />
              </label>
            </div>
            <div className="contact-actions">
              <button
                className="icon-text-button"
                type="button"
                disabled={!sessionId || duplicateSearchStatus === 'loading'}
                onClick={handleDuplicateCheck}
              >
                <Search size={15} />
                <span>{duplicateSearchStatus === 'loading' ? 'Checking' : 'Check duplicates'}</span>
              </button>
              <button className="icon-text-button primary" type="submit" disabled={registrationSaveStatus === 'saving'}>
                <Check size={15} />
                <span>{registrationSaveStatus === 'saving' ? 'Registering' : 'Create chart'}</span>
              </button>
              <button
                className="icon-text-button"
                type="button"
                onClick={() => {
                  setRegistrationDraft(buildRegistrationDraft())
                  setIsRegistering(false)
                  setRegistrationSaveStatus('idle')
                  setRegistrationValidationMessages([])
                }}
              >
                <X size={15} />
                <span>Cancel</span>
              </button>
            </div>
            {registrationValidationMessages.length > 0 && (
              <div className="status-banner error" aria-label="Patient registration validation">
                <strong>Registration Validation</strong>
                <ul>
                  {registrationValidationMessages.map((message) => (
                    <li key={message}>{message}</li>
                  ))}
                </ul>
              </div>
            )}
            <div className="duplicate-readiness-panel" aria-label="Patient duplicate readiness">
              <div className="duplicate-readiness-header">
                <strong>Duplicate Readiness</strong>
                <span>{duplicateSearchStatus === 'ready' ? `${duplicateSearch?.totalCandidates ?? 0} candidates` : 'Not checked'}</span>
              </div>
              {duplicateSearchStatus === 'error' && (
                <div className="status-banner error">{duplicateSearchError ?? 'Duplicate check failed'}</div>
              )}
              {duplicateSearchStatus === 'ready' && (
                <PatientDuplicateCandidateList candidates={duplicateSearch?.candidates ?? []} />
              )}
            </div>
          </form>
        )}

        <div className="result-meta">
          <span>
            {!sessionId ? 'Sign in required' : searchStatus === 'loading' ? 'Searching' : `${searchResult?.totalMatches ?? 0} matches`}
          </span>
          <span>Limit {searchResult?.limit ?? 25}</span>
        </div>

        {searchStatus === 'error' && <div className="status-banner error">{error}</div>}

        <div className="patient-list">
          {!sessionId && <div className="empty-state">Sign in to search patient charts</div>}

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
                {isEditingDemographics && chart ? (
                  <form className="contact-form" onSubmit={handleDemographicsSubmit}>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>First name</span>
                        <input
                          value={demographicsDraft.firstName}
                          onChange={(event) => updateDemographicsDraft('firstName', event.target.value)}
                          aria-label="Patient first name"
                          required
                        />
                      </label>
                      <label className="contact-field">
                        <span>Last name</span>
                        <input
                          value={demographicsDraft.lastName}
                          onChange={(event) => updateDemographicsDraft('lastName', event.target.value)}
                          aria-label="Patient last name"
                          required
                        />
                      </label>
                    </div>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>Preferred</span>
                        <input
                          value={demographicsDraft.preferredName}
                          onChange={(event) => updateDemographicsDraft('preferredName', event.target.value)}
                          aria-label="Patient preferred name"
                        />
                      </label>
                      <label className="contact-field">
                        <span>Sex</span>
                        <select
                          value={demographicsDraft.sex}
                          onChange={(event) => updateDemographicsDraft('sex', event.target.value)}
                          aria-label="Patient sex"
                        >
                          <option value="">Unspecified</option>
                          <option value="Female">Female</option>
                          <option value="Male">Male</option>
                          <option value="Other">Other</option>
                        </select>
                      </label>
                    </div>
                    <label className="contact-field">
                      <span>Date of birth</span>
                      <input
                        type="date"
                        value={demographicsDraft.dateOfBirth}
                        onChange={(event) => updateDemographicsDraft('dateOfBirth', event.target.value)}
                        aria-label="Patient date of birth"
                        required
                      />
                    </label>
                    <label className="contact-field">
                      <span>Street</span>
                      <input
                        value={demographicsDraft.street}
                        onChange={(event) => updateDemographicsDraft('street', event.target.value)}
                        aria-label="Patient street"
                      />
                    </label>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>City</span>
                        <input
                          value={demographicsDraft.city}
                          onChange={(event) => updateDemographicsDraft('city', event.target.value)}
                          aria-label="Patient city"
                        />
                      </label>
                      <label className="contact-field">
                        <span>State</span>
                        <input
                          value={demographicsDraft.state}
                          onChange={(event) => updateDemographicsDraft('state', event.target.value)}
                          aria-label="Patient state"
                        />
                      </label>
                    </div>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>Postal code</span>
                        <input
                          value={demographicsDraft.postalCode}
                          onChange={(event) => updateDemographicsDraft('postalCode', event.target.value)}
                          aria-label="Patient postal code"
                        />
                      </label>
                      <label className="contact-field">
                        <span>Marital status</span>
                        <select
                          value={demographicsDraft.maritalStatus}
                          onChange={(event) => updateDemographicsDraft('maritalStatus', event.target.value)}
                          aria-label="Patient marital status"
                        >
                          <option value="">Unspecified</option>
                          <option value="single">Single</option>
                          <option value="married">Married</option>
                          <option value="partnered">Partnered</option>
                          <option value="divorced">Divorced</option>
                          <option value="widowed">Widowed</option>
                        </select>
                      </label>
                    </div>
                    <label className="contact-field">
                      <span>Occupation</span>
                      <input
                        value={demographicsDraft.occupation}
                        onChange={(event) => updateDemographicsDraft('occupation', event.target.value)}
                        aria-label="Patient occupation"
                      />
                    </label>
                    <div className="contact-actions">
                      <button className="icon-text-button primary" type="submit" disabled={demographicsSaveStatus === 'saving'}>
                        <Check size={15} />
                        <span>{demographicsSaveStatus === 'saving' ? 'Saving' : 'Save'}</span>
                      </button>
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setDemographicsDraft(buildDemographicsDraft(chart))
                          setIsEditingDemographics(false)
                          setDemographicsSaveStatus('idle')
                        }}
                      >
                        <X size={15} />
                        <span>Cancel</span>
                      </button>
                    </div>
                  </form>
                ) : (
                  <>
                    <Field label="Date of birth" value={activePatient.dateOfBirth} />
                    <Field label="Marital status" value={chart?.maritalStatus} />
                    <Field label="Occupation" value={chart?.occupation} />
                    <Field label="Address" value={formatAddress(chart)} />
                    <Field label="Registered" value={chart?.registrationDate} />
                    <div className="contact-actions">
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setDemographicsDraft(buildDemographicsDraft(chart ?? activePatient))
                          setIsEditingDemographics(true)
                          setDemographicsSaveStatus('idle')
                        }}
                        disabled={!chart}
                      >
                        <Pencil size={15} />
                        <span>Edit demographics</span>
                      </button>
                      {demographicsSaveStatus === 'saved' && <span className="save-note">Saved</span>}
                      {demographicsSaveStatus === 'error' && <span className="save-note error">Save failed</span>}
                    </div>
                  </>
                )}
              </InfoPanel>

              <InfoPanel title="Deceased Status" icon={HeartPulse}>
                {isEditingDeceasedStatus && chart ? (
                  <form className="contact-form" onSubmit={handleDeceasedStatusSubmit}>
                    <label className="contact-field">
                      <span>Deceased date</span>
                      <input
                        type="date"
                        value={deceasedStatusDraft.deceasedDate}
                        onChange={(event) => updateDeceasedStatusDraft('deceasedDate', event.target.value)}
                        aria-label="Patient deceased date"
                      />
                    </label>
                    <label className="contact-field">
                      <span>Reason deceased</span>
                      <input
                        value={deceasedStatusDraft.deceasedReason}
                        onChange={(event) => updateDeceasedStatusDraft('deceasedReason', event.target.value)}
                        aria-label="Patient deceased reason"
                      />
                    </label>
                    <div className="contact-actions">
                      <button
                        className="icon-text-button primary"
                        type="submit"
                        disabled={deceasedStatusSaveStatus === 'saving'}
                      >
                        <Check size={15} />
                        <span>{deceasedStatusSaveStatus === 'saving' ? 'Saving' : 'Save status'}</span>
                      </button>
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setDeceasedStatusDraft(buildDeceasedStatusDraft(chart))
                          setIsEditingDeceasedStatus(false)
                          setDeceasedStatusSaveStatus('idle')
                        }}
                      >
                        <X size={15} />
                        <span>Cancel</span>
                      </button>
                    </div>
                  </form>
                ) : (
                  <>
                    <Field label="Status" value={chart?.deceasedDate ? 'Deceased' : 'Active patient'} />
                    <Field label="Deceased date" value={chart?.deceasedDate} />
                    <Field label="Reason deceased" value={chart?.deceasedReason} />
                    <div className="contact-actions">
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setDeceasedStatusDraft(buildDeceasedStatusDraft(chart))
                          setIsEditingDeceasedStatus(true)
                          setDeceasedStatusSaveStatus('idle')
                        }}
                        disabled={!chart}
                      >
                        <Pencil size={15} />
                        <span>Edit status</span>
                      </button>
                      {deceasedStatusSaveStatus === 'saved' && <span className="save-note">Saved</span>}
                      {deceasedStatusSaveStatus === 'error' && <span className="save-note error">Save failed</span>}
                    </div>
                  </>
                )}
              </InfoPanel>

              <InfoPanel title="Guardian Contact" icon={UserPlus}>
                {isEditingGuardianContact && chart ? (
                  <form className="contact-form" onSubmit={handleGuardianContactSubmit}>
                    <label className="contact-field">
                      <span>Mother name</span>
                      <input
                        value={guardianContactDraft.motherName}
                        onChange={(event) => updateGuardianContactDraft('motherName', event.target.value)}
                        aria-label="Patient mother name"
                      />
                    </label>
                    <label className="contact-field">
                      <span>Guardian name</span>
                      <input
                        value={guardianContactDraft.guardianName}
                        onChange={(event) => updateGuardianContactDraft('guardianName', event.target.value)}
                        aria-label="Patient guardian name"
                      />
                    </label>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>Relationship</span>
                        <select
                          value={guardianContactDraft.guardianRelationship}
                          onChange={(event) => updateGuardianContactDraft('guardianRelationship', event.target.value)}
                          aria-label="Patient guardian relationship"
                        >
                          <option value="">Unspecified</option>
                          <option value="guardian">Guardian</option>
                          <option value="parent">Parent</option>
                          <option value="spouse">Spouse</option>
                          <option value="mother">Mother</option>
                          <option value="father">Father</option>
                          <option value="sibling">Sibling</option>
                          <option value="care_giver">Care giver</option>
                        </select>
                      </label>
                      <label className="contact-field">
                        <span>Sex</span>
                        <select
                          value={guardianContactDraft.guardianSex}
                          onChange={(event) => updateGuardianContactDraft('guardianSex', event.target.value)}
                          aria-label="Patient guardian sex"
                        >
                          <option value="">Unspecified</option>
                          <option value="Female">Female</option>
                          <option value="Male">Male</option>
                          <option value="UNK">Unknown</option>
                        </select>
                      </label>
                    </div>
                    <label className="contact-field">
                      <span>Address</span>
                      <input
                        value={guardianContactDraft.guardianAddress}
                        onChange={(event) => updateGuardianContactDraft('guardianAddress', event.target.value)}
                        aria-label="Patient guardian address"
                      />
                    </label>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>City</span>
                        <input
                          value={guardianContactDraft.guardianCity}
                          onChange={(event) => updateGuardianContactDraft('guardianCity', event.target.value)}
                          aria-label="Patient guardian city"
                        />
                      </label>
                      <label className="contact-field">
                        <span>State</span>
                        <input
                          value={guardianContactDraft.guardianState}
                          onChange={(event) => updateGuardianContactDraft('guardianState', event.target.value)}
                          aria-label="Patient guardian state"
                        />
                      </label>
                    </div>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>Postal code</span>
                        <input
                          value={guardianContactDraft.guardianPostalCode}
                          onChange={(event) => updateGuardianContactDraft('guardianPostalCode', event.target.value)}
                          aria-label="Patient guardian postal code"
                        />
                      </label>
                      <label className="contact-field">
                        <span>Country</span>
                        <select
                          value={guardianContactDraft.guardianCountry}
                          onChange={(event) => updateGuardianContactDraft('guardianCountry', event.target.value)}
                          aria-label="Patient guardian country"
                        >
                          <option value="">Unspecified</option>
                          <option value="USA">USA</option>
                        </select>
                      </label>
                    </div>
                    <div className="mutation-grid two-column">
                      <label className="contact-field">
                        <span>Phone</span>
                        <input
                          value={guardianContactDraft.guardianPhone}
                          onChange={(event) => updateGuardianContactDraft('guardianPhone', event.target.value)}
                          aria-label="Patient guardian phone"
                        />
                      </label>
                      <label className="contact-field">
                        <span>Work phone</span>
                        <input
                          value={guardianContactDraft.guardianWorkPhone}
                          onChange={(event) => updateGuardianContactDraft('guardianWorkPhone', event.target.value)}
                          aria-label="Patient guardian work phone"
                        />
                      </label>
                    </div>
                    <label className="contact-field">
                      <span>Email</span>
                      <input
                        value={guardianContactDraft.guardianEmail}
                        onChange={(event) => updateGuardianContactDraft('guardianEmail', event.target.value)}
                        aria-label="Patient guardian email"
                      />
                    </label>
                    <div className="contact-actions">
                      <button
                        className="icon-text-button primary"
                        type="submit"
                        disabled={guardianContactSaveStatus === 'saving'}
                      >
                        <Check size={15} />
                        <span>{guardianContactSaveStatus === 'saving' ? 'Saving' : 'Save guardian'}</span>
                      </button>
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setGuardianContactDraft(buildGuardianContactDraft(chart))
                          setIsEditingGuardianContact(false)
                          setGuardianContactSaveStatus('idle')
                        }}
                      >
                        <X size={15} />
                        <span>Cancel</span>
                      </button>
                    </div>
                  </form>
                ) : (
                  <>
                    <Field label="Mother name" value={chart?.motherName} />
                    <Field label="Guardian" value={chart?.guardianName} />
                    <Field label="Relationship" value={formatGuardianRelationship(chart?.guardianRelationship)} />
                    <Field label="Guardian sex" value={formatGuardianSex(chart?.guardianSex)} />
                    <Field label="Guardian address" value={chart?.guardianAddress} />
                    <Field label="Guardian city" value={chart?.guardianCity} />
                    <Field label="Guardian state" value={chart?.guardianState} />
                    <Field label="Guardian postal code" value={chart?.guardianPostalCode} />
                    <Field label="Guardian country" value={chart?.guardianCountry} />
                    <Field label="Guardian phone" value={chart?.guardianPhone} />
                    <Field label="Guardian work phone" value={chart?.guardianWorkPhone} />
                    <Field label="Guardian email" value={chart?.guardianEmail} />
                    <div className="contact-actions">
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setGuardianContactDraft(buildGuardianContactDraft(chart))
                          setIsEditingGuardianContact(true)
                          setGuardianContactSaveStatus('idle')
                        }}
                        disabled={!chart}
                      >
                        <Pencil size={15} />
                        <span>Edit guardian</span>
                      </button>
                      {guardianContactSaveStatus === 'saved' && <span className="save-note">Saved</span>}
                      {guardianContactSaveStatus === 'error' && <span className="save-note error">Save failed</span>}
                    </div>
                  </>
                )}
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

              <InfoPanel title="Duplicate Detection" icon={UserRound}>
                <div className="duplicate-readiness-panel" aria-label="Patient duplicate detection">
                  <div className="duplicate-readiness-header">
                    <strong>Patient duplicate detection</strong>
                    <span>{chart ? `${chart.duplicateCandidates.length} candidates` : 'Loading'}</span>
                  </div>
                  <PatientDuplicateCandidateList candidates={chart?.duplicateCandidates ?? []} />
                </div>
              </InfoPanel>

              <InfoPanel title="Insurance" icon={WalletCards}>
                <form className="insurance-form" onSubmit={handleInsuranceSubmit} aria-label="Insurance coverage form">
                  <div className="mutation-grid two-column">
                    <label className="contact-field">
                      <span>Type</span>
                      <select
                        value={insuranceDraft.type}
                        onChange={(event) => updateInsuranceDraft('type', event.target.value)}
                        aria-label="Insurance type"
                      >
                        <option value="primary">Primary</option>
                        <option value="secondary">Secondary</option>
                        <option value="tertiary">Tertiary</option>
                      </select>
                    </label>
                    <label className="contact-field">
                      <span>Relationship</span>
                      <select
                        value={insuranceDraft.relationship}
                        onChange={(event) => updateInsuranceDraft('relationship', event.target.value)}
                        aria-label="Insurance relationship"
                      >
                        <option value="self">Self</option>
                        <option value="spouse">Spouse</option>
                        <option value="child">Child</option>
                        <option value="other">Other</option>
                      </select>
                    </label>
                  </div>
                  <label className="contact-field">
                    <span>Provider</span>
                    <input
                      value={insuranceDraft.provider}
                      onChange={(event) => updateInsuranceDraft('provider', event.target.value)}
                      aria-label="Insurance provider"
                      required
                    />
                  </label>
                  <label className="contact-field">
                    <span>Plan</span>
                    <input
                      value={insuranceDraft.planName}
                      onChange={(event) => updateInsuranceDraft('planName', event.target.value)}
                      aria-label="Insurance plan"
                      required
                    />
                  </label>
                  <div className="mutation-grid two-column">
                    <label className="contact-field">
                      <span>Policy</span>
                      <input
                        value={insuranceDraft.policyNumber}
                        onChange={(event) => updateInsuranceDraft('policyNumber', event.target.value)}
                        aria-label="Insurance policy"
                        required
                      />
                    </label>
                    <label className="contact-field">
                      <span>Group</span>
                      <input
                        value={insuranceDraft.groupNumber}
                        onChange={(event) => updateInsuranceDraft('groupNumber', event.target.value)}
                        aria-label="Insurance group"
                        required
                      />
                    </label>
                  </div>
                  <div className="contact-actions">
                    <button className="icon-text-button primary" type="submit" disabled={!chart || insuranceSaveStatus === 'saving'}>
                      <Check size={15} />
                      <span>{editingInsuranceId ? 'Update coverage' : 'Add coverage'}</span>
                    </button>
                    {editingInsuranceId && (
                      <button
                        className="icon-text-button"
                        type="button"
                        onClick={() => {
                          setEditingInsuranceId(null)
                          setInsuranceDraft(buildInsuranceDraft())
                          setInsuranceSaveStatus('idle')
                        }}
                      >
                        <X size={15} />
                        <span>Cancel edit</span>
                      </button>
                    )}
                    {insuranceSaveStatus === 'saved' && <span className="save-note">Saved</span>}
                    {insuranceSaveStatus === 'error' && <span className="save-note error">Save failed</span>}
                  </div>
                </form>
                <InsuranceCoverageList
                  items={chart?.insurance ?? []}
                  loading={chartStatus === 'loading'}
                  disabled={!chart || insuranceSaveStatus === 'saving'}
                  onEdit={handleInsuranceEdit}
                  onDelete={(item) => void handleInsuranceDelete(item)}
                />
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
          <div className="empty-chart">
            {sessionId ? 'Select a patient to open the chart summary' : 'Sign in to load patient charts'}
          </div>
        )}
      </section>
    </section>
  )
}

const appointmentCategoryOptions = [
  { id: 9, label: 'Established Patient' },
  { id: 10, label: 'New Patient' },
  { id: 13, label: 'Preventive Care Services' },
] as const

const appointmentRepeatUnitOptions = [
  { id: 0, label: 'Day' },
  { id: 4, label: 'Workday' },
  { id: 1, label: 'Week' },
  { id: 2, label: 'Month' },
  { id: 3, label: 'Year' },
] as const

const appointmentWeekdayOptions = [
  { id: 1, shortLabel: 'Sun', label: 'Sunday' },
  { id: 2, shortLabel: 'Mon', label: 'Monday' },
  { id: 3, shortLabel: 'Tue', label: 'Tuesday' },
  { id: 4, shortLabel: 'Wed', label: 'Wednesday' },
  { id: 5, shortLabel: 'Thu', label: 'Thursday' },
  { id: 6, shortLabel: 'Fri', label: 'Friday' },
  { id: 7, shortLabel: 'Sat', label: 'Saturday' },
] as const

const appointmentRepeatOnOrdinalOptions = [
  { id: 1, label: '1st' },
  { id: 2, label: '2nd' },
  { id: 3, label: '3rd' },
  { id: 4, label: '4th' },
  { id: 5, label: 'Last' },
] as const

const appointmentRepeatOnWeekdayOptions = [
  { id: 0, shortLabel: 'Sun', label: 'Sunday' },
  { id: 1, shortLabel: 'Mon', label: 'Monday' },
  { id: 2, shortLabel: 'Tue', label: 'Tuesday' },
  { id: 3, shortLabel: 'Wed', label: 'Wednesday' },
  { id: 4, shortLabel: 'Thu', label: 'Thursday' },
  { id: 5, shortLabel: 'Fri', label: 'Friday' },
  { id: 6, shortLabel: 'Sat', label: 'Saturday' },
] as const

function appointmentCategoryLabel(appointment: Pick<AppointmentListItem, 'categoryId' | 'categoryName'>) {
  return appointment.categoryName ?? appointmentCategoryOptions.find((category) => category.id === appointment.categoryId)?.label ?? 'Category not recorded'
}

function appointmentCategoryDetail(appointment: Pick<AppointmentListItem, 'categoryId' | 'categoryName'>) {
  const label = appointmentCategoryLabel(appointment)
  return appointment.categoryId ? `${label} (${appointment.categoryId})` : label
}

function appointmentWeekdayLabels(recurrenceDays?: number[] | null) {
  const labels = new Map<number, string>(appointmentWeekdayOptions.map((day) => [day.id, day.shortLabel]))
  const values = Array.from(new Set(recurrenceDays ?? []))
    .filter((day) => day >= 1 && day <= 7)
    .sort((left, right) => left - right)
    .map((day) => labels.get(day) ?? `Day ${day}`)
  return values.length === 0 ? 'None' : values.join(', ')
}

function appointmentRepeatOnLabel(appointment: Pick<AppointmentListItem, 'repeatOnNum' | 'repeatOnDay' | 'repeatOnFrequency'>) {
  const ordinal = appointmentRepeatOnOrdinalOptions.find((option) => option.id === appointment.repeatOnNum)?.label
  const weekday = appointmentRepeatOnWeekdayOptions.find((option) => option.id === appointment.repeatOnDay)?.shortLabel
  if (!ordinal || !weekday) {
    return null
  }

  const frequency = Math.max(1, appointment.repeatOnFrequency ?? 1)
  return frequency === 1 ? `${ordinal} ${weekday} each month` : `${ordinal} ${weekday} every ${frequency} months`
}

function toggleAppointmentWeekday(days: number[], day: number, checked: boolean) {
  const next = new Set(days)
  if (checked) {
    next.add(day)
  } else {
    next.delete(day)
  }

  return Array.from(next)
    .filter((value) => value >= 1 && value <= 7)
    .sort((left, right) => left - right)
}

function appointmentOccurrenceDetail(
  appointment: Pick<AppointmentListItem, 'isRecurringSeries' | 'isVirtualOccurrence' | 'occurrenceNumber'>,
) {
  if (!appointment.isRecurringSeries) {
    return null
  }

  return appointment.isVirtualOccurrence
    ? `Generated occurrence ${appointment.occurrenceNumber ?? ''}`.trim()
    : 'Series anchor'
}

function appointmentSkippedDatesDetail(
  appointment: Pick<AppointmentListItem, 'recurrenceExdates' | 'recurrenceExceptionCount'>,
) {
  const dates = appointment.recurrenceExdates ?? []
  if (dates.length > 0) {
    return dates.join(', ')
  }

  return appointment.recurrenceExceptionCount > 0 ? `${appointment.recurrenceExceptionCount} skipped` : 'None'
}

function appointmentProviderOverlapDetail(
  appointment: Pick<AppointmentListItem, 'providerOverlapCount' | 'providerOverlapAppointmentIds'>,
) {
  return appointmentOverlapDetail(appointment.providerOverlapCount, appointment.providerOverlapAppointmentIds)
}

function appointmentPatientOverlapDetail(
  appointment: Pick<AppointmentListItem, 'patientOverlapCount' | 'patientOverlapAppointmentIds'>,
) {
  return appointmentOverlapDetail(appointment.patientOverlapCount, appointment.patientOverlapAppointmentIds)
}

function appointmentRoomOverlapDetail(
  appointment: Pick<AppointmentListItem, 'roomOverlapCount' | 'roomOverlapAppointmentIds'>,
) {
  return appointmentOverlapDetail(appointment.roomOverlapCount, appointment.roomOverlapAppointmentIds)
}

function appointmentReminderLeadDetail(appointment: Pick<AppointmentListItem, 'reminderLeadDays'>) {
  if (appointment.reminderLeadDays === null || appointment.reminderLeadDays === undefined) {
    return null
  }

  const suffix = appointment.reminderLeadDays === 1 ? 'day' : 'days'
  return `${appointment.reminderLeadDays} ${suffix}`
}

function formatSignatureCount(count: number) {
  return `${count} ${count === 1 ? 'signature' : 'signatures'}`
}

function formatAmendmentCount(count: number) {
  return `${count} ${count === 1 ? 'amendment' : 'amendments'}`
}

function appointmentOverlapDetail(count: number, appointmentIds: string[]) {
  if (count <= 0) {
    return 'None'
  }

  const suffix = count === 1 ? 'appointment' : 'appointments'
  const ids = appointmentIds.length > 0
    ? ` (${appointmentIds.join(', ')})`
    : ''
  return `${count} overlapping ${suffix}${ids}`
}

function careLocationDetail(name: string | null | undefined, id: number | null | undefined) {
  return id ? `${name || 'Not recorded'} (${id})` : name
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
  sessionId,
  onCalendarSessionActive,
  onPatientIdChange,
  onFromDateChange,
  onSelectAppointment,
  onCreateAppointment,
  onUpdateAppointment,
  onRescheduleAppointmentOccurrence,
  onArriveAppointment,
  onCheckOutAppointment,
  onNoShowAppointment,
  onCancelAppointment,
  onDeleteAppointment,
  onRestoreAppointmentOccurrence,
}: {
  patientId: string
  fromDate: string
  searchResult: AppointmentSearchResponse | null
  selectedAppointmentId: string | null
  appointmentDetail: AppointmentDetail | null
  searchStatus: 'idle' | 'loading' | 'ready' | 'error'
  detailStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onCalendarSessionActive: (sessionId: string) => void
  onPatientIdChange: (value: string) => void
  onFromDateChange: (value: string) => void
  onSelectAppointment: (appointmentId: string) => void
  onCreateAppointment: (input: AppointmentCreateInput) => Promise<AppointmentDetail>
  onUpdateAppointment: (appointment: AppointmentDetail, input: AppointmentUpdateInput) => Promise<AppointmentDetail>
  onRescheduleAppointmentOccurrence: (appointment: AppointmentDetail, input: AppointmentOccurrenceRescheduleInput) => Promise<AppointmentDetail>
  onArriveAppointment: (appointment: AppointmentDetail) => Promise<AppointmentDetail>
  onCheckOutAppointment: (appointment: AppointmentDetail) => Promise<AppointmentDetail>
  onNoShowAppointment: (appointment: AppointmentDetail) => Promise<AppointmentDetail>
  onCancelAppointment: (appointment: AppointmentDetail) => Promise<AppointmentDetail>
  onDeleteAppointment: (appointment: AppointmentDetail) => Promise<void>
  onRestoreAppointmentOccurrence: (appointment: AppointmentDetail, occurrenceDate: string) => Promise<AppointmentDetail>
}) {
  const [draftTitle, setDraftTitle] = useState('Parity Appointment')
  const [draftDate, setDraftDate] = useState('2026-10-15')
  const [draftStartTime, setDraftStartTime] = useState('10:30')
  const [draftDuration, setDraftDuration] = useState('30')
  const [draftRoom, setDraftRoom] = useState('Parity')
  const [draftCategoryId, setDraftCategoryId] = useState('9')
  const [draftProviderId, setDraftProviderId] = useState('')
  const [draftFacilityId, setDraftFacilityId] = useState('10')
  const [draftBillingLocationId, setDraftBillingLocationId] = useState('10')
  const [draftComments, setDraftComments] = useState('')
  const [draftRepeats, setDraftRepeats] = useState(false)
  const [draftRepeatFrequency, setDraftRepeatFrequency] = useState('1')
  const [draftRepeatUnit, setDraftRepeatUnit] = useState('1')
  const [draftSpecificWeekdays, setDraftSpecificWeekdays] = useState(false)
  const [draftMonthlyRepeatOn, setDraftMonthlyRepeatOn] = useState(false)
  const [draftRepeatOnNum, setDraftRepeatOnNum] = useState('2')
  const [draftRepeatOnDay, setDraftRepeatOnDay] = useState('2')
  const [draftRepeatOnFrequency, setDraftRepeatOnFrequency] = useState('1')
  const [draftRecurrenceDays, setDraftRecurrenceDays] = useState<number[]>([2, 4, 6])
  const [draftRecurrenceEndDate, setDraftRecurrenceEndDate] = useState('2026-12-31')
  const [editTitle, setEditTitle] = useState('')
  const [editDate, setEditDate] = useState('')
  const [editStartTime, setEditStartTime] = useState('')
  const [editDuration, setEditDuration] = useState('')
  const [editRoom, setEditRoom] = useState('')
  const [editCategoryId, setEditCategoryId] = useState('9')
  const [editProviderId, setEditProviderId] = useState('')
  const [editFacilityId, setEditFacilityId] = useState('')
  const [editBillingLocationId, setEditBillingLocationId] = useState('')
  const [editComments, setEditComments] = useState('')
  const [editRepeats, setEditRepeats] = useState(false)
  const [editRepeatFrequency, setEditRepeatFrequency] = useState('1')
  const [editRepeatUnit, setEditRepeatUnit] = useState('1')
  const [editSpecificWeekdays, setEditSpecificWeekdays] = useState(false)
  const [editMonthlyRepeatOn, setEditMonthlyRepeatOn] = useState(false)
  const [editRepeatOnNum, setEditRepeatOnNum] = useState('1')
  const [editRepeatOnDay, setEditRepeatOnDay] = useState('0')
  const [editRepeatOnFrequency, setEditRepeatOnFrequency] = useState('1')
  const [editRecurrenceDays, setEditRecurrenceDays] = useState<number[]>([])
  const [editRecurrenceEndDate, setEditRecurrenceEndDate] = useState('')
  const [editRecurrenceExdates, setEditRecurrenceExdates] = useState('')
  const [editStatus, setEditStatus] = useState('-')
  const [mutationStatus, setMutationStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [calendarLoginUsername, setCalendarLoginUsername] = useState('admin')
  const [calendarLoginPassword, setCalendarLoginPassword] = useState('pass')
  const [calendarLoginStatus, setCalendarLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [calendarLoginMessage, setCalendarLoginMessage] = useState<string | null>(null)
  const selectedOccurrenceIsVirtual = appointmentDetail?.isVirtualOccurrence ?? false
  const calendarLocked = !sessionId

  useEffect(() => {
    if (!appointmentDetail) {
      return
    }

    setEditTitle(appointmentDetail.title)
    setEditDate(appointmentDetail.date)
    setEditStartTime(appointmentDetail.startTime)
    setEditDuration(String(appointmentDetail.durationMinutes))
    setEditRoom(appointmentDetail.room ?? '')
    setEditCategoryId(String(appointmentDetail.categoryId ?? 9))
    setEditProviderId(appointmentDetail.providerId ? String(appointmentDetail.providerId) : '')
    setEditFacilityId(appointmentDetail.facilityId ? String(appointmentDetail.facilityId) : '')
    setEditBillingLocationId(appointmentDetail.billingLocationId ? String(appointmentDetail.billingLocationId) : '')
    setEditComments(appointmentDetail.comments ?? '')
    const usesMonthlyRepeatOn = appointmentDetail.recurrenceType === 2
    const usesSpecificWeekdays = appointmentDetail.recurrenceType === 3
    setEditRepeats(appointmentDetail.recurrenceType > 0)
    setEditRepeatFrequency(String(usesSpecificWeekdays || usesMonthlyRepeatOn ? 1 : appointmentDetail.repeatFrequency ?? 1))
    setEditRepeatUnit(String(usesSpecificWeekdays || usesMonthlyRepeatOn ? 1 : appointmentDetail.repeatUnit ?? 1))
    setEditSpecificWeekdays(usesSpecificWeekdays)
    setEditMonthlyRepeatOn(usesMonthlyRepeatOn)
    setEditRepeatOnNum(String(appointmentDetail.repeatOnNum ?? 1))
    setEditRepeatOnDay(String(appointmentDetail.repeatOnDay ?? 0))
    setEditRepeatOnFrequency(String(appointmentDetail.repeatOnFrequency ?? 1))
    setEditRecurrenceDays(appointmentDetail.recurrenceDays ?? [])
    setEditRecurrenceEndDate(appointmentDetail.recurrenceEndDate ?? appointmentDetail.date)
    setEditRecurrenceExdates(appointmentDetail.recurrenceExdates.join(', '))
    setEditStatus(appointmentDetail.status ?? '-')
  }, [appointmentDetail])

  async function handleCalendarLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setCalendarLoginStatus('checking')
    setCalendarLoginMessage(null)

    try {
      const result = await login({ username: calendarLoginUsername, password: calendarLoginPassword })
      if (result.authenticated && result.sessionId) {
        onCalendarSessionActive(result.sessionId)
        setCalendarLoginStatus('authenticated')
        setCalendarLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setCalendarLoginStatus('rejected')
        setCalendarLoginMessage(result.failureReason ?? 'Calendar access was rejected.')
      }
    } catch (loginError) {
      setCalendarLoginStatus('error')
      setCalendarLoginMessage(loginError instanceof Error ? loginError.message : 'Calendar access check failed')
    }
  }

  async function handleCreateSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (calendarLocked) {
      setMutationStatus('error')
      return
    }

    setMutationStatus('saving')

    try {
      const usesMonthlyRepeatOn = draftRepeats && draftMonthlyRepeatOn
      const usesSpecificWeekdays = draftRepeats && draftSpecificWeekdays
      await onCreateAppointment({
        patientId,
        title: draftTitle,
        date: draftDate,
        startTime: draftStartTime,
        durationMinutes: Number(draftDuration),
        room: draftRoom,
        categoryId: Number(draftCategoryId),
        providerId: numberOrNull(draftProviderId),
        facilityId: numberOrNull(draftFacilityId),
        billingLocationId: numberOrNull(draftBillingLocationId),
        comments: draftComments,
        recurrenceType: usesMonthlyRepeatOn ? 2 : usesSpecificWeekdays ? 3 : draftRepeats ? 1 : 0,
        repeatFrequency: usesSpecificWeekdays || usesMonthlyRepeatOn ? null : draftRepeats ? Number(draftRepeatFrequency) : null,
        repeatUnit: usesSpecificWeekdays ? 6 : usesMonthlyRepeatOn ? null : draftRepeats ? Number(draftRepeatUnit) : null,
        repeatOnNum: usesMonthlyRepeatOn ? Number(draftRepeatOnNum) : null,
        repeatOnDay: usesMonthlyRepeatOn ? Number(draftRepeatOnDay) : null,
        repeatOnFrequency: usesMonthlyRepeatOn ? Number(draftRepeatOnFrequency) : null,
        recurrenceDays: usesSpecificWeekdays ? draftRecurrenceDays : null,
        recurrenceEndDate: draftRepeats ? draftRecurrenceEndDate : null,
      })
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleCancelSelected() {
    if (!appointmentDetail || calendarLocked) {
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

  async function handleArriveSelected() {
    if (!appointmentDetail || calendarLocked) {
      return
    }

    setMutationStatus('saving')
    try {
      await onArriveAppointment(appointmentDetail)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleCheckOutSelected() {
    if (!appointmentDetail || calendarLocked) {
      return
    }

    setMutationStatus('saving')
    try {
      await onCheckOutAppointment(appointmentDetail)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleNoShowSelected() {
    if (!appointmentDetail || calendarLocked) {
      return
    }

    setMutationStatus('saving')
    try {
      await onNoShowAppointment(appointmentDetail)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleUpdateSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!appointmentDetail || calendarLocked) {
      return
    }

    setMutationStatus('saving')
    try {
      const usesMonthlyRepeatOn = editRepeats && editMonthlyRepeatOn
      const usesSpecificWeekdays = editRepeats && editSpecificWeekdays
      const scheduleInput = {
        title: editTitle,
        date: editDate,
        startTime: editStartTime,
        durationMinutes: Number(editDuration),
        room: editRoom,
        categoryId: Number(editCategoryId),
        providerId: numberOrNull(editProviderId),
        facilityId: numberOrNull(editFacilityId),
        billingLocationId: numberOrNull(editBillingLocationId),
        status: editStatus,
        comments: editComments,
      }

      if (selectedOccurrenceIsVirtual) {
        await onRescheduleAppointmentOccurrence(appointmentDetail, scheduleInput)
      } else {
        await onUpdateAppointment(appointmentDetail, {
          ...scheduleInput,
          recurrenceType: usesMonthlyRepeatOn ? 2 : usesSpecificWeekdays ? 3 : editRepeats ? 1 : 0,
          repeatFrequency: usesSpecificWeekdays || usesMonthlyRepeatOn ? null : editRepeats ? Number(editRepeatFrequency) : null,
          repeatUnit: usesSpecificWeekdays ? 6 : usesMonthlyRepeatOn ? null : editRepeats ? Number(editRepeatUnit) : null,
          repeatOnNum: usesMonthlyRepeatOn ? Number(editRepeatOnNum) : null,
          repeatOnDay: usesMonthlyRepeatOn ? Number(editRepeatOnDay) : null,
          repeatOnFrequency: usesMonthlyRepeatOn ? Number(editRepeatOnFrequency) : null,
          recurrenceDays: usesSpecificWeekdays ? editRecurrenceDays : null,
          recurrenceEndDate: editRepeats ? editRecurrenceEndDate : null,
          recurrenceExdates: editRepeats ? parseDateList(editRecurrenceExdates) : null,
        })
      }
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  async function handleDeleteSelected() {
    if (!appointmentDetail || calendarLocked) {
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

  async function handleRestoreSkippedDate(occurrenceDate: string) {
    if (!appointmentDetail || calendarLocked) {
      return
    }

    setMutationStatus('saving')
    try {
      await onRestoreAppointmentOccurrence(appointmentDetail, occurrenceDate)
      setMutationStatus('saved')
    } catch {
      setMutationStatus('error')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Appointment search">
        {(!sessionId || searchStatus === 'error') && (
          <form className="mutation-form" aria-label="Calendar access" onSubmit={handleCalendarLogin}>
            <div className="panel-heading">
              <ShieldCheck size={17} />
              <h3>Calendar Access</h3>
            </div>
            <p className="access-copy">Sign in to load appointment schedules.</p>
            <label>
              Username
              <input value={calendarLoginUsername} onChange={(event) => setCalendarLoginUsername(event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={calendarLoginPassword}
                onChange={(event) => setCalendarLoginPassword(event.target.value)}
              />
            </label>
            <button type="submit" disabled={calendarLoginStatus === 'checking'}>
              <LogIn size={15} />
              {calendarLoginStatus === 'checking' ? 'Checking' : 'Verify Calendar Access'}
            </button>
            {calendarLoginMessage && (
              <div className={calendarLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {calendarLoginMessage}
              </div>
            )}
          </form>
        )}
        {sessionId && calendarLoginMessage && calendarLoginStatus === 'authenticated' && (
          <div className="status-banner">{calendarLoginMessage}</div>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Appointment patient ID"
              placeholder="MOD-PAT-0003"
              disabled={calendarLocked}
            />
          </label>
          <label className="filter-field">
            <span>From</span>
            <input
              value={fromDate}
              onChange={(event) => onFromDateChange(event.target.value)}
              aria-label="Appointment from date"
              type="date"
              disabled={calendarLocked}
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
              <span>Category</span>
              <select value={draftCategoryId} onChange={(event) => setDraftCategoryId(event.target.value)} aria-label="New appointment category">
                {appointmentCategoryOptions.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="mutation-grid two-column">
            <label className="contact-field">
              <span>Provider ID</span>
              <input
                type="number"
                value={draftProviderId}
                onChange={(event) => setDraftProviderId(event.target.value)}
                aria-label="New appointment provider ID"
                placeholder="Patient default"
              />
            </label>
            <label className="contact-field">
              <span>Facility ID</span>
              <input
                type="number"
                value={draftFacilityId}
                onChange={(event) => setDraftFacilityId(event.target.value)}
                aria-label="New appointment facility ID"
              />
            </label>
            <label className="contact-field">
              <span>Billing facility ID</span>
              <input
                type="number"
                value={draftBillingLocationId}
                onChange={(event) => setDraftBillingLocationId(event.target.value)}
                aria-label="New appointment billing facility ID"
              />
            </label>
            <label className="contact-field">
              <span>Room</span>
              <input value={draftRoom} onChange={(event) => setDraftRoom(event.target.value)} aria-label="New appointment room" />
            </label>
            <label className="contact-field">
              <span>Comments</span>
              <textarea
                rows={2}
                value={draftComments}
                onChange={(event) => setDraftComments(event.target.value)}
                aria-label="New appointment comments"
              />
            </label>
            <label className="contact-field checkbox-field">
              <input
                type="checkbox"
                checked={draftRepeats}
                onChange={(event) => {
                  setDraftRepeats(event.target.checked)
                  if (!event.target.checked) {
                    setDraftSpecificWeekdays(false)
                    setDraftMonthlyRepeatOn(false)
                  }
                }}
                aria-label="New appointment repeats"
              />
              <span>Repeats</span>
            </label>
            <label className="contact-field checkbox-field">
              <input
                type="checkbox"
                checked={draftSpecificWeekdays}
                onChange={(event) => {
                  setDraftSpecificWeekdays(event.target.checked)
                  if (event.target.checked) {
                    setDraftMonthlyRepeatOn(false)
                  }
                }}
                aria-label="New appointment specific weekdays"
                disabled={!draftRepeats}
              />
              <span>Specific weekdays</span>
            </label>
            <label className="contact-field checkbox-field">
              <input
                type="checkbox"
                checked={draftMonthlyRepeatOn}
                onChange={(event) => {
                  setDraftMonthlyRepeatOn(event.target.checked)
                  if (event.target.checked) {
                    setDraftSpecificWeekdays(false)
                  }
                }}
                aria-label="New appointment monthly repeat on"
                disabled={!draftRepeats}
              />
              <span>Monthly repeat on</span>
            </label>
            <div className="weekday-toggle-row" aria-label="New appointment recurrence weekdays">
              {appointmentWeekdayOptions.map((day) => (
                <label className="weekday-toggle" key={day.id}>
                  <input
                    type="checkbox"
                    checked={draftRecurrenceDays.includes(day.id)}
                    onChange={(event) => setDraftRecurrenceDays((current) => toggleAppointmentWeekday(current, day.id, event.target.checked))}
                    aria-label={`New appointment weekday ${day.label}`}
                    disabled={!draftRepeats || !draftSpecificWeekdays}
                  />
                  <span>{day.shortLabel}</span>
                </label>
              ))}
            </div>
            <label className="contact-field">
              <span>Every</span>
              <input
                type="number"
                min="1"
                value={draftRepeatFrequency}
                onChange={(event) => setDraftRepeatFrequency(event.target.value)}
                aria-label="New appointment repeat frequency"
                disabled={!draftRepeats || draftSpecificWeekdays || draftMonthlyRepeatOn}
              />
            </label>
            <label className="contact-field">
              <span>Repeat unit</span>
              <select
                value={draftRepeatUnit}
                onChange={(event) => setDraftRepeatUnit(event.target.value)}
                aria-label="New appointment repeat unit"
                disabled={!draftRepeats || draftSpecificWeekdays || draftMonthlyRepeatOn}
              >
                {appointmentRepeatUnitOptions.map((unit) => (
                  <option key={unit.id} value={unit.id}>
                    {unit.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="contact-field">
              <span>Repeat on</span>
              <select
                value={draftRepeatOnNum}
                onChange={(event) => setDraftRepeatOnNum(event.target.value)}
                aria-label="New appointment repeat-on ordinal"
                disabled={!draftRepeats || !draftMonthlyRepeatOn}
              >
                {appointmentRepeatOnOrdinalOptions.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="contact-field">
              <span>Weekday</span>
              <select
                value={draftRepeatOnDay}
                onChange={(event) => setDraftRepeatOnDay(event.target.value)}
                aria-label="New appointment repeat-on weekday"
                disabled={!draftRepeats || !draftMonthlyRepeatOn}
              >
                {appointmentRepeatOnWeekdayOptions.map((day) => (
                  <option key={day.id} value={day.id}>
                    {day.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="contact-field">
              <span>Month cadence</span>
              <input
                type="number"
                min="1"
                value={draftRepeatOnFrequency}
                onChange={(event) => setDraftRepeatOnFrequency(event.target.value)}
                aria-label="New appointment repeat-on frequency"
                disabled={!draftRepeats || !draftMonthlyRepeatOn}
              />
            </label>
            <label className="contact-field">
              <span>Until</span>
              <input
                type="date"
                value={draftRecurrenceEndDate}
                onChange={(event) => setDraftRecurrenceEndDate(event.target.value)}
                aria-label="New appointment recurrence end date"
                disabled={!draftRepeats}
              />
            </label>
          </div>
          <div className="contact-actions">
            <button className="icon-text-button primary" type="submit" disabled={calendarLocked || mutationStatus === 'saving'}>
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
        {calendarLocked && <div className="status-banner">Sign in to load appointment schedules.</div>}

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
                onClick={handleArriveSelected}
                disabled={calendarLocked || detailStatus === 'loading' || selectedOccurrenceIsVirtual || appointmentDetail.status === '@' || appointmentDetail.status === '>' || appointmentDetail.status === '?'}
              >
                <Check size={15} />
                <span>Mark arrived</span>
              </button>
              <button
                className="icon-text-button"
                type="button"
                onClick={handleCheckOutSelected}
                disabled={calendarLocked || detailStatus === 'loading' || selectedOccurrenceIsVirtual || appointmentDetail.status === '>' || appointmentDetail.status === '?'}
              >
                <ClipboardList size={15} />
                <span>Mark checked out</span>
              </button>
              <button
                className="icon-text-button"
                type="button"
                onClick={handleNoShowSelected}
                disabled={calendarLocked || detailStatus === 'loading' || selectedOccurrenceIsVirtual || appointmentDetail.status === '?' || appointmentDetail.status === '@' || appointmentDetail.status === '>'}
              >
                <Clock size={15} />
                <span>Mark no-show</span>
              </button>
              <button
                className="icon-text-button"
                type="button"
                onClick={handleCancelSelected}
                disabled={calendarLocked || detailStatus === 'loading' || selectedOccurrenceIsVirtual || appointmentDetail.status === 'x'}
              >
                <Ban size={15} />
                <span>Cancel appointment</span>
              </button>
              <button
                className="icon-text-button danger"
                type="button"
                onClick={handleDeleteSelected}
                disabled={calendarLocked || detailStatus === 'loading'}
              >
                {selectedOccurrenceIsVirtual ? <Ban size={15} /> : <Trash2 size={15} />}
                <span>{selectedOccurrenceIsVirtual ? 'Skip occurrence' : 'Delete appointment'}</span>
              </button>
            </div>

            <form
              className="appointment-mutation-panel appointment-edit-panel"
              onSubmit={handleUpdateSubmit}
              aria-label={selectedOccurrenceIsVirtual ? 'Reschedule occurrence' : 'Reschedule appointment'}
            >
              <div className="panel-heading compact-heading">
                <Pencil size={16} />
                <h3>{selectedOccurrenceIsVirtual ? 'Reschedule Occurrence' : 'Reschedule Appointment'}</h3>
              </div>
              <label className="contact-field">
                <span>Title</span>
                <input
                  value={editTitle}
                  onChange={(event) => setEditTitle(event.target.value)}
                  aria-label="Edit appointment title"
                />
              </label>
              <div className="mutation-grid two-column">
                <label className="contact-field">
                  <span>Date</span>
                  <input
                    type="date"
                    value={editDate}
                    onChange={(event) => setEditDate(event.target.value)}
                    aria-label="Edit appointment date"
                  />
                </label>
                <label className="contact-field">
                  <span>Start</span>
                  <input
                    type="time"
                    value={editStartTime}
                    onChange={(event) => setEditStartTime(event.target.value)}
                    aria-label="Edit appointment start time"
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
                    value={editDuration}
                    onChange={(event) => setEditDuration(event.target.value)}
                    aria-label="Edit appointment duration"
                  />
                </label>
                <label className="contact-field">
                  <span>Status</span>
                  <select value={editStatus} onChange={(event) => setEditStatus(event.target.value)} aria-label="Edit appointment status">
                    <option value="-">Scheduled</option>
                    <option value="@">Arrived</option>
                    <option value="~">Pending</option>
                    <option value="x">Cancelled</option>
                  </select>
                </label>
              </div>
              <div className="mutation-grid two-column">
                <label className="contact-field">
                  <span>Category</span>
                  <select value={editCategoryId} onChange={(event) => setEditCategoryId(event.target.value)} aria-label="Edit appointment category">
                    {appointmentCategoryOptions.map((category) => (
                      <option key={category.id} value={category.id}>
                        {category.label}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <div className="mutation-grid two-column">
                <label className="contact-field">
                  <span>Provider ID</span>
                  <input
                    type="number"
                    value={editProviderId}
                    onChange={(event) => setEditProviderId(event.target.value)}
                    aria-label="Edit appointment provider ID"
                  />
                </label>
                <label className="contact-field">
                  <span>Facility ID</span>
                  <input
                    type="number"
                    value={editFacilityId}
                    onChange={(event) => setEditFacilityId(event.target.value)}
                    aria-label="Edit appointment facility ID"
                  />
                </label>
                <label className="contact-field">
                  <span>Billing facility ID</span>
                  <input
                    type="number"
                    value={editBillingLocationId}
                    onChange={(event) => setEditBillingLocationId(event.target.value)}
                    aria-label="Edit appointment billing facility ID"
                  />
                </label>
                <label className="contact-field">
                  <span>Room</span>
                  <input value={editRoom} onChange={(event) => setEditRoom(event.target.value)} aria-label="Edit appointment room" />
                </label>
                <label className="contact-field">
                  <span>Comments</span>
                  <textarea
                    rows={2}
                    value={editComments}
                    onChange={(event) => setEditComments(event.target.value)}
                    aria-label="Edit appointment comments"
                  />
                </label>
                <label className="contact-field checkbox-field">
                  <input
                    type="checkbox"
                    checked={editRepeats}
                    onChange={(event) => {
                      setEditRepeats(event.target.checked)
                      if (!event.target.checked) {
                        setEditSpecificWeekdays(false)
                        setEditMonthlyRepeatOn(false)
                      }
                    }}
                    aria-label="Edit appointment repeats"
                    disabled={selectedOccurrenceIsVirtual}
                  />
                  <span>Repeats</span>
                </label>
                <label className="contact-field checkbox-field">
                  <input
                    type="checkbox"
                    checked={editSpecificWeekdays}
                    onChange={(event) => {
                      setEditSpecificWeekdays(event.target.checked)
                      if (event.target.checked) {
                        setEditMonthlyRepeatOn(false)
                      }
                    }}
                    aria-label="Edit appointment specific weekdays"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats}
                  />
                  <span>Specific weekdays</span>
                </label>
                <label className="contact-field checkbox-field">
                  <input
                    type="checkbox"
                    checked={editMonthlyRepeatOn}
                    onChange={(event) => {
                      setEditMonthlyRepeatOn(event.target.checked)
                      if (event.target.checked) {
                        setEditSpecificWeekdays(false)
                      }
                    }}
                    aria-label="Edit appointment monthly repeat on"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats}
                  />
                  <span>Monthly repeat on</span>
                </label>
                <div className="weekday-toggle-row" aria-label="Edit appointment recurrence weekdays">
                  {appointmentWeekdayOptions.map((day) => (
                    <label className="weekday-toggle" key={day.id}>
                      <input
                        type="checkbox"
                        checked={editRecurrenceDays.includes(day.id)}
                        onChange={(event) => setEditRecurrenceDays((current) => toggleAppointmentWeekday(current, day.id, event.target.checked))}
                        aria-label={`Edit appointment weekday ${day.label}`}
                        disabled={selectedOccurrenceIsVirtual || !editRepeats || !editSpecificWeekdays}
                      />
                      <span>{day.shortLabel}</span>
                    </label>
                  ))}
                </div>
                <label className="contact-field">
                  <span>Every</span>
                  <input
                    type="number"
                    min="1"
                    value={editRepeatFrequency}
                    onChange={(event) => setEditRepeatFrequency(event.target.value)}
                    aria-label="Edit appointment repeat frequency"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats || editSpecificWeekdays || editMonthlyRepeatOn}
                  />
                </label>
                <label className="contact-field">
                  <span>Repeat unit</span>
                  <select
                    value={editRepeatUnit}
                    onChange={(event) => setEditRepeatUnit(event.target.value)}
                    aria-label="Edit appointment repeat unit"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats || editSpecificWeekdays || editMonthlyRepeatOn}
                  >
                    {appointmentRepeatUnitOptions.map((unit) => (
                      <option key={unit.id} value={unit.id}>
                        {unit.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="contact-field">
                  <span>Repeat on</span>
                  <select
                    value={editRepeatOnNum}
                    onChange={(event) => setEditRepeatOnNum(event.target.value)}
                    aria-label="Edit appointment repeat-on ordinal"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats || !editMonthlyRepeatOn}
                  >
                    {appointmentRepeatOnOrdinalOptions.map((option) => (
                      <option key={option.id} value={option.id}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="contact-field">
                  <span>Weekday</span>
                  <select
                    value={editRepeatOnDay}
                    onChange={(event) => setEditRepeatOnDay(event.target.value)}
                    aria-label="Edit appointment repeat-on weekday"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats || !editMonthlyRepeatOn}
                  >
                    {appointmentRepeatOnWeekdayOptions.map((day) => (
                      <option key={day.id} value={day.id}>
                        {day.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="contact-field">
                  <span>Month cadence</span>
                  <input
                    type="number"
                    min="1"
                    value={editRepeatOnFrequency}
                    onChange={(event) => setEditRepeatOnFrequency(event.target.value)}
                    aria-label="Edit appointment repeat-on frequency"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats || !editMonthlyRepeatOn}
                  />
                </label>
                <label className="contact-field">
                  <span>Until</span>
                  <input
                    type="date"
                    value={editRecurrenceEndDate}
                    onChange={(event) => setEditRecurrenceEndDate(event.target.value)}
                    aria-label="Edit appointment recurrence end date"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats}
                  />
                </label>
                <label className="contact-field">
                  <span>Skipped dates</span>
                  <textarea
                    rows={2}
                    value={editRecurrenceExdates}
                    onChange={(event) => setEditRecurrenceExdates(event.target.value)}
                    aria-label="Edit appointment skipped dates"
                    disabled={selectedOccurrenceIsVirtual || !editRepeats}
                  />
                </label>
              </div>
              <div className="contact-actions">
                <button className="icon-text-button primary" type="submit" disabled={calendarLocked || detailStatus === 'loading' || mutationStatus === 'saving'}>
                  <Check size={15} />
                  <span>{mutationStatus === 'saving' ? 'Saving' : selectedOccurrenceIsVirtual ? 'Reschedule occurrence' : 'Save schedule'}</span>
                </button>
              </div>
            </form>

            <div className="appointment-detail-grid">
              <InfoPanel title="Schedule" icon={Clock}>
                <Field label="Date" value={appointmentDetail.date} />
                <Field label="Start time" value={appointmentDetail.startTime} />
                <Field label="Duration" value={`${appointmentDetail.durationMinutes} minutes`} />
                <Field label="Room" value={appointmentDetail.room} />
                <Field label="Comments" value={appointmentDetail.comments} />
                <Field label="Reminder status" value={appointmentDetail.reminderStatus} />
                <Field label="Reminder channel" value={appointmentDetail.reminderChannel} />
                <Field label="Reminder contact" value={appointmentDetail.reminderContact} />
                <Field label="Reminder lead" value={appointmentReminderLeadDetail(appointmentDetail)} />
                <Field label="Recurrence" value={appointmentDetail.recurrenceLabel} />
                <Field label="Repeat on" value={appointmentDetail.recurrenceType === 2 ? appointmentRepeatOnLabel(appointmentDetail) : null} />
                <Field label="Weekdays" value={appointmentDetail.recurrenceType === 3 ? appointmentWeekdayLabels(appointmentDetail.recurrenceDays) : null} />
                <div className="field-row skipped-dates-field">
                  <span>Skipped dates</span>
                  <div className="skipped-date-actions">
                    <strong>{appointmentSkippedDatesDetail(appointmentDetail)}</strong>
                    {appointmentDetail.recurrenceExdates.length > 0 && (
                      <div className="skipped-date-list">
                        {appointmentDetail.recurrenceExdates.map((skippedDate) => (
                          <button
                            className="icon-text-button secondary"
                            type="button"
                            key={skippedDate}
                            onClick={() => void handleRestoreSkippedDate(skippedDate)}
                            disabled={calendarLocked || detailStatus === 'loading' || mutationStatus === 'saving'}
                            aria-label={`Restore occurrence ${skippedDate}`}
                          >
                            <RotateCcw size={14} />
                            <span>Restore {skippedDate}</span>
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
                <Field label="Occurrence" value={appointmentOccurrenceDetail(appointmentDetail)} />
              </InfoPanel>

              <InfoPanel title="Patient" icon={UserRound}>
                <Field label="Patient ID" value={appointmentDetail.pubpid} />
                <Field label="Date of birth" value={appointmentDetail.dateOfBirth} />
                <Field label="Sex" value={appointmentDetail.sex} />
                <Field label="Test purpose" value={appointmentDetail.patientPurpose} />
              </InfoPanel>

              <InfoPanel title="Care Location" icon={MapPin}>
                <Field label="Provider" value={careLocationDetail(appointmentDetail.providerName, appointmentDetail.providerId)} />
                <Field label="Provider overlaps" value={appointmentProviderOverlapDetail(appointmentDetail)} />
                <Field label="Patient overlaps" value={appointmentPatientOverlapDetail(appointmentDetail)} />
                <Field label="Room overlaps" value={appointmentRoomOverlapDetail(appointmentDetail)} />
                <Field label="Facility" value={careLocationDetail(appointmentDetail.facilityName, appointmentDetail.facilityId)} />
                <Field label="Billing facility" value={careLocationDetail(appointmentDetail.billingLocationName, appointmentDetail.billingLocationId)} />
                <Field label="Category" value={appointmentCategoryDetail(appointmentDetail)} />
                <Field label="Appointment ID" value={appointmentDetail.id} />
                <Field label="Series root" value={appointmentDetail.isRecurringSeries ? appointmentDetail.seriesRootId : null} />
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
  includeArchivedDocuments,
  sessionId,
  onEncounterSessionActive,
  onPatientIdChange,
  onFromDateChange,
  onSelectEncounter,
  onIncludeArchivedDocumentsChange,
  onCreateEncounter,
  onUpdateEncounter,
  onDeleteEncounter,
  onCreateVitals,
  onCreateSoapNote,
  onSignEncounter,
  onDeleteEncounterSignature,
  onCreateEncounterDocument,
  onCreateEncounterBinaryDocument,
  onCreateEncounterExternalLinkDocument,
  onUpdateEncounterDocumentMetadata,
  onMoveEncounterDocument,
  onReplaceEncounterDocumentContent,
  onReplaceEncounterDocumentBinaryContent,
  onArchiveEncounterDocument,
  onRestoreEncounterDocument,
  onSignEncounterDocument,
  onDenyEncounterDocument,
  onCreateFeeSheetLine,
  onCreateProcedureOrder,
  onCreateProcedureResultSet,
  onUpdateProcedureResult,
}: {
  patientId: string
  fromDate: string
  searchResult: EncounterSearchResponse | null
  selectedEncounter: number | null
  encounterDetail: EncounterDetail | null
  searchStatus: 'idle' | 'loading' | 'ready' | 'error'
  detailStatus: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  includeArchivedDocuments: boolean
  sessionId: string | null
  onEncounterSessionActive: (sessionId: string) => void
  onPatientIdChange: (value: string) => void
  onFromDateChange: (value: string) => void
  onSelectEncounter: (encounter: number) => void
  onIncludeArchivedDocumentsChange: (value: boolean) => void
  onCreateEncounter: (input: EncounterCreateInput) => Promise<EncounterDetail>
  onUpdateEncounter: (encounter: EncounterDetail, update: EncounterUpdateInput) => Promise<EncounterDetail>
  onDeleteEncounter: (encounter: EncounterDetail) => Promise<void>
  onCreateVitals: (encounter: EncounterDetail, input: EncounterVitalsCreateInput) => Promise<unknown>
  onCreateSoapNote: (encounter: EncounterDetail, input: EncounterSoapNoteCreateInput) => Promise<unknown>
  onSignEncounter: (encounter: EncounterDetail, input: EncounterSignInput) => Promise<EncounterSignatureMutationResponse>
  onDeleteEncounterSignature: (encounter: EncounterDetail, signature: EncounterSignatureItem) => Promise<void>
  onCreateEncounterDocument: (
    encounter: EncounterDetail,
    input: EncounterDocumentCreateInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onCreateEncounterBinaryDocument: (
    encounter: EncounterDetail,
    input: EncounterBinaryDocumentCreateInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onCreateEncounterExternalLinkDocument: (
    encounter: EncounterDetail,
    input: EncounterExternalLinkDocumentCreateInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onUpdateEncounterDocumentMetadata: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentMetadataUpdateInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onMoveEncounterDocument: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    targetEncounter: number,
  ) => Promise<EncounterDocumentMoveResponse>
  onReplaceEncounterDocumentContent: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentContentReplaceInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onReplaceEncounterDocumentBinaryContent: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
    input: PatientDocumentBinaryContentReplaceInput,
  ) => Promise<EncounterDocumentMutationResponse>
  onArchiveEncounterDocument: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ) => Promise<EncounterDocumentMutationResponse>
  onRestoreEncounterDocument: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ) => Promise<EncounterDocumentMutationResponse>
  onSignEncounterDocument: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ) => Promise<EncounterDocumentMutationResponse>
  onDenyEncounterDocument: (
    encounter: EncounterDetail,
    document: EncounterDocumentAttachment,
  ) => Promise<EncounterDocumentMutationResponse>
  onCreateFeeSheetLine: (encounter: EncounterDetail, input: BillingLineCreateInput) => Promise<unknown>
  onCreateProcedureOrder: (encounter: EncounterDetail, input: ProcedureOrderCreateInput) => Promise<unknown>
  onCreateProcedureResultSet: (
    encounter: EncounterDetail,
    input: EncounterProcedureResultSetInput,
  ) => Promise<unknown>
  onUpdateProcedureResult: (
    encounter: EncounterDetail,
    result: ProcedureResultItem,
    input: ProcedureResultUpdateInput,
  ) => Promise<unknown>
}) {
  const [createPatientId, setCreatePatientId] = useState(patientId)
  const [createDateTime, setCreateDateTime] = useState('2026-06-18T10:00')
  const [createReason, setCreateReason] = useState('Follow-up encounter')
  const [createProviderId, setCreateProviderId] = useState('')
  const [createFacilityId, setCreateFacilityId] = useState('10')
  const [createBillingFacilityId, setCreateBillingFacilityId] = useState('10')
  const [createSensitivity, setCreateSensitivity] = useState('normal')
  const [createReferralSource, setCreateReferralSource] = useState('self')
  const [createExternalId, setCreateExternalId] = useState('')
  const [createPosCode, setCreatePosCode] = useState('11')
  const [createBillingNote, setCreateBillingNote] = useState('Created from modernized encounter workspace.')
  const [createStatus, setCreateStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [encounterLoginUsername, setEncounterLoginUsername] = useState('admin')
  const [encounterLoginPassword, setEncounterLoginPassword] = useState('pass')
  const [encounterLoginStatus, setEncounterLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [encounterLoginMessage, setEncounterLoginMessage] = useState<string | null>(null)
  const encounterLocked = !sessionId

  const [summaryReason, setSummaryReason] = useState('')
  const [summarySensitivity, setSummarySensitivity] = useState('')
  const [summaryReferralSource, setSummaryReferralSource] = useState('')
  const [summaryExternalId, setSummaryExternalId] = useState('')
  const [summaryPosCode, setSummaryPosCode] = useState('')
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

  const [signatureSigner, setSignatureSigner] = useState('admin')
  const [signatureSignedAt, setSignatureSignedAt] = useState('2026-06-18T10:20')
  const [signatureMode, setSignatureMode] = useState<'signed' | 'locked'>('signed')
  const [signatureAmendment, setSignatureAmendment] = useState('Encounter reviewed and signed from modernized workspace.')
  const [signatureStatus, setSignatureStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const [encounterDocumentCategoryId, setEncounterDocumentCategoryId] = useState('3')
  const [encounterDocumentName, setEncounterDocumentName] = useState('Encounter attachment')
  const [encounterDocumentDate, setEncounterDocumentDate] = useState('2026-06-18')
  const [encounterDocumentNotes, setEncounterDocumentNotes] = useState('Attached from the modernized encounter workspace.')
  const [encounterDocumentContent, setEncounterDocumentContent] = useState('Encounter attachment text payload.')
  const [encounterDocumentStatus, setEncounterDocumentStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [encounterBinaryDocumentCategoryId, setEncounterBinaryDocumentCategoryId] = useState('3')
  const [encounterBinaryDocumentName, setEncounterBinaryDocumentName] = useState('Encounter binary attachment')
  const [encounterBinaryDocumentDate, setEncounterBinaryDocumentDate] = useState('2026-06-18')
  const [encounterBinaryDocumentNotes, setEncounterBinaryDocumentNotes] = useState('Binary file attached from the modernized encounter workspace.')
  const [encounterBinaryFileName, setEncounterBinaryFileName] = useState('')
  const [encounterBinaryMimeType, setEncounterBinaryMimeType] = useState('')
  const [encounterBinaryContentBase64, setEncounterBinaryContentBase64] = useState('')
  const [encounterBinaryFileMessage, setEncounterBinaryFileMessage] = useState('No file selected')
  const [encounterBinaryDocumentStatus, setEncounterBinaryDocumentStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [encounterExternalLinkCategoryId, setEncounterExternalLinkCategoryId] = useState('3')
  const [encounterExternalLinkName, setEncounterExternalLinkName] = useState('Encounter external link')
  const [encounterExternalLinkDate, setEncounterExternalLinkDate] = useState('2026-06-18')
  const [encounterExternalLinkUrl, setEncounterExternalLinkUrl] = useState('https://example.test/legacy-ehr/encounter-record')
  const [encounterExternalLinkNotes, setEncounterExternalLinkNotes] = useState('External link attached from the modernized encounter workspace.')
  const [encounterExternalLinkStatus, setEncounterExternalLinkStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [encounterDocumentReviewStatus, setEncounterDocumentReviewStatus] = useState<
    'idle' | 'saving' | 'signed' | 'denied' | 'error'
  >('idle')
  const [encounterDocumentMetadataStatus, setEncounterDocumentMetadataStatus] = useState<
    'idle' | 'saving' | 'saved' | 'error'
  >('idle')
  const [encounterDocumentMoveStatus, setEncounterDocumentMoveStatus] = useState<
    'idle' | 'saving' | 'moved' | 'error'
  >('idle')
  const [encounterDocumentContentStatus, setEncounterDocumentContentStatus] = useState<
    'idle' | 'saving' | 'saved' | 'error'
  >('idle')
  const [encounterDocumentArchiveStatus, setEncounterDocumentArchiveStatus] = useState<
    'idle' | 'saving' | 'archived' | 'restored' | 'error'
  >('idle')

  const [feeSheetCodeType, setFeeSheetCodeType] = useState<'CPT4' | 'ICD10'>('CPT4')
  const [feeSheetDate, setFeeSheetDate] = useState('2026-06-18')
  const [feeSheetCode, setFeeSheetCode] = useState('99499')
  const [feeSheetModifier, setFeeSheetModifier] = useState('')
  const [feeSheetDescription, setFeeSheetDescription] = useState('Encounter fee sheet service')
  const [feeSheetFee, setFeeSheetFee] = useState('42.00')
  const [feeSheetUnits, setFeeSheetUnits] = useState('1')
  const [feeSheetJustify, setFeeSheetJustify] = useState('E78.5')
  const [feeSheetStatus, setFeeSheetStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const [encounterProcedureDate, setEncounterProcedureDate] = useState('2026-06-18')
  const [encounterProcedureCode, setEncounterProcedureCode] = useState('80053')
  const [encounterProcedureName, setEncounterProcedureName] = useState('Comprehensive metabolic panel')
  const [encounterProcedureDiagnosis, setEncounterProcedureDiagnosis] = useState('E78.5')
  const [encounterProcedurePriority, setEncounterProcedurePriority] = useState('routine')
  const [encounterProcedureStatusValue, setEncounterProcedureStatusValue] = useState('pending')
  const [encounterProcedureType, setEncounterProcedureType] = useState('laboratory')
  const [encounterProcedureInstructions, setEncounterProcedureInstructions] = useState('Collect fasting sample.')
  const [encounterProcedureStatus, setEncounterProcedureStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>(
    'idle',
  )

  useEffect(() => {
    setCreatePatientId(patientId)
  }, [patientId])

  useEffect(() => {
    if (!encounterDetail) {
      setSummaryReason('')
      setSummarySensitivity('')
      setSummaryReferralSource('')
      setSummaryExternalId('')
      setSummaryPosCode('')
      setSummaryBillingNote('')
      setEncounterDocumentReviewStatus('idle')
      setEncounterDocumentMetadataStatus('idle')
      setEncounterDocumentMoveStatus('idle')
      setEncounterDocumentContentStatus('idle')
      setEncounterDocumentArchiveStatus('idle')
      setEncounterExternalLinkStatus('idle')
      return
    }

    setSummaryReason(encounterDetail.reason ?? '')
    setSummarySensitivity(encounterDetail.sensitivity ?? '')
    setSummaryReferralSource(encounterDetail.referralSource ?? '')
    setSummaryExternalId(encounterDetail.externalId ?? '')
    setSummaryPosCode(encounterDetail.posCode?.toString() ?? '')
    setSummaryBillingNote(encounterDetail.billingNote ?? '')
    setVitalsDateTime(`${encounterDetail.date}T10:05`)
    setSoapDateTime(`${encounterDetail.date}T10:10`)
    setSignatureSignedAt(`${encounterDetail.date}T10:20`)
    setEncounterDocumentDate(encounterDetail.date)
    setEncounterBinaryDocumentDate(encounterDetail.date)
    setEncounterExternalLinkDate(encounterDetail.date)
    setEncounterDocumentReviewStatus('idle')
    setEncounterDocumentMetadataStatus('idle')
    setEncounterDocumentMoveStatus('idle')
    setEncounterDocumentContentStatus('idle')
    setEncounterDocumentArchiveStatus('idle')
    setEncounterExternalLinkStatus('idle')
    setFeeSheetDate(encounterDetail.date)
    setEncounterProcedureDate(encounterDetail.date)
  }, [encounterDetail])

  function handleFeeSheetCodeTypeChange(event: ChangeEvent<HTMLSelectElement>) {
    const nextCodeType = event.target.value === 'ICD10' ? 'ICD10' : 'CPT4'
    setFeeSheetCodeType(nextCodeType)
    setFeeSheetStatus('idle')

    if (nextCodeType === 'ICD10') {
      setFeeSheetCode('R73.03')
      setFeeSheetModifier('')
      setFeeSheetDescription('Prediabetes')
      setFeeSheetFee('0.00')
      setFeeSheetUnits('1')
      setFeeSheetJustify('R73.03')
    } else {
      setFeeSheetCode('99499')
      setFeeSheetDescription('Encounter fee sheet service')
      setFeeSheetFee('42.00')
      setFeeSheetUnits('1')
      setFeeSheetJustify('E78.5')
    }
  }

  async function handleEncounterLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setEncounterLoginStatus('checking')
    setEncounterLoginMessage(null)

    try {
      const result = await login({ username: encounterLoginUsername, password: encounterLoginPassword })
      if (result.authenticated && result.sessionId) {
        onEncounterSessionActive(result.sessionId)
        setEncounterLoginStatus('authenticated')
        setEncounterLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setEncounterLoginStatus('rejected')
        setEncounterLoginMessage(result.failureReason ?? 'Encounter access was rejected.')
      }
    } catch (loginError) {
      setEncounterLoginStatus('error')
      setEncounterLoginMessage(loginError instanceof Error ? loginError.message : 'Encounter access check failed')
    }
  }

  async function handleCreateSubmit(event: FormEvent) {
    event.preventDefault()
    if (encounterLocked) {
      setCreateStatus('error')
      return
    }

    setCreateStatus('saving')

    try {
      await onCreateEncounter({
        patientId: createPatientId,
        providerId: numberOrNull(createProviderId),
        dateTime: createDateTime,
        reason: createReason,
        facilityId: numberOrNull(createFacilityId),
        billingFacilityId: numberOrNull(createBillingFacilityId),
        sensitivity: createSensitivity,
        referralSource: createReferralSource,
        externalId: createExternalId,
        posCode: numberOrNull(createPosCode),
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
        sensitivity: summarySensitivity,
        referralSource: summaryReferralSource,
        externalId: summaryExternalId,
        posCode: numberOrNull(summaryPosCode),
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

  async function handleSignatureSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setSignatureStatus('saving')
    try {
      await onSignEncounter(encounterDetail, {
        signerUsername: signatureSigner,
        signedAt: signatureSignedAt,
        isLock: signatureMode === 'locked',
        amendment: signatureAmendment,
      })
      setSignatureStatus('saved')
    } catch {
      setSignatureStatus('error')
    }
  }

  async function handleSignatureDelete(signature: EncounterSignatureItem) {
    if (!encounterDetail) {
      return
    }

    setSignatureStatus('saving')
    try {
      await onDeleteEncounterSignature(encounterDetail, signature)
      setSignatureStatus('saved')
    } catch {
      setSignatureStatus('error')
    }
  }

  async function handleEncounterDocumentSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentStatus('saving')
    try {
      await onCreateEncounterDocument(encounterDetail, {
        categoryId: Number(encounterDocumentCategoryId),
        name: encounterDocumentName,
        docDate: encounterDocumentDate,
        content: encounterDocumentContent,
        notes: encounterDocumentNotes,
      })
      setEncounterDocumentStatus('saved')
    } catch {
      setEncounterDocumentStatus('error')
    }
  }

  async function handleEncounterBinaryFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      setEncounterBinaryFileName('')
      setEncounterBinaryMimeType('')
      setEncounterBinaryContentBase64('')
      setEncounterBinaryFileMessage('No file selected')
      return
    }

    const contentBase64 = await readFileAsBase64(file)
    setEncounterBinaryFileName(file.name)
    setEncounterBinaryMimeType(file.type || 'application/octet-stream')
    setEncounterBinaryContentBase64(contentBase64)
    setEncounterBinaryFileMessage(`${file.name} selected (${formatBytes(file.size)})`)
    if (encounterBinaryDocumentName === 'Encounter binary attachment') {
      setEncounterBinaryDocumentName(file.name)
    }
  }

  async function handleEncounterBinaryDocumentSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    if (!encounterBinaryContentBase64 || !encounterBinaryFileName || !encounterBinaryMimeType) {
      setEncounterBinaryFileMessage('Choose a file to upload')
      setEncounterBinaryDocumentStatus('error')
      return
    }

    setEncounterBinaryDocumentStatus('saving')
    try {
      await onCreateEncounterBinaryDocument(encounterDetail, {
        categoryId: Number(encounterBinaryDocumentCategoryId),
        name: encounterBinaryDocumentName,
        docDate: encounterBinaryDocumentDate,
        fileName: encounterBinaryFileName,
        mimetype: encounterBinaryMimeType,
        contentBase64: encounterBinaryContentBase64,
        notes: encounterBinaryDocumentNotes,
      })
      setEncounterBinaryDocumentStatus('saved')
    } catch {
      setEncounterBinaryDocumentStatus('error')
    }
  }

  async function handleEncounterExternalLinkSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setEncounterExternalLinkStatus('saving')
    try {
      await onCreateEncounterExternalLinkDocument(encounterDetail, {
        categoryId: Number(encounterExternalLinkCategoryId),
        name: encounterExternalLinkName,
        docDate: encounterExternalLinkDate,
        url: encounterExternalLinkUrl,
        notes: encounterExternalLinkNotes,
      })
      setEncounterExternalLinkStatus('saved')
    } catch {
      setEncounterExternalLinkStatus('error')
    }
  }

  async function handleEncounterDocumentMetadataUpdate(
    document: EncounterDocumentAttachment,
    input: PatientDocumentMetadataUpdateInput,
  ) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentMetadataStatus('saving')
    try {
      await onUpdateEncounterDocumentMetadata(encounterDetail, document, input)
      setEncounterDocumentMetadataStatus('saved')
    } catch {
      setEncounterDocumentMetadataStatus('error')
    }
  }

  async function handleEncounterDocumentMove(document: EncounterDocumentAttachment, targetEncounter: number) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentMoveStatus('saving')
    try {
      await onMoveEncounterDocument(encounterDetail, document, targetEncounter)
      setEncounterDocumentMoveStatus('moved')
    } catch {
      setEncounterDocumentMoveStatus('error')
    }
  }

  async function handleEncounterDocumentContentReplace(
    document: EncounterDocumentAttachment,
    input: PatientDocumentContentReplaceInput,
  ) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentContentStatus('saving')
    try {
      await onReplaceEncounterDocumentContent(encounterDetail, document, input)
      setEncounterDocumentContentStatus('saved')
    } catch {
      setEncounterDocumentContentStatus('error')
    }
  }

  async function handleEncounterDocumentBinaryContentReplace(
    document: EncounterDocumentAttachment,
    input: PatientDocumentBinaryContentReplaceInput,
  ) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentContentStatus('saving')
    try {
      await onReplaceEncounterDocumentBinaryContent(encounterDetail, document, input)
      setEncounterDocumentContentStatus('saved')
    } catch {
      setEncounterDocumentContentStatus('error')
    }
  }

  async function handleEncounterDocumentArchive(document: EncounterDocumentAttachment) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentArchiveStatus('saving')
    try {
      await onArchiveEncounterDocument(encounterDetail, document)
      setEncounterDocumentArchiveStatus('archived')
    } catch {
      setEncounterDocumentArchiveStatus('error')
    }
  }

  async function handleEncounterDocumentRestore(document: EncounterDocumentAttachment) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentArchiveStatus('saving')
    try {
      await onRestoreEncounterDocument(encounterDetail, document)
      setEncounterDocumentArchiveStatus('restored')
    } catch {
      setEncounterDocumentArchiveStatus('error')
    }
  }

  async function handleEncounterDocumentSign(document: EncounterDocumentAttachment) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentReviewStatus('saving')
    try {
      await onSignEncounterDocument(encounterDetail, document)
      setEncounterDocumentReviewStatus('signed')
    } catch {
      setEncounterDocumentReviewStatus('error')
    }
  }

  async function handleEncounterDocumentDeny(document: EncounterDocumentAttachment) {
    if (!encounterDetail) {
      return
    }

    setEncounterDocumentReviewStatus('saving')
    try {
      await onDenyEncounterDocument(encounterDetail, document)
      setEncounterDocumentReviewStatus('denied')
    } catch {
      setEncounterDocumentReviewStatus('error')
    }
  }

  async function handleEncounterDocumentDownload(document: EncounterDocumentAttachment) {
    if (!sessionId) {
      setEncounterDocumentContentStatus('error')
      return
    }

    try {
      const blob = await downloadPatientDocument(document.id, sessionId)
      const url = URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = document.fileName || document.name
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch {
      setEncounterDocumentContentStatus('error')
    }
  }

  async function handleFeeSheetSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setFeeSheetStatus('saving')
    try {
      await onCreateFeeSheetLine(encounterDetail, {
        patientId: encounterDetail.pubpid,
        encounter: encounterDetail.encounter,
        billingDate: feeSheetDate,
        codeType: feeSheetCodeType,
        code: feeSheetCode,
        modifier: feeSheetCodeType === 'ICD10' ? '' : feeSheetModifier,
        codeText: feeSheetDescription,
        fee: Number(feeSheetFee),
        units: Number(feeSheetUnits),
        justify: feeSheetJustify,
      })
      setFeeSheetStatus('saved')
    } catch {
      setFeeSheetStatus('error')
    }
  }

  async function handleEncounterProcedureSubmit(event: FormEvent) {
    event.preventDefault()
    if (!encounterDetail) {
      return
    }

    setEncounterProcedureStatus('saving')
    try {
      await onCreateProcedureOrder(encounterDetail, {
        patientId: encounterDetail.pubpid,
        encounterId: encounterDetail.encounter,
        dateOrdered: encounterProcedureDate,
        priority: encounterProcedurePriority,
        status: encounterProcedureStatusValue,
        procedureCode: encounterProcedureCode,
        procedureName: encounterProcedureName,
        procedureType: encounterProcedureType,
        diagnosis: encounterProcedureDiagnosis,
        instructions: encounterProcedureInstructions,
      })
      setEncounterProcedureStatus('saved')
    } catch {
      setEncounterProcedureStatus('error')
    }
  }

  const attachedDocuments = encounterDetail?.documents ?? []
  const archivedAttachedDocuments = attachedDocuments.filter((document) => document.deleted !== 0)
  const activeAttachedDocumentCount = attachedDocuments.length - archivedAttachedDocuments.length
  const encounterSignatures = encounterDetail?.signatures ?? []
  const encounterAmendmentHistory = encounterDetail?.amendmentHistory ?? []
  const encounterBillingLines = encounterDetail?.billingLines ?? []
  const encounterBillingTotal = encounterBillingLines.reduce((sum, line) => sum + (line.fee ?? 0), 0)
  const encounterClaims = encounterDetail?.claims ?? []
  const encounterProcedureOrders = encounterDetail?.procedureOrders ?? []
  const encounterProcedureResultCount = countProcedureResults(encounterProcedureOrders)
  const encounterDiagnosisCodes = encounterDetail?.diagnosisCodes ?? []
  const diagnosisBillingLinkCount = encounterDiagnosisCodes.reduce((sum, diagnosis) => sum + diagnosis.billingLineCount, 0)
  const diagnosisProcedureOrderLinkCount = encounterDiagnosisCodes.reduce(
    (sum, diagnosis) => sum + diagnosis.procedureOrderCount,
    0,
  )

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Encounter search">
        {(!sessionId || searchStatus === 'error') && (
          <form className="mutation-form" aria-label="Encounter access" onSubmit={handleEncounterLogin}>
            <div className="panel-heading">
              <ShieldCheck size={17} />
              <h3>Encounter Access</h3>
            </div>
            <p className="access-copy">Sign in to load encounters.</p>
            <label>
              Username
              <input value={encounterLoginUsername} onChange={(event) => setEncounterLoginUsername(event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={encounterLoginPassword}
                onChange={(event) => setEncounterLoginPassword(event.target.value)}
              />
            </label>
            <button type="submit" disabled={encounterLoginStatus === 'checking'}>
              <LogIn size={15} />
              {encounterLoginStatus === 'checking' ? 'Checking' : 'Verify Encounter Access'}
            </button>
            {encounterLoginMessage && (
              <div className={encounterLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {encounterLoginMessage}
              </div>
            )}
          </form>
        )}

        {sessionId && encounterLoginMessage && encounterLoginStatus === 'authenticated' && searchStatus !== 'error' && (
          <div className="status-banner">{encounterLoginMessage}</div>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Encounter patient ID"
              placeholder="MOD-PAT-0001"
              disabled={encounterLocked}
            />
          </label>
          <label className="filter-field">
            <span>From</span>
            <input
              value={fromDate}
              onChange={(event) => onFromDateChange(event.target.value)}
              aria-label="Encounter from date"
              type="date"
              disabled={encounterLocked}
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
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Sensitivity</span>
                <select
                  value={createSensitivity}
                  onChange={(event) => setCreateSensitivity(event.target.value)}
                  aria-label="New encounter sensitivity"
                >
                  <option value="">None</option>
                  <option value="normal">Normal</option>
                  <option value="high">High</option>
                </select>
              </label>
              <label className="filter-field">
                <span>Referral Source</span>
                <input
                  value={createReferralSource}
                  onChange={(event) => setCreateReferralSource(event.target.value)}
                  aria-label="New encounter referral source"
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>External ID</span>
                <input
                  value={createExternalId}
                  onChange={(event) => setCreateExternalId(event.target.value)}
                  aria-label="New encounter external ID"
                />
              </label>
              <label className="filter-field">
                <span>POS Code</span>
                <input
                  value={createPosCode}
                  onChange={(event) => setCreatePosCode(event.target.value)}
                  aria-label="New encounter place of service"
                  inputMode="numeric"
                />
              </label>
            </div>
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
            <button className="icon-text-button primary" type="submit" disabled={encounterLocked || createStatus === 'saving'}>
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
                <Field label="Sensitivity" value={formatEncounterSensitivity(encounterDetail.sensitivity)} />
                <Field label="Referral source" value={encounterDetail.referralSource} />
                <Field label="External ID" value={encounterDetail.externalId} />
                <Field label="POS code" value={encounterDetail.posCode} />
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

            <section className="info-panel encounter-signature-panel" aria-label="Encounter sign-off">
              <div className="panel-heading">
                <ShieldCheck size={17} />
                <h3>Sign-Off</h3>
                <span className="panel-count-pill">{formatSignatureCount(encounterSignatures.length)}</span>
                {encounterSignatures.length > 0 && <span className="panel-count-pill">Signed</span>}
              </div>
              <form className="encounter-signature-form" onSubmit={handleSignatureSubmit}>
                <label className="filter-field">
                  <span>Signer</span>
                  <input
                    value={signatureSigner}
                    onChange={(event) => setSignatureSigner(event.target.value)}
                    aria-label="Encounter sign-off signer"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Signed At</span>
                  <input
                    value={signatureSignedAt}
                    onChange={(event) => setSignatureSignedAt(event.target.value)}
                    aria-label="Encounter sign-off signed at"
                    type="datetime-local"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Mode</span>
                  <select
                    value={signatureMode}
                    onChange={(event) => setSignatureMode(event.target.value === 'locked' ? 'locked' : 'signed')}
                    aria-label="Encounter sign-off mode"
                  >
                    <option value="signed">Signed</option>
                    <option value="locked">Locked</option>
                  </select>
                </label>
                <label className="filter-field encounter-signature-note-field">
                  <span>Note</span>
                  <input
                    value={signatureAmendment}
                    onChange={(event) => setSignatureAmendment(event.target.value)}
                    aria-label="Encounter sign-off note"
                  />
                </label>
                <button className="icon-text-button primary" type="submit" disabled={signatureStatus === 'saving'}>
                  <ShieldCheck size={16} />
                  <span>{signatureStatus === 'saving' ? 'Saving' : 'Sign'}</span>
                </button>
              </form>
              {signatureStatus === 'saved' && <span className="save-note">Saved</span>}
              {signatureStatus === 'error' && <span className="save-note error">Action failed</span>}
              <div className="encounter-signature-list">
                {encounterSignatures.map((signature) => (
                  <EncounterSignatureCard
                    key={signature.id}
                    signature={signature}
                    disabled={signatureStatus === 'saving'}
                    onDelete={() => handleSignatureDelete(signature)}
                  />
                ))}
                {encounterSignatures.length === 0 && (
                  <div className="timeline-placeholder">No sign-off recorded for this encounter</div>
                )}
              </div>
            </section>

            <section className="info-panel encounter-amendment-panel" aria-label="Encounter amendment history">
              <div className="panel-heading">
                <FileClock size={17} />
                <h3>Amendment History</h3>
                <span className="panel-count-pill">{formatAmendmentCount(encounterAmendmentHistory.length)}</span>
                {encounterAmendmentHistory.some((item) => item.isLock) && (
                  <span className="panel-count-pill">Locked</span>
                )}
              </div>
              <div className="encounter-amendment-list">
                {encounterAmendmentHistory.map((item) => (
                  <EncounterAmendmentHistoryCard key={item.signatureId} item={item} />
                ))}
                {encounterAmendmentHistory.length === 0 && (
                  <div className="timeline-placeholder">No amendments recorded for this encounter</div>
                )}
              </div>
            </section>

            <section className="info-panel encounter-diagnosis-panel" aria-label="Encounter diagnosis coding linkage">
              <div className="panel-heading">
                <ClipboardList size={17} />
                <h3>Diagnosis Coding</h3>
                <span className="panel-count-pill">{encounterDiagnosisCodes.length}</span>
                <span className="panel-count-pill">{diagnosisBillingLinkCount} billing links</span>
                <span className="panel-count-pill">{diagnosisProcedureOrderLinkCount} order links</span>
              </div>
              <div className="encounter-diagnosis-list">
                {encounterDiagnosisCodes.map((diagnosis) => (
                  <EncounterDiagnosisCodeCard key={diagnosis.code} diagnosis={diagnosis} />
                ))}
                {encounterDiagnosisCodes.length === 0 && (
                  <div className="timeline-placeholder">No diagnosis coding linked to this encounter</div>
                )}
              </div>
            </section>

            <section className="info-panel encounter-billing-panel" aria-label="Encounter billing linkage">
              <div className="panel-heading">
                <WalletCards size={17} />
                <h3>Fee Sheet Linkage</h3>
                <span className="panel-count-pill">{encounterBillingLines.length}</span>
                <span className="billing-total-pill">{formatCurrency(encounterBillingTotal)}</span>
              </div>
              <div className="encounter-billing-list">
                {encounterBillingLines.map((line) => (
                  <EncounterBillingLineCard key={line.id} line={line} />
                ))}
                {encounterBillingLines.length === 0 && (
                  <div className="timeline-placeholder">No active fee-sheet lines linked to this encounter</div>
                )}
              </div>
            </section>

            <form className="appointment-mutation-panel encounter-fee-sheet-panel" onSubmit={handleFeeSheetSubmit} aria-label="Encounter fee sheet entry">
              <div className="panel-heading compact-heading">
                <WalletCards size={16} />
                <h3>Fee Sheet Entry</h3>
              </div>
              <div className="mutation-grid encounter-fee-sheet-grid">
                <label className="filter-field">
                  <span>Type</span>
                  <select
                    value={feeSheetCodeType}
                    onChange={handleFeeSheetCodeTypeChange}
                    aria-label="Encounter fee sheet code type"
                  >
                    <option value="CPT4">CPT4</option>
                    <option value="ICD10">ICD10</option>
                  </select>
                </label>
                <label className="filter-field">
                  <span>Date</span>
                  <input
                    value={feeSheetDate}
                    onChange={(event) => setFeeSheetDate(event.target.value)}
                    aria-label="Encounter fee sheet date"
                    type="date"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Code</span>
                  <input
                    value={feeSheetCode}
                    onChange={(event) => setFeeSheetCode(event.target.value)}
                    aria-label="Encounter fee sheet code"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Modifier</span>
                  <input
                    value={feeSheetModifier}
                    onChange={(event) => setFeeSheetModifier(event.target.value)}
                    aria-label="Encounter fee sheet modifier"
                    disabled={feeSheetCodeType === 'ICD10'}
                    placeholder="25"
                  />
                </label>
                <label className="filter-field fee-sheet-description-field">
                  <span>Description</span>
                  <input
                    value={feeSheetDescription}
                    onChange={(event) => setFeeSheetDescription(event.target.value)}
                    aria-label="Encounter fee sheet description"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Fee</span>
                  <input
                    value={feeSheetFee}
                    onChange={(event) => setFeeSheetFee(event.target.value)}
                    aria-label="Encounter fee sheet fee"
                    inputMode="decimal"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Units</span>
                  <input
                    value={feeSheetUnits}
                    onChange={(event) => setFeeSheetUnits(event.target.value)}
                    aria-label="Encounter fee sheet units"
                    inputMode="numeric"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Justify</span>
                  <input
                    value={feeSheetJustify}
                    onChange={(event) => setFeeSheetJustify(event.target.value)}
                    aria-label="Encounter fee sheet justification"
                    required
                  />
                </label>
              </div>
              <div className="detail-actions">
                <button className="icon-text-button primary" type="submit" disabled={feeSheetStatus === 'saving'}>
                  <Check size={16} />
                  <span>{feeSheetStatus === 'saving' ? 'Saving' : 'Add Line'}</span>
                </button>
                {feeSheetStatus === 'saved' && <span className="save-note">Saved</span>}
                {feeSheetStatus === 'error' && <span className="save-note error">Action failed</span>}
              </div>
            </form>

            <section className="info-panel encounter-claim-panel" aria-label="Encounter claim linkage">
              <div className="panel-heading">
                <FileCheck2 size={17} />
                <h3>Claim Linkage</h3>
                <span className="panel-count-pill">{encounterClaims.length}</span>
              </div>
              <div className="encounter-claim-list">
                {encounterClaims.map((claim) => (
                  <EncounterClaimCard key={claim.id} claim={claim} />
                ))}
                {encounterClaims.length === 0 && (
                  <div className="timeline-placeholder">No claim status linked to this encounter</div>
                )}
              </div>
            </section>

            <section className="info-panel encounter-procedure-panel" aria-label="Encounter procedure order linkage">
              <div className="panel-heading">
                <FlaskConical size={17} />
                <h3>Procedure Orders</h3>
                <span className="panel-count-pill">{encounterProcedureOrders.length}</span>
                <span className="panel-count-pill">{encounterProcedureResultCount} results</span>
              </div>
              <div className="encounter-procedure-list">
                {encounterProcedureOrders.map((order) => (
                  <EncounterProcedureOrderCard
                    key={order.id}
                    order={order}
                    onCreateResultSet={(input) => onCreateProcedureResultSet(encounterDetail, input)}
                    onUpdateResult={(result, input) => onUpdateProcedureResult(encounterDetail, result, input)}
                  />
                ))}
                {encounterProcedureOrders.length === 0 && (
                  <div className="timeline-placeholder">No procedure orders linked to this encounter</div>
                )}
              </div>
            </section>

            <form className="appointment-mutation-panel encounter-procedure-entry-panel" onSubmit={handleEncounterProcedureSubmit} aria-label="Encounter procedure order entry">
              <div className="panel-heading compact-heading">
                <FlaskConical size={16} />
                <h3>Procedure Order Entry</h3>
              </div>
              <div className="mutation-grid encounter-procedure-entry-grid">
                <label className="filter-field">
                  <span>Date</span>
                  <input
                    value={encounterProcedureDate}
                    onChange={(event) => setEncounterProcedureDate(event.target.value)}
                    aria-label="Encounter procedure order date"
                    type="date"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Code</span>
                  <input
                    value={encounterProcedureCode}
                    onChange={(event) => setEncounterProcedureCode(event.target.value)}
                    aria-label="Encounter procedure order code"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Priority</span>
                  <select
                    value={encounterProcedurePriority}
                    onChange={(event) => setEncounterProcedurePriority(event.target.value)}
                    aria-label="Encounter procedure order priority"
                  >
                    <option value="routine">Routine</option>
                    <option value="urgent">Urgent</option>
                    <option value="stat">STAT</option>
                  </select>
                </label>
                <label className="filter-field">
                  <span>Status</span>
                  <select
                    value={encounterProcedureStatusValue}
                    onChange={(event) => setEncounterProcedureStatusValue(event.target.value)}
                    aria-label="Encounter procedure order status"
                  >
                    <option value="pending">Pending</option>
                    <option value="scheduled">Scheduled</option>
                    <option value="complete">Complete</option>
                  </select>
                </label>
                <label className="filter-field procedure-order-name-field">
                  <span>Name</span>
                  <input
                    value={encounterProcedureName}
                    onChange={(event) => setEncounterProcedureName(event.target.value)}
                    aria-label="Encounter procedure order name"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Diagnosis</span>
                  <input
                    value={encounterProcedureDiagnosis}
                    onChange={(event) => setEncounterProcedureDiagnosis(event.target.value)}
                    aria-label="Encounter procedure order diagnosis"
                    required
                  />
                </label>
                <label className="filter-field">
                  <span>Type</span>
                  <input
                    value={encounterProcedureType}
                    onChange={(event) => setEncounterProcedureType(event.target.value)}
                    aria-label="Encounter procedure order type"
                    required
                  />
                </label>
                <label className="filter-field procedure-order-instructions-field">
                  <span>Instructions</span>
                  <input
                    value={encounterProcedureInstructions}
                    onChange={(event) => setEncounterProcedureInstructions(event.target.value)}
                    aria-label="Encounter procedure order instructions"
                  />
                </label>
              </div>
              <div className="detail-actions">
                <button className="icon-text-button primary" type="submit" disabled={encounterProcedureStatus === 'saving'}>
                  <Check size={16} />
                  <span>{encounterProcedureStatus === 'saving' ? 'Saving' : 'Add Order'}</span>
                </button>
                {encounterProcedureStatus === 'saved' && <span className="save-note">Saved</span>}
                {encounterProcedureStatus === 'error' && <span className="save-note error">Action failed</span>}
              </div>
            </form>

            <section className="info-panel encounter-documents-panel" aria-label="Encounter attached documents">
              <div className="panel-heading">
                <FolderOpen size={17} />
                <h3>Attached Documents</h3>
                <span className="panel-count-pill">{activeAttachedDocumentCount}</span>
              </div>
              <label className="inline-toggle">
                <input
                  type="checkbox"
                  checked={includeArchivedDocuments}
                  onChange={(event) => onIncludeArchivedDocumentsChange(event.target.checked)}
                  aria-label="Show archived attached documents"
                />
                <span>Show archived attached documents</span>
                {archivedAttachedDocuments.length > 0 && (
                  <span className="status-tag danger">{archivedAttachedDocuments.length} archived</span>
                )}
              </label>
              <form className="encounter-document-form" onSubmit={handleEncounterDocumentSubmit} aria-label="Encounter document upload">
                <label className="filter-field">
                  <span>Category</span>
                  <select
                    value={encounterDocumentCategoryId}
                    onChange={(event) => setEncounterDocumentCategoryId(event.target.value)}
                    aria-label="Encounter document category"
                  >
                    <option value="3">Medical Record</option>
                    <option value="6">Advance Directive</option>
                    <option value="13">CCDA</option>
                  </select>
                </label>
                <label className="filter-field">
                  <span>Date</span>
                  <input
                    value={encounterDocumentDate}
                    onChange={(event) => setEncounterDocumentDate(event.target.value)}
                    aria-label="Encounter document date"
                    type="date"
                    required
                  />
                </label>
                <label className="filter-field encounter-document-name-field">
                  <span>Name</span>
                  <input
                    value={encounterDocumentName}
                    onChange={(event) => setEncounterDocumentName(event.target.value)}
                    aria-label="Encounter document name"
                    required
                  />
                </label>
                <label className="filter-field encounter-document-note-field">
                  <span>Notes</span>
                  <input
                    value={encounterDocumentNotes}
                    onChange={(event) => setEncounterDocumentNotes(event.target.value)}
                    aria-label="Encounter document notes"
                  />
                </label>
                <label className="filter-field encounter-document-content-field">
                  <span>Content</span>
                  <textarea
                    value={encounterDocumentContent}
                    onChange={(event) => setEncounterDocumentContent(event.target.value)}
                    aria-label="Encounter document content"
                    rows={2}
                    required
                  />
                </label>
                <button className="icon-text-button primary" type="submit" disabled={encounterDocumentStatus === 'saving'}>
                  <Upload size={16} />
                  <span>{encounterDocumentStatus === 'saving' ? 'Saving' : 'Attach'}</span>
                </button>
                {encounterDocumentStatus === 'saved' && <span className="save-note">Saved</span>}
                {encounterDocumentStatus === 'error' && <span className="save-note error">Action failed</span>}
              </form>
              <form className="encounter-document-form encounter-binary-document-form" onSubmit={handleEncounterBinaryDocumentSubmit} aria-label="Encounter binary document upload">
                <label className="filter-field">
                  <span>Category</span>
                  <select
                    value={encounterBinaryDocumentCategoryId}
                    onChange={(event) => setEncounterBinaryDocumentCategoryId(event.target.value)}
                    aria-label="Encounter binary document category"
                  >
                    <option value="3">Medical Record</option>
                    <option value="6">Advance Directive</option>
                    <option value="13">CCDA</option>
                  </select>
                </label>
                <label className="filter-field">
                  <span>Date</span>
                  <input
                    value={encounterBinaryDocumentDate}
                    onChange={(event) => setEncounterBinaryDocumentDate(event.target.value)}
                    aria-label="Encounter binary document date"
                    type="date"
                    required
                  />
                </label>
                <label className="filter-field encounter-document-name-field">
                  <span>Name</span>
                  <input
                    value={encounterBinaryDocumentName}
                    onChange={(event) => setEncounterBinaryDocumentName(event.target.value)}
                    aria-label="Encounter binary document name"
                    required
                  />
                </label>
                <label className="filter-field encounter-binary-file-field">
                  <span>File</span>
                  <input type="file" onChange={handleEncounterBinaryFileChange} aria-label="Encounter binary document file" required />
                </label>
                <label className="filter-field encounter-document-note-field">
                  <span>Notes</span>
                  <input
                    value={encounterBinaryDocumentNotes}
                    onChange={(event) => setEncounterBinaryDocumentNotes(event.target.value)}
                    aria-label="Encounter binary document notes"
                  />
                </label>
                <button className="icon-text-button primary" type="submit" disabled={encounterBinaryDocumentStatus === 'saving'}>
                  <Upload size={16} />
                  <span>{encounterBinaryDocumentStatus === 'saving' ? 'Uploading' : 'Upload'}</span>
                </button>
                <span className={encounterBinaryDocumentStatus === 'error' ? 'save-note error' : 'save-note'}>
                  {encounterBinaryDocumentStatus === 'saved' ? 'Uploaded' : encounterBinaryFileMessage}
                </span>
              </form>
              <form className="encounter-document-form encounter-external-link-form" onSubmit={handleEncounterExternalLinkSubmit} aria-label="Encounter external-link document attach">
                <label className="filter-field">
                  <span>Category</span>
                  <select
                    value={encounterExternalLinkCategoryId}
                    onChange={(event) => setEncounterExternalLinkCategoryId(event.target.value)}
                    aria-label="Encounter external-link document category"
                  >
                    <option value="3">Medical Record</option>
                    <option value="6">Advance Directive</option>
                    <option value="13">CCDA</option>
                  </select>
                </label>
                <label className="filter-field">
                  <span>Date</span>
                  <input
                    value={encounterExternalLinkDate}
                    onChange={(event) => setEncounterExternalLinkDate(event.target.value)}
                    aria-label="Encounter external-link document date"
                    type="date"
                    required
                  />
                </label>
                <label className="filter-field encounter-document-name-field">
                  <span>Name</span>
                  <input
                    value={encounterExternalLinkName}
                    onChange={(event) => setEncounterExternalLinkName(event.target.value)}
                    aria-label="Encounter external-link document name"
                    required
                  />
                </label>
                <label className="filter-field encounter-external-link-url-field">
                  <span>URL</span>
                  <input
                    value={encounterExternalLinkUrl}
                    onChange={(event) => setEncounterExternalLinkUrl(event.target.value)}
                    aria-label="Encounter external-link document URL"
                    type="url"
                    required
                  />
                </label>
                <label className="filter-field encounter-document-note-field">
                  <span>Notes</span>
                  <input
                    value={encounterExternalLinkNotes}
                    onChange={(event) => setEncounterExternalLinkNotes(event.target.value)}
                    aria-label="Encounter external-link document notes"
                  />
                </label>
                <button className="icon-text-button primary" type="submit" disabled={encounterExternalLinkStatus === 'saving'}>
                  <ExternalLink size={16} />
                  <span>{encounterExternalLinkStatus === 'saving' ? 'Saving' : 'Attach Link'}</span>
                </button>
                {encounterExternalLinkStatus === 'saved' && <span className="save-note">Linked</span>}
                {encounterExternalLinkStatus === 'error' && <span className="save-note error">Action failed</span>}
              </form>
              <div className="encounter-documents-list">
                {attachedDocuments.map((document) => (
                  <EncounterDocumentAttachmentCard
                    key={document.id}
                    document={document}
                    encounter={encounterDetail.encounter}
                    disabled={
                      encounterDocumentReviewStatus === 'saving'
                      || encounterDocumentMetadataStatus === 'saving'
                      || encounterDocumentMoveStatus === 'saving'
                      || encounterDocumentContentStatus === 'saving'
                      || encounterDocumentArchiveStatus === 'saving'
                      || encounterExternalLinkStatus === 'saving'
                    }
                    onUpdateMetadata={handleEncounterDocumentMetadataUpdate}
                    onMove={handleEncounterDocumentMove}
                    onReplaceContent={handleEncounterDocumentContentReplace}
                    onReplaceBinaryContent={handleEncounterDocumentBinaryContentReplace}
                    onArchive={handleEncounterDocumentArchive}
                    onRestore={handleEncounterDocumentRestore}
                    onSign={handleEncounterDocumentSign}
                    onDeny={handleEncounterDocumentDeny}
                    onDownload={handleEncounterDocumentDownload}
                  />
                ))}
                {attachedDocuments.length === 0 && (
                  <div className="timeline-placeholder">No documents linked to this encounter</div>
                )}
              </div>
              <span
                className={
                  encounterDocumentReviewStatus === 'error'
                  || encounterDocumentMetadataStatus === 'error'
                  || encounterDocumentMoveStatus === 'error'
                  || encounterDocumentContentStatus === 'error'
                  || encounterDocumentArchiveStatus === 'error'
                    ? 'save-note error'
                    : 'save-note'
                }
              >
                {encounterDocumentReviewStatus === 'signed'
                  ? 'Document signed'
                  : encounterDocumentReviewStatus === 'denied'
                    ? 'Document denied'
                  : encounterDocumentReviewStatus === 'saving'
                    ? 'Reviewing document'
                  : encounterDocumentReviewStatus === 'error'
                    ? 'Document review failed'
                  : encounterDocumentMetadataStatus === 'saved'
                    ? 'Document metadata saved'
                  : encounterDocumentMetadataStatus === 'saving'
                    ? 'Saving document metadata'
                  : encounterDocumentMetadataStatus === 'error'
                    ? 'Document metadata update failed'
                  : encounterDocumentMoveStatus === 'moved'
                    ? 'Document moved'
                  : encounterDocumentMoveStatus === 'saving'
                    ? 'Moving document'
                  : encounterDocumentMoveStatus === 'error'
                    ? 'Document move failed'
                  : encounterDocumentContentStatus === 'saved'
                    ? 'Document content saved'
                  : encounterDocumentContentStatus === 'saving'
                    ? 'Saving document content'
                  : encounterDocumentContentStatus === 'error'
                    ? 'Document content replacement failed'
                  : encounterDocumentArchiveStatus === 'archived'
                    ? 'Document archived'
                  : encounterDocumentArchiveStatus === 'restored'
                    ? 'Document restored'
                  : encounterDocumentArchiveStatus === 'saving'
                    ? 'Saving document archive state'
                  : encounterDocumentArchiveStatus === 'error'
                    ? 'Document archive state failed'
                    : ''}
              </span>
            </section>

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
                <div className="mutation-grid two-column">
                  <label className="filter-field">
                    <span>Sensitivity</span>
                    <select
                      value={summarySensitivity}
                      onChange={(event) => setSummarySensitivity(event.target.value)}
                      aria-label="Encounter sensitivity"
                    >
                      <option value="">None</option>
                      <option value="normal">Normal</option>
                      <option value="high">High</option>
                    </select>
                  </label>
                  <label className="filter-field">
                    <span>Referral Source</span>
                    <input
                      value={summaryReferralSource}
                      onChange={(event) => setSummaryReferralSource(event.target.value)}
                      aria-label="Encounter referral source"
                    />
                  </label>
                </div>
                <div className="mutation-grid two-column">
                  <label className="filter-field">
                    <span>External ID</span>
                    <input
                      value={summaryExternalId}
                      onChange={(event) => setSummaryExternalId(event.target.value)}
                      aria-label="Encounter external ID"
                    />
                  </label>
                  <label className="filter-field">
                    <span>POS Code</span>
                    <input
                      value={summaryPosCode}
                      onChange={(event) => setSummaryPosCode(event.target.value)}
                      aria-label="Encounter place of service"
                      inputMode="numeric"
                    />
                  </label>
                </div>
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

function EncounterDiagnosisCodeCard({ diagnosis }: { diagnosis: EncounterDiagnosisCode }) {
  const sources = diagnosis.sources.length > 0 ? diagnosis.sources.join(', ') : 'No source recorded'
  const supportingCodes =
    diagnosis.supportingBillingCodes.length > 0 ? diagnosis.supportingBillingCodes.join(', ') : 'No fee-sheet support'

  return (
    <article className="encounter-diagnosis-card">
      <div className="diagnosis-line-main">
        <div>
          <strong>{diagnosis.code}</strong>
          <span>{diagnosis.description || 'No diagnosis description recorded'}</span>
        </div>
        <span className="status-tag">{diagnosis.sources.length} sources</span>
      </div>
      <div className="document-meta-grid encounter-diagnosis-meta">
        <span>{diagnosis.billingLineCount} billing lines</span>
        <span>{diagnosis.procedureOrderCount} procedure orders</span>
        <span>{supportingCodes}</span>
        <span>{sources}</span>
      </div>
    </article>
  )
}

function EncounterSignatureCard({
  signature,
  disabled,
  onDelete,
}: {
  signature: EncounterSignatureItem
  disabled: boolean
  onDelete: () => void
}) {
  return (
    <article className="encounter-signature-card">
      <div>
        <strong>{signature.isLock ? 'Locked' : 'Signed'}</strong>
        <span>{signature.signerUsername} / {signature.signedAt}</span>
        {signature.amendment && <p>{signature.amendment}</p>}
        <code>{signature.hash.slice(0, 12)}</code>
      </div>
      <button
        className="icon-button danger"
        type="button"
        aria-label={`Remove encounter signature ${signature.id}`}
        onClick={onDelete}
        disabled={disabled}
      >
        <Trash2 size={15} />
      </button>
    </article>
  )
}

function EncounterAmendmentHistoryCard({ item }: { item: EncounterAmendmentHistoryItem }) {
  return (
    <article className="encounter-amendment-card">
      <div>
        <strong>{item.isLock ? 'Locked amendment' : 'Signed amendment'}</strong>
        <span>{item.signerUsername} / {item.signedAt}</span>
        <p>{item.amendment}</p>
        <code>{item.hash.slice(0, 12)}</code>
      </div>
    </article>
  )
}

function EncounterClaimCard({ claim }: { claim: BillingClaimItem }) {
  const payerLabel = claim.payerName || `Payer ${claim.payerId}`
  const processLabel = claim.processFile || 'No process file'

  return (
    <article className="encounter-claim-card">
      <div className="claim-line-main">
        <div>
          <strong>{payerLabel}</strong>
          <span>Version {claim.version} / {claim.target || 'No target'}</span>
        </div>
        <span className="claim-status-pill">{claim.statusLabel}</span>
      </div>
      <div className="document-meta-grid encounter-claim-meta">
        <span>Bill {claim.billTime || 'Not billed'}</span>
        <span>Process {claim.processTime || 'Not processed'}</span>
        <span>Type {claim.payerType}</span>
        <span>Status {claim.status}</span>
      </div>
      <div className="document-footnote">
        <span>{processLabel}</span>
        <span>{claim.submittedClaim ? 'Submitted payload recorded' : claim.id}</span>
      </div>
    </article>
  )
}

function EncounterProcedureOrderCard({
  order,
  onCreateResultSet,
  onUpdateResult,
}: {
  order: ProcedureOrderItem
  onCreateResultSet: (input: EncounterProcedureResultSetInput) => Promise<unknown>
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
}) {
  const reportCount = order.reports.length
  const resultCount = countReportResults(order.reports)
  const specimenCount = order.specimens.length
  const defaultResultDate = order.orderDate || '2026-06-18'
  const [reportDate, setReportDate] = useState(defaultResultDate)
  const [specimenNumber, setSpecimenNumber] = useState('ENCOUNTER-SPECIMEN')
  const [reviewStatus, setReviewStatus] = useState('reviewed')
  const [resultCode, setResultCode] = useState('2345-7')
  const [resultText, setResultText] = useState('Glucose')
  const [resultValue, setResultValue] = useState('104')
  const [resultUnits, setResultUnits] = useState('mg/dL')
  const [resultRange, setResultRange] = useState('70-99')
  const [abnormalFlag, setAbnormalFlag] = useState('high')
  const [resultStatusValue, setResultStatusValue] = useState('final')
  const [resultComments, setResultComments] = useState('Entered from the modernized encounter workflow.')
  const [resultEntryStatus, setResultEntryStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  async function handleProcedureResultSubmit(event: FormEvent) {
    event.preventDefault()
    setResultEntryStatus('saving')

    try {
      await onCreateResultSet({
        report: {
          orderId: order.id,
          dateCollected: `${reportDate} 12:30:00`,
          dateReport: `${reportDate} 13:00:00`,
          specimenNumber,
          reportStatus: resultStatusValue,
          reviewStatus,
          notes: resultComments,
        },
        result: {
          resultCode,
          resultText,
          dateTime: `${reportDate} 13:05:00`,
          facility: 'Legacy EHR Modernization Clinic',
          units: resultUnits,
          result: resultValue,
          range: resultRange,
          abnormal: abnormalFlag,
          comments: resultComments,
          status: resultStatusValue,
        },
      })
      setResultEntryStatus('saved')
    } catch {
      setResultEntryStatus('error')
    }
  }

  return (
    <article className="encounter-procedure-card">
      <div className="procedure-report-title">
        <div>
          <strong>{order.name || 'Procedure order'}</strong>
          <span>{[order.code, order.diagnosis, order.orderDate].filter(Boolean).join(' / ')}</span>
        </div>
        <span className="status-tag">{order.orderStatus || 'Status pending'}</span>
      </div>

      <div className="document-meta-grid encounter-procedure-meta">
        <span>{order.providerName || 'Provider not recorded'}</span>
        <span>{order.orderPriority || 'No priority'}</span>
        <span>{order.procedureType || 'No type'}</span>
        <span>
          {reportCount} reports / {resultCount} results
        </span>
        <span>{specimenCount} specimens</span>
      </div>

      {order.instructions && <p className="procedure-scheduled-note">{order.instructions}</p>}
      <ProcedureSpecimenList specimens={order.specimens} />

      <div className="encounter-procedure-report-list">
        {order.reports.map((report) => (
          <EncounterProcedureReportCard key={report.id} report={report} onUpdateResult={onUpdateResult} />
        ))}
        {order.reports.length === 0 && <div className="timeline-placeholder">No reports recorded for this order</div>}
      </div>

      <form
        className="appointment-mutation-panel encounter-procedure-result-entry-panel"
        onSubmit={handleProcedureResultSubmit}
        aria-label={`Encounter procedure result entry ${order.id}`}
      >
        <div className="panel-heading compact-heading">
          <FlaskConical size={16} />
          <h3>Result Entry</h3>
        </div>
        <div className="mutation-grid encounter-procedure-result-entry-grid">
          <label className="filter-field">
            <span>Report Date</span>
            <input
              value={reportDate}
              onChange={(event) => setReportDate(event.target.value)}
              aria-label="Encounter procedure report date"
              type="date"
              required
            />
          </label>
          <label className="filter-field">
            <span>Specimen</span>
            <input
              value={specimenNumber}
              onChange={(event) => setSpecimenNumber(event.target.value)}
              aria-label="Encounter procedure specimen number"
              required
            />
          </label>
          <label className="filter-field">
            <span>Review</span>
            <select
              value={reviewStatus}
              onChange={(event) => setReviewStatus(event.target.value)}
              aria-label="Encounter procedure review status"
            >
              <option value="reviewed">Reviewed</option>
              <option value="pending">Pending</option>
            </select>
          </label>
          <label className="filter-field">
            <span>Status</span>
            <select
              value={resultStatusValue}
              onChange={(event) => setResultStatusValue(event.target.value)}
              aria-label="Encounter procedure result status"
            >
              <option value="final">Final</option>
              <option value="preliminary">Preliminary</option>
              <option value="corrected">Corrected</option>
            </select>
          </label>
          <label className="filter-field">
            <span>Code</span>
            <input
              value={resultCode}
              onChange={(event) => setResultCode(event.target.value)}
              aria-label="Encounter procedure result code"
              required
            />
          </label>
          <label className="filter-field procedure-order-name-field">
            <span>Result</span>
            <input
              value={resultText}
              onChange={(event) => setResultText(event.target.value)}
              aria-label="Encounter procedure result text"
              required
            />
          </label>
          <label className="filter-field">
            <span>Value</span>
            <input
              value={resultValue}
              onChange={(event) => setResultValue(event.target.value)}
              aria-label="Encounter procedure result value"
              required
            />
          </label>
          <label className="filter-field">
            <span>Units</span>
            <input
              value={resultUnits}
              onChange={(event) => setResultUnits(event.target.value)}
              aria-label="Encounter procedure result units"
              required
            />
          </label>
          <label className="filter-field">
            <span>Range</span>
            <input
              value={resultRange}
              onChange={(event) => setResultRange(event.target.value)}
              aria-label="Encounter procedure result range"
              required
            />
          </label>
          <label className="filter-field">
            <span>Flag</span>
            <input
              value={abnormalFlag}
              onChange={(event) => setAbnormalFlag(event.target.value)}
              aria-label="Encounter procedure result abnormal flag"
            />
          </label>
          <label className="filter-field procedure-result-comment-field">
            <span>Notes</span>
            <input
              value={resultComments}
              onChange={(event) => setResultComments(event.target.value)}
              aria-label="Encounter procedure result notes"
            />
          </label>
        </div>
        <div className="detail-actions compact-actions">
          <button className="icon-text-button primary" type="submit" disabled={resultEntryStatus === 'saving'}>
            <Check size={16} />
            <span>{resultEntryStatus === 'saving' ? 'Saving' : 'Add Result'}</span>
          </button>
          {resultEntryStatus === 'saved' && <span className="save-note">Saved</span>}
          {resultEntryStatus === 'error' && <span className="save-note error">Action failed</span>}
        </div>
      </form>
    </article>
  )
}

function EncounterProcedureReportCard({
  report,
  onUpdateResult,
}: {
  report: ProcedureReportItem
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
}) {
  return (
    <section className="encounter-procedure-report-card">
      <div className="procedure-report-title">
        <div>
          <strong>Report {report.id}</strong>
          <span>
            {[
              report.reportDate,
              report.dateCollected ? `Collected ${report.dateCollected}` : '',
              report.specimenNumber ? `Specimen ${report.specimenNumber}` : '',
              report.reviewStatus,
              report.reviewedBy ? `Signed by ${report.reviewedBy}` : '',
              report.reviewedAt ? `Signed ${report.reviewedAt}` : '',
              report.notes,
            ]
              .filter(Boolean)
              .join(' / ')}
          </span>
        </div>
        <span className="status-tag">{report.status || 'Status pending'}</span>
      </div>
      <div className="encounter-procedure-result-list">
        {report.results.map((result) => (
          <EncounterProcedureResultCard key={result.id} result={result} onUpdateResult={onUpdateResult} />
        ))}
        {report.results.length === 0 && <div className="timeline-placeholder">No result rows recorded</div>}
      </div>
    </section>
  )
}

function EncounterProcedureResultCard({
  result,
  onUpdateResult,
}: {
  result: ProcedureResultItem
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
}) {
  const [isCorrecting, setIsCorrecting] = useState(false)
  const [resultCode, setResultCode] = useState(result.code ?? '')
  const [resultText, setResultText] = useState(result.text ?? '')
  const [resultDate, setResultDate] = useState(result.resultDate)
  const [resultValue, setResultValue] = useState(result.result ?? '')
  const [resultUnits, setResultUnits] = useState(result.units ?? '')
  const [resultRange, setResultRange] = useState(result.range ?? '')
  const [abnormalFlag, setAbnormalFlag] = useState(result.abnormal ?? '')
  const [resultStatus, setResultStatus] = useState(result.resultStatus ?? 'corrected')
  const [correctionStatus, setCorrectionStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    setResultCode(result.code ?? '')
    setResultText(result.text ?? '')
    setResultDate(result.resultDate)
    setResultValue(result.result ?? '')
    setResultUnits(result.units ?? '')
    setResultRange(result.range ?? '')
    setAbnormalFlag(result.abnormal ?? '')
    setResultStatus(result.resultStatus ?? 'corrected')
    setCorrectionStatus('idle')
  }, [result])

  async function handleCorrectionSubmit(event: FormEvent) {
    event.preventDefault()
    setCorrectionStatus('saving')

    try {
      await onUpdateResult(result, {
        resultCode,
        resultText,
        dateTime: resultDate,
        units: resultUnits,
        result: resultValue,
        range: resultRange,
        abnormal: abnormalFlag,
        status: resultStatus,
      })
      setCorrectionStatus('saved')
      setIsCorrecting(false)
    } catch {
      setCorrectionStatus('error')
    }
  }

  return (
    <article className="encounter-procedure-result-card">
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
        <span>{result.range || 'No range'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button className="icon-text-button secondary" type="button" onClick={() => setIsCorrecting((current) => !current)}>
          <Pencil size={15} />
          Correct
        </button>
        {correctionStatus === 'saved' && <span className="save-note">Saved</span>}
        {correctionStatus === 'error' && <span className="save-note error">Action failed</span>}
      </div>
      {isCorrecting && (
        <form
          className="appointment-mutation-panel encounter-procedure-result-entry-panel"
          aria-label={`Encounter procedure result correction ${result.id}`}
          onSubmit={handleCorrectionSubmit}
        >
          <div className="mutation-grid encounter-procedure-result-entry-grid">
            <label className="filter-field">
              <span>Code</span>
              <input
                value={resultCode}
                onChange={(event) => setResultCode(event.target.value)}
                aria-label="Encounter procedure corrected result code"
                required
              />
            </label>
            <label className="filter-field procedure-order-name-field">
              <span>Result</span>
              <input
                value={resultText}
                onChange={(event) => setResultText(event.target.value)}
                aria-label="Encounter procedure corrected result text"
                required
              />
            </label>
            <label className="filter-field">
              <span>Date</span>
              <input
                value={resultDate}
                onChange={(event) => setResultDate(event.target.value)}
                aria-label="Encounter procedure corrected result date"
                required
              />
            </label>
            <label className="filter-field">
              <span>Status</span>
              <select
                value={resultStatus}
                onChange={(event) => setResultStatus(event.target.value)}
                aria-label="Encounter procedure corrected result status"
              >
                <option value="final">Final</option>
                <option value="preliminary">Preliminary</option>
                <option value="corrected">Corrected</option>
              </select>
            </label>
            <label className="filter-field">
              <span>Value</span>
              <input
                value={resultValue}
                onChange={(event) => setResultValue(event.target.value)}
                aria-label="Encounter procedure corrected result value"
                required
              />
            </label>
            <label className="filter-field">
              <span>Units</span>
              <input
                value={resultUnits}
                onChange={(event) => setResultUnits(event.target.value)}
                aria-label="Encounter procedure corrected result units"
              />
            </label>
            <label className="filter-field">
              <span>Range</span>
              <input
                value={resultRange}
                onChange={(event) => setResultRange(event.target.value)}
                aria-label="Encounter procedure corrected result range"
              />
            </label>
            <label className="filter-field">
              <span>Flag</span>
              <input
                value={abnormalFlag}
                onChange={(event) => setAbnormalFlag(event.target.value)}
                aria-label="Encounter procedure corrected result abnormal flag"
              />
            </label>
          </div>
          <div className="detail-actions compact-actions">
            <button className="icon-text-button primary" type="submit" disabled={correctionStatus === 'saving'}>
              <Check size={15} />
              <span>{correctionStatus === 'saving' ? 'Saving' : 'Save Correction'}</span>
            </button>
          </div>
        </form>
      )}
    </article>
  )
}

function EncounterBillingLineCard({ line }: { line: BillingLineItem }) {
  const codeLabel = [line.codeType, line.code].filter(Boolean).join(' ')
  const statusLabel = line.billed === 1 ? 'Billed' : 'Unbilled'
  const activityLabel = line.activity === 1 ? 'Active' : 'Inactive'

  return (
    <article className="encounter-billing-card">
      <div className="billing-line-main">
        <div>
          <strong>{codeLabel || 'Uncoded line'}</strong>
          <span>{line.codeText || 'No billing description'}</span>
        </div>
        <div className="billing-line-fee">{formatCurrency(line.fee)}</div>
      </div>
      <div className="document-meta-grid encounter-billing-meta">
        <span>{line.billingDate}</span>
        <span>Units {line.units}</span>
        <span>{statusLabel}</span>
        <span>{activityLabel}</span>
      </div>
      <div className="document-footnote">
        <span>Justification {line.justify || 'None'}</span>
        <span>{line.modifier ? `Modifier ${line.modifier}` : line.id}</span>
      </div>
    </article>
  )
}

function EncounterDocumentAttachmentCard({
  document,
  encounter,
  disabled,
  onUpdateMetadata,
  onMove,
  onReplaceContent,
  onReplaceBinaryContent,
  onArchive,
  onRestore,
  onSign,
  onDeny,
  onDownload,
}: {
  document: EncounterDocumentAttachment
  encounter: number
  disabled: boolean
  onUpdateMetadata: (
    document: EncounterDocumentAttachment,
    input: PatientDocumentMetadataUpdateInput,
  ) => Promise<void>
  onMove: (document: EncounterDocumentAttachment, targetEncounter: number) => Promise<void>
  onReplaceContent: (
    document: EncounterDocumentAttachment,
    input: PatientDocumentContentReplaceInput,
  ) => Promise<void>
  onReplaceBinaryContent: (
    document: EncounterDocumentAttachment,
    input: PatientDocumentBinaryContentReplaceInput,
  ) => Promise<void>
  onArchive: (document: EncounterDocumentAttachment) => Promise<void>
  onRestore: (document: EncounterDocumentAttachment) => Promise<void>
  onSign: (document: EncounterDocumentAttachment) => Promise<void>
  onDeny: (document: EncounterDocumentAttachment) => Promise<void>
  onDownload: (document: EncounterDocumentAttachment) => Promise<void>
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [editName, setEditName] = useState(document.name)
  const [editCategoryId, setEditCategoryId] = useState(String(document.categoryId || 3))
  const [editDocDate, setEditDocDate] = useState(document.docDate)
  const [editNotes, setEditNotes] = useState(document.notes ?? '')
  const [editError, setEditError] = useState<string | null>(null)
  const [moveEncounter, setMoveEncounter] = useState('')
  const [moveError, setMoveError] = useState<string | null>(null)
  const [isReplacing, setIsReplacing] = useState(false)
  const [isReplacingBinary, setIsReplacingBinary] = useState(false)
  const [replacementFileName, setReplacementFileName] = useState(document.fileName || `${document.name}.txt`)
  const [replacementContent, setReplacementContent] = useState('')
  const [replacementBinaryFileName, setReplacementBinaryFileName] = useState(document.fileName || document.name)
  const [replacementBinaryMimeType, setReplacementBinaryMimeType] = useState(document.mimetype || 'application/octet-stream')
  const [replacementBinaryContentBase64, setReplacementBinaryContentBase64] = useState('')
  const [replacementBinaryFileMessage, setReplacementBinaryFileMessage] = useState('No file selected')
  const [replaceError, setReplaceError] = useState<string | null>(null)
  const hasExternalLink = document.storageMethod === 'web_url' && Boolean(document.url)
  const isArchived = document.deleted !== 0
  const canReplaceContent = !isArchived && !hasExternalLink
  const isReviewed = document.reviewStatus === 'approved' || document.reviewStatus === 'denied'

  useEffect(() => {
    setEditName(document.name)
    setEditCategoryId(String(document.categoryId || 3))
    setEditDocDate(document.docDate)
    setEditNotes(document.notes ?? '')
    setEditError(null)
    setIsMoving(false)
    setMoveEncounter('')
    setMoveError(null)
    setIsReplacing(false)
    setIsReplacingBinary(false)
    setReplacementFileName(document.fileName || `${document.name}.txt`)
    setReplacementContent('')
    setReplacementBinaryFileName(document.fileName || document.name)
    setReplacementBinaryMimeType(document.mimetype || 'application/octet-stream')
    setReplacementBinaryContentBase64('')
    setReplacementBinaryFileMessage('No file selected')
    setReplaceError(null)
  }, [document])

  async function handleMetadataSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setEditError(null)

    const categoryId = Number(editCategoryId)
    if (!Number.isInteger(categoryId)) {
      setEditError('Check numeric fields')
      return
    }

    try {
      await onUpdateMetadata(document, {
        categoryId,
        name: editName,
        docDate: editDocDate,
        encounter,
        notes: editNotes.trim().length > 0 ? editNotes : null,
      })
      setIsEditing(false)
    } catch {
      setEditError('Metadata save failed')
    }
  }

  async function handleMoveSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMoveError(null)

    const targetEncounter = Number(moveEncounter)
    if (!Number.isInteger(targetEncounter) || targetEncounter <= 0) {
      setMoveError('Enter a target encounter')
      return
    }

    if (targetEncounter === encounter) {
      setMoveError('Choose a different encounter')
      return
    }

    try {
      await onMove(document, targetEncounter)
      setIsMoving(false)
    } catch {
      setMoveError('Move failed')
    }
  }

  async function handleContentReplacementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setReplaceError(null)

    if (!replacementFileName.trim() || !replacementContent.trim()) {
      setReplaceError('Enter a file name and replacement body')
      return
    }

    try {
      await onReplaceContent(document, {
        fileName: replacementFileName,
        content: replacementContent,
      })
      setIsReplacing(false)
      setReplacementContent('')
    } catch {
      setReplaceError('Content save failed')
    }
  }

  async function handleBinaryReplacementFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      setReplacementBinaryFileName(document.fileName || document.name)
      setReplacementBinaryMimeType(document.mimetype || 'application/octet-stream')
      setReplacementBinaryContentBase64('')
      setReplacementBinaryFileMessage('No file selected')
      return
    }

    const contentBase64 = await readFileAsBase64(file)
    setReplacementBinaryFileName(file.name)
    setReplacementBinaryMimeType(file.type || 'application/octet-stream')
    setReplacementBinaryContentBase64(contentBase64)
    setReplacementBinaryFileMessage(`${file.name} selected (${formatBytes(file.size)})`)
  }

  async function handleBinaryContentReplacementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setReplaceError(null)

    if (!replacementBinaryFileName.trim() || !replacementBinaryMimeType.trim() || !replacementBinaryContentBase64.trim()) {
      setReplaceError('Choose a replacement file')
      return
    }

    try {
      await onReplaceBinaryContent(document, {
        fileName: replacementBinaryFileName,
        mimetype: replacementBinaryMimeType,
        contentBase64: replacementBinaryContentBase64,
      })
      setIsReplacingBinary(false)
      setReplacementBinaryContentBase64('')
      setReplacementBinaryFileMessage('No file selected')
    } catch {
      setReplaceError('Binary content save failed')
    }
  }

  return (
    <article className="encounter-document-card">
      <div className="document-preview-readiness">
        <div
          className={`document-thumbnail document-thumbnail-${document.previewKind || 'file'}`}
          aria-label={`Document preview ${document.thumbnailLabel || 'DOC'}`}
        >
          <span>{document.thumbnailLabel || 'DOC'}</span>
        </div>
        <div className="document-preview-summary">
          <strong>{document.name}</strong>
          {isArchived && <span className="status-tag danger">Archived</span>}
          <span>{document.previewStatus || 'Preview pending'}</span>
          <p>{document.thumbnailText || document.contentPreview || document.fileName || 'No preview generated'}</p>
        </div>
      </div>

      <div className="document-meta-grid encounter-document-meta">
        <span>{document.categoryName}</span>
        <span>{document.docDate}</span>
        <span>{document.mimetype || 'No mimetype'}</span>
        <span>{formatBytes(document.sizeBytes)}</span>
      </div>
      <div className="document-revision-readiness">
        <span>{document.versionLabel || 'Version 1'} / {document.versionStatus || 'Current version'}</span>
        <span>{document.revisionAt || document.uploadedAt}</span>
        <span>{document.hasPriorVersions ? `${document.versionHistoryCount} versions` : 'No prior versions'}</span>
      </div>
      {document.isScannedAttachment && (
        <div className="document-scan-readiness" aria-label={`Scan readiness for ${document.name}`}>
          <span>{document.scanStatus}</span>
          <span>{document.captureSource}</span>
          <span>{document.scanPageCount} scanned page{document.scanPageCount === 1 ? '' : 's'}</span>
          <span>{document.ocrStatus}</span>
        </div>
      )}
      <div className="procedure-order-meta">
        <span>{document.reviewStatus === 'approved' ? 'approved' : document.reviewStatus || 'pending'}</span>
        <span>{document.reviewedBy ? `Reviewed by ${document.reviewedBy}` : 'Not reviewed'}</span>
        <span>{document.reviewedAt || 'No review time'}</span>
      </div>

      <div className="document-lifecycle-readiness" aria-label={`Lifecycle for ${document.name}`}>
        {(document.lifecycleEvents ?? []).map((event) => (
          <div className="document-lifecycle-event" key={event.code}>
            <strong>{event.label}</strong>
            <span>{event.occurredAt || 'Current state'}</span>
            <span>{event.actor ? `By ${event.actor}` : event.detail}</span>
          </div>
        ))}
      </div>

      <p className="document-preview">{document.contentPreview || document.notes || 'No preview available'}</p>
      {document.notes && <p className="document-note">Notes: {document.notes}</p>}

      <div className="document-footnote">
        <span>{document.documentKey}</span>
        <span>{hasExternalLink ? document.url : document.fileName || document.hash || 'No document reference'}</span>
      </div>

      {isEditing && (
        <form className="document-edit-form" onSubmit={handleMetadataSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Name</span>
              <input
                value={editName}
                onChange={(event) => setEditName(event.target.value)}
                aria-label="Encounter document metadata name"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Category</span>
                <select
                  value={editCategoryId}
                  onChange={(event) => setEditCategoryId(event.target.value)}
                  aria-label="Encounter document metadata category"
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
                  value={editDocDate}
                  onChange={(event) => setEditDocDate(event.target.value)}
                  aria-label="Encounter document metadata date"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Encounter</span>
              <input value={encounter} aria-label="Encounter document metadata encounter" readOnly />
            </label>
            <label className="filter-field">
              <span>Notes</span>
              <textarea
                value={editNotes}
                onChange={(event) => setEditNotes(event.target.value)}
                aria-label="Encounter document metadata notes"
                rows={3}
              />
            </label>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Metadata
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsEditing(false)}>
              <X size={14} />
              Cancel
            </button>
            {editError && <span className="save-note error">{editError}</span>}
          </div>
        </form>
      )}

      {isReplacing && (
        <form className="document-edit-form" onSubmit={handleContentReplacementSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>File Name</span>
              <input
                value={replacementFileName}
                onChange={(event) => setReplacementFileName(event.target.value)}
                aria-label="Encounter replacement document file name"
                required
              />
            </label>
            <label className="filter-field">
              <span>Replacement Body</span>
              <textarea
                value={replacementContent}
                onChange={(event) => setReplacementContent(event.target.value)}
                aria-label="Encounter replacement document body"
                rows={4}
                required
              />
            </label>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Content
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsReplacing(false)}>
              <X size={14} />
              Cancel
            </button>
            {replaceError && <span className="save-note error">{replaceError}</span>}
          </div>
        </form>
      )}

      {isReplacingBinary && (
        <form className="document-edit-form" onSubmit={handleBinaryContentReplacementSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Binary File</span>
              <input
                type="file"
                onChange={handleBinaryReplacementFileChange}
                aria-label="Encounter replacement binary document upload"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>File Name</span>
                <input
                  value={replacementBinaryFileName}
                  onChange={(event) => setReplacementBinaryFileName(event.target.value)}
                  aria-label="Encounter replacement binary document file name"
                  required
                />
              </label>
              <label className="filter-field">
                <span>MIME Type</span>
                <input
                  value={replacementBinaryMimeType}
                  onChange={(event) => setReplacementBinaryMimeType(event.target.value)}
                  aria-label="Encounter replacement binary document MIME type"
                  required
                />
              </label>
            </div>
            <span className="save-note">{replacementBinaryFileMessage}</span>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Binary
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsReplacingBinary(false)}>
              <X size={14} />
              Cancel
            </button>
            {replaceError && <span className="save-note error">{replaceError}</span>}
          </div>
        </form>
      )}

      {isMoving && (
        <form className="document-edit-form" onSubmit={handleMoveSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Target Encounter</span>
              <input
                value={moveEncounter}
                onChange={(event) => setMoveEncounter(event.target.value)}
                aria-label="Encounter document move target encounter"
                inputMode="numeric"
                required
              />
            </label>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <MapPin size={14} />
              Move
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsMoving(false)}>
              <X size={14} />
              Cancel
            </button>
            {moveError && <span className="save-note error">{moveError}</span>}
          </div>
        </form>
      )}

      <div className="document-item-actions">
        {document.canDownload && (
          <button
            className="icon-text-button secondary"
            type="button"
            disabled={disabled || isArchived}
            onClick={() => void onDownload(document)}
          >
            <Download size={14} />
            Download
          </button>
        )}
        {hasExternalLink && (
          <a className="icon-text-button secondary" href={document.url ?? '#'} target="_blank" rel="noreferrer">
            <ExternalLink size={14} />
            Open Link
          </a>
        )}
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => {
            setIsMoving(false)
            setIsReplacing(false)
            setIsReplacingBinary(false)
            setIsEditing((current) => !current)
          }}
        >
          <Pencil size={14} />
          Edit
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => {
            setIsEditing(false)
            setIsReplacing(false)
            setIsReplacingBinary(false)
            setIsMoving((current) => !current)
          }}
        >
          <MapPin size={14} />
          Move
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || !canReplaceContent}
          onClick={() => {
            setIsEditing(false)
            setIsMoving(false)
            setIsReplacingBinary(false)
            setIsReplacing((current) => !current)
          }}
        >
          <FileText size={14} />
          Replace
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || !canReplaceContent}
          onClick={() => {
            setIsEditing(false)
            setIsMoving(false)
            setIsReplacing(false)
            setIsReplacingBinary((current) => !current)
          }}
        >
          <Upload size={14} />
          Binary File
        </button>
        <button
          className="icon-text-button danger"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => void onArchive(document)}
        >
          <Ban size={14} />
          Archive
        </button>
        {isArchived && (
          <button
            className="icon-text-button secondary"
            type="button"
            disabled={disabled}
            onClick={() => void onRestore(document)}
          >
            <RotateCcw size={14} />
            Restore
          </button>
        )}
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isReviewed || isArchived}
          onClick={() => void onSign(document)}
        >
          <ShieldCheck size={14} />
          Sign
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isReviewed || isArchived}
          onClick={() => void onDeny(document)}
        >
          <X size={14} />
          Deny
        </button>
      </div>
    </article>
  )
}

function ClinicalListsWorkspace({
  patientId,
  clinicalLists,
  status,
  error,
  sessionId,
  onClinicalListsSessionActive,
  onPatientIdChange,
  onCreateAllergy,
  onDeactivateAllergy,
  onDeleteAllergy,
  onCreateProblem,
  onDeactivateProblem,
  onDeleteProblem,
  onCreateMedication,
  onDeactivateMedication,
  onDeleteMedication,
  onCreatePrescription,
  onDeactivatePrescription,
  onDeletePrescription,
  onCreateImmunization,
  onMarkImmunizationEnteredInError,
  onDeleteImmunization,
}: {
  patientId: string
  clinicalLists: ClinicalListsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onClinicalListsSessionActive: (sessionId: string) => void
  onPatientIdChange: (value: string) => void
  onCreateAllergy: (input: ClinicalAllergyCreateInput) => Promise<unknown>
  onDeactivateAllergy: (allergy: AllergyListItem) => Promise<unknown>
  onDeleteAllergy: (allergy: AllergyListItem) => Promise<void>
  onCreateProblem: (input: ClinicalProblemCreateInput) => Promise<unknown>
  onDeactivateProblem: (problem: ProblemListItem) => Promise<unknown>
  onDeleteProblem: (problem: ProblemListItem) => Promise<void>
  onCreateMedication: (input: ClinicalMedicationCreateInput) => Promise<unknown>
  onDeactivateMedication: (medication: MedicationListItem) => Promise<unknown>
  onDeleteMedication: (medication: MedicationListItem) => Promise<void>
  onCreatePrescription: (input: ClinicalPrescriptionCreateInput) => Promise<unknown>
  onDeactivatePrescription: (prescription: PrescriptionListItem) => Promise<unknown>
  onDeletePrescription: (prescription: PrescriptionListItem) => Promise<void>
  onCreateImmunization: (input: ClinicalImmunizationCreateInput) => Promise<unknown>
  onMarkImmunizationEnteredInError: (immunization: ImmunizationListItem) => Promise<unknown>
  onDeleteImmunization: (immunization: ImmunizationListItem) => Promise<void>
}) {
  const [listsLoginUsername, setListsLoginUsername] = useState('admin')
  const [listsLoginPassword, setListsLoginPassword] = useState('pass')
  const [listsLoginStatus, setListsLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [listsLoginMessage, setListsLoginMessage] = useState<string | null>(null)
  const [allergyTitle, setAllergyTitle] = useState('Parity Allergy')
  const [allergyDate, setAllergyDate] = useState('2026-06-18 09:00:00')
  const [allergyReaction, setAllergyReaction] = useState('Rash')
  const [allergySeverity, setAllergySeverity] = useState('mild')
  const [allergyComments, setAllergyComments] = useState('Created from the modernized Lists workspace.')
  const [problemTitle, setProblemTitle] = useState('Parity Problem')
  const [problemDate, setProblemDate] = useState('2026-06-18 09:00:00')
  const [problemDiagnosis, setProblemDiagnosis] = useState('ICD10:Z00.00')
  const [problemComments, setProblemComments] = useState('Created from the modernized Lists workspace.')
  const [medicationTitle, setMedicationTitle] = useState('Parity Medication List Entry')
  const [medicationDate, setMedicationDate] = useState('2026-07-15 09:00:00')
  const [medicationDiagnosis, setMedicationDiagnosis] = useState('ICD10:Z00.00')
  const [medicationComments, setMedicationComments] = useState('Created from the modernized Lists workspace.')
  const [prescriptionDrug, setPrescriptionDrug] = useState('Parity Medication')
  const [prescriptionStartDate, setPrescriptionStartDate] = useState('2026-07-15')
  const [prescriptionDosage, setPrescriptionDosage] = useState('1 tablet daily')
  const [prescriptionQuantity, setPrescriptionQuantity] = useState('30')
  const [prescriptionRefills, setPrescriptionRefills] = useState('1')
  const [prescriptionDiagnosis, setPrescriptionDiagnosis] = useState('Z00.00')
  const [prescriptionNote, setPrescriptionNote] = useState('Created from the modernized Lists workspace.')
  const [immunizationVaccine, setImmunizationVaccine] = useState('Influenza, seasonal, injectable')
  const [immunizationAdministeredAt, setImmunizationAdministeredAt] = useState('2026-09-10 10:30:00')
  const [immunizationCvxCode, setImmunizationCvxCode] = useState('141')
  const [immunizationLotNumber, setImmunizationLotNumber] = useState('MUT-IMM-DEMO')
  const [immunizationManufacturer, setImmunizationManufacturer] = useState('Sanofi Pasteur')
  const [immunizationRoute, setImmunizationRoute] = useState('intramuscular')
  const [immunizationSite, setImmunizationSite] = useState('left deltoid')
  const [immunizationVisDate, setImmunizationVisDate] = useState('2026-08-01')
  const [immunizationNote, setImmunizationNote] = useState('Created from the modernized Lists workspace.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const isLoading = status === 'loading'
  const canUseLists = Boolean(sessionId)

  async function handleListsLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setListsLoginStatus('checking')
    setListsLoginMessage(null)

    try {
      const result = await login({ username: listsLoginUsername, password: listsLoginPassword })
      if (result.authenticated && result.sessionId) {
        onClinicalListsSessionActive(result.sessionId)
        setListsLoginStatus('authenticated')
        setListsLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setListsLoginStatus('rejected')
        setListsLoginMessage(result.failureReason ?? 'Lists access was rejected.')
      }
    } catch (error) {
      setListsLoginStatus('error')
      setListsLoginMessage(error instanceof Error ? error.message : 'Lists access check failed')
    }
  }

  async function handleAllergySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canUseLists) {
      return
    }
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

  async function handleProblemSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canUseLists) {
      return
    }
    setMutationMessage(null)

    await onCreateProblem({
      patientId,
      title: problemTitle,
      dateTime: problemDate,
      diagnosis: problemDiagnosis,
      comments: problemComments,
    })

    setMutationMessage('Problem saved')
  }

  async function handleMedicationSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canUseLists) {
      return
    }
    setMutationMessage(null)

    await onCreateMedication({
      patientId,
      title: medicationTitle,
      dateTime: medicationDate,
      diagnosis: medicationDiagnosis,
      comments: medicationComments,
    })

    setMutationMessage('Medication saved')
  }

  async function handlePrescriptionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canUseLists) {
      return
    }
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

  async function handleImmunizationSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canUseLists) {
      return
    }
    setMutationMessage(null)

    await onCreateImmunization({
      patientId,
      immunizationId: 30,
      cvxCode: immunizationCvxCode,
      vaccine: immunizationVaccine,
      administeredAt: immunizationAdministeredAt,
      manufacturer: immunizationManufacturer,
      lotNumber: immunizationLotNumber,
      administeredBy: 'admin',
      educationDate: immunizationAdministeredAt.slice(0, 10),
      visDate: immunizationVisDate,
      amountAdministered: 0.5,
      amountAdministeredUnit: 'mL',
      expirationDate: '2027-06-30',
      route: immunizationRoute,
      administrationSite: immunizationSite,
      completionStatus: 'completed',
      informationSource: 'new_immunization_record',
      note: immunizationNote,
    })

    setMutationMessage('Immunization saved')
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Clinical lists search">
        {(!sessionId || status === 'error') && (
          <form className="mutation-form" aria-label="Lists access" onSubmit={handleListsLogin}>
            <div className="panel-heading">
              <ShieldCheck size={17} />
              <h3>Lists Access</h3>
            </div>
            <label>
              Username
              <input value={listsLoginUsername} onChange={(event) => setListsLoginUsername(event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={listsLoginPassword}
                onChange={(event) => setListsLoginPassword(event.target.value)}
              />
            </label>
            <button type="submit" disabled={listsLoginStatus === 'checking'}>
              <LogIn size={15} />
              {listsLoginStatus === 'checking' ? 'Checking' : 'Verify Lists Access'}
            </button>
            {listsLoginMessage && (
              <div className={listsLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {listsLoginMessage}
              </div>
            )}
          </form>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Clinical lists patient ID"
              placeholder="MOD-PAT-0001"
              disabled={!canUseLists}
            />
          </label>
        </div>

        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Chart lists'}</span>
          <span>Problem, allergy, medication, Rx, and immunization lifecycles</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}
        {mutationMessage && status !== 'error' && <div className="status-banner success">{mutationMessage}</div>}

        {clinicalLists ? (
          <div className="list-counts">
            <MetricRow label="Problems" value={clinicalLists.problems.length} />
            <MetricRow label="Allergies" value={clinicalLists.allergies.length} />
            <MetricRow label="Medications" value={clinicalLists.medications.length} />
            <MetricRow label="Immunizations" value={clinicalLists.immunizations.length} />
            <MetricRow label="Prescriptions" value={clinicalLists.prescriptions.length} />
          </div>
        ) : (
          <div className="empty-state">No clinical lists loaded</div>
        )}

        <form className="appointment-mutation-panel" onSubmit={handleProblemSubmit}>
          <div className="panel-heading compact-heading">
            <ClipboardList size={16} />
            <h3>New Problem</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Title</span>
              <input
                value={problemTitle}
                onChange={(event) => setProblemTitle(event.target.value)}
                aria-label="New problem title"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Date</span>
                <input
                  value={problemDate}
                  onChange={(event) => setProblemDate(event.target.value)}
                  aria-label="New problem date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Diagnosis</span>
                <input
                  value={problemDiagnosis}
                  onChange={(event) => setProblemDiagnosis(event.target.value)}
                  aria-label="New problem diagnosis"
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Comments</span>
              <textarea
                value={problemComments}
                onChange={(event) => setProblemComments(event.target.value)}
                aria-label="New problem comments"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading || !canUseLists}>
              <Check size={15} />
              Save Problem
            </button>
          </div>
        </form>

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
            <button className="icon-text-button primary" type="submit" disabled={isLoading || !canUseLists}>
              <Check size={15} />
              Save Allergy
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleMedicationSubmit}>
          <div className="panel-heading compact-heading">
            <HeartPulse size={16} />
            <h3>New Medication</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Title</span>
              <input
                value={medicationTitle}
                onChange={(event) => setMedicationTitle(event.target.value)}
                aria-label="New medication title"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Date</span>
                <input
                  value={medicationDate}
                  onChange={(event) => setMedicationDate(event.target.value)}
                  aria-label="New medication date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Diagnosis</span>
                <input
                  value={medicationDiagnosis}
                  onChange={(event) => setMedicationDiagnosis(event.target.value)}
                  aria-label="New medication diagnosis"
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Comments</span>
              <textarea
                value={medicationComments}
                onChange={(event) => setMedicationComments(event.target.value)}
                aria-label="New medication comments"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading || !canUseLists}>
              <Check size={15} />
              Save Medication
            </button>
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
            <button className="icon-text-button primary" type="submit" disabled={isLoading || !canUseLists}>
              <Check size={15} />
              Save Rx
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleImmunizationSubmit}>
          <div className="panel-heading compact-heading">
            <Syringe size={16} />
            <h3>New Immunization</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Vaccine</span>
              <input
                value={immunizationVaccine}
                onChange={(event) => setImmunizationVaccine(event.target.value)}
                aria-label="New immunization vaccine"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Administered</span>
                <input
                  value={immunizationAdministeredAt}
                  onChange={(event) => setImmunizationAdministeredAt(event.target.value)}
                  aria-label="New immunization administered date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>CVX</span>
                <input
                  value={immunizationCvxCode}
                  onChange={(event) => setImmunizationCvxCode(event.target.value)}
                  aria-label="New immunization CVX code"
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Manufacturer</span>
                <input
                  value={immunizationManufacturer}
                  onChange={(event) => setImmunizationManufacturer(event.target.value)}
                  aria-label="New immunization manufacturer"
                />
              </label>
              <label className="filter-field">
                <span>Lot</span>
                <input
                  value={immunizationLotNumber}
                  onChange={(event) => setImmunizationLotNumber(event.target.value)}
                  aria-label="New immunization lot number"
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Route</span>
                <input
                  value={immunizationRoute}
                  onChange={(event) => setImmunizationRoute(event.target.value)}
                  aria-label="New immunization route"
                />
              </label>
              <label className="filter-field">
                <span>Site</span>
                <input
                  value={immunizationSite}
                  onChange={(event) => setImmunizationSite(event.target.value)}
                  aria-label="New immunization site"
                />
              </label>
            </div>
            <label className="filter-field">
              <span>VIS date</span>
              <input
                value={immunizationVisDate}
                onChange={(event) => setImmunizationVisDate(event.target.value)}
                aria-label="New immunization VIS date"
              />
            </label>
            <label className="filter-field">
              <span>Note</span>
              <textarea
                value={immunizationNote}
                onChange={(event) => setImmunizationNote(event.target.value)}
                aria-label="New immunization note"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading || !canUseLists}>
              <Check size={15} />
              Save Immunization
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
                {clinicalLists.problems.length +
                  clinicalLists.allergies.length +
                  clinicalLists.medications.length +
                  clinicalLists.immunizations.length} active
              </div>
            </div>

            <div className="clinical-list-grid">
              <ProblemPanel
                items={clinicalLists.problems}
                onDeactivate={onDeactivateProblem}
                onDelete={onDeleteProblem}
                disabled={isLoading || !canUseLists}
              />
              <AllergyPanel
                items={clinicalLists.allergies}
                onDeactivate={onDeactivateAllergy}
                onDelete={onDeleteAllergy}
                disabled={isLoading || !canUseLists}
              />
              <MedicationPanel
                items={clinicalLists.medications}
                onDeactivate={onDeactivateMedication}
                onDelete={onDeleteMedication}
                disabled={isLoading || !canUseLists}
              />
              <ImmunizationPanel
                items={clinicalLists.immunizations}
                onMarkEnteredInError={onMarkImmunizationEnteredInError}
                onDelete={onDeleteImmunization}
                disabled={isLoading || !canUseLists}
              />
              <PrescriptionPanel
                items={clinicalLists.prescriptions}
                onDeactivate={onDeactivatePrescription}
                onDelete={onDeletePrescription}
                disabled={isLoading || !canUseLists}
              />
            </div>
          </>
        ) : !sessionId ? (
          <div className="empty-chart">Sign in to load clinical lists</div>
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
  sessionId,
  onBillingSessionActive,
  onPatientIdChange,
  onCreateLine,
  onUpdateLine,
  onDeactivateLine,
  onDeleteLine,
  onCreateClaim,
  onUpdateClaimStatus,
  onDeleteClaim,
  onCreatePayment,
  onVoidPayment,
  onDeletePayment,
}: {
  patientId: string
  patientBilling: PatientBillingResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onBillingSessionActive: (sessionId: string) => void
  onPatientIdChange: (value: string) => void
  onCreateLine: (input: BillingLineCreateInput) => Promise<unknown>
  onUpdateLine: (lineId: string, input: BillingLineUpdateInput) => Promise<unknown>
  onDeactivateLine: (line: BillingLineItem) => Promise<unknown>
  onDeleteLine: (line: BillingLineItem) => Promise<void>
  onCreateClaim: (input: BillingClaimCreateInput) => Promise<unknown>
  onUpdateClaimStatus: (claim: BillingClaimItem, input: BillingClaimStatusUpdateInput) => Promise<unknown>
  onDeleteClaim: (claim: BillingClaimItem) => Promise<void>
  onCreatePayment: (input: BillingPaymentCreateInput) => Promise<unknown>
  onVoidPayment: (payment: BillingPaymentItem) => Promise<unknown>
  onDeletePayment: (payment: BillingPaymentItem) => Promise<void>
}) {
  const [billingEncounter, setBillingEncounter] = useState('')
  const [billingDate, setBillingDate] = useState('2026-06-18')
  const [billingCode, setBillingCode] = useState('99213')
  const [billingModifier, setBillingModifier] = useState('')
  const [billingCodeText, setBillingCodeText] = useState('Established patient office visit')
  const [billingFee, setBillingFee] = useState('125.00')
  const [billingUnits, setBillingUnits] = useState('1')
  const [billingJustify, setBillingJustify] = useState('Z00.00')
  const [diagnosisCode, setDiagnosisCode] = useState('R73.03')
  const [diagnosisText, setDiagnosisText] = useState('Prediabetes')
  const [correctionLineId, setCorrectionLineId] = useState('')
  const [correctionCodeText, setCorrectionCodeText] = useState('Corrected established patient office visit')
  const [correctionModifier, setCorrectionModifier] = useState('25')
  const [correctionFee, setCorrectionFee] = useState('132.50')
  const [correctionUnits, setCorrectionUnits] = useState('2')
  const [correctionJustify, setCorrectionJustify] = useState('Z00.00')
  const [claimPayerId, setClaimPayerId] = useState('9005')
  const [claimPayerName, setClaimPayerName] = useState('Northstar HMO')
  const [claimTarget, setClaimTarget] = useState('HCFA')
  const [claimBillTime, setClaimBillTime] = useState('2026-06-18 12:15:00')
  const [claimPayload, setClaimPayload] = useState('Parity claim status mutation')
  const [paymentReference, setPaymentReference] = useState('EOB-PARITY-1000052')
  const [paymentPostDate, setPaymentPostDate] = useState('2026-06-18')
  const [paymentSource, setPaymentSource] = useState<'insurance' | 'patient'>('insurance')
  const [paymentPayerId, setPaymentPayerId] = useState('9005')
  const [paymentPayerName, setPaymentPayerName] = useState('Northstar HMO')
  const [paymentMethod, setPaymentMethod] = useState('check_payment')
  const [paymentCode, setPaymentCode] = useState('99214')
  const [paymentMemo, setPaymentMemo] = useState('Parity payment posting')
  const [paymentPayAmount, setPaymentPayAmount] = useState('21.00')
  const [paymentAdjustmentAmount, setPaymentAdjustmentAmount] = useState('3.50')
  const [paymentReasonCode, setPaymentReasonCode] = useState('CO-45')
  const [paymentPayerClaimNumber, setPaymentPayerClaimNumber] = useState('NSTAR-CLM-PARITY')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const [statementBatch, setStatementBatch] = useState<StatementBatchResponse | null>(null)
  const [statementBatchStatus, setStatementBatchStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [statementBatchError, setStatementBatchError] = useState<string | null>(null)
  const [collectionsWorkQueue, setCollectionsWorkQueue] = useState<CollectionsWorkQueueResponse | null>(null)
  const [collectionsWorkQueueStatus, setCollectionsWorkQueueStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [collectionsWorkQueueError, setCollectionsWorkQueueError] = useState<string | null>(null)
  const [collectionsFollowUpStatus, setCollectionsFollowUpStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [collectionsFollowUpMessage, setCollectionsFollowUpMessage] = useState<string | null>(null)
  const [billingLoginUsername, setBillingLoginUsername] = useState('admin')
  const [billingLoginPassword, setBillingLoginPassword] = useState('pass')
  const [billingLoginStatus, setBillingLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [billingLoginMessage, setBillingLoginMessage] = useState<string | null>(null)
  const [statementPdfStatus, setStatementPdfStatus] = useState<'idle' | 'downloading' | 'ready' | 'error'>('idle')
  const [statementPdfError, setStatementPdfError] = useState<string | null>(null)
  const [batchPackageStatus, setBatchPackageStatus] = useState<'idle' | 'downloading' | 'ready' | 'error'>('idle')
  const [batchPackageError, setBatchPackageError] = useState<string | null>(null)
  const lineCount = countBillingLines(patientBilling?.encounters)
  const claimCount = countBillingClaims(patientBilling?.encounters)
  const paymentCount = countBillingPayments(patientBilling?.encounters)
  const totalFee = patientBilling?.encounters.reduce((sum, encounter) => sum + encounter.totalFee, 0) ?? 0
  const accountSummary = patientBilling?.accountSummary
  const agingSummary = patientBilling?.agingSummary
  const ledgerSummary = patientBilling?.ledgerSummary
  const statementSummary = patientBilling?.statementSummary
  const statementDocument = patientBilling?.statementDocument
  const ledgerEntries = patientBilling?.ledgerEntries ?? []
  const statementLineItems = statementDocument?.lineItems ?? []
  const isLoading = status === 'loading'
  const billingAuthorizationError = status === 'error' && Boolean(error?.includes('Billing access'))
  const feesLocked = !sessionId || billingAuthorizationError

  useEffect(() => {
    if (!sessionId) {
      setStatementBatch(null)
      setStatementBatchStatus('idle')
      setStatementBatchError(null)
      setCollectionsWorkQueue(null)
      setCollectionsWorkQueueStatus('idle')
      setCollectionsWorkQueueError(null)
      return
    }

    const controller = new AbortController()
    setStatementBatchStatus('loading')
    setStatementBatchError(null)
    setCollectionsWorkQueueStatus('loading')
    setCollectionsWorkQueueError(null)

    getStatementBatch(5, sessionId, controller.signal)
      .then((result) => {
        setStatementBatch(result)
        setStatementBatchStatus('ready')
      })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setStatementBatchError(loadError instanceof Error ? loadError.message : 'Statement batch load failed')
          setStatementBatchStatus('error')
        }
      })

    getCollectionsWorkQueue(5, sessionId, controller.signal)
      .then((result) => {
        setCollectionsWorkQueue(result)
        setCollectionsWorkQueueStatus('ready')
      })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setCollectionsWorkQueueError(loadError instanceof Error ? loadError.message : 'Collections work queue load failed')
          setCollectionsWorkQueueStatus('error')
        }
      })

    return () => controller.abort()
  }, [sessionId])

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
      modifier: billingModifier,
      codeText: billingCodeText,
      fee: Number(billingFee),
      units: Number(billingUnits),
      justify: billingJustify,
    })

    setMutationMessage('Billing line saved')
  }

  async function handleDiagnosisLineSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateLine({
      patientId,
      encounter: Number(billingEncounter),
      billingDate,
      codeType: 'ICD10',
      code: diagnosisCode,
      codeText: diagnosisText,
      fee: 0,
      units: 1,
      justify: diagnosisCode,
    })

    setMutationMessage('Diagnosis line saved')
  }

  function handleSelectCorrectionLine(line: BillingLineItem) {
    setCorrectionLineId(line.id)
    setCorrectionCodeText(line.codeText || '')
    setCorrectionModifier(line.modifier || '')
    setCorrectionFee(line.fee?.toFixed(2) ?? '0.00')
    setCorrectionUnits(String(line.units))
    setCorrectionJustify(line.justify || '')
    setMutationMessage(null)
  }

  async function handleBillingCorrectionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onUpdateLine(correctionLineId, {
      codeText: correctionCodeText,
      modifier: correctionModifier,
      fee: Number(correctionFee),
      units: Number(correctionUnits),
      justify: correctionJustify,
    })

    setMutationMessage('Billing correction saved')
  }

  async function handleClaimStatusSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    await onCreateClaim({
      patientId,
      encounter: Number(billingEncounter),
      payerId: Number(claimPayerId),
      payerName: claimPayerName,
      payerType: 1,
      status: 1,
      billProcess: 1,
      billTime: claimBillTime,
      target: claimTarget,
      x12PartnerId: claimTarget === 'X12' ? 1 : 0,
      submittedClaim: claimPayload,
    })

    setMutationMessage('Claim status queued')
  }

  async function handlePaymentPostingSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)
    const isPatientPayment = paymentSource === 'patient'

    await onCreatePayment({
      patientId,
      encounter: Number(billingEncounter),
      payerId: isPatientPayment ? 0 : Number(paymentPayerId),
      payerName: isPatientPayment ? '' : paymentPayerName,
      payerType: isPatientPayment ? 0 : 1,
      reference: paymentReference,
      postDate: paymentPostDate,
      checkDate: paymentPostDate,
      depositDate: paymentPostDate,
      paymentType: isPatientPayment ? 'patient_payment' : 'insurance_payment',
      paymentMethod,
      codeType: 'CPT4',
      code: paymentCode,
      memo: paymentMemo,
      payAmount: Number(paymentPayAmount),
      adjustmentAmount: isPatientPayment ? 0 : Number(paymentAdjustmentAmount),
      accountCode: isPatientPayment ? '' : paymentReasonCode.replace('-', ''),
      reasonCode: isPatientPayment ? '' : paymentReasonCode,
      payerClaimNumber: isPatientPayment ? '' : paymentPayerClaimNumber,
    })

    setMutationMessage('Payment posting saved')
  }

  async function handleCollectionsFollowUp(item: CollectionsWorkQueueItem): Promise<CollectionsFollowUpMutationResponse> {
    setCollectionsFollowUpStatus('saving')
    setCollectionsFollowUpMessage(null)

    try {
      if (!sessionId) {
        throw new Error('Sign in to create collections follow-up tasks.')
      }
      const response = await createCollectionsFollowUp({
        patientId: item.pubpid,
        assignedTo: 'billing',
        action: item.recommendedAction,
        note: 'Created from the modernized Fees collections work queue.',
      }, sessionId)
      setCollectionsFollowUpStatus('saved')
      setCollectionsFollowUpMessage(`Created ${response.task.title} assigned to ${response.task.assignedTo}`)
      onPatientIdChange(response.task.pubpid)
      return response
    } catch (followUpError) {
      setCollectionsFollowUpStatus('error')
      const message = followUpError instanceof Error ? followUpError.message : 'Collections follow-up create failed'
      setCollectionsFollowUpMessage(message)
      throw followUpError
    }
  }

  async function handleBillingLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBillingLoginStatus('checking')
    setBillingLoginMessage(null)

    try {
      const result = await login({ username: billingLoginUsername, password: billingLoginPassword })
      if (result.authenticated && result.sessionId) {
        onBillingSessionActive(result.sessionId)
        setBillingLoginStatus('authenticated')
        setBillingLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setBillingLoginStatus('rejected')
        setBillingLoginMessage(result.failureReason ?? 'Billing access was rejected.')
      }
    } catch (error) {
      setBillingLoginStatus('error')
      setBillingLoginMessage(error instanceof Error ? error.message : 'Billing access check failed')
    }
  }

  async function handleStatementPdfDownload() {
    if (!statementDocument || !patientBilling || !sessionId) {
      return
    }

    setStatementPdfStatus('downloading')
    setStatementPdfError(null)
    try {
      const blob = await downloadBillingStatementPdf(patientBilling.pubpid, sessionId)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${statementDocument.statementNumber}.pdf`
      link.click()
      URL.revokeObjectURL(url)
      setStatementPdfStatus('ready')
    } catch (error) {
      setStatementPdfStatus('error')
      setStatementPdfError(error instanceof Error ? error.message : 'Billing statement PDF download failed')
    }
  }

  async function handleStatementBatchPackageDownload() {
    if (!sessionId) {
      return
    }

    setBatchPackageStatus('downloading')
    setBatchPackageError(null)
    try {
      const blob = await downloadStatementBatchPackage(5, sessionId)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = statementBatch
        ? `statement-batch-${statementBatch.asOfDate.replaceAll('-', '')}-top${statementBatch.candidates.length}.zip`
        : 'statement-batch.zip'
      link.click()
      URL.revokeObjectURL(url)
      setBatchPackageStatus('ready')
    } catch (error) {
      setBatchPackageStatus('error')
      setBatchPackageError(error instanceof Error ? error.message : 'Statement batch package download failed')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Fees search">
        {(!sessionId || billingAuthorizationError) && (
          <form className="mutation-form" aria-label="Billing access" onSubmit={handleBillingLogin}>
            <div className="panel-heading compact-heading">
              <ShieldCheck size={16} />
              <h3>Billing Access</h3>
            </div>
            <p className="form-help-text">Sign in to load fee sheet data.</p>
            <label>
              Username
              <input
                value={billingLoginUsername}
                onChange={(event) => setBillingLoginUsername(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={billingLoginPassword}
                onChange={(event) => setBillingLoginPassword(event.target.value)}
              />
            </label>
            <div className="detail-actions">
              <button className="icon-text-button primary" type="submit" disabled={billingLoginStatus === 'checking'}>
                <LogIn size={15} />
                {billingLoginStatus === 'checking' ? 'Checking' : 'Verify Billing Access'}
              </button>
            </div>
            {billingLoginMessage && (
              <div className={billingLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {billingLoginMessage}
              </div>
            )}
          </form>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Fees patient ID"
              placeholder="MOD-PAT-0001"
              disabled={feesLocked}
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
            <MetricRow label="Claims" value={claimCount} />
            <MetricRow label="Payments" value={paymentCount} />
            <MetricRow label="CPT lines" value={countBillingLinesByType(patientBilling.encounters, 'CPT4')} />
            <MetricRow label="Diagnosis lines" value={countBillingLinesByType(patientBilling.encounters, 'ICD10')} />
            <MetricRow label="Total fee" value={Math.round(totalFee)} />
            <MetricRow label="Balance" value={Math.round(accountSummary?.balanceAmount ?? totalFee)} />
            <MetricRow label="Aging total" value={Math.round(agingSummary?.totalBalanceAmount ?? 0)} />
            <MetricRow label="Ledger entries" value={ledgerEntries.length} />
            <MetricRow label="Statement ready" value={statementSummary ? 1 : 0} />
          </div>
        ) : feesLocked ? (
          <div className="empty-state">Sign in to load fee sheet data</div>
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
              <span>Modifier</span>
              <input
                value={billingModifier}
                onChange={(event) => setBillingModifier(event.target.value)}
                aria-label="New billing modifier"
                placeholder="25"
              />
            </label>
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
              disabled={feesLocked || isLoading || !patientBilling || patientBilling.encounters.length === 0}
            >
              <Check size={15} />
              Save CPT
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleDiagnosisLineSubmit}>
          <div className="panel-heading compact-heading">
            <Stethoscope size={16} />
            <h3>New Diagnosis Line</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={billingEncounter}
                onChange={(event) => setBillingEncounter(event.target.value)}
                aria-label="New diagnosis encounter"
                inputMode="numeric"
                required
              />
            </label>
            <label className="filter-field">
              <span>Date</span>
              <input
                value={billingDate}
                onChange={(event) => setBillingDate(event.target.value)}
                aria-label="New diagnosis date"
                required
              />
            </label>
            <label className="filter-field">
              <span>ICD10</span>
              <input
                value={diagnosisCode}
                onChange={(event) => setDiagnosisCode(event.target.value)}
                aria-label="New diagnosis ICD10 code"
                required
              />
            </label>
            <label className="filter-field">
              <span>Description</span>
              <input
                value={diagnosisText}
                onChange={(event) => setDiagnosisText(event.target.value)}
                aria-label="New diagnosis description"
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={feesLocked || isLoading || !patientBilling || patientBilling.encounters.length === 0}
            >
              <Check size={15} />
              Save Diagnosis
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleClaimStatusSubmit}>
          <div className="panel-heading compact-heading">
            <FileCheck2 size={16} />
            <h3>Claim Status</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={billingEncounter}
                onChange={(event) => setBillingEncounter(event.target.value)}
                aria-label="New claim encounter"
                inputMode="numeric"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Payer ID</span>
                <input
                  value={claimPayerId}
                  onChange={(event) => setClaimPayerId(event.target.value)}
                  aria-label="New claim payer ID"
                  inputMode="numeric"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Target</span>
                <input
                  value={claimTarget}
                  onChange={(event) => setClaimTarget(event.target.value)}
                  aria-label="New claim target"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Payer</span>
              <input
                value={claimPayerName}
                onChange={(event) => setClaimPayerName(event.target.value)}
                aria-label="New claim payer name"
                required
              />
            </label>
            <label className="filter-field">
              <span>Bill time</span>
              <input
                value={claimBillTime}
                onChange={(event) => setClaimBillTime(event.target.value)}
                aria-label="New claim bill time"
                required
              />
            </label>
            <label className="filter-field">
              <span>Payload</span>
              <input
                value={claimPayload}
                onChange={(event) => setClaimPayload(event.target.value)}
                aria-label="New claim payload"
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={feesLocked || isLoading || !patientBilling || patientBilling.encounters.length === 0}
            >
              <Check size={15} />
              Queue Claim
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handlePaymentPostingSubmit}>
          <div className="panel-heading compact-heading">
            <WalletCards size={16} />
            <h3>Payment Posting</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={billingEncounter}
                onChange={(event) => setBillingEncounter(event.target.value)}
                aria-label="New payment encounter"
                inputMode="numeric"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Source</span>
                <select
                  value={paymentSource}
                  onChange={(event) => setPaymentSource(event.target.value as 'insurance' | 'patient')}
                  aria-label="New payment source"
                >
                  <option value="insurance">Insurance</option>
                  <option value="patient">Patient</option>
                </select>
              </label>
              <label className="filter-field">
                <span>Post date</span>
                <input
                  value={paymentPostDate}
                  onChange={(event) => setPaymentPostDate(event.target.value)}
                  aria-label="New payment post date"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Payer ID</span>
                <input
                  value={paymentPayerId}
                  onChange={(event) => setPaymentPayerId(event.target.value)}
                  aria-label="New payment payer ID"
                  inputMode="numeric"
                  disabled={paymentSource === 'patient'}
                  required={paymentSource !== 'patient'}
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Payer</span>
              <input
                value={paymentPayerName}
                onChange={(event) => setPaymentPayerName(event.target.value)}
                aria-label="New payment payer name"
                disabled={paymentSource === 'patient'}
                required={paymentSource !== 'patient'}
              />
            </label>
            <label className="filter-field">
              <span>Reference</span>
              <input
                value={paymentReference}
                onChange={(event) => setPaymentReference(event.target.value)}
                aria-label="New payment reference"
                required
              />
            </label>
            <label className="filter-field">
              <span>Method</span>
              <select
                value={paymentMethod}
                onChange={(event) => setPaymentMethod(event.target.value)}
                aria-label="New payment method"
              >
                <option value="check_payment">Check</option>
                <option value="credit_card">Card</option>
                <option value="cash_payment">Cash</option>
              </select>
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Paid</span>
                <input
                  value={paymentPayAmount}
                  onChange={(event) => setPaymentPayAmount(event.target.value)}
                  aria-label="New payment amount"
                  inputMode="decimal"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Adjusted</span>
                <input
                  value={paymentAdjustmentAmount}
                  onChange={(event) => setPaymentAdjustmentAmount(event.target.value)}
                  aria-label="New payment adjustment amount"
                  inputMode="decimal"
                  disabled={paymentSource === 'patient'}
                  required={paymentSource !== 'patient'}
                />
              </label>
            </div>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>CPT</span>
                <input
                  value={paymentCode}
                  onChange={(event) => setPaymentCode(event.target.value)}
                  aria-label="New payment CPT code"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Reason</span>
                <input
                  value={paymentReasonCode}
                  onChange={(event) => setPaymentReasonCode(event.target.value)}
                  aria-label="New payment reason code"
                  disabled={paymentSource === 'patient'}
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Claim</span>
              <input
                value={paymentPayerClaimNumber}
                onChange={(event) => setPaymentPayerClaimNumber(event.target.value)}
                aria-label="New payment payer claim number"
                disabled={paymentSource === 'patient'}
              />
            </label>
            <label className="filter-field">
              <span>Memo</span>
              <input
                value={paymentMemo}
                onChange={(event) => setPaymentMemo(event.target.value)}
                aria-label="New payment memo"
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={feesLocked || isLoading || !patientBilling || patientBilling.encounters.length === 0}
            >
              <Check size={15} />
              Post Payment
            </button>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleBillingCorrectionSubmit}>
          <div className="panel-heading compact-heading">
            <Pencil size={16} />
            <h3>Correct Billing Line</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Line ID</span>
              <input
                value={correctionLineId}
                onChange={(event) => setCorrectionLineId(event.target.value)}
                aria-label="Billing correction line ID"
                required
              />
            </label>
            <label className="filter-field">
              <span>Description</span>
              <input
                value={correctionCodeText}
                onChange={(event) => setCorrectionCodeText(event.target.value)}
                aria-label="Billing correction description"
                required
              />
            </label>
            <label className="filter-field">
              <span>Modifier</span>
              <input
                value={correctionModifier}
                onChange={(event) => setCorrectionModifier(event.target.value)}
                aria-label="Billing correction modifier"
                placeholder="25"
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Fee</span>
                <input
                  value={correctionFee}
                  onChange={(event) => setCorrectionFee(event.target.value)}
                  aria-label="Billing correction fee"
                  inputMode="decimal"
                  required
                />
              </label>
              <label className="filter-field">
                <span>Units</span>
                <input
                  value={correctionUnits}
                  onChange={(event) => setCorrectionUnits(event.target.value)}
                  aria-label="Billing correction units"
                  inputMode="numeric"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Justify</span>
              <input
                value={correctionJustify}
                onChange={(event) => setCorrectionJustify(event.target.value)}
                aria-label="Billing correction justification"
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={feesLocked || isLoading || !patientBilling || !correctionLineId}
            >
              <Check size={15} />
              Save Correction
            </button>
          </div>
        </form>
      </section>

      <section className="appointment-detail-panel" aria-label="Fees detail">
        <StatementBatchPanel
          batch={statementBatch}
          status={statementBatchStatus}
          error={statementBatchError}
          disabled={feesLocked}
          downloadStatus={batchPackageStatus}
          downloadError={batchPackageError}
          onDownloadPackage={handleStatementBatchPackageDownload}
          onSelectCandidate={(candidate) => onPatientIdChange(candidate.pubpid)}
        />

        <CollectionsWorkQueuePanel
          queue={collectionsWorkQueue}
          status={collectionsWorkQueueStatus}
          error={collectionsWorkQueueError}
          followUpStatus={collectionsFollowUpStatus}
          followUpMessage={collectionsFollowUpMessage}
          disabled={feesLocked}
          onSelectItem={(item) => onPatientIdChange(item.pubpid)}
          onCreateFollowUp={handleCollectionsFollowUp}
        />

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
              <div className="portal-pill">Balance {formatCurrency(accountSummary?.balanceAmount ?? totalFee)}</div>
            </div>

            <div className="billing-detail-grid">
              <InfoPanel title="Billing Summary" icon={WalletCards}>
                <Field label="Patient ID" value={patientBilling.pubpid} />
                <Field label="Encounters" value={patientBilling.encounters.length} />
                <Field label="Billing lines" value={lineCount} />
                <Field label="Claims" value={claimCount} />
                <Field label="Payments" value={paymentCount} />
                <Field label="Diagnosis lines" value={countBillingLinesByType(patientBilling.encounters, 'ICD10')} />
                <Field label="Total fee" value={formatCurrency(totalFee)} />
              </InfoPanel>

              <InfoPanel title="Account Balance" icon={WalletCards}>
                <Field label="Charges" value={formatCurrency(accountSummary?.chargeAmount ?? totalFee)} />
                <Field label="Paid" value={formatCurrency(accountSummary?.paymentAmount ?? 0)} />
                <Field label="Adjusted" value={formatCurrency(accountSummary?.adjustmentAmount ?? 0)} />
                <Field label="Balance" value={formatCurrency(accountSummary?.balanceAmount ?? totalFee)} />
              </InfoPanel>

              <InfoPanel title="Aging Summary" icon={Clock}>
                <Field label="As of" value={agingSummary?.asOfDate ?? 'Not calculated'} />
                <Field label="Current" value={formatCurrency(agingSummary?.currentAmount ?? 0)} />
                <Field label="31-60" value={formatCurrency(agingSummary?.days31To60Amount ?? 0)} />
                <Field label="61-90" value={formatCurrency(agingSummary?.days61To90Amount ?? 0)} />
                <Field label="Over 90" value={formatCurrency(agingSummary?.over90Amount ?? 0)} />
                <Field label="Total balance" value={formatCurrency(agingSummary?.totalBalanceAmount ?? 0)} />
              </InfoPanel>

              <section className="info-panel statement-readiness-panel">
                <div className="panel-heading">
                  <Mail size={17} />
                  <h3>Statement Readiness</h3>
                </div>
                <div className="statement-readiness-body">
                  <div className="statement-status-row">
                    <span className="status-pill">{statementSummary?.statementStatus ?? 'Pending'}</span>
                    <strong>{formatCurrency(statementSummary?.balanceDueAmount ?? 0)}</strong>
                  </div>
                  <div className="statement-readiness-grid">
                    <Field label="Period" value={`${statementSummary?.statementPeriodStart ?? ''} to ${statementSummary?.statementPeriodEnd ?? ''}`} />
                    <Field label="Statement date" value={statementSummary?.statementDate} />
                    <Field label="Due date" value={statementSummary?.dueDate} />
                    <Field label="Open encounters" value={statementSummary?.openEncounterCount ?? 0} />
                    <Field label="Recipient" value={statementSummary?.recipientName} />
                    <Field label="Address" value={statementSummary?.mailingAddressLine1} />
                    <Field label="City/state" value={statementSummary?.mailingAddressLine2} />
                    <Field label="Phone" value={statementSummary?.phone} />
                    <Field label="Charges" value={formatCurrency(statementSummary?.chargeAmount ?? 0)} />
                    <Field label="Paid" value={formatCurrency(statementSummary?.paymentAmount ?? 0)} />
                    <Field label="Adjusted" value={formatCurrency(statementSummary?.adjustmentAmount ?? 0)} />
                    <Field label="Past due" value={formatCurrency(statementSummary?.pastDueAmount ?? 0)} />
                    <Field label="Current due" value={formatCurrency(statementSummary?.currentDueAmount ?? 0)} />
                    <Field label="Oldest open" value={statementSummary?.oldestOpenDate} />
                    <Field label="Oldest age" value={`${statementSummary?.oldestOpenAgeDays ?? 0} days`} />
                    <Field label="Ledger entries" value={statementSummary?.ledgerEntryCount ?? ledgerEntries.length} />
                  </div>
                </div>
              </section>

              <section className="info-panel statement-document-panel">
                <div className="panel-heading">
                  <FileText size={17} />
                  <h3>Patient Statement</h3>
                </div>
                <div className="statement-readiness-body">
                  <div className="statement-status-row">
                    <span className="status-pill">{statementDocument?.statementNumber ?? 'No statement'}</span>
                    <strong>{formatCurrency(statementDocument?.balanceDueAmount ?? 0)}</strong>
                  </div>
                  {statementDocument && (
                    <div className="statement-document-actions">
                      <button
                        className="icon-text-button secondary"
                        type="button"
                        disabled={feesLocked || statementPdfStatus === 'downloading'}
                        onClick={() => void handleStatementPdfDownload()}
                      >
                        <Download size={14} />
                        {statementPdfStatus === 'downloading' ? 'Preparing PDF' : 'PDF Export'}
                      </button>
                    </div>
                  )}
                  {statementPdfError && <div className="status-banner error">{statementPdfError}</div>}
                  <div className="statement-readiness-grid">
                    <Field label="Status" value={statementDocument?.statementStatus} />
                    <Field label="Period" value={`${statementDocument?.statementPeriodStart ?? ''} to ${statementDocument?.statementPeriodEnd ?? ''}`} />
                    <Field label="Statement date" value={statementDocument?.statementDate} />
                    <Field label="Due date" value={statementDocument?.dueDate} />
                    <Field label="Recipient" value={statementDocument?.recipientName} />
                    <Field label="Address" value={statementDocument?.mailingAddressLine1} />
                    <Field label="City/state" value={statementDocument?.mailingAddressLine2} />
                    <Field label="Lines" value={statementLineItems.length} />
                    <Field label="Charges" value={formatCurrency(statementDocument?.chargeAmount ?? 0)} />
                    <Field label="Paid" value={formatCurrency(statementDocument?.paymentAmount ?? 0)} />
                    <Field label="Adjusted" value={formatCurrency(statementDocument?.adjustmentAmount ?? 0)} />
                    <Field label="Balance due" value={formatCurrency(statementDocument?.balanceDueAmount ?? 0)} />
                  </div>
                  {statementDocument?.paymentInstructions && (
                    <div className="statement-payment-instructions">{statementDocument.paymentInstructions}</div>
                  )}
                  {statementDocument?.generatedText && (
                    <pre className="statement-document-preview">{statementDocument.generatedText}</pre>
                  )}
                </div>
                <div className="statement-document-line-list">
                  {statementLineItems.map((line) => (
                    <BillingStatementLineItemCard key={`${line.lineNumber}-${line.encounter}-${line.entryDate}`} line={line} />
                  ))}
                  {statementLineItems.length === 0 && <div className="timeline-placeholder">No patient statement lines</div>}
                </div>
              </section>

              <section className="info-panel billing-ledger-panel">
                <div className="panel-heading">
                  <ClipboardList size={17} />
                  <h3>Account Ledger</h3>
                </div>
                <div className="ledger-summary-grid">
                  <Field label="Entries" value={ledgerSummary?.entryCount ?? ledgerEntries.length} />
                  <Field label="First entry" value={ledgerSummary?.firstEntryDate} />
                  <Field label="Last entry" value={ledgerSummary?.lastEntryDate} />
                  <Field label="Charges" value={formatCurrency(ledgerSummary?.chargeAmount ?? 0)} />
                  <Field label="Paid" value={formatCurrency(ledgerSummary?.paymentAmount ?? 0)} />
                  <Field label="Adjusted" value={formatCurrency(ledgerSummary?.adjustmentAmount ?? 0)} />
                  <Field label="Ending balance" value={formatCurrency(ledgerSummary?.endingBalanceAmount ?? 0)} />
                </div>
                <div className="billing-ledger-list">
                  {ledgerEntries.map((entry) => (
                    <BillingLedgerEntryCard key={entry.entryId} entry={entry} />
                  ))}
                  {ledgerEntries.length === 0 && <div className="timeline-placeholder">No account ledger entries</div>}
                </div>
              </section>

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
                      disabled={isLoading || feesLocked}
                      onSelectCorrectionLine={handleSelectCorrectionLine}
                      onDeactivateLine={onDeactivateLine}
                      onDeleteLine={onDeleteLine}
                      onUpdateClaimStatus={onUpdateClaimStatus}
                      onDeleteClaim={onDeleteClaim}
                      onVoidPayment={onVoidPayment}
                      onDeletePayment={onDeletePayment}
                    />
                  ))}
                  {patientBilling.encounters.length === 0 && (
                    <div className="timeline-placeholder">No billing lines recorded</div>
                  )}
                </div>
              </section>
            </div>
          </>
        ) : feesLocked ? (
          <div className="empty-chart">Sign in to load fee sheet data</div>
        ) : status === 'loading' ? (
          <div className="empty-chart">Loading fee sheet</div>
        ) : (
          <div className="empty-chart">Enter a patient ID to load fee sheet lines</div>
        )}
      </section>
    </section>
  )
}

function StatementBatchPanel({
  batch,
  status,
  error,
  disabled,
  downloadStatus,
  downloadError,
  onDownloadPackage,
  onSelectCandidate,
}: {
  batch: StatementBatchResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  disabled: boolean
  downloadStatus: 'idle' | 'downloading' | 'ready' | 'error'
  downloadError: string | null
  onDownloadPackage: () => Promise<void>
  onSelectCandidate: (candidate: StatementBatchCandidate) => void
}) {
  const candidates = batch?.candidates ?? []

  return (
    <section className="info-panel statement-batch-panel" aria-label="Statement batch candidates">
      <div className="panel-heading">
        <Mail size={17} />
        <h3>Statement Batch</h3>
        <button
          className="icon-text-button secondary statement-batch-export"
          type="button"
          disabled={disabled || downloadStatus === 'downloading' || !batch}
          onClick={() => void onDownloadPackage()}
        >
          <Download size={14} />
          {downloadStatus === 'downloading' ? 'Preparing Batch' : 'Batch Export'}
        </button>
      </div>

      <div className="statement-batch-body">
        {status === 'error' && <div className="status-banner error">{error}</div>}
        {downloadError && <div className="status-banner error">{downloadError}</div>}
        <div className="statement-batch-summary">
          <Field label="Candidates" value={batch?.candidateCount ?? (status === 'loading' ? 'Loading' : 0)} />
          <Field label="Total balance" value={batch ? formatCurrency(batch.totalBalanceAmount) : status === 'loading' ? 'Loading' : 0} />
          <Field label="Past due" value={batch ? formatCurrency(batch.totalPastDueAmount) : status === 'loading' ? 'Loading' : 0} />
          <Field label="Current due" value={batch ? formatCurrency(batch.totalCurrentDueAmount) : status === 'loading' ? 'Loading' : 0} />
          <Field label="As of" value={batch?.asOfDate ?? (status === 'loading' ? 'Loading' : '')} />
        </div>

        <div className="statement-batch-list">
          {candidates.map((candidate) => (
            <article className="statement-batch-row" key={candidate.statementNumber}>
              <div className="statement-batch-row-main">
                <div>
                  <strong>{candidate.patientDisplayName}</strong>
                  <span>{candidate.pubpid} / {candidate.statementNumber}</span>
                </div>
                <div className="statement-batch-actions">
                  <span className="status-pill">{candidate.statementStatus}</span>
                  <button
                    className="icon-text-button secondary"
                    type="button"
                    onClick={() => onSelectCandidate(candidate)}
                  >
                    <Search size={14} />
                    Open
                  </button>
                </div>
              </div>
              <div className="statement-batch-row-grid">
                <Field label="Balance" value={formatCurrency(candidate.balanceDueAmount)} />
                <Field label="Past due" value={formatCurrency(candidate.pastDueAmount)} />
                <Field label="Due date" value={candidate.dueDate} />
                <Field label="Oldest age" value={`${candidate.oldestOpenAgeDays} days`} />
                <Field label="Open encounters" value={candidate.openEncounterCount} />
                <Field label="Ledger entries" value={candidate.ledgerEntryCount} />
                <Field label="Delivery" value={candidate.deliveryMethod} />
              </div>
            </article>
          ))}

          {disabled && <div className="timeline-placeholder">Sign in to load statement candidates</div>}
          {!disabled && status === 'loading' && <div className="timeline-placeholder">Loading statement candidates</div>}
          {status === 'ready' && candidates.length === 0 && (
            <div className="timeline-placeholder">No accounts are ready for statements</div>
          )}
        </div>
      </div>
    </section>
  )
}

function CollectionsWorkQueuePanel({
  queue,
  status,
  error,
  followUpStatus,
  followUpMessage,
  disabled,
  onSelectItem,
  onCreateFollowUp,
}: {
  queue: CollectionsWorkQueueResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  followUpStatus: 'idle' | 'saving' | 'saved' | 'error'
  followUpMessage: string | null
  disabled: boolean
  onSelectItem: (item: CollectionsWorkQueueItem) => void
  onCreateFollowUp: (item: CollectionsWorkQueueItem) => Promise<unknown>
}) {
  const items = queue?.items ?? []

  return (
    <section className="info-panel statement-batch-panel collections-work-queue-panel" aria-label="Collections work queue">
      <div className="panel-heading">
        <WalletCards size={17} />
        <h3>Collections Work Queue</h3>
      </div>

      <div className="statement-batch-body">
        {status === 'error' && <div className="status-banner error">{error}</div>}
        {followUpMessage && (
          <div className={followUpStatus === 'error' ? 'status-banner error' : 'status-banner success'}>
            {followUpMessage}
          </div>
        )}
        <div className="statement-batch-summary">
          <Field label="Accounts" value={queue?.accountCount ?? (status === 'loading' ? 'Loading' : 0)} />
          <Field label="High priority" value={queue?.highPriorityCount ?? (status === 'loading' ? 'Loading' : 0)} />
          <Field label="Past due" value={queue ? formatCurrency(queue.totalPastDueAmount) : 'Loading'} />
          <Field label="Over 90" value={queue ? formatCurrency(queue.totalOver90Amount) : 'Loading'} />
          <Field label="As of" value={queue?.asOfDate ?? 'Loading'} />
        </div>

        <div className="statement-batch-list">
          {items.map((item) => (
            <article className="statement-batch-row" key={`${item.pubpid}-${item.statementNumber}`}>
              <div className="statement-batch-row-main">
                <div>
                  <strong>{item.patientDisplayName}</strong>
                  <span>{item.pubpid} / {item.statementNumber}</span>
                </div>
                <div className="statement-batch-actions">
                  <span className="status-pill">{item.collectionTier}</span>
                  <button
                    className="icon-text-button secondary"
                    type="button"
                    disabled={disabled}
                    onClick={() => onSelectItem(item)}
                  >
                    <Search size={14} />
                    Open
                  </button>
                  <button
                    className="icon-text-button primary"
                    type="button"
                    disabled={disabled || followUpStatus === 'saving'}
                    onClick={() => void onCreateFollowUp(item)}
                  >
                    <ClipboardList size={14} />
                    Create Task
                  </button>
                </div>
              </div>
              <div className="statement-batch-row-grid">
                <Field label="Action" value={item.recommendedAction} />
                <Field label="Past due" value={formatCurrency(item.pastDueAmount)} />
                <Field label="Over 90" value={formatCurrency(item.over90Amount)} />
                <Field label="Balance" value={formatCurrency(item.balanceDueAmount)} />
                <Field label="Oldest age" value={`${item.oldestOpenAgeDays} days`} />
                <Field label="Due date" value={item.dueDate} />
                <Field label="Contact" value={item.contactMethod} />
              </div>
            </article>
          ))}

          {disabled && <div className="timeline-placeholder">Sign in to load collections work queue</div>}
          {!disabled && status === 'loading' && <div className="timeline-placeholder">Loading collections work queue</div>}
          {status === 'ready' && items.length === 0 && (
            <div className="timeline-placeholder">No accounts require collections follow-up</div>
          )}
        </div>
      </div>
    </section>
  )
}

function ProceduresWorkspace({
  patientId,
  procedureResults,
  status,
  error,
  orderCatalog,
  orderCatalogStatus,
  orderCatalogError,
  sessionId,
  onProceduresSessionActive,
  onPatientIdChange,
  onCreateOrder,
  onCompleteOrder,
  onUpdateOrder,
  onCreateReport,
  onUpdateReport,
  onSignReport,
  onReopenReportReview,
  onCreateSpecimen,
  onCreateResult,
  onUpdateResult,
  onDeleteOrder,
}: {
  patientId: string
  procedureResults: ProcedureResultsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  orderCatalog: ProcedureOrderCatalogResponse | null
  orderCatalogStatus: 'idle' | 'loading' | 'ready' | 'error'
  orderCatalogError: string | null
  sessionId: string | null
  onProceduresSessionActive: (sessionId: string) => void
  onPatientIdChange: (value: string) => void
  onCreateOrder: (input: ProcedureOrderCreateInput) => Promise<unknown>
  onCompleteOrder: (order: ProcedureOrderItem) => Promise<unknown>
  onUpdateOrder: (order: ProcedureOrderItem, input: ProcedureOrderUpdateInput) => Promise<unknown>
  onCreateReport: (input: ProcedureReportCreateInput) => Promise<unknown>
  onUpdateReport: (report: ProcedureReportItem, input: ProcedureReportUpdateInput) => Promise<unknown>
  onSignReport: (report: ProcedureReportItem, input: ProcedureReportSignInput) => Promise<unknown>
  onReopenReportReview: (report: ProcedureReportItem) => Promise<unknown>
  onCreateSpecimen: (input: ProcedureSpecimenCreateInput) => Promise<unknown>
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
  onDeleteOrder: (order: ProcedureOrderItem) => Promise<void>
}) {
  const [procedureEncounter, setProcedureEncounter] = useState('')
  const [procedureDate, setProcedureDate] = useState('2026-06-18')
  const [procedureCode, setProcedureCode] = useState('80053')
  const [procedureName, setProcedureName] = useState('Comprehensive metabolic panel')
  const [procedureDiagnosis, setProcedureDiagnosis] = useState('Z00.00')
  const [procedureInstructions, setProcedureInstructions] = useState('Collect fasting sample.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const [proceduresLoginUsername, setProceduresLoginUsername] = useState('admin')
  const [proceduresLoginPassword, setProceduresLoginPassword] = useState('pass')
  const [proceduresLoginStatus, setProceduresLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [proceduresLoginMessage, setProceduresLoginMessage] = useState<string | null>(null)
  const procedureCounts = procedureResults?.counts
  const scheduledOrders = procedureResults?.orders.filter(isScheduledProcedureOrder) ?? []
  const reportlessOrders = procedureResults?.orders.filter((order) => order.reports.length === 0) ?? []
  const reportCount = procedureCounts?.reports ?? countProcedureReports(procedureResults?.orders)
  const specimenCount = procedureCounts?.specimens ?? countProcedureSpecimens(procedureResults?.orders)
  const resultCount = procedureCounts?.results ?? countProcedureResults(procedureResults?.orders)
  const finalCount = procedureCounts?.finalResults ?? countProcedureResultsByStatus(procedureResults?.orders, 'final')
  const scheduledCount = procedureCounts?.scheduledOrders ?? scheduledOrders.length
  const reportlessCount = procedureCounts?.reportlessOrders ?? reportlessOrders.length
  const futureScheduledCount = procedureCounts?.futureScheduledOrders ?? scheduledOrders.length
  const catalogOrders = useMemo(
    () =>
      (orderCatalog?.items ?? [])
        .filter((item) => item.active && item.itemType === 'ord' && item.code)
        .slice(0, 6),
    [orderCatalog],
  )
  const isLoading = status === 'loading'
  const procedureAuthorizationError =
    (status === 'error' && Boolean(error?.includes('Procedure access'))) ||
    (orderCatalogStatus === 'error' && Boolean(orderCatalogError?.includes('Procedure access')))
  const proceduresLocked = !sessionId || procedureAuthorizationError

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
    if (proceduresLocked) {
      setMutationMessage('Sign in before saving procedure orders.')
      return
    }

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

  async function handleProceduresLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setProceduresLoginStatus('checking')
    setProceduresLoginMessage(null)

    try {
      const result = await login({ username: proceduresLoginUsername, password: proceduresLoginPassword })
      if (result.authenticated && result.sessionId) {
        onProceduresSessionActive(result.sessionId)
        setProceduresLoginStatus('authenticated')
        setProceduresLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setProceduresLoginStatus('rejected')
        setProceduresLoginMessage(result.failureReason ?? 'Procedure access was rejected.')
      }
    } catch (error) {
      setProceduresLoginStatus('error')
      setProceduresLoginMessage(error instanceof Error ? error.message : 'Procedure access check failed')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Procedure results search">
        {(!sessionId || procedureAuthorizationError) && (
          <form className="mutation-form" aria-label="Procedures access" onSubmit={handleProceduresLogin}>
            <div className="panel-heading compact-heading">
              <ShieldCheck size={16} />
              <h3>Procedures Access</h3>
            </div>
            <p className="form-help-text">Sign in to load procedure results, orders, reports, and specimens.</p>
            <label>
              Username
              <input
                value={proceduresLoginUsername}
                onChange={(event) => setProceduresLoginUsername(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={proceduresLoginPassword}
                onChange={(event) => setProceduresLoginPassword(event.target.value)}
              />
            </label>
            <div className="detail-actions">
              <button className="icon-text-button primary" type="submit" disabled={proceduresLoginStatus === 'checking'}>
                <LogIn size={15} />
                {proceduresLoginStatus === 'checking' ? 'Checking' : 'Verify Procedures Access'}
              </button>
            </div>
            {proceduresLoginMessage && (
              <div className={proceduresLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {proceduresLoginMessage}
              </div>
            )}
          </form>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Procedure patient ID"
              placeholder="MOD-PAT-0009"
              disabled={proceduresLocked}
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
            <MetricRow label="Specimens" value={specimenCount} />
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
                disabled={proceduresLocked}
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
                  disabled={proceduresLocked}
                  required
                />
              </label>
              <label className="filter-field">
                <span>Code</span>
                <input
                  value={procedureCode}
                  onChange={(event) => setProcedureCode(event.target.value)}
                  aria-label="New procedure code"
                  disabled={proceduresLocked}
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
                disabled={proceduresLocked}
                required
              />
            </label>
            <div className="procedure-order-catalog-picks" aria-label="Procedure order catalog picks">
              <span>Catalog</span>
              {orderCatalogStatus === 'error' && <em>{orderCatalogError}</em>}
              {orderCatalogStatus === 'loading' && <em>Loading catalog</em>}
              {catalogOrders.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className="catalog-pick-button"
                  disabled={proceduresLocked}
                  onClick={() => {
                    setProcedureCode(item.code ?? '')
                    setProcedureName(item.name)
                  }}
                >
                  {item.code} {item.name}
                </button>
              ))}
            </div>
            <label className="filter-field">
              <span>Diagnosis</span>
              <input
                value={procedureDiagnosis}
                onChange={(event) => setProcedureDiagnosis(event.target.value)}
                aria-label="New procedure diagnosis"
                disabled={proceduresLocked}
                required
              />
            </label>
            <label className="filter-field">
              <span>Instructions</span>
              <input
                value={procedureInstructions}
                onChange={(event) => setProcedureInstructions(event.target.value)}
                aria-label="New procedure instructions"
                disabled={proceduresLocked}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button
              className="icon-text-button primary"
              type="submit"
              disabled={proceduresLocked || isLoading || !procedureResults || !procedureEncounter}
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
                <Field label="Specimens" value={specimenCount} />
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
                      disabled={proceduresLocked || isLoading}
                      onComplete={onCompleteOrder}
                      onUpdate={onUpdateOrder}
                      onCreateReport={onCreateReport}
                      onCreateSpecimen={onCreateSpecimen}
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
                    disabled={proceduresLocked || isLoading}
                    onCreateResult={onCreateResult}
                    onUpdateReport={onUpdateReport}
                    onSignReport={onSignReport}
                    onReopenReportReview={onReopenReportReview}
                    onUpdateResult={onUpdateResult}
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
        ) : proceduresLocked ? (
          <div className="empty-chart">Sign in to load procedure results</div>
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
  sessionId,
  onPatientIdChange,
  onMessagesSessionActive,
  onCreateMessage,
  onCloseMessage,
  onUpdateMessageContent,
  onAssignMessage,
  onReplyMessage,
  onArchiveMessage,
  onDeleteMessage,
}: {
  patientId: string
  patientMessages: PatientMessagesResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onPatientIdChange: (value: string) => void
  onMessagesSessionActive: (sessionId: string) => void
  onCreateMessage: (input: PatientMessageCreateInput) => Promise<unknown>
  onCloseMessage: (message: PatientMessageItem) => Promise<unknown>
  onUpdateMessageContent: (message: PatientMessageItem, update: PatientMessageContentUpdateInput) => Promise<unknown>
  onAssignMessage: (message: PatientMessageItem, update: PatientMessageAssignmentUpdateInput) => Promise<unknown>
  onReplyMessage: (message: PatientMessageItem, reply: PatientMessageReplyInput) => Promise<unknown>
  onArchiveMessage: (message: PatientMessageItem) => Promise<unknown>
  onDeleteMessage: (message: PatientMessageItem) => Promise<void>
}) {
  const [messageTitle, setMessageTitle] = useState('Parity Message')
  const [messageBody, setMessageBody] = useState('Created from the modernized Messages workspace.')
  const [assignedTo, setAssignedTo] = useState('admin')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const [messagesLoginUsername, setMessagesLoginUsername] = useState('admin')
  const [messagesLoginPassword, setMessagesLoginPassword] = useState('pass')
  const [messagesLoginStatus, setMessagesLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [messagesLoginMessage, setMessagesLoginMessage] = useState<string | null>(null)
  const newCount = countMessagesByStatus(patientMessages?.messages, 'New')
  const doneCount = countMessagesByStatus(patientMessages?.messages, 'Done')
  const isLoading = status === 'loading'
  const messageAuthorizationError = status === 'error' && Boolean(error?.includes('Message access'))
  const messagesLocked = !sessionId || messageAuthorizationError

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

  async function handleMessagesLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessagesLoginStatus('checking')
    setMessagesLoginMessage(null)

    try {
      const result = await login({ username: messagesLoginUsername, password: messagesLoginPassword })
      if (result.authenticated && result.sessionId) {
        onMessagesSessionActive(result.sessionId)
        setMessagesLoginStatus('authenticated')
        setMessagesLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setMessagesLoginStatus('rejected')
        setMessagesLoginMessage(result.failureReason ?? 'Message access was rejected.')
      }
    } catch (error) {
      setMessagesLoginStatus('error')
      setMessagesLoginMessage(error instanceof Error ? error.message : 'Message access check failed')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Messages search">
        {(!sessionId || messageAuthorizationError) && (
          <form className="mutation-form" aria-label="Messages access" onSubmit={handleMessagesLogin}>
            <div className="panel-heading compact-heading">
              <ShieldCheck size={16} />
              <h3>Messages Access</h3>
            </div>
            <p className="form-help-text">Sign in to load patient messages.</p>
            <label>
              Username
              <input
                value={messagesLoginUsername}
                onChange={(event) => setMessagesLoginUsername(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={messagesLoginPassword}
                onChange={(event) => setMessagesLoginPassword(event.target.value)}
              />
            </label>
            <div className="detail-actions">
              <button className="icon-text-button primary" type="submit" disabled={messagesLoginStatus === 'checking'}>
                <LogIn size={15} />
                {messagesLoginStatus === 'checking' ? 'Checking' : 'Verify Messages Access'}
              </button>
            </div>
            {messagesLoginMessage && (
              <div className={messagesLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {messagesLoginMessage}
              </div>
            )}
          </form>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Messages patient ID"
              placeholder="MOD-PAT-0004"
              disabled={messagesLocked}
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
                disabled={messagesLocked}
                required
              />
            </label>
            <label className="filter-field">
              <span>Assigned To</span>
              <input
                value={assignedTo}
                onChange={(event) => setAssignedTo(event.target.value)}
                aria-label="New message assigned to"
                disabled={messagesLocked}
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
                disabled={messagesLocked}
                required
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={messagesLocked || isLoading}>
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
                      disabled={messagesLocked || isLoading}
                      onClose={onCloseMessage}
                      onUpdateContent={onUpdateMessageContent}
                      onAssign={onAssignMessage}
                      onReply={onReplyMessage}
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
        ) : messagesLocked ? (
          <div className="empty-chart">Sign in to load patient messages</div>
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
  includeArchived,
  sessionId,
  onPatientIdChange,
  onIncludeArchivedChange,
  onDocumentsSessionActive,
  onCreateDocument,
  onCreateBinaryDocument,
  onCreateExternalLinkDocument,
  onUpdateDocumentMetadata,
  onReplaceDocumentContent,
  onReplaceDocumentBinaryContent,
  onArchiveDocument,
  onRestoreDocument,
  onSignDocument,
  onDenyDocument,
  onDeleteDocument,
}: {
  patientId: string
  patientDocuments: PatientDocumentsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  includeArchived: boolean
  sessionId: string | null
  onPatientIdChange: (value: string) => void
  onIncludeArchivedChange: (value: boolean) => void
  onDocumentsSessionActive: (sessionId: string) => void
  onCreateDocument: (input: PatientDocumentCreateInput) => Promise<unknown>
  onCreateBinaryDocument: (input: PatientDocumentBinaryCreateInput) => Promise<unknown>
  onCreateExternalLinkDocument: (input: PatientDocumentExternalLinkCreateInput) => Promise<unknown>
  onUpdateDocumentMetadata: (
    document: PatientDocumentItem,
    input: PatientDocumentMetadataUpdateInput,
  ) => Promise<unknown>
  onReplaceDocumentContent: (
    document: PatientDocumentItem,
    input: PatientDocumentContentReplaceInput,
  ) => Promise<unknown>
  onReplaceDocumentBinaryContent: (
    document: PatientDocumentItem,
    input: PatientDocumentBinaryContentReplaceInput,
  ) => Promise<unknown>
  onArchiveDocument: (document: PatientDocumentItem) => Promise<unknown>
  onRestoreDocument: (document: PatientDocumentItem) => Promise<unknown>
  onSignDocument: (document: PatientDocumentItem) => Promise<unknown>
  onDenyDocument: (document: PatientDocumentItem) => Promise<unknown>
  onDeleteDocument: (document: PatientDocumentItem) => Promise<void>
}) {
  const [documentName, setDocumentName] = useState('Parity Document')
  const [documentCategoryId, setDocumentCategoryId] = useState('3')
  const [documentDate, setDocumentDate] = useState('2026-06-18')
  const [documentEncounter, setDocumentEncounter] = useState('1000013')
  const [documentContent, setDocumentContent] = useState('Created from the modernized Documents workspace.')
  const [binaryDocumentName, setBinaryDocumentName] = useState('Parity Binary Document')
  const [binaryDocumentCategoryId, setBinaryDocumentCategoryId] = useState('3')
  const [binaryDocumentDate, setBinaryDocumentDate] = useState('2026-06-18')
  const [binaryDocumentEncounter, setBinaryDocumentEncounter] = useState('1000013')
  const [binaryDocumentNotes, setBinaryDocumentNotes] = useState('Uploaded from the modernized Documents workspace.')
  const [binaryFileName, setBinaryFileName] = useState('')
  const [binaryMimeType, setBinaryMimeType] = useState('')
  const [binaryContentBase64, setBinaryContentBase64] = useState('')
  const [binaryFileMessage, setBinaryFileMessage] = useState('No file selected')
  const [linkDocumentName, setLinkDocumentName] = useState('External Referral Link')
  const [linkDocumentCategoryId, setLinkDocumentCategoryId] = useState('3')
  const [linkDocumentDate, setLinkDocumentDate] = useState('2026-06-18')
  const [linkDocumentEncounter, setLinkDocumentEncounter] = useState('1000013')
  const [linkDocumentUrl, setLinkDocumentUrl] = useState('https://example.test/legacy-ehr/external-record')
  const [linkDocumentNotes, setLinkDocumentNotes] = useState('Linked from the modernized Documents workspace.')
  const [mutationMessage, setMutationMessage] = useState<string | null>(null)
  const [documentsLoginUsername, setDocumentsLoginUsername] = useState('admin')
  const [documentsLoginPassword, setDocumentsLoginPassword] = useState('pass')
  const [documentsLoginStatus, setDocumentsLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [documentsLoginMessage, setDocumentsLoginMessage] = useState<string | null>(null)
  const [viewedDocument, setViewedDocument] = useState<PatientDocumentContentResponse | null>(null)
  const [documentContentStatus, setDocumentContentStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [documentContentError, setDocumentContentError] = useState<string | null>(null)
  const documents = patientDocuments?.documents ?? []
  const activeDocumentCount = documents.filter((document) => document.deleted === 0).length
  const archivedDocumentCount = documents.filter((document) => document.deleted !== 0).length
  const categories = useMemo(
    () => Array.from(new Set(documents.map((document) => document.categoryName))).sort(),
    [documents],
  )
  const linkedEncounterCount = documents.filter((document) => document.encounter).length
  const totalPages = documents.reduce((total, document) => total + (document.pages ?? 0), 0)
  const latestDocument = documents[0]
  const isLoading = status === 'loading'
  const documentAuthorizationError = status === 'error' && Boolean(error?.includes('Document access'))
  const documentsLocked = !sessionId || documentAuthorizationError

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

  async function handleBinaryFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      setBinaryFileName('')
      setBinaryMimeType('')
      setBinaryContentBase64('')
      setBinaryFileMessage('No file selected')
      return
    }

    const contentBase64 = await readFileAsBase64(file)
    setBinaryFileName(file.name)
    setBinaryMimeType(file.type || 'application/octet-stream')
    setBinaryContentBase64(contentBase64)
    setBinaryFileMessage(`${file.name} selected (${formatBytes(file.size)})`)
    if (binaryDocumentName === 'Parity Binary Document') {
      setBinaryDocumentName(file.name)
    }
  }

  async function handleBinaryDocumentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    const categoryId = Number(binaryDocumentCategoryId)
    const encounter = binaryDocumentEncounter.trim().length > 0 ? Number(binaryDocumentEncounter) : null
    if (!Number.isInteger(categoryId) || (encounter !== null && !Number.isInteger(encounter))) {
      setMutationMessage('Check numeric fields')
      return
    }

    if (!binaryContentBase64 || !binaryFileName || !binaryMimeType) {
      setMutationMessage('Choose a file to upload')
      return
    }

    await onCreateBinaryDocument({
      patientId,
      categoryId,
      name: binaryDocumentName,
      docDate: binaryDocumentDate,
      encounter,
      fileName: binaryFileName,
      mimetype: binaryMimeType,
      contentBase64: binaryContentBase64,
      notes: binaryDocumentNotes,
    })

    setMutationMessage('File uploaded')
  }

  async function handleExternalLinkDocumentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMutationMessage(null)

    const categoryId = Number(linkDocumentCategoryId)
    const encounter = linkDocumentEncounter.trim().length > 0 ? Number(linkDocumentEncounter) : null
    if (!Number.isInteger(categoryId) || (encounter !== null && !Number.isInteger(encounter))) {
      setMutationMessage('Check numeric fields')
      return
    }

    await onCreateExternalLinkDocument({
      patientId,
      categoryId,
      name: linkDocumentName,
      docDate: linkDocumentDate,
      encounter,
      url: linkDocumentUrl,
      notes: linkDocumentNotes,
    })

    setMutationMessage('External link saved')
  }

  async function handleDocumentsLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setDocumentsLoginStatus('checking')
    setDocumentsLoginMessage(null)

    try {
      const result = await login({ username: documentsLoginUsername, password: documentsLoginPassword })
      if (result.authenticated && result.sessionId) {
        onDocumentsSessionActive(result.sessionId)
        setDocumentsLoginStatus('authenticated')
        setDocumentsLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setDocumentsLoginStatus('rejected')
        setDocumentsLoginMessage(result.failureReason ?? 'Document access was rejected.')
      }
    } catch (error) {
      setDocumentsLoginStatus('error')
      setDocumentsLoginMessage(error instanceof Error ? error.message : 'Document access check failed')
    }
  }

  async function handleDocumentView(document: PatientDocumentItem) {
    if (!sessionId) {
      setDocumentContentStatus('error')
      setDocumentContentError('Sign in to load document content.')
      return
    }

    setDocumentContentStatus('loading')
    setDocumentContentError(null)

    try {
      const content = await getPatientDocumentContent(document.id, sessionId)
      setViewedDocument(content)
      setDocumentContentStatus('ready')
    } catch (error) {
      setViewedDocument(null)
      setDocumentContentStatus('error')
      setDocumentContentError(error instanceof Error ? error.message : 'Patient document content load failed')
    }
  }

  async function handleDocumentDownload(document: PatientDocumentItem | PatientDocumentContentResponse) {
    if (!sessionId) {
      setDocumentContentStatus('error')
      setDocumentContentError('Sign in to download documents.')
      return
    }

    try {
      const blob = await downloadPatientDocument(document.id, sessionId)
      const url = URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = document.fileName || document.name
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (error) {
      setDocumentContentStatus('error')
      setDocumentContentError(error instanceof Error ? error.message : 'Patient document download failed')
    }
  }

  function getDocumentInlineDataUri(document: PatientDocumentContentResponse) {
    if (!document.contentBase64) {
      return undefined
    }

    return `data:${document.mimetype || 'application/octet-stream'};base64,${document.contentBase64}`
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Documents search">
        {(!sessionId || documentAuthorizationError) && (
          <form className="mutation-form" aria-label="Documents access" onSubmit={handleDocumentsLogin}>
            <div className="panel-heading compact-heading">
              <ShieldCheck size={16} />
              <h3>Documents Access</h3>
            </div>
            <p className="form-help-text">Sign in to load patient documents.</p>
            <label>
              Username
              <input
                value={documentsLoginUsername}
                onChange={(event) => setDocumentsLoginUsername(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={documentsLoginPassword}
                onChange={(event) => setDocumentsLoginPassword(event.target.value)}
              />
            </label>
            <div className="detail-actions">
              <button className="icon-text-button primary" type="submit" disabled={documentsLoginStatus === 'checking'}>
                <LogIn size={15} />
                {documentsLoginStatus === 'checking' ? 'Checking' : 'Verify Documents Access'}
              </button>
            </div>
            {documentsLoginMessage && (
              <div className={documentsLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {documentsLoginMessage}
              </div>
            )}
          </form>
        )}

        <div className="filter-grid">
          <label className="filter-field">
            <span>Patient ID</span>
            <input
              value={patientId}
              onChange={(event) => onPatientIdChange(event.target.value)}
              aria-label="Documents patient ID"
              placeholder="MOD-PAT-0001"
              disabled={documentsLocked}
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={includeArchived}
              onChange={(event) => onIncludeArchivedChange(event.target.checked)}
              aria-label="Show archived documents"
              disabled={documentsLocked}
            />
            <span>Show archived documents</span>
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
              <MetricRow label="Active" value={activeDocumentCount} />
              <MetricRow label="Archived" value={archivedDocumentCount} />
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
                <Field label="Review status" value={latestDocument?.reviewStatus} />
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
            <button className="icon-text-button primary" type="submit" disabled={documentsLocked || isLoading}>
              <Check size={15} />
              Save Document
            </button>
            {mutationMessage && <span className="save-note">{mutationMessage}</span>}
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleBinaryDocumentSubmit}>
          <div className="panel-heading compact-heading">
            <Upload size={16} />
            <h3>Upload File</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Name</span>
              <input
                value={binaryDocumentName}
                onChange={(event) => setBinaryDocumentName(event.target.value)}
                aria-label="Binary document name"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Category</span>
                <select
                  value={binaryDocumentCategoryId}
                  onChange={(event) => setBinaryDocumentCategoryId(event.target.value)}
                  aria-label="Binary document category"
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
                  value={binaryDocumentDate}
                  onChange={(event) => setBinaryDocumentDate(event.target.value)}
                  aria-label="Binary document date"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={binaryDocumentEncounter}
                onChange={(event) => setBinaryDocumentEncounter(event.target.value)}
                aria-label="Binary document encounter"
                inputMode="numeric"
              />
            </label>
            <label className="filter-field">
              <span>File</span>
              <input type="file" onChange={handleBinaryFileChange} aria-label="Binary document file" required />
            </label>
            <label className="filter-field">
              <span>Notes</span>
              <textarea
                value={binaryDocumentNotes}
                onChange={(event) => setBinaryDocumentNotes(event.target.value)}
                aria-label="Binary document notes"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={isLoading}>
              <Upload size={15} />
              Upload File
            </button>
            <span className="save-note">{binaryFileMessage}</span>
          </div>
        </form>

        <form className="appointment-mutation-panel" onSubmit={handleExternalLinkDocumentSubmit}>
          <div className="panel-heading compact-heading">
            <ExternalLink size={16} />
            <h3>External Link</h3>
          </div>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Name</span>
              <input
                value={linkDocumentName}
                onChange={(event) => setLinkDocumentName(event.target.value)}
                aria-label="External link document name"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Category</span>
                <select
                  value={linkDocumentCategoryId}
                  onChange={(event) => setLinkDocumentCategoryId(event.target.value)}
                  aria-label="External link document category"
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
                  value={linkDocumentDate}
                  onChange={(event) => setLinkDocumentDate(event.target.value)}
                  aria-label="External link document date"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={linkDocumentEncounter}
                onChange={(event) => setLinkDocumentEncounter(event.target.value)}
                aria-label="External link document encounter"
                inputMode="numeric"
              />
            </label>
            <label className="filter-field">
              <span>URL</span>
              <input
                type="url"
                value={linkDocumentUrl}
                onChange={(event) => setLinkDocumentUrl(event.target.value)}
                aria-label="External link document URL"
                required
              />
            </label>
            <label className="filter-field">
              <span>Notes</span>
              <textarea
                value={linkDocumentNotes}
                onChange={(event) => setLinkDocumentNotes(event.target.value)}
                aria-label="External link document notes"
                rows={3}
              />
            </label>
          </div>
          <div className="detail-actions">
            <button className="icon-text-button primary" type="submit" disabled={documentsLocked || isLoading}>
              <ExternalLink size={15} />
              Save Link
            </button>
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
                <MetricRow label="Active" value={activeDocumentCount} />
                <MetricRow label="Archived" value={archivedDocumentCount} />
                <MetricRow label="Categories" value={categories.length} />
                <MetricRow label="Linked encounters" value={linkedEncounterCount} />
                <MetricRow label="Pages" value={totalPages} />
              </InfoPanel>

              <InfoPanel title="Latest Filing" icon={FileText}>
                <Field label="Name" value={latestDocument?.name} />
                <Field label="Category" value={latestDocument?.categoryName} />
                <Field label="Document date" value={latestDocument?.docDate} />
                <Field label="Uploaded" value={latestDocument?.uploadedAt} />
                <Field label="Revision" value={latestDocument?.versionLabel} />
                <Field label="Revision time" value={latestDocument?.revisionAt} />
                <Field label="Review status" value={latestDocument?.reviewStatus} />
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
                      <Field label="Category" value={viewedDocument.categoryName} />
                      <Field label="Document date" value={viewedDocument.docDate} />
                      <Field label="File" value={viewedDocument.fileName} />
                      <Field label="Revision" value={viewedDocument.versionLabel} />
                      <Field label="Revision time" value={viewedDocument.revisionAt} />
                      <Field label="Version history" value={`${viewedDocument.versionHistoryCount} current version`} />
                      <Field label="Scan status" value={viewedDocument.scanStatus} />
                      <Field label="Capture source" value={viewedDocument.captureSource} />
                      <Field label="Scan pages" value={viewedDocument.scanPageCount} />
                      <Field label="OCR status" value={viewedDocument.ocrStatus} />
                      <Field label="MIME" value={viewedDocument.mimetype} />
                      <Field label="Encounter" value={viewedDocument.encounter} />
                      <Field label="Storage" value={viewedDocument.storageMethod} />
                      <Field label="URL" value={viewedDocument.url} />
                      <Field label="Hash" value={viewedDocument.hash} />
                      <Field label="Notes" value={viewedDocument.notes} />
                      <Field label="Review status" value={viewedDocument.reviewStatus} />
                      <Field label="Reviewed by" value={viewedDocument.reviewedBy} />
                    </div>
                    <div className="document-lifecycle-readiness" aria-label={`Lifecycle for ${viewedDocument.name}`}>
                      {(viewedDocument.lifecycleEvents ?? []).map((event) => (
                        <div className="document-lifecycle-event" key={event.code}>
                          <strong>{event.label}</strong>
                          <span>{event.occurredAt || 'Current state'}</span>
                          <span>{event.actor ? `By ${event.actor}` : event.detail}</span>
                        </div>
                      ))}
                    </div>
                    {viewedDocument.storageMethod === 'web_url' && viewedDocument.url ? (
                      <div className="document-content-block">
                        <strong>{viewedDocument.url}</strong>
                        <span>{viewedDocument.content}</span>
                      </div>
                    ) : viewedDocument.previewKind === 'image' && viewedDocument.contentBase64 ? (
                      <div className="document-image-preview">
                        <img
                          alt={viewedDocument.name}
                          className="document-inline-image-preview"
                          src={`data:${viewedDocument.mimetype || 'application/octet-stream'};base64,${viewedDocument.contentBase64}`}
                        />
                        <div className="document-image-caption">
                          <strong>{viewedDocument.fileName}</strong>
                          <span>{viewedDocument.previewStatus}</span>
                          <span>{formatBytes(viewedDocument.sizeBytes)} stored as {viewedDocument.mimetype}</span>
                        </div>
                      </div>
                    ) : viewedDocument.previewKind === 'pdf' ? (
                      <div className="document-pdf-preview" aria-label={`PDF preview for ${viewedDocument.name}`}>
                        {getDocumentInlineDataUri(viewedDocument) ? (
                          <iframe
                            className="document-inline-pdf-preview"
                            src={getDocumentInlineDataUri(viewedDocument)}
                            title={`${viewedDocument.name} PDF preview`}
                          />
                        ) : (
                          <div className="timeline-placeholder">PDF preview requires document content</div>
                        )}
                        <div className="document-image-caption">
                          <strong>{viewedDocument.fileName}</strong>
                          <span>{viewedDocument.previewStatus}</span>
                          <span>{viewedDocument.content}</span>
                          <span>{formatBytes(viewedDocument.sizeBytes)} stored as {viewedDocument.mimetype}</span>
                        </div>
                      </div>
                    ) : viewedDocument.isBinary ? (
                      <div className="document-content-block">
                        <strong>{viewedDocument.fileName}</strong>
                        <span>{viewedDocument.content}</span>
                        <span>{formatBytes(viewedDocument.sizeBytes)} stored as {viewedDocument.mimetype}</span>
                      </div>
                    ) : (
                      <pre className="document-content-block">{viewedDocument.content}</pre>
                    )}
                    <div className="document-item-actions">
                      <button
                        className="icon-text-button secondary"
                        type="button"
                        onClick={() => void handleDocumentDownload(viewedDocument)}
                      >
                        <Download size={14} />
                        Download
                      </button>
                      {viewedDocument.storageMethod === 'web_url' && viewedDocument.url && (
                        <a className="icon-text-button secondary" href={viewedDocument.url} target="_blank" rel="noreferrer">
                          <ExternalLink size={14} />
                          Open Link
                        </a>
                      )}
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
                      onUpdateMetadata={onUpdateDocumentMetadata}
                      onReplaceContent={onReplaceDocumentContent}
                      onReplaceBinaryContent={onReplaceDocumentBinaryContent}
                      onArchive={onArchiveDocument}
                      onRestore={onRestoreDocument}
                      onSign={onSignDocument}
                      onDeny={onDenyDocument}
                      onDelete={onDeleteDocument}
                      onDownload={handleDocumentDownload}
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
  sessionId,
  onReportsSessionActive,
  labProviders,
  labProvidersStatus,
  labProvidersError,
  labProvidersIncludeInactive,
  orderCatalog,
  orderCatalogStatus,
  orderCatalogError,
  onLabProvidersIncludeInactiveChange,
  onCreateLabProvider,
  onUpdateLabProvider,
  onDeleteLabProvider,
  onCreateOrderCatalogItem,
  onUpdateOrderCatalogItem,
  onDeleteOrderCatalogItem,
  onImportOrderCatalogCompendium,
  orderQueue,
  orderQueueStatus,
  orderQueueError,
  orderQueueFilter,
  orderQueuePatientFilter,
  orderQueueProviderFilter,
  orderQueueLabFilter,
  orderQueueFromDate,
  orderQueueToDate,
  onOrderQueueFilterChange,
  onOrderQueuePatientFilterChange,
  onOrderQueueProviderFilterChange,
  onOrderQueueLabFilterChange,
  onOrderQueueFromDateChange,
  onOrderQueueToDateChange,
  onOrderQueueTransmit,
  reviewQueue,
  reviewQueueStatus,
  reviewQueueError,
  reviewQueueFilter,
  reviewQueuePatientFilter,
  reviewQueueProviderFilter,
  reviewQueueLabFilter,
  reviewQueueFromDate,
  reviewQueueToDate,
  onReviewQueueFilterChange,
  onReviewQueuePatientFilterChange,
  onReviewQueueProviderFilterChange,
  onReviewQueueLabFilterChange,
  onReviewQueueFromDateChange,
  onReviewQueueToDateChange,
  onReviewQueueBulkSign,
}: {
  reports: OperationalReportsResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  sessionId: string | null
  onReportsSessionActive: (sessionId: string) => void
  labProviders: ProcedureLabProviderDirectoryResponse | null
  labProvidersStatus: 'idle' | 'loading' | 'ready' | 'error'
  labProvidersError: string | null
  labProvidersIncludeInactive: boolean
  orderCatalog: ProcedureOrderCatalogResponse | null
  orderCatalogStatus: 'idle' | 'loading' | 'ready' | 'error'
  orderCatalogError: string | null
  onLabProvidersIncludeInactiveChange: (includeInactive: boolean) => void
  onCreateLabProvider: (input: ProcedureLabProviderMutationInput) => Promise<unknown>
  onUpdateLabProvider: (
    provider: ProcedureLabProviderItem,
    input: ProcedureLabProviderMutationInput,
  ) => Promise<unknown>
  onDeleteLabProvider: (provider: ProcedureLabProviderItem) => Promise<void>
  onCreateOrderCatalogItem: (input: ProcedureOrderCatalogMutationInput) => Promise<unknown>
  onUpdateOrderCatalogItem: (
    item: ProcedureOrderCatalogItem,
    input: ProcedureOrderCatalogMutationInput,
  ) => Promise<unknown>
  onDeleteOrderCatalogItem: (item: ProcedureOrderCatalogItem) => Promise<void>
  onImportOrderCatalogCompendium: (
    input: ProcedureOrderCatalogImportInput,
  ) => Promise<ProcedureOrderCatalogImportResponse>
  orderQueue: ProcedureOrderQueueResponse | null
  orderQueueStatus: 'idle' | 'loading' | 'ready' | 'error'
  orderQueueError: string | null
  orderQueueFilter: string
  orderQueuePatientFilter: string
  orderQueueProviderFilter: string
  orderQueueLabFilter: string
  orderQueueFromDate: string
  orderQueueToDate: string
  onOrderQueueFilterChange: (filter: string) => void
  onOrderQueuePatientFilterChange: (patientId: string) => void
  onOrderQueueProviderFilterChange: (providerId: string) => void
  onOrderQueueLabFilterChange: (labId: string) => void
  onOrderQueueFromDateChange: (fromDate: string) => void
  onOrderQueueToDateChange: (toDate: string) => void
  onOrderQueueTransmit: (order: ProcedureOrderQueueItem) => Promise<void>
  reviewQueue: ProcedureReportReviewQueueResponse | null
  reviewQueueStatus: 'idle' | 'loading' | 'ready' | 'error'
  reviewQueueError: string | null
  reviewQueueFilter: string
  reviewQueuePatientFilter: string
  reviewQueueProviderFilter: string
  reviewQueueLabFilter: string
  reviewQueueFromDate: string
  reviewQueueToDate: string
  onReviewQueueFilterChange: (filter: string) => void
  onReviewQueuePatientFilterChange: (patientId: string) => void
  onReviewQueueProviderFilterChange: (providerId: string) => void
  onReviewQueueLabFilterChange: (labId: string) => void
  onReviewQueueFromDateChange: (fromDate: string) => void
  onReviewQueueToDateChange: (toDate: string) => void
  onReviewQueueBulkSign: (reportIds: number[]) => Promise<ProcedureReportBulkSignResponse>
}) {
  const [reportsLoginUsername, setReportsLoginUsername] = useState('admin')
  const [reportsLoginPassword, setReportsLoginPassword] = useState('pass')
  const [reportsLoginStatus, setReportsLoginStatus] =
    useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [reportsLoginMessage, setReportsLoginMessage] = useState<string | null>(null)
  const [csvStatus, setCsvStatus] = useState<'idle' | 'downloading' | 'ready' | 'error'>('idle')
  const [csvError, setCsvError] = useState<string | null>(null)

  async function handleReportsLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setReportsLoginStatus('checking')
    setReportsLoginMessage(null)

    try {
      const result = await login({ username: reportsLoginUsername, password: reportsLoginPassword })
      if (result.authenticated && result.sessionId) {
        onReportsSessionActive(result.sessionId)
        setReportsLoginStatus('authenticated')
        setReportsLoginMessage(`Signed in as ${result.displayName}`)
      } else {
        setReportsLoginStatus('rejected')
        setReportsLoginMessage(result.failureReason ?? 'Reports access was rejected.')
      }
    } catch (error) {
      setReportsLoginStatus('error')
      setReportsLoginMessage(error instanceof Error ? error.message : 'Reports access check failed')
    }
  }

  async function handleCsvExport() {
    if (!sessionId) {
      setCsvStatus('error')
      setCsvError('Sign in before exporting operational reports.')
      return
    }

    setCsvStatus('downloading')
    setCsvError(null)

    try {
      const csv = await getOperationalReportsCsv(sessionId)
      const blob = new Blob([csv], { type: 'text/csv' })
      const href = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = href
      link.download = 'legacy-ehr-operational-report.csv'
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(href)
      setCsvStatus('ready')
    } catch (error) {
      setCsvStatus('error')
      setCsvError(error instanceof Error ? error.message : 'Operational reports CSV export failed')
    }
  }

  return (
    <section className="scheduler-layout">
      <section className="finder-panel" aria-label="Reports summary">
        <div className="result-meta">
          <span>{status === 'loading' ? 'Loading' : 'Operational reports'}</span>
          <span>Read only</span>
        </div>

        {status === 'error' && <div className="status-banner error">{error}</div>}

        {(!sessionId || status === 'error') && (
          <form className="mutation-form" aria-label="Reports access" onSubmit={handleReportsLogin}>
            <div className="panel-heading">
              <ShieldCheck size={17} />
              <h3>Reports Access</h3>
            </div>
            <label>
              Username
              <input value={reportsLoginUsername} onChange={(event) => setReportsLoginUsername(event.target.value)} />
            </label>
            <label>
              Password
              <input
                type="password"
                value={reportsLoginPassword}
                onChange={(event) => setReportsLoginPassword(event.target.value)}
              />
            </label>
            <button type="submit" disabled={reportsLoginStatus === 'checking'}>
              <LogIn size={15} />
              {reportsLoginStatus === 'checking' ? 'Checking' : 'Verify Reports Access'}
            </button>
            {reportsLoginMessage && (
              <div className={reportsLoginStatus === 'authenticated' ? 'status-banner' : 'status-banner error'}>
                {reportsLoginMessage}
              </div>
            )}
          </form>
        )}

        {sessionId && reports ? (
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
              <button
                type="button"
                className="icon-text-button secondary"
                onClick={() => void handleCsvExport()}
                disabled={csvStatus === 'downloading'}
              >
                <Download size={15} />
                {csvStatus === 'downloading' ? 'Preparing CSV' : 'CSV Export'}
              </button>
              {csvError && <div className="status-banner error">{csvError}</div>}
            </div>
          </>
        ) : (
          <div className="empty-state">
            {sessionId ? 'No operational reports loaded' : 'Sign in to load operational reports'}
          </div>
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

            <ProcedureLabProvidersPanel
              directory={labProviders}
              status={labProvidersStatus}
              error={labProvidersError}
              includeInactive={labProvidersIncludeInactive}
              onIncludeInactiveChange={onLabProvidersIncludeInactiveChange}
              onCreateProvider={onCreateLabProvider}
              onUpdateProvider={onUpdateLabProvider}
              onDeleteProvider={onDeleteLabProvider}
            />

            <ProcedureOrderCatalogPanel
              catalog={orderCatalog}
              status={orderCatalogStatus}
              error={orderCatalogError}
              onCreateItem={onCreateOrderCatalogItem}
              onUpdateItem={onUpdateOrderCatalogItem}
              onDeleteItem={onDeleteOrderCatalogItem}
              onImportCompendium={onImportOrderCatalogCompendium}
            />

            <ProcedureOrderQueuePanel
              queue={orderQueue}
              status={orderQueueStatus}
              error={orderQueueError}
              activeFilter={orderQueueFilter}
              patientFilter={orderQueuePatientFilter}
              providerFilter={orderQueueProviderFilter}
              labFilter={orderQueueLabFilter}
              fromDate={orderQueueFromDate}
              toDate={orderQueueToDate}
              onFilterChange={onOrderQueueFilterChange}
              onPatientFilterChange={onOrderQueuePatientFilterChange}
              onProviderFilterChange={onOrderQueueProviderFilterChange}
              onLabFilterChange={onOrderQueueLabFilterChange}
              onFromDateChange={onOrderQueueFromDateChange}
              onToDateChange={onOrderQueueToDateChange}
              onTransmitOrder={onOrderQueueTransmit}
            />

            <ProcedureReportReviewQueuePanel
              queue={reviewQueue}
              status={reviewQueueStatus}
              error={reviewQueueError}
              activeFilter={reviewQueueFilter}
              patientFilter={reviewQueuePatientFilter}
              providerFilter={reviewQueueProviderFilter}
              labFilter={reviewQueueLabFilter}
              fromDate={reviewQueueFromDate}
              toDate={reviewQueueToDate}
              onFilterChange={onReviewQueueFilterChange}
              onPatientFilterChange={onReviewQueuePatientFilterChange}
              onProviderFilterChange={onReviewQueueProviderFilterChange}
              onLabFilterChange={onReviewQueueLabFilterChange}
              onFromDateChange={onReviewQueueFromDateChange}
              onToDateChange={onReviewQueueToDateChange}
              onBulkSignReports={onReviewQueueBulkSign}
            />

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

function ProcedureOrderCatalogPanel({
  catalog,
  status,
  error,
  onCreateItem,
  onUpdateItem,
  onDeleteItem,
  onImportCompendium,
}: {
  catalog: ProcedureOrderCatalogResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  onCreateItem: (input: ProcedureOrderCatalogMutationInput) => Promise<unknown>
  onUpdateItem: (item: ProcedureOrderCatalogItem, input: ProcedureOrderCatalogMutationInput) => Promise<unknown>
  onDeleteItem: (item: ProcedureOrderCatalogItem) => Promise<void>
  onImportCompendium: (input: ProcedureOrderCatalogImportInput) => Promise<ProcedureOrderCatalogImportResponse>
}) {
  const [catalogDraft, setCatalogDraft] = useState<ProcedureOrderCatalogMutationInput>({
    parentId: 9040,
    labId: 504,
    name: '',
    code: '',
    itemType: 'ord',
    procedureTypeName: 'laboratory',
    description: '',
    specimen: 'blood',
    standardCode: '',
    sequence: 99,
    active: true,
  })
  const [importDraft, setImportDraft] = useState<ProcedureOrderCatalogImportInput>({
    vendorFormat: 'pathgroup',
    parentId: 9040,
    labId: 504,
    csvText: '',
  })
  const [lastImport, setLastImport] = useState<ProcedureOrderCatalogImportResponse | null>(null)
  const [pendingItemId, setPendingItemId] = useState<number | 'new' | 'import' | null>(null)
  const providerGroups = useMemo(
    () => (catalog?.items ?? []).filter((item) => item.itemType === 'grp' && item.parentId),
    [catalog],
  )
  const ordersByParent = useMemo(() => {
    const grouped = new Map<number, ProcedureOrderCatalogItem[]>()
    for (const item of catalog?.items ?? []) {
      if (item.itemType !== 'ord' || !item.parentId) {
        continue
      }
      grouped.set(item.parentId, [...(grouped.get(item.parentId) ?? []), item])
    }
    return grouped
  }, [catalog])
  const isBusy = status === 'loading' || pendingItemId !== null

  function handleCatalogGroupChange(parentId: number) {
    const group = providerGroups.find((item) => item.id === parentId)
    setCatalogDraft((current) => ({
      ...current,
      parentId,
      labId: group?.labId ?? current.labId,
    }))
  }

  function handleImportGroupChange(parentId: number) {
    const group = providerGroups.find((item) => item.id === parentId)
    setImportDraft((current) => ({
      ...current,
      parentId,
      labId: group?.labId ?? current.labId,
    }))
  }

  async function handleCatalogCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPendingItemId('new')
    try {
      await onCreateItem(catalogDraft)
      setCatalogDraft((current) => ({
        ...current,
        name: '',
        code: '',
        description: '',
        standardCode: '',
        active: true,
      }))
    } finally {
      setPendingItemId(null)
    }
  }

  async function handleCatalogActiveToggle(item: ProcedureOrderCatalogItem) {
    setPendingItemId(item.id)
    try {
      await onUpdateItem(item, catalogMutationFromItem(item, { active: !item.active }))
    } finally {
      setPendingItemId(null)
    }
  }

  async function handleCompendiumImport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPendingItemId('import')
    try {
      const response = await onImportCompendium(importDraft)
      setLastImport(response)
      setImportDraft((current) => ({ ...current, csvText: '' }))
    } finally {
      setPendingItemId(null)
    }
  }

  async function handleCatalogDelete(item: ProcedureOrderCatalogItem) {
    setPendingItemId(item.id)
    try {
      await onDeleteItem(item)
    } finally {
      setPendingItemId(null)
    }
  }

  return (
    <section className="info-panel procedure-order-catalog-panel" aria-label="Procedure order catalog">
      <div className="panel-heading">
        <ClipboardList size={17} />
        <h3>Procedure Order Catalog</h3>
        <span className="panel-count-pill">{catalog?.orderCount ?? 0}</span>
      </div>

      <div className="review-queue-metrics procedure-order-catalog-metrics" aria-label="Procedure order catalog counts">
        <span>{catalog?.labProviderCount ?? 0} labs</span>
        <span>{catalog?.groupCount ?? 0} groups</span>
        <span>{catalog?.orderCount ?? 0} orders</span>
      </div>

      {status === 'error' && <div className="status-banner error">{error}</div>}
      {status === 'loading' && <div className="timeline-placeholder">Loading procedure order catalog</div>}

      <form className="appointment-mutation-panel procedure-order-catalog-form" onSubmit={handleCatalogCreate}>
        <div className="mutation-grid two-column">
          <label className="contact-field">
            Provider group
            <select
              value={catalogDraft.parentId ?? ''}
              onChange={(event) => handleCatalogGroupChange(Number(event.target.value))}
            >
              {providerGroups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name}
                </option>
              ))}
            </select>
          </label>
          <label className="contact-field">
            Code
            <input
              required
              value={catalogDraft.code ?? ''}
              onChange={(event) =>
                setCatalogDraft((current) => ({
                  ...current,
                  code: event.target.value,
                  standardCode: event.target.value ? `CPT4:${event.target.value}` : current.standardCode,
                }))
              }
            />
          </label>
          <label className="contact-field">
            Name
            <input
              required
              value={catalogDraft.name}
              onChange={(event) => setCatalogDraft((current) => ({ ...current, name: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Specimen
            <input
              value={catalogDraft.specimen ?? ''}
              onChange={(event) => setCatalogDraft((current) => ({ ...current, specimen: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Standard code
            <input
              value={catalogDraft.standardCode ?? ''}
              onChange={(event) => setCatalogDraft((current) => ({ ...current, standardCode: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Sequence
            <input
              type="number"
              value={catalogDraft.sequence ?? 0}
              onChange={(event) => setCatalogDraft((current) => ({ ...current, sequence: Number(event.target.value) }))}
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={catalogDraft.active}
              onChange={(event) => setCatalogDraft((current) => ({ ...current, active: event.target.checked }))}
            />
            Active
          </label>
        </div>
        <label className="contact-field">
          Description
          <input
            value={catalogDraft.description ?? ''}
            onChange={(event) => setCatalogDraft((current) => ({ ...current, description: event.target.value }))}
          />
        </label>
        <div className="detail-actions">
          <button className="icon-text-button primary" type="submit" disabled={isBusy || providerGroups.length === 0}>
            <ClipboardList size={15} />
            Add Catalog Item
          </button>
        </div>
      </form>

      <form className="appointment-mutation-panel procedure-order-catalog-form" onSubmit={handleCompendiumImport}>
        <div className="mutation-grid two-column">
          <label className="contact-field">
            Vendor
            <select
              value={importDraft.vendorFormat}
              onChange={(event) => setImportDraft((current) => ({ ...current, vendorFormat: event.target.value }))}
            >
              <option value="pathgroup">PathGroup</option>
              <option value="ympg-dpmg">YPMG / DPMG</option>
            </select>
          </label>
          <label className="contact-field">
            Provider group
            <select value={importDraft.parentId} onChange={(event) => handleImportGroupChange(Number(event.target.value))}>
              {providerGroups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <label className="contact-field">
          CSV
          <textarea
            rows={5}
            value={importDraft.csvText}
            onChange={(event) => setImportDraft((current) => ({ ...current, csvText: event.target.value }))}
            placeholder="Order Code,Order Name,Result Code,Result Name"
            required
          />
        </label>
        <div className="detail-actions">
          <button className="icon-text-button primary" type="submit" disabled={isBusy || providerGroups.length === 0}>
            <Upload size={15} />
            Import Compendium
          </button>
          {lastImport && (
            <span className="save-note">
              Imported {lastImport.importedOrderCount} orders / {lastImport.importedResultCount} results
            </span>
          )}
        </div>
      </form>

      <div className="procedure-order-catalog-list">
        {providerGroups.map((group) => {
          const orders = ordersByParent.get(group.id) ?? []
          return (
            <article key={group.id} className="procedure-order-catalog-card">
              <div className="review-queue-card-main">
                <div>
                  <h4>{group.name}</h4>
                  <p>
                    #{group.labId} / {orders.length} orderable panels
                  </p>
                </div>
                <span className="portal-pill compact">{group.active ? 'Active' : 'Inactive'}</span>
              </div>
              <div className="procedure-order-catalog-orders">
                {orders.map((order) => (
                  <div key={order.id} className="procedure-order-catalog-row">
                    <div>
                      <span>{order.code}</span>
                      <strong>{order.name}</strong>
                      <em>{order.specimen || 'specimen pending'}</em>
                    </div>
                    <div className="procedure-order-catalog-row-actions">
                      <span className="portal-pill compact">{order.active ? 'Active' : 'Inactive'}</span>
                      <button
                        className="icon-text-button secondary"
                        type="button"
                        disabled={isBusy || pendingItemId === order.id}
                        onClick={() => void handleCatalogActiveToggle(order)}
                      >
                        {order.active ? <Ban size={14} /> : <RotateCcw size={14} />}
                        {order.active ? 'Deactivate' : 'Activate'}
                      </button>
                      <button
                        className="icon-text-button danger"
                        type="button"
                        disabled={isBusy || pendingItemId === order.id}
                        onClick={() => void handleCatalogDelete(order)}
                      >
                        <Trash2 size={14} />
                        Delete
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </article>
          )
        })}

        {status === 'ready' && providerGroups.length === 0 && (
          <div className="timeline-placeholder">No procedure order catalog rows loaded</div>
        )}
      </div>
    </section>
  )
}

function catalogMutationFromItem(
  item: ProcedureOrderCatalogItem,
  updates: Partial<ProcedureOrderCatalogMutationInput> = {},
): ProcedureOrderCatalogMutationInput {
  return {
    parentId: item.parentId,
    labId: item.labId,
    name: item.name,
    code: item.code,
    itemType: item.itemType,
    procedureTypeName: item.procedureTypeName,
    description: item.description,
    specimen: item.specimen,
    standardCode: item.standardCode,
    sequence: item.sequence,
    active: item.active,
    ...updates,
  }
}

function ProcedureLabProvidersPanel({
  directory,
  status,
  error,
  includeInactive,
  onIncludeInactiveChange,
  onCreateProvider,
  onUpdateProvider,
  onDeleteProvider,
}: {
  directory: ProcedureLabProviderDirectoryResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  includeInactive: boolean
  onIncludeInactiveChange: (includeInactive: boolean) => void
  onCreateProvider: (input: ProcedureLabProviderMutationInput) => Promise<unknown>
  onUpdateProvider: (
    provider: ProcedureLabProviderItem,
    input: ProcedureLabProviderMutationInput,
  ) => Promise<unknown>
  onDeleteProvider: (provider: ProcedureLabProviderItem) => Promise<void>
}) {
  const providers = directory?.providers ?? []
  const [providerDraft, setProviderDraft] = useState<ProcedureLabProviderMutationInput>({
    name: 'Slice 144 Temporary Lab',
    labDirectorId: null,
    npi: '1720123499',
    protocol: 'DL',
    usage: 'D',
    direction: 'B',
    sendApplicationId: '',
    sendFacilityId: '',
    receiveApplicationId: '',
    receiveFacilityId: '',
    remoteHost: '',
    login: '',
    password: '',
    ordersPath: '',
    resultsPath: '',
    notes: '',
    active: true,
  })
  const [pendingProviderId, setPendingProviderId] = useState<number | 'new' | null>(null)
  const isBusy = status === 'loading'

  async function handleCreateProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPendingProviderId('new')
    try {
      await onCreateProvider(providerDraft)
      setProviderDraft({
        name: 'Slice 144 Temporary Lab',
        labDirectorId: null,
        npi: '1720123499',
        protocol: 'DL',
        usage: 'D',
        direction: 'B',
        sendApplicationId: '',
        sendFacilityId: '',
        receiveApplicationId: '',
        receiveFacilityId: '',
        remoteHost: '',
        login: '',
        password: '',
        ordersPath: '',
        resultsPath: '',
        notes: '',
        active: true,
      })
    } finally {
      setPendingProviderId(null)
    }
  }

  async function handleProviderStatusToggle(provider: ProcedureLabProviderItem) {
    setPendingProviderId(provider.id)
    try {
      await onUpdateProvider(provider, {
        ...labProviderMutationFromProvider(provider),
        active: !provider.active,
      })
    } finally {
      setPendingProviderId(null)
    }
  }

  async function handleProviderDelete(provider: ProcedureLabProviderItem) {
    setPendingProviderId(provider.id)
    try {
      await onDeleteProvider(provider)
    } finally {
      setPendingProviderId(null)
    }
  }

  return (
    <section className="info-panel procedure-lab-provider-directory-panel" aria-label="Procedure lab provider directory">
      <div className="panel-heading">
        <FlaskConical size={17} />
        <h3>Procedure Lab Providers</h3>
        <span className="panel-count-pill">{providers.length}</span>
      </div>

      <label className="inline-toggle">
        <input
          type="checkbox"
          checked={includeInactive}
          onChange={(event) => onIncludeInactiveChange(event.target.checked)}
        />
        Include inactive providers
      </label>

      <div className="review-queue-metrics procedure-lab-provider-metrics" aria-label="Procedure lab provider counts">
        <span>{directory?.activeProviders ?? 0} active</span>
        <span>{directory?.inactiveProviders ?? 0} inactive</span>
        <span>{directory?.totalProviders ?? 0} total</span>
      </div>

      {status === 'error' && <div className="status-banner error">{error}</div>}

      <form className="appointment-mutation-panel procedure-lab-provider-form" onSubmit={handleCreateProvider}>
        <div className="mutation-grid two-column">
          <label className="contact-field">
            Lab provider name
            <input
              required
              value={providerDraft.name}
              onChange={(event) => setProviderDraft((current) => ({ ...current, name: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            NPI
            <input
              value={providerDraft.npi ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, npi: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Address book ID
            <input
              type="number"
              min="1"
              value={providerDraft.labDirectorId ?? ''}
              onChange={(event) => {
                const value = event.target.value.trim()
                setProviderDraft((current) => ({
                  ...current,
                  labDirectorId: value ? Number(value) : null,
                }))
              }}
            />
          </label>
          <label className="contact-field">
            Protocol
            <select
              value={providerDraft.protocol ?? 'DL'}
              onChange={(event) => setProviderDraft((current) => ({ ...current, protocol: event.target.value }))}
            >
              <option value="DL">Download</option>
              <option value="SFTP">SFTP</option>
              <option value="FS">Local Filesystem</option>
              <option value="WS">Web Service</option>
              <option value="DORN">Dorn</option>
            </select>
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={providerDraft.active}
              onChange={(event) => setProviderDraft((current) => ({ ...current, active: event.target.checked }))}
            />
            Active
          </label>
        </div>
        <div className="mutation-grid two-column">
          <label className="contact-field">
            Usage
            <select
              value={providerDraft.usage ?? 'D'}
              onChange={(event) => setProviderDraft((current) => ({ ...current, usage: event.target.value }))}
            >
              <option value="D">Debugging</option>
              <option value="P">Production</option>
              <option value="T">Quest Cert Testing</option>
              <option value="Q">Quest Cert Debug</option>
            </select>
          </label>
          <label className="contact-field">
            Direction
            <select
              value={providerDraft.direction ?? 'B'}
              onChange={(event) => setProviderDraft((current) => ({ ...current, direction: event.target.value }))}
            >
              <option value="B">Bidirectional</option>
              <option value="R">Results Only</option>
            </select>
          </label>
          <label className="contact-field">
            Sender application
            <input
              value={providerDraft.sendApplicationId ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, sendApplicationId: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Sender facility
            <input
              value={providerDraft.sendFacilityId ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, sendFacilityId: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Receiver application
            <input
              value={providerDraft.receiveApplicationId ?? ''}
              onChange={(event) =>
                setProviderDraft((current) => ({ ...current, receiveApplicationId: event.target.value }))
              }
            />
          </label>
          <label className="contact-field">
            Receiver facility
            <input
              value={providerDraft.receiveFacilityId ?? ''}
              onChange={(event) =>
                setProviderDraft((current) => ({ ...current, receiveFacilityId: event.target.value }))
              }
            />
          </label>
          <label className="contact-field">
            Remote host
            <input
              value={providerDraft.remoteHost ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, remoteHost: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Login
            <input
              value={providerDraft.login ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, login: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Password
            <input
              type="password"
              value={providerDraft.password ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, password: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Orders path
            <input
              value={providerDraft.ordersPath ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, ordersPath: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Results path
            <input
              value={providerDraft.resultsPath ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, resultsPath: event.target.value }))}
            />
          </label>
          <label className="contact-field">
            Notes
            <input
              value={providerDraft.notes ?? ''}
              onChange={(event) => setProviderDraft((current) => ({ ...current, notes: event.target.value }))}
            />
          </label>
        </div>
        <div className="detail-actions">
          <button className="icon-text-button primary" type="submit" disabled={isBusy || pendingProviderId === 'new'}>
            <UserPlus size={15} />
            Add Provider
          </button>
        </div>
      </form>

      <div className="review-queue-list procedure-lab-provider-list">
        {providers.map((provider) => (
          <article key={provider.id} className="review-queue-card procedure-lab-provider-card">
            <div className="review-queue-card-main">
              <div>
                <p className="eyebrow">Provider #{provider.id}</p>
                <h4>{provider.name}</h4>
                <p>{provider.npi ? `NPI ${provider.npi}` : 'NPI not recorded'}</p>
              </div>
              <div className="statement-batch-actions">
                <span className="status-pill">{provider.active ? 'Active' : 'Inactive'}</span>
                <button
                  className="icon-text-button secondary"
                  type="button"
                  disabled={isBusy || pendingProviderId === provider.id}
                  onClick={() => void handleProviderStatusToggle(provider)}
                >
                  {provider.active ? <Ban size={14} /> : <RotateCcw size={14} />}
                  {provider.active ? 'Deactivate' : 'Activate'}
                </button>
                <button
                  className="icon-text-button danger"
                  type="button"
                  disabled={isBusy || pendingProviderId === provider.id}
                  onClick={() => void handleProviderDelete(provider)}
                >
                  <Trash2 size={14} />
                  Delete
                </button>
              </div>
            </div>
            <div className="review-queue-card-grid">
              <Field label="Address book organization" value={provider.labDirectorName || 'Not linked'} />
              <Field label="Address book type" value={provider.labDirectorType || 'Not linked'} />
              <Field label="Protocol" value={provider.protocol} />
              <Field label="Usage" value={formatLabProviderUsage(provider.usage)} />
              <Field label="Direction" value={formatLabProviderDirection(provider.direction)} />
              <Field label="Sender" value={formatLabProviderPair(provider.sendApplicationId, provider.sendFacilityId)} />
              <Field
                label="Receiver"
                value={formatLabProviderPair(provider.receiveApplicationId, provider.receiveFacilityId)}
              />
              <Field label="Remote host" value={provider.remoteHost || 'Not set'} />
              <Field label="Login" value={provider.login || 'Not set'} />
              <Field label="Password" value={provider.password ? 'Stored' : 'Not set'} />
              <Field label="Orders path" value={provider.ordersPath || 'Not set'} />
              <Field label="Results path" value={provider.resultsPath || 'Not set'} />
              <Field label="Orders" value={provider.orderCount} />
              <Field label="Reports" value={provider.reportCount} />
              <Field label="Future orders" value={provider.futureOrderCount} />
            </div>
            {provider.notes && <p className="review-queue-notes">{provider.notes}</p>}
          </article>
        ))}
        {status === 'loading' && <div className="timeline-placeholder">Loading procedure lab providers</div>}
        {status !== 'loading' && providers.length === 0 && <div className="timeline-placeholder">No procedure lab providers</div>}
      </div>
    </section>
  )
}

function labProviderMutationFromProvider(provider: ProcedureLabProviderItem): ProcedureLabProviderMutationInput {
  return {
    name: provider.name,
    labDirectorId: provider.labDirectorId,
    npi: provider.npi,
    protocol: provider.protocol ?? 'DL',
    usage: provider.usage ?? 'D',
    direction: provider.direction ?? 'B',
    sendApplicationId: provider.sendApplicationId,
    sendFacilityId: provider.sendFacilityId,
    receiveApplicationId: provider.receiveApplicationId,
    receiveFacilityId: provider.receiveFacilityId,
    remoteHost: provider.remoteHost,
    login: provider.login,
    password: provider.password,
    ordersPath: provider.ordersPath,
    resultsPath: provider.resultsPath,
    notes: provider.notes,
    active: provider.active,
  }
}

function formatLabProviderUsage(usage?: string | null) {
  switch ((usage ?? 'D').toUpperCase()) {
    case 'P':
      return 'Production'
    case 'T':
      return 'Quest Cert Testing'
    case 'Q':
      return 'Quest Cert Debug'
    default:
      return 'Debugging'
  }
}

function formatLabProviderDirection(direction?: string | null) {
  return (direction ?? 'B').toUpperCase() === 'R' ? 'Results Only' : 'Bidirectional'
}

function formatLabProviderPair(first?: string | null, second?: string | null) {
  const parts = [first, second].filter((part): part is string => Boolean(part))
  return parts.length > 0 ? parts.join(' / ') : 'Not set'
}

function formatProcedureOrderQueueState(value?: string | null) {
  switch ((value ?? '').toLowerCase()) {
    case 'reported':
      return 'Reported'
    case 'transmitted-pending':
      return 'Sent, awaiting results'
    case 'ready-to-send':
      return 'Ready to send'
    default:
      return value || 'Queued'
  }
}

const procedureOrderQueueFilters = [
  { id: 'ready-to-send', label: 'Ready to send' },
  { id: 'transmitted-pending', label: 'Sent, awaiting results' },
  { id: 'reported', label: 'Reported' },
  { id: 'scheduled', label: 'Scheduled' },
  { id: 'completed', label: 'Completed' },
  { id: 'all', label: 'All' },
]

function ProcedureOrderQueuePanel({
  queue,
  status,
  error,
  activeFilter,
  patientFilter,
  providerFilter,
  labFilter,
  fromDate,
  toDate,
  onFilterChange,
  onPatientFilterChange,
  onProviderFilterChange,
  onLabFilterChange,
  onFromDateChange,
  onToDateChange,
  onTransmitOrder,
}: {
  queue: ProcedureOrderQueueResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  activeFilter: string
  patientFilter: string
  providerFilter: string
  labFilter: string
  fromDate: string
  toDate: string
  onFilterChange: (filter: string) => void
  onPatientFilterChange: (patientId: string) => void
  onProviderFilterChange: (providerId: string) => void
  onLabFilterChange: (labId: string) => void
  onFromDateChange: (fromDate: string) => void
  onToDateChange: (toDate: string) => void
  onTransmitOrder: (order: ProcedureOrderQueueItem) => Promise<void>
}) {
  const orders = queue?.orders ?? []

  return (
    <section className="info-panel procedure-order-queue-panel" aria-label="Procedure order queue">
      <div className="panel-heading">
        <FlaskConical size={17} />
        <h3>Procedure Order Queue</h3>
      </div>

      <div className="report-review-queue-toolbar">
        <div className="segmented-control procedure-order-queue-segments" aria-label="Procedure order queue filter">
          {procedureOrderQueueFilters.map((filter) => (
            <button
              key={filter.id}
              type="button"
              className={filter.id === activeFilter ? 'active' : ''}
              aria-pressed={filter.id === activeFilter}
              onClick={() => onFilterChange(filter.id)}
            >
              {filter.label}
            </button>
          ))}
        </div>
        <div className="review-queue-metrics" aria-label="Procedure order queue counts">
          <span>{queue?.readyToSendOrders ?? 0} ready</span>
          <span>{queue?.reportedOrders ?? 0} reported</span>
          <span>{queue?.totalOrders ?? 0} total</span>
        </div>
      </div>

      <div className="review-queue-filter-grid">
        <label>
          Patient
          <input
            type="search"
            value={patientFilter}
            placeholder="MOD-PAT-0009"
            onChange={(event) => onPatientFilterChange(event.target.value)}
          />
        </label>
        <label>
          Provider
          <input
            type="number"
            min="1"
            value={providerFilter}
            placeholder="101"
            onChange={(event) => onProviderFilterChange(event.target.value)}
          />
        </label>
        <label>
          Lab
          <input
            type="number"
            min="1"
            value={labFilter}
            placeholder="501"
            onChange={(event) => onLabFilterChange(event.target.value)}
          />
        </label>
        <label>
          From
          <input type="date" value={fromDate} onChange={(event) => onFromDateChange(event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={toDate} onChange={(event) => onToDateChange(event.target.value)} />
        </label>
      </div>

      {status === 'error' && <div className="status-banner error">{error}</div>}

      <div className="review-queue-list">
        {orders.map((order) => (
          <article key={order.orderId} className="review-queue-card">
            <div className="review-queue-card-main">
              <div>
                <p className="eyebrow">Order #{order.orderId}</p>
                <h4>{order.procedureName || 'Unnamed procedure'}</h4>
                <p>
                  {order.patientDisplayName} / {order.pubpid} / {order.encounterId ? `Encounter #${order.encounterId}` : 'No encounter'}
                </p>
              </div>
              <span className="status-pill">{formatProcedureOrderQueueState(order.queueState)}</span>
            </div>
            <div className="review-queue-card-grid">
              <Field label="Order date" value={order.orderDate} />
              <Field label="Code" value={order.procedureCode} />
              <Field label="Order status" value={order.orderStatus} />
              <Field label="Priority" value={order.orderPriority} />
              <Field
                label="Provider"
                value={order.providerId ? `${order.providerName || 'Provider'} #${order.providerId}` : order.providerName}
              />
              <Field label="Lab" value={order.labId ? `${order.labName || 'Lab'} #${order.labId}` : order.labName} />
              <Field label="Reports" value={order.reportCount} />
              <Field label="Results" value={order.resultCount} />
              <Field label="Specimens" value={order.specimenCount} />
              <Field label="Transmit" value={order.canTransmit ? 'Ready' : order.dateTransmitted || 'Not needed'} />
            </div>
            {order.canTransmit && (
              <div className="queue-card-actions">
                <button
                  type="button"
                  className="icon-text-button secondary compact"
                  onClick={() => {
                    void onTransmitOrder(order)
                  }}
                >
                  <Check size={14} />
                  Mark sent
                </button>
              </div>
            )}
            {order.instructions && <p className="review-queue-notes">{order.instructions}</p>}
          </article>
        ))}
        {status === 'loading' && <div className="timeline-placeholder">Loading procedure order queue</div>}
        {status !== 'loading' && orders.length === 0 && <div className="timeline-placeholder">No matching orders</div>}
      </div>
    </section>
  )
}

const procedureReportReviewQueueFilters = [
  { id: 'unreviewed', label: 'Received, unreviewed' },
  { id: 'reviewed', label: 'Reviewed' },
  { id: 'all', label: 'All' },
]

function ProcedureReportReviewQueuePanel({
  queue,
  status,
  error,
  activeFilter,
  patientFilter,
  providerFilter,
  labFilter,
  fromDate,
  toDate,
  onFilterChange,
  onPatientFilterChange,
  onProviderFilterChange,
  onLabFilterChange,
  onFromDateChange,
  onToDateChange,
  onBulkSignReports,
}: {
  queue: ProcedureReportReviewQueueResponse | null
  status: 'idle' | 'loading' | 'ready' | 'error'
  error: string | null
  activeFilter: string
  patientFilter: string
  providerFilter: string
  labFilter: string
  fromDate: string
  toDate: string
  onFilterChange: (filter: string) => void
  onPatientFilterChange: (patientId: string) => void
  onProviderFilterChange: (providerId: string) => void
  onLabFilterChange: (labId: string) => void
  onFromDateChange: (fromDate: string) => void
  onToDateChange: (toDate: string) => void
  onBulkSignReports: (reportIds: number[]) => Promise<ProcedureReportBulkSignResponse>
}) {
  const reports = queue?.reports ?? []
  const unreviewedReports = reports.filter((report) => (report.reviewStatus ?? '').toLowerCase() !== 'reviewed')
  const canBulkSign = activeFilter === 'unreviewed' && unreviewedReports.length > 0
  const [bulkSignStatus, setBulkSignStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [bulkSignCount, setBulkSignCount] = useState(0)

  async function handleBulkSign() {
    if (!canBulkSign) {
      return
    }

    setBulkSignStatus('saving')
    try {
      const response = await onBulkSignReports(unreviewedReports.map((report) => report.reportId))
      setBulkSignCount(response.signedCount)
      setBulkSignStatus('saved')
    } catch {
      setBulkSignStatus('error')
    }
  }

  return (
    <section className="info-panel report-review-queue-panel" aria-label="Procedure report review queue">
      <div className="panel-heading">
        <FileCheck2 size={17} />
        <h3>Procedure Report Review Queue</h3>
      </div>

      <div className="report-review-queue-toolbar">
        <div className="segmented-control" aria-label="Procedure report review queue filter">
          {procedureReportReviewQueueFilters.map((filter) => (
            <button
              key={filter.id}
              type="button"
              className={filter.id === activeFilter ? 'active' : ''}
              aria-pressed={filter.id === activeFilter}
              onClick={() => onFilterChange(filter.id)}
            >
              {filter.label}
            </button>
          ))}
        </div>
        <div className="review-queue-metrics" aria-label="Procedure report review queue counts">
          <span>{queue?.unreviewedReports ?? 0} unreviewed</span>
          <span>{queue?.reviewedReports ?? 0} reviewed</span>
          <span>{queue?.totalReports ?? 0} total</span>
        </div>
        <button
          type="button"
          className="icon-text-button secondary compact"
          disabled={!canBulkSign || bulkSignStatus === 'saving'}
          onClick={() => {
            void handleBulkSign()
          }}
        >
          <ShieldCheck size={14} />
          Sign visible
        </button>
        {bulkSignStatus === 'saved' && <span className="save-note">Signed {bulkSignCount}</span>}
        {bulkSignStatus === 'error' && <span className="save-note error">Sign failed</span>}
      </div>

      <div className="review-queue-filter-grid">
        <label>
          Patient
          <input
            type="search"
            value={patientFilter}
            placeholder="MOD-PAT-0009"
            onChange={(event) => onPatientFilterChange(event.target.value)}
          />
        </label>
        <label>
          Provider
          <input
            type="number"
            min="1"
            value={providerFilter}
            placeholder="101"
            onChange={(event) => onProviderFilterChange(event.target.value)}
          />
        </label>
        <label>
          Lab
          <input
            type="number"
            min="1"
            value={labFilter}
            placeholder="501"
            onChange={(event) => onLabFilterChange(event.target.value)}
          />
        </label>
        <label>
          From
          <input type="date" value={fromDate} onChange={(event) => onFromDateChange(event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={toDate} onChange={(event) => onToDateChange(event.target.value)} />
        </label>
      </div>

      {status === 'error' && <div className="status-banner error">{error}</div>}

      <div className="review-queue-list">
        {reports.map((report) => (
          <article key={report.reportId} className="review-queue-card">
            <div className="review-queue-card-main">
              <div>
                <p className="eyebrow">Report #{report.reportId}</p>
                <h4>{report.procedureName || 'Unnamed procedure'}</h4>
                <p>
                  {report.patientDisplayName} / {report.pubpid} / Order #{report.orderId}
                </p>
              </div>
              <span className="status-pill">{report.reviewStatus || 'received'}</span>
            </div>
            <div className="review-queue-card-grid">
              <Field label="Order date" value={report.orderDate} />
              <Field label="Report date" value={report.reportDate} />
              <Field label="Code" value={report.procedureCode} />
              <Field label="Report status" value={report.reportStatus} />
              <Field label="Specimen" value={report.specimenNumber} />
              <Field
                label="Provider"
                value={report.providerId ? `${report.providerName || 'Provider'} #${report.providerId}` : report.providerName}
              />
              <Field label="Lab" value={report.labId ? `${report.labName || 'Lab'} #${report.labId}` : report.labName} />
              <Field label="Reviewed by" value={report.reviewedBy || 'Not reviewed'} />
              <Field label="Reviewed at" value={report.reviewedAt || 'No review time'} />
            </div>
            {report.notes && <p className="review-queue-notes">{report.notes}</p>}
          </article>
        ))}
        {status === 'loading' && <div className="timeline-placeholder">Loading procedure report review queue</div>}
        {status !== 'loading' && reports.length === 0 && <div className="timeline-placeholder">No matching reports</div>}
      </div>
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
  onAdminSessionActive,
  onAdminSessionEnded,
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
  onAdminSessionActive: (sessionId: string) => void
  onAdminSessionEnded: () => void
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
  const [loginUsername, setLoginUsername] = useState('admin')
  const [loginPassword, setLoginPassword] = useState('pass')
  const [loginStatus, setLoginStatus] = useState<'idle' | 'checking' | 'authenticated' | 'rejected' | 'error'>('idle')
  const [loginResult, setLoginResult] = useState<AuthLoginResponse | null>(null)
  const [loginError, setLoginError] = useState<string | null>(null)
  const [authSession, setAuthSession] = useState<AuthSessionResponse | null>(null)
  const [sessionStatus, setSessionStatus] = useState<'idle' | 'checking' | 'active' | 'ended' | 'error'>('idle')
  const [sessionError, setSessionError] = useState<string | null>(null)
  const [loginAudit, setLoginAudit] = useState<AuthAuditResponse | null>(null)
  const [loginAuditStatus, setLoginAuditStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [loginAuditError, setLoginAuditError] = useState<string | null>(null)

  useEffect(() => {
    if (!authSession?.authenticated || !authSession.sessionId) {
      return
    }

    const controller = new AbortController()
    void refreshLoginAudit(authSession.sessionId, controller.signal)
    return () => controller.abort()
  }, [authSession?.authenticated, authSession?.sessionId])

  async function refreshLoginAudit(sessionId?: string | null, signal?: AbortSignal) {
    if (!sessionId) {
      setLoginAudit(null)
      setLoginAuditStatus('idle')
      setLoginAuditError(null)
      return
    }

    setLoginAuditStatus('loading')
    setLoginAuditError(null)

    try {
      const audit = await getLoginAudit(8, sessionId, signal)
      setLoginAudit(audit)
      setLoginAuditStatus('ready')
    } catch (error) {
      if (signal?.aborted) {
        return
      }
      setLoginAuditStatus('error')
      setLoginAuditError(error instanceof Error ? error.message : 'Login audit load failed')
    }
  }

  async function handleLoginCheck(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setLoginStatus('checking')
    setLoginResult(null)
    setLoginError(null)
    setAuthSession(null)
    setSessionStatus('idle')
    setSessionError(null)
    onAdminSessionEnded()

    try {
      const result = await login({ username: loginUsername, password: loginPassword })
      setLoginResult(result)
      setLoginStatus(result.authenticated ? 'authenticated' : 'rejected')
      if (result.authenticated && result.sessionId) {
        setAuthSession({
          authenticated: true,
          sessionId: result.sessionId,
          username: result.username,
          displayName: result.displayName,
          role: result.role,
          staffId: result.staffId ?? null,
          createdAt: result.sessionCreatedAt,
          lastSeenAt: result.sessionCreatedAt,
          expiresAt: result.sessionExpiresAt,
          endedAt: null,
          failureReason: null,
          sessionSource: 'avenchart',
        })
        setSessionStatus('active')
        onAdminSessionActive(result.sessionId)
      }
    } catch (error) {
      setLoginStatus('error')
      setLoginError(error instanceof Error ? error.message : 'Login readiness check failed')
    }
  }

  async function handleSessionValidate() {
    const sessionId = authSession?.sessionId ?? loginResult?.sessionId
    if (!sessionId) {
      setSessionStatus('error')
      setSessionError('No session has been issued yet.')
      return
    }

    setSessionStatus('checking')
    setSessionError(null)

    try {
      const session = await getCurrentSession(sessionId)
      setAuthSession(session)
      setSessionStatus(session.authenticated ? 'active' : 'ended')
      if (session.authenticated && session.sessionId) {
        onAdminSessionActive(session.sessionId)
      } else {
        onAdminSessionEnded()
      }
      if (!session.authenticated) {
        setSessionError(session.failureReason ?? 'Session is not active.')
      }
    } catch (error) {
      setSessionStatus('error')
      setSessionError(error instanceof Error ? error.message : 'Session readiness check failed')
    }
  }

  async function handleSessionLogout() {
    const sessionId = authSession?.sessionId ?? loginResult?.sessionId
    if (!sessionId) {
      setSessionStatus('error')
      setSessionError('No session has been issued yet.')
      return
    }

    setSessionStatus('checking')
    setSessionError(null)

    try {
      const session = await logout(sessionId)
      setAuthSession(session)
      setSessionStatus('ended')
      setLoginAudit(null)
      setLoginAuditStatus('idle')
      setLoginAuditError(null)
      onAdminSessionEnded()
    } catch (error) {
      setSessionStatus('error')
      setSessionError(error instanceof Error ? error.message : 'Session logout failed')
    }
  }

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
          <Field label="Authentication" value="Demo login and session readiness active" />
          <Field
            label="Session"
            value={authSession?.authenticated ? `Active for ${authSession.username}` : 'No active session'}
          />
          <Field label="Authorization" value="Admin ACL policy enforced" />
          <Field
            label="Audit logging"
            value={loginAudit ? `${loginAudit.totalEvents} login events captured` : 'Login audit endpoint active'}
          />
          <Field label="Directory mode" value="User/facility mutation and ACL read model" />
          {directory && (
            <>
              <Field label="Access groups" value={String(directory.counts.accessGroups)} />
              <Field label="Permission entries" value={String(directory.counts.accessGroupPermissions)} />
              <Field label="Access memberships" value={String(directory.counts.accessUserMemberships)} />
            </>
          )}
        </div>

        <form className="appointment-mutation-panel" aria-label="Login readiness" onSubmit={handleLoginCheck}>
          <div className="panel-heading">
            <LogIn size={17} />
            <h3>Login Readiness</h3>
          </div>
          <label className="form-field">
            <span>Username</span>
            <input
              value={loginUsername}
              autoComplete="username"
              onChange={(event) => setLoginUsername(event.target.value)}
            />
          </label>
          <label className="form-field">
            <span>Password</span>
            <input
              type="password"
              value={loginPassword}
              autoComplete="current-password"
              onChange={(event) => setLoginPassword(event.target.value)}
            />
          </label>
          <button type="submit" className="icon-text-button primary" disabled={loginStatus === 'checking'}>
            <LogIn size={16} />
            Verify Login
          </button>
          {loginStatus === 'authenticated' && loginResult && (
            <div className="status-banner success">
              Signed in as {loginResult.displayName} ({loginResult.username})
            </div>
          )}
          {loginStatus === 'rejected' && (
            <div className="status-banner error">{loginResult?.failureReason ?? 'Invalid username or password.'}</div>
          )}
          {loginStatus === 'error' && <div className="status-banner error">{loginError}</div>}
          {loginResult?.authenticated && (
            <div className="access-scope-panel">
              <Field label="Role" value={loginResult.role} />
              <Field label="Staff link" value={loginResult.staffId ? String(loginResult.staffId) : 'Not linked'} />
              <Field label="Session" value={loginResult.sessionId ? 'Issued' : 'Not issued'} />
            </div>
          )}
        </form>

        <div className="access-scope-panel" aria-label="Session readiness">
          <div className="panel-heading">
            <ShieldCheck size={17} />
            <h3>Session Readiness</h3>
          </div>
          <div className="access-scope-panel">
            <Field label="State" value={authSession?.authenticated ? 'Active' : sessionStatus === 'ended' ? 'Ended' : 'Not started'} />
            <Field label="Source" value={authSession?.sessionSource ?? 'avenchart'} />
          </div>
          {authSession?.authenticated && (
            <div className="status-banner success">Active session for {authSession.displayName} ({authSession.username})</div>
          )}
          {sessionStatus === 'ended' && (
            <div className="status-banner">Session ended for {authSession?.username || loginResult?.username || 'admin'}</div>
          )}
          {sessionStatus === 'error' && <div className="status-banner error">{sessionError}</div>}
          <div className="access-scope-panel">
            <Field label="Session ID" value={authSession?.sessionId ?? loginResult?.sessionId ?? 'None'} />
            <Field label="Created" value={formatAuditDateTime(authSession?.createdAt ?? loginResult?.sessionCreatedAt)} />
            <Field label="Last seen" value={formatAuditDateTime(authSession?.lastSeenAt)} />
            <Field label="Expires" value={formatAuditDateTime(authSession?.expiresAt ?? loginResult?.sessionExpiresAt)} />
            <Field label="Ended" value={formatAuditDateTime(authSession?.endedAt)} />
          </div>
          <div className="form-actions">
            <button
              type="button"
              className="icon-text-button"
              onClick={handleSessionValidate}
              disabled={sessionStatus === 'checking' || !(authSession?.sessionId ?? loginResult?.sessionId)}
            >
              <ShieldCheck size={16} />
              Validate Session
            </button>
            <button
              type="button"
              className="icon-text-button danger"
              onClick={handleSessionLogout}
              disabled={sessionStatus === 'checking' || !(authSession?.sessionId ?? loginResult?.sessionId)}
            >
              <X size={16} />
              End Session
            </button>
          </div>
        </div>

        <div className="access-scope-panel" aria-label="Login audit events">
          <div className="panel-heading">
            <ShieldCheck size={17} />
            <h3>Login Audit</h3>
          </div>
          {loginAuditStatus === 'loading' && <div className="timeline-placeholder">Loading login audit</div>}
          {loginAuditStatus === 'error' && <div className="status-banner error">{loginAuditError}</div>}
          {loginAuditStatus === 'idle' && <div className="empty-state">Sign in to view login audit events</div>}
          {loginAudit && (
            <>
              <div className="list-counts">
                <MetricRow label="Events" value={loginAudit.totalEvents} />
                <MetricRow label="Successes" value={loginAudit.successfulLogins} />
                <MetricRow label="Failures" value={loginAudit.failedLogins} />
              </div>
              <div className="review-queue-list">
                {loginAudit.events.map((auditEvent) => (
                  <article key={auditEvent.id} className="review-queue-card">
                    <div className="review-queue-card-main">
                      <div>
                        <p className="eyebrow">{auditEvent.event}</p>
                        <h4>{auditEvent.username}</h4>
                        <p>{auditEvent.comment}</p>
                      </div>
                      <span className="status-pill">{auditEvent.success ? 'Success' : 'Failure'}</span>
                    </div>
                    <div className="review-queue-card-grid">
                      <Field label="Occurred" value={formatAuditDateTime(auditEvent.occurredAt)} />
                      <Field label="Source" value={auditEvent.sourceIp ?? 'Unknown'} />
                      <Field label="Log source" value={auditEvent.logSource} />
                    </div>
                  </article>
                ))}
              </div>
              {loginAudit.events.length === 0 && <div className="empty-state">No login audit events captured yet</div>}
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
          <div className="empty-chart">Sign in to load the users and facilities directory</div>
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
  onUpdateMetadata,
  onReplaceContent,
  onReplaceBinaryContent,
  onArchive,
  onRestore,
  onSign,
  onDeny,
  onDownload,
  onDelete,
}: {
  document: PatientDocumentItem
  disabled: boolean
  onView: (document: PatientDocumentItem) => Promise<void>
  onUpdateMetadata: (document: PatientDocumentItem, input: PatientDocumentMetadataUpdateInput) => Promise<unknown>
  onReplaceContent: (document: PatientDocumentItem, input: PatientDocumentContentReplaceInput) => Promise<unknown>
  onReplaceBinaryContent: (
    document: PatientDocumentItem,
    input: PatientDocumentBinaryContentReplaceInput,
  ) => Promise<unknown>
  onArchive: (document: PatientDocumentItem) => Promise<unknown>
  onRestore: (document: PatientDocumentItem) => Promise<unknown>
  onSign: (document: PatientDocumentItem) => Promise<unknown>
  onDeny: (document: PatientDocumentItem) => Promise<unknown>
  onDownload: (document: PatientDocumentItem) => Promise<void>
  onDelete: (document: PatientDocumentItem) => Promise<void>
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [editName, setEditName] = useState(document.name)
  const [editCategoryId, setEditCategoryId] = useState(String(document.categoryId || 3))
  const [editDocDate, setEditDocDate] = useState(document.docDate)
  const [editEncounter, setEditEncounter] = useState(document.encounter ? String(document.encounter) : '')
  const [editNotes, setEditNotes] = useState(document.notes ?? document.documentationOf ?? '')
  const [editError, setEditError] = useState<string | null>(null)
  const [isReplacing, setIsReplacing] = useState(false)
  const [isReplacingBinary, setIsReplacingBinary] = useState(false)
  const [replacementFileName, setReplacementFileName] = useState(document.fileName || `${document.name}.txt`)
  const [replacementContent, setReplacementContent] = useState('')
  const [replacementBinaryFileName, setReplacementBinaryFileName] = useState(document.fileName || document.name)
  const [replacementBinaryMimeType, setReplacementBinaryMimeType] = useState(document.mimetype || 'application/octet-stream')
  const [replacementBinaryContentBase64, setReplacementBinaryContentBase64] = useState('')
  const [replacementBinaryFileMessage, setReplacementBinaryFileMessage] = useState('No file selected')
  const [replaceError, setReplaceError] = useState<string | null>(null)
  const isApproved = document.reviewStatus === 'approved'
  const isReviewed = document.reviewStatus === 'approved' || document.reviewStatus === 'denied'
  const isExternalLink = document.storageMethod === 'web_url' && Boolean(document.url)
  const isArchived = document.deleted !== 0
  const canReplaceContent = !isArchived && !isExternalLink

  useEffect(() => {
    setEditName(document.name)
    setEditCategoryId(String(document.categoryId || 3))
    setEditDocDate(document.docDate)
    setEditEncounter(document.encounter ? String(document.encounter) : '')
    setEditNotes(document.notes ?? document.documentationOf ?? '')
    setEditError(null)
    setReplacementFileName(document.fileName || `${document.name}.txt`)
    setReplacementContent('')
    setReplacementBinaryFileName(document.fileName || document.name)
    setReplacementBinaryMimeType(document.mimetype || 'application/octet-stream')
    setReplacementBinaryContentBase64('')
    setReplacementBinaryFileMessage('No file selected')
    setReplaceError(null)
  }, [document])

  async function handleMetadataSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setEditError(null)

    const categoryId = Number(editCategoryId)
    const encounter = editEncounter.trim().length > 0 ? Number(editEncounter) : null
    if (!Number.isInteger(categoryId) || (encounter !== null && !Number.isInteger(encounter))) {
      setEditError('Check numeric fields')
      return
    }

    await onUpdateMetadata(document, {
      categoryId,
      name: editName,
      docDate: editDocDate,
      encounter,
      notes: editNotes.trim().length > 0 ? editNotes : null,
    })
    setIsEditing(false)
  }

  async function handleContentReplacementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setReplaceError(null)

    if (!replacementFileName.trim() || !replacementContent.trim()) {
      setReplaceError('Enter a file name and replacement body')
      return
    }

    await onReplaceContent(document, {
      fileName: replacementFileName,
      content: replacementContent,
    })
    setIsReplacing(false)
    setReplacementContent('')
  }

  async function handleBinaryReplacementFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      setReplacementBinaryContentBase64('')
      setReplacementBinaryFileMessage('No file selected')
      return
    }

    const contentBase64 = await readFileAsBase64(file)
    setReplacementBinaryFileName(file.name)
    setReplacementBinaryMimeType(file.type || 'application/octet-stream')
    setReplacementBinaryContentBase64(contentBase64)
    setReplacementBinaryFileMessage(`${file.name} selected (${formatBytes(file.size)})`)
  }

  async function handleBinaryContentReplacementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setReplaceError(null)

    if (!replacementBinaryFileName.trim() || !replacementBinaryMimeType.trim() || !replacementBinaryContentBase64) {
      setReplaceError('Choose a replacement file')
      return
    }

    await onReplaceBinaryContent(document, {
      fileName: replacementBinaryFileName,
      mimetype: replacementBinaryMimeType,
      contentBase64: replacementBinaryContentBase64,
    })
    setIsReplacingBinary(false)
    setReplacementBinaryContentBase64('')
    setReplacementBinaryFileMessage('No file selected')
  }

  return (
    <article className="document-card">
      <div className="message-item-header">
        <strong>{document.name}</strong>
        <div className="document-card-tags">
          <span className="status-tag">{document.categoryName}</span>
          {isArchived && <span className="status-tag danger">Archived</span>}
        </div>
      </div>
      <div className="document-preview-readiness">
        <div className={`document-thumbnail document-thumbnail-${document.previewKind || 'file'}`} aria-label={`Document preview ${document.thumbnailLabel || 'DOC'}`}>
          {document.thumbnailDataUri ? (
            <img
              alt={`${document.name} thumbnail`}
              className="document-thumbnail-image-preview"
              src={document.thumbnailDataUri}
            />
          ) : (
            <span>{document.thumbnailLabel || 'DOC'}</span>
          )}
        </div>
        <div className="document-preview-summary">
          <span>{document.previewStatus || 'Preview pending'}</span>
          <p>{document.thumbnailText || document.contentPreview || document.fileName || 'No preview generated'}</p>
        </div>
      </div>
      <div className="document-revision-readiness">
        <span>{document.versionLabel || 'Version 1'} / {document.versionStatus || 'Current version'}</span>
        <span>{document.revisionAt || document.uploadedAt}</span>
        <span>{document.hasPriorVersions ? `${document.versionHistoryCount} versions` : 'No prior versions'}</span>
      </div>
      {document.isScannedAttachment && (
        <div className="document-scan-readiness" aria-label={`Scan readiness for ${document.name}`}>
          <span>{document.scanStatus}</span>
          <span>{document.captureSource}</span>
          <span>{document.scanPageCount} scanned page{document.scanPageCount === 1 ? '' : 's'}</span>
          <span>{document.ocrStatus}</span>
        </div>
      )}
      <div className="document-lifecycle-readiness" aria-label={`Lifecycle for ${document.name}`}>
        {(document.lifecycleEvents ?? []).map((event) => (
          <div className="document-lifecycle-event" key={event.code}>
            <strong>{event.label}</strong>
            <span>{event.occurredAt || 'Current state'}</span>
            <span>{event.actor ? `By ${event.actor}` : event.detail}</span>
          </div>
        ))}
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
      <div className="procedure-order-meta">
        <span>{isApproved ? 'approved' : document.reviewStatus}</span>
        <span>{document.reviewedBy ? `Reviewed by ${document.reviewedBy}` : 'Not reviewed'}</span>
      </div>
      <p className="document-preview">{document.contentPreview || document.notes || 'No preview available'}</p>
      {document.notes && <p className="document-note">Notes: {document.notes}</p>}
      <div className="document-footnote">
        <span>{document.documentKey}</span>
        <span>{isExternalLink ? document.url : document.fileName || document.hash || 'No document reference'}</span>
      </div>
      {isEditing && (
        <form className="document-edit-form" onSubmit={handleMetadataSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Name</span>
              <input
                value={editName}
                onChange={(event) => setEditName(event.target.value)}
                aria-label="Document metadata name"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>Category</span>
                <select
                  value={editCategoryId}
                  onChange={(event) => setEditCategoryId(event.target.value)}
                  aria-label="Document metadata category"
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
                  value={editDocDate}
                  onChange={(event) => setEditDocDate(event.target.value)}
                  aria-label="Document metadata date"
                  required
                />
              </label>
            </div>
            <label className="filter-field">
              <span>Encounter</span>
              <input
                value={editEncounter}
                onChange={(event) => setEditEncounter(event.target.value)}
                aria-label="Document metadata encounter"
                inputMode="numeric"
              />
            </label>
            <label className="filter-field">
              <span>Notes</span>
              <textarea
                value={editNotes}
                onChange={(event) => setEditNotes(event.target.value)}
                aria-label="Document metadata notes"
                rows={3}
              />
            </label>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Metadata
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsEditing(false)}>
              <X size={14} />
              Cancel
            </button>
            {editError && <span className="save-note error">{editError}</span>}
          </div>
        </form>
      )}
      {isReplacing && (
        <form className="document-edit-form" onSubmit={handleContentReplacementSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>File Name</span>
              <input
                value={replacementFileName}
                onChange={(event) => setReplacementFileName(event.target.value)}
                aria-label="Replacement document file name"
                required
              />
            </label>
            <label className="filter-field">
              <span>Replacement Body</span>
              <textarea
                value={replacementContent}
                onChange={(event) => setReplacementContent(event.target.value)}
                aria-label="Replacement document body"
                rows={4}
                required
              />
            </label>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Content
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsReplacing(false)}>
              <X size={14} />
              Cancel
            </button>
            {replaceError && <span className="save-note error">{replaceError}</span>}
          </div>
        </form>
      )}
      {isReplacingBinary && (
        <form className="document-edit-form" onSubmit={handleBinaryContentReplacementSubmit}>
          <div className="mutation-grid">
            <label className="filter-field">
              <span>Binary File</span>
              <input
                type="file"
                onChange={handleBinaryReplacementFileChange}
                aria-label="Patient replacement binary document upload"
                required
              />
            </label>
            <div className="mutation-grid two-column">
              <label className="filter-field">
                <span>File Name</span>
                <input
                  value={replacementBinaryFileName}
                  onChange={(event) => setReplacementBinaryFileName(event.target.value)}
                  aria-label="Patient replacement binary document file name"
                  required
                />
              </label>
              <label className="filter-field">
                <span>MIME Type</span>
                <input
                  value={replacementBinaryMimeType}
                  onChange={(event) => setReplacementBinaryMimeType(event.target.value)}
                  aria-label="Patient replacement binary document MIME type"
                  required
                />
              </label>
            </div>
            <span className="save-note">{replacementBinaryFileMessage}</span>
          </div>
          <div className="document-item-actions">
            <button className="icon-text-button primary" type="submit" disabled={disabled}>
              <Check size={14} />
              Save Binary
            </button>
            <button className="icon-text-button secondary" type="button" onClick={() => setIsReplacingBinary(false)}>
              <X size={14} />
              Cancel
            </button>
            {replaceError && <span className="save-note error">{replaceError}</span>}
          </div>
        </form>
      )}
      <div className="document-item-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => void onView(document)}
        >
          <FileText size={14} />
          View
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => void onDownload(document)}
        >
          <Download size={14} />
          Download
        </button>
        {isExternalLink && (
          <a className="icon-text-button secondary" href={document.url ?? '#'} target="_blank" rel="noreferrer">
            <ExternalLink size={14} />
            Open Link
          </a>
        )}
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => {
            setIsReplacing(false)
            setIsReplacingBinary(false)
            setIsEditing((current) => !current)
          }}
        >
          <Pencil size={14} />
          Edit
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || !canReplaceContent}
          onClick={() => {
            setIsEditing(false)
            setIsReplacingBinary(false)
            setIsReplacing((current) => !current)
          }}
        >
          <FileText size={14} />
          Replace
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || !canReplaceContent}
          onClick={() => {
            setIsEditing(false)
            setIsReplacing(false)
            setIsReplacingBinary((current) => !current)
          }}
        >
          <Upload size={14} />
          Binary File
        </button>
        <button
          className="icon-text-button danger"
          type="button"
          disabled={disabled || isArchived}
          onClick={() => void onArchive(document)}
        >
          <Ban size={14} />
          Archive
        </button>
        {isArchived && (
          <button
            className="icon-text-button secondary"
            type="button"
            disabled={disabled}
            onClick={() => void onRestore(document)}
          >
            <RotateCcw size={14} />
            Restore
          </button>
        )}
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isReviewed || isArchived}
          onClick={() => void onSign(document)}
        >
          <ShieldCheck size={14} />
          Sign
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || isReviewed || isArchived}
          onClick={() => void onDeny(document)}
        >
          <X size={14} />
          Deny
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

function PatientDuplicateCandidateList({ candidates }: { candidates: PatientDuplicateCandidate[] }) {
  if (candidates.length === 0) {
    return <div className="timeline-placeholder">No duplicate candidates detected</div>
  }

  return (
    <div className="duplicate-candidate-list">
      {candidates.map((candidate) => (
        <article className="duplicate-candidate-card" key={candidate.canonicalId}>
          <div className="message-item-header">
            <strong>{candidate.displayName}</strong>
            <span className="status-tag">Score {candidate.matchScore}</span>
          </div>
          <div className="procedure-order-meta">
            <span>{candidate.pubpid}</span>
            <span>DOB {candidate.dateOfBirth}</span>
          </div>
          <div className="procedure-order-meta">
            <span>{candidate.phoneHome ?? candidate.phoneCell ?? candidate.phone ?? 'No phone'}</span>
            <span>{candidate.email ?? 'No email'}</span>
          </div>
          <div className="duplicate-reason-list">
            {candidate.matchReasons.map((reason) => (
              <span key={reason}>{reason}</span>
            ))}
          </div>
        </article>
      ))}
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
      {appointment.isRecurringSeries && (
        <div className="patient-result-sub">
          <span>{appointmentOccurrenceDetail(appointment)}</span>
          <span>{appointment.recurrenceLabel}</span>
          {appointment.recurrenceExceptionCount > 0 && <span>{appointment.recurrenceExceptionCount} skipped</span>}
        </div>
      )}
      <div className="patient-result-sub">
        <span>
          {appointment.date} at {appointment.startTime}
        </span>
        <span>{appointment.durationMinutes} min</span>
      </div>
      <div className="patient-result-sub">
        <span>{appointment.patientDisplayName}</span>
        <span>{appointmentCategoryLabel(appointment)}</span>
      </div>
      <div className="patient-result-sub">
        <span>{appointment.providerName ?? 'Provider not recorded'}</span>
        <span>{appointment.facilityName ?? 'Facility not recorded'}</span>
        {appointment.reminderDue && <span>Reminder due</span>}
        {appointment.providerOverlapCount > 0 && <span>{appointment.providerOverlapCount} provider overlap</span>}
        {appointment.patientOverlapCount > 0 && <span>{appointment.patientOverlapCount} patient overlap</span>}
        {appointment.roomOverlapCount > 0 && <span>{appointment.roomOverlapCount} room overlap</span>}
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
  onUpdateContent,
  onAssign,
  onReply,
  onArchive,
  onDelete,
}: {
  message: PatientMessageItem
  disabled: boolean
  onClose: (message: PatientMessageItem) => Promise<unknown>
  onUpdateContent: (message: PatientMessageItem, update: PatientMessageContentUpdateInput) => Promise<unknown>
  onAssign: (message: PatientMessageItem, update: PatientMessageAssignmentUpdateInput) => Promise<unknown>
  onReply: (message: PatientMessageItem, reply: PatientMessageReplyInput) => Promise<unknown>
  onArchive: (message: PatientMessageItem) => Promise<unknown>
  onDelete: (message: PatientMessageItem) => Promise<void>
}) {
  const [titleDraft, setTitleDraft] = useState(message.title || '')
  const [bodyDraft, setBodyDraft] = useState(message.body || '')
  const [assigneeDraft, setAssigneeDraft] = useState(message.assignedTo || '')
  const [replyDraft, setReplyDraft] = useState('')

  useEffect(() => {
    setTitleDraft(message.title || '')
    setBodyDraft(message.body || '')
    setAssigneeDraft(message.assignedTo || '')
    setReplyDraft('')
  }, [message.title, message.body, message.assignedTo])

  async function handleContentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await onUpdateContent(message, { title: titleDraft.trim(), body: bodyDraft.trim() })
  }

  async function handleAssignSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await onAssign(message, { assignedTo: assigneeDraft.trim() })
  }

  async function handleReplySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await onReply(message, { body: replyDraft.trim(), assignedTo: assigneeDraft.trim() || message.assignedTo || 'admin' })
    setReplyDraft('')
  }

  const contentUnchanged = titleDraft.trim() === (message.title || '') && bodyDraft.trim() === (message.body || '')

  return (
    <article className="message-item">
      <div className="message-item-header">
        <strong>{message.title || 'Patient message'}</strong>
        <span className="status-tag">{message.status || 'Status pending'}</span>
      </div>
      <p>{message.body || 'No message body recorded'}</p>
      <span>{[message.date || 'No date', message.assignedTo ? `Assigned to ${message.assignedTo}` : null].filter(Boolean).join(' / ')}</span>
      <div className="message-metadata" aria-label={`${message.title || 'Message'} message metadata`}>
        <span>{message.portalRelation ? `Portal relation ${message.portalRelation}` : 'No portal relation'}</span>
        <span>{message.isEncrypted ? 'Encrypted message' : 'Plain text message'}</span>
        {message.updatedAt && (
          <span>{message.updatedBy ? `Updated by user ${message.updatedBy}` : 'Updated'}</span>
        )}
        {message.updatedAt && <span>{`Updated ${message.updatedAt}`}</span>}
      </div>
      <form className="message-content-form" onSubmit={handleContentSubmit}>
        <label className="compact-inline-field">
          <span>Title</span>
          <input
            value={titleDraft}
            onChange={(event) => setTitleDraft(event.target.value)}
            aria-label={`Edit ${message.title || 'message'} title`}
            required
          />
        </label>
        <label className="compact-inline-field message-body-field">
          <span>Body</span>
          <textarea
            value={bodyDraft}
            onChange={(event) => setBodyDraft(event.target.value)}
            aria-label={`Edit ${message.title || 'message'} body`}
            rows={3}
            required
          />
        </label>
        <button
          className="icon-text-button secondary"
          type="submit"
          disabled={disabled || titleDraft.trim().length === 0 || bodyDraft.trim().length === 0 || contentUnchanged}
        >
          <Pencil size={14} />
          Save Edit
        </button>
      </form>
      <form className="message-assignment-form" onSubmit={handleAssignSubmit}>
        <label className="compact-inline-field">
          <span>Assign To</span>
          <input
            value={assigneeDraft}
            onChange={(event) => setAssigneeDraft(event.target.value)}
            aria-label={`Assign ${message.title || 'message'} to`}
            required
          />
        </label>
        <button
          className="icon-text-button secondary"
          type="submit"
          disabled={disabled || assigneeDraft.trim().length === 0 || assigneeDraft.trim() === (message.assignedTo || '')}
        >
          <UserRound size={14} />
          Reassign
        </button>
      </form>
      <form className="message-reply-form" onSubmit={handleReplySubmit}>
        <label className="compact-inline-field message-body-field">
          <span>Reply</span>
          <textarea
            value={replyDraft}
            onChange={(event) => setReplyDraft(event.target.value)}
            aria-label={`Reply to ${message.title || 'message'}`}
            rows={2}
            required
          />
        </label>
        <button
          className="icon-text-button secondary"
          type="submit"
          disabled={disabled || replyDraft.trim().length === 0 || (assigneeDraft.trim() || message.assignedTo || '').length === 0}
        >
          <Reply size={14} />
          Reply
        </button>
      </form>
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
  onSelectCorrectionLine,
  onDeactivateLine,
  onDeleteLine,
  onUpdateClaimStatus,
  onDeleteClaim,
  onVoidPayment,
  onDeletePayment,
}: {
  encounter: BillingEncounterItem
  disabled: boolean
  onSelectCorrectionLine: (line: BillingLineItem) => void
  onDeactivateLine: (line: BillingLineItem) => Promise<unknown>
  onDeleteLine: (line: BillingLineItem) => Promise<void>
  onUpdateClaimStatus: (claim: BillingClaimItem, input: BillingClaimStatusUpdateInput) => Promise<unknown>
  onDeleteClaim: (claim: BillingClaimItem) => Promise<void>
  onVoidPayment: (payment: BillingPaymentItem) => Promise<unknown>
  onDeletePayment: (payment: BillingPaymentItem) => Promise<void>
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
        <div className="document-card-tags">
          <span className="status-tag">{encounter.agingBucket || 'Current'}</span>
          <span className="status-tag">Balance {formatCurrency(encounter.balanceAmount)}</span>
        </div>
      </div>
      <div className="procedure-order-meta">
        <span>{encounter.providerName || 'Provider not recorded'}</span>
        <span>{encounter.facilityName || 'Facility not recorded'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>{encounter.diagnosisCode || 'No diagnosis'}</span>
        <span>{encounter.diagnosisText || 'No diagnosis text'}</span>
      </div>
      <div className="procedure-order-meta">
        <span>Charges {formatCurrency(encounter.totalFee)}</span>
        <span>Paid {formatCurrency(encounter.paymentAmount)}</span>
        <span>Adjusted {formatCurrency(encounter.adjustmentAmount)}</span>
        <span>Balance {formatCurrency(encounter.balanceAmount)}</span>
        <span>Age {encounter.ageDays} days</span>
      </div>
      <div className="billing-line-list">
        {encounter.claims.map((claim) => (
          <BillingClaimCard
            key={claim.id}
            claim={claim}
            disabled={disabled}
            onUpdateStatus={onUpdateClaimStatus}
            onDelete={onDeleteClaim}
          />
        ))}
        {encounter.claims.length === 0 && <div className="timeline-placeholder">No claim status recorded</div>}
      </div>
      <div className="billing-line-list">
        {encounter.payments.map((payment) => (
          <BillingPaymentCard
            key={payment.activityId}
            payment={payment}
            disabled={disabled}
            onVoid={onVoidPayment}
            onDelete={onDeletePayment}
          />
        ))}
        {encounter.payments.length === 0 && <div className="timeline-placeholder">No payment posting recorded</div>}
      </div>
      <div className="billing-line-list">
        {encounter.lines.map((line) => (
          <BillingLineCard
            key={line.id}
            line={line}
            disabled={disabled}
            onSelectCorrection={onSelectCorrectionLine}
            onDeactivate={onDeactivateLine}
            onDelete={onDeleteLine}
          />
        ))}
        {encounter.lines.length === 0 && <div className="timeline-placeholder">No fee sheet codes recorded</div>}
      </div>
    </article>
  )
}

function BillingClaimCard({
  claim,
  disabled,
  onUpdateStatus,
  onDelete,
}: {
  claim: BillingClaimItem
  disabled: boolean
  onUpdateStatus: (claim: BillingClaimItem, input: BillingClaimStatusUpdateInput) => Promise<unknown>
  onDelete: (claim: BillingClaimItem) => Promise<void>
}) {
  const generatedProcessFile = claim.processFile || `CLAIM-${claim.encounter}-PARITY-837P.txt`

  function handleGenerate() {
    void onUpdateStatus(claim, {
      status: 2,
      billProcess: 0,
      processTime: '2026-06-18 14:15:00',
      processFile: generatedProcessFile,
      target: 'X12',
      x12PartnerId: 1,
      submittedClaim: claim.submittedClaim || `Generated claim ${claim.encounter}`,
    })
  }

  function handleClear() {
    void onUpdateStatus(claim, {
      status: 3,
      billProcess: 0,
      processTime: null,
      processFile: '',
      target: 'HCFA',
      x12PartnerId: 0,
      submittedClaim: claim.submittedClaim || `Cleared claim ${claim.encounter}`,
    })
  }

  return (
    <article className="billing-line-card">
      <div className="message-item-header">
        <strong>Claim Status</strong>
        <span className="status-tag">{claim.statusLabel}</span>
      </div>
      <p>
        Version {claim.version} / {formatPayerType(claim.payerType)} {claim.payerName || `Payer ${claim.payerId}`}
      </p>
      <div className="procedure-order-meta">
        <span>{claim.target ? `${claim.target} billing` : 'No billing target'}</span>
        <span>{claim.billTime ? `Billed ${claim.billTime}` : 'No bill time'}</span>
        <span>{claim.processTime ? `Processed ${claim.processTime}` : 'Not processed'}</span>
        <span>{claim.processFile ? `File ${claim.processFile}` : 'No claim file'}</span>
        <span>{claim.submittedClaim ? 'Reviewed claim data' : 'No submitted claim payload'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button type="button" className="icon-text-button" disabled={disabled} onClick={handleGenerate}>
          <Upload size={14} />
          Generate
        </button>
        <button type="button" className="icon-text-button primary" disabled={disabled} onClick={handleClear}>
          <Check size={14} />
          Clear
        </button>
        <button type="button" className="icon-text-button danger" disabled={disabled} onClick={() => void onDelete(claim)}>
          <Trash2 size={14} />
          Delete
        </button>
      </div>
    </article>
  )
}

function BillingPaymentCard({
  payment,
  disabled,
  onVoid,
  onDelete,
}: {
  payment: BillingPaymentItem
  disabled: boolean
  onVoid: (payment: BillingPaymentItem) => Promise<unknown>
  onDelete: (payment: BillingPaymentItem) => Promise<void>
}) {
  const statusLabel = payment.adjustmentAmount > 0 && payment.payAmount === 0 ? 'Adjustment' : 'Payment'
  const postedDate = payment.postDate || payment.postTime

  return (
    <article className="billing-line-card">
      <div className="message-item-header">
        <strong>Payment Posting</strong>
        <span className="status-tag">{statusLabel}</span>
      </div>
      <p>
        {formatPaymentPayer(payment)} / {payment.reference || 'No reference'}
      </p>
      <div className="procedure-order-meta">
        <span>{payment.code ? `${payment.code}${payment.modifier ? `:${payment.modifier}` : ''}` : 'No code'}</span>
        <span>{payment.memo || 'No memo'}</span>
        <span>{payment.paymentMethod || 'No method'}</span>
        <span>{postedDate ? `Posted ${postedDate}` : 'No post date'}</span>
        <span>{payment.payAmount > 0 ? `Paid ${formatCurrency(payment.payAmount)}` : 'No payment amount'}</span>
        <span>
          {payment.adjustmentAmount > 0 ? `Adjusted ${formatCurrency(payment.adjustmentAmount)}` : 'No adjustment'}
        </span>
        <span>{payment.accountCode ? `Account ${payment.accountCode}` : 'No account code'}</span>
        <span>{payment.reasonCode ? `Reason ${payment.reasonCode}` : 'No reason code'}</span>
        <span>{payment.payerClaimNumber ? `Claim ${payment.payerClaimNumber}` : 'No payer claim number'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button
          type="button"
          className="icon-text-button"
          disabled={disabled}
          onClick={() => void onVoid(payment)}
        >
          <Ban size={14} />
          Void
        </button>
        <button
          type="button"
          className="icon-text-button danger"
          disabled={disabled}
          onClick={() => void onDelete(payment)}
        >
          <Trash2 size={14} />
          Delete
        </button>
      </div>
    </article>
  )
}

function BillingStatementLineItemCard({ line }: { line: BillingStatementLineItem }) {
  const amountClassName = line.chargeAmount > 0 ? 'ledger-amount charge' : 'ledger-amount credit'
  const primaryAmount = line.chargeAmount > 0
    ? line.chargeAmount
    : -(line.paymentAmount || line.adjustmentAmount)

  return (
    <article className="billing-ledger-entry">
      <div className="ledger-entry-main">
        <div>
          <strong>{line.description}</strong>
          <span>
            Line {line.lineNumber} / {line.entryDate} / Encounter {line.encounter}
          </span>
        </div>
        <div className="document-card-tags">
          <span className="status-tag">{line.entryType}</span>
          <span className={amountClassName}>{formatCurrency(primaryAmount)}</span>
        </div>
      </div>
      <div className="procedure-order-meta">
        <span>{line.code ? `Code ${line.code}` : 'No code'}</span>
        <span>{line.reference ? `Reference ${line.reference}` : 'No reference'}</span>
        <span>Charge {formatCurrency(line.chargeAmount)}</span>
        <span>Payment {formatCurrency(line.paymentAmount)}</span>
        <span>Adjustment {formatCurrency(line.adjustmentAmount)}</span>
        <span>Balance {formatCurrency(line.balanceAmount)}</span>
      </div>
    </article>
  )
}

function BillingLedgerEntryCard({ entry }: { entry: BillingLedgerEntry }) {
  const amountClassName = entry.amount < 0 ? 'ledger-amount credit' : 'ledger-amount charge'

  return (
    <article className="billing-ledger-entry">
      <div className="ledger-entry-main">
        <div>
          <strong>{entry.description}</strong>
          <span>
            {entry.entryDate} / Encounter {entry.encounter}
          </span>
        </div>
        <div className="document-card-tags">
          <span className="status-tag">{entry.entryType}</span>
          <span className={amountClassName}>{formatCurrency(entry.amount)}</span>
        </div>
      </div>
      <div className="procedure-order-meta">
        <span>{entry.code ? `Code ${entry.code}` : 'No code'}</span>
        <span>{entry.reference ? `Reference ${entry.reference}` : 'No reference'}</span>
        <span>Running {formatCurrency(entry.runningBalanceAmount)}</span>
      </div>
    </article>
  )
}

function BillingLineCard({
  line,
  disabled,
  onSelectCorrection,
  onDeactivate,
  onDelete,
}: {
  line: BillingLineItem
  disabled: boolean
  onSelectCorrection: (line: BillingLineItem) => void
  onDeactivate: (line: BillingLineItem) => Promise<unknown>
  onDelete: (line: BillingLineItem) => Promise<void>
}) {
  return (
    <article className="billing-line-card">
      <div className="message-item-header">
        <strong>{line.code ? `${line.code}${line.modifier ? `:${line.modifier}` : ''}` : 'Billing code'}</strong>
        <span className="status-tag">{line.codeType || 'Code type'}</span>
      </div>
      <p>{line.codeText || 'No description recorded'}</p>
      <div className="procedure-order-meta">
        <span>{line.justify ? `Justify ${line.justify}` : 'No justification'}</span>
        <span>{line.modifier ? `Modifier ${line.modifier}` : 'No modifier'}</span>
        <span>{line.units} unit{line.units === 1 ? '' : 's'}</span>
        <span>{line.billed === 1 ? 'Billed' : 'Unbilled'}</span>
        <span>{formatCurrency(line.fee)}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => onSelectCorrection(line)}
        >
          <Pencil size={15} />
          Correct
        </button>
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
  onUpdate,
  onCreateReport,
  onCreateSpecimen,
  onDelete,
}: {
  order: ProcedureOrderItem
  disabled: boolean
  onComplete: (order: ProcedureOrderItem) => Promise<unknown>
  onUpdate: (order: ProcedureOrderItem, input: ProcedureOrderUpdateInput) => Promise<unknown>
  onCreateReport: (input: ProcedureReportCreateInput) => Promise<unknown>
  onCreateSpecimen: (input: ProcedureSpecimenCreateInput) => Promise<unknown>
  onDelete: (order: ProcedureOrderItem) => Promise<void>
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [orderDate, setOrderDate] = useState(order.orderDate)
  const [orderCode, setOrderCode] = useState(order.code ?? '')
  const [orderName, setOrderName] = useState(order.name ?? '')
  const [orderType, setOrderType] = useState(order.procedureType ?? '')
  const [orderDiagnosis, setOrderDiagnosis] = useState(order.diagnosis ?? '')
  const [orderPriority, setOrderPriority] = useState(order.orderPriority ?? 'routine')
  const [orderStatus, setOrderStatus] = useState(order.orderStatus ?? 'pending')
  const [orderInstructions, setOrderInstructions] = useState(order.instructions ?? '')
  const [editStatus, setEditStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    setOrderDate(order.orderDate)
    setOrderCode(order.code ?? '')
    setOrderName(order.name ?? '')
    setOrderType(order.procedureType ?? '')
    setOrderDiagnosis(order.diagnosis ?? '')
    setOrderPriority(order.orderPriority ?? 'routine')
    setOrderStatus(order.orderStatus ?? 'pending')
    setOrderInstructions(order.instructions ?? '')
    setEditStatus('idle')
  }, [order])

  async function handleOrderCorrectionSubmit(event: FormEvent) {
    event.preventDefault()
    setEditStatus('saving')

    try {
      await onUpdate(order, {
        dateOrdered: orderDate,
        priority: orderPriority,
        status: orderStatus,
        procedureCode: orderCode,
        procedureName: orderName,
        procedureType: orderType,
        diagnosis: orderDiagnosis,
        instructions: orderInstructions,
      })
      setEditStatus('saved')
      setIsEditing(false)
    } catch {
      setEditStatus('error')
    }
  }

  async function handleCreateSpecimen() {
    await onCreateSpecimen({
      orderId: order.id,
      specimenIdentifier: `SID-${order.id}`,
      accessionIdentifier: `ACC-${order.id}`,
      specimenTypeCode: 'BLD',
      specimenType: 'Blood',
      collectionMethodCode: 'VP',
      collectionMethod: 'Venipuncture',
      specimenLocationCode: 'LAC',
      specimenLocation: 'Left antecubital',
      collectedDate: '2026-06-18 12:20:00',
      volumeValue: 4.5,
      volumeUnit: 'mL',
      conditionCode: 'OK',
      specimenCondition: 'Acceptable',
      comments: 'Created from the modernized Procedures workspace.',
    })
  }

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
      <div className="procedure-order-meta">
        <span>{order.diagnosis || 'No diagnosis'}</span>
        <span>{order.instructions || 'No instructions'}</span>
      </div>
      <ProcedureSpecimenList specimens={order.specimens} />
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
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => setIsEditing((current) => !current)}
        >
          <Pencil size={15} />
          Correct Order
        </button>
        <button className="icon-text-button secondary" type="button" disabled={disabled} onClick={handleCreateSpecimen}>
          <FlaskConical size={15} />
          Add Specimen
        </button>
        <button className="icon-text-button secondary" type="button" disabled={disabled} onClick={handleCreateReport}>
          <FileText size={15} />
          Add Report
        </button>
        <button className="icon-text-button danger" type="button" disabled={disabled} onClick={() => onDelete(order)}>
          <Trash2 size={15} />
          Delete
        </button>
        {editStatus === 'saved' && <span className="save-note">Saved</span>}
        {editStatus === 'error' && <span className="save-note error">Action failed</span>}
      </div>
      {isEditing && (
        <form
          className="appointment-mutation-panel encounter-procedure-result-entry-panel"
          aria-label={`Procedure order correction ${order.id}`}
          onSubmit={handleOrderCorrectionSubmit}
        >
          <div className="mutation-grid encounter-procedure-result-entry-grid">
            <label className="filter-field">
              <span>Date</span>
              <input
                value={orderDate}
                onChange={(event) => setOrderDate(event.target.value)}
                aria-label="Procedure corrected order date"
                required
              />
            </label>
            <label className="filter-field">
              <span>Code</span>
              <input
                value={orderCode}
                onChange={(event) => setOrderCode(event.target.value)}
                aria-label="Procedure corrected order code"
                required
              />
            </label>
            <label className="filter-field procedure-order-name-field">
              <span>Name</span>
              <input
                value={orderName}
                onChange={(event) => setOrderName(event.target.value)}
                aria-label="Procedure corrected order name"
                required
              />
            </label>
            <label className="filter-field">
              <span>Type</span>
              <input
                value={orderType}
                onChange={(event) => setOrderType(event.target.value)}
                aria-label="Procedure corrected order type"
                required
              />
            </label>
            <label className="filter-field">
              <span>Priority</span>
              <select
                value={orderPriority}
                onChange={(event) => setOrderPriority(event.target.value)}
                aria-label="Procedure corrected order priority"
              >
                <option value="routine">Routine</option>
                <option value="urgent">Urgent</option>
                <option value="stat">Stat</option>
              </select>
            </label>
            <label className="filter-field">
              <span>Status</span>
              <select
                value={orderStatus}
                onChange={(event) => setOrderStatus(event.target.value)}
                aria-label="Procedure corrected order status"
              >
                <option value="pending">Pending</option>
                <option value="complete">Complete</option>
                <option value="cancelled">Cancelled</option>
              </select>
            </label>
            <label className="filter-field">
              <span>Diagnosis</span>
              <input
                value={orderDiagnosis}
                onChange={(event) => setOrderDiagnosis(event.target.value)}
                aria-label="Procedure corrected order diagnosis"
                required
              />
            </label>
            <label className="filter-field procedure-order-name-field">
              <span>Instructions</span>
              <input
                value={orderInstructions}
                onChange={(event) => setOrderInstructions(event.target.value)}
                aria-label="Procedure corrected order instructions"
              />
            </label>
          </div>
          <div className="detail-actions compact-actions">
            <button className="icon-text-button primary" type="submit" disabled={editStatus === 'saving'}>
              <Check size={15} />
              <span>{editStatus === 'saving' ? 'Saving' : 'Save Order Correction'}</span>
            </button>
          </div>
        </form>
      )}
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
      <ProcedureSpecimenList specimens={order.specimens} />
      {order.instructions && <p className="procedure-scheduled-note">{order.instructions}</p>}
    </article>
  )
}

function ProcedureSpecimenList({ specimens }: { specimens: ProcedureSpecimenItem[] }) {
  if (specimens.length === 0) {
    return null
  }

  return (
    <div className="procedure-specimen-list" aria-label="Procedure specimens">
      {specimens.map((specimen) => {
        const title =
          specimen.specimenIdentifier || specimen.accessionIdentifier || `Specimen ${specimen.id}`
        const volume = formatSpecimenVolume(specimen)
        return (
          <article className="procedure-specimen-card" key={specimen.id}>
            <div className="message-item-header">
              <strong>{title}</strong>
              <span className="status-tag">{specimen.specimenCondition || specimen.specimenType || 'Specimen'}</span>
            </div>
            <div className="procedure-specimen-meta">
              <span>{specimen.accessionIdentifier ? `Accession ${specimen.accessionIdentifier}` : 'No accession'}</span>
              <span>{specimen.specimenType || specimen.specimenTypeCode || 'No specimen type'}</span>
              <span>{specimen.collectionMethod || specimen.collectionMethodCode || 'No collection method'}</span>
              <span>{specimen.specimenLocation || specimen.specimenLocationCode || 'No location'}</span>
              <span>{specimen.collectedDate ? `Collected ${specimen.collectedDate}` : 'No collection date'}</span>
              <span>{volume || 'No volume'}</span>
            </div>
            {specimen.comments && <p className="procedure-scheduled-note">{specimen.comments}</p>}
          </article>
        )
      })}
    </div>
  )
}

function ProcedureReportGroup({
  order,
  disabled,
  onCreateResult,
  onUpdateReport,
  onSignReport,
  onReopenReportReview,
  onUpdateResult,
}: {
  order: ProcedureOrderItem
  disabled: boolean
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
  onUpdateReport: (report: ProcedureReportItem, input: ProcedureReportUpdateInput) => Promise<unknown>
  onSignReport: (report: ProcedureReportItem, input: ProcedureReportSignInput) => Promise<unknown>
  onReopenReportReview: (report: ProcedureReportItem) => Promise<unknown>
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
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
      <ProcedureSpecimenList specimens={order.specimens} />

      {order.reports.map((report) => (
        <ProcedureReportCard
          key={report.id}
          report={report}
          disabled={disabled}
          onCreateResult={onCreateResult}
          onUpdateReport={onUpdateReport}
          onSignReport={onSignReport}
          onReopenReportReview={onReopenReportReview}
          onUpdateResult={onUpdateResult}
        />
      ))}
      {order.reports.length === 0 && <div className="timeline-placeholder">No reports recorded for this order</div>}
    </article>
  )
}

function ProcedureReportCard({
  report,
  disabled,
  onCreateResult,
  onUpdateReport,
  onSignReport,
  onReopenReportReview,
  onUpdateResult,
}: {
  report: ProcedureReportItem
  disabled: boolean
  onCreateResult: (input: ProcedureResultCreateInput) => Promise<unknown>
  onUpdateReport: (report: ProcedureReportItem, input: ProcedureReportUpdateInput) => Promise<unknown>
  onSignReport: (report: ProcedureReportItem, input: ProcedureReportSignInput) => Promise<unknown>
  onReopenReportReview: (report: ProcedureReportItem) => Promise<unknown>
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
}) {
  const [isCorrecting, setIsCorrecting] = useState(false)
  const [dateCollected, setDateCollected] = useState(report.dateCollected)
  const [dateReport, setDateReport] = useState(report.reportDate)
  const [specimenNumber, setSpecimenNumber] = useState(report.specimenNumber ?? '')
  const [reportStatus, setReportStatus] = useState(report.status ?? 'final')
  const [reviewStatus, setReviewStatus] = useState(report.reviewStatus ?? 'reviewed')
  const [notes, setNotes] = useState(report.notes ?? '')
  const [correctionStatus, setCorrectionStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [signStatus, setSignStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [reopenStatus, setReopenStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const isReviewed = (report.reviewStatus ?? '').toLowerCase() === 'reviewed'

  useEffect(() => {
    setDateCollected(report.dateCollected)
    setDateReport(report.reportDate)
    setSpecimenNumber(report.specimenNumber ?? '')
    setReportStatus(report.status ?? 'final')
    setReviewStatus(report.reviewStatus ?? 'reviewed')
    setNotes(report.notes ?? '')
    setCorrectionStatus('idle')
    setSignStatus('idle')
    setReopenStatus('idle')
  }, [report])

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

  async function handleReportCorrectionSubmit(event: FormEvent) {
    event.preventDefault()
    setCorrectionStatus('saving')

    try {
      await onUpdateReport(report, {
        dateCollected,
        dateReport,
        specimenNumber,
        reportStatus,
        reviewStatus,
        notes,
      })
      setCorrectionStatus('saved')
      setIsCorrecting(false)
    } catch {
      setCorrectionStatus('error')
    }
  }

  async function handleReportSign() {
    setSignStatus('saving')

    try {
      await onSignReport(report, {
        reviewedBy: 'admin',
        reviewedAt: '2026-06-19 14:15:00',
      })
      setSignStatus('saved')
    } catch {
      setSignStatus('error')
    }
  }

  async function handleReportReviewReopen() {
    setReopenStatus('saving')

    try {
      await onReopenReportReview(report)
      setReopenStatus('saved')
    } catch {
      setReopenStatus('error')
    }
  }

  return (
    <section className="procedure-report-card">
      <div className="procedure-report-title">
        <div>
          <strong>Report {report.id}</strong>
          <span>
            {[
              report.reportDate,
              report.dateCollected ? `Collected ${report.dateCollected}` : '',
              report.specimenNumber ? `Specimen ${report.specimenNumber}` : '',
              report.reviewStatus,
              report.reviewedBy ? `Signed by ${report.reviewedBy}` : '',
              report.reviewedAt ? `Signed ${report.reviewedAt}` : '',
              report.notes,
            ]
              .filter(Boolean)
              .join(' / ')}
          </span>
        </div>
        <span className="status-tag">{report.status || 'Status pending'}</span>
      </div>
      <div className="detail-actions compact-actions">
        <button className="icon-text-button secondary" type="button" disabled={disabled} onClick={handleCreateResult}>
          <Activity size={15} />
          Add Result
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => setIsCorrecting((current) => !current)}
        >
          <Pencil size={15} />
          Correct Report
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || signStatus === 'saving'}
          onClick={handleReportSign}
          aria-label={`Sign procedure report ${report.id}`}
        >
          <ShieldCheck size={15} />
          Sign Report
        </button>
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled || !isReviewed || reopenStatus === 'saving'}
          onClick={handleReportReviewReopen}
          aria-label={`Reopen procedure report review ${report.id}`}
        >
          <RotateCcw size={15} />
          Reopen Review
        </button>
        {correctionStatus === 'saved' && <span className="save-note">Saved</span>}
        {correctionStatus === 'error' && <span className="save-note error">Action failed</span>}
        {signStatus === 'saved' && <span className="save-note">Signed</span>}
        {signStatus === 'error' && <span className="save-note error">Sign failed</span>}
        {reopenStatus === 'saved' && <span className="save-note">Reopened</span>}
        {reopenStatus === 'error' && <span className="save-note error">Reopen failed</span>}
      </div>
      {isCorrecting && (
        <form
          className="appointment-mutation-panel encounter-procedure-result-entry-panel"
          aria-label={`Procedure report correction ${report.id}`}
          onSubmit={handleReportCorrectionSubmit}
        >
          <div className="mutation-grid encounter-procedure-result-entry-grid">
            <label className="filter-field">
              <span>Collected</span>
              <input
                value={dateCollected}
                onChange={(event) => setDateCollected(event.target.value)}
                aria-label="Procedure corrected report collected date"
                required
              />
            </label>
            <label className="filter-field">
              <span>Reported</span>
              <input
                value={dateReport}
                onChange={(event) => setDateReport(event.target.value)}
                aria-label="Procedure corrected report date"
                required
              />
            </label>
            <label className="filter-field">
              <span>Specimen</span>
              <input
                value={specimenNumber}
                onChange={(event) => setSpecimenNumber(event.target.value)}
                aria-label="Procedure corrected report specimen number"
                required
              />
            </label>
            <label className="filter-field">
              <span>Status</span>
              <select
                value={reportStatus}
                onChange={(event) => setReportStatus(event.target.value)}
                aria-label="Procedure corrected report status"
              >
                <option value="final">Final</option>
                <option value="preliminary">Preliminary</option>
                <option value="corrected">Corrected</option>
              </select>
            </label>
            <label className="filter-field">
              <span>Review</span>
              <select
                value={reviewStatus}
                onChange={(event) => setReviewStatus(event.target.value)}
                aria-label="Procedure corrected report review status"
              >
                <option value="pending">Pending</option>
                <option value="received">Received</option>
                <option value="reviewed">Reviewed</option>
                <option value="approved">Approved</option>
              </select>
            </label>
            <label className="filter-field procedure-order-name-field">
              <span>Notes</span>
              <input
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                aria-label="Procedure corrected report notes"
              />
            </label>
          </div>
          <div className="detail-actions compact-actions">
            <button className="icon-text-button primary" type="submit" disabled={correctionStatus === 'saving'}>
              <Check size={15} />
              <span>{correctionStatus === 'saving' ? 'Saving' : 'Save Report Correction'}</span>
            </button>
          </div>
        </form>
      )}
      <div className="procedure-result-grid">
        {report.results.map((result) => (
          <ProcedureResultCard key={result.id} result={result} disabled={disabled} onUpdateResult={onUpdateResult} />
        ))}
        {report.results.length === 0 && <div className="timeline-placeholder">No result rows recorded</div>}
      </div>
    </section>
  )
}

function ProcedureResultCard({
  result,
  disabled,
  onUpdateResult,
}: {
  result: ProcedureResultItem
  disabled: boolean
  onUpdateResult: (result: ProcedureResultItem, input: ProcedureResultUpdateInput) => Promise<unknown>
}) {
  const [isCorrecting, setIsCorrecting] = useState(false)
  const [resultCode, setResultCode] = useState(result.code ?? '')
  const [resultText, setResultText] = useState(result.text ?? '')
  const [resultDate, setResultDate] = useState(result.resultDate)
  const [resultValue, setResultValue] = useState(result.result ?? '')
  const [resultUnits, setResultUnits] = useState(result.units ?? '')
  const [resultRange, setResultRange] = useState(result.range ?? '')
  const [abnormalFlag, setAbnormalFlag] = useState(result.abnormal ?? '')
  const [resultStatus, setResultStatus] = useState(result.resultStatus ?? 'corrected')
  const [correctionStatus, setCorrectionStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    setResultCode(result.code ?? '')
    setResultText(result.text ?? '')
    setResultDate(result.resultDate)
    setResultValue(result.result ?? '')
    setResultUnits(result.units ?? '')
    setResultRange(result.range ?? '')
    setAbnormalFlag(result.abnormal ?? '')
    setResultStatus(result.resultStatus ?? 'corrected')
    setCorrectionStatus('idle')
  }, [result])

  async function handleCorrectionSubmit(event: FormEvent) {
    event.preventDefault()
    setCorrectionStatus('saving')

    try {
      await onUpdateResult(result, {
        resultCode,
        resultText,
        dateTime: resultDate,
        units: resultUnits,
        result: resultValue,
        range: resultRange,
        abnormal: abnormalFlag,
        status: resultStatus,
      })
      setCorrectionStatus('saved')
      setIsCorrecting(false)
    } catch {
      setCorrectionStatus('error')
    }
  }

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
      <div className="detail-actions compact-actions">
        <button
          className="icon-text-button secondary"
          type="button"
          disabled={disabled}
          onClick={() => setIsCorrecting((current) => !current)}
        >
          <Pencil size={15} />
          Correct
        </button>
        {correctionStatus === 'saved' && <span className="save-note">Saved</span>}
        {correctionStatus === 'error' && <span className="save-note error">Action failed</span>}
      </div>
      {isCorrecting && (
        <form
          className="appointment-mutation-panel encounter-procedure-result-entry-panel"
          aria-label={`Procedure result correction ${result.id}`}
          onSubmit={handleCorrectionSubmit}
        >
          <div className="mutation-grid encounter-procedure-result-entry-grid">
            <label className="filter-field">
              <span>Code</span>
              <input
                value={resultCode}
                onChange={(event) => setResultCode(event.target.value)}
                aria-label="Procedure corrected result code"
                required
              />
            </label>
            <label className="filter-field procedure-order-name-field">
              <span>Result</span>
              <input
                value={resultText}
                onChange={(event) => setResultText(event.target.value)}
                aria-label="Procedure corrected result text"
                required
              />
            </label>
            <label className="filter-field">
              <span>Date</span>
              <input
                value={resultDate}
                onChange={(event) => setResultDate(event.target.value)}
                aria-label="Procedure corrected result date"
                required
              />
            </label>
            <label className="filter-field">
              <span>Status</span>
              <select
                value={resultStatus}
                onChange={(event) => setResultStatus(event.target.value)}
                aria-label="Procedure corrected result status"
              >
                <option value="final">Final</option>
                <option value="preliminary">Preliminary</option>
                <option value="corrected">Corrected</option>
              </select>
            </label>
            <label className="filter-field">
              <span>Value</span>
              <input
                value={resultValue}
                onChange={(event) => setResultValue(event.target.value)}
                aria-label="Procedure corrected result value"
                required
              />
            </label>
            <label className="filter-field">
              <span>Units</span>
              <input
                value={resultUnits}
                onChange={(event) => setResultUnits(event.target.value)}
                aria-label="Procedure corrected result units"
              />
            </label>
            <label className="filter-field">
              <span>Range</span>
              <input
                value={resultRange}
                onChange={(event) => setResultRange(event.target.value)}
                aria-label="Procedure corrected result range"
              />
            </label>
            <label className="filter-field">
              <span>Flag</span>
              <input
                value={abnormalFlag}
                onChange={(event) => setAbnormalFlag(event.target.value)}
                aria-label="Procedure corrected result abnormal flag"
              />
            </label>
          </div>
          <div className="detail-actions compact-actions">
            <button className="icon-text-button primary" type="submit" disabled={correctionStatus === 'saving'}>
              <Check size={15} />
              <span>{correctionStatus === 'saving' ? 'Saving' : 'Save Correction'}</span>
            </button>
          </div>
        </form>
      )}
    </article>
  )
}

function ProblemPanel({
  items,
  onDeactivate,
  onDelete,
  disabled,
}: {
  items: ProblemListItem[]
  onDeactivate: (problem: ProblemListItem) => Promise<unknown>
  onDelete: (problem: ProblemListItem) => Promise<void>
  disabled: boolean
}) {
  return (
    <ClinicalSection title="Problems" icon={ClipboardList} emptyText="No active problems">
      {items.map((item) => (
        <ClinicalItem key={item.id} title={item.title} meta={item.diagnosis} date={item.date} note={item.comments}>
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
            <button className="icon-text-button" type="button" disabled={disabled} onClick={() => void onDelete(item)}>
              <Trash2 size={14} />
              Delete
            </button>
          </div>
        </ClinicalItem>
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

function MedicationPanel({
  items,
  onDeactivate,
  onDelete,
  disabled,
}: {
  items: MedicationListItem[]
  onDeactivate: (medication: MedicationListItem) => Promise<unknown>
  onDelete: (medication: MedicationListItem) => Promise<void>
  disabled: boolean
}) {
  return (
    <ClinicalSection title="Medication List" icon={HeartPulse} emptyText="No active medications">
      {items.map((item) => (
        <ClinicalItem key={item.id} title={item.title} meta={item.diagnosis} date={item.date} note={item.comments}>
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
      {items.length === 0 && <div className="timeline-placeholder">No active medications</div>}
    </ClinicalSection>
  )
}

function ImmunizationPanel({
  items,
  onMarkEnteredInError,
  onDelete,
  disabled,
}: {
  items: ImmunizationListItem[]
  onMarkEnteredInError: (immunization: ImmunizationListItem) => Promise<unknown>
  onDelete: (immunization: ImmunizationListItem) => Promise<void>
  disabled: boolean
}) {
  return (
    <ClinicalSection title="Immunizations" icon={Syringe} emptyText="No immunizations recorded">
      {items.map((item) => (
        <ClinicalItem
          key={item.key}
          title={item.vaccine}
          meta={[item.cvxCode ? `CVX ${item.cvxCode}` : null, item.completionStatus, item.route].filter(Boolean).join(' / ')}
          date={item.administeredAt}
          note={[item.manufacturer, item.lotNumber ? `Lot ${item.lotNumber}` : null, item.administrationSite].filter(Boolean).join(' / ')}
        >
          <p className="clinical-item-note">
            {[item.amountAdministered ? `${item.amountAdministered} ${item.amountAdministeredUnit ?? ''}`.trim() : null, item.visDate ? `VIS ${item.visDate}` : null, item.administeredBy ? `By ${item.administeredBy}` : null, item.note]
              .filter(Boolean)
              .join(' / ')}
          </p>
          <div className="clinical-item-actions">
            <button
              className="icon-text-button danger"
              type="button"
              disabled={disabled}
              onClick={() => void onMarkEnteredInError(item)}
            >
              <Ban size={14} />
              Entered in Error
            </button>
            <button className="icon-text-button" type="button" disabled={disabled} onClick={() => void onDelete(item)}>
              <Trash2 size={14} />
              Delete
            </button>
          </div>
        </ClinicalItem>
      ))}
      {items.length === 0 && <div className="timeline-placeholder">No immunizations recorded</div>}
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

function InsuranceCoverageList({
  items,
  loading,
  disabled,
  onEdit,
  onDelete,
}: {
  items: PatientInsuranceItem[]
  loading: boolean
  disabled: boolean
  onEdit: (item: PatientInsuranceItem) => void
  onDelete: (item: PatientInsuranceItem) => void
}) {
  if (loading) {
    return <div className="empty-state inline">Loading coverage</div>
  }

  if (items.length === 0) {
    return <div className="empty-state inline">No coverage recorded</div>
  }

  return (
    <div className="insurance-list" aria-label="Insurance coverage">
      {items.map((item) => (
        <article className="insurance-item" key={item.id}>
          <div className="insurance-item-title">
            <span>{formatCoverageType(item.type)}</span>
            <strong>{item.provider || 'Not recorded'}</strong>
          </div>
          <Field label="Plan" value={item.planName} />
          <Field label="Policy" value={item.policyNumber} />
          <Field label="Group" value={item.groupNumber} />
          <Field label="Relationship" value={formatCoverageRelationship(item.relationship)} />
          <div className="insurance-actions">
            <button className="icon-text-button" type="button" disabled={disabled} onClick={() => onEdit(item)}>
              <Pencil size={14} />
              Edit
            </button>
            <button className="icon-text-button danger" type="button" disabled={disabled} onClick={() => onDelete(item)}>
              <Trash2 size={14} />
              Delete
            </button>
          </div>
        </article>
      ))}
    </div>
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

function formatAuditDateTime(value?: string | null) {
  if (!value) {
    return null
  }

  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

function formatEncounterSensitivity(value?: string | null) {
  if (!value) {
    return null
  }

  const normalized = value.toLowerCase()
  if (normalized === 'normal') {
    return 'Normal'
  }

  if (normalized === 'high') {
    return 'High'
  }

  return value
}

function formatCoverageType(value?: string | null) {
  if (!value) {
    return 'Coverage'
  }

  return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase()
}

function formatCoverageRelationship(value?: string | null) {
  return value ? formatCoverageType(value) : null
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

function countBillingClaims(encounters: BillingEncounterItem[] | undefined) {
  return encounters?.reduce((count, encounter) => count + encounter.claims.length, 0) ?? 0
}

function countBillingPayments(encounters: BillingEncounterItem[] | undefined) {
  return encounters?.reduce((count, encounter) => count + encounter.payments.length, 0) ?? 0
}

function countBillingLinesByType(encounters: BillingEncounterItem[] | undefined, codeType: string) {
  return (
    encounters?.reduce(
      (count, encounter) => count + encounter.lines.filter((line) => line.codeType === codeType).length,
      0,
    ) ?? 0
  )
}

function formatPayerType(value: number) {
  if (value === 1) return 'Primary'
  if (value === 2) return 'Secondary'
  if (value === 3) return 'Tertiary'
  return 'Patient'
}

function formatPaymentPayer(payment: BillingPaymentItem) {
  if (payment.payerType === 0) {
    return 'Patient'
  }

  return `${formatPayerType(payment.payerType)} ${payment.payerName || 'Payer'}`
}

function countUsersByRole(users: AdministrationUserItem[] | undefined, role: string) {
  return users?.filter((user) => user.role === role).length ?? 0
}

function countProcedureReports(orders: ProcedureOrderItem[] | undefined) {
  return orders?.reduce((count, order) => count + order.reports.length, 0) ?? 0
}

function countProcedureSpecimens(orders: ProcedureOrderItem[] | undefined) {
  return orders?.reduce((count, order) => count + order.specimens.length, 0) ?? 0
}

function formatSpecimenVolume(specimen: ProcedureSpecimenItem) {
  if (specimen.volumeValue === null || specimen.volumeValue === undefined) {
    return null
  }

  return `${specimen.volumeValue} ${specimen.volumeUnit || ''}`.trim()
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

function buildDemographicsDraft(patient: PatientListItem | PatientChartSummary | null): PatientDemographicsUpdate {
  const chart = hasChartDemographics(patient) ? patient : null

  return {
    firstName: patient?.firstName ?? '',
    lastName: patient?.lastName ?? '',
    preferredName: patient?.preferredName ?? '',
    sex: patient?.sex ?? '',
    dateOfBirth: patient?.dateOfBirth ?? '',
    street: chart?.street ?? '',
    city: chart?.city ?? '',
    state: chart?.state ?? '',
    postalCode: chart?.postalCode ?? '',
    maritalStatus: chart?.maritalStatus ?? '',
    occupation: chart?.occupation ?? '',
  }
}

function buildDeceasedStatusDraft(patient: PatientChartSummary | null): PatientDeceasedStatusUpdate {
  return {
    deceasedDate: patient?.deceasedDate ?? '',
    deceasedReason: patient?.deceasedReason ?? '',
  }
}

function buildGuardianContactDraft(patient: PatientChartSummary | null): PatientGuardianContactUpdate {
  return {
    motherName: patient?.motherName ?? '',
    guardianName: patient?.guardianName ?? '',
    guardianRelationship: patient?.guardianRelationship ?? '',
    guardianPhone: patient?.guardianPhone ?? '',
    guardianEmail: patient?.guardianEmail ?? '',
    guardianSex: patient?.guardianSex ?? '',
    guardianAddress: patient?.guardianAddress ?? '',
    guardianCity: patient?.guardianCity ?? '',
    guardianState: patient?.guardianState ?? '',
    guardianPostalCode: patient?.guardianPostalCode ?? '',
    guardianCountry: patient?.guardianCountry ?? '',
    guardianWorkPhone: patient?.guardianWorkPhone ?? '',
  }
}

function formatGuardianRelationship(value: string | null | undefined) {
  const labels: Record<string, string> = {
    associate: 'Associate',
    brother: 'Brother',
    care_giver: 'Care giver',
    child: 'Child',
    father: 'Father',
    guardian: 'Guardian',
    mother: 'Mother',
    parent: 'Parent',
    sibling: 'Sibling',
    sister: 'Sister',
    spouse: 'Spouse',
  }
  return value ? (labels[value] ?? value) : ''
}

function formatGuardianSex(value: string | null | undefined) {
  const labels: Record<string, string> = {
    Female: 'Female',
    Male: 'Male',
    UNK: 'Unknown',
  }
  return value ? (labels[value] ?? value) : ''
}

function buildRegistrationDraft(): PatientRegistrationInput {
  return {
    pubpid: '',
    firstName: '',
    lastName: '',
    preferredName: '',
    sex: '',
    dateOfBirth: '',
    street: '',
    city: '',
    state: 'CT',
    postalCode: '',
    maritalStatus: 'single',
    occupation: '',
    phoneHome: '',
    phoneCell: '',
    email: '',
    hipaaAllowSms: 'YES',
    hipaaAllowEmail: 'YES',
  }
}

function buildInsuranceDraft(item?: PatientInsuranceItem | null): PatientInsuranceMutationInput {
  return {
    type: item?.type ?? 'tertiary',
    provider: item?.provider ?? 'Parity Coverage Co',
    planName: item?.planName ?? 'Continuity Bridge',
    policyNumber: item?.policyNumber ?? 'PAR-TEMP-1005',
    groupNumber: item?.groupNumber ?? 'PAR-GRP-1005',
    relationship: item?.relationship ?? 'self',
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

function hasChartDemographics(patient: PatientListItem | PatientChartSummary | null): patient is PatientChartSummary {
  return Boolean(patient && 'street' in patient && 'maritalStatus' in patient)
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

function parseDateList(value: string) {
  return Array.from(
    new Set(
      value
        .split(/[\s,;]+/)
        .map((item) => item.trim())
        .filter((item) => /^\d{4}-\d{2}-\d{2}$/.test(item)),
    ),
  ).sort()
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

async function readFileAsBase64(file: File) {
  const bytes = new Uint8Array(await file.arrayBuffer())
  let binary = ''
  const chunkSize = 0x8000

  for (let index = 0; index < bytes.length; index += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize))
  }

  return btoa(binary)
}

export default App
