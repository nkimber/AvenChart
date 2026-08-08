// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { EncounterDetail } from "../api.ts";
import { apiBaseUrl, apiFetch, ApiRequestError } from "./transport.ts";

export const LOCAL_ENCOUNTER_SIGNATURE_POLICY = "local-encounter-signature-v1";

export type EncounterLifecycleDetail = EncounterDetail & {
  archivedAt?: string | null;
  archiveVersion: number;
};

export type EncounterSignInput = {
  isLock: boolean;
  amendment?: string | null;
};

export class EncounterLifecycleConflictError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "EncounterLifecycleConflictError";
  }
}

function clinicianHeaders(sessionId: string) {
  return {
    "content-type": "application/json",
    "X-AvenChart-Session": sessionId,
  };
}

async function lifecycleFetch(
  input: RequestInfo | URL,
  init: RequestInit,
  operation: string,
) {
  try {
    return await apiFetch(input, init);
  } catch (caught) {
    if (caught instanceof ApiRequestError && caught.status === 409) {
      throw new EncounterLifecycleConflictError(
        `${operation} could not be completed because the encounter changed. Reloaded state is required before retrying.`,
      );
    }
    throw caught;
  }
}

function requireArchiveVersion(version: number) {
  if (!Number.isInteger(version) || version < 0) {
    throw new EncounterLifecycleConflictError(
      "The encounter archive version is unavailable. Reload the encounter before changing its archive state.",
    );
  }
}

export function asEncounterLifecycleDetail(
  detail: EncounterDetail,
): EncounterLifecycleDetail {
  return detail as EncounterLifecycleDetail;
}

export async function signEncounterUnderLocalPolicy(
  sessionId: string,
  encounter: number,
  input: EncounterSignInput,
  signal?: AbortSignal,
): Promise<{ id: number; detail: EncounterDetail }> {
  const response = await lifecycleFetch(
    `${apiBaseUrl}/api/encounters/${encounter}/sign`,
    {
      method: "PUT",
      headers: clinicianHeaders(sessionId),
      body: JSON.stringify(input),
      signal,
    },
    "Encounter signature",
  );
  return response.json();
}

async function changeEncounterArchiveState(
  sessionId: string,
  encounter: number,
  action: "archive" | "restore",
  expectedArchiveVersion: number,
  reason: string,
  signal?: AbortSignal,
) {
  requireArchiveVersion(expectedArchiveVersion);
  await lifecycleFetch(
    `${apiBaseUrl}/api/encounters/${encounter}/${action}`,
    {
      method: "PUT",
      headers: clinicianHeaders(sessionId),
      body: JSON.stringify({ expectedArchiveVersion, reason }),
      signal,
    },
    action === "archive" ? "Encounter archive" : "Encounter restore",
  );
}

export async function archiveEncounterWithReason(
  sessionId: string,
  encounter: number,
  expectedArchiveVersion: number,
  reason: string,
  signal?: AbortSignal,
) {
  await changeEncounterArchiveState(
    sessionId,
    encounter,
    "archive",
    expectedArchiveVersion,
    reason,
    signal,
  );
}

export async function restoreEncounterWithReason(
  sessionId: string,
  encounter: number,
  expectedArchiveVersion: number,
  reason: string,
  signal?: AbortSignal,
) {
  await changeEncounterArchiveState(
    sessionId,
    encounter,
    "restore",
    expectedArchiveVersion,
    reason,
    signal,
  );
}
