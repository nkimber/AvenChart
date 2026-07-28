import { useCallback, useEffect, useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  ApiRequestError,
  createPatientAuthorization,
  getClinicalWorkflowAssignees,
  getPatientAuthorizationHistory,
  getPatientAuthorizations,
  updatePatientAuthorizationAssignment,
  updatePatientAuthorizationStatus,
  type ClinicalWorkflowAssignee,
  type ClinicalWorkflowTransitionOption,
  type PatientAuthorization,
  type PatientAuthorizationWorkflowEvent,
} from "../../api.ts";
import type { PatientOutletContext } from "./PatientShell.tsx";

type MutableAuthorizationState =
  | "submitted"
  | "approved"
  | "denied"
  | "expired"
  | "cancelled";

type DraftForm = {
  payer: string;
  service: string;
  expiresAt: string;
  assignedTo: string;
  dueAt: string;
  reason: string;
};

const emptyDraft: DraftForm = {
  payer: "",
  service: "",
  expiresAt: "",
  assignedTo: "",
  dueAt: "",
  reason: "",
};

function displayDate(value?: string | null) {
  return value
    ? new Date(value).toLocaleDateString(undefined, { timeZone: "UTC" })
    : "Not set";
}

function displayDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function titleCase(value: string) {
  return value
    .split("-")
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(" ");
}

function eventSummary(event: PatientAuthorizationWorkflowEvent) {
  if (event.action === "reassigned") {
    return `Responsibility changed from ${event.fromAssignedTo ?? "unassigned"} to ${event.toAssignedTo ?? "unassigned"}.`;
  }
  if (!event.fromState) return `Draft created in ${event.toState} state.`;
  return `State changed from ${event.fromState} to ${event.toState}.`;
}

