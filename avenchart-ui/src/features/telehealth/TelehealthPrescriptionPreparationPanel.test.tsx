// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthPrescriptionPreparationPanel from './TelehealthPrescriptionPreparationPanel.tsx'
import {
  getTelehealthPrescriptionPreparationDraft,
  recordTelehealthPrescriptionPreparationDraft,
  type TelehealthPrescriptionPreparationDraft,
  type TelehealthPrescriptionPreparationWorkspace,
} from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return {
    ...original,
    getTelehealthPrescriptionPreparationDraft: vi.fn(),
    recordTelehealthPrescriptionPreparationDraft: vi.fn(),
  }
})

const catalogItem = {
  rxNormCode: '860975',
  drugName: 'Metformin',
  displayName: 'Metformin 500 mg tablet',
  form: 'tablet',
  strength: '500 mg',
  route: 'oral',
}

const workspace: TelehealthPrescriptionPreparationWorkspace = {
  consultationId: 'consultation-1',
  consultationStatus: 'MediaEnded',
  asOf: '2026-08-27T16:00:00Z',
  catalogSource: 'AvenChartSyntheticMedicationVocabulary',
  catalogDatasetId: 'avenchart-gold',
  catalogDatasetVersion: '1',
  adapterMode: 'NON_PRODUCTION',
  canonicalModelVersion: 'AVENCHART_ERX_PREPARATION_V1',
  intendedStandard: 'NCPDP_SCRIPT_2017071',
  currentPharmacyChoiceVersion: 1,
  catalogResults: [],
  currentDraft: null,
  safetyCheckEnabled: false,
  signingEnabled: false,
  prescriptionCreationEnabled: false,
  transmissionEnabled: false,
  patientDeliveryEnabled: false,
  completionEnabled: false,
  limitations: ['This is not a prescription.'],
}

const savedDraft: TelehealthPrescriptionPreparationDraft = {
  version: 1,
  ...catalogItem,
  doseAmount: 500,
  doseUnit: 'mg',
  frequency: 'twice daily',
  quantityValue: 60,
  quantityUnit: 'tablets',
  durationDays: 30,
  refills: 0,
  indication: 'Physician-entered synthetic indication.',
  directions: 'Physician-entered synthetic directions.',
  medicationListReviewed: true,
  allergyListReviewed: true,
  adequateEvaluationCompleted: true,
  pharmacyChoiceVersion: 1,
  recordedAt: '2026-08-27T16:01:00Z',
  legalEffect: false,
  safetyChecked: false,
  signed: false,
  transmissionQueued: false,
  transmitted: false,
  patientDelivered: false,
}

describe('TelehealthPrescriptionPreparationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTelehealthPrescriptionPreparationDraft).mockResolvedValue(workspace)
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000000013')
  })

  it('loads with no drug or dosing default and keeps every consequential capability unavailable', async () => {
    render(<TelehealthPrescriptionPreparationPanel consultationId="consultation-1" />)

    expect(await screen.findByText(/No prescription-preparation draft recorded/i)).toBeInTheDocument()
    expect(screen.getByText((_, element) =>
      element?.tagName === 'P' && element.textContent?.startsWith('No medication selected.') === true,
    )).toBeInTheDocument()
    expect(screen.getByLabelText('Dose amount')).toHaveValue('')
    expect(screen.getByLabelText('Frequency')).toHaveValue('')
    expect(screen.getByText(/interaction\/contraindication checking, signing, canonical prescription creation/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /sign|send|transmit|prescribe/i })).not.toBeInTheDocument()
  })

  it('searches intentionally and selecting catalog facts does not populate dose or directions', async () => {
    vi.mocked(getTelehealthPrescriptionPreparationDraft)
      .mockResolvedValueOnce(workspace)
      .mockResolvedValueOnce({ ...workspace, catalogResults: [catalogItem] })
    render(<TelehealthPrescriptionPreparationPanel consultationId="consultation-1" />)
    await screen.findByText(/No prescription-preparation draft recorded/i)

    fireEvent.change(screen.getByLabelText('Search the synthetic medication catalog'), { target: { value: 'metformin' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search catalog' }))
    fireEvent.click(await screen.findByLabelText(/Metformin 500 mg tablet/i))

    expect(screen.getByText((_, element) =>
      element?.tagName === 'P'
        && element.textContent?.includes('Selected catalog fact: Metformin 500 mg tablet') === true,
    )).toBeInTheDocument()
    expect(screen.getByLabelText('Dose amount')).toHaveValue('')
    expect(screen.getByLabelText('Physician-entered directions')).toHaveValue('')
  })

  it('retains the exact semantic command through an ambiguous failure and explicit retry', async () => {
    vi.mocked(getTelehealthPrescriptionPreparationDraft).mockResolvedValue({ ...workspace, catalogResults: [catalogItem] })
    vi.mocked(recordTelehealthPrescriptionPreparationDraft)
      .mockRejectedValueOnce(new Error('Synthetic write outcome unknown.'))
      .mockResolvedValueOnce(savedDraft)
    render(<TelehealthPrescriptionPreparationPanel consultationId="consultation-1" />)
    await screen.findByText(/No prescription-preparation draft recorded/i)

    fireEvent.click(screen.getByLabelText(/Metformin 500 mg tablet/i))
    fill('Dose amount', '500')
    fill('Dose unit', 'mg')
    fill('Frequency', 'twice daily')
    fill('Quantity', '60')
    fill('Quantity unit', 'tablets')
    fill('Duration in days', '30')
    fill('Refills (0–5)', '0')
    fill('Physician-entered indication', 'Physician-entered synthetic indication.')
    fill('Physician-entered directions', 'Physician-entered synthetic directions.')
    for (const name of [
      /reviewed the current medication information/i,
      /reviewed the current allergy information/i,
      /available evaluation was adequate/i,
      /synthetic demonstration data only/i,
    ]) fireEvent.click(screen.getByRole('checkbox', { name }))

    fireEvent.click(screen.getByRole('button', { name: 'Record preparation draft' }))
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveFocus()
    expect(screen.getByLabelText('Physician-entered directions')).toHaveValue('Physician-entered synthetic directions.')

    fireEvent.click(screen.getByRole('button', { name: 'Record preparation draft' }))
    await waitFor(() => expect(recordTelehealthPrescriptionPreparationDraft).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordTelehealthPrescriptionPreparationDraft).mock.calls[0][2]).toBe(
      vi.mocked(recordTelehealthPrescriptionPreparationDraft).mock.calls[1][2],
    )
    expect(await screen.findByText(/version 1 recorded.*not safety checked, signed/i)).toBeInTheDocument()
  })
})

function fill(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } })
}
