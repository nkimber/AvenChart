import { useOutletContext } from 'react-router-dom'
import { LogOut } from 'lucide-react'
import type { PortalOutletContext } from './PortalShell.tsx'

export default function PortalAccount() {
  const { session, home, signOut } = useOutletContext<PortalOutletContext>()

  return (
    <div className="portal-page">
      <section className="portal-section">
        <div className="account-avatar-row">
          <div className="account-avatar">
            {session.displayName
              .split(' ')
              .filter(Boolean)
              .slice(0, 2)
              .map((p) => p[0]?.toUpperCase())
              .join('')}
          </div>
          <div>
            <p className="account-name">{session.displayName}</p>
            <p className="muted">{session.portalUsername}</p>
          </div>
        </div>
      </section>

      <section className="portal-section">
        <h2 className="portal-section-title" style={{ marginBottom: 14 }}>Account details</h2>
        <ul className="fact-list" style={{ marginBottom: 0 }}>
          <li className="fact-row">
            <span>Portal username</span>
            <span>{session.portalUsername}</span>
          </li>
          <li className="fact-row">
            <span>Display name</span>
            <span>{session.displayName}</span>
          </li>
          {home && (
            <>
              <li className="fact-row">
                <span>Upcoming appointments</span>
                <span>{home.upcomingAppointmentCount}</span>
              </li>
              <li className="fact-row">
                <span>Inbox messages</span>
                <span>{home.messages.totalMessages}</span>
              </li>
            </>
          )}
        </ul>
      </section>

      <section className="portal-section">
        <h2 className="portal-section-title" style={{ marginBottom: 14 }}>Session</h2>
        <p className="muted" style={{ marginBottom: 16, fontSize: 13 }}>
          You are currently signed in to the patient portal. Sign out to end your session on this device.
        </p>
        <button className="button-sign-out" type="button" onClick={signOut}>
          <LogOut size={16} />
          Sign out
        </button>
      </section>
    </div>
  )
}
