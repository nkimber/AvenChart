import { useEffect, useEffectEvent, useMemo, useState } from "react";
import {
  actOnManagedRecord,
  createManagedRecord,
  getManagedRecordHistory,
  getManagedRecordPolicy,
  getManagedRecords,
  updateManagedRecordClassification,
  type ManagedRecordHistory,
  type ManagedRecordItem,
  type ManagedRecordList,
  type ManagedRecordPolicy,
} from "../../api/managedRecords.ts";

type Props = {
  sessionId: string;
  patientId: string;
  categories: Array<{ id: number; name: string }>;
  onReleased: () => Promise<void>;
};

type ReadyState = {
  status: "ready";
  policy: ManagedRecordPolicy;
  records: ManagedRecordList;
};

type State =
  | { status: "loading" }
  | ReadyState
  | { status: "error"; message: string };

type Draft = {
  title: string;
  serviceDate: string;
  categoryId: string;
  recordClass: string;
  sourceType: string;
  authorName: string;
  facilityId: string;
  sensitivity: string;
  languageTag: string;
  encounter: string;
  reason: string;
};

function initialDraft(categories: Props["categories"]): Draft {
  return {
    title: "",
    serviceDate: new Date().toISOString().slice(0, 10),
    categoryId: String(categories[0]?.id ?? 3),
    recordClass: "clinical-record",
    sourceType: "file-upload",
    authorName: "",
    facilityId: "",
    sensitivity: "standard",
    languageTag: "en-US",
    encounter: "",
    reason: "",
  };
}

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Managed record intake could not be completed.";
}

function readable(value: string) {
  return value.replaceAll("-", " ");
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} bytes`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KiB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MiB`;
}

async function encodeFile(file: File) {
  const bytes = new Uint8Array(await file.arrayBuffer());
  const digest = new Uint8Array(
    await crypto.subtle.digest("SHA-256", bytes),
  );
  let binary = "";
  const chunkSize = 0x8000;
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
  }
  return {
    contentBase64: btoa(binary),
    checksum: Array.from(digest, (value) =>
      value.toString(16).padStart(2, "0"),
    ).join(""),
  };
}

function StateBadge({ item }: { item: ManagedRecordItem }) {
  const className =
    item.state === "available"
      ? "cl-badge cl-badge-green"
      : item.state === "failed"
        ? "cl-badge cl-badge-red"
        : "cl-badge cl-badge-amber";
  return <span className={className}>{readable(item.state)}</span>;
}

