// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  getExperienceBaseline,
  type ExperienceBaseline,
} from "./experienceBaseline.ts";

const response: ExperienceBaseline = {
  revision: "local-experience-baseline-v1",
  lifecycleState: "proposed",
  ownerRole: "UX + clinical product owner",
  accessibilityStandard:
    "WCAG 2.2 AA proposed; current automated evidence is WCAG 2.1 A/AA",
  scope: "Modern UI staff and portal applications using synthetic data",
  counts: {
    roles: 4,
    environments: 5,
    tasks: 13,
    criteria: 12,
    metLocal: 5,
    measuredLocal: 3,
    ownerGated: 3,
    proposed: 1,
    analyticsEvents: 6,
    analyticsEventsCollected: 0,
    gaps: 6,
  },
  roles: [],
  environments: [],
  tasks: [],
  criteria: [],
  analyticsEvents: [],
  forbiddenAnalyticsProperties: ["patientId", "sessionId"],
  gaps: [],
};

describe("getExperienceBaseline", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("loads the protected, non-collecting experience registry", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(response), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    const controller = new AbortController();

    const result = await getExperienceBaseline(
      "staff-session",
      controller.signal,
    );

    expect(result).toEqual(response);
    expect(result.lifecycleState).toBe("proposed");
    expect(result.counts.analyticsEventsCollected).toBe(0);
    expect(result.forbiddenAnalyticsProperties).toEqual([
      "patientId",
      "sessionId",
    ]);
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5001/api/administration/experience-baseline",
      expect.objectContaining({
        headers: { "X-Legacy EHR-Session": "staff-session" },
      }),
    );
    expect(fetchMock.mock.calls[0]?.[1]?.signal).not.toBe(controller.signal);
  });
});
