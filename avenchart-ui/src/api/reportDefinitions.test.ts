import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  createGovernedReportDefinition,
  getGovernedReportDefinitions,
  transitionGovernedReportDefinition,
} from "./reportDefinitions.ts";

describe("governed report-definition transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends bounded list filters and a complete governed definition", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            definitions: [],
            page: 2,
            pageSize: 5,
            total: 0,
          }),
          { status: 200, headers: { "content-type": "application/json" } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            definitionId: "11111111-1111-1111-1111-111111111111",
            stableKey: "tmp-report-test",
            governanceVersion: 1,
            latestRevisionId: "22222222-2222-2222-2222-222222222222",
            activeRevisionId: null,
            revisions: [],
            events: [],
          }),
          { status: 201, headers: { "content-type": "application/json" } },
        ),
      );

    await getGovernedReportDefinitions("staff-session", {
      search: "quality",
      status: "draft",
      page: 2,
      pageSize: 5,
    });
    await createGovernedReportDefinition("staff-session", {
      stableKey: "tmp-report-test",
      title: "Synthetic quality report",
      ownerUsername: "admin",
      purpose: "Verify the governed report-definition transport contract.",
      reportFamily: "appointments",
      sensitivity: "restricted",
      rowPolicy: "facility-scoped",
      retentionDays: 30,
      allowedRecipients: ["requesting-user"],
      deliveryModes: ["local-download"],
      reason: "Create a bounded synthetic report definition.",
    });

    expect(fetchMock.mock.calls[0]?.[0]).toContain(
      "search=quality&status=draft",
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual(
      expect.objectContaining({
        reportFamily: "appointments",
        rowPolicy: "facility-scoped",
        retentionDays: 30,
        deliveryModes: ["local-download"],
      }),
    );
  });

  it("sends the loaded revision version for lifecycle transitions", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          definitionId: "11111111-1111-1111-1111-111111111111",
          stableKey: "tmp-report-test",
          governanceVersion: 2,
          latestRevisionId: "22222222-2222-2222-2222-222222222222",
          activeRevisionId: null,
          revisions: [],
          events: [],
        }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    );

    await transitionGovernedReportDefinition(
      "staff-session",
      "11111111-1111-1111-1111-111111111111",
      "review",
      0,
      "Owner reviewed the synthetic dictionary and purpose.",
    );

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5001/api/reports/definitions/11111111-1111-1111-1111-111111111111/review",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          expectedVersion: 0,
          reason: "Owner reviewed the synthetic dictionary and purpose.",
        }),
      }),
    );
  });
});
