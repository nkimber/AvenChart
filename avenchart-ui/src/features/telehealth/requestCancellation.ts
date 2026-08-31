// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { TelehealthRequestStatus } from './api.ts'

export function canCancelPatientTelehealthRequest(status: TelehealthRequestStatus) {
  return ['Draft', 'LocationConfirmed', 'SafetyScreening', 'Intake', 'Verification', 'OperationalReview', 'Queued'].includes(status)
}
