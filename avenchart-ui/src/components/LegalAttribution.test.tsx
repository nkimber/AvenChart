// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import LegalAttribution from './LegalAttribution.tsx'

describe('LegalAttribution', () => {
  it('links the modernized license and the original Legacy EHR project', () => {
    render(<LegalAttribution />)

    expect(screen.getByRole('link', { name: 'Software license' })).toHaveAttribute(
      'href',
      '/LICENSE.txt',
    )
    expect(screen.getByRole('link', { name: 'Modernized source' })).toHaveAttribute(
      'href',
      'https://github.com/nkimber/Legacy EHR-Legacy',
    )
    expect(screen.getByRole('link', { name: 'Original Legacy EHR project' })).toHaveAttribute(
      'href',
      'https://www.open-emr.org/',
    )
    expect(screen.getByRole('link', { name: 'Original source code' })).toHaveAttribute(
      'href',
      'https://github.com/legacy-ehr/legacy-ehr',
    )
    expect(screen.getByText(/GNU GPL v3 or later/)).toBeInTheDocument()
  })
})
