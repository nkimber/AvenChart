// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthLanding from './TelehealthLanding.tsx'
import { getPracticeContext } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getPracticeContext: vi.fn() }
})

describe('TelehealthLanding', () => {
  beforeEach(() => {
    vi.mocked(getPracticeContext).mockResolvedValue({
      available: true,
      practiceDisplayName: 'AvenChart Synthetic Practice',
      supportedStates: ['GA', 'CA', 'FL'],
      syntheticOnly: true,
      entryMessage: 'Synthetic demonstration only.',
    })
  })

  it('shows scenario-specific credentials in an expandable demo account guide', async () => {
    render(<MemoryRouter><TelehealthLanding /></MemoryRouter>)

    const disclosure = await screen.findByText('Demo accounts and scenarios')
    const details = disclosure.closest('details')
    expect(details).not.toHaveAttribute('open')

    fireEvent.click(disclosure)

    expect(details).toHaveAttribute('open')
    expect(screen.getByRole('heading', { name: 'Physician — two-sided telehealth visit' })).toBeVisible()
    expect(screen.getByText('gold-provider-01')).toBeVisible()
    expect(screen.getByText('pass')).toBeVisible()
    expect(screen.getByRole('heading', { name: 'Patient — two-sided telehealth visit' })).toBeVisible()
    expect(screen.getByText('mod-pat-0012@example.test')).toBeVisible()
    expect(screen.getByText('PortalPass207!')).toBeVisible()
    expect(screen.getByText('No account required.')).toBeVisible()
  })
})
