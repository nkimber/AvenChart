// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import {
  useEffect,
  useEffectEvent,
  useMemo,
  useState,
  type FormEvent,
} from "react";
import {
  createPracticeSettingChangeRequest,
  getPracticeSettingDelegations,
  getEffectivePracticeSettings,
  getPracticeSettingChangeRequest,
  getPracticeSettingChangeRequestImpactPreview,
  getPracticeSettingChangeRequests,
  getPracticeSettingRegistry,
  grantPracticeSettingDelegation,
  transitionPracticeSettingChangeRequest,
  type EffectivePracticeSettingItem,
  type PracticeSettingImpactPreview,
  type PracticeSettingChangeRequestAction,
  type PracticeSettingChangeRequestDetail,
  type PracticeSettingChangeRequestsResponse,
  type PracticeSettingChangeRequestStatus,
  type PracticeSettingItem,
  type PracticeSettingDelegation,
  type PracticeSettingRegistryItem,
} from "../../api.ts";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

type Props = {
  sessionId: string;
  settings: PracticeSettingItem[];
  onSettingsChanged: () => Promise<void>;
  onOpenHistory: (key: string) => void;
};

const PAGE_SIZE = 8;

const statusLabels: Record<PracticeSettingChangeRequestStatus, string> = {
  draft: "Draft",
  submitted: "Awaiting review",
  approved: "Approved",
  rejected: "Rejected",
  activated: "Activated",
  cancelled: "Cancelled",
};

function statusBadgeClass(status: PracticeSettingChangeRequestStatus) {
  if (status === "activated") return "cl-badge-green";
  if (status === "rejected") return "cl-badge-red";
  if (status === "submitted" || status === "approved")
    return "cl-badge-amber";
  return "cl-badge-muted";
}

function formatDateTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback;
}

