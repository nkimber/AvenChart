// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ApiRequestError,
  apiFetch,
  isRequestCancellation,
} from "./transport.ts";
import { saveClinicianSession, savePortalSession } from "../auth/session.ts";

describe("isRequestCancellation", () => {
  it("recognizes normalized caller cancellation", () => {
    expect(
      isRequestCancellation(
        new ApiRequestError("cancelled", undefined, undefined, "cancelled"),
      ),
    ).toBe(true);
  });

  it("recognizes native abort errors and rejects unrelated failures", () => {
    expect(
      isRequestCancellation(new DOMException("aborted", "AbortError")),
    ).toBe(true);
    expect(isRequestCancellation(new TypeError("network unavailable"))).toBe(
      false,
    );
  });
});

describe("staff access context transport", () => {
  afterEach(() => {
    window.sessionStorage.clear();
    vi.unstubAllGlobals();
  });

  it("adds the selected facility and purpose to a clinician request", async () => {
    saveClinicianSession({
      sessionId: "staff-session",
      username: "clinician",
      displayName: "Clinician",
      role: "provider",
      facilityId: 17,
      purposeOfUse: "treatment",
    });
    const request = vi.fn().mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", request);

    await apiFetch("http://localhost:5001/api/patients", {
      headers: { "X-AvenChart-Session": "staff-session" },
    });

    const [, init] = request.mock.calls[0] as [string, RequestInit];
    const headers = new Headers(init.headers);
    expect(headers.get("X-AvenChart-Facility-Id")).toBe("17");
    expect(headers.get("X-AvenChart-Purpose-Of-Use")).toBe("treatment");
  });

  it("adds the BFF CSRF proof and includes credentials for an unsafe clinician request", async () => {
    saveClinicianSession({
      sessionId: "browser-staff-session",
      username: "clinician",
      displayName: "Clinician",
      role: "provider",
      authenticationMode: "oidc-bff",
      csrfToken: "staff-csrf-proof",
    });
    const request = vi.fn().mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", request);

    await apiFetch("http://localhost:5001/api/patients", {
      method: "POST",
      headers: { "X-AvenChart-Session": "browser-staff-session" },
    });

    const [, init] = request.mock.calls[0] as [string, RequestInit];
    expect(new Headers(init.headers).get("X-AvenChart-CSRF")).toBe("staff-csrf-proof");
    expect(init.credentials).toBe("include");
  });

  it("adds the BFF CSRF proof for an unsafe portal request", async () => {
    savePortalSession({
      sessionId: "browser-portal-session",
      username: "portal-user",
      portalUsername: "portal-user",
      displayName: "Portal User",
      authenticationMode: "oidc-bff",
      csrfToken: "portal-csrf-proof",
    });
    const request = vi.fn().mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", request);

    await apiFetch("http://localhost:5001/api/patient-portal/messages", {
      method: "POST",
      headers: { "X-AvenChart-Patient-Portal-Session": "browser-portal-session" },
    });

    const [, init] = request.mock.calls[0] as [string, RequestInit];
    expect(new Headers(init.headers).get("X-AvenChart-CSRF")).toBe("portal-csrf-proof");
  });
});
