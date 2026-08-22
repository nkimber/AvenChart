// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  getBrowserOidcConfiguration,
  getBrowserOidcPortalSession,
  getBrowserOidcStaffSession,
} from './browserOidc.ts'

describe('browser OIDC BFF client', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('reads the credential-free browser sign-in configuration', async () => {
    const request = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      mode: 'oidc',
      browserSignInEnabled: true,
      audiences: ['staff', 'portal'],
      startPath: '/api/auth/oidc/start',
      callbackPath: '/api/auth/oidc/callback',
      clientId: 'public-browser-client',
      scopes: 'openid profile',
    }), { status: 200, headers: { 'content-type': 'application/json' } }))
    vi.stubGlobal('fetch', request)

    const configuration = await getBrowserOidcConfiguration()

    expect(configuration.browserSignInEnabled).toBe(true)
    expect(configuration.clientId).toBe('public-browser-client')
    const [url, init] = request.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('http://localhost:5001/api/auth/oidc/browser-configuration')
    expect(init.credentials).toBe('include')
  })

  it('requires a server-issued CSRF proof before accepting a staff BFF session', async () => {
    const request = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      authenticated: true,
      sessionId: '10000000-0000-0000-0000-000000000001',
      username: 'admin',
      displayName: 'Administrator',
      role: 'administrator',
    }), { status: 200, headers: { 'content-type': 'application/json', 'X-AvenChart-CSRF': 'staff-csrf' } }))
    vi.stubGlobal('fetch', request)

    const result = await getBrowserOidcStaffSession()

    expect(result.csrfToken).toBe('staff-csrf')
    expect(result.session.username).toBe('admin')
    const [, init] = request.mock.calls[0] as [string, RequestInit]
    expect(new Headers(init.headers).get('X-AvenChart-Session')).toBe('browser-oidc')
  })

  it('fails closed when a portal BFF session has no CSRF proof', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      authenticated: true,
      sessionId: '10000000-0000-0000-0000-000000000002',
      username: 'portal-user',
      portalUsername: 'portal-user',
      displayName: 'Portal User',
    }), { status: 200, headers: { 'content-type': 'application/json' } })))

    await expect(getBrowserOidcPortalSession()).rejects.toThrow(
      'Browser single sign-on portal session could not be established.',
    )
  })
})
