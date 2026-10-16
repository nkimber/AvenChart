// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useRef } from 'react'
import type { TelehealthConnectionGrant, TelehealthDevicePreflight } from './api.ts'

// Serialize renewal: concurrent ICE writes must not revoke each other's new
// grants. Only the scoped application grant is retained in component memory.
export function useActiveCallGrant(
  grant: TelehealthConnectionGrant | null,
  active: boolean,
  evidence: TelehealthDevicePreflight | null,
  prepare: (evidence: TelehealthDevicePreflight, key: string) => Promise<TelehealthConnectionGrant>,
) {
  const cached = useRef<TelehealthConnectionGrant | null>(null)
  const pending = useRef<Promise<TelehealthConnectionGrant> | null>(null)
  return async () => {
    if (!grant) throw new Error('Prepare the connection first.')
    if (!active) return grant
    const current = cached.current?.requestId === grant.requestId && Date.parse(cached.current.expiresAt) > Date.parse(grant.expiresAt) ? cached.current : grant
    if (current.requestStatus === 'InConsultation' && Date.parse(current.expiresAt) > Date.now() + 30_000) return current
    if (!evidence) throw new Error('Recheck your devices to rejoin this active visit.')
    pending.current ??= prepare(evidence, crypto.randomUUID()).then(result => { cached.current = result; return result }).finally(() => { pending.current = null })
    return pending.current
  }
}
