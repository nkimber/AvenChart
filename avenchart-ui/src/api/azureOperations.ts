// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { apiBaseUrl, apiFetch } from "./transport.ts";

export type AzureDeploymentProfileDocument = {
  environmentKind: "demo" | "development" | "test" | "production";
  workloadMode: string;
  tenantId: string;
  subscriptionId: string;
  location: string;
  resourceGroupName: string;
  resourceNamePrefix: string;
  containerRegistryName: string;
  keyVaultName: string;
  postgresServerName: string;
  containerAppsEnvironmentName: string;
  managedIdentityName: string;
  logAnalyticsWorkspaceName: string;
  containerAppName: string;
  migrationJobName: string;
  databaseName: string;
  databaseAdministratorLogin: string;
  databasePasswordSecretName: string;
  expectedNamedUsers: number;
  expectedConcurrentUsers: number;
  apiCpu: number;
  apiMemoryGiB: number;
  uiCpu: number;
  uiMemoryGiB: number;
  minimumReplicas: number;
  maximumReplicas: number;
  httpConcurrency: number;
  postgresSkuName: string;
  postgresTier: string;
  postgresStorageGiB: number;
  connectionPoolMaximum: number;
  backupRetentionDays: number;
  enableGeoRedundantBackup: boolean;
  enableHighAvailability: boolean;
  vnetAddressPrefix: string;
  infrastructureSubnetPrefix: string;
  databaseSubnetPrefix: string;
  customDomain: string;
  dnsZoneResourceId: string;
  allowedIpRanges: string[];
  apiImage: string;
  uiImage: string;
  sourceRevision: string;
  rateLimitPermitLimit: number;
  logRetentionDays: number;
  monthlyBudgetUsd: number;
  alertEmails: string[];
  owner: string;
  costCenter: string;
  tags: Record<string, string>;
  enableDemoSeed: boolean;
  enableDemoReset: boolean;
  acknowledgedSyntheticOnly: boolean;
};

export type AzureDeploymentValidationIssue = {
  field: string;
  code: string;
  severity: "error" | "warning";
  message: string;
};

export type AzureDeploymentProfileAssessment = {
  valid: boolean;
  deploymentReady: boolean;
  maximumPotentialDatabaseConnections: number;
  databaseUserConnectionLimit: number;
  costPosture: string;
  issues: AzureDeploymentValidationIssue[];
  productionBlockers: string[];
  plannedResources: string[];
  pricingCalculatorUrl: string;
};

export type AzureDeploymentProfileSummary = {
  profileId: string;
  name: string;
  environmentKind: string;
  location: string;
  resourceGroupName: string;
  version: number;
  updatedBy: string;
  updatedAt: string;
  deploymentReady: boolean;
  validationIssueCount: number;
};

export type AzureDeploymentProfileDetail = {
  profileId: string;
  name: string;
  document: AzureDeploymentProfileDocument;
  version: number;
  createdBy: string;
  createdAt: string;
  updatedBy: string;
  updatedAt: string;
  assessment: AzureDeploymentProfileAssessment;
};

export type AzureOperationsCapability = {
  enabled: boolean;
  planExecutionEnabled: boolean;
  deploymentExecutionEnabled: boolean;
  azureCliAvailable: boolean;
  azureCliVersion: string;
  authenticated: boolean;
  signedInIdentity?: string | null;
  tenantId?: string | null;
  subscriptionId?: string | null;
  environmentBoundary: string;
  requiredProviders: string[];
  productionBlockers: string[];
};

export type AzureAccessValidationResponse = {
  valid: boolean;
  checkedAt: string;
  checks: Array<{ check: string; status: "passed" | "warning" | "failed"; message: string }>;
};

export type AzureDeploymentExecutionSummary = {
  executionId: string;
  profileId: string;
  profileVersion: number;
  kind: "plan" | "deploy" | "rollback" | "verify";
  status: "queued" | "running" | "cancelling" | "cancelled" | "succeeded" | "failed";
  phase: string;
  requestedBy: string;
  requestedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  summary?: string | null;
  error?: string | null;
  applicationUrl?: string | null;
  azureDeploymentName?: string | null;
  cancellationRequested: boolean;
};

export type AzureDeploymentExecutionDetail = {
  execution: AzureDeploymentExecutionSummary;
  events: Array<{ eventId: number; level: string; phase: string; message: string; occurredAt: string }>;
};

export type AzureDeploymentHealth = {
  deployed: boolean;
  applicationUrl?: string | null;
  revisionName?: string | null;
  revisionHealthState?: string | null;
  uiHealth: string;
  apiLiveness: string;
  apiReadiness: string;
  checkedAt: string;
  messages: string[];
};

export type AzureOperationsUnlockResponse = {
  accessToken: string;
  expiresAt: string;
  requiresCodeChange: boolean;
};

export type AzureOperationsChangeCodeResponse = {
  changed: boolean;
  requiresUnlock: boolean;
  changedAt: string;
};

