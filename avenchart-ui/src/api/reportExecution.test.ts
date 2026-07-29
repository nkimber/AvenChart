import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  downloadGovernedReportRun,
  getGovernedReportExecutionPolicy,
  getGovernedReportRuns,
  previewGovernedReport,
  runGovernedReport,
} from "./reportDefinitions.ts";

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("governed report execution transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("loads the execution policy and paged authorized history", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          revision: "local-report-execution-v2",
          scopeRevision: "local-report-scope-v1",
          executableRowPolicies: [
            "practice-wide",
            "facility-scoped",
            "patient-assigned",
          ],
          currentActorScope: {
            activeStaffLinked: true,
            staffId: 101,
            facilityId: 10,
            assignedPatientCount: 83,
          },
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({ runs: [], page: 2, pageSize: 5, total: 7 }),
      );

    await getGovernedReportExecutionPolicy("staff-session");
    await getGovernedReportRuns(
      "staff-session",
      "definition/unsafe",
      2,
      5,
    );

    expect(fetchMock.mock.calls[0]?.[0]).toContain(
      "/api/reports/execution-policy",
    );
    expect(fetchMock.mock.calls[1]?.[0]).toContain(
      "/definitions/definition%2Funsafe/runs?page=2&pageSize=5",
    );
    expect(fetchMock.mock.calls[0]?.[1]?.headers).toEqual(
      expect.objectContaining({ "X-Legacy EHR-Session": "staff-session" }),
    );
  });

  it("sends the same policy inputs to preview and execution", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          definitionId: "definition-1",
          revisionId: "revision-1",
          revisionNumber: 1,
          rows: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse(
          {
            run: { runId: "RPT-123", status: "completed" },
            events: [],
          },
          201,
        ),
      );
    const input = {
      purpose: "Approved patient list.",
      recipientUsername: "admin",
      deliveryMode: "local-download",
      asOfDate: "2026-06-18",
      parameters: { from: null, to: "2026-06-18" },
    };

    await previewGovernedReport("staff-session", "definition-1", input);
    await runGovernedReport("staff-session", "definition-1", {
      ...input,
      idempotencyKey: "report-run-transport-1",
    });

    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual(
      input,
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({
      ...input,
      idempotencyKey: "report-run-transport-1",
    });
    expect(fetchMock.mock.calls[0]?.[1]?.method).toBe("POST");
    expect(fetchMock.mock.calls[1]?.[1]?.method).toBe("POST");
  });

  it("downloads an authenticated run artifact as a blob", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("Identifier,Subject\n1,Example\n", {
        headers: { "content-type": "text/csv; charset=utf-8" },
      }),
    );

    const blob = await downloadGovernedReportRun(
      "staff-session",
      "RPT-abc/unsafe",
    );

    expect(fetchMock.mock.calls[0]?.[0]).toContain(
      "/api/reports/runs/RPT-abc%2Funsafe/download",
    );
    expect(fetchMock.mock.calls[0]?.[1]?.headers).toEqual(
      expect.objectContaining({ "X-Legacy EHR-Session": "staff-session" }),
    );
    expect(await blob.text()).toContain("Identifier,Subject");
  });
});
