import { useEffect, useState } from "react";
import {
  getGovernedReportOperations,
  getGovernedReportOperationsRun,
  type GovernedReportOperationsFilters,
  type GovernedReportOperationsResponse,
  type GovernedReportRunDetail,
} from "../../api/reportDefinitions.ts";

type Props = {
  sessionId: string;
  username: string;
};

const EMPTY_FILTERS: GovernedReportOperationsFilters = {
  search: "",
  status: "",
  family: "",
  requestedBy: "",
  attentionOnly: false,
  from: "",
  to: "",
};

function formatInstant(value: string | null) {
  if (!value) return "-";
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatDuration(value: number | null) {
  if (value === null) return "-";
  if (value < 1000) return `${value} ms`;
  return `${(value / 1000).toFixed(1)} s`;
}

export default function GovernedReportOperations({
  sessionId,
  username,
}: Props) {
  const [draftFilters, setDraftFilters] =
    useState<GovernedReportOperationsFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] =
    useState<GovernedReportOperationsFilters>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [data, setData] =
    useState<GovernedReportOperationsResponse | null>(null);
  const [selectedRun, setSelectedRun] =
    useState<GovernedReportRunDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [inspecting, setInspecting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    let timeoutId: number | undefined;

    async function refresh(initial: boolean) {
      let nextPollSeconds = 5;
      if (initial) setLoading(true);
      try {
        const response = await getGovernedReportOperations(
          sessionId,
          { ...appliedFilters, page, pageSize: 20 },
          controller.signal,
        );
        if (controller.signal.aborted) return;
        setData(response);
        setError("");
        nextPollSeconds = Math.max(1, response.pollIntervalSeconds);
      } catch (cause) {
        if (controller.signal.aborted) return;
        setError(
          cause instanceof Error
            ? cause.message
            : "Could not load report operations.",
        );
      } finally {
        if (!controller.signal.aborted) {
          if (initial) setLoading(false);
          timeoutId = window.setTimeout(
            () => void refresh(false),
            nextPollSeconds * 1000,
          );
        }
      }
    }

    void refresh(true);
    return () => {
      controller.abort();
      if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    };
  }, [appliedFilters, page, refreshVersion, sessionId]);

  async function inspectRun(runId: string) {
    setInspecting(true);
    setError("");
    try {
      setSelectedRun(
        await getGovernedReportOperationsRun(sessionId, runId),
      );
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "Could not load operator run evidence.",
      );
    } finally {
      setInspecting(false);
    }
  }

  function applyFilters() {
    setPage(1);
    setSelectedRun(null);
    setAppliedFilters({ ...draftFilters });
  }

  function clearFilters() {
    setPage(1);
    setSelectedRun(null);
    setDraftFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
  }

  return (
    <section
      className="report-execution-result report-operations-workspace"
      aria-labelledby="report-operations-title"
    >
      <div className="cl-card-header">
        <div>
          <h3 id="report-operations-title">Report operations</h3>
          <p className="cl-empty-text">
            Read-only cross-definition run discovery, failure triage, and
            local worker-health evidence for {username}. This surface does not
            delegate requester cancellation, retry, or artifact access.
          </p>
        </div>
        {data && (
          <span
            className={`status-badge ${
              data.health === "healthy" ? "status-active" : "status-warning"
            }`}
          >
            {data.revision} / {data.health}
          </span>
        )}
      </div>

      {loading && <p>Loading report operations...</p>}
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {data && (
        <>
          <div className="warning-banner">
            <strong>Local operations boundary:</strong> this PostgreSQL
            projection refreshes every {data.pollIntervalSeconds} seconds.
            It is not production-approved monitoring, paging, incident
            management, or an authorization override.
          </div>

          <dl className="report-definition-facts">
            <div>
              <dt>Total runs</dt>
              <dd>{data.summary.totalRuns.toLocaleString()}</dd>
            </div>
            <div>
              <dt>Queued / running</dt>
              <dd>
                {data.summary.statusCounts.queued ?? 0} /{" "}
                {data.summary.statusCounts.running ?? 0}
              </dd>
            </div>
            <div>
              <dt>Ready / delayed</dt>
              <dd>
                {data.summary.queuedReady} / {data.summary.queuedDelayed}
              </dd>
            </div>
            <div>
              <dt>Retryable / permanent</dt>
              <dd>
                {data.summary.retryableFailures} /{" "}
                {data.summary.permanentFailures}
              </dd>
            </div>
            <div>
              <dt>Queue / artifact expired</dt>
              <dd>
                {data.summary.queueExpired} / {data.summary.artifactExpired}
              </dd>
            </div>
            <div>
              <dt>Completed / failed (24h)</dt>
              <dd>
                {data.summary.completedLast24Hours} /{" "}
                {data.summary.failedLast24Hours}
              </dd>
            </div>
            <div>
              <dt>P95 completed duration</dt>
              <dd>{formatDuration(data.summary.p95CompletedDurationMs)}</dd>
            </div>
            <div>
              <dt>Generated</dt>
              <dd>{formatInstant(data.generatedAt)}</dd>
            </div>
          </dl>

          <section aria-labelledby="report-operations-alerts-title">
            <h4 id="report-operations-alerts-title">Local alert signals</h4>
            {data.alerts.length === 0 ? (
              <p className="cl-empty-text">
                No local attention condition is currently active.
              </p>
            ) : (
              <div
                className="table-scroll"
                tabIndex={0}
                role="region"
                aria-label="Governed report operations alerts"
              >
                <table>
                  <thead>
                    <tr>
                      <th>Severity</th>
                      <th>Signal</th>
                      <th>Count</th>
                      <th>Meaning</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.alerts.map((alert) => (
                      <tr key={alert.code}>
                        <td>{alert.severity}</td>
                        <td>
                          <code>{alert.code}</code>
                        </td>
                        <td>{alert.count}</td>
                        <td>{alert.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <div className="report-execution-controls">
            <label className="cl-admin-field report-execution-wide">
              <span>Search runs</span>
              <input
                className="ne-input"
                value={draftFilters.search ?? ""}
                maxLength={100}
                placeholder="Run ID, definition, title, or failure code"
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    search: event.target.value,
                  }))
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Status</span>
              <select
                className="ne-input"
                value={draftFilters.status ?? ""}
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    status: event.target.value,
                  }))
                }
              >
                <option value="">All states</option>
                {data.statuses.map((status) => (
                  <option key={status} value={status}>
                    {status}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Family</span>
              <select
                className="ne-input"
                value={draftFilters.family ?? ""}
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    family: event.target.value,
                  }))
                }
              >
                <option value="">All families</option>
                {data.families.map((family) => (
                  <option key={family} value={family}>
                    {family}
                  </option>
                ))}
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Requester (exact)</span>
              <input
                className="ne-input"
                value={draftFilters.requestedBy ?? ""}
                maxLength={100}
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    requestedBy: event.target.value,
                  }))
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Requested from</span>
              <input
                className="ne-input"
                type="date"
                value={draftFilters.from ?? ""}
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    from: event.target.value,
                  }))
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Requested through</span>
              <input
                className="ne-input"
                type="date"
                value={draftFilters.to ?? ""}
                onChange={(event) =>
                  setDraftFilters((current) => ({
                    ...current,
                    to: event.target.value,
                  }))
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Queue view</span>
              <span className="cl-checkbox-row">
                <input
                  type="checkbox"
                  checked={draftFilters.attentionOnly ?? false}
                  onChange={(event) =>
                    setDraftFilters((current) => ({
                      ...current,
                      attentionOnly: event.target.checked,
                    }))
                  }
                />
                Needs attention only
              </span>
            </label>
          </div>

          <div className="cl-inline-actions">
            <button
              className="cl-btn-primary cl-btn-sm"
              type="button"
              onClick={applyFilters}
            >
              Apply operations filters
            </button>
            <button
              className="cl-btn-secondary cl-btn-sm"
              type="button"
              onClick={clearFilters}
            >
              Clear filters
            </button>
            <button
              className="cl-btn-secondary cl-btn-sm"
              type="button"
              onClick={() => setRefreshVersion((version) => version + 1)}
            >
              Refresh operations
            </button>
          </div>

          <h4>Operator run queue</h4>
          {data.runs.length === 0 ? (
            <p className="cl-empty-text">
              No report runs match the applied operations filters.
            </p>
          ) : (
            <div
              className="table-scroll"
              tabIndex={0}
              role="region"
              aria-label="Governed report operator run queue"
            >
              <table>
                <thead>
                  <tr>
                    <th>Run</th>
                    <th>Definition</th>
                    <th>Status</th>
                    <th>Requester / recipient</th>
                    <th>Attempts</th>
                    <th>Requested</th>
                    <th>Evidence</th>
                  </tr>
                </thead>
                <tbody>
                  {data.runs.map((run) => (
                    <tr key={run.runId}>
                      <td>
                        <code>{run.runId}</code>
                      </td>
                      <td>
                        {run.definitionTitle}
                        <span className="cl-field-help">
                          {run.reportFamily} / revision{" "}
                          {run.revisionNumber ?? "-"}
                        </span>
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
                      <td>
                        {run.requestedBy}
                        <span className="cl-field-help">
                          to {run.recipientUsername}
                        </span>
                      </td>
                      <td>
                        {run.attemptCount}/{run.maxAttempts}
                      </td>
                      <td>{formatInstant(run.requestedAt)}</td>
                      <td>
                        <button
                          className="cl-btn-secondary cl-btn-sm"
                          type="button"
                          disabled={inspecting}
                          onClick={() => inspectRun(run.runId)}
                        >
                          Inspect operations evidence
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {data.total > data.pageSize && (
            <div className="cl-pagination">
              <button
                className="cl-btn-secondary cl-btn-sm"
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </button>
              <span>
                Page {page} of{" "}
                {Math.max(1, Math.ceil(data.total / data.pageSize))}
              </span>
              <button
                className="cl-btn-secondary cl-btn-sm"
                type="button"
                disabled={page * data.pageSize >= data.total}
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </button>
            </div>
          )}

          {selectedRun && (
            <section aria-live="polite">
              <h4>Operator evidence</h4>
              <p className="cl-empty-text">
                <code>{selectedRun.run.runId}</code> /{" "}
                {selectedRun.run.status} / requested by{" "}
                {selectedRun.run.requestedBy}. Requester lifecycle controls
                and artifact download remain unavailable here.
              </p>
              {selectedRun.run.failureMessage && (
                <div className="error-banner">
                  {selectedRun.run.failureMessage}
                </div>
              )}
              <div
                className="table-scroll"
                tabIndex={0}
                role="region"
                aria-label="Governed report operator run events"
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

          <details>
            <summary>Production operations blockers</summary>
            <ul>
              {data.productionBlockers.map((blocker) => (
                <li key={blocker}>{blocker}</li>
              ))}
            </ul>
          </details>
        </>
      )}
    </section>
  );
}
