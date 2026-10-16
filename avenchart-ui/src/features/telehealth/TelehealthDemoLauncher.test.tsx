// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { apiFetch } from '../../api/transport.ts'
import TelehealthDemoLauncher from './TelehealthDemoLauncher.tsx'

vi.mock('../../api/transport.ts', async importOriginal => ({ ...await importOriginal<typeof import('../../api/transport.ts')>(), apiBaseUrl: '', apiFetch: vi.fn() }))
const run = { runId: '10000000-0000-4000-8000-000000000001', expiresAt: new Date(Date.now() + 1800_000).toISOString(), patientInvitation: 'a'.repeat(64), physicianInvitation: 'b'.repeat(64), transport: 'NON_PRODUCTION_LOCAL_WEBRTC_POC' }
const status = { ...run, status: 'Active', patientStatus: 'Invited', physicianStatus: 'Invited' }
beforeEach(() => {
  sessionStorage.clear(); vi.clearAllMocks()
  vi.mocked(apiFetch).mockImplementation(async (url, options) => Response.json(String(url).endsWith('/end') ? { ...status, status: 'Ended' } : options?.method === 'POST' ? run : status))
})
function show() { render(<MemoryRouter><TelehealthDemoLauncher /></MemoryRouter>) }
async function create() {
  fireEvent.click(screen.getByRole('checkbox'))
  fireEvent.click(screen.getByRole('button', { name: 'Create synthetic demo' }))
  await screen.findByRole('link', { name: 'Open patient session' })
}

it('requires synthetic confirmation and provides separate role invitations without clinical credentials', async () => {
  show()
  expect(screen.getByRole('button', { name: 'Create synthetic demo' })).toBeDisabled()
  expect(apiFetch).not.toHaveBeenCalled()
  await create()
  expect(screen.getByRole('link', { name: 'Open patient session' })).toHaveAttribute('href', expect.stringContaining(`/patient#invite=${run.patientInvitation}`))
  expect(screen.getByRole('link', { name: 'Open physician session' })).toHaveAttribute('href', expect.stringContaining(`/physician#invite=${run.physicianInvitation}`))
  const call = vi.mocked(apiFetch).mock.calls.find(([, options]) => options?.method === 'POST')!
  expect(call[0]).toBe('/api/telehealth/v1/demo/runs')
  expect(call[1]?.credentials).toBe('omit')
  expect(JSON.parse(String(call[1]?.body)).syntheticDataConfirmed).toBe(true)
  expect(JSON.stringify(sessionStorage)).not.toContain(run.patientInvitation)
  expect(JSON.stringify(sessionStorage)).not.toContain(run.physicianInvitation)
})

it('retries an ambiguous creation with the same command and observer rather than creating another pair', async () => {
  vi.mocked(apiFetch).mockRejectedValueOnce(new Error('Connection lost'))
  show()
  fireEvent.click(screen.getByRole('checkbox'))
  fireEvent.click(screen.getByRole('button', { name: 'Create synthetic demo' }))
  await screen.findByRole('alert')
  fireEvent.click(screen.getByRole('button', { name: 'Retry preparation' }))
  await screen.findByRole('link', { name: 'Open patient session' })
  const posts = vi.mocked(apiFetch).mock.calls.filter(([url, options]) => String(url).endsWith('/runs') && options?.method === 'POST')
  expect(posts).toHaveLength(2)
  expect(posts[0][1]?.headers).toEqual(posts[1][1]?.headers)
  expect(posts[0][1]?.body).toEqual(posts[1][1]?.body)
})

it('ends only the current demo and lets the presenter create another without a data reset', async () => {
  show(); await create()
  fireEvent.click(screen.getByRole('button', { name: 'End this demo' }))
  await screen.findByRole('button', { name: 'Create another synthetic demo' })
  expect(screen.queryByRole('link', { name: 'Open patient session' })).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Create another synthetic demo' }))
  await waitFor(() => expect(vi.mocked(apiFetch).mock.calls.filter(([url, options]) => String(url).endsWith('/runs') && options?.method === 'POST')).toHaveLength(2))
  expect(vi.mocked(apiFetch).mock.calls.every(([url]) => String(url).includes('/demo/'))).toBe(true)
})
