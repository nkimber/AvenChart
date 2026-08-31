// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import type { TelehealthReservation } from './api.ts'

export type ClinicianReservationReleaseConfirmations = {
  noConnectionOrConsultationConfirmed: boolean
  syntheticReleaseConfirmed: boolean
}

export default function ClinicianReservationReleaseControl({ reservation, disabled, onRelease }: {
  reservation: TelehealthReservation
  disabled: boolean
  onRelease: (confirmations: ClinicianReservationReleaseConfirmations) => void
}) {
  const [confirmations, setConfirmations] = useState<ClinicianReservationReleaseConfirmations>({ noConnectionOrConsultationConfirmed: false, syntheticReleaseConfirmed: false })
  useEffect(() => { setConfirmations({ noConnectionOrConsultationConfirmed: false, syntheticReleaseConfirmed: false }) }, [reservation.reservationId, reservation.requestVersion])
  const ready = !disabled && confirmations.noConnectionOrConsultationConfirmed && confirmations.syntheticReleaseConfirmed
  return <section className="telehealth-reservation-release" aria-labelledby="reservation-release-title">
    <h3 id="reservation-release-title">Release synthetic reservation</h3>
    <p id="reservation-release-note" role="note">This returns only this unconnected synthetic request to the same queue. It cannot be used after a connection room or consultation exists, and it does not record a clinical decision or change clinical, billing, claim, media, integration, or external state.</p>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.noConnectionOrConsultationConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, noConnectionOrConsultationConfirmed: event.target.checked }))} />I confirm no connection room or consultation has been started for this request.</label>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.syntheticReleaseConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, syntheticReleaseConfirmed: event.target.checked }))} />I understand this only releases the synthetic reservation and returns the request to its existing queue position.</label>
    <button className="telehealth-button telehealth-button-secondary" type="button" aria-describedby="reservation-release-note" disabled={!ready} onClick={() => onRelease(confirmations)}>Release reservation to queue</button>
  </section>
}
