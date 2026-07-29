import { apiBaseUrl, apiFetch } from "./transport.ts";

export type ClinicalFormOption = {
  code: string;
  display: string;
};

export type ClinicalFormSection = {
  key: string;
  title: string;
  sequence: number;
  description: string | null;
};

export type ClinicalFormField = {
  key: string;
  sectionKey: string;
  label: string;
  type: string;
  sequence: number;
  required: boolean;
  accessibilityLabel: string;
  helpText: string | null;
  maxLength: number | null;
  minimum: number | null;
  maximum: number | null;
  precision: number | null;
  unit: string | null;
  codeSystem: string | null;
  options: ClinicalFormOption[];
  repeatMinimum: number | null;
  repeatMaximum: number | null;
  children: ClinicalFormField[];
  readOnly: boolean;
};

export type ClinicalFormCondition = {
  fieldKey: string;
  operator: string;
  value?: unknown;
};

export type ClinicalFormCalculation = {
  operator: string;
  operands: Array<{ fieldKey: string | null; constant: number | null }>;
  precision: number | null;
};

export type ClinicalFormRule = {
  key: string;
  condition: ClinicalFormCondition;
  action: string;
  targetFieldKey: string;
  message: string | null;
  calculation: ClinicalFormCalculation | null;
};

export type ClinicalFormSchema = {
  stableKey: string;
  name: string;
  purpose: string;
  contextScope: "patient" | "encounter";
  owningService: string;
  capability: string;
  signaturePolicy: "author-only" | "author-and-cosigner";
  sections: ClinicalFormSection[];
  fields: ClinicalFormField[];
  rules: ClinicalFormRule[];
};

export type ClinicalFormPolicy = {
  revision: string;
  rendererVersion: string;
  signaturePolicyRevision: string;
  supportedFieldTypes: string[];
  supportedRuleActions: string[];
  supportedCalculationOperators: string[];
  supportedConditionOperators: string[];
  definitionStates: string[];
  instanceStates: string[];
  forbiddenCapabilities: string[];
  productionBlockers: string[];
  arbitraryScriptsAllowed: boolean;
  rawHtmlAllowed: boolean;
  externalFetchAllowed: boolean;
  previewPersistsClinicalData: boolean;
  productionSignatureStandardApproved: boolean;
};

export type ClinicalFormDefinitionSummary = {
  definitionId: string;
  stableKey: string;
  name: string;
  purpose: string;
  contextScope: string;
  latestRevision: number;
  effectiveRevision: number | null;
  latestStatus: string;
  latestVersion: number;
  signaturePolicy: string;
  updatedAt: string;
  updatedBy: string;
};

export type ClinicalFormDefinitionList = {
  definitions: ClinicalFormDefinitionSummary[];
  total: number;
  page: number;
  pageSize: number;
};

export type ClinicalFormRevision = {
  definitionId: string;
  revision: number;
  status: string;
  version: number;
  definition: ClinicalFormSchema;
  rendererVersion: string;
  schemaHash: string;
  author: string;
  reviewedBy: string | null;
  approvedBy: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  createdAt: string;
  updatedAt: string;
  updatedBy: string;
  predecessorRevision: number | null;
};

export type ClinicalFormDefinitionEvent = {
  eventId: number;
  revision: number;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  actor: string;
  reason: string;
  occurredAt: string;
  snapshotHash: string;
};

export type ClinicalFormDefinitionDetail = {
  definition: ClinicalFormDefinitionSummary;
  currentRevision: ClinicalFormRevision;
  revisions: ClinicalFormRevision[];
  events: ClinicalFormDefinitionEvent[];
};

export type ClinicalFormValidationIssue = {
  fieldKey: string;
  severity: "error" | "warning";
  message: string;
  ruleKey: string | null;
};

export type ClinicalFormRuleEvaluation = {
  ruleKey: string;
  triggered: boolean;
  action: string;
  targetFieldKey: string;
  explanation: string;
};

