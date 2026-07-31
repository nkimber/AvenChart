// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from "vitest";
import { normalizeLabResultFlag } from "./labResultFlag.ts";

describe("normalizeLabResultFlag", () => {
  it.each([undefined, null, "", "no", "NO", "false", false, "normal", "N"])(
    "treats %s as a normal result",
    (value) => {
      expect(normalizeLabResultFlag(value)).toEqual({
        state: "normal",
        label: null,
        isAlert: false,
      });
    },
  );

  it.each([
    ["H", "high", "High"],
    ["low", "low", "Low"],
    ["A", "abnormal", "Abnormal"],
    ["panic", "critical", "Critical"],
    ["HH", "critical", "Critical"],
  ])("maps %s to an accessible %s label", (value, state, label) => {
    expect(normalizeLabResultFlag(value)).toMatchObject({
      state,
      label,
      isAlert: true,
    });
  });

  it("keeps an unknown non-empty flag visible without exposing an unexplained code", () => {
    expect(normalizeLabResultFlag("vendor-x")).toEqual({
      state: "unknown",
      label: "Review flag",
      isAlert: true,
    });
  });
});
