import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getPatientPortalAppointmentsWithRequestHistory } from "./portalAppointments.ts";

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("patient portal appointment history transport", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("uses the protected portal session and retains lifecycle evidence", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({
        authenticated: true,
        appointmentRequestCount: 1,
        appointmentRequests: [
          {
            appointmentId: "APPT-PORTAL-1",
            state: "pending",
            stateLabel: "Pending practice review",
            stateSource: "stored lifecycle",
            requestedAt: "2026-07-29T20:00:00Z",
            updatedAt: "2026-07-29T20:00:00Z",
            nextAction: "The practice will review this request.",
            version: 1,
            date: "2026-08-01",
            startTime: "09:30",
            durationMinutes: 20,
            title: "Office visit",
            rawStatus: "^",
            evidenceSource: "runtime",
            events: [
              {
                eventId: "40000000-0000-0000-0000-000000000001",
                sequence: 1,
                action: "requested",
                state: "pending",
                rawStatus: "^",
                occurredAt: "2026-07-29T20:00:00Z",
                evidenceSource: "runtime",
              },
            ],
          },
        ],
      }),
    );

    const result =
      await getPatientPortalAppointmentsWithRequestHistory("portal-session");

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/patient-portal/appointments"),
      expect.objectContaining({
        headers: {
          "X-Legacy EHR-Patient-Portal-Session": "portal-session",
        },
      }),
    );
    expect(result.appointmentRequests[0]).toMatchObject({
      state: "pending",
      version: 1,
      events: [{ action: "requested", evidenceSource: "runtime" }],
    });
  });
});
