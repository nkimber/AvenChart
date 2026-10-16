// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { ApiRequestError, apiFetch } from '../../api/transport.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import TelehealthDemoRoom from './TelehealthDemoRoom.tsx'

vi.mock('../../api/transport.ts', async importOriginal => ({ ...await importOriginal<typeof import('../../api/transport.ts')>(), apiBaseUrl: '', apiFetch: vi.fn() }))
vi.mock('./devicePreflight.ts', () => ({ runTelehealthDevicePreflight: vi.fn() }))
vi.mock('./TelehealthLocalWebRtcPocPanel.tsx', () => ({ default: () => <p>Local call controls</p> }))
const id = '10000000-0000-4000-8000-000000000001'
const member = { participantId: '20000000-0000-4000-8000-000000000001', runId: id, role: 'patient', expiresAt: new Date(Date.now() + 1800_000).toISOString(), transport: 'NON_PRODUCTION_LOCAL_WEBRTC_POC' }
const status = { runId: id, expiresAt: member.expiresAt, transport: member.transport, status: 'Active', patientStatus: 'Opened', physicianStatus: 'Invited' }
beforeEach(() => {
  sessionStorage.clear(); vi.clearAllMocks()
  window.history.replaceState(null, '', `/telehealth/demo/${id}/patient#invite=${'c'.repeat(64)}`)
  vi.mocked(apiFetch).mockImplementation(async url => Response.json(String(url).endsWith('/redeem') ? member : status))
})
function show() { render(<MemoryRouter initialEntries={[`/telehealth/demo/${id}/patient`]}><Routes><Route path="/telehealth/demo/:runId/:role" element={<TelehealthDemoRoom />} /></Routes></MemoryRouter>) }

it('consumes the invitation fragment after activation and waits for explicit media permission', async () => {
  show()
  await screen.findByRole('button', { name: 'Check devices and enter room' })
  expect(window.location.hash).toBe('')
  expect(JSON.stringify(sessionStorage)).not.toContain('c'.repeat(64))
  expect(JSON.stringify(sessionStorage)).not.toContain('accessToken')
  expect(runTelehealthDevicePreflight).not.toHaveBeenCalled()
  expect(screen.queryByText('Local call controls')).not.toBeInTheDocument()
})

it('preserves the scoped session while recovering from denied device permission', async () => {
  vi.mocked(runTelehealthDevicePreflight).mockResolvedValueOnce({ status: 'failed', message: 'Allow the camera and try again.' })
    .mockResolvedValueOnce({ status: 'passed', evidence: { browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true, networkQuality: 'good', syntheticDataConfirmed: true } })
  show()
  fireEvent.click(await screen.findByRole('button', { name: 'Check devices and enter room' }))
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Allow the camera'))
  const statusReads = () => vi.mocked(apiFetch).mock.calls.filter(([url, options]) => String(url).endsWith(`/runs/${id}`) && options?.method === 'GET').length
  const beforePoll = statusReads()
  await waitFor(() => expect(statusReads()).toBeGreaterThan(beforePoll), { timeout: 7000 })
  expect(screen.getByRole('alert')).toHaveTextContent('Allow the camera')
  fireEvent.click(screen.getByRole('button', { name: 'Check devices and enter room' }))
  await screen.findByText('Local call controls')
  expect(vi.mocked(apiFetch).mock.calls.filter(([url]) => String(url).endsWith('/redeem'))).toHaveLength(1)
}, 10_000)

it('removes media controls when the server revokes the room session', async () => {
  vi.mocked(runTelehealthDevicePreflight).mockResolvedValue({ status: 'passed', evidence: { browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true, networkQuality: 'good', syntheticDataConfirmed: true } })
  show()
  fireEvent.click(await screen.findByRole('button', { name: 'Check devices and enter room' }))
  await screen.findByText('Local call controls')
  vi.mocked(apiFetch).mockRejectedValue(new ApiRequestError('Room no longer available', 404))
  await screen.findByText(/This synthetic demo is no longer available/, {}, { timeout: 6000 })
  expect(screen.queryByText('Local call controls')).not.toBeInTheDocument()
})

it('keeps the authoritative ended state when an in-flight heartbeat is rejected', async () => {
  let rejectHeartbeat: (error: Error) => void = () => undefined
  vi.mocked(apiFetch).mockImplementation(async url => {
    if (String(url).endsWith('/redeem')) return Response.json(member)
    if (String(url).endsWith('/heartbeat')) return new Promise<Response>((_, reject) => { rejectHeartbeat = reject })
    return Response.json(String(url).endsWith('/end') ? { ...status, status: 'Ended' } : status)
  })
  show()
  await screen.findByRole('button', { name: 'Check devices and enter room' })
  await waitFor(() => expect(vi.mocked(apiFetch).mock.calls.some(([url]) => String(url).endsWith('/heartbeat'))).toBe(true))
  fireEvent.click(screen.getByRole('button', { name: 'End this demo' }))
  await screen.findByText(/This synthetic demo has ended/)
  await act(async () => { rejectHeartbeat(new ApiRequestError('Room no longer active', 404)) })
  expect(screen.getByText(/This synthetic demo has ended/)).toBeInTheDocument()
})
