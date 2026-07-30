import {
  type EncounterCreateInput,
  type EncounterDetail,
  type ProcedureOrderItem,
} from "../api.ts";
import {
  apiBaseUrl,
  apiFetch,
  requireSuccessfulResponse,
} from "./transport.ts";

export type CompleteEncounterCreateInput = EncounterCreateInput & {
  billingFacilityId?: number | null;
  referralSource?: string | null;
  externalId?: string | null;
  posCode?: number | null;
  billingNote?: string | null;
  sourceAppointmentId?: string | null;
};

export type EncounterBillingLine = {
  id: string;
  encounter: number;
  billingDate: string;
  codeType?: string | null;
  code?: string | null;
  modifier?: string | null;
  codeText?: string | null;
  fee?: number | null;
  justify?: string | null;
  units: number;
  billed: number;
  activity: number;
};

export type EncounterBillingClaim = {
  id: string;
  encounter: number;
  version: number;
  payerId: number;
  payerName?: string | null;
  payerType: number;
  status: number;
  statusLabel: string;
  billProcess: number;
  billTime?: string | null;
  processTime?: string | null;
  processFile?: string | null;
  target?: string | null;
  submittedClaim?: string | null;
};

export type EncounterCodingDetail = EncounterDetail & {
  billingLines: EncounterBillingLine[];
  claims: EncounterBillingClaim[];
  procedureOrders: ProcedureOrderItem[];
};

export type BillingLineCreateInput = {
  patientId: string;
  providerId?: number | null;
  encounter: number;
  billingDate: string;
  codeType: string;
  code: string;
  modifier?: string | null;
  codeText: string;
  fee: number;
  units: number;
  justify: string;
};

function clinicianHeaders(sessionId: string) {
  return {
    "content-type": "application/json",
    "X-Legacy EHR-Session": sessionId,
  };
}

export async function createCompleteEncounter(
  sessionId: string,
  input: CompleteEncounterCreateInput,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  const response = await apiFetch(`${apiBaseUrl}/api/encounters`, {
    method: "POST",
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(input),
    signal,
  });
  await requireSuccessfulResponse(
    response,
    "POST /api/encounters",
    "clinician",
  );
  return response.json();
}

export async function createEncounterBillingLine(
  sessionId: string,
  input: BillingLineCreateInput,
  signal?: AbortSignal,
): Promise<{ id: string }> {
  const response = await apiFetch(`${apiBaseUrl}/api/billing/lines`, {
    method: "POST",
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(input),
    signal,
  });
  await requireSuccessfulResponse(
    response,
    "POST /api/billing/lines",
    "clinician",
  );
  return response.json();
}

export function asEncounterCodingDetail(
  detail: EncounterDetail,
): EncounterCodingDetail {
  return detail as EncounterCodingDetail;
}
