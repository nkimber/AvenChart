import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  amendClinicalFormInstance,
  createClinicalFormDefinition,
  createPatientClinicalFormInstance,
  exportClinicalFormInstanceStructured,
  getClinicalFormCatalog,
  getClinicalFormInstanceFieldDictionary,
  getLegacyClinicalFormSnapshot,
  getPatientLegacyClinicalFormMigrationManifest,
  getPatientLegacyClinicalFormSnapshots,
  previewClinicalForm,
  transitionClinicalFormDefinition,
  transitionClinicalFormInstance,
  updateClinicalFormInstance,
  type ClinicalFormSchema,
} from "./clinicalForms.ts";

const schema: ClinicalFormSchema = {
  stableKey: "tmp.form.transport",
  name: "Transport form",
  purpose: "Verify the clinical-form browser transport contract.",
  contextScope: "encounter",
  owningService: "clinical_operations",
  capability: "encounters.auth_a",
  signaturePolicy: "author-and-cosigner",
  sections: [
    {
      key: "main",
      title: "Main",
      sequence: 10,
      description: "Transport verification",
    },
  ],
  fields: [
    {
      key: "chief_concern",
      sectionKey: "main",
      label: "Chief concern",
      type: "multiline",
      sequence: 10,
      required: true,
      accessibilityLabel: "Chief concern",
      helpText: null,
      maxLength: 500,
      minimum: null,
      maximum: null,
      precision: null,
      unit: null,
      codeSystem: null,
      options: [],
      repeatMinimum: null,
      repeatMaximum: null,
      children: [],
      readOnly: false,
    },
  ],
  rules: [],
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("governed clinical-form transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("encodes bounded catalog filters and sends the complete definition", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ definitions: [], total: 0, page: 2, pageSize: 5 }),
      )
      .mockResolvedValueOnce(
        jsonResponse(
          {
            definition: {
              definitionId: "definition-1",
              stableKey: schema.stableKey,
            },
            currentRevision: { revision: 1, status: "draft", version: 0 },
            revisions: [],
            events: [],
          },
          201,
        ),
      );

    await getClinicalFormCatalog("staff-session", "pain & follow-up");
    await createClinicalFormDefinition(
      "staff-session",
      schema,
      "Create the bounded synthetic definition.",
    );

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/catalog?page=1&pageSize=100&search=pain+%26+follow-up",
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({
      definition: schema,
      reason: "Create the bounded synthetic definition.",
    });
    expect(fetchMock.mock.calls[1]?.[1]).toEqual(
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("forwards caller cancellation to non-persisting form previews", async () => {
    const controller = new AbortController();
    fetchMock.mockImplementationOnce((_input, init) => {
      const transportSignal = init?.signal;
      return new Promise<Response>((_resolve, reject) => {
        transportSignal?.addEventListener(
          "abort",
          () => reject(new DOMException("Aborted", "AbortError")),
          { once: true },
        );
      });
    });

    const preview = previewClinicalForm(
      "staff-session",
      schema,
      { chief_concern: "Live draft" },
      controller.signal,
    );
    const cancelled = expect(preview).rejects.toMatchObject({
      kind: "cancelled",
    });
    controller.abort();
    await cancelled;

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[1]?.signal?.aborted).toBe(true);
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      definition: schema,
      values: { chief_concern: "Live draft" },
    });
  });

  it("encodes read-only legacy snapshot and migration-manifest paths", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ snapshots: [], total: 0, returned: 0, limit: 100 }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          snapshot: { snapshotId: "90f00000-0000-4000-9000-000000000001" },
          fields: [],
          unmappedFacts: [],
          readOnly: true,
          converted: false,
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          manifest: {
            stableKey: "legacy.clinicnote",
            status: "draft",
            productionApproved: false,
            executionEnabled: false,
          },
          patientId: "MOD PAT/1",
          reconciliation: { sourceRows: 0, rows: [] },
        }),
      );

    await getPatientLegacyClinicalFormSnapshots(
      "staff-session",
      "MOD PAT/1",
    );
    await getLegacyClinicalFormSnapshot(
      "staff-session",
      "90f00000-0000-4000-9000-000000000001",
    );
    await getPatientLegacyClinicalFormMigrationManifest(
      "staff-session",
      "MOD PAT/1",
      "legacy.clinicnote/v1",
    );

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/patients/MOD%20PAT%2F1/legacy-snapshots",
    );
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000001",
    );
    expect(fetchMock.mock.calls[2]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/patients/MOD%20PAT%2F1/legacy-migration-manifests/legacy.clinicnote%2Fv1",
    );
    expect(fetchMock.mock.calls[0]?.[1]?.headers).toMatchObject({
      "X-Legacy EHR-Session": "staff-session",
    });
  });

  it("sends loaded revision and instance versions for governed transitions", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          definition: { definitionId: "definition-1" },
          currentRevision: { revision: 2, status: "effective", version: 3 },
          revisions: [],
          events: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          instance: {
            instanceId: "instance-1",
            state: "ready-for-signature",
            version: 6,
          },
          definition: schema,
          values: {},
          validation: { valid: true, issues: [] },
          signatures: [],
          events: [],
        }),
      );

    await transitionClinicalFormDefinition(
      "staff-session",
      "definition-1",
      "activate",
      2,
      2,
      "Activate the approved successor.",
      "2026-08-01T00:00:00Z",
      null,
    );
    await transitionClinicalFormInstance(
      "staff-session",
      "instance-1",
      "finalize",
      5,
      "Finalize the validated draft.",
    );

    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      revision: 2,
      expectedVersion: 2,
      reason: "Activate the approved successor.",
      effectiveFrom: "2026-08-01T00:00:00Z",
      effectiveTo: null,
    });
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/instances/instance-1/finalize",
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({
      expectedVersion: 5,
      reason: "Finalize the validated draft.",
    });
  });

  it("preserves idempotency keys, values, and amendment evidence", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          instance: {
            instanceId: "instance-1",
            definitionRevision: 1,
            version: 0,
            state: "draft",
          },
          definition: schema,
          values: { chief_concern: "Pain" },
          validation: { valid: true, issues: [] },
          signatures: [],
          events: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          instance: { instanceId: "instance-1", version: 1, state: "draft" },
          definition: schema,
          values: { chief_concern: "Updated pain" },
          validation: { valid: true, issues: [] },
          signatures: [],
          events: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          instance: {
            instanceId: "instance-2",
            predecessorInstanceId: "instance-1",
            version: 0,
            state: "draft",
          },
          definition: schema,
          values: { chief_concern: "Updated pain" },
          validation: { valid: true, issues: [] },
          signatures: [],
          events: [],
        }, 201),
      );

    await createPatientClinicalFormInstance(
      "staff-session",
      "MOD-PAT-0001",
      {
        definitionId: "definition-1",
        revision: null,
        encounterId: 1000013,
        idempotencyKey: "create-form-1",
        values: { chief_concern: "Pain" },
        reason: "Create encounter form.",
      },
    );
    await updateClinicalFormInstance(
      "staff-session",
      "instance-1",
      0,
      { chief_concern: "Updated pain" },
      "Update typed value.",
    );
    await amendClinicalFormInstance(
      "staff-session",
      "instance-1",
      4,
      "Correct through successor.",
      "amend-form-1",
    );

    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual(
      expect.objectContaining({
        idempotencyKey: "create-form-1",
        encounterId: 1000013,
        values: { chief_concern: "Pain" },
      }),
    );
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({
      expectedVersion: 0,
      values: { chief_concern: "Updated pain" },
      reason: "Update typed value.",
    });
    expect(JSON.parse(String(fetchMock.mock.calls[2]?.[1]?.body))).toEqual({
      expectedVersion: 4,
      reason: "Correct through successor.",
      idempotencyKey: "amend-form-1",
    });
  });

  it("requests revision-pinned dictionary and structured export contracts", async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          definitionId: "definition-1",
          stableKey: schema.stableKey,
          definitionRevision: 2,
          schemaHash: "a".repeat(64),
          rendererVersion: "local-clinical-form-renderer-v1",
          fields: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          mediaType: "application/vnd.legacy-ehr.clinical-form+json;version=1",
          exportedAt: "2026-07-29T00:00:00Z",
          instance: { instanceId: "instance-1", definitionRevision: 2 },
          definition: schema,
          schemaHash: "a".repeat(64),
          rendererVersion: "local-clinical-form-renderer-v1",
          contentHash: "b".repeat(64),
          dictionary: { fields: [] },
          values: {},
          signatures: [],
        }),
      );

    await getClinicalFormInstanceFieldDictionary(
      "staff-session",
      "instance/with spaces",
    );
    await exportClinicalFormInstanceStructured(
      "staff-session",
      "instance/with spaces",
    );

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/instances/instance%2Fwith%20spaces/field-dictionary",
    );
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      "http://localhost:5001/api/form-engine/instances/instance%2Fwith%20spaces/structured-export",
    );
    expect(fetchMock.mock.calls[0]?.[1]?.headers).toEqual(
      expect.objectContaining({ "X-Legacy EHR-Session": "staff-session" }),
    );
  });
});
