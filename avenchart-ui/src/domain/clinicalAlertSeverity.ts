// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export type ClinicalAlertSeverity =
  | "info"
  | "warning"
  | "critical"
  | "unknown";

export type ClinicalAlertSeverityPresentation = {
  severity: ClinicalAlertSeverity;
  label: string;
  badgeClassName: string;
};

export function getClinicalAlertSeverity(
  value?: string | null,
): ClinicalAlertSeverityPresentation {
  switch (value?.trim().toLowerCase()) {
    case "info":
      return {
        severity: "info",
        label: "Information alert",
        badgeClassName: "cl-badge-blue",
      };
    case "warning":
      return {
        severity: "warning",
        label: "Warning alert",
        badgeClassName: "cl-badge-amber",
      };
    case "critical":
      return {
        severity: "critical",
        label: "Critical alert",
        badgeClassName: "cl-badge-red",
      };
    default:
      return {
        severity: "unknown",
        label: "Review alert",
        badgeClassName: "cl-badge-muted",
      };
  }
}
