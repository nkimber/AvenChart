// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState, type FormEvent } from "react";
import {
  createCodingCatalogChangeRequest,
  getCodingCatalogChangeRequest,
  getCodingCatalogChangeRequests,
  transitionCodingCatalogChangeRequest,
  type CodingCatalogChangeRequestAction,
  type CodingCatalogChangeRequestDetail,
  type CodingCatalogChangeRequestsResponse,
  type CodingCatalogChangeRequestStatus,
  type CodingCatalogItem,
} from "../../api.ts";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

type Props = {
  sessionId: string;
  catalogs: CodingCatalogItem[];
  onCatalogsChanged: () => Promise<void>;
  onOpenHistory: (key: string) => void;
};

const PAGE_SIZE = 8;
const statusLabels: Record<CodingCatalogChangeRequestStatus, string> = {
  draft: "Draft",
  submitted: "Awaiting review",
  approved: "Approved",
  rejected: "Rejected",
  activated: "Activated",
  cancelled: "Cancelled",
};

function statusBadgeClass(status: CodingCatalogChangeRequestStatus) {
  if (status === "activated") return "cl-badge-green";
  if (status === "rejected") return "cl-badge-red";
  if (status === "submitted" || status === "approved") return "cl-badge-amber";
  return "cl-badge-muted";
}

function formatDateTime(value?: string | null) {
  if (!value) return "No active baseline";
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
}

function emptyProposal() {
  return {
    key: "",
    displayName: "",
    sequence: 10,
    modifierLength: 0,
    active: true,
    claimEnabled: false,
    feeEnabled: false,
    reason: "",
  };
}

function catalogSummary(catalog: {
  displayName: string;
  sequence: number;
  modifierLength: number;
  active: boolean;
  claimEnabled: boolean;
  feeEnabled: boolean;
}) {
  return `${catalog.displayName} · order ${catalog.sequence} · modifier ${catalog.modifierLength} · ${catalog.active ? "active" : "inactive"} · ${catalog.claimEnabled ? "claims" : "no claims"} · ${catalog.feeEnabled ? "fees" : "no fees"}`;
}

