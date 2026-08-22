// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { AuthSessionResponse, PatientPortalSessionResponse } from '../api.ts'
import {
  apiBaseUrl,
  apiFetch,
  requireSuccessfulResponse,
} from '../api/transport.ts'

export type BrowserOidcAudience = 'staff' | 'portal'

export type BrowserOidcConfiguration = {
  mode: 'local' | 'oidc' | 'test-oidc'
  browserSignInEnabled: boolean
  failureReason?: string | null
  audiences: BrowserOidcAudience[]
  startPath: string
  callbackPath: string
  clientId: string
  scopes: string
}

export type BrowserOidcStaffSession = {
  session: AuthSessionResponse
  csrfToken: string
}

export type BrowserOidcPortalSession = {
  session: PatientPortalSessionResponse
  csrfToken: string
}

export async function getBrowserOidcConfiguration(
  signal?: AbortSignal,
): Promise<BrowserOidcConfiguration> {
  const response = await apiFetch(`${apiBaseUrl}/api/auth/oidc/browser-configuration`, {
    signal,
  })
  await requireSuccessfulResponse(response, 'Browser single sign-on configuration')
  return response.json()
}

export function startBrowserOidcSignIn(
  configuration: BrowserOidcConfiguration,
  audience: BrowserOidcAudience,
) {
  if (!configuration.browserSignInEnabled || !configuration.audiences.includes(audience)) {
    throw new Error(configuration.failureReason ?? 'Browser single sign-on is not available.')
  }
  const returnUrl = new URL('/auth/oidc/callback', window.location.origin)
  returnUrl.searchParams.set('audience', audience)
  const startUrl = new URL(configuration.startPath, apiBaseUrl)
  startUrl.searchParams.set('audience', audience)
  startUrl.searchParams.set('returnUrl', returnUrl.toString())
  window.location.assign(startUrl.toString())
}

export async function getBrowserOidcStaffSession(
  signal?: AbortSignal,
): Promise<BrowserOidcStaffSession> {
  const response = await apiFetch(`${apiBaseUrl}/api/auth/session`, {
    headers: { 'X-AvenChart-Session': 'browser-oidc' },
    signal,
  })
  await requireSuccessfulResponse(response, 'Browser single sign-on session', 'clinician')
  const session = (await response.json()) as AuthSessionResponse
  const csrfToken = response.headers.get('X-AvenChart-CSRF')
  if (!session.authenticated || !session.sessionId || !csrfToken) {
    throw new Error(session.failureReason ?? 'Browser single sign-on session could not be established.')
  }
  return { session, csrfToken }
}

export async function getBrowserOidcPortalSession(
  signal?: AbortSignal,
): Promise<BrowserOidcPortalSession> {
  const response = await apiFetch(`${apiBaseUrl}/api/patient-portal/session`, {
    headers: { 'X-AvenChart-Patient-Portal-Session': 'browser-oidc' },
    signal,
  })
  await requireSuccessfulResponse(response, 'Browser single sign-on portal session', 'portal')
  const session = (await response.json()) as PatientPortalSessionResponse
  const csrfToken = response.headers.get('X-AvenChart-CSRF')
  if (!session.authenticated || !session.sessionId || !csrfToken) {
    throw new Error(session.failureReason ?? 'Browser single sign-on portal session could not be established.')
  }
  return { session, csrfToken }
}
