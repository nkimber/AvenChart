// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthPharmacyChoicePanel from './TelehealthPharmacyChoicePanel.tsx'
import { getTelehealthPharmacyChoices, recordTelehealthPharmacyChoice, type TelehealthPharmacyChoiceDraft, type TelehealthPharmacyChoiceWorkspace } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthPharmacyChoices: vi.fn(), recordTelehealthPharmacyChoice: vi.fn() }
})

const choice: TelehealthPharmacyChoiceDraft = {
  version: 1,
  directoryEntryId: '00000000-0000-4000-8000-000000001001',
  name: 'Atlanta Synthetic Community Pharmacy',
  address: { line1: '100 Synthetic Peachtree Way', line2: null, city: 'Atlanta', state: 'GA', postalCode: '30303', country: 'US' },
  phone: '404-555-0101',
  ncpdpId: null,
  npi: null,
  electronicRoutingCapability: 'NON_PRODUCTION_ONLY',
  directorySource: 'avenchart-synthetic-pharmacy-directory',
  directoryVersion: '2026.08.27.1',
  choiceBasis: 'PatientConfirmedDuringConsultation',
  patientChoiceConfirmed: true,
  selectedAt: '2026-08-27T12:00:00Z',
  prescriptionCreated: false,
  transmitted: false,
}

const workspace: TelehealthPharmacyChoiceWorkspace = {
  consultationId: 'consultation-1',
  consultationStatus: 'MediaEnded',
  adapterMode: 'NON_PRODUCTION',
  datasetId: 'avenchart-synthetic-pharmacy-directory',
  datasetVersion: '2026.08.27.1',
  asOf: '2026-08-27T12:00:00Z',
  searchState: 'GA',
  searchPostalCode: null,
  distanceOrigin: null,
  locationSearchAcknowledged: false,
  chartPreferenceCount: 1,
  pharmacies: [{
    ...choice,
    isChartPreferred: true,
    approximateDistanceMiles: null,
  }],
  currentChoice: null,
  prescriptionEnabled: false,
  transmissionEnabled: false,
  limitations: ['No prescription or transmission is created.'],
}

describe('TelehealthPharmacyChoicePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTelehealthPharmacyChoices).mockResolvedValue(workspace)
    vi.mocked(recordTelehealthPharmacyChoice).mockResolvedValue(choice)
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000009999')
  })

  it('requires explicit patient confirmation and records only the destination draft', async () => {
    render(<TelehealthPharmacyChoicePanel consultationId="consultation-1" patientState="GA" />)

    expect(await screen.findByText('Chart preference')).toBeInTheDocument()
    const recordButton = screen.getByRole('button', { name: 'Record destination draft' })
    expect(recordButton).toBeDisabled()

    fireEvent.click(screen.getByRole('radio', { name: /Atlanta Synthetic Community Pharmacy/ }))
    fireEvent.click(screen.getByRole('checkbox', { name: /patient chose or confirmed this destination/i }))
    expect(recordButton).toBeEnabled()
    fireEvent.click(recordButton)

    await waitFor(() => expect(recordTelehealthPharmacyChoice).toHaveBeenCalledWith(
      'consultation-1', 0, choice.directoryEntryId, '00000000-0000-4000-8000-000000009999',
    ))
    expect(await screen.findByText(/No prescription was created or transmitted/)).toBeInTheDocument()
    expect(screen.getByText(/Prescription created: no. Transmitted: no./)).toBeInTheDocument()
  })

  it('keeps approximate-distance search disabled until origin use is acknowledged', async () => {
    render(<TelehealthPharmacyChoicePanel consultationId="consultation-1" patientState="GA" />)
    await screen.findByText('Chart preference')

    fireEvent.change(screen.getByLabelText('Approximate-distance postal origin'), { target: { value: '30303' } })
    const search = screen.getByRole('button', { name: 'Search neutral choices' })
    expect(search).toBeDisabled()
    fireEvent.click(screen.getByRole('checkbox', { name: /authorized use of this entered postal origin/i }))
    expect(search).toBeEnabled()
    fireEvent.click(search)

    await waitFor(() => expect(getTelehealthPharmacyChoices).toHaveBeenLastCalledWith(
      'consultation-1', expect.objectContaining({ originPostalCode: '30303', locationSearchAcknowledged: true }), undefined,
    ))
  })

  it('moves focus to a recording failure while preserving the retry identity', async () => {
    vi.mocked(recordTelehealthPharmacyChoice).mockRejectedValue(new Error('Reload the current destination.'))
    render(<TelehealthPharmacyChoicePanel consultationId="consultation-1" patientState="GA" />)
    await screen.findByText('Chart preference')
    fireEvent.click(screen.getByRole('radio', { name: /Atlanta Synthetic Community Pharmacy/ }))
    fireEvent.click(screen.getByRole('checkbox', { name: /patient chose or confirmed this destination/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Record destination draft' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Reload the current destination.')
    await waitFor(() => expect(alert).toHaveFocus())
  })
})
