// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { renderHook } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { useActiveCallGrant } from './useActiveCallGrant.ts'
import type { TelehealthConnectionGrant } from './api.ts'

it('serializes concurrent reconnect requests and renews after the active grant expires', async () => {
  const waiting = { requestId: 'request', requestStatus: 'Connecting', expiresAt: new Date().toISOString() } as TelehealthConnectionGrant
  const fresh = { ...waiting, requestStatus: 'InConsultation', expiresAt: new Date(Date.now() + 300_000).toISOString() } as TelehealthConnectionGrant
  const prepare = vi.fn().mockResolvedValue(fresh)
  const evidence = { browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true, networkQuality: 'good' as const, syntheticDataConfirmed: true as const }
  const { result } = renderHook(() => useActiveCallGrant(waiting, true, evidence, prepare))
  const [one, two] = await Promise.all([result.current(), result.current()])
  expect(one).toBe(fresh); expect(two).toBe(fresh)
  await result.current()
  expect(prepare).toHaveBeenCalledTimes(1)
  const now = vi.spyOn(Date, 'now').mockReturnValue(Date.now() + 300_000)
  await result.current()
  expect(prepare).toHaveBeenCalledTimes(2)
  now.mockRestore()
})