export default function ManagedRecordIntake({
  sessionId,
  patientId,
  categories,
  onReleased,
}: Props) {
  const [state, setState] = useState<State>({ status: "loading" });
  const [reload, setReload] = useState(0);
  const [showForm, setShowForm] = useState(false);
  const [draft, setDraft] = useState(() => initialDraft(categories));
  const [file, setFile] = useState<File | null>(null);
  const [fileKey, setFileKey] = useState(0);
  const [idempotencyKey, setIdempotencyKey] = useState(() =>
    crypto.randomUUID(),
  );
  const [busy, setBusy] = useState(false);
  const [mutationError, setMutationError] = useState("");
  const [actionReason, setActionReason] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [history, setHistory] = useState<
    | { status: "loading" }
    | { status: "ready"; data: ManagedRecordHistory }
    | { status: "error"; message: string }
    | null
  >(null);
  const [classification, setClassification] = useState<Draft | null>(null);

  const load = useEffectEvent(async (signal: AbortSignal) => {
    setState({ status: "loading" });
    try {
      const [policy, records] = await Promise.all([
        getManagedRecordPolicy(sessionId, signal),
        getManagedRecords(sessionId, patientId, signal),
      ]);
      setState({ status: "ready", policy, records });
    } catch (error) {
      if (signal.aborted) return;
      setState({ status: "error", message: errorMessage(error) });
    }
  });

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [sessionId, patientId, reload]);

  const selected = useMemo(
    () =>
      state.status === "ready"
        ? state.records.items.find((item) => item.intakeId === selectedId) ??
          null
        : null,
    [selectedId, state],
  );

  async function refresh(released = false) {
    setReload((value) => value + 1);
    if (released) await onReleased();
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!file) {
      setMutationError("Choose a file for managed intake.");
      return;
    }
    setBusy(true);
    setMutationError("");
    try {
      const encoded = await encodeFile(file);
      const result = await createManagedRecord(sessionId, {
        patientId,
        categoryId: Number(draft.categoryId),
        title: draft.title,
        serviceDate: draft.serviceDate,
        encounter: draft.encounter ? Number(draft.encounter) : null,
        recordClass: draft.recordClass,
        sourceType: draft.sourceType,
        authorName: draft.authorName,
        facilityId: draft.facilityId ? Number(draft.facilityId) : null,
        sensitivity: draft.sensitivity,
        languageTag: draft.languageTag,
        fileName: file.name,
        mediaType: file.type || "application/octet-stream",
        contentBase64: encoded.contentBase64,
        expectedChecksumSha256: encoded.checksum,
        idempotencyKey,
        reason: draft.reason,
      });
      setSelectedId(result.intake.intakeId);
      setShowForm(false);
      setDraft(initialDraft(categories));
      setFile(null);
      setFileKey((value) => value + 1);
      setIdempotencyKey(crypto.randomUUID());
      await refresh();
    } catch (error) {
      setMutationError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  async function act(item: ManagedRecordItem, action: string) {
    if (!actionReason.trim()) {
      setMutationError("Enter a workflow reason before taking an action.");
      return;
    }
    setBusy(true);
    setMutationError("");
    try {
      const result = await actOnManagedRecord(
        sessionId,
        item.intakeId,
        action,
        item.workflowVersion,
        actionReason,
      );
      setSelectedId(result.intakeId);
      setActionReason("");
      await refresh(action === "release" && result.state === "available");
      await openHistory(result.intakeId);
    } catch (error) {
      setMutationError(errorMessage(error));
      await refresh();
    } finally {
      setBusy(false);
    }
  }

  async function openHistory(intakeId: string) {
    setSelectedId(intakeId);
    setHistory({ status: "loading" });
    try {
      const result = await getManagedRecordHistory(sessionId, intakeId);
      setHistory({ status: "ready", data: result });
    } catch (error) {
      setHistory({ status: "error", message: errorMessage(error) });
    }
  }

  function beginClassification(item: ManagedRecordItem) {
    setSelectedId(item.intakeId);
    setClassification({
      title: item.title,
      serviceDate: item.serviceDate,
      categoryId: String(item.categoryId),
      recordClass: item.recordClass,
      sourceType: item.sourceType,
      authorName: item.authorName,
      facilityId: item.facilityId ? String(item.facilityId) : "",
      sensitivity: item.sensitivity,
      languageTag: item.languageTag,
      encounter: item.encounter ? String(item.encounter) : "",
      reason: "",
    });
  }

  async function saveClassification(event: React.FormEvent) {
    event.preventDefault();
    if (!selected || !classification) return;
    setBusy(true);
    setMutationError("");
    try {
      await updateManagedRecordClassification(sessionId, selected.intakeId, {
        expectedVersion: selected.workflowVersion,
        recordClass: classification.recordClass,
        sourceType: classification.sourceType,
        authorName: classification.authorName,
        facilityId: classification.facilityId
          ? Number(classification.facilityId)
          : null,
        sensitivity: classification.sensitivity,
        languageTag: classification.languageTag,
        reason: classification.reason,
      });
      setClassification(null);
      await refresh();
      await openHistory(selected.intakeId);
    } catch (error) {
      setMutationError(errorMessage(error));
      await refresh();
    } finally {
      setBusy(false);
    }
  }

  return (
    <section
      className="managed-records"
      aria-labelledby="managed-records-heading"
    >
      <div className="managed-records-heading">
        <div>
          <p className="document-workspace-eyebrow">
            REC-01/02 managed boundary
          </p>
          <h2 id="managed-records-heading">Managed record intake</h2>
          <p>
            Capture classified content outside the chart, quarantine it, run
            bounded validation, and release it into patient documents only
            after an explicit version-safe decision.
          </p>
        </div>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() => setShowForm((value) => !value)}
          aria-expanded={showForm}
        >
          {showForm ? "Close managed intake" : "New managed intake"}
        </button>
      </div>

      {state.status === "loading" && (
        <div className="managed-record-message" role="status">
          Loading managed record controls...
        </div>
      )}
      {state.status === "error" && (
        <div className="managed-record-message" role="alert">
          <p>{state.message}</p>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setReload((value) => value + 1)}
          >
            Retry
          </button>
        </div>
      )}

      {state.status === "ready" && (
        <>
          <aside className="managed-record-boundary" role="note">
            <strong>{state.policy.revision}:</strong>{" "}
            {state.policy.environmentBoundary} Anti-malware verified:{" "}
            <strong>{state.policy.antiMalwareVerified ? "yes" : "no"}</strong>.
          </aside>

          <div className="managed-record-counts" aria-label="Managed record counts">
            <span>
              <strong>{state.records.counts.withheld}</strong> withheld
            </span>
            <span>
              <strong>{state.records.counts.quarantined}</strong> quarantined
            </span>
            <span>
              <strong>{state.records.counts.scanning}</strong> validating
            </span>
            <span>
              <strong>{state.records.counts.failed}</strong> failed
            </span>
            <span>
              <strong>{state.records.counts.available}</strong> available
            </span>
          </div>

          <details className="managed-record-policy">
            <summary>Adapter and production boundary</summary>
            <dl>
              <div>
                <dt>Storage</dt>
                <dd>
                  <code>{state.policy.storageAdapter.adapterId}</code>
                  <span>{state.policy.storageAdapter.evidence}</span>
                </dd>
              </div>
              <div>
                <dt>Validation</dt>
                <dd>
                  <code>{state.policy.validationAdapter.adapterId}</code>
                  <span>{state.policy.validationAdapter.evidence}</span>
                </dd>
              </div>
            </dl>
            <ul>
              {state.policy.productionBlockers.map((blocker) => (
                <li key={blocker}>{blocker}</li>
              ))}
            </ul>
          </details>

          {showForm && (
            <form
              className="managed-record-form"
              aria-label="Create managed record intake"
              onSubmit={submit}
            >
              <label>
                Record title
                <input
                  required
                  maxLength={255}
                  value={draft.title}
                  onChange={(event) =>
                    setDraft({ ...draft, title: event.target.value })
                  }
                />
              </label>
              <label>
                File
                <input
                  key={fileKey}
                  required
                  type="file"
                  accept={state.policy.acceptedMediaTypes.join(",")}
                  onChange={(event) => setFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <label>
                Service date
                <input
                  required
                  type="date"
                  value={draft.serviceDate}
                  onChange={(event) =>
                    setDraft({ ...draft, serviceDate: event.target.value })
                  }
                />
              </label>
              <label>
                Filing category
                <select
                  value={draft.categoryId}
                  onChange={(event) =>
                    setDraft({ ...draft, categoryId: event.target.value })
                  }
                >
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Record class
                <select
                  value={draft.recordClass}
                  onChange={(event) =>
                    setDraft({ ...draft, recordClass: event.target.value })
                  }
                >
                  {state.policy.recordClasses.map((value) => (
                    <option key={value} value={value}>
                      {readable(value)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Source
                <select
                  value={draft.sourceType}
                  onChange={(event) =>
                    setDraft({ ...draft, sourceType: event.target.value })
                  }
                >
                  {state.policy.sourceTypes.map((value) => (
                    <option key={value} value={value}>
                      {readable(value)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Author or originator
                <input
                  required
                  maxLength={200}
                  value={draft.authorName}
                  onChange={(event) =>
                    setDraft({ ...draft, authorName: event.target.value })
                  }
                />
              </label>
              <label>
                Facility ID (optional)
                <input
                  min={1}
                  type="number"
                  value={draft.facilityId}
                  onChange={(event) =>
                    setDraft({ ...draft, facilityId: event.target.value })
                  }
                />
              </label>
              <label>
                Sensitivity
                <select
                  value={draft.sensitivity}
                  onChange={(event) =>
                    setDraft({ ...draft, sensitivity: event.target.value })
                  }
                >
                  {state.policy.sensitivityLevels.map((value) => (
                    <option key={value} value={value}>
                      {readable(value)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Language
                <input
                  required
                  maxLength={35}
                  value={draft.languageTag}
                  onChange={(event) =>
                    setDraft({ ...draft, languageTag: event.target.value })
                  }
                />
              </label>
              <label>
                Encounter number (optional)
                <input
                  min={1}
                  type="number"
                  value={draft.encounter}
                  onChange={(event) =>
                    setDraft({ ...draft, encounter: event.target.value })
                  }
                />
              </label>
              <label className="managed-record-form-wide">
                Capture reason
                <textarea
                  required
                  maxLength={500}
                  value={draft.reason}
                  onChange={(event) =>
                    setDraft({ ...draft, reason: event.target.value })
                  }
                />
              </label>
              <div className="managed-record-form-wide managed-record-submit">
                <span>
                  Browser SHA-256 and idempotency are verified before bytes are
                  accepted.
                </span>
                <button className="cl-btn-primary" disabled={busy} type="submit">
                  {busy ? "Capturing..." : "Capture outside chart"}
                </button>
              </div>
            </form>
          )}

          {mutationError && (
            <div className="managed-record-error" role="alert">
              {mutationError}
            </div>
          )}

          {state.records.items.length === 0 ? (
            <div className="managed-record-message">
              No managed record intakes exist for this patient.
            </div>
          ) : (
            <div className="managed-record-list" aria-label="Managed record intakes">
              {state.records.items.map((item) => (
                <article
                  className={
                    item.intakeId === selectedId
                      ? "managed-record-card is-selected"
                      : "managed-record-card"
                  }
                  key={item.intakeId}
                >
                  <div className="managed-record-card-heading">
                    <div>
                      <h3>{item.title}</h3>
                      <p>
                        {item.categoryName} / {item.recordClass} /{" "}
                        {item.sensitivity}
                      </p>
                    </div>
                    <StateBadge item={item} />
                  </div>
                  <dl>
                    <div>
                      <dt>Availability</dt>
                      <dd>{item.availabilityStatus}</dd>
                    </div>
                    <div>
                      <dt>Validation</dt>
                      <dd>{readable(item.validationStatus)}</dd>
                    </div>
                    <div>
                      <dt>Content</dt>
                      <dd>
                        v{item.contentVersion} / {formatBytes(item.sizeBytes)}
                      </dd>
                    </div>
                    <div>
                      <dt>Workflow</dt>
                      <dd>v{item.workflowVersion}</dd>
                    </div>
                    <div>
                      <dt>SHA-256</dt>
                      <dd>
                        <code>{item.contentChecksumSha256.slice(0, 16)}...</code>
                      </dd>
                    </div>
                    <div>
                      <dt>Anti-malware</dt>
                      <dd>{item.antiMalwareVerified ? "verified" : "not verified"}</dd>
                    </div>
                  </dl>
                  {item.failureReason && (
                    <p className="managed-record-failure">
                      <strong>Failure:</strong> {item.failureReason}
                    </p>
                  )}
                  {item.documentId && (
                    <p className="managed-record-release">
                      Released as patient document {item.documentId}.
                    </p>
                  )}
                  <div className="managed-record-actions">
                    {item.availableActions
                      .filter((action) => action !== "reclassify")
                      .map((action) => (
                        <button
                          className={
                            action === "release"
                              ? "cl-btn-primary"
                              : "cl-btn-secondary"
                          }
                          disabled={busy}
                          key={action}
                          type="button"
                          onClick={() => void act(item, action)}
                        >
                          {action === "start"
                            ? "Start local validation"
                            : readable(action)}
                        </button>
                      ))}
                    {item.availableActions.includes("reclassify") && (
                      <button
                        className="cl-btn-secondary"
                        disabled={busy}
                        type="button"
                        onClick={() => beginClassification(item)}
                      >
                        Reclassify
                      </button>
                    )}
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => void openHistory(item.intakeId)}
                    >
                      History
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}

          {selected &&
            selected.availableActions.some((action) => action !== "reclassify") && (
              <label className="managed-record-action-reason">
                Workflow reason for the selected intake
                <textarea
                  maxLength={500}
                  value={actionReason}
                  onChange={(event) => setActionReason(event.target.value)}
                />
              </label>
            )}

          {selected && classification && (
            <form
              className="managed-record-classification"
              aria-label="Update managed record classification"
              onSubmit={saveClassification}
            >
              <div>
                <h3>Classification revision</h3>
                <p>
                  Update the loaded workflow version before validation begins.
                </p>
              </div>
              <label>
                Record class
                <select
                  value={classification.recordClass}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      recordClass: event.target.value,
                    })
                  }
                >
                  {state.policy.recordClasses.map((value) => (
                    <option key={value} value={value}>
                      {readable(value)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Sensitivity
                <select
                  value={classification.sensitivity}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      sensitivity: event.target.value,
                    })
                  }
                >
                  {state.policy.sensitivityLevels.map((value) => (
                    <option key={value} value={value}>
                      {readable(value)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Author or originator
                <input
                  required
                  value={classification.authorName}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      authorName: event.target.value,
                    })
                  }
                />
              </label>
              <label>
                Language
                <input
                  required
                  value={classification.languageTag}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      languageTag: event.target.value,
                    })
                  }
                />
              </label>
              <label>
                Facility ID (optional)
                <input
                  min={1}
                  type="number"
                  value={classification.facilityId}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      facilityId: event.target.value,
                    })
                  }
                />
              </label>
              <label>
                Revision reason
                <input
                  required
                  maxLength={500}
                  value={classification.reason}
                  onChange={(event) =>
                    setClassification({
                      ...classification,
                      reason: event.target.value,
                    })
                  }
                />
              </label>
              <div className="managed-record-actions">
                <button className="cl-btn-primary" disabled={busy} type="submit">
                  Save classification revision
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setClassification(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}

          {history?.status === "loading" && (
            <div className="managed-record-message" role="status">
              Loading intake history...
            </div>
          )}
          {history?.status === "error" && (
            <div className="managed-record-error" role="alert">
              {history.message}
            </div>
          )}
          {history?.status === "ready" && (
            <section
              className="managed-record-history"
              aria-labelledby="managed-record-history-heading"
            >
              <div>
                <h3 id="managed-record-history-heading">
                  Immutable intake history
                </h3>
                <span>{history.data.eventCount} events</span>
              </div>
              <ol>
                {history.data.events.map((event) => (
                  <li key={event.eventId}>
                    <strong>{readable(event.action)}</strong>
                    <span>
                      {event.fromState ? `${event.fromState} to ` : ""}
                      {event.toState} / v{event.workflowVersion}
                    </span>
                    <p>{event.reason}</p>
                    <small>
                      {event.actor} / {new Date(event.occurredAt).toLocaleString()}
                    </small>
                  </li>
                ))}
              </ol>
            </section>
          )}
        </>
      )}
    </section>
  );
}
