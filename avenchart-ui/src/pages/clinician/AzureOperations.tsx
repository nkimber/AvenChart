// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  CircleDollarSign,
  CloudCog,
  Database,
  ExternalLink,
  FileClock,
  KeyRound,
  LockKeyhole,
  Network,
  Play,
  RefreshCw,
  RotateCcw,
  Save,
  ServerCog,
  ShieldCheck,
  ShieldAlert,
  Square,
  Trash2,
} from "lucide-react";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
import {
  archiveAzureDeploymentProfile,
  assessAzureDeploymentProfile,
  cancelAzureDeploymentExecution,
  changeAzureOperationsAccessCode,
  createAzureDeploymentProfile,
  getAzureDeploymentExecution,
  getAzureDeploymentExecutions,
  getAzureDeploymentHealth,
  getAzureDeploymentProfile,
  getAzureDeploymentProfileHistory,
  getAzureDeploymentProfiles,
  getAzureOperationsCapabilities,
  lockAzureOperations,
  startAzureDeploymentExecution,
  unlockAzureOperations,
  updateAzureDeploymentProfile,
  validateAzureDeploymentAccess,
  type AzureAccessValidationResponse,
  type AzureDeploymentExecutionDetail,
  type AzureDeploymentExecutionSummary,
  type AzureDeploymentHealth,
  type AzureDeploymentProfileAssessment,
  type AzureDeploymentProfileDetail,
  type AzureDeploymentProfileDocument,
  type AzureDeploymentProfileSummary,
  type AzureOperationsCapability,
  type AzureOperationsUnlockResponse,
} from "../../api/azureOperations.ts";
import { ApiRequestError } from "../../api/transport.ts";

type View = "configure" | "review" | "deploy" | "monitor" | "history" | "security";

function uniqueSuffix() {
  try {
    return crypto.randomUUID().replaceAll("-", "").slice(0, 8);
  } catch {
    return Date.now().toString(36).slice(-8);
  }
}

function defaultDocument(): AzureDeploymentProfileDocument {
  const suffix = uniqueSuffix();
  const prefix = `avc${suffix}`;
  return {
    environmentKind: "demo",
    workloadMode: "synthetic-interactive",
    tenantId: "",
    subscriptionId: "",
    location: "eastus2",
    resourceGroupName: `rg-avenchart-demo-${suffix}`,
    resourceNamePrefix: `avc-${suffix}`,
    containerRegistryName: `${prefix}acr`,
    keyVaultName: `${prefix}-kv`,
    postgresServerName: `${prefix}-pg`,
    containerAppsEnvironmentName: `${prefix}-cae`,
    managedIdentityName: `${prefix}-identity`,
    logAnalyticsWorkspaceName: `${prefix}-logs`,
    containerAppName: `${prefix}-app`,
    migrationJobName: `${prefix}-migrate`,
    databaseName: "avenchart",
    databaseAdministratorLogin: "avenchartadmin",
    databasePasswordSecretName: "avenchart-database-administrator-password",
    expectedNamedUsers: 20,
    expectedConcurrentUsers: 10,
    apiCpu: 0.5,
    apiMemoryGiB: 1,
    uiCpu: 0.25,
    uiMemoryGiB: 0.5,
    minimumReplicas: 1,
    maximumReplicas: 2,
    httpConcurrency: 20,
    postgresSkuName: "Standard_B1ms",
    postgresTier: "Burstable",
    postgresStorageGiB: 32,
    connectionPoolMaximum: 15,
    backupRetentionDays: 7,
    enableGeoRedundantBackup: false,
    enableHighAvailability: false,
    vnetAddressPrefix: "10.42.0.0/16",
    infrastructureSubnetPrefix: "10.42.0.0/23",
    databaseSubnetPrefix: "10.42.2.0/28",
    customDomain: "",
    dnsZoneResourceId: "",
    allowedIpRanges: [],
    apiImage: "avenchart-api:demo",
    uiImage: "avenchart-ui:demo",
    sourceRevision: "demo",
    rateLimitPermitLimit: 300,
    logRetentionDays: 30,
    monthlyBudgetUsd: 150,
    alertEmails: [],
    owner: "",
    costCenter: "",
    tags: { application: "AvenChart", environment: "demo" },
    enableDemoSeed: true,
    enableDemoReset: false,
    acknowledgedSyntheticOnly: false,
  };
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : "The Azure operation could not be completed.";
}

function isOperationsAccessError(error: unknown) {
  return error instanceof ApiRequestError &&
    error.status === 403 &&
    ["operations_access_required", "operations_code_change_required"].includes(String(error.problem?.error));
}

function Field({ label, value, onChange, type = "text", help, required = false }: { label: string; value: string | number; onChange: (value: string) => void; type?: "text" | "number" | "email"; help?: string; required?: boolean }) {
  return (
    <label className="azure-ops-field">
      <span>{label}{required && <em aria-hidden="true"> *</em>}</span>
      <input className="ne-input" type={type} value={value} required={required} onChange={(event) => onChange(event.target.value)} />
      {help && <small>{help}</small>}
    </label>
  );
}

function Toggle({ label, checked, onChange, help, disabled = false }: { label: string; checked: boolean; onChange: (checked: boolean) => void; help?: string; disabled?: boolean }) {
  return (
    <label className={`azure-ops-toggle${disabled ? " azure-ops-toggle-disabled" : ""}`}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} />
      <span><strong>{label}</strong>{help && <small>{help}</small>}</span>
    </label>
  );
}

function FormSection({ icon: Icon, title, copy, children }: { icon: typeof CloudCog; title: string; copy: string; children: React.ReactNode }) {
  return (
    <section className="azure-ops-form-section">
      <header><Icon size={19} aria-hidden="true" /><div><h2>{title}</h2><p>{copy}</p></div></header>
      <div className="azure-ops-form-grid">{children}</div>
    </section>
  );
}

function statusClass(status: string) {
  return status === "passed" || status === "succeeded" || status === "healthy" ? "azure-status-good" :
    status === "failed" || status.startsWith("http-") || status === "unreachable" ? "azure-status-bad" : "azure-status-warn";
}

