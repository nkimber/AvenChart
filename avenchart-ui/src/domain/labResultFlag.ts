// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export type LabResultFlagState =
  | "normal"
  | "abnormal"
  | "high"
  | "low"
  | "critical"
  | "unknown";

export type NormalizedLabResultFlag = {
  state: LabResultFlagState;
  label: string | null;
  isAlert: boolean;
};

const normalValues = new Set([
  "",
  "0",
  "false",
  "n",
  "no",
  "none",
  "normal",
]);

export function normalizeLabResultFlag(
  value?: string | boolean | null,
): NormalizedLabResultFlag {
  const normalized = String(value ?? "")
    .trim()
    .toLowerCase();

  if (normalValues.has(normalized)) {
    return { state: "normal", label: null, isAlert: false };
  }

  if (
    normalized === "critical" ||
    normalized === "panic" ||
    normalized === "hh" ||
    normalized === "ll"
  ) {
    return { state: "critical", label: "Critical", isAlert: true };
  }

  if (normalized === "h" || normalized === "high") {
    return { state: "high", label: "High", isAlert: true };
  }

  if (normalized === "l" || normalized === "low") {
    return { state: "low", label: "Low", isAlert: true };
  }

  if (
    normalized === "a" ||
    normalized === "aa" ||
    normalized === "abnormal" ||
    normalized === "true" ||
    normalized === "yes" ||
    normalized === "1"
  ) {
    return { state: "abnormal", label: "Abnormal", isAlert: true };
  }

  return { state: "unknown", label: "Review flag", isAlert: true };
}