function headers(sessionId: string, accessToken?: string, json = false) {
  return {
    "X-AvenChart-Session": sessionId,
    ...(accessToken ? { "X-AvenChart-Operations-Access": accessToken } : {}),
    ...(json ? { "content-type": "application/json" } : {}),
  };
}

export async function unlockAzureOperations(sessionId: string, code: string): Promise<AzureOperationsUnlockResponse> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/access/unlock`, {
    method: "POST", headers: headers(sessionId, undefined, true), body: JSON.stringify({ code }),
  });
  return response.json();
}

export async function changeAzureOperationsAccessCode(sessionId: string, accessToken: string, currentCode: string, newCode: string): Promise<AzureOperationsChangeCodeResponse> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/access/change-code`, {
    method: "POST", headers: headers(sessionId, accessToken, true), body: JSON.stringify({ currentCode, newCode }),
  });
  return response.json();
}

export async function lockAzureOperations(sessionId: string, accessToken: string): Promise<void> {
  await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/access/lock`, {
    method: "POST", headers: headers(sessionId, accessToken),
  });
}

export async function getAzureOperationsCapabilities(sessionId: string, accessToken: string, signal?: AbortSignal): Promise<AzureOperationsCapability> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/capabilities`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function assessAzureDeploymentProfile(sessionId: string, accessToken: string, document: AzureDeploymentProfileDocument, signal?: AbortSignal): Promise<AzureDeploymentProfileAssessment> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/assess`, { method: "POST", headers: headers(sessionId, accessToken, true), body: JSON.stringify(document), signal });
  return response.json();
}

export async function getAzureDeploymentProfiles(sessionId: string, accessToken: string, signal?: AbortSignal): Promise<AzureDeploymentProfileSummary[]> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function getAzureDeploymentProfile(sessionId: string, accessToken: string, profileId: string, signal?: AbortSignal): Promise<AzureDeploymentProfileDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function createAzureDeploymentProfile(sessionId: string, accessToken: string, name: string, document: AzureDeploymentProfileDocument): Promise<AzureDeploymentProfileDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles`, { method: "POST", headers: headers(sessionId, accessToken, true), body: JSON.stringify({ name, document }) });
  return response.json();
}

export async function updateAzureDeploymentProfile(sessionId: string, accessToken: string, profileId: string, name: string, expectedVersion: number, document: AzureDeploymentProfileDocument): Promise<AzureDeploymentProfileDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}`, { method: "PUT", headers: headers(sessionId, accessToken, true), body: JSON.stringify({ name, expectedVersion, document }) });
  return response.json();
}

export async function archiveAzureDeploymentProfile(sessionId: string, accessToken: string, profileId: string, expectedVersion: number): Promise<void> {
  await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}?expectedVersion=${expectedVersion}`, { method: "DELETE", headers: headers(sessionId, accessToken) });
}

export async function getAzureDeploymentProfileHistory(sessionId: string, accessToken: string, profileId: string, signal?: AbortSignal): Promise<{ profileId: string; revisions: Array<{ revisionId: number; version: number; action: string; changedBy: string; changedAt: string }> }> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}/history`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function validateAzureDeploymentAccess(sessionId: string, accessToken: string, profileId: string): Promise<AzureAccessValidationResponse> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}/validate-access`, { method: "POST", headers: headers(sessionId, accessToken) });
  return response.json();
}

export async function getAzureDeploymentHealth(sessionId: string, accessToken: string, profileId: string, signal?: AbortSignal): Promise<AzureDeploymentHealth> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}/health`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function getAzureDeploymentExecutions(sessionId: string, accessToken: string, profileId?: string, signal?: AbortSignal): Promise<{ total: number; executions: AzureDeploymentExecutionSummary[] }> {
  const query = profileId ? `?profileId=${encodeURIComponent(profileId)}&limit=50` : "?limit=50";
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/executions${query}`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function getAzureDeploymentExecution(sessionId: string, accessToken: string, executionId: string, signal?: AbortSignal): Promise<AzureDeploymentExecutionDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/executions/${encodeURIComponent(executionId)}`, { headers: headers(sessionId, accessToken), signal });
  return response.json();
}

export async function startAzureDeploymentExecution(sessionId: string, accessToken: string, profileId: string, kind: AzureDeploymentExecutionSummary["kind"], expectedProfileVersion: number, confirmation: string): Promise<AzureDeploymentExecutionSummary> {
  const response = await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/profiles/${encodeURIComponent(profileId)}/${kind}`, { method: "POST", headers: headers(sessionId, accessToken, true), body: JSON.stringify({ expectedProfileVersion, confirmation }) });
  return response.json();
}

export async function cancelAzureDeploymentExecution(sessionId: string, accessToken: string, executionId: string): Promise<void> {
  await apiFetch(`${apiBaseUrl}/api/administration/azure-operations/executions/${encodeURIComponent(executionId)}/cancel`, { method: "POST", headers: headers(sessionId, accessToken) });
}
