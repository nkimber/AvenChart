// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Transport tests use a fixed configured origin; browser evidence exercises
// the application's IPv4 loopback default independently of host resolver
// preferences.
Object.assign(import.meta.env, {
  VITE_API_BASE_URL: 'http://localhost:5001',
})

afterEach(() => {
  cleanup()
  sessionStorage.clear()
})
