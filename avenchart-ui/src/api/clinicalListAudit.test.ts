// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  getAllergyAuditHistory,
  getImmunizationAuditHistory,
  getProblemAuditHistory,
} from "../api.ts";

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json" },
  });
}

describe("clinical-list immutable audit transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("uses protected, encoded history endpoints for each audited resource", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ eventCount: 1, events: [] }))
      .mockResolvedValueOnce(jsonResponse({ eventCount: 1, events: [] }))
      .mockResolvedValueOnce(jsonResponse({ eventCount: 1, events: [] }));

    await getProblemAuditHistory("staff-session", "PROB-41");
    await getAllergyAuditHistory("staff-session", "ALG / 41");
    await getImmunizationAuditHistory("staff-session", "IMM-MODERN-41");

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "http://localhost:5001/api/clinical-lists/problems/PROB-41/audit-history",
      "http://localhost:5001/api/clinical-lists/allergies/ALG%20%2F%2041/audit-history",
      "http://localhost:5001/api/clinical-lists/immunizations/IMM-MODERN-41/audit-history",
    ]);
    expect(
      fetchMock.mock.calls.map(([, request]) =>
        new Headers(request?.headers).get("X-AvenChart-Session"),
      ),
    ).toEqual(["staff-session", "staff-session", "staff-session"]);
  });
});
