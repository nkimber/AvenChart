// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthSyntheticVisitClosurePanel from './TelehealthSyntheticVisitClosurePanel.tsx'
import { closeSyntheticTelehealthVisit } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, closeSyntheticTelehealthVisit: vi.fn() }
})

describe('TelehealthSyntheticVisitClosurePanel', () => {
  beforeEach(() => vi.clearAllMocks())
  it('reuses the same closure key after an interrupted response', async () => {
    vi.mocked(closeSyntheticTelehealthVisit).mockRejectedValue(new Error('Response interrupted'))
    render(<TelehealthSyntheticVisitClosurePanel consultationId="consultation-1" expectedVersion={5} onClosed={vi.fn()} />)
    screen.getAllByRole('checkbox').forEach(checkbox => fireEvent.click(checkbox))
    const button = screen.getByRole('button', { name: /close synthetic visit and return to availability/i })
    fireEvent.click(button)
    await screen.findByText('Response interrupted')
    const firstKey = vi.mocked(closeSyntheticTelehealthVisit).mock.calls[0][3]
    fireEvent.click(button)
    await waitFor(() => expect(closeSyntheticTelehealthVisit).toHaveBeenCalledTimes(2))
    expect(firstKey).toEqual(expect.any(String))
    expect(vi.mocked(closeSyntheticTelehealthVisit).mock.calls[1][3]).toBe(firstKey)
  })
  it('requires both confirmations before closing only the synthetic lifecycle', async () => {
    const onClosed = vi.fn()
    vi.mocked(closeSyntheticTelehealthVisit).mockResolvedValue({ consultationId: 'consultation-1', consultationVersion: 6, requestVersion: 9, closedAt: '2026-08-30T17:00:00Z', encounterLocked: true, clinicianAvailableForNewWork: true, appointmentCompleted: false, patientDeliveryCreated: false, billingCreated: false, claimCreated: false, externalDestinationContacted: false, limitations: [] })
    render(<TelehealthSyntheticVisitClosurePanel consultationId="consultation-1" expectedVersion={5} onClosed={onClosed} />)
    const button = screen.getByRole('button', { name: /close synthetic visit and return to availability/i })
    expect(button).toBeDisabled()
    screen.getAllByRole('checkbox').forEach((checkbox) => fireEvent.click(checkbox))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    await waitFor(() => expect(closeSyntheticTelehealthVisit).toHaveBeenCalledWith('consultation-1', { expectedConsultationVersion: 5, encounterLockReviewed: true, syntheticClosureConfirmed: true }, undefined, expect.any(String)))
    expect(onClosed).toHaveBeenCalledWith(expect.objectContaining({ consultationId: 'consultation-1', clinicianAvailableForNewWork: true, appointmentCompleted: false }))
    expect(await screen.findByText(/appointment, delivery, billing, claims, and integrations remain unchanged/i)).toBeInTheDocument()
  })

  it('does not claim that the doctor is available when the closure result does not confirm it', async () => {
    const onClosed = vi.fn()
    vi.mocked(closeSyntheticTelehealthVisit).mockResolvedValue({ consultationId: 'consultation-2', consultationVersion: 6, requestVersion: 9, closedAt: '2026-08-30T17:00:00Z', encounterLocked: true, clinicianAvailableForNewWork: false, appointmentCompleted: false, patientDeliveryCreated: false, billingCreated: false, claimCreated: false, externalDestinationContacted: false, limitations: [] })
    render(<TelehealthSyntheticVisitClosurePanel consultationId="consultation-2" expectedVersion={5} onClosed={onClosed} />)
    screen.getAllByRole('checkbox').forEach((checkbox) => fireEvent.click(checkbox))
    fireEvent.click(screen.getByRole('button', { name: /close synthetic visit and return to availability/i }))
    expect(await screen.findByText(/confirm the doctor's current availability before assigning new work/i)).toBeInTheDocument()
    expect(onClosed).toHaveBeenCalledWith(expect.objectContaining({ clinicianAvailableForNewWork: false }))
  })
})
