import { lazy, Suspense } from 'react'
import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom'
import { ToastContainer } from './components/Toast.tsx'
import { AppErrorBoundary } from './components/AppErrorBoundary.tsx'
import EntryChooser from './pages/EntryChooser.tsx'
import ClinicianLogin from './pages/ClinicianLogin.tsx'
import PortalLogin from './pages/PortalLogin.tsx'

const PortalShell = lazy(() => import('./pages/portal/PortalShell.tsx'))
const PortalDashboard = lazy(() => import('./pages/portal/PortalDashboard.tsx'))
const PortalMessages = lazy(() => import('./pages/portal/PortalMessages.tsx'))
const PortalAppointments = lazy(() => import('./pages/portal/PortalAppointments.tsx'))
const PortalRecords = lazy(() => import('./pages/portal/PortalRecords.tsx'))
const PortalAccount = lazy(() => import('./pages/portal/PortalAccount.tsx'))
const ClinicianShell = lazy(() => import('./pages/clinician/ClinicianShell.tsx'))
const ClinicianDashboard = lazy(() => import('./pages/clinician/ClinicianDashboard.tsx'))
const ClinicianSchedule = lazy(() => import('./pages/clinician/ClinicianSchedule.tsx'))
const ClinicianMessages = lazy(() => import('./pages/clinician/ClinicianMessages.tsx'))
const PatientSearch = lazy(() => import('./pages/clinician/PatientSearch.tsx'))
const PatientShell = lazy(() => import('./pages/clinician/PatientShell.tsx'))
const PatientSummary = lazy(() => import('./pages/clinician/PatientSummary.tsx'))
const PatientChart = lazy(() => import('./pages/clinician/PatientChart.tsx'))
const PatientTimeline = lazy(() => import('./pages/clinician/PatientTimeline.tsx'))
const PatientEncounters = lazy(() => import('./pages/clinician/PatientEncounters.tsx'))
const PatientDocuments = lazy(() => import('./pages/clinician/PatientDocuments.tsx'))
const PatientLabs = lazy(() => import('./pages/clinician/PatientLabs.tsx'))
const PatientAppointments = lazy(() => import('./pages/clinician/PatientAppointments.tsx'))
const PatientMessages = lazy(() => import('./pages/clinician/PatientMessages.tsx'))
const PatientReferrals = lazy(() => import('./pages/clinician/PatientReferrals.tsx'))
const PatientAuthorizations = lazy(() => import('./pages/clinician/PatientAuthorizations.tsx'))
const PatientSdoh = lazy(() => import('./pages/clinician/PatientSdoh.tsx'))
const PatientPrintOutputs = lazy(() => import('./pages/clinician/PatientPrintOutputs.tsx'))
const ClinicianCalendar = lazy(() => import('./pages/clinician/ClinicianCalendar.tsx'))
const FlowBoard = lazy(() => import('./pages/clinician/FlowBoard.tsx'))
const LabQueue = lazy(() => import('./pages/clinician/LabQueue.tsx'))
const OperationalReports = lazy(() => import('./pages/clinician/OperationalReports.tsx'))
const TherapyGroups = lazy(() => import('./pages/clinician/TherapyGroups.tsx'))
const BillingWorkspace = lazy(() => import('./pages/clinician/BillingWorkspace.tsx'))
const InventoryWorkspace = lazy(() => import('./pages/clinician/InventoryWorkspace.tsx'))
const AdminDirectory = lazy(() => import('./pages/clinician/AdminDirectory.tsx'))
const NewEncounter = lazy(() => import('./pages/clinician/NewEncounter.tsx'))
const NewPatient = lazy(() => import('./pages/clinician/NewPatient.tsx'))
const PrescriptionRenewals = lazy(
  () => import('./pages/clinician/PrescriptionRenewals.tsx'),
)
const SchedulingOperations = lazy(
  () => import('./pages/clinician/SchedulingOperations.tsx'),
)
const OfficeNotes = lazy(() => import('./pages/clinician/OfficeNotes.tsx'))
const AddressBook = lazy(() => import('./pages/clinician/AddressBook.tsx'))
const TrackAnything = lazy(() => import('./pages/clinician/TrackAnything.tsx'))
const EncounterTracks = lazy(() => import('./pages/clinician/EncounterTracks.tsx'))
const PatientTrackHistory = lazy(
  () => import('./pages/clinician/PatientTrackHistory.tsx'),
)
const PatientEducation = lazy(() => import('./pages/clinician/PatientEducation.tsx'))
const RecallBoard = lazy(() => import('./pages/clinician/RecallBoard.tsx'))
const BatchCommunication = lazy(
  () => import('./pages/clinician/BatchCommunication.tsx'),
)
const ChartTracker = lazy(() => import('./pages/clinician/ChartTracker.tsx'))
const DocumentTemplates = lazy(
  () => import('./pages/clinician/DocumentTemplates.tsx'),
)
const DocumentRoutingQueue = lazy(
  () => import('./pages/clinician/DocumentRoutingQueue.tsx'),
)
const DocumentOcrQueue = lazy(
  () => import('./pages/clinician/DocumentOcrQueue.tsx'),
)
const DuplicateReview = lazy(() => import('./pages/clinician/DuplicateReview.tsx'))

