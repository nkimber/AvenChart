import { Link } from 'react-router-dom'
import { ArrowRight, ShieldCheck, Stethoscope, Users } from 'lucide-react'
import { WelcomeIllustration } from '../illustrations.tsx'

export default function EntryChooser() {
  return (
    <div className="auth-shell">
      <div className="auth-hero">
        <div className="auth-hero-brand">
          <span className="auth-hero-brand-mark">
            <ShieldCheck size={16} />
          </span>
          Legacy EHR · Modern UI
        </div>
        <h1 className="auth-hero-title">One system, built for everyone who touches a patient's care.</h1>
        <p className="auth-hero-text">
          A design-first client for the modernized Legacy EHR backend — fast, focused screens for staff and patients
          alike.
        </p>
        <div className="auth-hero-illustration">
          <WelcomeIllustration />
        </div>
        <div className="auth-hero-badges">
          <span className="auth-hero-badge">
            <span className="auth-hero-badge-icon">
              <ShieldCheck size={12} />
            </span>
            Same secure backend
          </span>
          <span className="auth-hero-badge">
            <span className="auth-hero-badge-icon">
              <Users size={12} />
            </span>
            Built for staff &amp; patients
          </span>
        </div>
      </div>
      <div className="auth-panel">
        <div className="auth-card auth-card-wide">
          <p className="eyebrow">Sign in</p>
          <h1 className="title">Choose how you'd like to sign in</h1>
          <p className="subtitle">
            This is a separate, design-first client that talks to the same modernized Legacy EHR backend.
          </p>
          <div className="chooser-grid">
            <Link to="/login" className="chooser-tile">
              <span className="chooser-tile-icon">
                <Stethoscope size={20} />
              </span>
              <span className="chooser-tile-body">
                <p className="chooser-tile-title">Professional sign-in</p>
                <p className="chooser-tile-desc">For clinicians and staff using the modernized system.</p>
              </span>
              <ArrowRight className="chooser-tile-arrow" size={18} />
            </Link>
            <Link to="/portal/login" className="chooser-tile">
              <span className="chooser-tile-icon">
                <Users size={20} />
              </span>
              <span className="chooser-tile-body">
                <p className="chooser-tile-title">Patient portal</p>
                <p className="chooser-tile-desc">For patients accessing their own records and messages.</p>
              </span>
              <ArrowRight className="chooser-tile-arrow" size={18} />
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}