export type ClinicalFormEvaluation = {
  values: Record<string, unknown>;
  visibleFields: Record<string, boolean>;
  requiredFields: Record<string, boolean>;
  issues: ClinicalFormValidationIssue[];
  ruleEvaluations: ClinicalFormRuleEvaluation[];
  valid: boolean;
};

export type ClinicalFormSignature = {
  signatureId: string;
  role: string;
  signer: string;
  method: string;
  policyRevision: string;
  credentialContext: string;
  signedAt: string;
  contentHash: string;
};

export type ClinicalFormInstanceEvent = {
  eventId: number;
  version: number;
  action: string;
  fromState: string | null;
  toState: string;
  actor: string;
  reason: string;
  occurredAt: string;
  snapshotHash: string;
};

export type ClinicalFormInstanceSummary = {
  instanceId: string;
  definitionId: string;
  definitionRevision: number;
  stableKey: string;
  name: string;
  patientId: string;
  encounterId: number | null;
  state: string;
  version: number;
  author: string;
  signaturePolicy: string;
  predecessorInstanceId: string | null;
  successorInstanceId: string | null;
  amendmentReason: string | null;
  createdAt: string;
  updatedAt: string;
  finalizedAt: string | null;
  signedAt: string | null;
};

export type ClinicalFormInstanceDetail = {
  instance: ClinicalFormInstanceSummary;
  definition: ClinicalFormSchema;
  values: Record<string, unknown>;
  validation: ClinicalFormEvaluation;
  signatures: ClinicalFormSignature[];
  events: ClinicalFormInstanceEvent[];
};

export type ClinicalFormInstanceList = {
  instances: ClinicalFormInstanceSummary[];
  total: number;
};

export type LegacyClinicalFormSnapshotSummary = {
  snapshotId: string;
  sourceSystem: string;
  sourceBaselineVersion: string;
  extractionRevision: string;
  sourceTable: string;
  sourceRowId: string;
  sourceRevision: string;
  stableKey: string;
  name: string;
  patientId: string;
  encounterId: number;
  sourceActive: boolean;
  sourceRecordedAt: string | null;
  capturedAt: string;
  rawSha256: string;
  adapterRevision: string;
  targetDefinitionRevision: number;
  targetSchemaHash: string;
  unmappedCount: number;
  readOnly: boolean;
  converted: boolean;
};

export type LegacyClinicalFormSnapshotList = {
  snapshots: LegacyClinicalFormSnapshotSummary[];
  total: number;
  returned: number;
  limit: number;
};

export type LegacyClinicalFormDisplayField = {
  sourceField: string;
  targetField: string | null;
  label: string;
  sourceValue: unknown;
  displayValue: string;
  mappingState: "exact" | "normalized" | "unmapped";
  mappingNote: string | null;
};

export type LegacyClinicalFormUnmappedFact = {
  sourceField: string;
  sourceValue: unknown;
  reason: string;
};

export type LegacyClinicalFormSnapshotDetail = {
  snapshot: LegacyClinicalFormSnapshotSummary;
  sourceSchema: string;
  targetDefinitionId: string;
  targetRendererRevision: string;
  rawValues: Record<string, unknown>;
  fields: LegacyClinicalFormDisplayField[];
  unmappedFacts: LegacyClinicalFormUnmappedFact[];
  migrationApproved: boolean;
  governedInstanceId: string | null;
};

export type LegacyClinicalFormMigrationMappingRule = {
  sourceField: string;
  targetField: string;
  transform: string;
  knownCodes?: Record<string, string>;
};

export type LegacyClinicalFormMigrationContract = {
  contractRevision: string;
  mappingRules: LegacyClinicalFormMigrationMappingRule[];
  changedSemantics: string[];
  errorDisposition: string[];
  reconciliationRequired: string[];
  compensationRollback: string[];
  requiredApprovals: string[];
};

