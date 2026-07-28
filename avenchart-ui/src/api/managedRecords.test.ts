import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  actOnManagedRecord,
  createManagedRecord,
  getManagedRecords,
  type ManagedRecordItem,
} from "./managedRecords.ts";

const item: ManagedRecordItem = {
  intakeId: "11111111-1111-1111-1111-111111111111",
  documentId: null,
  patientId: "MOD-PAT-0001",
  legacyPid: 1000001,
  categoryId: 3,
  categoryName: "Medical Record",
  title: "Managed result",
  serviceDate: "2026-07-28",
  encounter: null,
  recordClass: "clinical-record",
  sourceType: "file-upload",
  authorName: "Synthetic Lab",
  facilityId: null,
  facilityName: null,
  sensitivity: "standard",
  languageTag: "en-US",
  fileName: "result.txt",
  mediaType: "text/plain",
  sizeBytes: 6,
  contentVersion: 1,
  contentChecksumSha256: "a".repeat(64),
  storageAdapter: "local-database-record-intake",
  storageReference: "record-intake/111/content/1",
  state: "captured",
  workflowVersion: 0,
  availabilityStatus: "withheld",
  validationStatus: "pending",
  validationAdapter: "local-structural-validator",
  antiMalwareVerified: false,
  failureReason: null,
  lastActor: "admin",
  lastActionAt: "2026-07-28T16:00:00Z",
  lastReason: "Capture",
  idempotentReplay: false,
  availableActions: ["quarantine", "reclassify"],
};

describe("managed record transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("serializes patient list and idempotent managed capture", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            revision: "local-record-control-v1",
            patientId: "MOD-PAT-0001",
            totalCount: 1,
            counts: {
              captured: 1,
              quarantined: 0,
              scanning: 0,
              failed: 0,
              available: 0,
              withheld: 1,
            },
            items: [item],
          }),
          { status: 200, headers: { "content-type": "application/json" } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({ idempotentReplay: false, intake: item }),
          { status: 201, headers: { "content-type": "application/json" } },
        ),
      );

    const list = await getManagedRecords(
      "staff-session",
      "MOD-PAT-0001",
    );
    const created = await createManagedRecord("staff-session", {
      patientId: "MOD-PAT-0001",
      categoryId: 3,
      title: "Managed result",
      serviceDate: "2026-07-28",
      encounter: null,
      recordClass: "clinical-record",
      sourceType: "file-upload",
      authorName: "Synthetic Lab",
      facilityId: null,
      sensitivity: "standard",
      languageTag: "en-US",
      fileName: "result.txt",
      mediaType: "text/plain",
      contentBase64: "cmVzdWx0",
      expectedChecksumSha256: "a".repeat(64),
      idempotencyKey: "test-id",
      reason: "Capture",
    });

    expect(list.counts.withheld).toBe(1);
    expect(created.intake.state).toBe("captured");
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "http://localhost:5001/api/records/?patientId=MOD-PAT-0001",
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual(
      expect.objectContaining({
        idempotencyKey: "test-id",
        expectedChecksumSha256: "a".repeat(64),
      }),
    );
  });

  it("sends the loaded workflow version for a state action", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          ...item,
          state: "quarantined",
          workflowVersion: 1,
          validationStatus: "queued",
        }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    );

    const result = await actOnManagedRecord(
      "staff-session",
      item.intakeId,
      "quarantine",
      0,
      "Hold outside chart",
    );

    expect(result.workflowVersion).toBe(1);
    expect(fetchMock).toHaveBeenCalledWith(
      `http://localhost:5001/api/records/${item.intakeId}/quarantine`,
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          expectedVersion: 0,
          reason: "Hold outside chart",
        }),
      }),
    );
  });
});
