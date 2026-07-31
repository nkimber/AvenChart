// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { normalizeLabResultFlag } from "../domain/labResultFlag.ts";

export function labResultFlagClass(value?: string | null) {
  const normalized = normalizeLabResultFlag(value);
  if (!normalized.isAlert) return "";
  if (normalized.state === "high") return "lab-result-high";
  if (normalized.state === "low") return "lab-result-low";
  if (normalized.state === "critical") return "lab-result-critical";
  return "lab-result-abnormal";
}

export function LabResultFlag({ value }: { value?: string | null }) {
  const normalized = normalizeLabResultFlag(value);
  if (!normalized.isAlert || !normalized.label) return null;

  return (
    <span className={`lab-result-flag ${labResultFlagClass(value)}`}>
      {normalized.label}
    </span>
  );
}
