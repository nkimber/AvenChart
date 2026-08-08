// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useMemo, useState } from "react";
import {
  createGovernedReportDefinition,
  createGovernedReportRevision,
  getGovernedReportCatalog,
  getGovernedReportDefinition,
  getGovernedReportDefinitions,
  getReportDefinitionPolicy,
  transitionGovernedReportDefinition,
  type GovernedReportDefinitionDetail,
  type GovernedReportDefinitionList,
  type GovernedReportDefinitionRevision,
  type ReportDefinitionGovernancePolicy,
} from "../../api/reportDefinitions.ts";
import { showToast } from "../../components/Toast.tsx";

type Props = {
  sessionId: string;
  username: string;
};

type FormState = {
  stableKey: string;
  title: string;
  ownerUsername: string;
  purpose: string;
  reportFamily: string;
  sensitivity: string;
  rowPolicy: string;
  retentionDays: string;
  reason: string;
};

const EMPTY_LIST: GovernedReportDefinitionList = {
  definitions: [],
  page: 1,
  pageSize: 10,
  total: 0,
};

function initialForm(username: string): FormState {
  return {
    stableKey: "",
    title: "",
    ownerUsername: username,
    purpose: "",
    reportFamily: "operational",
    sensitivity: "restricted",
    rowPolicy: "practice-wide",
    retentionDays: "30",
    reason: "",
  };
}

function formatInstant(value: string | null) {
  if (!value) return "Not set";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString(undefined, {
        dateStyle: "medium",
        timeStyle: "short",
      });
}

function nextActions(status: string) {
  switch (status) {
    case "draft":
      return ["review", "retire"];
    case "reviewed":
      return ["approve", "retire"];
    case "approved":
      return ["activate", "retire"];
    case "active":
      return ["suspend", "retire"];
    case "suspended":
      return ["activate", "retire"];
    default:
      return [];
  }
}

function DefinitionStatus({ status }: { status: string }) {
  return (
    <span className={`report-definition-status is-${status}`}>
      {status.replace("-", " ")}
    </span>
  );
}

