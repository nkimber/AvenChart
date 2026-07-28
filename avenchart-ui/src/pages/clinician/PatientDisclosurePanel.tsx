import {
  useEffect,
  useEffectEvent,
  useMemo,
  useState,
  type FormEvent,
} from "react";
import {
  createPatientDisclosureAuthority,
  createPatientDisclosureRequest,
  decidePatientDisclosureRequest,
  getPatientDisclosureAuthorities,
  getPatientDisclosureAuthorityHistory,
  getPatientDisclosurePolicy,
  getPatientDisclosureRequestHistory,
  getPatientDisclosureRequests,
  transitionPatientDisclosureAuthority,
  type PatientDisclosureAuthority,
  type PatientDisclosureAuthorityEvent,
  type PatientDisclosurePolicy,
  type PatientDisclosureRequest,
  type PatientDisclosureRequestEvent,
} from "../../api/patientDisclosure.ts";
import { showToast } from "../../components/Toast.tsx";

type LoadedState = {
  policy: PatientDisclosurePolicy;
  authorities: PatientDisclosureAuthority[];
  requests: PatientDisclosureRequest[];
};

type AsyncState =
  | { status: "loading" }
  | { status: "ready"; data: LoadedState }
  | { status: "error"; message: string };

type AuthorityDraft = {
  authorityType: "patient" | "proxy";
  proxyName: string;
  proxyRelationship: string;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  effectiveDate: string;
  expiresDate: string;
  verificationMethod: string;
  verificationReference: string;
  reason: string;
};

type RequestDraft = {
  authorityId: string;
  purpose: string;
  recipient: string;
  scopeKeys: string[];
  reason: string;
};

function dateInput(offsetDays: number) {
  const value = new Date();
  value.setDate(value.getDate() + offsetDays);
  return value.toISOString().slice(0, 10);
}

function emptyAuthorityDraft(): AuthorityDraft {
  return {
    authorityType: "patient",
    proxyName: "",
    proxyRelationship: "",
    purpose: "",
    recipient: "",
    scopeKeys: [],
    effectiveDate: dateInput(0),
    expiresDate: dateInput(30),
    verificationMethod: "in-person",
    verificationReference: "",
    reason: "",
  };
}

function emptyRequestDraft(): RequestDraft {
  return {
    authorityId: "",
    purpose: "",
    recipient: "",
    scopeKeys: [],
    reason: "",
  };
}

function readable(value: string) {
  return value.replaceAll("-", " ");
}

function message(error: unknown) {
  return error instanceof Error ? error.message : "The request failed.";
}

function authorityBadge(status: string) {
  if (status === "active") return "cl-badge-green";
  if (status === "revoked" || status === "expired") return "cl-badge-red";
  return "cl-badge-amber";
}

function requestBadge(status: string) {
  if (status === "approved") return "cl-badge-green";
  if (status === "denied") return "cl-badge-red";
  return "cl-badge-amber";
}

