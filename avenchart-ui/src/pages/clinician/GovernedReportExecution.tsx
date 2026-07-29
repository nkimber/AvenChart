import { useCallback, useEffect, useMemo, useState } from "react";
import {
  downloadGovernedReportRun,
  getGovernedReportCatalog,
  getGovernedReportDefinition,
  getGovernedReportExecutionPolicy,
  getGovernedReportRun,
  getGovernedReportRuns,
  previewGovernedReport,
  runGovernedReport,
  type GovernedReportDefinitionDetail,
  type GovernedReportExecutionInput,
  type GovernedReportExecutionPolicy,
  type GovernedReportPreview,
  type GovernedReportRunDetail,
  type GovernedReportRunList,
} from "../../api/reportDefinitions.ts";
import { showToast } from "../../components/Toast.tsx";

type Props = {
  sessionId: string;
  username: string;
};

const EMPTY_RUNS: GovernedReportRunList = {
  runs: [],
  page: 1,
  pageSize: 10,
  total: 0,
};

function formatInstant(value: string | null) {
  if (!value) return "-";
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function newIdempotencyKey() {
  const suffix =
    typeof globalThis.crypto?.randomUUID === "function"
      ? globalThis.crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `report-run-${suffix}`;
}

export default function GovernedReportExecution({
  sessionId,
  username,
}: Props) {
  const [policy, setPolicy] = useState<GovernedReportExecutionPolicy | null>(
    null,
  );
  const [catalog, setCatalog] = useState<
    Awaited<ReturnType<typeof getGovernedReportCatalog>>["definitions"]
  >([]);
  const [definitionId, setDefinitionId] = useState("");
  const [detail, setDetail] = useState<GovernedReportDefinitionDetail | null>(
    null,
  );
  const [runs, setRuns] = useState<GovernedReportRunList>(EMPTY_RUNS);
  const [runPage, setRunPage] = useState(1);
  const [recipient, setRecipient] = useState(username);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [preview, setPreview] = useState<GovernedReportPreview | null>(null);
  const [selectedRun, setSelectedRun] =
    useState<GovernedReportRunDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState("");

  const activeRevision = useMemo(
    () =>
      detail?.revisions.find(
        (revision) => revision.revisionId === detail.activeRevisionId,
      ) ?? null,
    [detail],
  );
  const policySupported =
    policy !== null &&
    activeRevision !== null &&
    policy.executableRowPolicies.includes(activeRevision.rowPolicy) &&
    (
      policy.rowPolicyFamilySupport[activeRevision.rowPolicy] ?? []
    ).includes(activeRevision.reportFamily);
  const executable =
    policySupported &&
    activeRevision !== null &&
    policy !== null &&
    (activeRevision.rowPolicy === "practice-wide" ||
      (activeRevision.rowPolicy === "facility-scoped" &&
        policy.currentActorScope.facilityId !== null) ||
      (activeRevision.rowPolicy === "patient-assigned" &&
        policy.currentActorScope.activeStaffLinked));
  const hasDateParameters =
    activeRevision?.parameterSchema.some(
      (parameter) => parameter.key === "from" || parameter.key === "to",
    ) ?? false;
  const recipients = useMemo(() => {
    if (!activeRevision) return [username];
    const values: string[] = [];
    if (activeRevision.allowedRecipients.includes("requesting-user")) {
      values.push(username);
    }
    if (activeRevision.allowedRecipients.includes("report-owner")) {
      values.push(activeRevision.ownerUsername);
    }
    return [...new Set(values)];
  }, [activeRevision, username]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    Promise.all([
      getGovernedReportExecutionPolicy(sessionId, controller.signal),
      getGovernedReportCatalog(sessionId, "", controller.signal),
    ])
      .then(([loadedPolicy, loadedCatalog]) => {
        setPolicy(loadedPolicy);
        setCatalog(loadedCatalog.definitions);
        setDefinitionId((current) => {
          if (
            current &&
            loadedCatalog.definitions.some(
              (definition) => definition.definitionId === current,
            )
          ) {
            return current;
          }
          return loadedCatalog.definitions[0]?.definitionId ?? "";
        });
        setToDate(loadedPolicy.requiredAsOfDate);
        setError("");
      })
      .catch((cause) => {
        if (controller.signal.aborted) return;
        setError(
          cause instanceof Error
            ? cause.message
            : "Could not load governed report execution.",
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [sessionId]);

  const refreshDefinition = useCallback(
    async (selectedDefinitionId: string, page: number) => {
      if (!selectedDefinitionId) {
        setDetail(null);
        setRuns(EMPTY_RUNS);
        return;
      }
      const [loadedDetail, loadedRuns] = await Promise.all([
        getGovernedReportDefinition(sessionId, selectedDefinitionId),
        getGovernedReportRuns(sessionId, selectedDefinitionId, page, 10),
      ]);
      setDetail(loadedDetail);
      setRuns(loadedRuns);
      const loadedActive =
        loadedDetail.revisions.find(
          (revision) => revision.revisionId === loadedDetail.activeRevisionId,
        ) ?? null;
      const availableRecipients = [
        ...(loadedActive?.allowedRecipients.includes("requesting-user")
          ? [username]
          : []),
        ...(loadedActive?.allowedRecipients.includes("report-owner")
          ? [loadedActive.ownerUsername]
          : []),
      ];
      setRecipient((current) =>
        availableRecipients.includes(current)
          ? current
          : (availableRecipients[0] ?? username),
      );
    },
    [sessionId, username],
  );

  useEffect(() => {
    setPreview(null);
    setSelectedRun(null);
    setError("");
    refreshDefinition(definitionId, runPage).catch((cause) =>
      setError(
        cause instanceof Error
          ? cause.message
          : "Could not load report run history.",
      ),
    );
  }, [definitionId, refreshDefinition, runPage]);

  function executionInput(): GovernedReportExecutionInput {
    if (!activeRevision || !policy) {
      throw new Error("Select an active governed definition.");
    }
    return {
      purpose: activeRevision.purpose,
      recipientUsername: recipient,
      deliveryMode: "local-download",
      asOfDate: policy.requiredAsOfDate,
      parameters: hasDateParameters
        ? { from: fromDate || null, to: toDate || null }
        : {},
    };
  }

  async function previewReport() {
    if (!definitionId || !executable) return;
    setWorking(true);
    setError("");
    try {
      const result = await previewGovernedReport(
        sessionId,
        definitionId,
        executionInput(),
      );
      setPreview(result);
      showToast("Governed preview completed without creating a run.", "success");
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : "Report preview failed.",
      );
    } finally {
      setWorking(false);
    }
  }

  async function executeReport() {
    if (!definitionId) return;
    setWorking(true);
    setError("");
    try {
      const result = await runGovernedReport(sessionId, definitionId, {
        ...executionInput(),
        idempotencyKey: newIdempotencyKey(),
      });
      setSelectedRun(result);
      await refreshDefinition(definitionId, 1);
      setRunPage(1);
      showToast(
        result.run.status === "completed"
          ? "Governed report artifact completed."
          : "The report failed closed and retained evidence.",
        result.run.status === "completed" ? "success" : "error",
      );
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Report run failed.");
    } finally {
      setWorking(false);
    }
  }

  async function inspectRun(runId: string) {
    setWorking(true);
    setError("");
    try {
      setSelectedRun(await getGovernedReportRun(sessionId, runId));
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : "Could not load run evidence.",
      );
    } finally {
      setWorking(false);
    }
  }

  async function downloadRun(runId: string, fileName: string | null) {
    setWorking(true);
    setError("");
    try {
      const blob = await downloadGovernedReportRun(sessionId, runId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName ?? `${runId}.csv`;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 0);
      setSelectedRun(await getGovernedReportRun(sessionId, runId));
      showToast("Governed report artifact downloaded.", "success");
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "Could not download the governed artifact.",
      );
    } finally {
      setWorking(false);
    }
  }

  return (
    <section className="cl-card report-execution-workspace">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Governed report execution</h2>
          <p className="cl-empty-text">
            Revision-pinned local preview, run, history, and download evidence.
            Facility and assigned-patient filters use the authenticated local
            staff relationship and retain their scope snapshot.
          </p>
        </div>
        {policy && (
          <span className="status-badge status-neutral">
            {policy.revision}
          </span>
        )}
      </div>

      {loading && <p>Loading governed execution policy...</p>}
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {!loading && policy && (
        <>
          <div className="warning-banner">
            <strong>Local boundary:</strong> practice, active staff facility,
            and provider/care-team patient policies are executable development
            mappings. Missing staff/facility links and unsupported
            patient-linked families fail with evidence and no artifact.
            External delivery and production artifact storage are disabled.
          </div>
          <dl className="report-definition-facts">
            <div>
              <dt>Dataset</dt>
              <dd>
                {policy.datasetId}@{policy.datasetVersion}
              </dd>
            </div>
            <div>
              <dt>Required as of</dt>
              <dd>{policy.requiredAsOfDate}</dd>
            </div>
            <div>
              <dt>Result limit</dt>
              <dd>{policy.maximumRows.toLocaleString()} rows</dd>
            </div>
            <div>
              <dt>Delivery</dt>
              <dd>Local download only</dd>
            </div>
            <div>
              <dt>Scope mapping</dt>
              <dd>{policy.scopeRevision}</dd>
            </div>
            <div>
              <dt>Current staff scope</dt>
              <dd>
                {policy.currentActorScope.activeStaffLinked
                  ? `staff ${policy.currentActorScope.staffId}${
                      policy.currentActorScope.facilityCode
                        ? ` / ${policy.currentActorScope.facilityCode}`
                        : ""
                    } / ${policy.currentActorScope.assignedPatientCount} assigned patients`
                  : "No active staff link"}
              </dd>
            </div>
          </dl>

          {catalog.length === 0 ? (
            <p className="cl-empty-text">
              Activate a governed report definition before creating a run.
            </p>
          ) : (
            <>
              <div className="report-execution-controls">
                <label className="cl-admin-field report-execution-wide">
                  <span>Active definition</span>
                  <select
                    className="ne-input"
                    value={definitionId}
                    onChange={(event) => {
                      setDefinitionId(event.target.value);
                      setRunPage(1);
                    }}
                    disabled={working}
                  >
                    {catalog.map((definition) => (
                      <option
                        key={definition.definitionId}
                        value={definition.definitionId}
                      >
                        {definition.title} - revision{" "}
                        {definition.activeRevisionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>Recipient</span>
                  <select
                    className="ne-input"
                    value={recipient}
                    onChange={(event) => setRecipient(event.target.value)}
                    disabled={working}
                  >
                    {recipients.map((value) => (
                      <option key={value} value={value}>
                        {value}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>As-of date</span>
                  <input
                    className="ne-input"
                    type="date"
                    value={policy.requiredAsOfDate}
                    readOnly
                  />
                </label>
                {hasDateParameters && (
                  <>
                    <label className="cl-admin-field">
                      <span>From date (optional)</span>
                      <input
                        className="ne-input"
                        type="date"
                        max={policy.requiredAsOfDate}
                        value={fromDate}
                        onChange={(event) => setFromDate(event.target.value)}
                        disabled={working}
                      />
                    </label>
                    <label className="cl-admin-field">
                      <span>To date</span>
                      <input
                        className="ne-input"
                        type="date"
                        max={policy.requiredAsOfDate}
                        value={toDate}
                        onChange={(event) => setToDate(event.target.value)}
                        disabled={working}
                      />
                    </label>
                  </>
                )}
              </div>

              {activeRevision && (
                <div className="report-execution-contract">
                  <p>
                    <strong>Purpose:</strong> {activeRevision.purpose}
                  </p>
                  <p>
                    <strong>Policy:</strong> {activeRevision.rowPolicy} /{" "}
                    {activeRevision.sensitivity} / revision{" "}
                    {activeRevision.revisionNumber}
                  </p>
                  {!executable && (
                    <p className="warning-banner">
                      {!policySupported
                        ? "This report family has no approved relationship for the selected row policy."
                        : "The current account lacks the active staff or facility relationship required by this row policy."}{" "}
                      Running it records a visible <strong>failed</strong>{" "}
                      attempt without creating an artifact.
                    </p>
                  )}
                </div>
              )}

              <div className="cl-inline-actions">
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={previewReport}
                  disabled={working || !executable}
                >
                  Preview {policy.previewRows} rows
                </button>
                <button
                  className="cl-btn-primary"
                  type="button"
                  onClick={executeReport}
                  disabled={working || !activeRevision}
                >
                  {working
                    ? "Working..."
                    : executable
                      ? "Run governed report"
                      : "Record blocked run"}
                </button>
              </div>
            </>
          )}

          {preview && (
            <section className="report-execution-result" aria-live="polite">
              <h3>Non-persistent preview</h3>
              <p className="cl-empty-text">
                {preview.totalRows.toLocaleString()} total rows / revision{" "}
                {preview.revisionNumber} / {preview.scopeRevision} /{" "}
                {preview.scopeSubjectCount?.toLocaleString() ?? "practice"}{" "}
                scoped patients / checksum{" "}
                <code>{preview.resultChecksum}</code>
              </p>
              <div
                className="table-scroll"
                tabIndex={0}
                role="region"
                aria-label="Governed report preview"
              >
                <table>
                  <thead>
                    <tr>
                      {preview.columns.map((column) => (
                        <th key={column}>{column}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((row, rowIndex) => (
                      <tr key={`${preview.resultChecksum}-${rowIndex}`}>
                        {row.map((value, columnIndex) => (
                          <td key={`${columnIndex}-${value}`}>{value}</td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {definitionId && (
            <section className="report-execution-result">
              <h3>Authorized run history</h3>
              {runs.runs.length === 0 ? (
                <p className="cl-empty-text">
                  No runs are visible to this user for the selected definition.
                </p>
              ) : (
                <div
                  className="table-scroll"
                  tabIndex={0}
                  role="region"
                  aria-label="Governed report run history"
                >
                  <table>
                    <thead>
                      <tr>
                        <th>Run</th>
                        <th>Status</th>
                        <th>Revision</th>
                        <th>Rows</th>
                        <th>Requested</th>
                        <th>Evidence</th>
                      </tr>
                    </thead>
                    <tbody>
                      {runs.runs.map((run) => (
                        <tr key={run.runId}>
                          <td>
                            <code>{run.runId}</code>
                          </td>
                          <td>
                            <span
                              className={`report-definition-status is-${run.status}`}
                            >
                              {run.status}
                            </span>
                            {run.failureCode && (
                              <span className="cl-field-help">
                                {run.failureCode}
                              </span>
                            )}
                          </td>
                          <td>{run.revisionNumber ?? "-"}</td>
                          <td>{run.rowCount.toLocaleString()}</td>
                          <td>{formatInstant(run.requestedAt)}</td>
                          <td>
                            <div className="cl-inline-actions">
                              <button
                                className="cl-btn-secondary cl-btn-sm"
                                type="button"
                                onClick={() => inspectRun(run.runId)}
                                disabled={working}
                              >
                                Inspect
                              </button>
                              {run.downloadAvailable && (
                                <button
                                  className="cl-btn-secondary cl-btn-sm"
                                  type="button"
                                  onClick={() =>
                                    downloadRun(
                                      run.runId,
                                      run.artifactFileName,
                                    )
                                  }
                                  disabled={working}
                                >
                                  Download
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {runs.total > runs.pageSize && (
                <div className="cl-pagination">
                  <button
                    className="cl-btn-secondary cl-btn-sm"
                    type="button"
                    disabled={runPage <= 1 || working}
                    onClick={() => setRunPage((page) => Math.max(1, page - 1))}
                  >
                    Previous
                  </button>
                  <span>
                    Page {runPage} of{" "}
                    {Math.max(1, Math.ceil(runs.total / runs.pageSize))}
                  </span>
                  <button
                    className="cl-btn-secondary cl-btn-sm"
                    type="button"
                    disabled={
                      runPage * runs.pageSize >= runs.total || working
                    }
                    onClick={() => setRunPage((page) => page + 1)}
                  >
                    Next
                  </button>
                </div>
              )}
            </section>
          )}

          {selectedRun && (
            <section className="report-execution-result" aria-live="polite">
              <h3>Run evidence</h3>
              <dl className="report-definition-facts">
                <div>
                  <dt>Run</dt>
                  <dd>
                    <code>{selectedRun.run.runId}</code>
                  </dd>
                </div>
                <div>
                  <dt>Definition</dt>
                  <dd>
                    {selectedRun.run.definitionStableKey} revision{" "}
                    {selectedRun.run.revisionNumber}
                  </dd>
                </div>
                <div>
                  <dt>Dataset</dt>
                  <dd>
                    {selectedRun.run.datasetId}@
                    {selectedRun.run.datasetVersion}
                  </dd>
                </div>
                <div>
                  <dt>Checksum</dt>
                  <dd>
                    <code>{selectedRun.run.resultChecksum ?? "-"}</code>
                  </dd>
                </div>
                <div>
                  <dt>Scope</dt>
                  <dd>
                    {selectedRun.run.scopeRevision} /{" "}
                    {selectedRun.run.scopeFacilityId === null
                      ? "no facility pin"
                      : `facility ${selectedRun.run.scopeFacilityId}`}{" "}
                    /{" "}
                    {selectedRun.run.scopeSubjectCount?.toLocaleString() ??
                      "unknown"}{" "}
                    patients
                  </dd>
                </div>
                <div>
                  <dt>Scope checksum</dt>
                  <dd>
                    <code>{selectedRun.run.scopeSnapshotChecksum || "-"}</code>
                  </dd>
                </div>
              </dl>
              {selectedRun.run.failureMessage && (
                <div className="error-banner">
                  {selectedRun.run.failureMessage}
                </div>
              )}
              <div
                className="table-scroll"
                tabIndex={0}
                role="region"
                aria-label="Governed report run events"
              >
                <table>
                  <thead>
                    <tr>
                      <th>Action</th>
                      <th>State</th>
                      <th>Actor</th>
                      <th>Reason</th>
                      <th>When</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedRun.events.map((event) => (
                      <tr key={event.eventId}>
                        <td>{event.action}</td>
                        <td>
                          {event.fromStatus ?? "-"} to {event.toStatus}
                        </td>
                        <td>{event.actorUsername}</td>
                        <td>{event.reason}</td>
                        <td>{formatInstant(event.occurredAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </>
      )}
    </section>
  );
}
