// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { ApiRequestError, apiBaseUrl, apiFetch } from '../../api/transport.ts'
export type DemoRole = 'patient' | 'physician'
export type DemoTransport = 'NON_PRODUCTION_LOCAL_WEBRTC_POC' | 'NON_PRODUCTION_INTERNET_ACS_CALLING_POC'
export type DemoRun = { runId: string; expiresAt: string; patientInvitation: string; physicianInvitation: string; transport: DemoTransport }
export type DemoParticipant = { participantId: string; runId: string; role: DemoRole; expiresAt: string; transport: DemoTransport }
export type DemoStatus = { runId: string; status: 'Active' | 'Ended' | 'Expired'; expiresAt: string; patientStatus: string; physicianStatus: string; transport: DemoTransport }
export class DemoRequestError extends Error {
  readonly status: number
  constructor(message: string, status: number) { super(message); this.name = 'DemoRequestError'; this.status = status }
}

export function newDemoKey() {
  return Array.from(crypto.getRandomValues(new Uint8Array(32)), value => value.toString(16).padStart(2, '0')).join('')
}

export async function demoRequest<T>(path: string, key: string, body?: unknown, method = body === undefined ? 'GET' : 'POST', signal?: AbortSignal, commandId?: string): Promise<T> {
  const response = await apiFetch(`${apiBaseUrl}/api/telehealth/v1/demo${path}`, {
    method, signal, cache: 'no-store', credentials: 'omit',
    headers: { 'Content-Type': 'application/json', 'X-AvenChart-Telehealth-Demo-Key': key, ...(commandId ? { 'X-Idempotency-Key': commandId } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }).catch((error: unknown) => {
    if (error instanceof ApiRequestError) throw new DemoRequestError(error.message, error.status ?? 0)
    throw error
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null
    throw new DemoRequestError(problem?.detail ?? 'This synthetic demo is unavailable or its invitation has expired. Start a new demo or retry.', response.status)
  }
  return response.json() as Promise<T>
}

export function demoInvitationUrl(run: DemoRun, role: DemoRole) {
  return `${window.location.origin}/telehealth/demo/${run.runId}/${role}#invite=${role === 'patient' ? run.patientInvitation : run.physicianInvitation}`
}

export const demoStageLabel: Record<string, string> = {
  Invited: 'Waiting for the invitation to open', Opened: 'Session opened', DevicesReady: 'Devices ready', Connected: 'Connected', Offline: 'Session not responding',
}