function RevisionContract({
  revision,
}: {
  revision: Pick<
    GovernedReportDefinitionRevision,
    | "metricDictionary"
    | "parameterSchema"
    | "sourceDatasets"
    | "outputSchema"
  >;
}) {
  return (
    <div className="report-contract-grid">
      <div>
        <h4>Metric dictionary</h4>
        <ul className="report-contract-list">
          {revision.metricDictionary.map((metric) => (
            <li key={metric.key}>
              <strong>{metric.label}</strong>
              <span>{metric.definition}</span>
              <code>{metric.sourceField}</code>
            </li>
          ))}
        </ul>
      </div>
      <div>
        <h4>Bounded parameters</h4>
        {revision.parameterSchema.length === 0 ? (
          <p className="cl-empty-text">This family accepts no parameters.</p>
        ) : (
          <ul className="report-contract-list">
            {revision.parameterSchema.map((parameter) => (
              <li key={parameter.key}>
                <strong>{parameter.label}</strong>
                <span>
                  {parameter.type}
                  {parameter.maxSpanDays
                    ? ` · maximum ${parameter.maxSpanDays} days`
                    : ""}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div>
        <h4>Source datasets</h4>
        <ul className="report-contract-list">
          {revision.sourceDatasets.map((dataset) => (
            <li key={dataset.key}>
              <strong>{dataset.key}</strong>
              <span>{dataset.description}</span>
              <code>{dataset.fields.join(", ")}</code>
            </li>
          ))}
        </ul>
      </div>
      <div>
        <h4>Output schema</h4>
        <ul className="report-contract-list">
          {revision.outputSchema.map((field) => (
            <li key={field.key}>
              <strong>{field.label}</strong>
              <span>
                {field.type} · {field.sensitivity}
              </span>
              <code>{field.key}</code>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

export default function GovernedReportDefinitions({
  sessionId,
  username,
}: Props) {
  const [policy, setPolicy] =
    useState<ReportDefinitionGovernancePolicy | null>(null);
  const [definitions, setDefinitions] =
    useState<GovernedReportDefinitionList>(EMPTY_LIST);
  const [catalog, setCatalog] =
    useState<GovernedReportDefinitionList>(EMPTY_LIST);
  const [detail, setDetail] =
    useState<GovernedReportDefinitionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [detailError, setDetailError] = useState("");
  const [mutationError, setMutationError] = useState("");
  const [mutating, setMutating] = useState(false);
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [form, setForm] = useState<FormState>(() => initialForm(username));
  const [successorDefinitionId, setSuccessorDefinitionId] = useState<
    string | null
  >(null);
  const [actionReason, setActionReason] = useState("");

  async function loadWorkspace() {
    setLoading(true);
    setLoadError("");
    try {
      const [loadedPolicy, loadedDefinitions, loadedCatalog] =
        await Promise.all([
          getReportDefinitionPolicy(sessionId),
          getGovernedReportDefinitions(sessionId, {
            search: appliedSearch,
            status,
            page,
            pageSize: 10,
          }),
          getGovernedReportCatalog(sessionId),
        ]);
      setPolicy(loadedPolicy);
      setDefinitions(loadedDefinitions);
      setCatalog(loadedCatalog);
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Could not load report-definition governance.",
      );
    } finally {
      setLoading(false);
    }
  }

  const loadWorkspaceEvent = useEffectEvent(loadWorkspace);
  useEffect(() => {
    void loadWorkspaceEvent();
  }, [sessionId, appliedSearch, status, page]);

  const selectedFamily = useMemo(
    () =>
      policy?.families.find((family) => family.key === form.reportFamily) ??
      null,
    [policy, form.reportFamily],
  );

  const latestRevision = detail?.revisions[0] ?? null;
  const canCreateSuccessor =
    latestRevision?.legacyReviewRequired ||
    latestRevision?.status === "active" ||
    latestRevision?.status === "suspended";

  async function openDefinition(definitionId: string) {
    setDetailError("");
    try {
      setDetail(
        await getGovernedReportDefinition(sessionId, definitionId),
      );
      setActionReason("");
    } catch (error) {
      setDetailError(
        error instanceof Error
          ? error.message
          : "Could not load report-definition evidence.",
      );
    }
  }

  function updateForm<K extends keyof FormState>(
    key: K,
    value: FormState[K],
  ) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function resetForm() {
    setForm(initialForm(username));
    setSuccessorDefinitionId(null);
    setMutationError("");
  }

  function prepareSuccessor() {
    if (!detail || !latestRevision) return;
    setSuccessorDefinitionId(detail.definitionId);
    setForm({
      stableKey: detail.stableKey,
      title: latestRevision.title,
      ownerUsername: latestRevision.ownerUsername,
      purpose: latestRevision.purpose,
      reportFamily: latestRevision.reportFamily,
      sensitivity: latestRevision.legacyReviewRequired
        ? "restricted"
        : latestRevision.sensitivity,
      rowPolicy: latestRevision.legacyReviewRequired
        ? "practice-wide"
        : latestRevision.rowPolicy,
      retentionDays: String(latestRevision.retentionDays ?? 30),
      reason: latestRevision.legacyReviewRequired
        ? "Replace the migrated legacy draft with a complete governed revision."
        : `Create a successor to revision ${latestRevision.revisionNumber}.`,
    });
    document
      .getElementById("report-definition-editor")
      ?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function submitDefinition(event: React.FormEvent) {
    event.preventDefault();
    if (!policy || !selectedFamily) return;
    const retentionDays = Number(form.retentionDays);
    setMutating(true);
    setMutationError("");
    try {
      const input = {
        title: form.title,
        ownerUsername: form.ownerUsername,
        purpose: form.purpose,
        reportFamily: form.reportFamily,
        sensitivity: form.sensitivity,
        rowPolicy: form.rowPolicy,
        retentionDays,
        allowedRecipients: ["requesting-user"],
        deliveryModes: ["local-download"],
        reason: form.reason,
      };
      const created =
        successorDefinitionId && latestRevision
          ? await createGovernedReportRevision(
              sessionId,
              successorDefinitionId,
              {
                ...input,
                expectedLatestRevisionNumber:
                  latestRevision.revisionNumber,
              },
            )
          : await createGovernedReportDefinition(sessionId, {
              stableKey: form.stableKey,
              ...input,
            });
      setDetail(created);
      resetForm();
      await loadWorkspace();
      showToast(
        successorDefinitionId
          ? "Successor report revision created."
          : "Governed report definition created.",
        "success",
      );
    } catch (error) {
      setMutationError(
        error instanceof Error
          ? error.message
          : "Could not save the report definition.",
      );
    } finally {
      setMutating(false);
    }
  }

  async function transition(action: string) {
    if (!detail || !latestRevision) return;
    setMutating(true);
    setMutationError("");
    try {
      const transitioned = await transitionGovernedReportDefinition(
        sessionId,
        detail.definitionId,
        action,
        latestRevision.version,
        actionReason,
      );
      setDetail(transitioned);
      setActionReason("");
      await loadWorkspace();
      showToast(`Report definition moved to ${action}.`, "success");
    } catch (error) {
      setMutationError(
        error instanceof Error
          ? error.message
          : "Could not transition the report definition.",
      );
    } finally {
      setMutating(false);
    }
  }

  return (
    <>
      <section className="cl-card report-governance-hero">
        <div>
          <span className="report-governance-eyebrow">REP-01 foundation</span>
          <h2 className="cl-card-title">Governed report catalog</h2>
          <p>
            Definitions snapshot a stable family, owner, purpose, data
            dictionary, bounded parameters, source/output schema, sensitivity,
            row policy, retention, recipients, local delivery mode, and
            validation fixture.
          </p>
        </div>
        <div className="report-governance-boundary" role="note">
          <strong>Local definition governance only</strong>
          <span>
            Raw SQL, executable templates, external delivery, and unbounded
            custom parameters are rejected.
          </span>
          <span>
            Row-policy enforcement at execution and reproducible run artifacts
            remain REP-02 work.
          </span>
        </div>
      </section>

      {loading && (
        <section className="cl-card" aria-live="polite">
          <div className="skeleton-list">
            {[0, 1, 2].map((item) => (
              <div className="skeleton-row" key={item} />
            ))}
          </div>
        </section>
      )}
      {loadError && (
        <section className="cl-card">
          <div className="error-banner" role="alert">
            {loadError}
          </div>
          <button
            className="cl-btn-secondary"
            onClick={() => void loadWorkspace()}
            type="button"
          >
            Retry governance load
          </button>
        </section>
      )}

      {policy && !loading && (
        <>
          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h3 className="cl-card-title">Policy boundary</h3>
                <p className="cl-empty-text">
                  Revision {policy.revision} · {policy.families.length} curated
                  families · retention {policy.minimumRetentionDays}–
                  {policy.maximumRetentionDays} days
                </p>
              </div>
            </div>
            <div className="report-policy-flags">
              <span>Raw SQL: rejected</span>
              <span>Executable templates: rejected</span>
              <span>External delivery: disabled</span>
              <span>Execution row policy: pending REP-02</span>
            </div>
            <details>
              <summary>
                Production blockers ({policy.productionBlockers.length})
              </summary>
              <ol className="report-blocker-list">
                {policy.productionBlockers.map((blocker) => (
                  <li key={blocker}>{blocker}</li>
                ))}
              </ol>
            </details>
          </section>

          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h3 className="cl-card-title">Active accessible catalog</h3>
                <p className="cl-empty-text">
                  Draft, reviewed, approved, suspended, retired, and migrated
                  legacy rows never appear here.
                </p>
              </div>
              <span className="report-catalog-count">
                {catalog.total} active
              </span>
            </div>
            {catalog.definitions.length === 0 ? (
              <div className="cl-empty-state">
                No report definition has completed owner review, approval, and
                activation.
              </div>
            ) : (
              <div
                className="cl-table-wrap"
                role="region"
                aria-label="Active governed report catalog"
                tabIndex={0}
              >
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Definition</th>
                      <th>Family</th>
                      <th>Owner</th>
                      <th>Policy</th>
                      <th>Revision</th>
                    </tr>
                  </thead>
                  <tbody>
                    {catalog.definitions.map((definition) => (
                      <tr key={definition.definitionId}>
                        <td>
                          <button
                            className="report-definition-link"
                            onClick={() =>
                              void openDefinition(definition.definitionId)
                            }
                            type="button"
                          >
                            {definition.title}
                          </button>
                          <code>{definition.stableKey}</code>
                        </td>
                        <td>{definition.reportFamily}</td>
                        <td>{definition.ownerUsername}</td>
                        <td>
                          {definition.sensitivity} · {definition.rowPolicy}
                        </td>
                        <td>v{definition.activeRevisionNumber}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="cl-card" id="report-definition-editor">
            <div className="cl-card-header">
              <div>
                <h3 className="cl-card-title">
                  {successorDefinitionId
                    ? "Create immutable successor revision"
                    : "Create governed definition"}
                </h3>
                <p className="cl-empty-text">
                  Family contracts are server-owned snapshots; this editor
                  accepts no query or executable template content.
                </p>
              </div>
              {successorDefinitionId && (
                <button
                  className="cl-btn-secondary"
                  onClick={resetForm}
                  type="button"
                >
                  Cancel successor
                </button>
              )}
            </div>
            <form className="report-definition-form" onSubmit={submitDefinition}>
              <label className="cl-admin-field">
                <span>Stable key</span>
                <input
                  className="ne-input"
                  disabled={Boolean(successorDefinitionId)}
                  maxLength={80}
                  onChange={(event) =>
                    updateForm("stableKey", event.target.value)
                  }
                  pattern="[a-z][a-z0-9._-]{2,79}"
                  placeholder="operations.daily-appointments"
                  required
                  value={form.stableKey}
                />
              </label>
              <label className="cl-admin-field">
                <span>Title</span>
                <input
                  className="ne-input"
                  maxLength={120}
                  minLength={3}
                  onChange={(event) => updateForm("title", event.target.value)}
                  required
                  value={form.title}
                />
              </label>
              <label className="cl-admin-field">
                <span>Owner username</span>
                <input
                  className="ne-input"
                  maxLength={80}
                  onChange={(event) =>
                    updateForm("ownerUsername", event.target.value)
                  }
                  required
                  value={form.ownerUsername}
                />
              </label>
              <label className="cl-admin-field">
                <span>Curated family</span>
                <select
                  className="ne-input"
                  onChange={(event) =>
                    updateForm("reportFamily", event.target.value)
                  }
                  value={form.reportFamily}
                >
                  {policy.families.map((family) => (
                    <option key={family.key} value={family.key}>
                      {family.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field report-definition-wide">
                <span>Permitted purpose</span>
                <textarea
                  className="ne-input"
                  maxLength={500}
                  minLength={20}
                  onChange={(event) =>
                    updateForm("purpose", event.target.value)
                  }
                  required
                  rows={3}
                  value={form.purpose}
                />
              </label>
              <label className="cl-admin-field">
                <span>Sensitivity</span>
                <select
                  className="ne-input"
                  onChange={(event) =>
                    updateForm("sensitivity", event.target.value)
                  }
                  value={form.sensitivity}
                >
                  {policy.sensitivities.map((value) => (
                    <option key={value}>{value}</option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Declared row policy</span>
                <select
                  className="ne-input"
                  onChange={(event) =>
                    updateForm("rowPolicy", event.target.value)
                  }
                  value={form.rowPolicy}
                >
                  {policy.rowPolicies.map((value) => (
                    <option key={value}>{value}</option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Retention days</span>
                <input
                  className="ne-input"
                  max={policy.maximumRetentionDays}
                  min={policy.minimumRetentionDays}
                  onChange={(event) =>
                    updateForm("retentionDays", event.target.value)
                  }
                  required
                  type="number"
                  value={form.retentionDays}
                />
              </label>
              <label className="cl-admin-field report-definition-wide">
                <span>Governance reason</span>
                <textarea
                  className="ne-input"
                  maxLength={500}
                  minLength={10}
                  onChange={(event) => updateForm("reason", event.target.value)}
                  required
                  rows={2}
                  value={form.reason}
                />
              </label>
              <div className="report-fixed-controls report-definition-wide">
                <span>
                  Recipients: <strong>requesting-user</strong>
                </span>
                <span>
                  Delivery: <strong>local-download</strong>
                </span>
                <span>
                  Schedule: <strong>not governed in REP-01</strong>
                </span>
              </div>
              {mutationError && (
                <div
                  className="error-banner report-definition-wide"
                  role="alert"
                >
                  {mutationError}
                </div>
              )}
              <div className="report-definition-wide">
                <button
                  className="cl-btn-primary"
                  disabled={mutating}
                  type="submit"
                >
                  {mutating
                    ? "Saving…"
                    : successorDefinitionId
                      ? "Create successor revision"
                      : "Create draft definition"}
                </button>
              </div>
            </form>
            {selectedFamily && (
              <details className="report-family-preview">
                <summary>
                  Preview {selectedFamily.name} dictionary and schema
                </summary>
                <RevisionContract
                  revision={{
                    metricDictionary: selectedFamily.metricDictionary,
                    parameterSchema: selectedFamily.parameterSchema,
                    sourceDatasets: selectedFamily.sourceDatasets,
                    outputSchema: selectedFamily.outputSchema,
                  }}
                />
                <p className="cl-empty-text">
                  Fixture {selectedFamily.validationFixture.datasetId} ·{" "}
                  {selectedFamily.validationFixture.scenario}
                </p>
              </details>
            )}
          </section>

          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h3 className="cl-card-title">Definition workspace</h3>
                <p className="cl-empty-text">
                  Search, filter, and page every governance state. Ten rows per
                  page.
                </p>
              </div>
              <span className="report-catalog-count">
                {definitions.total} definitions
              </span>
            </div>
            <form
              className="report-definition-filters"
              onSubmit={(event) => {
                event.preventDefault();
                setPage(1);
                setAppliedSearch(search.trim());
              }}
            >
              <label className="cl-admin-field">
                <span>Search</span>
                <input
                  className="ne-input"
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Title, stable key, or owner"
                  value={search}
                />
              </label>
              <label className="cl-admin-field">
                <span>Status</span>
                <select
                  className="ne-input"
                  onChange={(event) => {
                    setStatus(event.target.value);
                    setPage(1);
                  }}
                  value={status}
                >
                  <option value="">All states</option>
                  {policy.states.map((value) => (
                    <option key={value}>{value}</option>
                  ))}
                </select>
              </label>
              <button className="cl-btn-secondary" type="submit">
                Apply filters
              </button>
            </form>
            {definitions.definitions.length === 0 ? (
              <div className="cl-empty-state">
                No report definitions match these filters.
              </div>
            ) : (
              <div
                className="cl-table-wrap"
                role="region"
                aria-label="Governed report definitions"
                tabIndex={0}
              >
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Definition</th>
                      <th>Status</th>
                      <th>Owner / policy</th>
                      <th>Revision</th>
                      <th>Updated</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {definitions.definitions.map((definition) => (
                      <tr key={definition.definitionId}>
                        <td>
                          <strong>{definition.title}</strong>
                          <code>{definition.stableKey}</code>
                          {definition.legacyReviewRequired && (
                            <span className="report-legacy-warning">
                              Owner review required
                            </span>
                          )}
                        </td>
                        <td>
                          <DefinitionStatus status={definition.status} />
                        </td>
                        <td>
                          {definition.ownerUsername}
                          <small>
                            {definition.sensitivity} · {definition.rowPolicy}
                          </small>
                        </td>
                        <td>
                          latest {definition.latestRevisionNumber}
                          <small>
                            active {definition.activeRevisionNumber ?? "none"}
                          </small>
                        </td>
                        <td>
                          {formatInstant(definition.updatedAt)}
                          <small>{definition.updatedBy}</small>
                        </td>
                        <td>
                          <button
                            className="cl-btn-secondary"
                            onClick={() =>
                              void openDefinition(definition.definitionId)
                            }
                            type="button"
                          >
                            Evidence
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <div className="report-pagination">
              <button
                className="cl-btn-secondary"
                disabled={page <= 1}
                onClick={() => setPage((current) => current - 1)}
                type="button"
              >
                Previous
              </button>
              <span>
                Page {definitions.page} of{" "}
                {Math.max(1, Math.ceil(definitions.total / 10))}
              </span>
              <button
                className="cl-btn-secondary"
                disabled={page * 10 >= definitions.total}
                onClick={() => setPage((current) => current + 1)}
                type="button"
              >
                Next
              </button>
            </div>
          </section>
        </>
      )}

      {detailError && (
        <div className="error-banner" role="alert">
          {detailError}
        </div>
      )}
      {detail && latestRevision && (
        <section className="cl-card report-definition-detail">
          <div className="cl-card-header">
            <div>
              <span className="report-governance-eyebrow">
                {detail.stableKey}
              </span>
              <h3 className="cl-card-title">{latestRevision.title}</h3>
              <p className="cl-empty-text">
                Latest revision {latestRevision.revisionNumber} · governance
                version {detail.governanceVersion}
              </p>
            </div>
            <button
              aria-label="Close report definition evidence"
              className="cl-btn-secondary"
              onClick={() => setDetail(null)}
              type="button"
            >
              Close
            </button>
          </div>

          {latestRevision.legacyReviewRequired && (
            <div className="warning-banner" role="note">
              This migrated definition has unknown sensitivity, row policy, or
              retention. It cannot advance. Create a complete governed
              replacement revision or retire it.
            </div>
          )}

          <dl className="report-definition-facts">
            <div>
              <dt>Status</dt>
              <dd>
                <DefinitionStatus status={latestRevision.status} />
              </dd>
            </div>
            <div>
              <dt>Owner</dt>
              <dd>{latestRevision.ownerUsername}</dd>
            </div>
            <div>
              <dt>Family</dt>
              <dd>{latestRevision.reportFamily}</dd>
            </div>
            <div>
              <dt>Sensitivity</dt>
              <dd>{latestRevision.sensitivity}</dd>
            </div>
            <div>
              <dt>Row policy</dt>
              <dd>{latestRevision.rowPolicy}</dd>
            </div>
            <div>
              <dt>Retention</dt>
              <dd>
                {latestRevision.retentionDays
                  ? `${latestRevision.retentionDays} days`
                  : "Owner review required"}
              </dd>
            </div>
            <div>
              <dt>Effective</dt>
              <dd>{formatInstant(latestRevision.effectiveFrom)}</dd>
            </div>
            <div>
              <dt>Last changed</dt>
              <dd>
                {formatInstant(latestRevision.updatedAt)} by{" "}
                {latestRevision.updatedBy}
              </dd>
            </div>
          </dl>
          <div className="report-purpose">
            <strong>Permitted purpose</strong>
            <p>{latestRevision.purpose}</p>
          </div>

          <RevisionContract revision={latestRevision} />

          <div className="report-validation-fixture">
            <strong>Validation fixture</strong>
            <span>{latestRevision.validationFixture.datasetId}</span>
            <span>{latestRevision.validationFixture.scenario}</span>
            <code>
              {latestRevision.validationFixture.expectedColumns.join(", ")}
            </code>
          </div>

          {(nextActions(latestRevision.status).length > 0 ||
            canCreateSuccessor) && (
            <div className="report-lifecycle-actions">
              <label className="cl-admin-field">
                <span>Lifecycle reason</span>
                <textarea
                  className="ne-input"
                  maxLength={500}
                  minLength={10}
                  onChange={(event) => setActionReason(event.target.value)}
                  rows={2}
                  value={actionReason}
                />
              </label>
              <div>
                {!latestRevision.legacyReviewRequired &&
                  nextActions(latestRevision.status).map((action) => (
                    <button
                      className={
                        action === "retire"
                          ? "cl-btn-danger"
                          : "cl-btn-secondary"
                      }
                      disabled={mutating || actionReason.trim().length < 10}
                      key={action}
                      onClick={() => void transition(action)}
                      type="button"
                    >
                      {action}
                    </button>
                  ))}
                {latestRevision.legacyReviewRequired && (
                  <button
                    className="cl-btn-danger"
                    disabled={mutating || actionReason.trim().length < 10}
                    onClick={() => void transition("retire")}
                    type="button"
                  >
                    retire
                  </button>
                )}
                {canCreateSuccessor && (
                  <button
                    className="cl-btn-primary"
                    disabled={mutating}
                    onClick={prepareSuccessor}
                    type="button"
                  >
                    {latestRevision.legacyReviewRequired
                      ? "Create governed replacement"
                      : "Prepare successor"}
                  </button>
                )}
              </div>
            </div>
          )}
          {mutationError && (
            <div className="error-banner" role="alert">
              {mutationError}
            </div>
          )}

          <details open>
            <summary>
              Immutable revisions ({detail.revisions.length})
            </summary>
            <div
              className="cl-table-wrap"
              role="region"
              aria-label="Report definition revisions"
              tabIndex={0}
            >
              <table className="cl-table">
                <thead>
                  <tr>
                    <th>Revision</th>
                    <th>Status</th>
                    <th>Title / family</th>
                    <th>Owner / policy</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.revisions.map((revision) => (
                    <tr key={revision.revisionId}>
                      <td>
                        {revision.revisionNumber}
                        <code>{revision.revisionId}</code>
                      </td>
                      <td>
                        <DefinitionStatus status={revision.status} />
                      </td>
                      <td>
                        {revision.title}
                        <small>{revision.reportFamily}</small>
                      </td>
                      <td>
                        {revision.ownerUsername}
                        <small>
                          {revision.sensitivity} · {revision.rowPolicy}
                        </small>
                      </td>
                      <td>
                        {formatInstant(revision.createdAt)}
                        <small>{revision.createdBy}</small>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
          <details open>
            <summary>Immutable events ({detail.events.length})</summary>
            <ol className="report-event-list">
              {detail.events.map((event) => (
                <li key={event.eventId}>
                  <div>
                    <strong>
                      Revision {event.revisionNumber} · {event.action}
                    </strong>
                    <DefinitionStatus status={event.toStatus} />
                  </div>
                  <p>{event.reason}</p>
                  <span>
                    {event.actorUsername} · {formatInstant(event.occurredAt)}
                  </span>
                  <code>SHA-256 {event.snapshotChecksum}</code>
                </li>
              ))}
            </ol>
          </details>
        </section>
      )}
    </>
  );
}
