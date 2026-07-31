// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { PatientPortalAppointmentsResponse } from "../api.ts";
import { apiBaseUrl, apiFetch } from "./transport.ts";

export type PatientPortalAppointmentRequestHistoryEvent = {
  eventId: string;
  sequence: number;
  action: string;
  state: "pending" | "accepted" | "declined" | "cancelled";
  rawStatus: string;
  occurredAt: string;
  evidenceSource: "runtime" | "migration-backfill";
};

export type PatientPortalAppointmentRequestHistoryItem = {
  appointmentId: string;
  state: "pending" | "accepted" | "declined" | "expired" | "cancelled";
  stateLabel: string;
  stateSource: string;
  requestedAt: string;
  updatedAt: string;
  nextAction: string;
  version: number;
  date: string;
  startTime: string;
  durationMinutes: number;
  categoryId?: number | null;
  categoryName?: string | null;
  providerId?: number | null;
  providerName?: string | null;
  facilityId?: number | null;
  facilityName?: string | null;
  title: string;
  reason?: string | null;
  rawStatus: string;
  evidenceSource: "runtime" | "migration-backfill";
  events: PatientPortalAppointmentRequestHistoryEvent[];
};

export type PatientPortalAppointmentsWithRequestHistoryResponse =
  PatientPortalAppointmentsResponse & {
    appointmentRequestCount: number;
    appointmentRequests: PatientPortalAppointmentRequestHistoryItem[];
  };

export async function getPatientPortalAppointmentsWithRequestHistory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentsWithRequestHistoryResponse> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/patient-portal/appointments`,
    {
      headers: { "X-Legacy EHR-Patient-Portal-Session": sessionId },
      signal,
    },
  );
  return response.json();
}
