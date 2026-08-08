// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import LegalAttribution from './LegalAttribution.tsx'

describe('LegalAttribution', () => {
  it('links the AvenChart license and source plus the original Legacy EHR project and community', () => {
    render(<LegalAttribution />)

    expect(screen.getByRole('link', { name: 'Software license' })).toHaveAttribute(
      'href',
      '/LICENSE.txt',
    )
    expect(screen.getByRole('link', { name: 'AvenChart source' })).toHaveAttribute(
      'href',
      'https://github.com/nkimber/AvenChart',
    )
    expect(screen.getByRole('link', { name: 'Original Legacy EHR project' })).toHaveAttribute(
      'href',
      'https://www.open-emr.org/',
    )
    expect(screen.getByRole('link', { name: 'Original source code' })).toHaveAttribute(
      'href',
      'https://github.com/legacy-ehr/legacy-ehr',
    )
    expect(screen.getByRole('link', { name: 'Legacy EHR community' })).toHaveAttribute(
      'href',
      'https://community.open-emr.org/',
    )
    expect(screen.getByText(/GNU GPL v3 or later/)).toBeInTheDocument()
    expect(screen.getByText(/gratefully thank/i)).toBeInTheDocument()
  })
})
