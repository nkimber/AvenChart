// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import ClinicianReservationReleaseControl from './ClinicianReservationReleaseControl.tsx'
import type { TelehealthReservation } from './api.ts'

const reservation: TelehealthReservation = {
  reservationId: 'reservation-52', requestId: 'request-52', queueEntryId: 'queue-52', shiftId: 'shift-52', clinicianStaffId: 7,
  reservedAt: '2026-08-30T17:00:00Z', leaseExpiresAt: '2026-08-30T17:05:00Z', status: 'Active', requestVersion: 14, applicantOriginated: false,
}

describe('ClinicianReservationReleaseControl', () => {
  it('requires both explicit pre-connection synthetic confirmations before forwarding the release command', () => {
    const onRelease = vi.fn()
    render(<ClinicianReservationReleaseControl reservation={reservation} disabled={false} onRelease={onRelease} />)
    const button = screen.getByRole('button', { name: /release reservation to queue/i })
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/no connection room or consultation/i))
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/only releases the synthetic reservation/i))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    expect(onRelease).toHaveBeenCalledWith({ noConnectionOrConsultationConfirmed: true, syntheticReleaseConfirmed: true })
    expect(screen.getByText(/does not record a clinical decision/i)).toBeInTheDocument()
  })

  it('withholds the command while another action is in progress', () => {
    render(<ClinicianReservationReleaseControl reservation={reservation} disabled onRelease={vi.fn()} />)
    expect(screen.getByRole('button', { name: /release reservation to queue/i })).toBeDisabled()
  })
})
