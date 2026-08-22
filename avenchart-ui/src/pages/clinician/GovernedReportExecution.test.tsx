// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getGovernedReportCatalog,
  getGovernedReportDefinition,
  getGovernedReportExecutionPolicy,
  getGovernedReportRun,
  getGovernedReportRuns,
  runGovernedReport,
} from "../../api/reportDefinitions.ts";
import GovernedReportExecution from "./GovernedReportExecution.tsx";

vi.mock("../../api/reportDefinitions.ts", () => ({
  cancelGovernedReportRun: vi.fn(),
  downloadGovernedReportRun: vi.fn(),
  getGovernedReportCatalog: vi.fn(),
  getGovernedReportDefinition: vi.fn(),
  getGovernedReportExecutionPolicy: vi.fn(),
  getGovernedReportRun: vi.fn(),
  getGovernedReportRuns: vi.fn(),
  previewGovernedReport: vi.fn(),
  retryGovernedReportRun: vi.fn(),
  runGovernedReport: vi.fn(),
}));

vi.mock("../../components/Toast.tsx", () => ({ showToast: vi.fn() }));

const run = {
  runId: "RPT-11111111111111111111111111111111",
  definitionId: "11111111-1111-1111-1111-111111111111",
  revisionId: "22222222-2222-2222-2222-222222222222",
  revisionNumber: 1,
  definitionStableKey: "synthetic-report",
  definitionTitle: "Synthetic report",
  reportFamily: "appointments",
  status: "queued",
  requestedBy: "alice",
  recipientUsername: "alice",
  purpose: "Synthetic report validation.",
  rowPolicy: "facility-scoped",
  asOfDate: "2026-08-22",
  normalizedParameters: {},
  datasetId: "synthetic",
  datasetVersion: "v1",
  executionRevision: "v1",
  scopeRevision: "v1",
  formReportingRevision: "not-applicable",
  queueRevision: "v1",
  scopeSnapshotChecksum: "checksum",
  scopeFacilityId: 1,
  scopeSubjectCount: 1,
  definitionSnapshotChecksum: "definition-checksum",
  lifecycleVersion: 0,
  attemptCount: 0,
  maxAttempts: 3,
  manualRetryCount: 0,
  nextAttemptAt: null,
  lastAttemptAt: null,
  leaseExpiresAt: null,
  queueExpiresAt: null,
  cancelRequestedAt: null,
  cancelRequestedBy: null,
  cancelReason: null,
  requestedAt: "2026-08-22T12:00:00Z",
  startedAt: null,
  finishedAt: null,
  durationMs: null,
  rowCount: 0,
  resultChecksum: null,
  artifactBytes: 0,
  artifactContentType: null,
  artifactFileName: null,
  artifactExpiresAt: null,
  artifactExpiredAt: null,
  failureCode: null,
  failureMessage: null,
  failureRetryable: null,
  downloadAvailable: false,
  canCancel: true,
  canRetry: false,
  replay: false,
};

const runDetail = { run, events: [] };

describe("GovernedReportExecution", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getGovernedReportExecutionPolicy).mockResolvedValue({
      revision: "v1",
      definitionRevision: "v1",
      scopeRevision: "v1",
      formReportingRevision: "not-applicable",
      queueRevision: "v1",
      datasetId: "synthetic",
      datasetVersion: "v1",
      requiredAsOfDate: "2026-08-22",
      runStates: ["queued", "running", "completed"],
      executableRowPolicies: ["facility-scoped"],
      rowPolicyFamilySupport: { "facility-scoped": ["appointments"] },
      scopeSources: [],
      currentActorScope: {
        username: "alice",
        activeStaffLinked: true,
        staffId: 1,
        facilityId: 1,
        facilityCode: "MAIN",
        assignedPatientCount: 1,
      },
      operatorAccess: false,
      deliveryModes: ["local-download"],
      maximumDateSpanDays: 365,
      maximumRows: 100,
      previewRows: 10,
      durableQueueEnabled: true,
      enqueueDelayMilliseconds: 1,
      pollIntervalMilliseconds: 100,
      leaseSeconds: 30,
      executionTimeoutSeconds: 30,
      queueExpirationMinutes: 30,
      maximumAttempts: 3,
      retryBaseDelaySeconds: 1,
      definitionRetentionEnforcedLocally: true,
      retryableFailureCodes: [],
      externalDeliveryEnabled: false,
      artifactStorageProductionApproved: false,
      productionBlockers: [],
    });
    vi.mocked(getGovernedReportCatalog).mockResolvedValue({
      definitions: [
        {
          definitionId: run.definitionId,
          title: "Synthetic report",
          activeRevisionNumber: 1,
        },
      ],
      page: 1,
      pageSize: 10,
      total: 1,
    } as never);
    vi.mocked(getGovernedReportDefinition).mockResolvedValue({
      definitionId: run.definitionId,
      activeRevisionId: run.revisionId,
      revisions: [
        {
          revisionId: run.revisionId,
          revisionNumber: 1,
          purpose: run.purpose,
          rowPolicy: run.rowPolicy,
          reportFamily: run.reportFamily,
          sensitivity: "restricted",
          allowedRecipients: ["requesting-user"],
          ownerUsername: "alice",
          parameterSchema: [],
        },
      ],
      events: [],
    } as never);
    vi.mocked(getGovernedReportRuns).mockResolvedValue({
      runs: [run],
      page: 1,
      pageSize: 10,
      total: 1,
    });
    vi.mocked(runGovernedReport).mockResolvedValue(runDetail);
  });

  it("does not leave a queued run permanently stale after one polling failure", async () => {
    const user = userEvent.setup();
    vi.mocked(getGovernedReportRun)
      .mockRejectedValueOnce(new Error("Temporary service outage."))
      .mockResolvedValue(runDetail);

    render(<GovernedReportExecution sessionId="staff-session" username="alice" />);

    await user.click(
      await screen.findByRole("button", { name: "Run governed report" }),
    );

    expect(
      await screen.findByText("Run evidence may be stale."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Retry refresh now" }));

    await waitFor(() =>
      expect(
        screen.queryByText("Run evidence may be stale."),
      ).not.toBeInTheDocument(),
    );
    expect(getGovernedReportRun).toHaveBeenCalledTimes(2);
  });
});
