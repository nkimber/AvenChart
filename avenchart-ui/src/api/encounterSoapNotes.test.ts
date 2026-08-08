// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  getEncounterSoapNoteConflict,
  saveEncounterSoapNote,
} from "./encounterSoapNotes.ts";

describe("encounter SOAP note transport", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts the optimistic version boundary through the governed clinician transport", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 91,
          detail: {
            encounter: 42,
            soapNote: { id: 91, version: 3, versions: [] },
          },
        }),
        { status: 201, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const response = await saveEncounterSoapNote("staff-session", 42, {
      dateTime: "2026-07-29 20:00:00",
      expectedVersion: 2,
      subjective: "Updated symptoms",
      objective: null,
      assessment: "Stable",
      plan: "Follow up",
    });

    expect(response.detail.soapNote?.version).toBe(3);
    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url.endsWith("/api/encounters/42/soap-notes")).toBe(true);
    expect(init.method).toBe("POST");
    expect(new Headers(init.headers).get("X-AvenChart-Session")).toBe(
      "staff-session",
    );
    expect(JSON.parse(String(init.body))).toEqual({
      dateTime: "2026-07-29 20:00:00",
      expectedVersion: 2,
      subjective: "Updated symptoms",
      objective: null,
      assessment: "Stable",
      plan: "Follow up",
    });
  });

  it("returns structured version-conflict evidence from a governed 409", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            error: "SOAP note version 4 is current.",
            code: "soap_note_version_conflict",
            currentVersion: 4,
            isLocked: false,
          }),
          { status: 409, headers: { "Content-Type": "application/json" } },
        ),
      ),
    );

    const caught = await saveEncounterSoapNote("staff-session", 42, {
      dateTime: "2026-07-29 20:00:00",
      expectedVersion: 3,
      subjective: "Stale draft",
    }).catch((error: unknown) => error);

    expect(getEncounterSoapNoteConflict(caught)).toEqual({
      message: "SOAP note version 4 is current.",
      currentVersion: 4,
      isLocked: false,
    });
  });
});
