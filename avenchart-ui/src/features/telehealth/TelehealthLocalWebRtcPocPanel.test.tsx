// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import TelehealthLocalWebRtcPocPanel from './TelehealthLocalWebRtcPocPanel.tsx'
import type { TelehealthConnectionGrant } from './api.ts'

const grant = {
  sessionId: '00000000-0000-4000-8000-000000000001',
  grantId: '00000000-0000-4000-8000-000000000002',
  joinCredential: 'local-poc-credential',
  expiresAt: '2026-08-31T12:00:00Z',
  mediaTransportEnabled: true,
} as TelehealthConnectionGrant

describe('TelehealthLocalWebRtcPocPanel', () => {
  it('fails closed before any signaling when browser media prerequisites are unavailable', () => {
    const writeSignal = vi.fn()
    const readSignals = vi.fn()

    render(<TelehealthLocalWebRtcPocPanel grant={grant} role="patient" writeSignal={writeSignal} readSignals={readSignals} />)

    expect(screen.getByText(/NON_PRODUCTION local-only demonstration/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'End local media POC' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Join local media POC' }))

    expect(screen.getByRole('status')).toHaveTextContent(/requires a secure browser context/i)
    expect(writeSignal).not.toHaveBeenCalled()
    expect(readSignals).not.toHaveBeenCalled()
  })
})
