// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState } from 'react'
import { closeSyntheticTelehealthVisit } from './api.ts'

export default function TelehealthSyntheticVisitClosurePanel({ consultationId, expectedVersion, onClosed }: { consultationId: string, expectedVersion: number, onClosed: () => void }) {
  const [lockReviewed, setLockReviewed] = useState(false); const [syntheticConfirmed, setSyntheticConfirmed] = useState(false); const [working, setWorking] = useState(false); const [message, setMessage] = useState<string | null>(null)
  const ready = lockReviewed && syntheticConfirmed && !working
  async function close() { if (!ready) return; setWorking(true); setMessage(null); try { await closeSyntheticTelehealthVisit(consultationId, { expectedConsultationVersion: expectedVersion, encounterLockReviewed: true, syntheticClosureConfirmed: true }); setMessage('Synthetic visit closed. The doctor is available for new work; the appointment, delivery, billing, claims, and integrations remain unchanged.'); onClosed() } catch (caught) { setMessage(caught instanceof Error ? caught.message : 'Synthetic visit closure could not be recorded. No other action occurred.') } finally { setWorking(false) } }
  return <section className="telehealth-completion-review"><p className="telehealth-kicker">Synthetic visit closure</p><h4>Return to availability</h4><p role="note">This closes only the synthetic consultation and request after its encounter lock. It does not complete the appointment or create patient delivery, billing, a claim, pharmacy transmission, or an external action.</p><label className="telehealth-check"><input type="checkbox" checked={lockReviewed} onChange={(event) => setLockReviewed(event.target.checked)} />I reviewed the governed encounter lock.</label><label className="telehealth-check"><input type="checkbox" checked={syntheticConfirmed} onChange={(event) => setSyntheticConfirmed(event.target.checked)} />I confirm this synthetic-only closure has no billing or delivery effect.</label><button className="telehealth-button" type="button" disabled={!ready} onClick={() => void close()}>{working ? 'Closing…' : 'Close synthetic visit and return to availability'}</button>{message ? <p role="status">{message}</p> : null}</section>
}