export default function PatientDisclosurePanel({
  sessionId,
  patientId,
}: {
  sessionId: string;
  patientId: string;
}) {
  const [state, setState] = useState<AsyncState>({ status: "loading" });
  const [reload, setReload] = useState(0);
  const [showAuthorityForm, setShowAuthorityForm] = useState(false);
  const [authorityDraft, setAuthorityDraft] = useState(emptyAuthorityDraft);
  const [requestDraft, setRequestDraft] = useState(emptyRequestDraft);
  const [busy, setBusy] = useState<string | null>(null);
  const [action, setAction] = useState<{
    kind: "authority" | "request";
    id: string;
    verb: "activate" | "revoke" | "approve" | "deny";
    reason: string;
  } | null>(null);
  const [history, setHistory] = useState<
    | {
        kind: "authority";
        id: string;
        events: PatientDisclosureAuthorityEvent[];
      }
    | {
        kind: "request";
        id: string;
        events: PatientDisclosureRequestEvent[];
      }
    | null
  >(null);
  const [historyLoading, setHistoryLoading] = useState<string | null>(null);

  const load = useEffectEvent(async (signal: AbortSignal) => {
    setState({ status: "loading" });
    try {
      const [policy, authorities, requests] = await Promise.all([
        getPatientDisclosurePolicy(sessionId, patientId, signal),
        getPatientDisclosureAuthorities(sessionId, patientId, signal),
        getPatientDisclosureRequests(sessionId, patientId, signal),
      ]);
      setState({ status: "ready", data: { policy, authorities, requests } });
    } catch (error) {
      if (signal.aborted) return;
      setState({ status: "error", message: message(error) });
    }
  });

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [sessionId, patientId, reload]);

  const data = state.status === "ready" ? state.data : null;
  const activeAuthorities = useMemo(
    () =>
      data?.authorities.filter(
        (authority) => authority.effectiveStatus === "active",
      ) ?? [],
    [data],
  );

  async function refresh() {
    const [policy, authorities, requests] = await Promise.all([
      getPatientDisclosurePolicy(sessionId, patientId),
      getPatientDisclosureAuthorities(sessionId, patientId),
      getPatientDisclosureRequests(sessionId, patientId),
    ]);
    setState({ status: "ready", data: { policy, authorities, requests } });
  }

  function toggleScope(
    target: "authority" | "request",
    scopeKey: string,
    selected: boolean,
  ) {
    const update = (keys: string[]) =>
      selected
        ? [...keys, scopeKey]
        : keys.filter((key) => key !== scopeKey);
    if (target === "authority") {
      setAuthorityDraft((current) => ({
        ...current,
        scopeKeys: update(current.scopeKeys),
      }));
    } else {
      setRequestDraft((current) => ({
        ...current,
        scopeKeys: update(current.scopeKeys),
      }));
    }
  }

  async function createAuthority(event: FormEvent) {
    event.preventDefault();
    setBusy("create-authority");
    try {
      await createPatientDisclosureAuthority(sessionId, patientId, {
        authorityType: authorityDraft.authorityType,
        proxyName:
          authorityDraft.authorityType === "proxy"
            ? authorityDraft.proxyName
            : null,
        proxyRelationship:
          authorityDraft.authorityType === "proxy"
            ? authorityDraft.proxyRelationship
            : null,
        purpose: authorityDraft.purpose,
        recipient: authorityDraft.recipient,
        scopeKeys: authorityDraft.scopeKeys,
        effectiveFrom: new Date(
          `${authorityDraft.effectiveDate}T00:00:00`,
        ).toISOString(),
        expiresAt: new Date(
          `${authorityDraft.expiresDate}T23:59:59`,
        ).toISOString(),
        verificationMethod: authorityDraft.verificationMethod,
        verificationReference: authorityDraft.verificationReference,
        reason: authorityDraft.reason,
      });
      await refresh();
      setAuthorityDraft(emptyAuthorityDraft());
      setShowAuthorityForm(false);
      showToast("Pending disclosure authority recorded.", "success");
    } catch (error) {
      showToast(message(error), "error");
    } finally {
      setBusy(null);
    }
  }

  function selectAuthorityForRequest(authorityId: string) {
    const authority = activeAuthorities.find(
      (candidate) => candidate.authorityId === authorityId,
    );
    setRequestDraft(
      authority
        ? {
            authorityId,
            purpose: authority.purpose,
            recipient: authority.recipient,
            scopeKeys: [...authority.scopeKeys],
            reason: "",
          }
        : emptyRequestDraft(),
    );
  }

  async function createRequest(event: FormEvent) {
    event.preventDefault();
    setBusy("create-request");
    try {
      await createPatientDisclosureRequest(
        sessionId,
        patientId,
        requestDraft,
      );
      await refresh();
      setRequestDraft(emptyRequestDraft());
      showToast("Disclosure decision request recorded.", "success");
    } catch (error) {
      showToast(message(error), "error");
    } finally {
      setBusy(null);
    }
  }

  async function submitAction(event: FormEvent) {
    event.preventDefault();
    if (!action) return;
    setBusy(`${action.kind}-${action.id}`);
    try {
      if (action.kind === "authority") {
        const current = data?.authorities.find(
          (authority) => authority.authorityId === action.id,
        );
        if (!current || !["activate", "revoke"].includes(action.verb)) return;
        await transitionPatientDisclosureAuthority(
          sessionId,
          patientId,
          current.authorityId,
          action.verb as "activate" | "revoke",
          current.version,
          action.reason,
        );
        showToast(
          action.verb === "activate"
            ? "Disclosure authority activated."
            : "Disclosure authority revoked.",
          "success",
        );
      } else {
        const current = data?.requests.find(
          (request) => request.requestId === action.id,
        );
        if (!current || !["approve", "deny"].includes(action.verb)) return;
        await decidePatientDisclosureRequest(
          sessionId,
          patientId,
          current.requestId,
          action.verb as "approve" | "deny",
          current.version,
          action.reason,
        );
        showToast(
          action.verb === "approve"
            ? "Disclosure decision approved."
            : "Disclosure decision denied.",
          "success",
        );
      }
      await refresh();
      setAction(null);
    } catch (error) {
      showToast(message(error), "error");
      await refresh();
    } finally {
      setBusy(null);
    }
  }

  async function openHistory(
    kind: "authority" | "request",
    id: string,
  ) {
    setHistoryLoading(`${kind}-${id}`);
    try {
      if (kind === "authority") {
        setHistory({
          kind,
          id,
          events: await getPatientDisclosureAuthorityHistory(
            sessionId,
            patientId,
            id,
          ),
        });
      } else {
        setHistory({
          kind,
          id,
          events: await getPatientDisclosureRequestHistory(
            sessionId,
            patientId,
            id,
          ),
        });
      }
    } catch (error) {
      showToast(message(error), "error");
    } finally {
      setHistoryLoading(null);
    }
  }

  return (
    <section
      className="cl-card patient-disclosure"
      aria-labelledby="patient-disclosure-heading"
    >
      <div className="cl-card-header patient-disclosure-heading">
        <div>
          <p className="practice-governance-kicker">
            SEC-03 local foundation
          </p>
          <h2 className="cl-card-title" id="patient-disclosure-heading">
            Consent, authority, and disclosure decisions
          </h2>
          <p className="cl-empty-text">
            Record verified authority, exact purpose, recipient, scope,
            effective dates, and review evidence before approving a local
            disclosure decision.
          </p>
        </div>
        {data && <code>{data.policy.revision}</code>}
      </div>

      {state.status === "loading" && (
        <div className="patient-disclosure-message" role="status">
          Loading consent and disclosure evidence…
        </div>
      )}
      {state.status === "error" && (
        <div className="patient-disclosure-message" role="alert">
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

      {data && (
        <>
          <aside className="patient-disclosure-boundary" role="note">
            <strong>Local decision boundary:</strong> contact or guardian facts
            do not establish proxy authority. Approval here does not package,
            download, transmit, or deliver records. Retention, legal hold,
            production policy, and delivery channels remain separate.
          </aside>
          <aside className="patient-disclosure-emergency" role="note">
            <div>
              <strong>Emergency access</strong>
              <span>{readable(data.policy.emergencyAccess.state)}</span>
            </div>
            <p>{data.policy.emergencyAccess.reason}</p>
          </aside>

          <div className="patient-disclosure-section-heading">
            <div>
              <h3>Authority register</h3>
              <p>
                Pending authority must be deliberately activated. Expiry and
                revocation block new requests and approval.
              </p>
            </div>
            <button
              className="cl-btn-secondary"
              type="button"
              aria-expanded={showAuthorityForm}
              onClick={() => setShowAuthorityForm((value) => !value)}
            >
              {showAuthorityForm ? "Close authority form" : "Record authority"}
            </button>
          </div>

          {showAuthorityForm && (
            <form
              className="patient-disclosure-form"
              aria-label="Record disclosure authority"
              onSubmit={createAuthority}
            >
              <div className="patient-disclosure-form-grid">
                <label className="cl-admin-field">
                  <span>Authority type</span>
                  <select
                    className="ne-input"
                    value={authorityDraft.authorityType}
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        authorityType: event.target.value as
                          | "patient"
                          | "proxy",
                        proxyName: "",
                        proxyRelationship: "",
                      }))
                    }
                  >
                    {data.policy.authorityTypes.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                {authorityDraft.authorityType === "proxy" && (
                  <>
                    <label className="cl-admin-field">
                      <span>Proxy name</span>
                      <input
                        className="ne-input"
                        value={authorityDraft.proxyName}
                        maxLength={120}
                        required
                        onChange={(event) =>
                          setAuthorityDraft((current) => ({
                            ...current,
                            proxyName: event.target.value,
                          }))
                        }
                      />
                    </label>
                    <label className="cl-admin-field">
                      <span>Proxy relationship</span>
                      <input
                        className="ne-input"
                        value={authorityDraft.proxyRelationship}
                        maxLength={80}
                        required
                        onChange={(event) =>
                          setAuthorityDraft((current) => ({
                            ...current,
                            proxyRelationship: event.target.value,
                          }))
                        }
                      />
                    </label>
                  </>
                )}
                <label className="cl-admin-field">
                  <span>Purpose</span>
                  <input
                    className="ne-input"
                    value={authorityDraft.purpose}
                    maxLength={120}
                    required
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        purpose: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Exact recipient</span>
                  <input
                    className="ne-input"
                    value={authorityDraft.recipient}
                    maxLength={160}
                    required
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        recipient: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Effective date</span>
                  <input
                    className="ne-input"
                    type="date"
                    value={authorityDraft.effectiveDate}
                    required
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        effectiveDate: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Expiration date</span>
                  <input
                    className="ne-input"
                    type="date"
                    value={authorityDraft.expiresDate}
                    min={authorityDraft.effectiveDate}
                    required
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        expiresDate: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="cl-admin-field">
                  <span>Verification method</span>
                  <select
                    className="ne-input"
                    value={authorityDraft.verificationMethod}
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        verificationMethod: event.target.value,
                      }))
                    }
                  >
                    {data.policy.verificationMethods.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>Verification reference</span>
                  <input
                    className="ne-input"
                    value={authorityDraft.verificationReference}
                    maxLength={160}
                    required
                    onChange={(event) =>
                      setAuthorityDraft((current) => ({
                        ...current,
                        verificationReference: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <fieldset className="patient-disclosure-scopes">
                <legend>Permitted record scope</legend>
                {data.policy.scopes.map((scope) => (
                  <label key={scope.key}>
                    <input
                      type="checkbox"
                      checked={authorityDraft.scopeKeys.includes(scope.key)}
                      onChange={(event) =>
                        toggleScope(
                          "authority",
                          scope.key,
                          event.target.checked,
                        )
                      }
                    />
                    <span>
                      <strong>{scope.label}</strong>
                      <small>{scope.description}</small>
                    </span>
                  </label>
                ))}
              </fieldset>
              <label className="cl-admin-field">
                <span>Why this authority is being recorded</span>
                <textarea
                  className="ne-input"
                  value={authorityDraft.reason}
                  maxLength={500}
                  required
                  onChange={(event) =>
                    setAuthorityDraft((current) => ({
                      ...current,
                      reason: event.target.value,
                    }))
                  }
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    busy !== null || authorityDraft.scopeKeys.length === 0
                  }
                >
                  {busy === "create-authority"
                    ? "Recording…"
                    : "Record pending authority"}
                </button>
              </div>
            </form>
          )}

          {data.authorities.length === 0 ? (
            <p className="patient-disclosure-empty">
              No disclosure authority has been recorded for this patient.
            </p>
          ) : (
            <ul
              className="patient-disclosure-list"
              aria-label="Disclosure authorities"
            >
              {data.authorities.map((authority) => (
                <li key={authority.authorityId}>
                  <div className="patient-disclosure-item-heading">
                    <div>
                      <strong>
                        {authority.authorityType === "proxy"
                          ? `${authority.proxyName} · ${authority.proxyRelationship}`
                          : "Patient authority"}
                      </strong>
                      <span>
                        {authority.purpose} · {authority.recipient}
                      </span>
                    </div>
                    <span
                      className={`cl-badge ${authorityBadge(authority.effectiveStatus)}`}
                    >
                      {readable(authority.effectiveStatus)}
                    </span>
                  </div>
                  <dl className="patient-disclosure-facts">
                    <div>
                      <dt>Scope</dt>
                      <dd>{authority.scopeKeys.map(readable).join(", ")}</dd>
                    </div>
                    <div>
                      <dt>Effective window</dt>
                      <dd>
                        {new Date(authority.effectiveFrom).toLocaleDateString()}{" "}
                        – {new Date(authority.expiresAt).toLocaleDateString()}
                      </dd>
                    </div>
                    <div>
                      <dt>Verification</dt>
                      <dd>
                        {readable(authority.verificationMethod)} ·{" "}
                        {authority.verificationReference}
                      </dd>
                    </div>
                    <div>
                      <dt>Evidence</dt>
                      <dd>
                        v{authority.version} · {authority.updatedBy} ·{" "}
                        {new Date(authority.updatedAt).toLocaleString()}
                      </dd>
                    </div>
                  </dl>
                  <div className="cl-inline-form-actions">
                    {authority.allowedActions.map((verb) => (
                      <button
                        className={
                          verb === "revoke"
                            ? "cl-btn-danger"
                            : "cl-btn-secondary"
                        }
                        type="button"
                        key={verb}
                        onClick={() =>
                          setAction({
                            kind: "authority",
                            id: authority.authorityId,
                            verb: verb as "activate" | "revoke",
                            reason: "",
                          })
                        }
                      >
                        {verb === "activate" ? "Activate" : "Revoke"}
                      </button>
                    ))}
                    <button
                      className="cl-btn-ghost"
                      type="button"
                      disabled={
                        historyLoading === `authority-${authority.authorityId}`
                      }
                      onClick={() =>
                        void openHistory("authority", authority.authorityId)
                      }
                    >
                      {historyLoading ===
                      `authority-${authority.authorityId}`
                        ? "Loading history…"
                        : "History"}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          {action?.kind === "authority" && (
            <form
              className="patient-disclosure-action"
              aria-label={`${action.verb} disclosure authority`}
              onSubmit={submitAction}
            >
              <label className="cl-admin-field">
                <span>
                  Reason to {action.verb} this disclosure authority
                </span>
                <textarea
                  className="ne-input"
                  value={action.reason}
                  maxLength={500}
                  required
                  onChange={(event) =>
                    setAction((current) =>
                      current
                        ? { ...current, reason: event.target.value }
                        : current,
                    )
                  }
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className={
                    action.verb === "revoke"
                      ? "cl-btn-danger"
                      : "cl-btn-primary"
                  }
                  type="submit"
                  disabled={busy !== null}
                >
                  Confirm {action.verb}
                </button>
                <button
                  className="cl-btn-ghost"
                  type="button"
                  onClick={() => setAction(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}

          <div className="patient-disclosure-section-heading">
            <div>
              <h3>Disclosure decision queue</h3>
              <p>
                A request must exactly match one active authority. Approval
                records a decision, not record delivery.
              </p>
            </div>
          </div>

          <form
            className="patient-disclosure-form"
            aria-label="Request disclosure decision"
            onSubmit={createRequest}
          >
            <div className="patient-disclosure-form-grid">
              <label className="cl-admin-field">
                <span>Active authority</span>
                <select
                  className="ne-input"
                  value={requestDraft.authorityId}
                  required
                  onChange={(event) =>
                    selectAuthorityForRequest(event.target.value)
                  }
                >
                  <option value="">Select active authority</option>
                  {activeAuthorities.map((authority) => (
                    <option
                      key={authority.authorityId}
                      value={authority.authorityId}
                    >
                      {authority.authorityType === "proxy"
                        ? authority.proxyName
                        : "Patient"}{" "}
                      · {authority.purpose} · {authority.recipient}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Purpose</span>
                <input
                  className="ne-input"
                  value={requestDraft.purpose}
                  maxLength={120}
                  required
                  onChange={(event) =>
                    setRequestDraft((current) => ({
                      ...current,
                      purpose: event.target.value,
                    }))
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Exact recipient</span>
                <input
                  className="ne-input"
                  value={requestDraft.recipient}
                  maxLength={160}
                  required
                  onChange={(event) =>
                    setRequestDraft((current) => ({
                      ...current,
                      recipient: event.target.value,
                    }))
                  }
                />
              </label>
            </div>
            <fieldset className="patient-disclosure-scopes">
              <legend>Requested record scope</legend>
              {data.policy.scopes.map((scope) => {
                const authority = activeAuthorities.find(
                  (item) => item.authorityId === requestDraft.authorityId,
                );
                const allowed = authority?.scopeKeys.includes(scope.key) ?? false;
                return (
                  <label key={scope.key}>
                    <input
                      type="checkbox"
                      checked={requestDraft.scopeKeys.includes(scope.key)}
                      disabled={!allowed}
                      onChange={(event) =>
                        toggleScope(
                          "request",
                          scope.key,
                          event.target.checked,
                        )
                      }
                    />
                    <span>
                      <strong>{scope.label}</strong>
                      <small>
                        {allowed
                          ? scope.description
                          : "Not included in the selected authority."}
                      </small>
                    </span>
                  </label>
                );
              })}
            </fieldset>
            <label className="cl-admin-field">
              <span>Why this disclosure decision is requested</span>
              <textarea
                className="ne-input"
                value={requestDraft.reason}
                maxLength={500}
                required
                onChange={(event) =>
                  setRequestDraft((current) => ({
                    ...current,
                    reason: event.target.value,
                  }))
                }
              />
            </label>
            {activeAuthorities.length === 0 && (
              <p className="patient-disclosure-empty">
                Activate non-expired authority before requesting a disclosure
                decision.
              </p>
            )}
            <div className="cl-inline-form-actions">
              <button
                className="cl-btn-primary"
                type="submit"
                disabled={
                  busy !== null ||
                  !requestDraft.authorityId ||
                  requestDraft.scopeKeys.length === 0
                }
              >
                {busy === "create-request"
                  ? "Recording…"
                  : "Request decision"}
              </button>
            </div>
          </form>

          {data.requests.length === 0 ? (
            <p className="patient-disclosure-empty">
              No disclosure decisions have been requested.
            </p>
          ) : (
            <ul
              className="patient-disclosure-list"
              aria-label="Disclosure decision requests"
            >
              {data.requests.map((request) => (
                <li key={request.requestId}>
                  <div className="patient-disclosure-item-heading">
                    <div>
                      <strong>{request.purpose}</strong>
                      <span>{request.recipient}</span>
                    </div>
                    <span
                      className={`cl-badge ${requestBadge(request.status)}`}
                    >
                      {request.status}
                    </span>
                  </div>
                  <dl className="patient-disclosure-facts">
                    <div>
                      <dt>Scope</dt>
                      <dd>{request.scopeKeys.map(readable).join(", ")}</dd>
                    </div>
                    <div>
                      <dt>Authority</dt>
                      <dd>
                        {readable(request.authorityEffectiveStatus)} · v
                        {request.authorityVersion}
                      </dd>
                    </div>
                    <div>
                      <dt>Requested</dt>
                      <dd>
                        {request.requestedBy} ·{" "}
                        {new Date(request.requestedAt).toLocaleString()}
                      </dd>
                    </div>
                    <div>
                      <dt>Decision</dt>
                      <dd>
                        {request.decidedAt
                          ? `${request.decidedBy} · ${new Date(request.decidedAt).toLocaleString()} · ${request.decisionReason}`
                          : `Pending · request v${request.version}`}
                      </dd>
                    </div>
                  </dl>
                  <div className="cl-inline-form-actions">
                    {request.allowedActions.map((verb) => (
                      <button
                        className={
                          verb === "deny" ? "cl-btn-danger" : "cl-btn-secondary"
                        }
                        type="button"
                        key={verb}
                        onClick={() =>
                          setAction({
                            kind: "request",
                            id: request.requestId,
                            verb: verb as "approve" | "deny",
                            reason: "",
                          })
                        }
                      >
                        {verb === "approve" ? "Approve decision" : "Deny"}
                      </button>
                    ))}
                    <button
                      className="cl-btn-ghost"
                      type="button"
                      disabled={
                        historyLoading === `request-${request.requestId}`
                      }
                      onClick={() =>
                        void openHistory("request", request.requestId)
                      }
                    >
                      {historyLoading === `request-${request.requestId}`
                        ? "Loading history…"
                        : "History"}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          {action?.kind === "request" && (
            <form
              className="patient-disclosure-action"
              aria-label={`${action.verb} disclosure decision`}
              onSubmit={submitAction}
            >
              <label className="cl-admin-field">
                <span>Reason for this {action.verb} decision</span>
                <textarea
                  className="ne-input"
                  value={action.reason}
                  maxLength={500}
                  required
                  onChange={(event) =>
                    setAction((current) =>
                      current
                        ? { ...current, reason: event.target.value }
                        : current,
                    )
                  }
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className={
                    action.verb === "deny"
                      ? "cl-btn-danger"
                      : "cl-btn-primary"
                  }
                  type="submit"
                  disabled={busy !== null}
                >
                  Confirm {action.verb}
                </button>
                <button
                  className="cl-btn-ghost"
                  type="button"
                  onClick={() => setAction(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}

          {history && (
            <section
              className="patient-disclosure-history"
              aria-label={`${history.kind} disclosure history`}
            >
              <div className="patient-disclosure-section-heading">
                <div>
                  <h3>
                    {history.kind === "authority"
                      ? "Authority history"
                      : "Decision history"}
                  </h3>
                  <p>Newest-first immutable actor, reason, state, and version.</p>
                </div>
                <button
                  className="cl-btn-ghost"
                  type="button"
                  onClick={() => setHistory(null)}
                >
                  Close history
                </button>
              </div>
              <ol>
                {history.events.map((event) => (
                  <li key={event.eventId}>
                    <div>
                      <strong>{readable(event.action)}</strong>
                      <span>
                        {event.fromStatus
                          ? `${readable(event.fromStatus)} → `
                          : ""}
                        {readable(event.toStatus)} · v{event.version}
                      </span>
                    </div>
                    <p>{event.reason}</p>
                    <small>
                      {event.username} ·{" "}
                      {new Date(event.occurredAt).toLocaleString()}
                      {"authorityVersion" in event
                        ? ` · authority v${event.authorityVersion} ${readable(event.authorityEffectiveStatus)}`
                        : ""}
                    </small>
                  </li>
                ))}
              </ol>
            </section>
          )}

          <details className="patient-disclosure-policy">
            <summary>Policy boundaries and next decisions</summary>
            <ul>
              {data.policy.boundaries.map((boundary) => (
                <li key={boundary}>{boundary}</li>
              ))}
            </ul>
          </details>
        </>
      )}
    </section>
  );
}