export default function AzureOperations() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [operationsAccess, setOperationsAccess] = useState<AzureOperationsUnlockResponse | null>(null);
  const [accessCode, setAccessCode] = useState("");
  const [currentCode, setCurrentCode] = useState("");
  const [newCode, setNewCode] = useState("");
  const [confirmNewCode, setConfirmNewCode] = useState("");
  const [accessBusy, setAccessBusy] = useState(false);
  const [accessMessage, setAccessMessage] = useState<string | null>(null);
  const [view, setView] = useState<View>("configure");
  const [capability, setCapability] = useState<AzureOperationsCapability | null>(null);
  const [profiles, setProfiles] = useState<AzureDeploymentProfileSummary[]>([]);
  const [selected, setSelected] = useState<AzureDeploymentProfileDetail | null>(null);
  const [profileName, setProfileName] = useState("AvenChart demo");
  const [document, setDocument] = useState<AzureDeploymentProfileDocument>(() => defaultDocument());
  const [assessment, setAssessment] = useState<AzureDeploymentProfileAssessment | null>(null);
  const [access, setAccess] = useState<AzureAccessValidationResponse | null>(null);
  const [health, setHealth] = useState<AzureDeploymentHealth | null>(null);
  const [executions, setExecutions] = useState<AzureDeploymentExecutionSummary[]>([]);
  const [activeExecution, setActiveExecution] = useState<AzureDeploymentExecutionDetail | null>(null);
  const [history, setHistory] = useState<Array<{ revisionId: number; version: number; action: string; changedBy: string; changedAt: string }>>([]);
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const operationsToken = operationsAccess?.accessToken;

  function clearProtectedState(reason?: string) {
    setOperationsAccess(null);
    setCapability(null);
    setProfiles([]);
    setSelected(null);
    setProfileName("AvenChart demo");
    setDocument(defaultDocument());
    setAssessment(null);
    setAccess(null);
    setHealth(null);
    setExecutions([]);
    setActiveExecution(null);
    setHistory([]);
    setConfirmation("");
    setCurrentCode("");
    setNewCode("");
    setConfirmNewCode("");
    setMessage(null);
    setView("configure");
    setAccessMessage(reason ?? null);
  }

  function handleProtectedError(error: unknown) {
    if (isOperationsAccessError(error)) {
      clearProtectedState("Your Operations access grant expired or was revoked. Enter the access code again.");
      return;
    }
    setMessage(errorMessage(error));
  }

  const update = <K extends keyof AzureDeploymentProfileDocument>(key: K, value: AzureDeploymentProfileDocument[K]) => {
    setDocument((current) => ({ ...current, [key]: value }));
    setAssessment(null);
  };
  const number = <K extends keyof AzureDeploymentProfileDocument>(key: K, value: string) => update(key, Number(value) as AzureDeploymentProfileDocument[K]);
  const expectedDeployConfirmation = `DEPLOY ${document.resourceGroupName}`;
  const expectedRollbackConfirmation = `ROLLBACK ${document.containerAppName}`;
  const running = Boolean(activeExecution && ["queued", "running", "cancelling"].includes(activeExecution.execution.status));
  const issuesBySeverity = useMemo(() => ({
    errors: assessment?.issues.filter((issue) => issue.severity === "error") ?? [],
    warnings: assessment?.issues.filter((issue) => issue.severity === "warning") ?? [],
  }), [assessment]);

  async function reloadProfiles(selectId?: string) {
    if (!operationsToken) return;
    const list = await getAzureDeploymentProfiles(session.sessionId, operationsToken);
    setProfiles(list);
    if (selectId) await openProfile(selectId);
  }

  async function openProfile(profileId: string) {
    if (!operationsToken) return;
    setBusy(true);
    setMessage(null);
    try {
      const [detail, executionList] = await Promise.all([
        getAzureDeploymentProfile(session.sessionId, operationsToken, profileId),
        getAzureDeploymentExecutions(session.sessionId, operationsToken, profileId),
      ]);
      setSelected(detail);
      setProfileName(detail.name);
      setDocument(detail.document);
      setAssessment(detail.assessment);
      setExecutions(executionList.executions);
      setAccess(null);
      setHealth(null);
      setHistory([]);
      setActiveExecution(null);
      setConfirmation("");
    } catch (error) {
      handleProtectedError(error);
    } finally {
      setBusy(false);
    }
  }

  function createDraft() {
    setSelected(null);
    setProfileName("AvenChart demo");
    const next = defaultDocument();
    if (capability?.tenantId) next.tenantId = capability.tenantId;
    if (capability?.subscriptionId) next.subscriptionId = capability.subscriptionId;
    if (capability?.signedInIdentity) next.owner = capability.signedInIdentity;
    setDocument(next);
    setAssessment(null);
    setAccess(null);
    setHealth(null);
    setExecutions([]);
    setHistory([]);
    setActiveExecution(null);
    setView("configure");
  }

  const openProfileEffect = useEffectEvent((profileId: string) => openProfile(profileId));
  const refreshHealthEffect = useEffectEvent(() => refreshHealth());
  const handleProtectedErrorEffect = useEffectEvent((error: unknown) => handleProtectedError(error));

  useEffect(() => {
    if (!operationsToken || operationsAccess?.requiresCodeChange) return;
    const controller = new AbortController();
    Promise.all([
      getAzureOperationsCapabilities(session.sessionId, operationsToken, controller.signal),
      getAzureDeploymentProfiles(session.sessionId, operationsToken, controller.signal),
      getAzureDeploymentExecutions(session.sessionId, operationsToken, undefined, controller.signal),
    ]).then(([capabilities, profileList, executionList]) => {
      setCapability(capabilities);
      setProfiles(profileList);
      setExecutions(executionList.executions);
      if (profileList.length > 0) void openProfileEffect(profileList[0].profileId);
      else {
        setDocument((current) => ({ ...current, tenantId: capabilities.tenantId ?? current.tenantId, subscriptionId: capabilities.subscriptionId ?? current.subscriptionId, owner: capabilities.signedInIdentity ?? current.owner }));
      }
    }).catch((error) => {
      if (!controller.signal.aborted) handleProtectedErrorEffect(error);
    });
    return () => controller.abort();
  }, [operationsAccess?.requiresCodeChange, operationsToken, session.sessionId]);

  useEffect(() => {
    if (!operationsAccess) return;
    const remaining = new Date(operationsAccess.expiresAt).getTime() - Date.now();
    if (remaining <= 0) {
      clearProtectedState("Your Operations access grant expired. Enter the access code again.");
      return;
    }
    const timer = window.setTimeout(() => {
      clearProtectedState("Your Operations access grant expired. Enter the access code again.");
    }, remaining);
    return () => window.clearTimeout(timer);
  }, [operationsAccess]);

  useEffect(() => {
    if (!operationsToken || !activeExecution || !["queued", "running", "cancelling"].includes(activeExecution.execution.status)) return;
    const timer = window.setInterval(() => {
      void getAzureDeploymentExecution(session.sessionId, operationsToken, activeExecution.execution.executionId)
        .then((detail) => {
          setActiveExecution(detail);
          setExecutions((items) => [detail.execution, ...items.filter((item) => item.executionId !== detail.execution.executionId)]);
          if (!["queued", "running", "cancelling"].includes(detail.execution.status) && selected) {
            void refreshHealthEffect();
          }
        })
        .catch(handleProtectedErrorEffect);
    }, 3000);
    return () => window.clearInterval(timer);
  }, [activeExecution, operationsToken, selected, session.sessionId]);

  async function saveProfile() {
    if (!operationsToken) return;
    setBusy(true);
    setMessage(null);
    try {
      const saved = selected
        ? await updateAzureDeploymentProfile(session.sessionId, operationsToken, selected.profileId, profileName, selected.version, document)
        : await createAzureDeploymentProfile(session.sessionId, operationsToken, profileName, document);
      setSelected(saved);
      setDocument(saved.document);
      setAssessment(saved.assessment);
      await reloadProfiles();
      setMessage(`Deployment profile saved as version ${saved.version}.`);
    } catch (error) {
      handleProtectedError(error);
    } finally {
      setBusy(false);
    }
  }

  async function reviewProfile() {
    if (!operationsToken) return;
    setBusy(true);
    try {
      const result = await assessAzureDeploymentProfile(session.sessionId, operationsToken, document);
      setAssessment(result);
      setView("review");
    } catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function validateAccess() {
    if (!operationsToken || !selected) return;
    setBusy(true);
    try { setAccess(await validateAzureDeploymentAccess(session.sessionId, operationsToken, selected.profileId)); }
    catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function start(kind: AzureDeploymentExecutionSummary["kind"], requiredConfirmation: string) {
    if (!operationsToken || !selected) return;
    setBusy(true);
    setMessage(null);
    try {
      const execution = await startAzureDeploymentExecution(session.sessionId, operationsToken, selected.profileId, kind, selected.version, requiredConfirmation);
      const detail = await getAzureDeploymentExecution(session.sessionId, operationsToken, execution.executionId);
      setActiveExecution(detail);
      setExecutions((items) => [execution, ...items.filter((item) => item.executionId !== execution.executionId)]);
      setConfirmation("");
      setMessage(`${kind} operation queued.`);
    } catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function cancelActive() {
    if (!operationsToken || !activeExecution) return;
    setBusy(true);
    try { await cancelAzureDeploymentExecution(session.sessionId, operationsToken, activeExecution.execution.executionId); setMessage("Cancellation requested."); }
    catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function refreshHealth() {
    if (!operationsToken || !selected) return;
    setBusy(true);
    try { setHealth(await getAzureDeploymentHealth(session.sessionId, operationsToken, selected.profileId)); }
    catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function loadHistory() {
    if (!operationsToken || !selected) return;
    setBusy(true);
    try { setHistory((await getAzureDeploymentProfileHistory(session.sessionId, operationsToken, selected.profileId)).revisions); }
    catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function archiveProfile() {
    if (!operationsToken || !selected || confirmation !== `ARCHIVE ${selected.name}`) return;
    setBusy(true);
    try { await archiveAzureDeploymentProfile(session.sessionId, operationsToken, selected.profileId, selected.version); createDraft(); await reloadProfiles(); setMessage("Deployment profile archived. Azure resources were not deleted."); }
    catch (error) { handleProtectedError(error); }
    finally { setBusy(false); }
  }

  async function unlockWorkspace(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAccessBusy(true);
    setAccessMessage(null);
    try {
      const grant = await unlockAzureOperations(session.sessionId, accessCode);
      setOperationsAccess(grant);
      setAccessCode("");
    } catch (error) {
      setAccessMessage(errorMessage(error));
    } finally {
      setAccessBusy(false);
    }
  }

  async function changeAccessCode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!operationsToken) return;
    if (newCode !== confirmNewCode) {
      setAccessMessage("The new access-code entries do not match.");
      return;
    }
    setAccessBusy(true);
    setAccessMessage(null);
    try {
      await changeAzureOperationsAccessCode(session.sessionId, operationsToken, currentCode, newCode);
      clearProtectedState("The Operations access code was changed. Every existing grant was revoked; enter the new code to continue.");
    } catch (error) {
      if (isOperationsAccessError(error)) {
        clearProtectedState("Your Operations access grant expired or was revoked. Enter the access code again.");
      } else {
        setAccessMessage(errorMessage(error));
      }
    } finally {
      setAccessBusy(false);
    }
  }

  async function lockWorkspace() {
    if (!operationsToken) return;
    setAccessBusy(true);
    try {
      await lockAzureOperations(session.sessionId, operationsToken);
    } catch {
      // Local locking and memory cleanup remain mandatory if the API is unavailable.
    } finally {
      clearProtectedState("The Azure Operations workspace is locked.");
      setAccessBusy(false);
    }
  }

  function renderAccessCodeChange(required: boolean) {
    return (
      <div className="azure-ops-access-shell">
        <section className="azure-ops-access-card" aria-labelledby="azure-ops-change-code-title">
          <div className="azure-ops-access-icon"><ShieldCheck size={30} aria-hidden="true" /></div>
          <p className="practice-governance-kicker">{required ? "Bootstrap code accepted" : "Operations security"}</p>
          <h2 id="azure-ops-change-code-title">{required ? "Choose a private Operations access code" : "Change the Operations access code"}</h2>
          <p>{required
            ? "The default code must be replaced before any Azure configuration is disclosed. The change revokes every outstanding Operations grant."
            : "Enter the current code and a new code. All open Operations workspaces will be locked immediately."}</p>
          {accessMessage && <div className="error-banner" role="alert">{accessMessage}</div>}
          <form onSubmit={(event) => void changeAccessCode(event)}>
            <label className="azure-ops-field">
              <span>Current access code</span>
              <input className="ne-input" type="password" value={currentCode} onChange={(event) => setCurrentCode(event.target.value)} autoComplete="current-password" required />
            </label>
            <label className="azure-ops-field">
              <span>New access code</span>
              <input className="ne-input" type="password" value={newCode} onChange={(event) => setNewCode(event.target.value)} autoComplete="new-password" minLength={12} maxLength={128} required />
              <small>Use 12 to 128 characters. Leading and trailing spaces are not allowed.</small>
            </label>
            <label className="azure-ops-field">
              <span>Confirm new access code</span>
              <input className="ne-input" type="password" value={confirmNewCode} onChange={(event) => setConfirmNewCode(event.target.value)} autoComplete="new-password" minLength={12} maxLength={128} required />
            </label>
            <div className="azure-ops-access-actions">
              <button className="cl-btn-primary" type="submit" disabled={accessBusy || newCode.length < 12 || newCode !== confirmNewCode}>
                <KeyRound size={16} aria-hidden="true" />Change access code
              </button>
              {!required && <button className="cl-btn-secondary" type="button" disabled={accessBusy} onClick={() => setView("configure")}>Cancel</button>}
            </div>
          </form>
        </section>
      </div>
    );
  }

  function renderSecurity() {
    return (
      <div className="azure-ops-security">
        <section className="azure-ops-panel">
          <div className="azure-ops-panel-heading">
            <div><h2>Operations access grant</h2><p>This browser holds a short-lived, session-bound grant in memory only.</p></div>
            <button className="cl-btn-secondary" type="button" disabled={accessBusy} onClick={() => void lockWorkspace()}><LockKeyhole size={16} />Lock now</button>
          </div>
          <p className="azure-ops-host-note">The current grant expires at {operationsAccess ? new Date(operationsAccess.expiresAt).toLocaleString() : "unknown"}. Closing or refreshing this page also requires the code again.</p>
        </section>
        {renderAccessCodeChange(false)}
      </div>
    );
  }

  function renderConfigure() {
    return (
      <form className="azure-ops-form" onSubmit={(event) => { event.preventDefault(); void saveProfile(); }}>
        <FormSection icon={ShieldAlert} title="Deployment intent" copy="Define the accountable owner, bounded workload, and synthetic-data safety boundary.">
          <Field label="Profile name" value={profileName} onChange={setProfileName} required />
          <label className="azure-ops-field"><span>Environment</span><select className="ne-input" value={document.environmentKind} onChange={(event) => update("environmentKind", event.target.value as AzureDeploymentProfileDocument["environmentKind"])}><option value="demo">Demo</option><option value="development">Development</option><option value="test">Test</option><option value="production" disabled>Production — blocked</option></select></label>
          <Field label="Workload mode" value={document.workloadMode} onChange={(value) => update("workloadMode", value)} />
          <Field label="Owner" value={document.owner} onChange={(value) => update("owner", value)} required />
          <Field label="Cost center" value={document.costCenter} onChange={(value) => update("costCenter", value)} />
          <Field label="Named users" type="number" value={document.expectedNamedUsers} onChange={(value) => number("expectedNamedUsers", value)} />
          <Field label="Concurrent users" type="number" value={document.expectedConcurrentUsers} onChange={(value) => number("expectedConcurrentUsers", value)} help="This drives capacity more directly than registered users." />
          <Toggle label="Synthetic data only" checked={document.acknowledgedSyntheticOnly} onChange={(value) => update("acknowledgedSyntheticOnly", value)} help="Required before plan or deployment." />
        </FormSection>

        <FormSection icon={CloudCog} title="Azure scope and names" copy="Persist every non-secret Azure identifier needed for repeatable deployment.">
          <Field label="Tenant ID" value={document.tenantId} onChange={(value) => update("tenantId", value)} required />
          <Field label="Subscription ID" value={document.subscriptionId} onChange={(value) => update("subscriptionId", value)} required />
          <Field label="Region" value={document.location} onChange={(value) => update("location", value)} required />
          <Field label="Resource group" value={document.resourceGroupName} onChange={(value) => update("resourceGroupName", value)} required />
          <Field label="Resource prefix" value={document.resourceNamePrefix} onChange={(value) => update("resourceNamePrefix", value)} required />
          <Field label="Container registry" value={document.containerRegistryName} onChange={(value) => update("containerRegistryName", value)} required />
          <Field label="Key Vault" value={document.keyVaultName} onChange={(value) => update("keyVaultName", value)} required />
          <Field label="PostgreSQL server" value={document.postgresServerName} onChange={(value) => update("postgresServerName", value)} required />
          <Field label="Container Apps environment" value={document.containerAppsEnvironmentName} onChange={(value) => update("containerAppsEnvironmentName", value)} required />
          <Field label="Managed identity" value={document.managedIdentityName} onChange={(value) => update("managedIdentityName", value)} required />
          <Field label="Log Analytics workspace" value={document.logAnalyticsWorkspaceName} onChange={(value) => update("logAnalyticsWorkspaceName", value)} required />
          <Field label="Container App" value={document.containerAppName} onChange={(value) => update("containerAppName", value)} required />
          <Field label="Migration job" value={document.migrationJobName} onChange={(value) => update("migrationJobName", value)} required />
        </FormSection>

        <FormSection icon={ServerCog} title="Application capacity" copy="The default Consumption profile keeps one warm replica and allows one scale-out replica.">
          <Field label="API vCPU" type="number" value={document.apiCpu} onChange={(value) => number("apiCpu", value)} />
          <Field label="API memory (GiB)" type="number" value={document.apiMemoryGiB} onChange={(value) => number("apiMemoryGiB", value)} />
          <Field label="UI vCPU" type="number" value={document.uiCpu} onChange={(value) => number("uiCpu", value)} />
          <Field label="UI memory (GiB)" type="number" value={document.uiMemoryGiB} onChange={(value) => number("uiMemoryGiB", value)} />
          <Field label="Minimum replicas" type="number" value={document.minimumReplicas} onChange={(value) => number("minimumReplicas", value)} />
          <Field label="Maximum replicas" type="number" value={document.maximumReplicas} onChange={(value) => number("maximumReplicas", value)} />
          <Field label="HTTP concurrency target" type="number" value={document.httpConcurrency} onChange={(value) => number("httpConcurrency", value)} />
          <Field label="Requests per minute per client" type="number" value={document.rateLimitPermitLimit} onChange={(value) => number("rateLimitPermitLimit", value)} />
        </FormSection>

        <FormSection icon={Database} title="PostgreSQL and data lifecycle" copy="Pool capacity is calculated across every possible API replica and checked against the selected database SKU.">
          <label className="azure-ops-field"><span>PostgreSQL SKU</span><select className="ne-input" value={document.postgresSkuName} onChange={(event) => { const sku = event.target.value; update("postgresSkuName", sku); update("postgresTier", sku.startsWith("Standard_B") ? "Burstable" : "GeneralPurpose"); }}><option value="Standard_B1ms">B1ms · 1 vCPU / 2 GiB / 35 user connections</option><option value="Standard_B2s">B2s · 2 vCPU / 4 GiB / 414 user connections</option><option value="Standard_B2ms">B2ms · 2 vCPU / 8 GiB / 844 user connections</option><option value="Standard_D2ds_v5">General Purpose D2ds v5 · production-like</option></select></label>
          <Field label="Compute tier" value={document.postgresTier} onChange={(value) => update("postgresTier", value)} />
          <Field label="Storage (GiB)" type="number" value={document.postgresStorageGiB} onChange={(value) => number("postgresStorageGiB", value)} />
          <Field label="Pool maximum per replica" type="number" value={document.connectionPoolMaximum} onChange={(value) => number("connectionPoolMaximum", value)} help={`${document.connectionPoolMaximum * document.maximumReplicas} potential application connections.`} />
          <Field label="Backup retention (days)" type="number" value={document.backupRetentionDays} onChange={(value) => number("backupRetentionDays", value)} />
          <Field label="Database name" value={document.databaseName} onChange={(value) => update("databaseName", value)} />
          <Field label="Administrator login" value={document.databaseAdministratorLogin} onChange={(value) => update("databaseAdministratorLogin", value)} />
          <Field label="Password secret name" value={document.databasePasswordSecretName} onChange={(value) => update("databasePasswordSecretName", value)} help="The value is generated or reused in Key Vault and is never stored here." />
          <Toggle label="Zone-redundant high availability" checked={document.enableHighAvailability} onChange={(value) => update("enableHighAvailability", value)} />
          <Toggle label="Geo-redundant backup" checked={document.enableGeoRedundantBackup} onChange={(value) => update("enableGeoRedundantBackup", value)} />
          <Toggle label="Seed deterministic synthetic dataset" checked={document.enableDemoSeed} onChange={(value) => update("enableDemoSeed", value)} help="Idempotent: an existing dataset is not replaced." />
          <Toggle label="Reset demo data on startup" checked={false} onChange={() => undefined} disabled help="Permanently disabled for persistent Azure deployments." />
        </FormSection>

        <FormSection icon={Network} title="Private networking and domain" copy="PostgreSQL has no public endpoint; Container Apps uses delegated infrastructure and database subnets.">
          <Field label="VNet CIDR" value={document.vnetAddressPrefix} onChange={(value) => update("vnetAddressPrefix", value)} />
          <Field label="Container Apps subnet" value={document.infrastructureSubnetPrefix} onChange={(value) => update("infrastructureSubnetPrefix", value)} />
          <Field label="PostgreSQL subnet" value={document.databaseSubnetPrefix} onChange={(value) => update("databaseSubnetPrefix", value)} />
          <Field label="Custom domain" value={document.customDomain} onChange={(value) => update("customDomain", value)} help="Optional. DNS ownership and certificate issuance are validated after deployment." />
          <Field label="DNS zone resource ID" value={document.dnsZoneResourceId} onChange={(value) => update("dnsZoneResourceId", value)} />
          <label className="azure-ops-field azure-ops-field-wide"><span>Allowed office/VPN IP ranges</span><textarea className="ne-input" rows={3} value={document.allowedIpRanges.join("\n")} onChange={(event) => update("allowedIpRanges", event.target.value.split(/\r?\n|,/).map((value) => value.trim()).filter(Boolean))} /><small>One IPv4 CIDR per line. Empty keeps normal public UI ingress.</small></label>
        </FormSection>

        <FormSection icon={KeyRound} title="Images, observability, and cost" copy="Images are built in ACR from this repository; secrets remain Key Vault references.">
          <Field label="API image and tag" value={document.apiImage} onChange={(value) => update("apiImage", value)} />
          <Field label="UI image and tag" value={document.uiImage} onChange={(value) => update("uiImage", value)} />
          <Field label="Source revision" value={document.sourceRevision} onChange={(value) => update("sourceRevision", value)} help="Use an immutable commit or release identifier." />
          <Field label="Log retention (days)" type="number" value={document.logRetentionDays} onChange={(value) => number("logRetentionDays", value)} />
          <Field label="Monthly budget (USD)" type="number" value={document.monthlyBudgetUsd} onChange={(value) => number("monthlyBudgetUsd", value)} />
          <label className="azure-ops-field azure-ops-field-wide"><span>Alert recipients</span><textarea className="ne-input" rows={2} value={document.alertEmails.join("\n")} onChange={(event) => update("alertEmails", event.target.value.split(/\r?\n|,/).map((value) => value.trim()).filter(Boolean))} /><small>Cost budget alerts are emitted at 50%, 80%, and forecasted 100%.</small></label>
          <label className="azure-ops-field azure-ops-field-wide"><span>Resource tags</span><textarea className="ne-input" rows={4} value={Object.entries(document.tags).map(([key, value]) => `${key}=${value}`).join("\n")} onChange={(event) => update("tags", Object.fromEntries(event.target.value.split(/\r?\n/).map((line) => line.split("=", 2).map((value) => value.trim())).filter(([key, value]) => key && value)))} /><small>One key=value tag per line.</small></label>
        </FormSection>

        <div className="azure-ops-form-actions">
          <button className="cl-btn-primary" type="submit" disabled={busy || profileName.trim().length < 3}><Save size={16} />{selected ? "Save new version" : "Save draft profile"}</button>
          <button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void reviewProfile()}><CheckCircle2 size={16} />Review readiness</button>
        </div>
      </form>
    );
  }

  function renderReview() {
    if (!assessment) return <div className="azure-ops-empty"><CheckCircle2 size={30} /><p>Review the current editor values to calculate readiness.</p><button className="cl-btn-primary" onClick={() => void reviewProfile()}>Run readiness review</button></div>;
    return (
      <div className="azure-ops-review">
        <section className={`azure-ops-readiness ${assessment.deploymentReady ? "azure-ops-ready" : "azure-ops-not-ready"}`}>
          {assessment.deploymentReady ? <CheckCircle2 size={28} /> : <AlertTriangle size={28} />}
          <div><h2>{assessment.deploymentReady ? "Ready for Azure what-if" : "Not ready to deploy"}</h2><p>{issuesBySeverity.errors.length} errors · {issuesBySeverity.warnings.length} warnings · {assessment.costPosture.replaceAll("-", " ")}</p></div>
        </section>
        <div className="azure-ops-metric-grid">
          <article><strong>{document.expectedNamedUsers}</strong><span>Named users</span></article>
          <article><strong>{document.expectedConcurrentUsers}</strong><span>Concurrent users</span></article>
          <article><strong>{assessment.maximumPotentialDatabaseConnections}</strong><span>Potential pooled connections</span></article>
          <article><strong>{assessment.databaseUserConnectionLimit}</strong><span>Database user-connection limit</span></article>
          <article><strong>${document.monthlyBudgetUsd}</strong><span>Monthly budget</span></article>
        </div>
        {assessment.issues.length > 0 && <section className="azure-ops-panel"><h2>Validation findings</h2><ul className="azure-ops-findings">{assessment.issues.map((issue) => <li key={`${issue.code}-${issue.field}`} className={issue.severity === "error" ? "azure-finding-error" : "azure-finding-warning"}><strong>{issue.field}</strong><span>{issue.message}</span></li>)}</ul></section>}
        <div className="azure-ops-two-column">
          <section className="azure-ops-panel"><h2>Resources in the plan</h2><ul>{assessment.plannedResources.map((resource) => <li key={resource}>{resource}</li>)}</ul></section>
          <section className="azure-ops-panel azure-ops-blockers"><h2>Production blockers</h2><p>These remain blocking even when an Azure deployment succeeds.</p><ul>{assessment.productionBlockers.map((blocker) => <li key={blocker}>{blocker}</li>)}</ul></section>
        </div>
        <section className="azure-ops-panel azure-cost-panel"><CircleDollarSign size={22} /><div><h2>Cost review</h2><p>The selected posture is <strong>{assessment.costPosture.replaceAll("-", " ")}</strong>. Azure prices vary by agreement and region; confirm the live estimate before applying.</p><a href={assessment.pricingCalculatorUrl} target="_blank" rel="noreferrer">Open Azure Pricing Calculator <ExternalLink size={14} /></a></div></section>
      </div>
    );
  }

  function renderDeploy() {
    if (!selected) return <div className="azure-ops-empty"><Save size={30} /><p>Save the deployment profile before validating Azure or starting a plan.</p></div>;
    return (
      <div className="azure-ops-deploy">
        <section className="azure-ops-panel"><h2>Operator host</h2><div className="azure-ops-capability-grid"><span>Azure CLI <strong className={capability?.azureCliAvailable ? "azure-status-good" : "azure-status-bad"}>{capability?.azureCliAvailable ? capability.azureCliVersion : "Unavailable"}</strong></span><span>Azure identity <strong>{capability?.signedInIdentity ?? "Not authenticated"}</strong></span><span>What-if <strong className={capability?.planExecutionEnabled ? "azure-status-good" : "azure-status-bad"}>{capability?.planExecutionEnabled ? "Enabled" : "Disabled"}</strong></span><span>Deployment <strong className={capability?.deploymentExecutionEnabled ? "azure-status-good" : "azure-status-warn"}>{capability?.deploymentExecutionEnabled ? "Enabled" : "Host switch off"}</strong></span></div>{!capability?.deploymentExecutionEnabled && <p className="azure-ops-host-note">To permit mutations from this operator host, set <code>AzureOperations__AllowDeploymentExecution=true</code> and restart the API.</p>}</section>
        <section className="azure-ops-panel"><div className="azure-ops-panel-heading"><div><h2>Azure prerequisites</h2><p>Checks subscription, tenant, provider registration, CLI access, and local deployment policy without changing Azure.</p></div><button className="cl-btn-secondary" disabled={busy} onClick={() => void validateAccess()}><RefreshCw size={15} />Validate access</button></div>{access && <ul className="azure-access-checks">{access.checks.map((check) => <li key={check.check}><strong>{check.check}</strong><span className={statusClass(check.status)}>{check.status}</span><p>{check.message}</p></li>)}</ul>}</section>
        <section className="azure-ops-panel"><h2>1. Preview infrastructure changes</h2><p>Runs ARM what-if with a short-lived secure parameter file. It does not build images or alter Azure.</p><button className="cl-btn-primary" disabled={busy || running || !assessment?.deploymentReady || !capability?.planExecutionEnabled} onClick={() => void start("plan", "PLAN")}><Play size={16} />Run Azure what-if</button></section>
        <section className="azure-ops-panel azure-deploy-danger"><h2>2. Deploy the reviewed profile</h2><p>Creates the platform, builds both images in ACR, seeds synthetic data once, applies migrations, creates a new revision, and verifies health.</p><label className="azure-ops-field"><span>Type <code>{expectedDeployConfirmation}</code></span><input className="ne-input" value={confirmation} onChange={(event) => setConfirmation(event.target.value)} autoComplete="off" /></label><button className="cl-btn-primary" disabled={busy || running || confirmation !== expectedDeployConfirmation || !assessment?.deploymentReady || !capability?.deploymentExecutionEnabled} onClick={() => void start("deploy", confirmation)}><CloudCog size={16} />Deploy to Azure</button></section>
        {activeExecution && <ExecutionProgress detail={activeExecution} onCancel={() => void cancelActive()} cancellable={Boolean(running)} />}
      </div>
    );
  }

  function renderMonitor() {
    if (!selected) return <div className="azure-ops-empty"><Activity size={30} /><p>Select a saved deployment profile to query Azure health.</p></div>;
    return (
      <div className="azure-ops-monitor">
        <section className="azure-ops-panel"><div className="azure-ops-panel-heading"><div><h2>Deployment health</h2><p>Queries the current revision and externally verifies UI, API liveness, and database-backed readiness.</p></div><button className="cl-btn-secondary" disabled={busy} onClick={() => void refreshHealth()}><RefreshCw size={15} />Refresh</button></div>{health ? <><div className="azure-health-grid"><HealthTile label="UI" value={health.uiHealth} /><HealthTile label="API liveness" value={health.apiLiveness} /><HealthTile label="API readiness" value={health.apiReadiness} /><HealthTile label="Revision" value={health.revisionHealthState ?? "unknown"} /></div>{health.applicationUrl && <a className="azure-app-link" href={health.applicationUrl} target="_blank" rel="noreferrer">Open deployed application <ExternalLink size={14} /></a>}{health.messages.map((item) => <p className="azure-ops-host-note" key={item}>{item}</p>)}</> : <p className="cl-empty-text">Health has not been queried in this session.</p>}</section>
        <section className="azure-ops-panel"><h2>Verify without changing traffic</h2><button className="cl-btn-primary" disabled={busy || running} onClick={() => void start("verify", "VERIFY")}><CheckCircle2 size={16} />Run governed verification</button></section>
        <section className="azure-ops-panel azure-deploy-danger"><h2>Revision rollback</h2><p>Shifts 100% of traffic to the previous healthy active revision. Database migrations are not reversed.</p><label className="azure-ops-field"><span>Type <code>{expectedRollbackConfirmation}</code></span><input className="ne-input" value={confirmation} onChange={(event) => setConfirmation(event.target.value)} /></label><button className="cl-btn-secondary" disabled={busy || running || confirmation !== expectedRollbackConfirmation || !capability?.deploymentExecutionEnabled} onClick={() => void start("rollback", confirmation)}><RotateCcw size={16} />Roll back traffic</button></section>
      </div>
    );
  }

  function renderHistory() {
    return (
      <div className="azure-ops-history">
        <section className="azure-ops-panel"><div className="azure-ops-panel-heading"><div><h2>Deployment operations</h2><p>Every plan, deploy, verification, cancellation, and rollback is retained with phase events.</p></div>{selected && operationsToken && <button className="cl-btn-secondary" onClick={() => void getAzureDeploymentExecutions(session.sessionId, operationsToken, selected.profileId).then((result) => setExecutions(result.executions)).catch(handleProtectedError)}><RefreshCw size={15} />Refresh</button>}</div><div className="azure-execution-list">{executions.map((execution) => <button key={execution.executionId} type="button" onClick={() => operationsToken && void getAzureDeploymentExecution(session.sessionId, operationsToken, execution.executionId).then(setActiveExecution).catch(handleProtectedError)}><span className={statusClass(execution.status)}>{execution.status}</span><strong>{execution.kind}</strong><small>{execution.phase} · {new Date(execution.requestedAt).toLocaleString()}</small></button>)}{executions.length === 0 && <p className="cl-empty-text">No operations recorded.</p>}</div></section>
        {activeExecution && <ExecutionProgress detail={activeExecution} onCancel={() => void cancelActive()} cancellable={Boolean(running)} />}
        {selected && <section className="azure-ops-panel"><div className="azure-ops-panel-heading"><div><h2>Profile revisions</h2><p>Immutable non-secret configuration snapshots.</p></div><button className="cl-btn-secondary" onClick={() => void loadHistory()}><FileClock size={15} />Load revisions</button></div><ul className="azure-profile-history">{history.map((revision) => <li key={revision.revisionId}><strong>Version {revision.version}</strong><span>{revision.action} by {revision.changedBy}</span><time>{new Date(revision.changedAt).toLocaleString()}</time></li>)}</ul></section>}
        {selected && <section className="azure-ops-panel azure-archive-panel"><h2>Archive profile</h2><p>Archiving removes this profile from the active list. It never deletes Azure resources.</p><label className="azure-ops-field"><span>Type <code>ARCHIVE {selected.name}</code></span><input className="ne-input" value={confirmation} onChange={(event) => setConfirmation(event.target.value)} /></label><button className="cl-btn-secondary" disabled={busy || running || confirmation !== `ARCHIVE ${selected.name}`} onClick={() => void archiveProfile()}><Trash2 size={16} />Archive profile</button></section>}
      </div>
    );
  }

  if (!operationsAccess) {
    return (
      <div className="clinician-page azure-operations-page azure-operations-locked">
        <header className="clinician-page-header azure-ops-page-header">
          <div><p className="practice-governance-kicker">Restricted control plane</p><h1>Azure deployment operations</h1><p className="clinician-page-subtitle">A separate Operations access code is required before configuration or deployment information is disclosed.</p></div>
          <div className="azure-ops-boundary-badge"><LockKeyhole size={17} /><span>Workspace locked</span></div>
        </header>
        <main className="azure-ops-access-shell">
          <section className="azure-ops-access-card" aria-labelledby="azure-ops-unlock-title">
            <div className="azure-ops-access-icon"><LockKeyhole size={30} aria-hidden="true" /></div>
            <p className="practice-governance-kicker">Additional authorization required</p>
            <h2 id="azure-ops-unlock-title">Enter the Operations access code</h2>
            <p>Your normal AvenChart administrator session is active, but it does not grant access to Azure Operations information.</p>
            {accessMessage && <div className="error-banner" role="alert">{accessMessage}</div>}
            <form onSubmit={(event) => void unlockWorkspace(event)}>
              <label className="azure-ops-field">
                <span>Operations access code</span>
                <input className="ne-input" type="password" value={accessCode} onChange={(event) => setAccessCode(event.target.value)} autoComplete="current-password" minLength={12} maxLength={128} autoFocus required />
              </label>
              <button className="cl-btn-primary" type="submit" disabled={accessBusy || accessCode.length < 12}>
                <KeyRound size={16} aria-hidden="true" />{accessBusy ? "Checking…" : "Unlock Operations"}
              </button>
            </form>
            <small className="azure-ops-access-footnote">Failed attempts are throttled and audited. Access grants expire automatically and are never stored in browser storage.</small>
          </section>
        </main>
      </div>
    );
  }

  if (operationsAccess.requiresCodeChange) {
    return (
      <div className="clinician-page azure-operations-page azure-operations-locked">
        <header className="clinician-page-header azure-ops-page-header">
          <div><p className="practice-governance-kicker">Restricted control plane</p><h1>Azure deployment operations</h1><p className="clinician-page-subtitle">Replace the bootstrap access code before viewing Azure configuration.</p></div>
          <div className="azure-ops-boundary-badge"><ShieldCheck size={17} /><span>Code change required</span></div>
        </header>
        {renderAccessCodeChange(true)}
      </div>
    );
  }

  return (
    <div className="clinician-page azure-operations-page">
      <header className="clinician-page-header azure-ops-page-header"><div><p className="practice-governance-kicker">Protected deployment control plane</p><h1>Azure deployment operations</h1><p className="clinician-page-subtitle">Configure, review, plan, deploy, verify, and roll back a deliberately small synthetic AvenChart environment.</p></div><div className="azure-ops-header-actions"><div className="azure-ops-boundary-badge"><ShieldAlert size={17} /><span>Production clinical deployment blocked</span></div><button className="cl-btn-secondary" type="button" disabled={accessBusy} onClick={() => void lockWorkspace()}><LockKeyhole size={16} />Lock Operations</button></div></header>
      {capability && <aside className="azure-ops-boundary" role="note"><strong>Environment boundary:</strong> {capability.environmentBoundary}</aside>}
      {message && <div className="azure-ops-message" role="status">{message}<button type="button" aria-label="Dismiss message" onClick={() => setMessage(null)}>×</button></div>}
      <div className="azure-ops-layout">
        <aside className="azure-profile-sidebar" aria-label="Azure deployment profiles">
          <div className="azure-profile-sidebar-heading"><strong>Deployment profiles</strong><button type="button" onClick={createDraft}>New</button></div>
          <div className="azure-profile-list">{profiles.map((profile) => <button key={profile.profileId} type="button" className={selected?.profileId === profile.profileId ? "active" : ""} onClick={() => void openProfile(profile.profileId)}><span>{profile.name}</span><small>{profile.environmentKind} · {profile.location}</small><em className={profile.deploymentReady ? "azure-status-good" : "azure-status-warn"}>{profile.deploymentReady ? "ready" : `${profile.validationIssueCount} findings`}</em></button>)}{profiles.length === 0 && <p>No saved profiles.</p>}</div>
        </aside>
        <main className="azure-ops-workspace">
          <nav className="azure-ops-tabs" aria-label="Azure operations sections">{(["configure", "review", "deploy", "monitor", "history", "security"] as View[]).map((item) => <button key={item} type="button" className={view === item ? "active" : ""} aria-current={view === item ? "page" : undefined} onClick={() => setView(item)}>{item}</button>)}</nav>
          <div className="azure-ops-current-profile"><div><strong>{profileName || "Untitled profile"}</strong><span>{selected ? `Version ${selected.version} · saved ${new Date(selected.updatedAt).toLocaleString()}` : "Unsaved draft"}</span></div>{assessment && <span className={assessment.deploymentReady ? "azure-status-good" : "azure-status-warn"}>{assessment.deploymentReady ? "Ready" : "Needs review"}</span>}</div>
          {view === "configure" && renderConfigure()}
          {view === "review" && renderReview()}
          {view === "deploy" && renderDeploy()}
          {view === "monitor" && renderMonitor()}
          {view === "history" && renderHistory()}
          {view === "security" && renderSecurity()}
        </main>
      </div>
    </div>
  );
}

function HealthTile({ label, value }: { label: string; value: string }) {
  return <article><span>{label}</span><strong className={statusClass(value)}>{value}</strong></article>;
}

function ExecutionProgress({ detail, onCancel, cancellable }: { detail: AzureDeploymentExecutionDetail; onCancel: () => void; cancellable: boolean }) {
  return (
    <section className="azure-ops-panel azure-execution-progress">
      <div className="azure-ops-panel-heading"><div><h2>{detail.execution.kind} · {detail.execution.status}</h2><p>Current phase: <strong>{detail.execution.phase}</strong></p></div>{cancellable && <button className="cl-btn-secondary" onClick={onCancel}><Square size={14} />Request cancellation</button>}</div>
      {detail.execution.error && <div className="error-banner">{detail.execution.error}</div>}
      {detail.execution.summary && <p>{detail.execution.summary}</p>}
      {detail.execution.applicationUrl && <a href={detail.execution.applicationUrl} target="_blank" rel="noreferrer">{detail.execution.applicationUrl} <ExternalLink size={13} /></a>}
      <ol className="azure-event-timeline">{detail.events.map((event) => <li key={event.eventId} className={event.level === "error" ? "event-error" : event.level === "warning" ? "event-warning" : ""}><span /><div><strong>{event.phase}</strong><p>{event.message}</p><time>{new Date(event.occurredAt).toLocaleString()}</time></div></li>)}</ol>
    </section>
  );
}
