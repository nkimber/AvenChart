// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  getBrowserOidcPortalSession,
  getBrowserOidcStaffSession,
  type BrowserOidcAudience,
} from '../auth/browserOidc.ts'
import { saveClinicianSession, savePortalSession } from '../auth/session.ts'
import LegalAttribution from '../components/LegalAttribution.tsx'

type CallbackState =
  | { status: 'working' }
  | { status: 'failed'; reason: string }

function isAudience(value: string | null): value is BrowserOidcAudience {
  return value === 'staff' || value === 'portal'
}

export default function OidcCallback() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [state, setState] = useState<CallbackState>({ status: 'working' })
  const errorReference = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (state.status === 'failed') errorReference.current?.focus()
  }, [state])

  useEffect(() => {
    const controller = new AbortController()
    const audience = searchParams.get('audience')
    const completion = searchParams.get('oidc')
    const providerReason = searchParams.get('reason')
    if (completion !== 'complete' || !isAudience(audience)) {
      setState({
        status: 'failed',
        reason: providerReason ?? 'Single sign-on did not complete. Return to sign-in and try again.',
      })
      return () => controller.abort()
    }

    async function completeSignIn() {
      try {
        if (audience === 'staff') {
          const { session, csrfToken } = await getBrowserOidcStaffSession(controller.signal)
          const accessContext = session.accessContext
          if (!accessContext) {
            throw new Error('The single sign-on session did not include an AvenChart access context.')
          }
          saveClinicianSession({
            sessionId: session.sessionId!,
            username: session.username,
            displayName: session.displayName,
            role: session.role,
            staffId: session.staffId,
            facilityId: accessContext.defaultFacilityId ?? null,
            purposeOfUse: accessContext.defaultPurposeOfUse || 'treatment',
            facilities: accessContext.facilities ?? [],
            purposes: accessContext.purposes ?? ['treatment'],
            authenticationMode: 'oidc-bff',
            csrfToken,
          })
          navigate('/clinician/dashboard', { replace: true })
          return
        }

        const { session, csrfToken } = await getBrowserOidcPortalSession(controller.signal)
        savePortalSession({
          sessionId: session.sessionId!,
          username: session.username,
          portalUsername: session.portalUsername,
          displayName: session.displayName,
          authenticationMode: 'oidc-bff',
          csrfToken,
        })
        navigate('/portal/home', { replace: true })
      } catch (error) {
        if (controller.signal.aborted) return
        setState({
          status: 'failed',
          reason: error instanceof Error ? error.message : 'Single sign-on could not establish an AvenChart session.',
        })
      }
    }

    void completeSignIn()
    return () => controller.abort()
  }, [navigate, searchParams])

  return (
    <main id="main-content" className="auth-shell" tabIndex={-1}>
      <div className="auth-panel">
        <div className="auth-card">
          <p className="eyebrow">Secure sign-in</p>
          <h1 className="title">Completing single sign-on</h1>
          {state.status === 'working' ? (
            <p className="subtitle" aria-live="polite" aria-busy="true">
              Validating your identity and establishing a secure AvenChart session…
            </p>
          ) : (
            <>
              <div ref={errorReference} className="error-banner" role="alert" tabIndex={-1}>
                {state.reason}
              </div>
              <p className="subtitle">
                <a href={searchParams.get('audience') === 'portal' ? '/portal/login' : '/login'}>
                  Return to sign-in
                </a>
              </p>
            </>
          )}
          <LegalAttribution />
        </div>
      </div>
    </main>
  )
}
