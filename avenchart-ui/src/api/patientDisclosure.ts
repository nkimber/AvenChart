// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { apiBaseUrl, apiFetch } from "./transport.ts";

export type PatientDisclosureOption = {
  value: string;
  label: string;
};

export type PatientDisclosureScopeOption = {
  key: string;
  label: string;
  description: string;
};

export type PatientDisclosurePolicy = {
  revision: string;
  lifecycleState: string;
  authorityTypes: PatientDisclosureOption[];
  verificationMethods: PatientDisclosureOption[];
  scopes: PatientDisclosureScopeOption[];
  emergencyAccess: {
    enabled: boolean;
    state: string;
    reason: string;
    requiredDecisions: string[];
  };
  boundaries: string[];
};

export type PatientDisclosureAuthority = {
  authorityId: string;
  patientId: string;
  authorityType: string;
  proxyName: string | null;
  proxyRelationship: string | null;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  effectiveFrom: string;
  expiresAt: string;
  verificationMethod: string;
  verificationReference: string;
  policyRevision: string;
  status: string;
  effectiveStatus: string;
  version: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  allowedActions: string[];
};

export type PatientDisclosureAuthorityEvent = {
  eventId: number;
  authorityId: string;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  version: number;
  reason: string;
  occurredAt: string;
  username: string;
  policyRevision: string;
};

export type PatientDisclosureRequest = {
  requestId: string;
  patientId: string;
  authorityId: string;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  status: string;
  version: number;
  policyRevision: string;
  requestedAt: string;
  requestedBy: string;
  decidedAt: string | null;
  decidedBy: string | null;
  decisionReason: string | null;
  authorityEffectiveStatus: string;
  authorityVersion: number;
  allowedActions: string[];
};

export type PatientDisclosureRequestEvent = {
  eventId: number;
  requestId: string;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  version: number;
  reason: string;
  occurredAt: string;
  username: string;
  authorityId: string;
  authorityVersion: number;
  authorityEffectiveStatus: string;
  policyRevision: string;
};

export type PatientDisclosureAuthorityInput = {
  authorityType: string;
  proxyName: string | null;
  proxyRelationship: string | null;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  effectiveFrom: string;
  expiresAt: string;
  verificationMethod: string;
  verificationReference: string;
  reason: string;
};

export type PatientDisclosureRequestInput = {
  authorityId: string;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  reason: string;
};

function patientUrl(patientId: string, suffix: string) {
  return `${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/${suffix}`;
}

function staffHeaders(sessionId: string, json = false) {
  return {
    "X-AvenChart-Session": sessionId,
    ...(json ? { "content-type": "application/json" } : {}),
  };
}

export async function getPatientDisclosurePolicy(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientDisclosurePolicy> {
  const response = await apiFetch(
    patientUrl(patientId, "disclosure-policy"),
    { headers: staffHeaders(sessionId), signal },
  );
  return (await response.json()) as PatientDisclosurePolicy;
}

export async function getPatientDisclosureAuthorities(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientDisclosureAuthority[]> {
  const response = await apiFetch(
    patientUrl(patientId, "disclosure-authorities"),
    { headers: staffHeaders(sessionId), signal },
  );
  return (await response.json()) as PatientDisclosureAuthority[];
}

export async function createPatientDisclosureAuthority(
  sessionId: string,
  patientId: string,
  input: PatientDisclosureAuthorityInput,
): Promise<PatientDisclosureAuthority> {
  const response = await apiFetch(
    patientUrl(patientId, "disclosure-authorities"),
    {
      method: "POST",
      headers: staffHeaders(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as PatientDisclosureAuthority;
}

export async function transitionPatientDisclosureAuthority(
  sessionId: string,
  patientId: string,
  authorityId: string,
  action: "activate" | "revoke",
  expectedVersion: number,
  reason: string,
): Promise<PatientDisclosureAuthority> {
  const response = await apiFetch(
    patientUrl(
      patientId,
      `disclosure-authorities/${encodeURIComponent(authorityId)}/${action}`,
    ),
    {
      method: "POST",
      headers: staffHeaders(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason }),
    },
  );
  return (await response.json()) as PatientDisclosureAuthority;
}

export async function getPatientDisclosureAuthorityHistory(
  sessionId: string,
  patientId: string,
  authorityId: string,
): Promise<PatientDisclosureAuthorityEvent[]> {
  const response = await apiFetch(
    patientUrl(
      patientId,
      `disclosure-authorities/${encodeURIComponent(authorityId)}/history`,
    ),
    { headers: staffHeaders(sessionId) },
  );
  return (await response.json()) as PatientDisclosureAuthorityEvent[];
}

export async function getPatientDisclosureRequests(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientDisclosureRequest[]> {
  const response = await apiFetch(
    patientUrl(patientId, "disclosure-requests"),
    { headers: staffHeaders(sessionId), signal },
  );
  return (await response.json()) as PatientDisclosureRequest[];
}

export async function createPatientDisclosureRequest(
  sessionId: string,
  patientId: string,
  input: PatientDisclosureRequestInput,
): Promise<PatientDisclosureRequest> {
  const response = await apiFetch(
    patientUrl(patientId, "disclosure-requests"),
    {
      method: "POST",
      headers: staffHeaders(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as PatientDisclosureRequest;
}

export async function decidePatientDisclosureRequest(
  sessionId: string,
  patientId: string,
  requestId: string,
  action: "approve" | "deny",
  expectedVersion: number,
  reason: string,
): Promise<PatientDisclosureRequest> {
  const response = await apiFetch(
    patientUrl(
      patientId,
      `disclosure-requests/${encodeURIComponent(requestId)}/decision`,
    ),
    {
      method: "POST",
      headers: staffHeaders(sessionId, true),
      body: JSON.stringify({ action, expectedVersion, reason }),
    },
  );
  return (await response.json()) as PatientDisclosureRequest;
}

export async function getPatientDisclosureRequestHistory(
  sessionId: string,
  patientId: string,
  requestId: string,
): Promise<PatientDisclosureRequestEvent[]> {
  const response = await apiFetch(
    patientUrl(
      patientId,
      `disclosure-requests/${encodeURIComponent(requestId)}/history`,
    ),
    { headers: staffHeaders(sessionId) },
  );
  return (await response.json()) as PatientDisclosureRequestEvent[];
}
