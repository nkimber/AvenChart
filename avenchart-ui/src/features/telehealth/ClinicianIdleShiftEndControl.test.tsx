// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import ClinicianIdleShiftEndControl from './ClinicianIdleShiftEndControl.tsx'
import type { TelehealthShift } from './api.ts'

const shift: TelehealthShift = { shiftId: 'shift-63', status: 'Active', facilityId: 10, clinicianStaffId: 7, startedAt: '2026-08-30T17:00:00Z', version: 2, endedAt: null }

describe('ClinicianIdleShiftEndControl', () => {
  it('requires both explicit confirmations and forwards only the confirmed command', () => {
    const onEnd = vi.fn()
    render(<ClinicianIdleShiftEndControl shift={shift} reservationActive={false} consultationActive={false} working={false} onEnd={onEnd} />)
    const button = screen.getByRole('button', { name: /end idle telehealth shift/i })
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/no active telehealth reservation/i))
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/ends only my synthetic shift/i))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    expect(onEnd).toHaveBeenCalledWith({ noActiveWorkConfirmed: true, syntheticEndConfirmed: true })
    expect(screen.getByText(/does not change patient, appointment, encounter, clinical, billing, claim, media, integration, or external state/i)).toBeInTheDocument()
  })

  it('withholds the action when the physician holds a reservation or consultation', () => {
    const { rerender } = render(<ClinicianIdleShiftEndControl shift={shift} reservationActive consultationActive={false} working={false} onEnd={vi.fn()} />)
    expect(screen.queryByRole('button', { name: /end idle telehealth shift/i })).not.toBeInTheDocument()
    rerender(<ClinicianIdleShiftEndControl shift={shift} reservationActive={false} consultationActive working={false} onEnd={vi.fn()} />)
    expect(screen.queryByRole('button', { name: /end idle telehealth shift/i })).not.toBeInTheDocument()
  })
})
