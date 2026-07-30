import { afterEach, describe, expect, it, vi } from "vitest";
import {
  archiveEncounterWithReason,
  EncounterLifecycleConflictError,
  restoreEncounterWithReason,
  signEncounterUnderLocalPolicy,
} from "./encounterLifecycle.ts";

describe("encounter lifecycle transport", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends only signature intent because actor and time are server-derived", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 12,
          detail: { encounter: 42, signatures: [] },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await signEncounterUnderLocalPolicy("staff-session", 42, {
      isLock: true,
      amendment: "Append the corrected assessment.",
    });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url.endsWith("/api/encounters/42/sign")).toBe(true);
    expect(init.method).toBe("PUT");
    expect(new Headers(init.headers).get("X-Legacy EHR-Session")).toBe(
      "staff-session",
    );
    expect(JSON.parse(String(init.body))).toEqual({
      isLock: true,
      amendment: "Append the corrected assessment.",
    });
  });

  it("carries reason and loaded version for archive and restore", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await archiveEncounterWithReason(
      "staff-session",
      42,
      3,
      "Visit entered in the wrong operational queue.",
    );
    await restoreEncounterWithReason(
      "staff-session",
      42,
      4,
      "Clinical review confirmed the encounter should remain active.",
    );

    expect(fetchMock).toHaveBeenCalledTimes(2);
    const [archiveUrl, archiveInit] = fetchMock.mock.calls[0] as [
      string,
      RequestInit,
    ];
    expect(archiveUrl.endsWith("/api/encounters/42/archive")).toBe(true);
    expect(JSON.parse(String(archiveInit.body))).toEqual({
      expectedArchiveVersion: 3,
      reason: "Visit entered in the wrong operational queue.",
    });
    const [restoreUrl, restoreInit] = fetchMock.mock.calls[1] as [
      string,
      RequestInit,
    ];
    expect(restoreUrl.endsWith("/api/encounters/42/restore")).toBe(true);
    expect(JSON.parse(String(restoreInit.body))).toEqual({
      expectedArchiveVersion: 4,
      reason: "Clinical review confirmed the encounter should remain active.",
    });
  });

  it("maps a stale archive write to an explicit lifecycle conflict", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            error: "The encounter changed. Reload and try again.",
          }),
          { status: 409, headers: { "Content-Type": "application/json" } },
        ),
      ),
    );

    const caught = await archiveEncounterWithReason(
      "staff-session",
      42,
      1,
      "Archive after review.",
    ).catch((error: unknown) => error);

    expect(caught).toBeInstanceOf(EncounterLifecycleConflictError);
    expect((caught as Error).message).toContain("encounter changed");
  });

  it("fails closed when detail did not provide an archive version", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const caught = await restoreEncounterWithReason(
      "staff-session",
      42,
      Number.NaN,
      "Restore after review.",
    ).catch((error: unknown) => error);

    expect(caught).toBeInstanceOf(EncounterLifecycleConflictError);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
