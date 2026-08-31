// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import type { TelehealthReservation } from './api.ts'

export type ClinicianConnectionAbandonConfirmations = {
  noConsultationConfirmed: boolean
  syntheticConnectionAbandonConfirmed: boolean
}

export default function ClinicianConnectionAbandonControl({ reservation, disabled, onAbandon }: {
  reservation: TelehealthReservation
  disabled: boolean
  onAbandon: (confirmations: ClinicianConnectionAbandonConfirmations) => void
}) {
  const [confirmations, setConfirmations] = useState<ClinicianConnectionAbandonConfirmations>({ noConsultationConfirmed: false, syntheticConnectionAbandonConfirmed: false })
  useEffect(() => { setConfirmations({ noConsultationConfirmed: false, syntheticConnectionAbandonConfirmed: false }) }, [reservation.reservationId, reservation.requestVersion])
  const ready = !disabled && confirmations.noConsultationConfirmed && confirmations.syntheticConnectionAbandonConfirmed
  return <section className="telehealth-connection-abandon" aria-labelledby="connection-abandon-title">
    <h3 id="connection-abandon-title">Abandon synthetic connection attempt</h3>
    <p id="connection-abandon-note" role="note">Use this only when the prepared synthetic connection cannot continue and no consultation has started. It ends the pending local grants and synthetic session, returns this request to its existing queue position, and does not record a clinical decision or change clinical, billing, claim, integration, or external state.</p>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.noConsultationConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, noConsultationConfirmed: event.target.checked }))} />I confirm no consultation has been started for this request.</label>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.syntheticConnectionAbandonConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, syntheticConnectionAbandonConfirmed: event.target.checked }))} />I understand this ends only the pending synthetic connection attempt and returns the request to its existing queue position.</label>
    <button className="telehealth-button telehealth-button-secondary" type="button" aria-describedby="connection-abandon-note" disabled={!ready} onClick={() => onAbandon(confirmations)}>Abandon connection attempt</button>
  </section>
}
