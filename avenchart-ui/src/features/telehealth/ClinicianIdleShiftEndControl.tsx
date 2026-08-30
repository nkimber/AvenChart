// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import type { TelehealthShift } from './api.ts'

export type ClinicianIdleShiftEndConfirmations = {
  noActiveWorkConfirmed: boolean
  syntheticEndConfirmed: boolean
}

export default function ClinicianIdleShiftEndControl({ shift, reservationActive, consultationActive, working, onEnd }: {
  shift: TelehealthShift
  reservationActive: boolean
  consultationActive: boolean
  working: boolean
  onEnd: (confirmations: ClinicianIdleShiftEndConfirmations) => void
}) {
  const [confirmations, setConfirmations] = useState<ClinicianIdleShiftEndConfirmations>({ noActiveWorkConfirmed: false, syntheticEndConfirmed: false })
  useEffect(() => { setConfirmations({ noActiveWorkConfirmed: false, syntheticEndConfirmed: false }) }, [shift.shiftId, shift.version])
  if (shift.status !== 'Active' || reservationActive || consultationActive) return null
  const ready = confirmations.noActiveWorkConfirmed && confirmations.syntheticEndConfirmed && !working
  return <div className="telehealth-idle-shift-end">
    <p id="idle-shift-end-note" role="note">Ending this shift changes only your synthetic availability. It does not change patient, appointment, encounter, clinical, billing, claim, media, integration, or external state.</p>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.noActiveWorkConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, noActiveWorkConfirmed: event.target.checked }))} />I confirm I hold no active telehealth reservation or consultation work.</label>
    <label className="telehealth-check"><input type="checkbox" checked={confirmations.syntheticEndConfirmed} onChange={(event) => setConfirmations((current) => ({ ...current, syntheticEndConfirmed: event.target.checked }))} />I understand this ends only my synthetic shift and changes no patient-care or downstream state.</label>
    <button className="telehealth-button telehealth-button-secondary" type="button" aria-describedby="idle-shift-end-note" disabled={!ready} onClick={() => onEnd(confirmations)}>End idle telehealth shift</button>
  </div>
}
