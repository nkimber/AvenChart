// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  createPatientDisclosureAuthority,
  createPatientDisclosureRequest,
  decidePatientDisclosureRequest,
  getPatientDisclosureAuthorities,
  getPatientDisclosureAuthorityHistory,
  getPatientDisclosurePolicy,
  getPatientDisclosureRequestHistory,
  getPatientDisclosureRequests,
  transitionPatientDisclosureAuthority,
} from "./patientDisclosure.ts";

describe("patient disclosure transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("uses the protected patient-scoped authority and request contracts", async () => {
    fetchMock.mockImplementation(async () =>
      new Response("[]", {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );

    await getPatientDisclosurePolicy("session", "MOD PAT/1");
    await getPatientDisclosureAuthorities("session", "MOD PAT/1");
    await createPatientDisclosureAuthority("session", "MOD PAT/1", {
      authorityType: "patient",
      proxyName: null,
      proxyRelationship: null,
      purpose: "care coordination",
      recipient: "Patient",
      scopeKeys: ["clinical-summary"],
      effectiveFrom: "2026-07-28T00:00:00Z",
      expiresAt: "2026-08-28T00:00:00Z",
      verificationMethod: "in-person",
      verificationReference: "front desk verification",
      reason: "Authority recorded",
    });
    await transitionPatientDisclosureAuthority(
      "session",
      "MOD PAT/1",
      "authority-id",
      "activate",
      0,
      "Evidence reviewed",
    );
    await getPatientDisclosureAuthorityHistory(
      "session",
      "MOD PAT/1",
      "authority-id",
    );
    await getPatientDisclosureRequests("session", "MOD PAT/1");
    await createPatientDisclosureRequest("session", "MOD PAT/1", {
      authorityId: "authority-id",
      purpose: "care coordination",
      recipient: "Patient",
      scopeKeys: ["clinical-summary"],
      reason: "Request recorded",
    });
    await decidePatientDisclosureRequest(
      "session",
      "MOD PAT/1",
      "request-id",
      "approve",
      0,
      "Scope matches",
    );
    await getPatientDisclosureRequestHistory(
      "session",
      "MOD PAT/1",
      "request-id",
    );

    expect(fetchMock).toHaveBeenCalledTimes(9);
    for (const call of fetchMock.mock.calls) {
      expect(String(call[0])).toContain("/api/patients/MOD%20PAT%2F1/");
      expect(new Headers(call[1]?.headers).get("X-AvenChart-Session")).toBe(
        "session",
      );
    }
    expect(fetchMock.mock.calls[2]?.[1]).toMatchObject({ method: "POST" });
    expect(fetchMock.mock.calls[3]?.[1]?.body).toBe(
      JSON.stringify({ expectedVersion: 0, reason: "Evidence reviewed" }),
    );
    expect(fetchMock.mock.calls[7]?.[1]?.body).toBe(
      JSON.stringify({
        action: "approve",
        expectedVersion: 0,
        reason: "Scope matches",
      }),
    );
  });
});
