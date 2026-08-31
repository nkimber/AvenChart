// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { TelehealthRequestStatus } from './api.ts'

export function connectionWasReturnedToQueue(previous: TelehealthRequestStatus | null, next: TelehealthRequestStatus) {
  return previous === 'Connecting' && next === 'Queued'
}

export const connectionReturnedToQueueMessage = 'The synthetic connection room is no longer active. Your request has returned to its existing queue position. Keep this page open for current status; no consultation, clinical decision, or external action occurred.'