export type LegacyClinicalFormMigrationManifest = {
  manifestId: string;
  stableKey: string;
  sourceSystem: string;
  sourceBaselineVersion: string;
  extractionRevision: string;
  sourceSchema: string;
  sourceTable: string;
  targetDefinitionRevision: number;
  targetSchemaHash: string;
  targetRendererRevision: string;
  manifestRevision: number;
  version: number;
  status: string;
  contract: LegacyClinicalFormMigrationContract;
  blockers: string[];
  manifestSha256: string;
  productionApproved: boolean;
  executionEnabled: boolean;
  reviewedBy: string | null;
  reviewedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  decisionReason: string | null;
  createdAt: string;
  updatedAt: string;
  updatedBy: string;
};

export type LegacyClinicalFormMigrationManifestEvent = {
  eventId: number;
  version: number;
  action: "created" | "review" | "approve" | "reject";
  fromStatus: string | null;
  toStatus: string;
  actor: string;
  reason: string;
  occurredAt: string;
  snapshotSha256: string;
};

export type LegacyClinicalFormMigrationManifestDecision = {
  manifestId: string;
  version: number;
  status: string;
  productionApproved: boolean;
  executionEnabled: boolean;
  decision: LegacyClinicalFormMigrationManifestEvent;
};

export type LegacyClinicalFormMigrationRowDisposition = {
  snapshotId: string;
  sourceRowId: string;
  sourceActive: boolean;
  unmappedCount: number;
  disposition: "eligible-for-review" | "blocked";
  reasons: string[];
};

export type LegacyClinicalFormMigrationReconciliation = {
  sourceRows: number;
  activeRows: number;
  inactiveRows: number;
  fullyMappedRows: number;
  rowsWithUnmappedFacts: number;
  eligibleRows: number;
  blockedRows: number;
  governedInstancesCreated: number;
  sourceSnapshotDigest: string;
  rows: LegacyClinicalFormMigrationRowDisposition[];
};

export type LegacyClinicalFormMigrationManifestResponse = {
  manifest: LegacyClinicalFormMigrationManifest;
  patientId: string;
  reconciliation: LegacyClinicalFormMigrationReconciliation;
  events: LegacyClinicalFormMigrationManifestEvent[];
  allowedActions: Array<"review" | "approve" | "reject">;
};

export type ClinicalFormRender = {
  instance: ClinicalFormInstanceSummary;
  definition: ClinicalFormSchema;
  values: Record<string, unknown>;
  signatures: ClinicalFormSignature[];
  contentHash: string;
  renderedAt: string;
  rendererVersion: string;
};

export type ClinicalFormFieldDictionaryItem = {
  fieldKey: string;
  path: string;
  parentFieldKey: string | null;
  sectionKey: string;
  sectionTitle: string;
  label: string;
  type: string;
  required: boolean;
  repeating: boolean;
  codeSystem: string | null;
  unit: string | null;
  reportColumn: string;
};

export type ClinicalFormFieldDictionary = {
  definitionId: string;
  stableKey: string;
  revision: number;
  schemaHash: string;
  rendererVersion: string;
  fields: ClinicalFormFieldDictionaryItem[];
};

export type ClinicalFormStructuredExport = {
  exportFormat: string;
  exportedAt: string;
  instance: ClinicalFormInstanceSummary;
  definition: ClinicalFormSchema;
  schemaHash: string;
  rendererVersion: string;
  contentHash: string;
  fieldDictionary: ClinicalFormFieldDictionary;
  values: Record<string, unknown>;
  signatures: ClinicalFormSignature[];
};

function headers(sessionId: string, json = false) {
  return {
    "X-Legacy EHR-Session": sessionId,
    ...(json ? { "content-type": "application/json" } : {}),
  };
}

