// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { getAppointmentStatus } from '../domain/appointmentStatus.ts'

export function AppointmentStatusBadge({ value }: { value?: string | null }) {
  const status = getAppointmentStatus(value)
  return (
    <span
      className={`appt-status ${status.className}`}
      data-appointment-status={status.semantic}
      title={status.semantic === 'other' && value ? `Unrecognized status: ${value}` : undefined}
    >
      {status.label}
    </span>
  )
}
