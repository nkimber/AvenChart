// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useRef, useState } from 'react'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'
import type { TelehealthConnectionGrant, TelehealthDevicePreflight } from './api.ts'

type Props = {
  prepare: (preflight: TelehealthDevicePreflight, key: string) => Promise<TelehealthConnectionGrant>
  onReady: (grant: TelehealthConnectionGrant, evidence: TelehealthDevicePreflight) => void
}

export default function TelehealthCallRecovery({ prepare, onReady }: Props) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const key = useRef<string | null>(null)
  async function recover() {
    if (busy) return
    setBusy(true); setError(null)
    try {
      const result = await runTelehealthDevicePreflight()
      if (result.status === 'failed') throw new Error(result.message)
      key.current ??= crypto.randomUUID()
      onReady(await prepare(result.evidence, key.current), result.evidence)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'The active visit could not be rejoined. Refresh its status and try again.')
    } finally { setBusy(false) }
  }
  return <section aria-label="Reconnect to your active visit">
    <p>Your visit is still active. Recheck devices and rejoin the same video visit. Your saved record and physician ownership are preserved.</p>
    {error ? <p role="alert">{error}</p> : null}
    <button className="telehealth-button" type="button" disabled={busy} onClick={() => void recover()}>{busy ? 'Preparing reconnection…' : 'Prepare active-call reconnection'}</button>
  </section>
}
