// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  endPatientPortalSession,
  getPatientPortalHome,
  getPatientPortalSession,
} from '../../api.ts'
import { loadPortalSession, savePortalSession } from '../../auth/session.ts'
import PortalShell from './PortalShell.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    endPatientPortalSession: vi.fn(),
    getPatientPortalHome: vi.fn(),
    getPatientPortalSession: vi.fn(),
  }
})

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/portal/home']}>
      <Routes>
        <Route path="/portal/login" element={<p>Portal login</p>} />
        <Route path="/portal" element={<PortalShell />}>
          <Route path="home" element={<p>Portal home content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('PortalShell', () => {
  beforeEach(() => {
    savePortalSession({
      sessionId: 'portal-session',
      username: 'patient',
      portalUsername: 'patient.portal',
      displayName: 'Morgan Patient',
    })
    vi.mocked(getPatientPortalSession).mockResolvedValue({
      authenticated: true,
      sessionId: 'portal-session',
      username: 'patient',
      portalUsername: 'patient.portal',
      canonicalId: 'MOD-PAT-0004',
      pubpid: 'P0004',
      displayName: 'Morgan Patient',
      sessionSource: 'test',
    })
    vi.mocked(getPatientPortalHome).mockResolvedValue({
      authenticated: true,
      displayName: 'Morgan Patient',
      sessionSource: 'test',
      messages: {
        totalMessages: 0,
        newMessages: 0,
        doneMessages: 0,
      },
      upcomingAppointmentCount: 0,
      upcomingAppointments: [],
    })
    vi.mocked(endPatientPortalSession).mockResolvedValue({
      authenticated: false,
      username: 'patient',
      portalUsername: 'patient.portal',
      canonicalId: 'MOD-PAT-0004',
      pubpid: 'P0004',
      displayName: 'Morgan Patient',
      sessionSource: 'test',
    })
  })

  it('ends the server session and clears local portal authentication', async () => {
    const user = userEvent.setup()
    renderShell()
    await screen.findByText('Portal home content')

    await user.click(screen.getByRole('button', { name: 'Sign out' }))

    await waitFor(() => {
      expect(endPatientPortalSession).toHaveBeenCalledWith(
        'portal-session',
        expect.any(AbortSignal),
      )
    })
    expect(await screen.findByText('Portal login')).toBeInTheDocument()
    expect(loadPortalSession()).toBeNull()
  })

  it('shows a retryable error instead of rendering an unverified portal', async () => {
    vi.mocked(getPatientPortalSession).mockRejectedValueOnce(new TypeError('Network unavailable'))

    renderShell()

    expect(await screen.findByRole('alert')).toHaveTextContent('Network unavailable')
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
    expect(screen.queryByText('Portal home content')).not.toBeInTheDocument()
  })
})