export default function App() {
  return (
      <BrowserRouter>
      <ToastContainer />
      <AppErrorBoundary>
        <Suspense
          fallback={
            <main className="route-loading" aria-live="polite" aria-busy="true">
              Loading workspace…
            </main>
          }
        >
          <Routes>
        <Route path="/" element={<EntryChooser />} />
        <Route path="/login" element={<ClinicianLogin />} />
        {/* Legacy redirect */}
        <Route path="/home" element={<Navigate to="/clinician/dashboard" replace />} />

        {/* Clinician application */}
        <Route path="/clinician" element={<ClinicianShell />}>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<ClinicianDashboard />} />
          <Route path="schedule" element={<ClinicianSchedule />} />
          <Route path="calendar" element={<ClinicianCalendar />} />
          <Route path="flow" element={<FlowBoard />} />
          <Route path="scheduling" element={<SchedulingOperations />} />
          <Route path="labs" element={<LabQueue />} />
          <Route path="messages" element={<ClinicianMessages />} />
          <Route path="office-notes" element={<OfficeNotes />} />
          <Route path="address-book" element={<AddressBook />} />
          <Route path="tracks" element={<TrackAnything />} />
          <Route path="track-entries" element={<EncounterTracks />} />
          <Route path="track-history" element={<PatientTrackHistory />} />
          <Route path="patient-education" element={<PatientEducation />} />
          <Route path="recalls" element={<RecallBoard />} />
          <Route path="batch-communication" element={<BatchCommunication />} />
          <Route path="chart-tracker" element={<ChartTracker />} />
          <Route path="document-templates" element={<DocumentTemplates />} />
          <Route path="documents" element={<DocumentRoutingQueue />} />
          <Route path="document-ocr" element={<DocumentOcrQueue />} />
          <Route path="duplicate-review" element={<DuplicateReview />} />
          <Route path="renewals" element={<PrescriptionRenewals />} />
          <Route path="reports" element={<OperationalReports />} />
          <Route path="groups" element={<TherapyGroups />} />
          <Route path="billing" element={<BillingWorkspace />} />
          <Route path="inventory" element={<InventoryWorkspace />} />
          <Route path="admin" element={<AdminDirectory />} />

          {/* Standalone new encounter (no patient context) */}
          <Route path="encounters/new" element={<NewEncounter />} />

          {/* Patient search & registration */}
          <Route path="patients" element={<PatientSearch />} />
          <Route path="patients/new" element={<NewPatient />} />

          {/* Patient chart shell — nested */}
          <Route path="patients/:patientId" element={<PatientShell />}>
            <Route path="summary" element={<PatientSummary />} />
            <Route path="chart" element={<PatientChart />} />
            <Route path="timeline" element={<PatientTimeline />} />
            <Route path="encounters" element={<PatientEncounters />} />
            <Route path="encounters/new" element={<NewEncounter />} />
            <Route path="documents" element={<PatientDocuments />} />
            <Route path="labs" element={<PatientLabs />} />
            <Route path="appointments" element={<PatientAppointments />} />
            <Route path="messages" element={<PatientMessages />} />
            <Route path="referrals" element={<PatientReferrals />} />
            <Route path="authorizations" element={<PatientAuthorizations />} />
            <Route path="sdoh" element={<PatientSdoh />} />
            <Route path="print" element={<PatientPrintOutputs />} />
          </Route>

          {/* Catch-all within clinician */}
          <Route path="*" element={<Navigate to="dashboard" replace />} />
        </Route>

        {/* Patient portal */}
        <Route path="/portal/login" element={<PortalLogin />} />
        <Route path="/portal" element={<PortalShell />}>
          <Route index element={<Navigate to="home" replace />} />
          <Route path="home" element={<PortalDashboard />} />
          <Route path="messages" element={<PortalMessages />} />
          <Route path="appointments" element={<PortalAppointments />} />
          <Route path="records" element={<PortalRecords />} />
          <Route path="account" element={<PortalAccount />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </AppErrorBoundary>
    </BrowserRouter>
  )
}
