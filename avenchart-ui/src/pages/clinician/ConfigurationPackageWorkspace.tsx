import { useState } from "react";
import {
  dryRunConfigurationPackage,
  exportConfigurationPackage,
  type ConfigurationPackageDryRun,
} from "../../api.ts";

export default function ConfigurationPackageWorkspace({
  sessionId,
}: {
  sessionId: string;
}) {
  const [packageJson, setPackageJson] = useState("");
  const [result, setResult] = useState<ConfigurationPackageDryRun | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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

  return (
    <section className="cl-card" aria-label="Configuration package workspace">
      <h2 className="cl-card-title">Configuration package</h2>
      <p className="clinician-page-subtitle">
        Export the three adopted non-secret practice settings, or validate a
        package before a future reviewed import. This workspace cannot apply a
        package.
      </p>
      <div className="practice-governance-boundary" role="note">
        Package export excludes secrets, access tokens, and private keys. A
        successful dry run reports differences only; review, import, and
        compensating rollback are separate ADM-03 work.
      </div>
      <div className="practice-setting-actions">
        <button className="cl-btn-secondary" type="button" onClick={() => void exportPackage()} disabled={busy}>
          Export adopted settings
        </button>
        <button className="cl-btn-secondary" type="button" onClick={() => void dryRun()} disabled={busy || !packageJson.trim()}>
          Validate package
        </button>
      </div>
      <label className="cl-admin-field">
        <span>Configuration package JSON</span>
        <textarea className="ne-input" rows={12} value={packageJson} onChange={(event) => setPackageJson(event.target.value)} spellCheck={false} />
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
    </section>
  );
}
