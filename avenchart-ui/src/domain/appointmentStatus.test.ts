import { describe, expect, it } from "vitest";
import {
  getAppointmentStatus,
  getAppointmentStatusOptions,
  isCancelledAppointment,
} from "./appointmentStatus.ts";

describe("appointment status model", () => {
  it.each([
    ["-", "Scheduled", "scheduled"],
    ["@", "Arrived", "arrived"],
    [">", "In room", "in-room"],
    ["<", "Checked out", "completed"],
    ["x", "Cancelled", "cancelled"],
    ["Canceled by patient", "Cancelled", "cancelled"],
    ["?", "No show", "no-show"],
    ["pending", "Pending", "pending"],
  ])("normalizes %s to %s", (raw, label, semantic) => {
    expect(getAppointmentStatus(raw)).toMatchObject({ label, semantic });
  });

  it("does not expose an unknown raw code as the user-facing label", () => {
    expect(getAppointmentStatus("vendor-code")).toMatchObject({
      label: "Other status",
      semantic: "other",
    });
    expect(getAppointmentStatusOptions("vendor-code")[0]).toMatchObject({
      apiValue: "vendor-code",
      label: "Other status",
    });
  });

  it("limits a roomed appointment to completion or cancellation", () => {
    expect(
      getAppointmentStatusOptions(">").map((status) => status.label),
    ).toEqual(["In room", "Checked out", "Cancelled"]);
  });

  it("recognizes cancellation aliases", () => {
    expect(isCancelledAppointment("CANCELED")).toBe(true);
  });
});