export default function PatientAuthorizations() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [authorizations, setAuthorizations] = useState<PatientAuthorization[]>(
    [],
  );
  const [assignees, setAssignees] = useState<ClinicalWorkflowAssignee[]>([]);
  const [policyRevision, setPolicyRevision] = useState("");
  const [draft, setDraft] = useState<DraftForm>(emptyDraft);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [transition, setTransition] =
    useState<ClinicalWorkflowTransitionOption | null>(null);
  const [transitionReason, setTransitionReason] = useState("");
  const [authorizationNumber, setAuthorizationNumber] = useState("");
  const [assignment, setAssignment] = useState({
    assignedTo: "",
    dueAt: "",
    reason: "",
  });
  const [history, setHistory] = useState<PatientAuthorizationWorkflowEvent[]>(
    [],
  );
  const [historyLoading, setHistoryLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const selected = useMemo(
    () => authorizations.find((item) => item.id === selectedId) ?? null,
    [authorizations, selectedId],
  );

  const loadHistory = useCallback(
    async (authorizationId: string) => {
      setHistoryLoading(true);
      try {
        const response = await getPatientAuthorizationHistory(
          session.sessionId,
          patientId,
          authorizationId,
        );
        setHistory(response.events);
      } catch (reason) {
        setError(
          reason instanceof Error
            ? reason.message
            : "Unable to load authorization history.",
        );
      } finally {
        setHistoryLoading(false);
      }
    },
    [patientId, session.sessionId],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [items, roster] = await Promise.all([
        getPatientAuthorizations(session.sessionId, patientId),
        getClinicalWorkflowAssignees(session.sessionId),
      ]);
      setAuthorizations(items);
      setAssignees(roster.assignees);
      setPolicyRevision(roster.policyRevision);
      setDraft((current) => ({
        ...current,
        assignedTo:
          current.assignedTo ||
          roster.assignees.find(
            (assignee) => assignee.username === session.username,
          )?.username ||
          roster.assignees[0]?.username ||
          "",
      }));
      setSelectedId((current) =>
        current && items.some((item) => item.id === current)
          ? current
          : (items[0]?.id ?? null),
      );
      setError("");
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Unable to load authorizations.",
      );
    } finally {
      setLoading(false);
    }
  }, [patientId, session.sessionId, session.username]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!selected) {
      setHistory([]);
      return;
    }
    setAssignment({
      assignedTo: selected.assignedTo,
      dueAt: selected.dueAt?.slice(0, 10) ?? "",
      reason: "",
    });
    setTransition(null);
    setTransitionReason("");
    setAuthorizationNumber("");
    void loadHistory(selected.id);
  }, [loadHistory, selected?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  async function handleMutationFailure(reason: unknown) {
    if (reason instanceof ApiRequestError && reason.status === 409) {
      setNotice("");
      setError(
        "This authorization changed after you opened it. Current values were reloaded; review them before trying again.",
      );
      await load();
      if (selectedId) await loadHistory(selectedId);
      return;
    }
    setError(
      reason instanceof Error
        ? reason.message
        : "The authorization could not be updated.",
    );
  }

  async function create(event: React.FormEvent) {
    event.preventDefault();
    if (
      !draft.payer.trim() ||
      !draft.service.trim() ||
      !draft.assignedTo ||
      !draft.reason.trim()
    ) {
      setError("Payer, service, responsible staff, and creation reason are required.");
      return;
    }
    setWorking("create");
    try {
      const created = await createPatientAuthorization(
        session.sessionId,
        patientId,
        {
          payer: draft.payer.trim(),
          service: draft.service.trim(),
          expiresAt: draft.expiresAt || undefined,
          assignedTo: draft.assignedTo,
          dueAt: draft.dueAt || undefined,
          reason: draft.reason.trim(),
        },
      );
      setDraft((current) => ({
        ...emptyDraft,
        assignedTo: current.assignedTo,
      }));
      setNotice("Authorization draft created with versioned workflow history.");
      setError("");
      await load();
      setSelectedId(created.id);
    } catch (reason) {
      await handleMutationFailure(reason);
    } finally {
      setWorking("");
    }
  }

  async function applyTransition(event: React.FormEvent) {
    event.preventDefault();
    if (!selected || !transition || !transitionReason.trim()) {
      setError("A transition reason is required.");
      return;
    }
    if (
      transition.requiresAuthorizationNumber &&
      !authorizationNumber.trim()
    ) {
      setError("An authorization number is required to approve this request.");
      return;
    }
    setWorking(`transition-${selected.id}`);
    try {
      const updated = await updatePatientAuthorizationStatus(
        session.sessionId,
        patientId,
        selected.id,
        {
          status: transition.toState as MutableAuthorizationState,
          authorizationNumber: authorizationNumber.trim() || undefined,
          expectedVersion: selected.workflowVersion,
          reasonCode: transition.reasonCode,
          reason: transitionReason.trim(),
        },
      );
      setAuthorizations((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
      setTransition(null);
      setTransitionReason("");
      setAuthorizationNumber("");
      setNotice(`${transition.label} recorded as workflow version ${updated.workflowVersion}.`);
      setError("");
      await loadHistory(updated.id);
    } catch (reason) {
      await handleMutationFailure(reason);
    } finally {
      setWorking("");
    }
  }

  async function saveAssignment(event: React.FormEvent) {
    event.preventDefault();
    if (!selected || !assignment.assignedTo || !assignment.reason.trim()) {
      setError("Responsible staff and an assignment reason are required.");
      return;
    }
    setWorking(`assignment-${selected.id}`);
    try {
      const updated = await updatePatientAuthorizationAssignment(
        session.sessionId,
        patientId,
        selected.id,
        {
          assignedTo: assignment.assignedTo,
          dueAt: assignment.dueAt || undefined,
          expectedVersion: selected.workflowVersion,
          reasonCode: "responsibility-transfer",
          reason: assignment.reason.trim(),
        },
      );
      setAuthorizations((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
      setAssignment((current) => ({ ...current, reason: "" }));
      setNotice(
        `Responsibility updated as workflow version ${updated.workflowVersion}.`,
      );
      setError("");
      await loadHistory(updated.id);
    } catch (reason) {
      await handleMutationFailure(reason);
    } finally {
      setWorking("");
    }
  }

  return (
    <div className="clinician-page authorization-workspace">
      <div className="clinician-page-header authorization-page-heading">
        <div>
          <h1 className="clinician-page-title">Payer authorizations</h1>
          <p className="clinician-page-subtitle">
            Govern local responsibility and state changes. This workspace does
            not submit to payers or verify eligibility.
          </p>
        </div>
        {policyRevision && (
          <span className="authorization-policy-revision">
            Policy {policyRevision}
          </span>
        )}
      </div>

      <section className="cl-card authorization-boundary" aria-label="Scope boundary">
        <strong>Local workflow only</strong>
        <span>
          Assignment choices, reason codes, and transitions are development
          defaults pending production clinical governance.
        </span>
      </section>

      <section className="cl-card">
        <div className="cl-card-header">
          <div>
            <h2 className="cl-card-title">Create authorization draft</h2>
            <p className="cl-card-subtitle">
              Start with an accountable owner, optional due date, and recorded
              reason.
            </p>
          </div>
        </div>
        <form className="authorization-create-grid" onSubmit={create}>
          <label>
            <span>Payer</span>
            <input
              className="ne-input"
              name="payer"
              value={draft.payer}
              onChange={(event) =>
                setDraft({ ...draft, payer: event.target.value })
              }
              required
            />
          </label>
          <label>
            <span>Service</span>
            <input
              className="ne-input"
              name="service"
              value={draft.service}
              onChange={(event) =>
                setDraft({ ...draft, service: event.target.value })
              }
              required
            />
          </label>
          <label>
            <span>Responsible staff</span>
            <select
              className="ne-input"
              name="assignedTo"
              value={draft.assignedTo}
              onChange={(event) =>
                setDraft({ ...draft, assignedTo: event.target.value })
              }
              required
            >
              <option value="">Select staff</option>
              {assignees.map((assignee) => (
                <option key={assignee.username} value={assignee.username}>
                  {assignee.displayName} · {assignee.role}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>Work due date</span>
            <input
              className="ne-input"
              name="dueAt"
              type="date"
              value={draft.dueAt}
              onChange={(event) =>
                setDraft({ ...draft, dueAt: event.target.value })
              }
            />
          </label>
          <label>
            <span>Authorization expiry</span>
            <input
              className="ne-input"
              name="expiresAt"
              type="date"
              value={draft.expiresAt}
              onChange={(event) =>
                setDraft({ ...draft, expiresAt: event.target.value })
              }
            />
          </label>
          <label className="authorization-reason-field">
            <span>Creation reason</span>
            <textarea
              className="cl-textarea"
              name="reason"
              rows={3}
              maxLength={500}
              value={draft.reason}
              onChange={(event) =>
                setDraft({ ...draft, reason: event.target.value })
              }
              required
            />
          </label>
          <div className="authorization-form-actions">
            <button
              className="cl-btn-primary"
              type="submit"
              disabled={working === "create" || loading}
            >
              {working === "create" ? "Creating…" : "Create governed draft"}
            </button>
          </div>
        </form>
      </section>

      <div className="authorization-feedback" aria-live="polite">
        {error && (
          <div className="authorization-message authorization-message-error" role="alert">
            <span>{error}</span>
            {loading === false && (
              <button className="cl-btn-secondary" type="button" onClick={load}>
                Reload
              </button>
            )}
          </div>
        )}
        {notice && !error && (
          <p className="authorization-message authorization-message-success">
            {notice}
          </p>
        )}
      </div>

      <section className="cl-card">
        <div className="cl-card-header">
          <div>
            <h2 className="cl-card-title">Authorization work queue</h2>
            <p className="cl-card-subtitle">
              {authorizations.length} authorization
              {authorizations.length === 1 ? "" : "s"} recorded for this patient.
            </p>
          </div>
        </div>
        {loading ? (
          <p className="cl-empty-text">Loading authorization work…</p>
        ) : authorizations.length === 0 ? (
          <p className="cl-empty-text">
            No authorizations have been recorded. Create a governed draft above.
          </p>
        ) : (
          <div className="authorization-queue">
            {authorizations.map((item) => (
              <button
                className={`authorization-queue-item${selectedId === item.id ? " is-selected" : ""}`}
                type="button"
                key={item.id}
                onClick={() => setSelectedId(item.id)}
                aria-pressed={selectedId === item.id}
              >
                <span className="authorization-queue-main">
                  <strong>{item.service}</strong>
                  <span>{item.payer}</span>
                </span>
                <span className={`authorization-state authorization-state-${item.status}`}>
                  {titleCase(item.status)}
                </span>
                <span>
                  Owner <strong>{item.assignedDisplayName}</strong>
                </span>
                <span>Due {displayDate(item.dueAt)}</span>
                <span>Version {item.workflowVersion}</span>
              </button>
            ))}
          </div>
        )}
      </section>

      {selected && (
        <section
          className="authorization-detail-grid"
          aria-label={`${selected.service} authorization details`}
        >
          <article className="cl-card authorization-detail-card">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title">{selected.service}</h2>
                <p className="cl-card-subtitle">{selected.payer}</p>
              </div>
              <span className={`authorization-state authorization-state-${selected.status}`}>
                {titleCase(selected.status)}
              </span>
            </div>
            <dl className="authorization-facts">
              <div>
                <dt>Responsible staff</dt>
                <dd>{selected.assignedDisplayName}</dd>
              </div>
              <div>
                <dt>Work due</dt>
                <dd>{displayDate(selected.dueAt)}</dd>
              </div>
              <div>
                <dt>Requested</dt>
                <dd>{displayDate(selected.requestedAt)}</dd>
              </div>
              <div>
                <dt>Expires</dt>
                <dd>{displayDate(selected.expiresAt)}</dd>
              </div>
              <div>
                <dt>Authorization number</dt>
                <dd>{selected.authorizationNumber || "Not assigned"}</dd>
              </div>
              <div>
                <dt>Workflow evidence</dt>
                <dd>
                  Version {selected.workflowVersion} · {selected.policyRevision}
                </dd>
              </div>
            </dl>

            <div className="authorization-action-section">
              <h3>Available state changes</h3>
              {selected.availableTransitions.length === 0 ? (
                <p className="cl-empty-text">
                  This authorization is in a terminal state.
                </p>
              ) : (
                <div className="authorization-action-list">
                  {selected.availableTransitions.map((option) => (
                    <button
                      className={
                        transition?.action === option.action
                          ? "cl-btn-primary"
                          : "cl-btn-secondary"
                      }
                      type="button"
                      key={`${option.action}-${option.toState}`}
                      onClick={() => {
                        setTransition(option);
                        setTransitionReason("");
                        setAuthorizationNumber("");
                      }}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
              )}
            </div>

            {transition && (
              <form
                className="authorization-editor"
                onSubmit={applyTransition}
              >
                <div>
                  <strong>{transition.label}</strong>
                  <span>
                    {titleCase(transition.fromState)} →{" "}
                    {titleCase(transition.toState)}
                  </span>
                </div>
                {transition.requiresAuthorizationNumber && (
                  <label>
                    <span>Authorization number</span>
                    <input
                      className="ne-input"
                      name="authorizationNumber"
                      value={authorizationNumber}
                      onChange={(event) =>
                        setAuthorizationNumber(event.target.value)
                      }
                      required
                    />
                  </label>
                )}
                <label>
                  <span>Reason</span>
                  <textarea
                    className="cl-textarea"
                    name="transitionReason"
                    rows={3}
                    maxLength={500}
                    value={transitionReason}
                    onChange={(event) =>
                      setTransitionReason(event.target.value)
                    }
                    required
                  />
                </label>
                <div className="authorization-form-actions">
                  <button
                    className="cl-btn-primary"
                    type="submit"
                    disabled={working === `transition-${selected.id}`}
                  >
                    {working === `transition-${selected.id}`
                      ? "Recording…"
                      : `Confirm ${transition.label.toLowerCase()}`}
                  </button>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => setTransition(null)}
                  >
                    Cancel
                  </button>
                </div>
              </form>
            )}

            {selected.availableTransitions.length > 0 && (
              <form
                className="authorization-editor"
                onSubmit={saveAssignment}
              >
                <div>
                  <strong>Responsibility and due date</strong>
                  <span>Every change creates a new workflow version.</span>
                </div>
                <div className="authorization-assignment-grid">
                  <label>
                    <span>Responsible staff</span>
                    <select
                      className="ne-input"
                      name="assignmentOwner"
                      value={assignment.assignedTo}
                      onChange={(event) =>
                        setAssignment({
                          ...assignment,
                          assignedTo: event.target.value,
                        })
                      }
                      required
                    >
                      {assignees.map((assignee) => (
                        <option
                          key={assignee.username}
                          value={assignee.username}
                        >
                          {assignee.displayName} · {assignee.role}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    <span>Work due date</span>
                    <input
                      className="ne-input"
                      name="assignmentDueAt"
                      type="date"
                      value={assignment.dueAt}
                      onChange={(event) =>
                        setAssignment({
                          ...assignment,
                          dueAt: event.target.value,
                        })
                      }
                    />
                  </label>
                </div>
                <label>
                  <span>Assignment reason</span>
                  <textarea
                    className="cl-textarea"
                    name="assignmentReason"
                    rows={3}
                    maxLength={500}
                    value={assignment.reason}
                    onChange={(event) =>
                      setAssignment({
                        ...assignment,
                        reason: event.target.value,
                      })
                    }
                    required
                  />
                </label>
                <div className="authorization-form-actions">
                  <button
                    className="cl-btn-secondary"
                    type="submit"
                    disabled={working === `assignment-${selected.id}`}
                  >
                    {working === `assignment-${selected.id}`
                      ? "Saving…"
                      : "Save responsibility"}
                  </button>
                </div>
              </form>
            )}
          </article>

          <article className="cl-card authorization-history-card">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title">Workflow history</h2>
                <p className="cl-card-subtitle">
                  Immutable actor, reason, state, and assignment evidence.
                </p>
              </div>
              <span>{history.length} events</span>
            </div>
            {historyLoading ? (
              <p className="cl-empty-text">Loading workflow history…</p>
            ) : history.length === 0 ? (
              <p className="cl-empty-text">No workflow events are available.</p>
            ) : (
              <ol className="authorization-history">
                {history.map((event) => (
                  <li key={event.eventId}>
                    <div className="authorization-history-heading">
                      <strong>{titleCase(event.action)}</strong>
                      <span>Version {event.workflowVersion}</span>
                    </div>
                    <p>{eventSummary(event)}</p>
                    <blockquote>{event.reason}</blockquote>
                    <dl>
                      <div>
                        <dt>Reason code</dt>
                        <dd>{event.reasonCode}</dd>
                      </div>
                      <div>
                        <dt>Actor</dt>
                        <dd>{event.actor}</dd>
                      </div>
                      <div>
                        <dt>Recorded</dt>
                        <dd>{displayDateTime(event.occurredAt)}</dd>
                      </div>
                    </dl>
                  </li>
                ))}
              </ol>
            )}
          </article>
        </section>
      )}
    </div>
  );
}
