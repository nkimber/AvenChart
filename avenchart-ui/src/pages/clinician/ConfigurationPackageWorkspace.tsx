import { useState } from "react";
import {
  createConfigurationPackageImportRequest,
  createConfigurationPackageCompensatingRollback,
  dryRunConfigurationPackage,
  exportConfigurationPackage,
  getConfigurationPackageImportRequests,
  getConfigurationPackageImportRequest,
  type ConfigurationPackageDryRun,
  type ConfigurationPackageImportRequestDetail,
  transitionConfigurationPackageImportRequest,
} from "../../api.ts";

export default function ConfigurationPackageWorkspace({
  sessionId,
}: {
  sessionId: string;
}) {
  const [packageJson, setPackageJson] = useState("");
  const [result, setResult] = useState<ConfigurationPackageDryRun | null>(null);
  const [reason, setReason] = useState("");
  const [decisionNote, setDecisionNote] = useState("");
  const [importRequest, setImportRequest] = useState<ConfigurationPackageImportRequestDetail | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [history, setHistory] = useState<{ requests: Array<{ requestId: string; kind: string; status: string; updatedAt: string }>; total: number } | null>(null);

  async function exportPackage() {
    setBusy(true);
    setMessage(null);
    try {
      const exported = await exportConfigurationPackage(sessionId);
      setPackageJson(JSON.stringify(exported.package, null, 2));
      setResult(null);
      setMessage(`Exported SHA-256 ${exported.sha256}.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not export the configuration package.");
    } finally {
      setBusy(false);
    }
  }

  async function dryRun() {
    setBusy(true);
    setMessage(null);
    try {
      const packageDocument = JSON.parse(packageJson);
      const response = await dryRunConfigurationPackage(sessionId, packageDocument);
      setResult(response);
      setMessage(response.valid ? "Package passed validation; nothing was applied." : "Package validation failed; nothing was applied.");
    } catch (error) {
      setResult(null);
      setMessage(error instanceof Error ? error.message : "Package JSON is invalid.");
    } finally {
      setBusy(false);
    }
  }

  async function loadHistory() {
    setBusy(true);
    try { setHistory(await getConfigurationPackageImportRequests(sessionId, { limit: 8 })); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Could not load package request history."); }
    finally { setBusy(false); }
  }
  async function openHistoryRequest(requestId: string) {
    setBusy(true);
    try { setImportRequest(await getConfigurationPackageImportRequest(sessionId, requestId)); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Could not load the package request."); }
    finally { setBusy(false); }
  }
  async function createCompensatingRollback() {
    if (!importRequest) return;
    setBusy(true);
    try { setImportRequest(await createConfigurationPackageCompensatingRollback(sessionId, importRequest.request.requestId, reason)); setMessage("Compensating rollback request created for review."); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Could not create the compensating rollback request."); }
    finally { setBusy(false); }
  }

  async function createImportRequest() {
    setBusy(true);
    setMessage(null);
    try {
      const packageDocument = JSON.parse(packageJson);
      const response = await createConfigurationPackageImportRequest(sessionId, packageDocument, reason);
      setImportRequest(response);
      setMessage("Reviewed import request created. Submit it when it is ready for approval.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not create the import request.");
    } finally {
      setBusy(false);
    }
  }

  async function transitionImportRequest(action: "submit" | "approve" | "reject" | "activate" | "cancel") {
    if (!importRequest) return;
    setBusy(true);
    setMessage(null);
    try {
      const response = await transitionConfigurationPackageImportRequest(
        sessionId,
        importRequest.request.requestId,
        action,
        importRequest.request.version,
        decisionNote,
      );
      setImportRequest(response);
      setDecisionNote("");
      setMessage(`Import request ${action === "approve" ? "approved" : action === "activate" ? "activated" : action === "submit" ? "submitted" : `${action}ed`}.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not update the import request.");
    } finally {
      setBusy(false);
    }
  }


  return (
    <section className="cl-card" aria-label="Configuration package workspace">
      <h2 className="cl-card-title">Configuration package</h2>
      <p className="clinician-page-subtitle">
        Export or validate the three adopted non-secret practice settings, then
        open a reviewed import request. Activation rechecks the complete
        captured baseline before it changes any setting.
      </p>
      <div className="practice-governance-boundary" role="note">
        Package export excludes secrets, access tokens, and private keys. A
        successful dry run reports differences only. A request has an
        immutable event trail; activation writes normal setting revisions.
        Compensating rollback remains a separate ADM-03 slice.
      </div>
      <div className="practice-setting-actions">
        <button className="cl-btn-secondary" type="button" onClick={() => void exportPackage()} disabled={busy}>
          Export adopted settings
        </button>
        <button className="cl-btn-secondary" type="button" onClick={() => void dryRun()} disabled={busy || !packageJson.trim()}>
          Validate package
        </button>
        <button className="cl-btn-secondary" type="button" onClick={() => void loadHistory()} disabled={busy}>Load request history</button>
        <button className="cl-btn-primary" type="button" onClick={() => void createImportRequest()} disabled={busy || !packageJson.trim() || !reason.trim() || Boolean(importRequest && ["draft", "submitted", "approved"].includes(importRequest.request.status))}>
          Create reviewed import
        </button>
      </div>
      <label className="cl-admin-field">
        <span>Configuration package JSON</span>
        <textarea className="ne-input" rows={12} value={packageJson} onChange={(event) => setPackageJson(event.target.value)} spellCheck={false} />
      </label>
      <label className="cl-admin-field">
        <span>Reason for reviewed import</span>
        <input className="ne-input" value={reason} onChange={(event) => setReason(event.target.value)} maxLength={1000} />
      </label>
      {message && <p className="cl-empty-text" role="status">{message}</p>}
      {result && (
        <div className="cl-access-panel">
          <p className="cl-admin-form-copy"><strong>SHA-256:</strong> {result.sha256 ?? "Unavailable"}</p>
          {result.issues.length > 0 && <ul>{result.issues.map((issue) => <li key={`${issue.code}-${issue.message}`}>{issue.message}</li>)}</ul>}
          {result.conflicts.length > 0 && <ul>{result.conflicts.map((conflict) => <li key={conflict.key}>{conflict.key}: {conflict.state === "would-change" ? `${conflict.currentValue} → ${conflict.proposedValue}` : "unchanged"}</li>)}</ul>}
          <p className="cl-empty-text">{result.boundary}</p>
        </div>
      )}
      {importRequest && (
        <div className="cl-access-panel">
          <p className="cl-admin-form-copy"><strong>Import request:</strong> {importRequest.request.status} (version {importRequest.request.version})</p>
          {["submitted", "approved", "draft"].includes(importRequest.request.status) && <label className="cl-admin-field"><span>Decision note (required for reject or cancel)</span><input className="ne-input" value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} maxLength={1000} /></label>}
          <div className="practice-setting-actions">
            {importRequest.request.status === "draft" && <button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void transitionImportRequest("submit")}>Submit</button>}
            {importRequest.request.status === "submitted" && <><button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transitionImportRequest("approve")}>Approve</button><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void transitionImportRequest("reject")}>Reject</button></>}
            {importRequest.request.status === "approved" && <button className="cl-btn-primary" type="button" disabled={busy} onClick={() => void transitionImportRequest("activate")}>Activate after baseline check</button>}
            {["draft", "submitted", "approved"].includes(importRequest.request.status) && <button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void transitionImportRequest("cancel")}>Cancel</button>}
            {importRequest.request.status === "activated" && importRequest.request.kind === "import" && <button className="cl-btn-secondary" type="button" disabled={busy || !reason.trim()} onClick={() => void createCompensatingRollback()}>Create compensating rollback</button>}
          </div>
          <ul>{importRequest.currentConflicts.map((conflict) => <li key={conflict.key}>{conflict.key}: {conflict.state === "would-change" ? `${conflict.currentValue} → ${conflict.proposedValue}` : "unchanged"}</li>)}</ul>
          <p className="cl-empty-text">{importRequest.events.map((event) => `${event.action} by ${event.username}`).join(" · ")}</p>
        </div>
      )}
      {history && <div className="cl-access-panel"><p className="cl-admin-form-copy"><strong>Recent package requests:</strong> {history.total}</p><ul>{history.requests.map((request) => <li key={request.requestId}><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void openHistoryRequest(request.requestId)}>Open</button> {request.kind} · {request.status} · {new Date(request.updatedAt).toLocaleString()}</li>)}</ul></div>}
    </section>
  );
}
