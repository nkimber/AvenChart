// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from "vitest";
import {
  appendClinicalFormRepeatChild,
  clinicalFormRepeatChildLimit,
  clinicalFormRepeatChildTypes,
  createSafeClinicalFormField,
  normalizeClinicalFormFieldType,
  parseClinicalFormOptionLines,
  removeClinicalFormRepeatChild,
} from "./clinicalFormRepeatAuthoring.ts";

describe("clinical form repeat authoring", () => {
  it("creates a bounded repeat with one safe text child", () => {
    const repeat = normalizeClinicalFormFieldType(
      createSafeClinicalFormField(1),
      "repeat",
    );

    expect(repeat).toEqual(
      expect.objectContaining({
        type: "repeat",
        repeatMinimum: 0,
        repeatMaximum: 5,
      }),
    );
    expect(repeat.children).toEqual([
      expect.objectContaining({
        key: "field_1_detail",
        sectionKey: "",
        type: "text",
        maxLength: 240,
      }),
    ]);
  });

  it("appends unique immutable children only through the server limit", () => {
    const original = normalizeClinicalFormFieldType(
      createSafeClinicalFormField(1),
      "repeat",
    );
    let repeat = original;
    for (let index = 1; index < clinicalFormRepeatChildLimit + 2; index += 1) {
      repeat = appendClinicalFormRepeatChild(repeat);
    }

    expect(original.children).toHaveLength(1);
    expect(repeat.children).toHaveLength(clinicalFormRepeatChildLimit);
    expect(new Set(repeat.children.map((child) => child.key)).size).toBe(
      clinicalFormRepeatChildLimit,
    );
    expect(repeat.children.at(-1)?.sequence).toBe(200);
  });

  it("excludes nested repeats while allowing same-row computed outputs", () => {
    expect(
      clinicalFormRepeatChildTypes([
        "text",
        "repeat",
        "integer",
        "computed",
        "coded",
      ]),
    ).toEqual(["text", "integer", "computed", "coded"]);
  });

  it("keeps one child and parses bounded option rows", () => {
    const repeat = normalizeClinicalFormFieldType(
      createSafeClinicalFormField(1),
      "repeat",
    );
    expect(removeClinicalFormRepeatChild(repeat, 0)).toBe(repeat);
    expect(
      parseClinicalFormOptionLines("yes|Yes\nno|No answer\n"),
    ).toEqual([
      { code: "yes", display: "Yes" },
      { code: "no", display: "No answer" },
    ]);
  });
});
