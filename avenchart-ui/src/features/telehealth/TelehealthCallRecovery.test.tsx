// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, expect, it, vi } from 'vitest'
import TelehealthCallRecovery from './TelehealthCallRecovery.tsx'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import type { TelehealthConnectionGrant } from './api.ts'

vi.mock('./devicePreflight.ts', () => ({ runTelehealthDevicePreflight: vi.fn() }))
const evidence = { browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true, networkQuality: 'good' as const, syntheticDataConfirmed: true as const }
beforeEach(() => { vi.clearAllMocks(); vi.mocked(runTelehealthDevicePreflight).mockResolvedValue({ status: 'passed', evidence }) })

it('retains the command identity after an ambiguous failure and returns only a fresh grant', async () => {
  const grant = { grantId: 'new-grant', requestId: 'same-request' } as TelehealthConnectionGrant
  const prepare = vi.fn().mockRejectedValueOnce(new Error('Connection unavailable')).mockResolvedValueOnce(grant)
  const ready = vi.fn()
  render(<TelehealthCallRecovery prepare={prepare} onReady={ready} />)
  fireEvent.click(screen.getByRole('button', { name: 'Prepare active-call reconnection' }))
  await screen.findByRole('alert')
  expect(ready).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Prepare active-call reconnection' }))
  await waitFor(() => expect(ready).toHaveBeenCalledWith(grant, evidence))
  expect(prepare.mock.calls[0][1]).toBe(prepare.mock.calls[1][1])
  expect(JSON.stringify(sessionStorage)).not.toContain('new-grant')
})

it('does not request authorization after device permission is denied', async () => {
  vi.mocked(runTelehealthDevicePreflight).mockResolvedValue({ status: 'failed', message: 'Allow your camera' })
  const prepare = vi.fn()
  render(<TelehealthCallRecovery prepare={prepare} onReady={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Prepare active-call reconnection' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Allow your camera')
  expect(prepare).not.toHaveBeenCalled()
})
