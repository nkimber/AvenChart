// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthConversationPanel from './TelehealthConversationPanel.tsx'
import { addPatientTelehealthConversationMessage, getPatientTelehealthConversation, type TelehealthConversation } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getPatientTelehealthConversation: vi.fn(), addPatientTelehealthConversationMessage: vi.fn() }
})

const conversation: TelehealthConversation = {
  consultationId: '00000000-0000-4000-8000-000000000001',
  requestId: '00000000-0000-4000-8000-000000000002',
  consultationStatus: 'InConsultation',
  canSend: true,
  realtimeDeliveryEnabled: false,
  messages: [],
  limitations: ['No external communication occurred.'],
}

describe('TelehealthConversationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getPatientTelehealthConversation).mockResolvedValue(conversation)
    vi.mocked(addPatientTelehealthConversationMessage).mockResolvedValue({
      ...conversation,
      messages: [{ messageId: '00000000-0000-4000-8000-000000000003', senderRole: 'patient', body: 'Synthetic hello', sentAt: '2026-08-30T12:00:00Z', legalEffect: false }],
    })
  })

  it('requires an explicit synthetic-data confirmation before adding a message', async () => {
    render(<TelehealthConversationPanel participant="patient" requestId={conversation.requestId} />)
    expect(await screen.findByText('No synthetic messages yet.')).toBeInTheDocument()
    const add = screen.getByRole('button', { name: 'Add synthetic message' })
    fireEvent.change(screen.getByLabelText('Demonstration message'), { target: { value: 'Synthetic hello' } })
    expect(add).toBeDisabled()
    fireEvent.click(screen.getByRole('checkbox', { name: /contains synthetic demonstration data only/i }))
    expect(add).toBeEnabled()
    fireEvent.click(add)

    await waitFor(() => expect(addPatientTelehealthConversationMessage).toHaveBeenCalledWith(conversation.requestId, 'Synthetic hello'))
    expect(await screen.findByText('Synthetic hello')).toBeInTheDocument()
    expect(screen.getAllByText(/No external communication occurred/).length).toBeGreaterThan(0)
  })
})
