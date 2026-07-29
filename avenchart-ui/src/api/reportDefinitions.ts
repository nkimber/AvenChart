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
