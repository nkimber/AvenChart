// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ShieldCheck, Stethoscope } from 'lucide-react'
import { login } from '../api.ts'
import { saveClinicianSession } from '../auth/session.ts'
import LegalAttribution from '../components/LegalAttribution.tsx'
import { ClinicianIllustration } from '../illustrations.tsx'

export default function ClinicianLogin() {
  const navigate = useNavigate()
  const [username, setUsername] = useState('admin')
  const [password, setPassword] = useState('pass')
  const [status, setStatus] = useState<'idle' | 'checking' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setStatus('checking')
    setError(null)
    try {
      const result = await login({ username, password })
      if (!result.authenticated || !result.sessionId) {
        setStatus('error')
        setError(result.failureReason ?? 'Those credentials were not recognized.')
        return
      }
      saveClinicianSession({
        sessionId: result.sessionId,
        username: result.username,
        displayName: result.displayName,
        role: result.role,
        staffId: result.staffId,
        facilityId: result.accessContext?.defaultFacilityId ?? null,
        purposeOfUse: result.accessContext?.defaultPurposeOfUse || 'treatment',
        facilities: result.accessContext?.facilities ?? [],
        purposes: result.accessContext?.purposes ?? ['treatment'],
      })
      navigate('/home')
    } catch (err) {
      setStatus('error')
      setError(err instanceof Error ? err.message : 'Sign-in failed.')
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-hero">
        <div className="auth-hero-brand">
          <span className="auth-hero-brand-mark" aria-hidden="true">
            <Stethoscope size={16} />
          </span>
          Professional sign-in
        </div>
        <h1 className="auth-hero-title">Everything you need for today's patients, in one place.</h1>
        <p className="auth-hero-text">
          Schedules, charts, and messages from the AvenChart backend, in a faster, cleaner workspace.
        </p>
        <div className="auth-hero-illustration">
          <ClinicianIllustration />
        </div>
        <div className="auth-hero-badges">
          <span className="auth-hero-badge">
            <span className="auth-hero-badge-icon" aria-hidden="true">
              <ShieldCheck size={12} />
            </span>
            Real-time, secure session
          </span>
        </div>
      </div>
      <div className="auth-panel">
        <div className="auth-card">
          <p className="eyebrow">Professional sign-in</p>
          <h2 className="title">Welcome back</h2>
          <p className="subtitle">Sign in with your AvenChart staff credentials.</p>

          <div className="hint-banner">Demo credentials are pre-filled: admin / pass.</div>

          {error && <div className="error-banner">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="field">
              <label className="label" htmlFor="clinician-username">Username</label>
              <input
                id="clinician-username"
                className="input"
                type="text"
                name="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                spellCheck={false}
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="clinician-password">Password</label>
              <input
                id="clinician-password"
                className="input"
                type="password"
                name="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
              />
            </div>
            <button className="button-primary" type="submit" disabled={status === 'checking'}>
              {status === 'checking' ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
          <LegalAttribution />
        </div>
      </div>
    </div>
  )
}
