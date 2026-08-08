// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { EncounterDetail, EncounterSoapNote } from "../api.ts";
import {
  ApiRequestError,
  apiBaseUrl,
  apiFetch,
  type ApiProblemDetails,
} from "./transport.ts";

export type EncounterSoapNoteVersion = {
  id: number;
  version: number;
  supersedesNoteId?: number | null;
  noteDateTime: string;
  savedAt: string;
  savedBy?: string | null;
  evidenceSource: "runtime" | "migration-backfill";
  subjective?: string | null;
  objective?: string | null;
  assessment?: string | null;
  plan?: string | null;
};

export type VersionedEncounterSoapNote = EncounterSoapNote & {
  id: number;
  version: number;
  noteDateTime: string;
  savedAt: string;
  savedBy?: string | null;
  evidenceSource: "runtime" | "migration-backfill";
  isLocked: boolean;
  versions: EncounterSoapNoteVersion[];
};

export type VersionedEncounterDetail = Omit<EncounterDetail, "soapNote"> & {
  soapNote?: VersionedEncounterSoapNote | null;
};

export type SaveEncounterSoapNoteInput = {
  dateTime: string;
  expectedVersion: number;
  subjective?: string | null;
  objective?: string | null;
  assessment?: string | null;
  plan?: string | null;
};

export type EncounterSoapNoteConflict = {
  message: string;
  currentVersion: number;
  isLocked: boolean;
};

type EncounterSoapNoteConflictProblem = ApiProblemDetails & {
  code?: string;
  currentVersion?: number;
  isLocked?: boolean;
};

export function getVersionedEncounterDetail(
  detail: EncounterDetail,
): VersionedEncounterDetail {
  return detail as VersionedEncounterDetail;
}

export function getEncounterSoapNoteConflict(
  error: unknown,
): EncounterSoapNoteConflict | null {
  if (!(error instanceof ApiRequestError) || error.status !== 409) return null;
  const problem = error.problem as EncounterSoapNoteConflictProblem | undefined;
  if (
    problem?.code !== "soap_note_version_conflict" &&
    problem?.code !== "encounter_locked"
  )
    return null;

  return {
    message: error.message,
    currentVersion: problem.currentVersion ?? 0,
    isLocked: problem.isLocked ?? problem.code === "encounter_locked",
  };
}

export async function saveEncounterSoapNote(
  sessionId: string,
  encounter: number,
  input: SaveEncounterSoapNoteInput,
  signal?: AbortSignal,
): Promise<{ id: number; detail: VersionedEncounterDetail }> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/encounters/${encounter}/soap-notes`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-AvenChart-Session": sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  );
  return response.json();
}