export default function PracticeSettingGovernance({
  sessionId,
  settings,
  onSettingsChanged,
  onOpenHistory,
}: Props) {
  const [listState, setListState] =
    useState<AsyncState<PracticeSettingChangeRequestsResponse>>({
      status: "loading",
    });
  const [statusFilter, setStatusFilter] = useState<
    "all" | "open" | PracticeSettingChangeRequestStatus
  >("open");
  const [settingFilter, setSettingFilter] = useState("");
  const [offset, setOffset] = useState(0);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [showProposal, setShowProposal] = useState(false);
  const [proposal, setProposal] = useState({
    settingKey: "",
    value: "",
    reason: "",
    facilityId: null as number | null,
  });
  const [savingProposal, setSavingProposal] = useState(false);
  const [proposalError, setProposalError] = useState<string | null>(null);
  const [detailState, setDetailState] =
    useState<AsyncState<PracticeSettingChangeRequestDetail> | null>(null);
  const [impactState, setImpactState] =
    useState<AsyncState<PracticeSettingImpactPreview> | null>(null);
  const [transitionNote, setTransitionNote] = useState("");
  const [transitioning, setTransitioning] =
    useState<PracticeSettingChangeRequestAction | null>(null);
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [effectiveSettings, setEffectiveSettings] = useState<
    Map<string, EffectivePracticeSettingItem>
  >(new Map());
  const [registryState, setRegistryState] = useState<
    AsyncState<Map<string, PracticeSettingRegistryItem>>
  >({ status: "loading" });
  const [delegationState, setDelegationState] = useState<
    AsyncState<PracticeSettingDelegation[]>
  >({ status: "loading" });
  const [delegation, setDelegation] = useState({
    username: "",
    settingKey: "",
    facilityId: "",
    expiresAt: "",
    reason: "",
  });
  const [delegationError, setDelegationError] = useState<string | null>(null);
  const [savingDelegation, setSavingDelegation] = useState(false);

  const settingByKey = useMemo(
    () => new Map(settings.map((setting) => [setting.key, setting])),
    [settings],
  );

  const loadRequests = useEffectEvent(async (signal?: AbortSignal) => {
    setListState({ status: "loading" });
    try {
      setListState({
        status: "ready",
        data: await getPracticeSettingChangeRequests(
          sessionId,
          {
            settingKey: settingFilter || undefined,
            status: statusFilter,
            offset,
            limit: PAGE_SIZE,
          },
          signal,
        ),
      });
    } catch (error) {
      if (signal?.aborted) return;
      setListState({
        status: "error",
        message: errorMessage(
          error,
          "Could not load configuration change requests.",
        ),
      });
    }
  });

  useEffect(() => {
    const controller = new AbortController();
    void loadRequests(controller.signal);
    return () => controller.abort();
  }, [sessionId, settingFilter, statusFilter, offset, refreshVersion]);

  const defaultFacilityId = Number(
    settings.find((setting) => setting.key === "practice.default-facility-id")
      ?.value,
  );

  useEffect(() => {
    let active = true;
    void getEffectivePracticeSettings(
      sessionId,
      Number.isInteger(defaultFacilityId) && defaultFacilityId > 0
        ? defaultFacilityId
        : undefined,
    )
      .then((result) => {
        if (!active) return;
        setEffectiveSettings(new Map(result.settings.map((setting) => [setting.key, setting])));
      })
      .catch(() => {
        if (active) setEffectiveSettings(new Map());
      });
    return () => {
      active = false;
    };
  }, [defaultFacilityId, sessionId, settings]);

  useEffect(() => {
    let active = true;
    void getPracticeSettingRegistry(sessionId)
      .then((result) => {
        if (active) {
          setRegistryState({
            status: "ready",
            data: new Map(result.items.map((item) => [item.key, item])),
          });
        }
      })
      .catch((error) => {
        if (active) {
          setRegistryState({
            status: "error",
            message: errorMessage(error, "Could not load the configuration registry."),
          });
        }
      });
    return () => {
      active = false;
    };
  }, [sessionId]);

  useEffect(() => {
    let active = true;
    void getPracticeSettingDelegations(sessionId)
      .then((items) => {
        if (active) setDelegationState({ status: "ready", data: items });
      })
      .catch((error) => {
        if (active) {
          setDelegationState({
            status: "error",
            message: errorMessage(error, "Could not load delegated authority."),
          });
        }
      });
    return () => {
      active = false;
    };
  }, [refreshVersion, sessionId]);

  useEffect(() => {
    if (proposal.settingKey || settings.length === 0) return;
    const first = settings[0]!;
    setProposal({
      settingKey: first.key,
      value: first.value,
      reason: "",
      facilityId: null,
    });
  }, [proposal.settingKey, settings]);

  function beginProposal(setting: PracticeSettingItem) {
    setProposal({
      settingKey: setting.key,
      value: setting.value,
      reason: "",
      facilityId: null,
    });
    setProposalError(null);
    setShowProposal(true);
  }

  function selectProposalSetting(key: string) {
    const setting = settingByKey.get(key);
    setProposal({
      settingKey: key,
      value:
        proposal.facilityId && effectiveSettings.get(key)?.value
          ? effectiveSettings.get(key)!.value
          : setting?.value ?? "",
      reason: "",
      facilityId: proposal.facilityId,
    });
    setProposalError(null);
  }

  async function submitProposal(event: FormEvent) {
    event.preventDefault();
    const active = settingByKey.get(proposal.settingKey);
    if (!active) {
      setProposalError("Select an available practice setting.");
      return;
    }
    const scopeBaseline = proposal.facilityId
      ? effectiveSettings.get(proposal.settingKey)?.value ?? active.value
      : active.value;
    if (proposal.value.trim() === scopeBaseline) {
      setProposalError("The proposed value must differ from the active value.");
      return;
    }
    if (!proposal.reason.trim()) {
      setProposalError("A change reason is required.");
      return;
    }

    setSavingProposal(true);
    setProposalError(null);
    try {
      const detail = await createPracticeSettingChangeRequest(
        sessionId,
        proposal.settingKey,
        {
          value: proposal.value,
          reason: proposal.reason,
          facilityId: proposal.facilityId,
        },
      );
      setDetailState({ status: "ready", data: detail });
      setImpactState({ status: "loading" });
      try {
        setImpactState({
          status: "ready",
          data: await getPracticeSettingChangeRequestImpactPreview(
            sessionId,
            detail.request.requestId,
          ),
        });
      } catch (error) {
        setImpactState({
          status: "error",
          message: errorMessage(error, "Could not calculate the local impact preview."),
        });
      }
      setTransitionNote("");
      setTransitionError(null);
      setShowProposal(false);
      setOffset(0);
      setRefreshVersion((version) => version + 1);
    } catch (error) {
      setProposalError(
        errorMessage(error, "Could not create the change request."),
      );
    } finally {
      setSavingProposal(false);
    }
  }

  async function submitDelegation(event: FormEvent) {
    event.preventDefault();
    const facilityId = Number(delegation.facilityId);
    if (!delegation.username.trim() || !delegation.settingKey || !Number.isInteger(facilityId) || facilityId <= 0 || !delegation.reason.trim()) {
      setDelegationError("Username, setting, active facility ID, and a reason are required.");
      return;
    }
    setSavingDelegation(true);
    setDelegationError(null);
    try {
      await grantPracticeSettingDelegation(sessionId, {
        username: delegation.username.trim(),
        settingKey: delegation.settingKey,
        facilityId,
        expiresAt: delegation.expiresAt ? new Date(delegation.expiresAt).toISOString() : null,
        reason: delegation.reason.trim(),
      });
      setDelegation({ username: "", settingKey: delegation.settingKey, facilityId: "", expiresAt: "", reason: "" });
      setRefreshVersion((version) => version + 1);
    } catch (error) {
      setDelegationError(errorMessage(error, "Could not grant delegated authority."));
    } finally {
      setSavingDelegation(false);
    }
  }

  async function openRequest(requestId: string) {
    setDetailState({ status: "loading" });
    setImpactState({ status: "loading" });
    setTransitionError(null);
    setTransitionNote("");
    try {
      const [detail, impact] = await Promise.all([
        getPracticeSettingChangeRequest(sessionId, requestId),
        getPracticeSettingChangeRequestImpactPreview(sessionId, requestId),
      ]);
      setDetailState({ status: "ready", data: detail });
      setImpactState({ status: "ready", data: impact });
    } catch (error) {
      setDetailState({
        status: "error",
        message: errorMessage(error, "Could not load the change request."),
      });
      setImpactState(null);
    }
  }

  async function transitionRequest(action: PracticeSettingChangeRequestAction) {
    if (detailState?.status !== "ready" || transitioning) return;
    if (
      (action === "reject" || action === "cancel") &&
      !transitionNote.trim()
    ) {
      setTransitionError(
        `A transition note is required to ${action} this request.`,
      );
      return;
    }

    setTransitioning(action);
    setTransitionError(null);
    try {
      const next = await transitionPracticeSettingChangeRequest(
        sessionId,
        detailState.data.request.requestId,
        action,
        {
          note: transitionNote.trim() || null,
          expectedVersion: detailState.data.request.version,
        },
      );
      setDetailState({ status: "ready", data: next });
      setImpactState({
        status: "ready",
        data: await getPracticeSettingChangeRequestImpactPreview(
          sessionId,
          next.request.requestId,
        ),
      });
      setTransitionNote("");
      setRefreshVersion((version) => version + 1);
      if (action === "activate") {
        await onSettingsChanged();
        const effective = await getEffectivePracticeSettings(
          sessionId,
          Number.isInteger(defaultFacilityId) && defaultFacilityId > 0
            ? defaultFacilityId
            : undefined,
        );
        setEffectiveSettings(new Map(effective.settings.map((setting) => [setting.key, setting])));
      }
    } catch (error) {
      setTransitionError(
        errorMessage(error, `Could not ${action} the change request.`),
      );
      try {
        setDetailState({
          status: "ready",
          data: await getPracticeSettingChangeRequest(
            sessionId,
            detailState.data.request.requestId,
          ),
        });
      } catch {
        // Keep the actionable mutation error visible if refresh also fails.
      }
    } finally {
      setTransitioning(null);
    }
  }

  const selected =
    detailState?.status === "ready" ? detailState.data.request : null;
  const canCancel =
    selected?.status === "draft" ||
    selected?.status === "submitted" ||
    selected?.status === "approved";

  return (
    <div className="practice-governance" aria-label="Practice configuration governance">
      <div className="practice-governance-header">
        <div>
          <h2 className="cl-card-title">Practice configuration governance</h2>
          <p className="clinician-page-subtitle">
            Proposals remain inactive until they are submitted, approved, and
            deliberately activated. Every transition retains the authenticated
            actor, time, note, and loaded request version.
          </p>
        </div>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() => {
            const first = settings[0];
            if (first) beginProposal(first);
          }}
          disabled={settings.length === 0 || showProposal}
        >
          Propose change
        </button>
      </div>

      <div className="practice-governance-boundary" role="note">
        <strong>Current local boundary:</strong> the same authorized
        administrator may submit, approve, and activate. Independent approver
        matrices, effective dates, and impact preview remain owner-governed
        ADM-01/ADM-02 work. A delegate may only create and submit their own
        facility-scoped draft; administrators retain review and activation.
        Effective
        values below now disclose their system or default-facility source. The
        older direct-update API remains compatibility-only and is not used by
        this screen.
      </div>

      <form className="practice-change-form" onSubmit={submitDelegation}>
        <div className="practice-change-form-heading">
          <div>
            <p className="cl-form-section-label">Delegated draft authority</p>
            <p className="cl-admin-form-copy">
              Grant a time-bounded user-and-facility scope. It cannot approve,
              activate, or bypass the governed workflow.
            </p>
          </div>
        </div>
        <label className="cl-admin-field">
          <span>Delegate username</span>
          <input className="ne-input" value={delegation.username} onChange={(event) => setDelegation({ ...delegation, username: event.target.value })} disabled={savingDelegation} />
        </label>
        <label className="cl-admin-field">
          <span>Practice setting</span>
          <select className="ne-input" value={delegation.settingKey} onChange={(event) => setDelegation({ ...delegation, settingKey: event.target.value })} disabled={savingDelegation}>
            <option value="">Select a setting</option>
            {settings.map((setting) => <option key={setting.key} value={setting.key}>{setting.label}</option>)}
          </select>
        </label>
        <label className="cl-admin-field">
          <span>Facility ID</span>
          <input className="ne-input" type="number" min="1" value={delegation.facilityId} onChange={(event) => setDelegation({ ...delegation, facilityId: event.target.value })} disabled={savingDelegation} />
        </label>
        <label className="cl-admin-field">
          <span>Expiry (optional)</span>
          <input className="ne-input" type="datetime-local" value={delegation.expiresAt} onChange={(event) => setDelegation({ ...delegation, expiresAt: event.target.value })} disabled={savingDelegation} />
        </label>
        <label className="cl-admin-field">
          <span>Reason</span>
          <input className="ne-input" value={delegation.reason} onChange={(event) => setDelegation({ ...delegation, reason: event.target.value })} disabled={savingDelegation} />
        </label>
        {delegationError && <p className="cl-form-error" role="alert">{delegationError}</p>}
        <button className="cl-btn-secondary" type="submit" disabled={savingDelegation}>Grant draft authority</button>
        {delegationState.status === "loading" && <p className="cl-empty-text">Loading delegated authority…</p>}
        {delegationState.status === "error" && <p className="cl-form-error" role="alert">{delegationState.message}</p>}
        {delegationState.status === "ready" && (
          <p className="cl-empty-text">
            Active delegations: {delegationState.data.filter((item) => item.active && (!item.expiresAt || new Date(item.expiresAt) > new Date())).map((item) => `${item.username} · ${item.settingKey} · facility ${item.facilityId}`).join("; ") || "none"}.
          </p>
        )}
      </form>

      <div className="practice-governance-boundary" role="note">
        <strong>Local configuration registry:</strong>{" "}
        {registryState.status === "loading" && "loading metadata…"}
        {registryState.status === "error" && registryState.message}
        {registryState.status === "ready" &&
          "every adopted setting is non-secret, permits system/facility scope, and explicitly prohibits break-glass activation."}
      </div>

      <div className="practice-setting-grid" aria-label="Active practice settings">
        {settings.map((setting) => (
          <article className="practice-setting-card" key={setting.key}>
            {registryState.status === "ready" && registryState.data.get(setting.key) && (
              <p className="cl-empty-text">
                Registry: {registryState.data.get(setting.key)?.impactClass} ·{" "}
                {registryState.data.get(setting.key)?.allowedScopes.join(" / ")} · no break-glass
              </p>
            )}
            {effectiveSettings.get(setting.key) && (
              <p className="cl-empty-text">
                Effective source: {effectiveSettings.get(setting.key)?.sourceScope}
                {effectiveSettings.get(setting.key)?.sourceFacilityId
                  ? ` facility ${effectiveSettings.get(setting.key)?.sourceFacilityId}`
                  : " default"}
              </p>
            )}
            <p className="cl-form-section-label">{setting.label}</p>
            <p className="practice-setting-value">{setting.value}</p>
            <p className="cl-empty-text">
              Active since {formatDateTime(setting.updatedAt)} ·{" "}
              {setting.updatedBy}
            </p>
            <div className="practice-setting-actions">
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => beginProposal(setting)}
              >
                Propose change
              </button>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => onOpenHistory(setting.key)}
              >
                Revision history
              </button>
            </div>
          </article>
        ))}
      </div>

      {showProposal && (
        <form className="practice-change-form" onSubmit={submitProposal}>
          <div className="practice-change-form-heading">
            <div>
              <p className="cl-form-section-label">New inactive proposal</p>
              <p className="cl-admin-form-copy">
                Creating a draft does not change the active setting. A facility
                proposal activates only a local override for the selected scope.
              </p>
            </div>
            <button
              className="cl-icon-button"
              type="button"
              aria-label="Close proposal"
              onClick={() => {
                setShowProposal(false);
                setProposalError(null);
              }}
            >
              ×
            </button>
          </div>
          <label className="cl-admin-field">
            <span>Practice setting</span>
            <select
              className="ne-input"
              aria-label="Proposal practice setting"
              value={proposal.settingKey}
              onChange={(event) => selectProposalSetting(event.target.value)}
              disabled={savingProposal}
            >
              {settings.map((setting) => (
                <option key={setting.key} value={setting.key}>
                  {setting.label}
                </option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Configuration scope</span>
            <select
              className="ne-input"
              aria-label="Proposal configuration scope"
              value={proposal.facilityId ?? "system"}
              onChange={(event) => {
                const facilityId =
                  event.target.value === "system"
                    ? null
                    : Number(event.target.value);
                const active = settingByKey.get(proposal.settingKey);
                setProposal((current) => ({
                  ...current,
                  facilityId,
                  value:
                    facilityId &&
                    effectiveSettings.get(current.settingKey)?.value
                      ? effectiveSettings.get(current.settingKey)!.value
                      : active?.value ?? "",
                }));
              }}
            >
              <option value="system">System default</option>
              {Number.isInteger(defaultFacilityId) && defaultFacilityId > 0 && (
                <option value={defaultFacilityId}>
                  Default facility ({defaultFacilityId})
                </option>
              )}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Active value</span>
            <input
              className="ne-input"
              value={
                proposal.facilityId
                  ? effectiveSettings.get(proposal.settingKey)?.value ??
                    settingByKey.get(proposal.settingKey)?.value ??
                    ""
                  : settingByKey.get(proposal.settingKey)?.value ?? ""
              }
              readOnly
            />
          </label>
          <label className="cl-admin-field">
            <span>Proposed value</span>
            <input
              className="ne-input"
              value={proposal.value}
              onChange={(event) =>
                setProposal((current) => ({
                  ...current,
                  value: event.target.value,
                }))
              }
              maxLength={250}
              required
              disabled={savingProposal}
            />
          </label>
          <label className="cl-admin-field practice-change-reason">
            <span>Change reason</span>
            <textarea
              className="ne-input"
              value={proposal.reason}
              onChange={(event) =>
                setProposal((current) => ({
                  ...current,
                  reason: event.target.value,
                }))
              }
              maxLength={1000}
              rows={3}
              required
              disabled={savingProposal}
            />
          </label>
          {proposalError && (
            <div className="error-banner practice-change-error" role="alert">
              {proposalError}
            </div>
          )}
          <button
            className="cl-btn-primary"
            type="submit"
            disabled={savingProposal}
          >
            {savingProposal ? "Creating…" : "Create inactive draft"}
          </button>
        </form>
      )}

      <div className="practice-request-toolbar">
        <label className="cl-admin-field">
          <span>Setting</span>
          <select
            className="ne-input"
            value={settingFilter}
            onChange={(event) => {
              setSettingFilter(event.target.value);
              setOffset(0);
            }}
          >
            <option value="">All settings</option>
            {settings.map((setting) => (
              <option key={setting.key} value={setting.key}>
                {setting.label}
              </option>
            ))}
          </select>
        </label>
        <label className="cl-admin-field">
          <span>Status</span>
          <select
            className="ne-input"
            value={statusFilter}
            onChange={(event) => {
              setStatusFilter(
                event.target.value as
                  | "all"
                  | "open"
                  | PracticeSettingChangeRequestStatus,
              );
              setOffset(0);
            }}
          >
            <option value="open">Open requests</option>
            <option value="all">All requests</option>
            {Object.entries(statusLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <button
          className="cl-btn-secondary"
          type="button"
          onClick={() => setRefreshVersion((version) => version + 1)}
          disabled={listState.status === "loading"}
        >
          Refresh requests
        </button>
      </div>

      {listState.status === "ready" && (
        <div className="practice-request-counts" aria-label="Change request counts">
          <span className="cl-badge cl-badge-muted">
            {listState.data.counts.draft} draft
          </span>
          <span className="cl-badge cl-badge-amber">
            {listState.data.counts.submitted} awaiting review
          </span>
          <span className="cl-badge cl-badge-amber">
            {listState.data.counts.approved} approved
          </span>
          <span className="cl-badge cl-badge-green">
            {listState.data.counts.activated} activated
          </span>
        </div>
      )}

      {listState.status === "loading" && (
        <div className="skeleton-list" aria-label="Loading change requests">
          {[0, 1, 2].map((item) => (
            <div className="skeleton-row" key={item} style={{ height: 62 }} />
          ))}
        </div>
      )}
      {listState.status === "error" && (
        <div className="error-banner practice-change-error" role="alert">
          <p>{listState.message}</p>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setRefreshVersion((version) => version + 1)}
          >
            Retry
          </button>
        </div>
      )}
      {listState.status === "ready" && listState.data.requests.length === 0 && (
        <div className="cl-empty-state">
          <p className="cl-empty-title">No matching change requests</p>
          <p className="cl-empty-text">
            Change the filters or create an inactive proposal.
          </p>
        </div>
      )}
      {listState.status === "ready" && listState.data.requests.length > 0 && (
        <>
          <div className="practice-request-list" aria-label="Practice setting change requests">
            {listState.data.requests.map((request) => (
              <button
                className={`practice-request-row${
                  selected?.requestId === request.requestId
                    ? " practice-request-row-selected"
                    : ""
                }`}
                type="button"
                key={request.requestId}
                onClick={() => void openRequest(request.requestId)}
              >
                <span>
                  <strong>
                    {settingByKey.get(request.settingKey)?.label ??
                      request.settingKey}
                  </strong>
                  <small>
                    {request.baselineValue} → {request.proposedValue}
                    {request.facilityId
                      ? ` · facility ${request.facilityId}`
                      : " · system default"}
                  </small>
                </span>
                <span
                  className={`cl-badge ${statusBadgeClass(request.status)}`}
                >
                  {statusLabels[request.status]}
                </span>
                <small>
                  v{request.version} · {formatDateTime(request.updatedAt)}
                </small>
              </button>
            ))}
          </div>
          <div className="practice-request-pagination">
            <p className="cl-empty-text">
              Showing {listState.data.offset + 1}–
              {listState.data.offset + listState.data.returned} of{" "}
              {listState.data.total}
            </p>
            <div>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={offset === 0}
                onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
              >
                Previous
              </button>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={
                  offset + listState.data.returned >= listState.data.total
                }
                onClick={() => setOffset(offset + PAGE_SIZE)}
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}

      {detailState?.status === "loading" && (
        <div className="cl-card" role="status">
          <span className="sr-only">Loading change request detail</span>
          <div
            className="skeleton-row"
            style={{ height: 120 }}
            aria-hidden="true"
          />
        </div>
      )}
      {detailState?.status === "error" && (
        <div className="error-banner practice-change-error" role="alert">
          {detailState.message}
        </div>
      )}
      {detailState?.status === "ready" && (
        <section className="practice-request-detail" aria-label="Change request detail">
          <div className="practice-change-form-heading">
            <div>
              <p className="cl-form-section-label">Selected request</p>
              <h3 className="cl-card-title">
                {detailState.data.setting.label}
              </h3>
            </div>
            <span
              className={`cl-badge ${statusBadgeClass(
                detailState.data.request.status,
              )}`}
            >
              {statusLabels[detailState.data.request.status]}
            </span>
          </div>

          <dl className="practice-request-facts">
            <div>
              <dt>Scope</dt>
              <dd>
                {detailState.data.request.facilityId
                  ? `Facility ${detailState.data.request.facilityId}`
                  : "System default"}
              </dd>
            </div>
            <div>
              <dt>Baseline at creation</dt>
              <dd>
                {detailState.data.request.baselineValue}
                <small>
                  {formatDateTime(detailState.data.request.baselineUpdatedAt)}
                </small>
              </dd>
            </div>
            <div>
              <dt>Current active value</dt>
              <dd>
                {detailState.data.setting.value}
                <small>{formatDateTime(detailState.data.setting.updatedAt)}</small>
              </dd>
            </div>
            <div>
              <dt>Proposed value</dt>
              <dd>
                {detailState.data.request.proposedValue}
                <small>Loaded request v{detailState.data.request.version}</small>
              </dd>
            </div>
          </dl>
          <p className="practice-request-reason">
            <strong>Reason:</strong> {detailState.data.request.reason}
          </p>

          <section className="practice-governance-boundary" aria-label="Configuration impact preview">
            <strong>Impact preview</strong>
            {impactState?.status === "loading" && <p>Calculating local impact…</p>}
            {impactState?.status === "error" && <p>{impactState.message}</p>}
            {impactState?.status === "ready" && (
              <>
                <p>
                  Generated {formatDateTime(impactState.data.generatedAt)} for {impactState.data.scope}
                  {impactState.data.facilityId ? ` facility ${impactState.data.facilityId}` : " scope"}.
                </p>
                <ul className="practice-request-events">
                  {impactState.data.impacts.map((impact) => (
                    <li key={impact.resourceType}>
                      <strong>{impact.resourceType}:</strong>{" "}
                      {impact.previewAvailable
                        ? `${impact.affectedCount ?? 0} locally countable`
                        : "No local preview available"}
                      <small>{impact.detail}</small>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </section>

          {canCancel && (
            <div className="practice-transition-panel">
              <label className="cl-admin-field">
                <span>Transition note</span>
                <textarea
                  className="ne-input"
                  rows={2}
                  maxLength={1000}
                  value={transitionNote}
                  onChange={(event) => setTransitionNote(event.target.value)}
                  disabled={transitioning !== null}
                  aria-describedby="practice-transition-note-help"
                />
              </label>
              <p id="practice-transition-note-help" className="cl-empty-text">
                Required for rejection or cancellation; optional for other
                transitions.
              </p>
              {transitionError && (
                <div className="error-banner practice-change-error" role="alert">
                  {transitionError}
                </div>
              )}
              <div className="practice-transition-actions">
                {selected?.status === "draft" && (
                  <button
                    className="cl-btn-primary"
                    type="button"
                    onClick={() => void transitionRequest("submit")}
                    disabled={transitioning !== null}
                  >
                    {transitioning === "submit"
                      ? "Submitting…"
                      : "Submit for review"}
                  </button>
                )}
                {selected?.status === "submitted" && (
                  <>
                    <button
                      className="cl-btn-primary"
                      type="button"
                      onClick={() => void transitionRequest("approve")}
                      disabled={transitioning !== null}
                    >
                      {transitioning === "approve" ? "Approving…" : "Approve"}
                    </button>
                    <button
                      className="cl-btn-danger"
                      type="button"
                      onClick={() => void transitionRequest("reject")}
                      disabled={transitioning !== null}
                    >
                      {transitioning === "reject" ? "Rejecting…" : "Reject"}
                    </button>
                  </>
                )}
                {selected?.status === "approved" && (
                  <button
                    className="cl-btn-primary"
                    type="button"
                    onClick={() => void transitionRequest("activate")}
                    disabled={transitioning !== null}
                  >
                    {transitioning === "activate"
                      ? "Activating…"
                      : "Activate current proposal"}
                  </button>
                )}
                {canCancel && (
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => void transitionRequest("cancel")}
                    disabled={transitioning !== null}
                  >
                    {transitioning === "cancel"
                      ? "Cancelling…"
                      : "Cancel request"}
                  </button>
                )}
              </div>
            </div>
          )}

          <div className="practice-request-history">
            <h4>Immutable transition history</h4>
            {detailState.data.events.map((event) => (
              <article key={event.eventId}>
                <span className="cl-badge cl-badge-muted">{event.action}</span>
                <p>{event.note || "No additional note."}</p>
                <small>
                  {event.username} · {formatDateTime(event.occurredAt)}
                </small>
              </article>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
