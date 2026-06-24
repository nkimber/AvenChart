import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom'
import { ToastContainer } from './components/Toast.tsx'
import EntryChooser from './pages/EntryChooser.tsx'
import ClinicianLogin from './pages/ClinicianLogin.tsx'
import ClinicianHome from './pages/ClinicianHome.tsx'
import PortalLogin from './pages/PortalLogin.tsx'
import PortalShell from './pages/portal/PortalShell.tsx'
import PortalDashboard from './pages/portal/PortalDashboard.tsx'
import PortalMessages from './pages/portal/PortalMessages.tsx'
import PortalAppointments from './pages/portal/PortalAppointments.tsx'
import PortalRecords from './pages/portal/PortalRecords.tsx'
import PortalAccount from './pages/portal/PortalAccount.tsx'

export default function App() {
  return (
    <BrowserRouter>
      <ToastContainer />
      <Routes>
        <Route path="/" element={<EntryChooser />} />
        <Route path="/login" element={<ClinicianLogin />} />
        <Route path="/home" element={<ClinicianHome />} />
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