export async function getClinicalFormPolicy(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ClinicalFormPolicy> {
  const response = await apiFetch(`${apiBaseUrl}/api/form-engine/policy`, {
    headers: headers(sessionId),
    signal,
  });
  return (await response.json()) as ClinicalFormPolicy;
}

export async function previewClinicalForm(
  sessionId: string,
  definition: ClinicalFormSchema,
  values: Record<string, unknown>,
  signal?: AbortSignal,
): Promise<ClinicalFormEvaluation> {
  const response = await apiFetch(`${apiBaseUrl}/api/form-engine/preview`, {
    method: "POST",
    headers: headers(sessionId, true),
    body: JSON.stringify({ definition, values }),
    signal,
  });
  return (await response.json()) as ClinicalFormEvaluation;
}

export async function getClinicalFormCatalog(
  sessionId: string,
  search = "",
  signal?: AbortSignal,
): Promise<ClinicalFormDefinitionList> {
  const query = new URLSearchParams({ page: "1", pageSize: "100" });
  if (search) query.set("search", search);
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/catalog?${query}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ClinicalFormDefinitionList;
}

export async function getClinicalFormDefinitions(
  sessionId: string,
  options: {
    search?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  } = {},
  signal?: AbortSignal,
): Promise<ClinicalFormDefinitionList> {
  const query = new URLSearchParams({
    page: String(options.page ?? 1),
    pageSize: String(options.pageSize ?? 20),
  });
  if (options.search) query.set("search", options.search);
  if (options.status) query.set("status", options.status);
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions?${query}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ClinicalFormDefinitionList;
}

export async function getClinicalFormDefinition(
  sessionId: string,
  definitionId: string,
  signal?: AbortSignal,
): Promise<ClinicalFormDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions/${encodeURIComponent(definitionId)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ClinicalFormDefinitionDetail;
}

export async function createClinicalFormDefinition(
  sessionId: string,
  definition: ClinicalFormSchema,
  reason: string,
): Promise<ClinicalFormDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ definition, reason }),
    },
  );
  return (await response.json()) as ClinicalFormDefinitionDetail;
}

export async function createClinicalFormRevision(
  sessionId: string,
  definitionId: string,
  definition: ClinicalFormSchema,
  expectedLatestRevision: number,
  reason: string,
): Promise<ClinicalFormDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions/${encodeURIComponent(definitionId)}/revisions`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({
        definition,
        expectedLatestRevision,
        reason,
      }),
    },
  );
  return (await response.json()) as ClinicalFormDefinitionDetail;
}

export async function transitionClinicalFormDefinition(
  sessionId: string,
  definitionId: string,
  action: string,
  revision: number,
  expectedVersion: number,
  reason: string,
  effectiveFrom?: string | null,
  effectiveTo?: string | null,
): Promise<ClinicalFormDefinitionDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions/${encodeURIComponent(definitionId)}/${encodeURIComponent(action)}`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({
        revision,
        expectedVersion,
        reason,
        effectiveFrom: effectiveFrom ?? null,
        effectiveTo: effectiveTo ?? null,
      }),
    },
  );
  return (await response.json()) as ClinicalFormDefinitionDetail;
}

export async function getPatientClinicalFormInstances(
  sessionId: string,
  patientId: string,
  encounterId?: number,
  signal?: AbortSignal,
): Promise<ClinicalFormInstanceList> {
  const query = new URLSearchParams();
  if (encounterId !== undefined) query.set("encounterId", String(encounterId));
  const suffix = query.size > 0 ? `?${query}` : "";
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/patients/${encodeURIComponent(patientId)}/instances${suffix}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ClinicalFormInstanceList;
}

export async function getPatientLegacyClinicalFormSnapshots(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<LegacyClinicalFormSnapshotList> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/patients/${encodeURIComponent(patientId)}/legacy-snapshots`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as LegacyClinicalFormSnapshotList;
}

export async function getLegacyClinicalFormSnapshot(
  sessionId: string,
  snapshotId: string,
  signal?: AbortSignal,
): Promise<LegacyClinicalFormSnapshotDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/legacy-snapshots/${encodeURIComponent(snapshotId)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as LegacyClinicalFormSnapshotDetail;
}

export async function getPatientLegacyClinicalFormMigrationManifest(
  sessionId: string,
  patientId: string,
  stableKey: string,
  signal?: AbortSignal,
): Promise<LegacyClinicalFormMigrationManifestResponse> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/patients/${encodeURIComponent(patientId)}/legacy-migration-manifests/${encodeURIComponent(stableKey)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as LegacyClinicalFormMigrationManifestResponse;
}

export async function transitionLegacyClinicalFormMigrationManifest(
  sessionId: string,
  manifestId: string,
  action: "review" | "approve" | "reject",
  expectedVersion: number,
  reason: string,
): Promise<LegacyClinicalFormMigrationManifestDecision> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/legacy-migration-manifests/${encodeURIComponent(manifestId)}/${action}`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason }),
    },
  );
  return (await response.json()) as LegacyClinicalFormMigrationManifestDecision;
}

