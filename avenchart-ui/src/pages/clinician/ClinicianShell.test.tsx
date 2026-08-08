// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getCurrentSession,
  getOperationalReports,
  getProcedureReportQueue,
  logout,
} from '../../api.ts'
import { loadClinicianSession, saveClinicianSession } from '../../auth/session.ts'
import ClinicianShell from './ClinicianShell.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getCurrentSession: vi.fn(),
    getOperationalReports: vi.fn(),
    getProcedureReportQueue: vi.fn(),
    logout: vi.fn(),
  }
})

function renderShell(initialEntry = '/clinician/dashboard') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/login" element={<p>Clinician login</p>} />
        <Route path="/clinician" element={<ClinicianShell />}>
          <Route path="dashboard" element={<p>Dashboard content</p>} />
          <Route path="reports" element={<p>Reports content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('ClinicianShell', () => {
  beforeEach(() => {
    window.sessionStorage.clear()
    saveClinicianSession({
      sessionId: 'staff-session',
      username: 'admin',
      displayName: 'Avery Clinician',
      role: 'physician',
    })
    vi.mocked(getCurrentSession).mockResolvedValue({
      authenticated: true,
      sessionId: 'staff-session',
      username: 'admin',
      displayName: 'Avery Clinician',
      role: 'physician',
      sessionSource: 'test',
    })
    vi.mocked(getProcedureReportQueue).mockResolvedValue({
      datasetId: 'test',
      datasetVersion: 'v1',
      statusFilter: 'unreviewed',
      limit: 100,
      totalReports: 0,
      reviewedReports: 0,
      unreviewedReports: 0,
      reports: [],
    })
    vi.mocked(getOperationalReports).mockResolvedValue({
      asOfDate: '2026-07-27',
      currentYear: 2026,
      counts: {
        patients: 0,
        portalPatients: 0,
        providers: 0,
        facilities: 0,
        appointments: 0,
        futureAppointments: 0,
        currentYearAppointments: 0,
        encounters: 0,
        currentYearEncounters: 0,
        billingLines: 0,
        billingTotal: 0,
        labReports: 0,
        patientDocuments: 0,
        messages: 0,
        newMessages: 0,
        doneMessages: 0,
      },
      providerActivity: [],
      facilityActivity: [],
      clinicalConditions: [],
    })
    vi.mocked(logout).mockResolvedValue({
      authenticated: false,
      username: 'admin',
      displayName: 'Avery Clinician',
      role: 'physician',
      sessionSource: 'test',
    })
  })

  it('exposes every clinician destination in an accessible mobile drawer', async () => {
    const user = userEvent.setup()
    renderShell()
    await screen.findByText('Dashboard content')
    const trigger = screen.getByRole('button', { name: 'Open navigation' })

    await user.click(trigger)

    const drawer = screen.getByRole('dialog', { name: 'Main navigation' })
    expect(within(drawer).getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Patients' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Admin' })).toBeInTheDocument()
    expect(within(drawer).getByRole('button', { name: 'Sign out' })).toBeInTheDocument()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Main navigation' })).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  it('keeps a selected lower navigation item visible with following context', async () => {
    const user = userEvent.setup()
    renderShell()
    await screen.findByText('Dashboard content')
    const navigation = screen.getByRole('navigation', { name: 'Main navigation' })
    const links = within(navigation).getAllByRole('link')

    Object.defineProperties(navigation, {
      clientHeight: { configurable: true, value: 240 },
      scrollHeight: { configurable: true, value: links.length * 40 },
      scrollTop: { configurable: true, value: 850, writable: true },
    })
    links.forEach((link, index) => {
      Object.defineProperties(link, {
        offsetHeight: { configurable: true, value: 40 },
        offsetTop: { configurable: true, value: index * 40 },
      })
    })

    await user.click(within(navigation).getByRole('link', { name: 'Reports' }))

    await screen.findByText('Reports content')
    expect(navigation.scrollTop).toBe(1008)
    expect(
      window.sessionStorage.getItem(
        'modern-ui:clinician-navigation-scroll:v1:desktop',
      ),
    ).toBe('1008')

    const reports = within(navigation).getByRole('link', { name: 'Reports' })
    const admin = within(navigation).getByRole('link', { name: 'Admin' })
    expect(reports.offsetTop - navigation.scrollTop).toBe(32)
    expect(admin.offsetTop + admin.offsetHeight - navigation.scrollTop).toBe(232)
  })

  it('blocks entry and offers retry when session validation is unavailable', async () => {
    vi.mocked(getCurrentSession).mockRejectedValueOnce(new TypeError('Network unavailable'))

    renderShell()

    expect(await screen.findByRole('heading', { name: 'Unable to verify your session' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
    expect(screen.queryByText('Dashboard content')).not.toBeInTheDocument()
  })

  it('ends the server session before clearing local authentication', async () => {
    const user = userEvent.setup()
    renderShell()
    await screen.findByText('Dashboard content')

    await user.click(screen.getAllByRole('button', { name: 'Sign out' })[0])

    await waitFor(() => expect(logout).toHaveBeenCalledWith('staff-session', expect.any(AbortSignal)))
    expect(await screen.findByText('Clinician login')).toBeInTheDocument()
    expect(loadClinicianSession()).toBeNull()
  })
})