export default function CodingCatalogGovernance({
  sessionId,
  catalogs,
  onCatalogsChanged,
  onOpenHistory,
}: Props) {
  const [listState, setListState] = useState<
    AsyncState<CodingCatalogChangeRequestsResponse>
  >({ status: "loading" });
  const [statusFilter, setStatusFilter] = useState<
    "all" | "open" | CodingCatalogChangeRequestStatus
  >("open");
  const [offset, setOffset] = useState(0);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [proposal, setProposal] = useState(emptyProposal);
  const [showProposal, setShowProposal] = useState(false);
  const [proposalError, setProposalError] = useState<string | null>(null);
  const [savingProposal, setSavingProposal] = useState(false);
  const [detailState, setDetailState] =
    useState<AsyncState<CodingCatalogChangeRequestDetail> | null>(null);
  const [transitionNote, setTransitionNote] = useState("");
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [transitioning, setTransitioning] =
    useState<CodingCatalogChangeRequestAction | null>(null);

  const loadRequests = useEffectEvent(async (signal?: AbortSignal) => {
    setListState({ status: "loading" });
    try {
      setListState({
        status: "ready",
        data: await getCodingCatalogChangeRequests(
          sessionId,
          { status: statusFilter, offset, limit: PAGE_SIZE },
          signal,
        ),
      });
    } catch (error) {
      if (signal?.aborted) return;
      setListState({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "Could not load catalog change requests.",
      });
    }
  });

  useEffect(() => {
    const controller = new AbortController();
    void loadRequests(controller.signal);
    return () => controller.abort();
  }, [sessionId, statusFilter, offset, refreshVersion]);

  function beginProposal(catalog?: CodingCatalogItem) {
    setProposal(
      catalog
        ? {
            key: catalog.key,
            displayName: catalog.displayName,
            sequence: catalog.sequence,
            modifierLength: catalog.modifierLength,
            active: catalog.active,
            claimEnabled: catalog.claimEnabled,
            feeEnabled: catalog.feeEnabled,
            reason: "",
          }
        : emptyProposal(),
    );
    setProposalError(null);
    setShowProposal(true);
  }

  async function submitProposal(event: FormEvent) {
    event.preventDefault();
    if (!proposal.key.trim() || !proposal.displayName.trim()) {
      setProposalError("A code-system key and display name are required.");
      return;
    }
    if (!proposal.reason.trim()) {
      setProposalError("A change reason is required.");
      return;
    }
    setSavingProposal(true);
    setProposalError(null);
    try {
      const detail = await createCodingCatalogChangeRequest(sessionId, proposal);
      setDetailState({ status: "ready", data: detail });
      setTransitionNote("");
      setTransitionError(null);
      setShowProposal(false);
      setOffset(0);
      setRefreshVersion((value) => value + 1);
    } catch (error) {
      setProposalError(
        error instanceof Error ? error.message : "Could not create the proposal.",
      );
    } finally {
      setSavingProposal(false);
    }
  }

  async function openRequest(requestId: string) {
    setDetailState({ status: "loading" });
    setTransitionError(null);
    setTransitionNote("");
    try {
      setDetailState({
        status: "ready",
        data: await getCodingCatalogChangeRequest(sessionId, requestId),
      });
    } catch (error) {
      setDetailState({
        status: "error",
        message:
          error instanceof Error ? error.message : "Could not load the proposal.",
      });
    }
  }

  async function transitionRequest(action: CodingCatalogChangeRequestAction) {
    if (detailState?.status !== "ready" || transitioning) return;
    if ((action === "reject" || action === "cancel") && !transitionNote.trim()) {
      setTransitionError(`A transition note is required to ${action} this request.`);
      return;
    }
    setTransitioning(action);
    setTransitionError(null);
    try {
      const next = await transitionCodingCatalogChangeRequest(
        sessionId,
        detailState.data.request.requestId,
        action,
        { note: transitionNote.trim() || null, expectedVersion: detailState.data.request.version },
      );
      setDetailState({ status: "ready", data: next });
      setTransitionNote("");
      setRefreshVersion((value) => value + 1);
      if (action === "activate") await onCatalogsChanged();
    } catch (error) {
      setTransitionError(
        error instanceof Error ? error.message : `Could not ${action} the proposal.`,
      );
      try {
        setDetailState({
          status: "ready",
          data: await getCodingCatalogChangeRequest(
            sessionId,
            detailState.data.request.requestId,
          ),
        });
      } catch {
        // Retain the actionable mutation error if authoritative refresh also fails.
      }
    } finally {
      setTransitioning(null);
    }
  }

  const selected = detailState?.status === "ready" ? detailState.data.request : null;
  const canCancel = selected?.status === "draft" || selected?.status === "submitted" || selected?.status === "approved";

  return (
    <section className="practice-governance" aria-label="Coding catalog governance">
      <div className="practice-governance-header">
        <div>
          <h2 className="cl-card-title">Coding catalog governance</h2>
          <p className="clinician-page-subtitle">
            Keep active code systems stable while proposed definitions move through review. A request can create a new catalog or change an existing definition, but activation is deliberate and revision-backed.
          </p>
        </div>
        <button className="cl-btn-primary" type="button" onClick={() => beginProposal()} disabled={showProposal}>
          Propose code system
        </button>
      </div>
      <div className="practice-governance-boundary" role="note">
        <strong>Current local boundary:</strong> the same authorized administrator may submit, approve, and activate. Terminology licensing, source/version control, effective dating, independent approval, and clinical or billing impact preview remain separately governed work. The direct catalog APIs are compatibility-only and are not used here.
      </div>

      <div className="practice-setting-grid" aria-label="Active coding catalogs">
        {catalogs.map((catalog) => (
          <article className="practice-setting-card" key={catalog.key}>
            <p className="cl-form-section-label">{catalog.key}</p>
            <p className="practice-setting-value">{catalog.displayName}</p>
            <p className="cl-empty-text">{catalogSummary(catalog)}</p>
            <div className="practice-setting-actions">
              <button className="cl-btn-secondary" type="button" onClick={() => beginProposal(catalog)}>Propose change</button>
              <button className="cl-btn-secondary" type="button" onClick={() => onOpenHistory(catalog.key)}>Revision history</button>
            </div>
          </article>
        ))}
      </div>

      {showProposal && (
        <form className="practice-change-form" onSubmit={submitProposal}>
          <div className="practice-change-form-heading"><div><p className="cl-form-section-label">New inactive proposal</p><p className="cl-admin-form-copy">Drafting a proposal does not change the active catalog.</p></div><button className="cl-icon-button" type="button" aria-label="Close proposal" onClick={() => setShowProposal(false)}>×</button></div>
          <label className="cl-admin-field"><span>Code system key</span><input className="ne-input" value={proposal.key} maxLength={32} onChange={(event) => setProposal((value) => ({ ...value, key: event.target.value.toUpperCase() }))} required disabled={savingProposal} /></label>
          <label className="cl-admin-field"><span>Display name</span><input className="ne-input" value={proposal.displayName} maxLength={120} onChange={(event) => setProposal((value) => ({ ...value, displayName: event.target.value }))} required disabled={savingProposal} /></label>
          <label className="cl-admin-field"><span>Order</span><input className="ne-input" type="number" min="0" value={proposal.sequence} onChange={(event) => setProposal((value) => ({ ...value, sequence: Number(event.target.value) }))} required disabled={savingProposal} /></label>
          <label className="cl-admin-field"><span>Modifier length</span><input className="ne-input" type="number" min="0" max="12" value={proposal.modifierLength} onChange={(event) => setProposal((value) => ({ ...value, modifierLength: Number(event.target.value) }))} required disabled={savingProposal} /></label>
          <label className="cl-admin-field"><span><input type="checkbox" checked={proposal.active} onChange={(event) => setProposal((value) => ({ ...value, active: event.target.checked }))} disabled={savingProposal} /> Active when activated</span></label>
          <label className="cl-admin-field"><span><input type="checkbox" checked={proposal.claimEnabled} onChange={(event) => setProposal((value) => ({ ...value, claimEnabled: event.target.checked }))} disabled={savingProposal} /> Claims capability</span></label>
          <label className="cl-admin-field"><span><input type="checkbox" checked={proposal.feeEnabled} onChange={(event) => setProposal((value) => ({ ...value, feeEnabled: event.target.checked }))} disabled={savingProposal} /> Fees capability</span></label>
          <label className="cl-admin-field practice-change-reason"><span>Change reason</span><textarea className="ne-input" rows={3} maxLength={1000} value={proposal.reason} onChange={(event) => setProposal((value) => ({ ...value, reason: event.target.value }))} required disabled={savingProposal} /></label>
          {proposalError && <div className="error-banner practice-change-error" role="alert">{proposalError}</div>}
          <button className="cl-btn-primary" type="submit" disabled={savingProposal}>{savingProposal ? "Creating…" : "Create inactive draft"}</button>
        </form>
      )}

      <div className="practice-request-toolbar"><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={statusFilter} onChange={(event) => { setStatusFilter(event.target.value as "all" | "open" | CodingCatalogChangeRequestStatus); setOffset(0); }}><option value="open">Open requests</option><option value="all">All requests</option>{Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><button className="cl-btn-secondary" type="button" onClick={() => setRefreshVersion((value) => value + 1)} disabled={listState.status === "loading"}>Refresh requests</button></div>
      {listState.status === "ready" && <div className="practice-request-counts" aria-label="Catalog change request counts"><span className="cl-badge cl-badge-muted">{listState.data.counts.draft} draft</span><span className="cl-badge cl-badge-amber">{listState.data.counts.submitted} awaiting review</span><span className="cl-badge cl-badge-amber">{listState.data.counts.approved} approved</span><span className="cl-badge cl-badge-green">{listState.data.counts.activated} activated</span></div>}
      {listState.status === "loading" && <div className="skeleton-list" aria-label="Loading catalog proposals">{[0, 1, 2].map((item) => <div className="skeleton-row" key={item} style={{ height: 62 }} />)}</div>}
      {listState.status === "error" && <div className="error-banner practice-change-error" role="alert"><p>{listState.message}</p><button className="cl-btn-secondary" type="button" onClick={() => setRefreshVersion((value) => value + 1)}>Retry</button></div>}
      {listState.status === "ready" && listState.data.requests.length === 0 && <div className="cl-empty-state"><p className="cl-empty-title">No matching catalog proposals</p><p className="cl-empty-text">Create an inactive proposal or change the status filter.</p></div>}
      {listState.status === "ready" && listState.data.requests.length > 0 && <><div className="practice-request-list" aria-label="Coding catalog change requests">{listState.data.requests.map((request) => <button className={`practice-request-row${selected?.requestId === request.requestId ? " practice-request-row-selected" : ""}`} type="button" key={request.requestId} onClick={() => void openRequest(request.requestId)}><span><strong>{request.catalogKey}</strong><small>{request.changeKind === "create" ? "New catalog" : request.baselineDisplayName} → {request.proposedDisplayName}</small></span><span className={`cl-badge ${statusBadgeClass(request.status)}`}>{statusLabels[request.status]}</span><small>v{request.version} · {formatDateTime(request.updatedAt)}</small></button>)}</div><div className="practice-request-pagination"><p className="cl-empty-text">Showing {listState.data.offset + 1}–{listState.data.offset + listState.data.returned} of {listState.data.total}</p><div><button className="cl-btn-secondary" type="button" disabled={offset === 0} onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}>Previous</button><button className="cl-btn-secondary" type="button" disabled={offset + listState.data.returned >= listState.data.total} onClick={() => setOffset(offset + PAGE_SIZE)}>Next</button></div></div></>}

      {detailState?.status === "loading" && <div className="cl-card" aria-label="Loading catalog proposal"><div className="skeleton-row" style={{ height: 120 }} /></div>}
      {detailState?.status === "error" && <div className="error-banner practice-change-error" role="alert">{detailState.message}</div>}
      {detailState?.status === "ready" && <section className="practice-request-detail" aria-label="Catalog change request detail"><div className="practice-change-form-heading"><div><p className="cl-form-section-label">Selected proposal</p><h3 className="cl-card-title">{detailState.data.request.catalogKey}</h3></div><span className={`cl-badge ${statusBadgeClass(detailState.data.request.status)}`}>{statusLabels[detailState.data.request.status]}</span></div><dl className="practice-request-facts"><div><dt>Baseline at creation</dt><dd>{detailState.data.request.baselineDisplayName ? catalogSummary({ displayName: detailState.data.request.baselineDisplayName, sequence: detailState.data.request.baselineSequence ?? 0, modifierLength: detailState.data.request.baselineModifierLength ?? 0, active: detailState.data.request.baselineActive ?? false, claimEnabled: detailState.data.request.baselineClaimEnabled ?? false, feeEnabled: detailState.data.request.baselineFeeEnabled ?? false }) : "Catalog will be created"}<small>{formatDateTime(detailState.data.request.baselineUpdatedAt)}</small></dd></div><div><dt>Current active definition</dt><dd>{detailState.data.activeCatalog ? catalogSummary(detailState.data.activeCatalog) : "No active catalog"}</dd></div><div><dt>Proposed definition</dt><dd>{catalogSummary({ displayName: detailState.data.request.proposedDisplayName, sequence: detailState.data.request.proposedSequence, modifierLength: detailState.data.request.proposedModifierLength, active: detailState.data.request.proposedActive, claimEnabled: detailState.data.request.proposedClaimEnabled, feeEnabled: detailState.data.request.proposedFeeEnabled })}<small>Loaded request v{detailState.data.request.version}</small></dd></div></dl><p className="practice-request-reason"><strong>Reason:</strong> {detailState.data.request.reason}</p>{canCancel && <div className="practice-transition-panel"><label className="cl-admin-field"><span>Transition note</span><textarea className="ne-input" rows={2} maxLength={1000} value={transitionNote} onChange={(event) => setTransitionNote(event.target.value)} disabled={transitioning !== null} /></label><p className="cl-empty-text">Required for rejection or cancellation; optional for other transitions.</p>{transitionError && <div className="error-banner practice-change-error" role="alert">{transitionError}</div>}<div className="practice-transition-actions">{selected?.status === "draft" && <button className="cl-btn-primary" type="button" onClick={() => void transitionRequest("submit")} disabled={transitioning !== null}>{transitioning === "submit" ? "Submitting…" : "Submit for review"}</button>}{selected?.status === "submitted" && <><button className="cl-btn-primary" type="button" onClick={() => void transitionRequest("approve")} disabled={transitioning !== null}>{transitioning === "approve" ? "Approving…" : "Approve"}</button><button className="cl-btn-danger" type="button" onClick={() => void transitionRequest("reject")} disabled={transitioning !== null}>{transitioning === "reject" ? "Rejecting…" : "Reject"}</button></>}{selected?.status === "approved" && <button className="cl-btn-primary" type="button" onClick={() => void transitionRequest("activate")} disabled={transitioning !== null}>{transitioning === "activate" ? "Activating…" : "Activate current proposal"}</button>}<button className="cl-btn-secondary" type="button" onClick={() => void transitionRequest("cancel")} disabled={transitioning !== null}>{transitioning === "cancel" ? "Cancelling…" : "Cancel request"}</button></div></div>}<div className="practice-request-history"><h4>Immutable transition history</h4>{detailState.data.events.map((event) => <article key={event.eventId}><span className="cl-badge cl-badge-muted">{event.action}</span><p>{event.note || "No additional note."}</p><small>{event.username} · {formatDateTime(event.occurredAt)}</small></article>)}</div></section>}
    </section>
  );
}
