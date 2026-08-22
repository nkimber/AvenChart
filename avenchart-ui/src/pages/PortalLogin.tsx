// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { HeartPulse, ShieldCheck } from 'lucide-react'
import { loginPatientPortal } from '../api.ts'
import { savePortalSession } from '../auth/session.ts'
import {
  getBrowserOidcConfiguration,
  startBrowserOidcSignIn,
  type BrowserOidcConfiguration,
} from '../auth/browserOidc.ts'
import LegalAttribution from '../components/LegalAttribution.tsx'
import { PatientIllustration } from '../illustrations.tsx'

export default function PortalLogin() {
  const navigate = useNavigate()
  const [username, setUsername] = useState('mod-pat-0004@example.test')
  const [password, setPassword] = useState('PortalPass207!')
  const [status, setStatus] = useState<'idle' | 'checking' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [identityConfiguration, setIdentityConfiguration] = useState<BrowserOidcConfiguration | null>(null)
  const errorReference = useRef<HTMLDivElement>(null)
  const errorId = 'portal-sign-in-error'

  useEffect(() => {
    if (error) errorReference.current?.focus()
  }, [error])

  useEffect(() => {
    const controller = new AbortController()
    getBrowserOidcConfiguration(controller.signal)
      .then((configuration) => setIdentityConfiguration(configuration))
      .catch(() => {
        // Retain the local portal path when the API cannot be reached. An
        // external deployment replaces it after its public configuration loads.
      })
    return () => controller.abort()
  }, [])

  const externalIdentityMode = identityConfiguration?.mode === 'oidc'
    || identityConfiguration?.mode === 'test-oidc'

  function handleBrowserSingleSignOn() {
    if (!identityConfiguration) return
    setStatus('checking')
    setError(null)
    try {
      startBrowserOidcSignIn(identityConfiguration, 'portal')
    } catch (err) {
      setStatus('error')
      setError(err instanceof Error ? err.message : 'Single sign-on could not be started.')
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setStatus('checking')
    setError(null)
    try {
      const result = await loginPatientPortal({ username, password })
      if (!result.authenticated || !result.sessionId) {
        setStatus('error')
        setError(result.failureReason ?? 'Those credentials were not recognized.')
        return
      }
      savePortalSession({
        sessionId: result.sessionId,
        username: result.username,
        portalUsername: result.portalUsername,
        displayName: result.displayName,
      })
      navigate('/portal/home')
    } catch (err) {
      setStatus('error')
      setError(err instanceof Error ? err.message : 'Sign-in failed.')
    }
  }

  return (
    <main id="main-content" className="auth-shell" tabIndex={-1}>
      <div className="auth-hero">
        <div className="auth-hero-brand">
          <span className="auth-hero-brand-mark" aria-hidden="true">
            <HeartPulse size={16} />
          </span>
          Patient portal
        </div>
        <h1 className="auth-hero-title">Your records, your messages, your care — always within reach.</h1>
        <p className="auth-hero-text">
          Check appointments, message your care team, and review results, all in one secure place.
        </p>
        <div className="auth-hero-illustration">
          <PatientIllustration />
        </div>
        <div className="auth-hero-badges">
          <span className="auth-hero-badge">
            <span className="auth-hero-badge-icon" aria-hidden="true">
              <ShieldCheck size={12} />
            </span>
            Private &amp; secure
          </span>
        </div>
      </div>
      <div className="auth-panel">
        <div className="auth-card">
          <p className="eyebrow">Patient portal</p>
          <h2 className="title">Hello, welcome back</h2>
          <p className="subtitle">
            {externalIdentityMode
              ? 'Continue with your organization’s approved identity provider.'
              : 'Sign in to view your messages and appointments.'}
          </p>

          {!externalIdentityMode && (
            <div className="hint-banner">Demo credentials are pre-filled: mod-pat-0004@example.test / PortalPass207!</div>
          )}

          {error && (
            <div
              ref={errorReference}
              id={errorId}
              className="error-banner"
              role="alert"
              tabIndex={-1}
            >
              {error}
            </div>
          )}

          {externalIdentityMode ? (
            identityConfiguration?.browserSignInEnabled ? (
              <button
                className="button-primary"
                type="button"
                onClick={handleBrowserSingleSignOn}
                disabled={status === 'checking'}
              >
                {status === 'checking' ? 'Redirecting to single sign-on…' : 'Continue with single sign-on'}
              </button>
            ) : (
              <div className="error-banner" role="alert">
                {identityConfiguration?.failureReason ?? 'Single sign-on is not available for this deployment.'}
              </div>
            )
          ) : (
          <form onSubmit={handleSubmit} aria-busy={status === 'checking'}>
            <div className="field">
              <label className="label" htmlFor="portal-username">Email or username</label>
              <input
                id="portal-username"
                className="input"
                type="text"
                name="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                spellCheck={false}
                aria-invalid={Boolean(error)}
                aria-describedby={error ? errorId : undefined}
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="portal-password">Password</label>
              <input
                id="portal-password"
                className="input"
                type="password"
                name="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
                aria-invalid={Boolean(error)}
                aria-describedby={error ? errorId : undefined}
              />
            </div>
            <button className="button-primary" type="submit" disabled={status === 'checking'}>
              {status === 'checking' ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
          )}
          <LegalAttribution />
        </div>
      </div>
    </main>
  )
}
