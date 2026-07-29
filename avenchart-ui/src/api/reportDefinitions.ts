import { apiBaseUrl, apiFetch } from "./transport.ts";

export type ReportMetricDefinition = {
  key: string;
  label: string;
  definition: string;
  unit: string;
  sourceField: string;
};

export type ReportParameterDefinition = {
  key: string;
  label: string;
  type: string;
  required: boolean;
  maxSpanDays: number | null;
};

export type ReportSourceDatasetDefinition = {
  key: string;
  description: string;
  fields: string[];
};

export type ReportOutputFieldDefinition = {
  key: string;
  label: string;
  type: string;
  sensitivity: string;
};

export type ReportValidationFixture = {
  datasetId: string;
  scenario: string;
  expectedColumns: string[];
  expectedRowCount: number | null;
};

export type GovernedReportFamily = {
  key: string;
  name: string;
  purpose: string;
  metricDictionary: ReportMetricDefinition[];
  parameterSchema: ReportParameterDefinition[];
  sourceDatasets: ReportSourceDatasetDefinition[];
  outputSchema: ReportOutputFieldDefinition[];
  validationFixture: ReportValidationFixture;
};

export type ReportDefinitionGovernancePolicy = {
  revision: string;
  rawSqlAccepted: boolean;
  executableTemplatesAccepted: boolean;
  externalDeliveryEnabled: boolean;
  rowPolicyExecutionEnforced: boolean;
  states: string[];
  sensitivities: string[];
  rowPolicies: string[];
  allowedRecipients: string[];
  deliveryModes: string[];
  minimumRetentionDays: number;
  maximumRetentionDays: number;
  families: GovernedReportFamily[];
  productionBlockers: string[];
};

export type GovernedReportDefinitionInput = {
  stableKey: string;
  title: string;
  ownerUsername: string;
  purpose: string;
  reportFamily: string;
  sensitivity: string;
  rowPolicy: string;
  retentionDays: number;
  allowedRecipients: string[];
  deliveryModes: string[];
  reason: string;
};

export type GovernedReportRevisionInput = Omit<
  GovernedReportDefinitionInput,
  "stableKey"
> & {
  expectedLatestRevisionNumber: number;
};

export type GovernedReportDefinitionSummary = {
  definitionId: string;
  stableKey: string;
  governanceVersion: number;
  latestRevisionId: string;
  latestRevisionNumber: number;
  title: string;
  ownerUsername: string;
  reportFamily: string;
  sensitivity: string;
  rowPolicy: string;
  retentionDays: number | null;
  status: string;
  version: number;
  activeRevisionNumber: number | null;
  updatedAt: string;
  updatedBy: string;
  legacyReviewRequired: boolean;
};

export type GovernedReportDefinitionList = {
  definitions: GovernedReportDefinitionSummary[];
  page: number;
  pageSize: number;
  total: number;
};

export type GovernedReportDefinitionRevision = {
  revisionId: string;
  definitionId: string;
  revisionNumber: number;
  title: string;
  ownerUsername: string;
  purpose: string;
  reportFamily: string;
  metricDictionary: ReportMetricDefinition[];
  parameterSchema: ReportParameterDefinition[];
  sourceDatasets: ReportSourceDatasetDefinition[];
  outputSchema: ReportOutputFieldDefinition[];
  sensitivity: string;
  rowPolicy: string;
  retentionDays: number | null;
  allowedRecipients: string[];
  deliveryModes: string[];
  validationFixture: ReportValidationFixture;
  status: string;
  version: number;
  predecessorRevisionId: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  legacyReviewRequired: boolean;
};

export type GovernedReportDefinitionEvent = {
  eventId: string;
  definitionId: string;
  revisionId: string;
  revisionNumber: number;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  reason: string;
  actorUsername: string;
  occurredAt: string;
  snapshotChecksum: string;
};

export type GovernedReportDefinitionDetail = {
  definitionId: string;
  stableKey: string;
  governanceVersion: number;
  latestRevisionId: string;
  activeRevisionId: string | null;
  revisions: GovernedReportDefinitionRevision[];
  events: GovernedReportDefinitionEvent[];
};

export type GovernedReportExecutionPolicy = {
  revision: string;
  definitionRevision: string;
  datasetId: string;
  datasetVersion: string;
  requiredAsOfDate: string;
  runStates: string[];
  executableRowPolicies: string[];
  deliveryModes: string[];
  maximumDateSpanDays: number;
  maximumRows: number;
  previewRows: number;
  externalDeliveryEnabled: boolean;
  artifactStorageProductionApproved: boolean;
  productionBlockers: string[];
};

export type GovernedReportExecutionInput = {
  purpose: string;
  recipientUsername: string;
  deliveryMode: string;
  asOfDate: string;
  parameters: Record<string, string | null>;
};

export type GovernedReportPreview = {
  definitionId: string;
  revisionId: string;
  revisionNumber: number;
  reportFamily: string;
  rowPolicy: string;
  purpose: string;
  recipientUsername: string;
  asOfDate: string;
  normalizedParameters: Record<string, string | null>;
  datasetId: string;
  datasetVersion: string;
  executionRevision: string;
  totalRows: number;
  previewRowLimit: number;
  columns: string[];
  rows: string[][];
  resultChecksum: string;
};

