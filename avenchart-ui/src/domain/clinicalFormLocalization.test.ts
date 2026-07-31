// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from "vitest";
import type {
  ClinicalFormField,
  ClinicalFormSchema,
} from "../api/clinicalForms.ts";
import {
  createClinicalFormLocalization,
  localizeClinicalFormSchema,
  localizeClinicalFormSummary,
  synchronizeClinicalFormLocalizations,
} from "./clinicalFormLocalization.ts";

function field(
  key: string,
  options: ClinicalFormField["options"] = [],
): ClinicalFormField {
  return {
    key,
    sectionKey: "main",
    label: `Label ${key}`,
    type: options.length > 0 ? "select" : "text",
    sequence: 10,
    required: false,
    accessibilityLabel: `Accessible ${key}`,
    helpText: `Help ${key}`,
    maxLength: options.length > 0 ? null : 240,
    minimum: null,
    maximum: null,
    precision: null,
    unit: null,
    codeSystem: null,
    options,
    repeatMinimum: null,
    repeatMaximum: null,
    children: [],
    readOnly: false,
  };
}

function schema(): ClinicalFormSchema {
  const repeat = field("observations");
  repeat.type = "repeat";
  repeat.maxLength = null;
  repeat.repeatMinimum = 0;
  repeat.repeatMaximum = 3;
  repeat.children = [field("note")];
  repeat.children[0]!.sectionKey = "";
  repeat.rowRules = [
    {
      key: "warn_note",
      condition: { fieldKey: "note", operator: "is-not-empty" },
      action: "warning",
      targetFieldKey: "note",
      message: "Review the note.",
      calculation: null,
    },
  ];

  return {
    stableKey: "tmp.form.localization",
    name: "Localized form",
    purpose: "Verify bounded clinical-form localization.",
    contextScope: "encounter",
    owningService: "clinical_operations",
    capability: "encounters.auth_a",
    signaturePolicy: "author-only",
    sections: [
      {
        key: "main",
        title: "Main section",
        sequence: 10,
        description: "Base section description.",
      },
    ],
    fields: [
      field("decision", [
        { code: "yes", display: "Yes" },
        { code: "no", display: "No" },
      ]),
      repeat,
    ],
    rules: [],
  };
}

describe("clinical form localization", () => {
  it("starts a complete locale from the immutable base schema", () => {
    const localized = createClinicalFormLocalization(schema(), "es-US");
    expect(localized.localizations).toHaveLength(1);
    expect(localized.localizations?.[0]).toMatchObject({
      locale: "es-US",
      name: "Localized form",
      sections: [{ sectionKey: "main", title: "Main section" }],
      fields: [
        {
          fieldKey: "decision",
          options: [
            { code: "yes", display: "Yes" },
            { code: "no", display: "No" },
          ],
        },
        { fieldKey: "observations" },
        { fieldKey: "note" },
      ],
      rules: [{ ruleKey: "warn_note", message: "Review the note." }],
    });
  });

  it("keeps translations aligned to stable keys and option codes", () => {
    const localized = createClinicalFormLocalization(schema(), "es-US");
    const translation = localized.localizations![0]!;
    translation.fields[0]!.label = "Decisión";
    translation.fields[0]!.helpText = null;
    translation.fields[0]!.options[0]!.display = "Sí";
    localized.fields[0]!.options = [
      { code: "yes", display: "Yes" },
      { code: "later", display: "Later" },
    ];
    localized.fields.push(field("comment"));

    const synchronized = synchronizeClinicalFormLocalizations(localized);
    expect(synchronized.localizations?.[0]?.fields).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          fieldKey: "decision",
          label: "Decisión",
          helpText: null,
          options: [
            { code: "yes", display: "Sí" },
            { code: "later", display: "Later" },
          ],
        }),
        expect.objectContaining({
          fieldKey: "comment",
          label: "Label comment",
        }),
      ]),
    );
  });

  it("localizes form, section, field, option, and repeat-child presentation", () => {
    const localized = createClinicalFormLocalization(schema(), "es-US");
    const translation = localized.localizations![0]!;
    translation.name = "Formulario localizado";
    translation.purpose = "Propósito localizado.";
    translation.sections[0]!.title = "Sección principal";
    translation.fields[0]!.label = "Decisión";
    translation.fields[0]!.accessibilityLabel = "Decisión clínica";
    translation.fields[0]!.options[0]!.display = "Sí";
    translation.fields[2]!.label = "Nota";
    translation.rules[0]!.message = "Revise la nota.";

    const displayed = localizeClinicalFormSchema(localized, "es-US");
    expect(displayed.name).toBe("Formulario localizado");
    expect(displayed.purpose).toBe("Propósito localizado.");
    expect(displayed.sections[0]?.title).toBe("Sección principal");
    expect(displayed.fields[0]).toMatchObject({
      label: "Decisión",
      accessibilityLabel: "Decisión clínica",
      options: [
        { code: "yes", display: "Sí" },
        { code: "no", display: "No" },
      ],
    });
    expect(displayed.fields[1]?.children[0]?.label).toBe("Nota");
    expect(displayed.fields[1]?.rowRules?.[0]?.message).toBe(
      "Revise la nota.",
    );
    expect(localizeClinicalFormSchema(localized, "fr-CA").name).toBe(
      "Localized form",
    );
  });

  it("localizes catalog summaries and falls back when a locale is absent", () => {
    const localized = createClinicalFormLocalization(schema(), "es-US");
    localized.localizations![0]!.name = "Formulario localizado";
    localized.localizations![0]!.purpose = "Propósito localizado.";
    const summary = {
      definitionId: "11111111-1111-1111-1111-111111111111",
      stableKey: localized.stableKey,
      name: localized.name,
      purpose: localized.purpose,
      contextScope: localized.contextScope,
      latestRevision: 1,
      effectiveRevision: 1,
      latestStatus: "effective",
      latestVersion: 3,
      signaturePolicy: localized.signaturePolicy,
      updatedAt: "2026-07-29T00:00:00Z",
      updatedBy: "admin",
      localizations: localized.localizations,
    };

    expect(localizeClinicalFormSummary(summary, "es-US").name).toBe(
      "Formulario localizado",
    );
    expect(localizeClinicalFormSummary(summary, "fr-CA").name).toBe(
      "Localized form",
    );
  });
});
