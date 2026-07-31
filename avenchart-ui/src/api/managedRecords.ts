// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { apiBaseUrl, apiFetch } from "./transport.ts";

export type ManagedRecordPolicy = {
  revision: string;
  lifecycleState: string;
  maxFileSizeBytes: number;
  acceptedMediaTypes: string[];
  recordClasses: string[];
  sourceTypes: string[];
  sensitivityLevels: string[];
  states: string[];
  storageAdapter: {
    adapterId: string;
    state: string;
    evidence: string;
  };
  validationAdapter: {
    adapterId: string;
    state: string;
    evidence: string;
  };
  antiMalwareVerified: boolean;
  environmentBoundary: string;
  productionBlockers: string[];
};

export type ManagedRecordItem = {
  intakeId: string;
  documentId: number | null;
  patientId: string;
  legacyPid: number;
  categoryId: number;
  categoryName: string;
  title: string;
  serviceDate: string;
  encounter: number | null;
  recordClass: string;
  sourceType: string;
  authorName: string;
  facilityId: number | null;
  facilityName: string | null;
  sensitivity: string;
  languageTag: string;
  fileName: string;
  mediaType: string;
  sizeBytes: number;
  contentVersion: number;
  contentChecksumSha256: string;
  storageAdapter: string;
  storageReference: string;
  state: string;
  workflowVersion: number;
  availabilityStatus: string;
  validationStatus: string;
  validationAdapter: string;
  antiMalwareVerified: boolean;
  failureReason: string | null;
  lastActor: string;
  lastActionAt: string;
  lastReason: string;
  idempotentReplay: boolean;
  availableActions: string[];
};

export type ManagedRecordList = {
  revision: string;
  patientId: string;
  totalCount: number;
  counts: {
    captured: number;
    quarantined: number;
    scanning: number;
    failed: number;
    available: number;
    withheld: number;
  };
  items: ManagedRecordItem[];
};

export type ManagedRecordEvent = {
  eventId: string;
  action: string;
  fromState: string | null;
  toState: string;
  fromRecordClass: string | null;
  toRecordClass: string;
  fromSensitivity: string | null;
  toSensitivity: string;
  reason: string;
  actor: string;
  occurredAt: string;
  workflowVersion: number;
  validationStatus: string;
  contentVersion: number;
  contentChecksumSha256: string;
  documentId: number | null;
};

export type ManagedRecordHistory = {
  revision: string;
  intakeId: string;
  currentState: string;
  currentVersion: number;
  eventCount: number;
  events: ManagedRecordEvent[];
};

export type ManagedRecordCreateInput = {
  patientId: string;
  categoryId: number;
  title: string;
  serviceDate: string;
  encounter: number | null;
  recordClass: string;
  sourceType: string;
  authorName: string;
  facilityId: number | null;
  sensitivity: string;
  languageTag: string;
  fileName: string;
  mediaType: string;
  contentBase64: string;
  expectedChecksumSha256: string;
  idempotencyKey: string;
  reason: string;
};

function headers(sessionId: string, json = false) {
  return {
    "X-Legacy EHR-Session": sessionId,
    ...(json ? { "content-type": "application/json" } : {}),
  };
}

export async function getManagedRecordPolicy(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ManagedRecordPolicy> {
  const response = await apiFetch(`${apiBaseUrl}/api/records/policy`, {
    headers: headers(sessionId),
    signal,
  });
  return (await response.json()) as ManagedRecordPolicy;
}

export async function getManagedRecords(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<ManagedRecordList> {
  const query = new URLSearchParams({ patientId });
  const response = await apiFetch(`${apiBaseUrl}/api/records/?${query}`, {
    headers: headers(sessionId),
    signal,
  });
  return (await response.json()) as ManagedRecordList;
}

export async function createManagedRecord(
  sessionId: string,
  input: ManagedRecordCreateInput,
): Promise<{ idempotentReplay: boolean; intake: ManagedRecordItem }> {
  const response = await apiFetch(`${apiBaseUrl}/api/records/`, {
    method: "POST",
    headers: headers(sessionId, true),
    body: JSON.stringify(input),
  });
  return (await response.json()) as {
    idempotentReplay: boolean;
    intake: ManagedRecordItem;
  };
}

export async function actOnManagedRecord(
  sessionId: string,
  intakeId: string,
  action: string,
  expectedVersion: number,
  reason: string,
): Promise<ManagedRecordItem> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/records/${encodeURIComponent(intakeId)}/${encodeURIComponent(action)}`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason }),
    },
  );
  return (await response.json()) as ManagedRecordItem;
}

export async function updateManagedRecordClassification(
  sessionId: string,
  intakeId: string,
  input: {
    expectedVersion: number;
    recordClass: string;
    sourceType: string;
    authorName: string;
    facilityId: number | null;
    sensitivity: string;
    languageTag: string;
    reason: string;
  },
): Promise<ManagedRecordItem> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/records/${encodeURIComponent(intakeId)}/classification`,
    {
      method: "PUT",
      headers: headers(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as ManagedRecordItem;
}

export async function getManagedRecordHistory(
  sessionId: string,
  intakeId: string,
  signal?: AbortSignal,
): Promise<ManagedRecordHistory> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/records/${encodeURIComponent(intakeId)}/history`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ManagedRecordHistory;
}

export async function deleteManagedRecordTestFixture(
  sessionId: string,
  intakeId: string,
): Promise<void> {
  await apiFetch(
    `${apiBaseUrl}/api/records/${encodeURIComponent(intakeId)}/test-fixture`,
    { method: "DELETE", headers: headers(sessionId) },
  );
}