export type GovernedReportRun = {
  runId: string;
  definitionId: string;
  revisionId: string | null;
  revisionNumber: number | null;
  definitionStableKey: string;
  definitionTitle: string;
  reportFamily: string;
  status: string;
  requestedBy: string;
  recipientUsername: string;
  purpose: string;
  rowPolicy: string;
  asOfDate: string;
  normalizedParameters: Record<string, string | null>;
  datasetId: string;
  datasetVersion: string;
  executionRevision: string;
  definitionSnapshotChecksum: string;
  requestedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  durationMs: number | null;
  rowCount: number;
  resultChecksum: string | null;
  artifactBytes: number;
  artifactContentType: string | null;
  artifactFileName: string | null;
  failureCode: string | null;
  failureMessage: string | null;
  downloadAvailable: boolean;
  replay: boolean;
};

export type GovernedReportRunEvent = {
  eventId: string;
  runId: string;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  actorUsername: string;
  reason: string;
  occurredAt: string;
  details: Record<string, unknown>;
};

export type GovernedReportRunDetail = {
  run: GovernedReportRun;
  events: GovernedReportRunEvent[];
};

export type GovernedReportRunList = {
  runs: GovernedReportRun[];
  page: number;
  pageSize: number;
  total: number;
};

function headers(sessionId: string, json = false) {
  return {
    "X-Legacy EHR-Session": sessionId,
    ...(json ? { "content-type": "application/json" } : {}),
  };
}

export async function getReportDefinitionPolicy(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ReportDefinitionGovernancePolicy> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definition-policy`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ReportDefinitionGovernancePolicy;
}

export async function getGovernedReportDefinitions(
  sessionId: string,
  options: {
    search?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  } = {},
  signal?: AbortSignal,
): Promise<GovernedReportDefinitionList> {
  const query = new URLSearchParams({
    page: String(options.page ?? 1),
    pageSize: String(options.pageSize ?? 10),
  });
  if (options.search) query.set("search", options.search);
  if (options.status) query.set("status", options.status);
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions?${query}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as GovernedReportDefinitionList;
}

export async function getGovernedReportCatalog(
  sessionId: string,
  search = "",
  signal?: AbortSignal,
): Promise<GovernedReportDefinitionList> {
  const query = new URLSearchParams({ page: "1", pageSize: "50" });
  if (search) query.set("search", search);
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/catalog?${query}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as GovernedReportDefinitionList;
}

export async function getGovernedReportDefinition(
  sessionId: string,
  definitionId: string,
  signal?: AbortSignal,
): Promise<GovernedReportDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as GovernedReportDefinitionDetail;
}

export async function createGovernedReportDefinition(
  sessionId: string,
  input: GovernedReportDefinitionInput,
): Promise<GovernedReportDefinitionDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/reports/definitions`, {
    method: "POST",
    headers: headers(sessionId, true),
    body: JSON.stringify(input),
  });
  return (await response.json()) as GovernedReportDefinitionDetail;
}

export async function createGovernedReportRevision(
  sessionId: string,
  definitionId: string,
  input: GovernedReportRevisionInput,
): Promise<GovernedReportDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/revisions`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as GovernedReportDefinitionDetail;
}

export async function transitionGovernedReportDefinition(
  sessionId: string,
  definitionId: string,
  action: string,
  expectedVersion: number,
  reason: string,
): Promise<GovernedReportDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/${encodeURIComponent(action)}`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason }),
    },
  );
  return (await response.json()) as GovernedReportDefinitionDetail;
}

export async function deleteGovernedReportDefinitionTestFixture(
  sessionId: string,
  definitionId: string,
): Promise<void> {
  await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/test-fixture`,
    { method: "DELETE", headers: headers(sessionId) },
  );
}

export async function getGovernedReportExecutionPolicy(
  sessionId: string,
  signal?: AbortSignal,
): Promise<GovernedReportExecutionPolicy> {
  const response = await apiFetch(`${apiBaseUrl}/api/reports/execution-policy`, {
    headers: headers(sessionId),
    signal,
  });
  return (await response.json()) as GovernedReportExecutionPolicy;
}

export async function previewGovernedReport(
  sessionId: string,
  definitionId: string,
  input: GovernedReportExecutionInput,
): Promise<GovernedReportPreview> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/preview`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as GovernedReportPreview;
}

export async function runGovernedReport(
  sessionId: string,
  definitionId: string,
  input: GovernedReportExecutionInput & { idempotencyKey: string },
): Promise<GovernedReportRunDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/run`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as GovernedReportRunDetail;
}

export async function getGovernedReportRuns(
  sessionId: string,
  definitionId: string,
  page = 1,
  pageSize = 10,
  signal?: AbortSignal,
): Promise<GovernedReportRunList> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/definitions/${encodeURIComponent(definitionId)}/runs?${query}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as GovernedReportRunList;
}

export async function getGovernedReportRun(
  sessionId: string,
  runId: string,
  signal?: AbortSignal,
): Promise<GovernedReportRunDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/runs/${encodeURIComponent(runId)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as GovernedReportRunDetail;
}

export async function downloadGovernedReportRun(
  sessionId: string,
  runId: string,
): Promise<Blob> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/reports/runs/${encodeURIComponent(runId)}/download`,
    { headers: headers(sessionId) },
  );
  return response.blob();
}
