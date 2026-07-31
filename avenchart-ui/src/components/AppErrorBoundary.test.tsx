// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { AppErrorBoundary } from './AppErrorBoundary.tsx'

function BrokenRoute(): never {
  throw new Error('Sensitive implementation diagnostic')
}

describe('AppErrorBoundary', () => {
  it('presents safe recovery without exposing the render diagnostic', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    render(
      <AppErrorBoundary>
        <BrokenRoute />
      </AppErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: 'This page could not be displayed' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry page' })).toBeInTheDocument()
    expect(screen.getByText(/Error reference:/)).toBeInTheDocument()
    expect(screen.queryByText('Sensitive implementation diagnostic')).not.toBeInTheDocument()
  })
})
