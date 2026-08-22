// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  getIdentityProviderReadiness,
  type IdentityProviderReadiness,
} from "./identityProvider.ts";

const readiness: IdentityProviderReadiness = {
  revision: "local-identity-adapter-v1",
  lifecycleState: "local-foundation-owner-gated",
  activeAdapterId: "local-database-staff-session",
  activeAdapterKind: "local-development-session",
  environmentBoundary:
    "Local development identities are not an approved production identity source.",
  counts: {
    identityTypes: 4,
    routedThroughAdapter: 1,
    productionApproved: 0,
    cryptographicallyValidated: 0,
    facilityScoped: 0,
    emergencyEnabled: 0,
    blockingGaps: 7,
  },
  adapter: {
    adapterId: "local-database-staff-session",
    adapterKind: "local-development-session",
    interface: "IStaffIdentityAdapter",
    credentialSource: "local database",
    subjectKey: "username",
    resolvedClaims: ["username", "role"],
    sessionStates: ["issued", "active", "expired", "revoked-by-logout"],
    productionApproved: false,
    validatesIssuer: false,
    validatesAudience: false,
    validatesSignature: false,
    enforcesMfa: false,
    enforcesDevicePolicy: false,
    enforcesFacilityScope: false,
  },
  identityTypes: [],
  boundaryControls: [],
  verification: [],
  gaps: [],
};

describe("getIdentityProviderReadiness", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("loads the protected SEC-02 readiness contract", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(readiness), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );

    const result = await getIdentityProviderReadiness("staff-session");

    expect(result).toEqual(readiness);
    expect(result.counts.productionApproved).toBe(0);
    expect(result.counts.emergencyEnabled).toBe(0);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(
      "http://localhost:5001/api/administration/identity-provider/readiness",
    );
    expect(new Headers(init.headers).get("X-AvenChart-Session")).toBe(
      "staff-session",
    );
    expect(init.credentials).toBe("include");
  });
});
