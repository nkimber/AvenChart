// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import ClinicianConnectionAbandonControl from './ClinicianConnectionAbandonControl.tsx'
import type { TelehealthReservation } from './api.ts'

const reservation: TelehealthReservation = {
  reservationId: 'reservation-73', requestId: 'request-73', queueEntryId: 'queue-73', shiftId: 'shift-73', clinicianStaffId: 7,
  reservedAt: '2026-08-31T17:00:00Z', leaseExpiresAt: '2026-08-31T17:05:00Z', status: 'Active', requestVersion: 15, applicantOriginated: false,
}

describe('ClinicianConnectionAbandonControl', () => {
  it('requires both explicit pre-consultation synthetic confirmations before forwarding the recovery command', () => {
    const onAbandon = vi.fn()
    render(<ClinicianConnectionAbandonControl reservation={reservation} disabled={false} onAbandon={onAbandon} />)
    const button = screen.getByRole('button', { name: /abandon connection attempt/i })
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/no consultation has been started/i))
    expect(button).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/ends only the pending synthetic connection/i))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    expect(onAbandon).toHaveBeenCalledWith({ noConsultationConfirmed: true, syntheticConnectionAbandonConfirmed: true })
    expect(screen.getByText(/does not record a clinical decision/i)).toBeInTheDocument()
  })

  it('withholds the recovery command while another action is in progress', () => {
    render(<ClinicianConnectionAbandonControl reservation={reservation} disabled onAbandon={vi.fn()} />)
    expect(screen.getByRole('button', { name: /abandon connection attempt/i })).toBeDisabled()
  })
})