export async function createPatientClinicalFormInstance(
  sessionId: string,
  patientId: string,
  input: {
    definitionId: string;
    revision?: number | null;
    encounterId?: number | null;
    idempotencyKey: string;
    values?: Record<string, unknown>;
    reason: string;
  },
): Promise<ClinicalFormInstanceDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/patients/${encodeURIComponent(patientId)}/instances`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify(input),
    },
  );
  return (await response.json()) as ClinicalFormInstanceDetail;
}

export async function getClinicalFormInstance(
  sessionId: string,
  instanceId: string,
  signal?: AbortSignal,
): Promise<ClinicalFormInstanceDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}`,
    { headers: headers(sessionId), signal },
  );
  return (await response.json()) as ClinicalFormInstanceDetail;
}

export async function updateClinicalFormInstance(
  sessionId: string,
  instanceId: string,
  expectedVersion: number,
  values: Record<string, unknown>,
  reason: string,
): Promise<ClinicalFormInstanceDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}`,
    {
      method: "PUT",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, values, reason }),
    },
  );
  return (await response.json()) as ClinicalFormInstanceDetail;
}

export async function transitionClinicalFormInstance(
  sessionId: string,
  instanceId: string,
  action: "finalize" | "sign" | "cosign",
  expectedVersion: number,
  reason: string,
): Promise<ClinicalFormInstanceDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/${action}`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason }),
    },
  );
  return (await response.json()) as ClinicalFormInstanceDetail;
}

export async function amendClinicalFormInstance(
  sessionId: string,
  instanceId: string,
  expectedVersion: number,
  reason: string,
  idempotencyKey: string,
): Promise<ClinicalFormInstanceDetail> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/amend`,
    {
      method: "POST",
      headers: headers(sessionId, true),
      body: JSON.stringify({ expectedVersion, reason, idempotencyKey }),
    },
  );
  return (await response.json()) as ClinicalFormInstanceDetail;
}

export async function renderClinicalFormInstance(
  sessionId: string,
  instanceId: string,
): Promise<ClinicalFormRender> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/render`,
    { headers: headers(sessionId) },
  );
  return (await response.json()) as ClinicalFormRender;
}

export async function getClinicalFormInstanceFieldDictionary(
  sessionId: string,
  instanceId: string,
): Promise<ClinicalFormFieldDictionary> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/field-dictionary`,
    { headers: headers(sessionId) },
  );
  return (await response.json()) as ClinicalFormFieldDictionary;
}

export async function exportClinicalFormInstanceStructured(
  sessionId: string,
  instanceId: string,
): Promise<ClinicalFormStructuredExport> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/structured-export`,
    { headers: headers(sessionId) },
  );
  return (await response.json()) as ClinicalFormStructuredExport;
}

export async function exportClinicalFormInstanceHtml(
  sessionId: string,
  instanceId: string,
): Promise<string> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/form-engine/instances/${encodeURIComponent(instanceId)}/export`,
    { headers: headers(sessionId) },
  );
  return await response.text();
}

export async function deleteClinicalFormTestFixture(
  sessionId: string,
  definitionId: string,
): Promise<void> {
  await apiFetch(
    `${apiBaseUrl}/api/form-engine/definitions/${encodeURIComponent(definitionId)}/test-fixture`,
    { method: "DELETE", headers: headers(sessionId) },
  );
}
