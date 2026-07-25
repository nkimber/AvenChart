import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom'
import { ToastContainer } from './components/Toast.tsx'
import EntryChooser from './pages/EntryChooser.tsx'
import ClinicianLogin from './pages/ClinicianLogin.tsx'
import PortalLogin from './pages/PortalLogin.tsx'
import PortalShell from './pages/portal/PortalShell.tsx'
import PortalDashboard from './pages/portal/PortalDashboard.tsx'
import PortalMessages from './pages/portal/PortalMessages.tsx'
import PortalAppointments from './pages/portal/PortalAppointments.tsx'
import PortalRecords from './pages/portal/PortalRecords.tsx'
import PortalAccount from './pages/portal/PortalAccount.tsx'
import ClinicianShell from './pages/clinician/ClinicianShell.tsx'
import ClinicianDashboard from './pages/clinician/ClinicianDashboard.tsx'
import ClinicianSchedule from './pages/clinician/ClinicianSchedule.tsx'
import ClinicianMessages from './pages/clinician/ClinicianMessages.tsx'
import PatientSearch from './pages/clinician/PatientSearch.tsx'
import PatientShell from './pages/clinician/PatientShell.tsx'
import PatientSummary from './pages/clinician/PatientSummary.tsx'
import PatientChart from './pages/clinician/PatientChart.tsx'
import PatientTimeline from './pages/clinician/PatientTimeline.tsx'
import PatientEncounters from './pages/clinician/PatientEncounters.tsx'
import PatientDocuments from './pages/clinician/PatientDocuments.tsx'
import PatientLabs from './pages/clinician/PatientLabs.tsx'
import PatientAppointments from './pages/clinician/PatientAppointments.tsx'
import PatientMessages from './pages/clinician/PatientMessages.tsx'
import PatientReferrals from './pages/clinician/PatientReferrals.tsx'
import PatientAuthorizations from './pages/clinician/PatientAuthorizations.tsx'
import PatientSdoh from './pages/clinician/PatientSdoh.tsx'
import ClinicianCalendar from './pages/clinician/ClinicianCalendar.tsx'
import FlowBoard from './pages/clinician/FlowBoard.tsx'
import LabQueue from './pages/clinician/LabQueue.tsx'
import OperationalReports from './pages/clinician/OperationalReports.tsx'
import TherapyGroups from './pages/clinician/TherapyGroups.tsx'
import BillingWorkspace from './pages/clinician/BillingWorkspace.tsx'
import InventoryWorkspace from './pages/clinician/InventoryWorkspace.tsx'
import AdminDirectory from './pages/clinician/AdminDirectory.tsx'
import NewEncounter from './pages/clinician/NewEncounter.tsx'
import NewPatient from './pages/clinician/NewPatient.tsx'
import PrescriptionRenewals from './pages/clinician/PrescriptionRenewals.tsx'
import SchedulingOperations from './pages/clinician/SchedulingOperations.tsx'
import OfficeNotes from './pages/clinician/OfficeNotes.tsx'
import AddressBook from './pages/clinician/AddressBook.tsx'

export default function App() {
  return (
    <BrowserRouter>
      <ToastContainer />
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
    </BrowserRouter>
  )
}
