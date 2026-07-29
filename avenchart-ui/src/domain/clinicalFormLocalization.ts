import type {
  ClinicalFormDefinitionSummary,
  ClinicalFormField,
  ClinicalFormFieldLocalization,
  ClinicalFormLocalization,
  ClinicalFormRule,
  ClinicalFormSchema,
} from "../api/clinicalForms.ts";

function flattenFields(fields: ClinicalFormField[]): ClinicalFormField[] {
  return fields.flatMap((field) => [
    field,
    ...flattenFields(field.children ?? []),
  ]);
}

function flattenRules(schema: ClinicalFormSchema): ClinicalFormRule[] {
  return [
    ...schema.rules,
    ...flattenFields(schema.fields).flatMap((field) => field.rowRules ?? []),
  ];
}

function synchronizeLocalization(
  schema: ClinicalFormSchema,
  localization: ClinicalFormLocalization,
): ClinicalFormLocalization {
  const sections = new Map(
    localization.sections.map((section) => [section.sectionKey, section]),
  );
  const fields = new Map(
    localization.fields.map((field) => [field.fieldKey, field]),
  );
  const rules = new Map(
    (localization.rules ?? []).map((rule) => [rule.ruleKey, rule]),
  );

  return {
    ...localization,
    sections: schema.sections.map((section) => ({
      sectionKey: section.key,
      title: sections.get(section.key)?.title ?? section.title,
      description: sections.has(section.key)
        ? (sections.get(section.key)?.description ?? null)
        : section.description,
    })),
    fields: flattenFields(schema.fields).map((field) => {
      const current = fields.get(field.key);
      const options = new Map(
        (current?.options ?? []).map((option) => [option.code, option]),
      );
      return {
        fieldKey: field.key,
        label: current?.label ?? field.label,
        accessibilityLabel:
          current?.accessibilityLabel ?? field.accessibilityLabel,
        helpText: current ? current.helpText : field.helpText,
        options: field.options.map((option) => ({
          code: option.code,
          display: options.get(option.code)?.display ?? option.display,
        })),
      };
    }),
    rules: flattenRules(schema).map((rule) => ({
      ruleKey: rule.key,
      message: rules.has(rule.key)
        ? (rules.get(rule.key)?.message ?? null)
        : rule.message,
    })),
  };
}

export function synchronizeClinicalFormLocalizations(
  schema: ClinicalFormSchema,
): ClinicalFormSchema {
  const localizations = schema.localizations ?? [];
  return {
    ...schema,
    localizations:
      localizations.length === 0
        ? null
        : localizations.map((localization) =>
            synchronizeLocalization(schema, localization),
          ),
  };
}

export function createClinicalFormLocalization(
  schema: ClinicalFormSchema,
  locale: string,
): ClinicalFormSchema {
  const synchronized = synchronizeClinicalFormLocalizations(schema);
  if (
    synchronized.localizations?.some(
      (localization) => localization.locale === locale,
    )
  ) {
    return synchronized;
  }

  const fields: ClinicalFormFieldLocalization[] = flattenFields(
    synchronized.fields,
  ).map((field) => ({
    fieldKey: field.key,
    label: field.label,
    accessibilityLabel: field.accessibilityLabel,
    helpText: field.helpText,
    options: field.options.map((option) => ({ ...option })),
  }));
  return {
    ...synchronized,
    localizations: [
      ...(synchronized.localizations ?? []),
      {
        locale,
        name: synchronized.name,
        purpose: synchronized.purpose,
        sections: synchronized.sections.map((section) => ({
          sectionKey: section.key,
          title: section.title,
          description: section.description,
        })),
        fields,
        rules: flattenRules(synchronized).map((rule) => ({
          ruleKey: rule.key,
          message: rule.message,
        })),
      },
    ],
  };
}

function localizeField(
  field: ClinicalFormField,
  localizations: ReadonlyMap<string, ClinicalFormFieldLocalization>,
  ruleLocalizations: ReadonlyMap<
    string,
    ClinicalFormLocalization["rules"][number]
  >,
): ClinicalFormField {
  const localization = localizations.get(field.key);
  if (!localization) return field;
  const options = new Map(
    localization.options.map((option) => [option.code, option.display]),
  );
  return {
    ...field,
    label: localization.label,
    accessibilityLabel: localization.accessibilityLabel,
    helpText: localization.helpText,
    options: field.options.map((option) => ({
      ...option,
      display: options.get(option.code) ?? option.display,
    })),
    children: field.children.map((child) =>
      localizeField(child, localizations, ruleLocalizations),
    ),
    rowRules: field.rowRules?.map((rule) => ({
      ...rule,
      message: ruleLocalizations.get(rule.key)?.message ?? rule.message,
    })),
  };
}

export function localizeClinicalFormSchema(
  schema: ClinicalFormSchema,
  locale: string,
): ClinicalFormSchema {
  const synchronized = synchronizeClinicalFormLocalizations(schema);
  const localization = synchronized.localizations?.find(
    (candidate) => candidate.locale === locale,
  );
  if (!localization) return synchronized;

  const sections = new Map(
    localization.sections.map((section) => [section.sectionKey, section]),
  );
  const fields = new Map(
    localization.fields.map((field) => [field.fieldKey, field]),
  );
  const rules = new Map(
    (localization.rules ?? []).map((rule) => [rule.ruleKey, rule]),
  );
  return {
    ...synchronized,
    name: localization.name,
    purpose: localization.purpose,
    sections: synchronized.sections.map((section) => {
      const translated = sections.get(section.key);
      return translated
        ? {
            ...section,
            title: translated.title,
            description: translated.description,
          }
        : section;
    }),
    fields: synchronized.fields.map((field) =>
      localizeField(field, fields, rules),
    ),
    rules: synchronized.rules.map((rule) => ({
      ...rule,
      message: rules.get(rule.key)?.message ?? rule.message,
    })),
  };
}

export function localizeClinicalFormSummary(
  summary: ClinicalFormDefinitionSummary,
  locale: string,
): ClinicalFormDefinitionSummary {
  const localization = summary.localizations?.find(
    (candidate) => candidate.locale === locale,
  );
  return localization
    ? {
        ...summary,
        name: localization.name,
        purpose: localization.purpose,
      }
    : summary;
}
