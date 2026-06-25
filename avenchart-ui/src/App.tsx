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
import PatientSearch from './pages/clinician/PatientSearch.tsx'
import PatientShell from './pages/clinician/PatientShell.tsx'
import PatientSummary from './pages/clinician/PatientSummary.tsx'
import PatientChart from './pages/clinician/PatientChart.tsx'
import PatientEncounters from './pages/clinician/PatientEncounters.tsx'
import PatientDocuments from './pages/clinician/PatientDocuments.tsx'
import PatientLabs from './pages/clinician/PatientLabs.tsx'
import PatientAppointments from './pages/clinician/PatientAppointments.tsx'
import PatientMessages from './pages/clinician/PatientMessages.tsx'
import ClinicianCalendar from './pages/clinician/ClinicianCalendar.tsx'
import LabQueue from './pages/clinician/LabQueue.tsx'
import OperationalReports from './pages/clinician/OperationalReports.tsx'
import AdminDirectory from './pages/clinician/AdminDirectory.tsx'

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
          <Route path="labs" element={<LabQueue />} />
          <Route path="reports" element={<OperationalReports />} />
          <Route path="admin" element={<AdminDirectory />} />

          {/* Patient search */}
          <Route path="patients" element={<PatientSearch />} />

          {/* Patient chart shell — nested */}
          <Route path="patients/:patientId" element={<PatientShell />}>
            <Route path="summary" element={<PatientSummary />} />
            <Route path="chart" element={<PatientChart />} />
            <Route path="encounters" element={<PatientEncounters />} />
            <Route path="documents" element={<PatientDocuments />} />
            <Route path="labs" element={<PatientLabs />} />
            <Route path="appointments" element={<PatientAppointments />} />
            <Route path="messages" element={<PatientMessages />} />
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
