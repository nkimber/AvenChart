// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export type AppointmentSemanticStatus =
  | "scheduled"
  | "pending"
  | "arrived"
  | "in-room"
  | "completed"
  | "no-show"
  | "cancelled"
  | "other";

export type AppointmentStatusDefinition = {
  apiValue: string;
  label: string;
  semantic: AppointmentSemanticStatus;
  className: string;
  terminal: boolean;
  aliases: readonly string[];
};

export const APPOINTMENT_STATUSES: readonly AppointmentStatusDefinition[] = [
  {
    apiValue: "-",
    label: "Scheduled",
    semantic: "scheduled",
    className: "appt-status-scheduled",
    terminal: false,
    aliases: ["", "-", "scheduled", "booked", "confirmed"],
  },
  {
    apiValue: "~",
    label: "Pending",
    semantic: "pending",
    className: "appt-status-pending",
    terminal: false,
    aliases: ["~", "pending", "requested", "request"],
  },
  {
    apiValue: "!",
    label: "Follow-up needed",
    semantic: "pending",
    className: "appt-status-pending",
    terminal: false,
    aliases: ["!", "left-message", "left message"],
  },
  {
    apiValue: "@",
    label: "Arrived",
    semantic: "arrived",
    className: "appt-status-arrived",
    terminal: false,
    aliases: ["@", "arrived", "checked-in", "checked in"],
  },
  {
    apiValue: ">",
    label: "In room",
    semantic: "in-room",
    className: "appt-status-in-room",
    terminal: false,
    aliases: [">", "in-room", "in room", "roomed"],
  },
  {
    apiValue: "<",
    label: "Checked out",
    semantic: "completed",
    className: "appt-status-completed",
    terminal: true,
    aliases: ["<", "checked-out", "checked out", "complete", "completed"],
  },
  {
    apiValue: "?",
    label: "No show",
    semantic: "no-show",
    className: "appt-status-no-show",
    terminal: true,
    aliases: ["?", "no-show", "no show", "noshow"],
  },
  {
    apiValue: "x",
    label: "Cancelled",
    semantic: "cancelled",
    className: "appt-status-cancelled",
    terminal: true,
    aliases: [
      "x",
      "cancelled",
      "canceled",
      "cancel",
      "cancelled by patient",
      "canceled by patient",
    ],
  },
] as const;

const allowedTransitions: Record<
  AppointmentSemanticStatus,
  readonly AppointmentSemanticStatus[]
> = {
  scheduled: ["pending", "arrived", "no-show", "cancelled"],
  pending: ["scheduled", "arrived", "no-show", "cancelled"],
  arrived: ["in-room", "no-show", "cancelled"],
  "in-room": ["completed", "cancelled"],
  completed: [],
  "no-show": ["scheduled"],
  cancelled: ["scheduled"],
  other: [
    "scheduled",
    "pending",
    "arrived",
    "in-room",
    "completed",
    "no-show",
    "cancelled",
  ],
};

const unknownStatus: AppointmentStatusDefinition = {
  apiValue: "",
  label: "Other status",
  semantic: "other",
  className: "appt-status-other",
  terminal: false,
  aliases: [],
};

export function getAppointmentStatus(
  value?: string | null,
): AppointmentStatusDefinition {
  const normalized = value?.trim().toLowerCase() ?? "";
  return (
    APPOINTMENT_STATUSES.find((status) =>
      status.aliases.includes(normalized),
    ) ?? {
      ...unknownStatus,
      apiValue: value?.trim() ?? "",
    }
  );
}

export function getAppointmentStatusOptions(
  currentValue?: string | null,
): readonly AppointmentStatusDefinition[] {
  const current = getAppointmentStatus(currentValue);
  const allowed = new Set(allowedTransitions[current.semantic]);
  const knownOptions = APPOINTMENT_STATUSES.filter(
    (status) =>
      status.semantic === current.semantic || allowed.has(status.semantic),
  );
  return current.semantic === "other" && current.apiValue
    ? [current, ...knownOptions]
    : knownOptions;
}

export function isCancelledAppointment(value?: string | null): boolean {
  return getAppointmentStatus(value).semantic === "cancelled";
}
