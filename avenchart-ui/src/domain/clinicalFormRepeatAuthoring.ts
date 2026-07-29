import type { ClinicalFormField } from "../api/clinicalForms.ts";

export const clinicalFormRepeatChildLimit = 20;

export function createSafeClinicalFormField(
  index = 1,
  sectionKey = "clinical",
): ClinicalFormField {
  return {
    key: `field_${index}`,
    sectionKey,
    label: `Field ${index}`,
    type: "text",
    sequence: index * 10,
    required: false,
    accessibilityLabel: `Field ${index}`,
    helpText: null,
    maxLength: 240,
    minimum: null,
    maximum: null,
    precision: null,
    unit: null,
    codeSystem: null,
    options: [],
    optionListReference: null,
    repeatMinimum: null,
    repeatMaximum: null,
    children: [],
    readOnly: false,
  };
}

export function normalizeClinicalFormFieldType(
  field: ClinicalFormField,
  type: string,
): ClinicalFormField {
  const numeric = ["integer", "decimal", "measurement", "computed"].includes(
    type,
  );
  const option = ["select", "multiselect", "coded"].includes(type);
  const repeat = type === "repeat";
  return {
    ...field,
    type,
    maxLength:
      type === "text" ? 240 : type === "multiline" ? 4000 : null,
    minimum: numeric ? (field.minimum ?? 0) : null,
    maximum: numeric ? (field.maximum ?? 100) : null,
    precision:
      type === "integer" ? 0 : numeric ? (field.precision ?? 2) : null,
    unit: type === "measurement" ? (field.unit ?? "unit") : null,
    codeSystem:
      type === "coded"
        ? (field.codeSystem ?? "local-code-system-v1")
        : option
          ? field.codeSystem
          : null,
    options: option
      ? field.options.length > 0
        ? field.options
        : [
            { code: "option_a", display: "Option A" },
            { code: "option_b", display: "Option B" },
          ]
      : [],
    optionListReference: option
      ? (field.optionListReference ?? null)
      : null,
    repeatMinimum: repeat ? 0 : null,
    repeatMaximum: repeat ? 5 : null,
    children: repeat
      ? field.children.length > 0
        ? field.children
        : [
            {
              ...createSafeClinicalFormField(1, ""),
              key: `${field.key}_detail`,
              label: "Detail",
              accessibilityLabel: "Repeating row detail",
            },
          ]
      : [],
    readOnly: type === "computed",
    required: type === "computed" ? false : field.required,
  };
}

export function clinicalFormRepeatChildTypes(
  supportedFieldTypes: string[],
): string[] {
  return supportedFieldTypes.filter(
    (type) => type !== "repeat" && type !== "computed",
  );
}

export function appendClinicalFormRepeatChild(
  field: ClinicalFormField,
): ClinicalFormField {
  if (
    field.type !== "repeat" ||
    field.children.length >= clinicalFormRepeatChildLimit
  ) {
    return field;
  }

  const nextNumber = field.children.length + 1;
  const usedKeys = new Set(field.children.map((child) => child.key));
  let keyNumber = nextNumber;
  let key = `${field.key}_field_${keyNumber}`;
  while (usedKeys.has(key)) {
    keyNumber += 1;
    key = `${field.key}_field_${keyNumber}`;
  }
  const nextSequence =
    Math.max(0, ...field.children.map((child) => child.sequence)) + 10;
  const child = {
    ...createSafeClinicalFormField(nextNumber, ""),
    key,
    label: `Child ${nextNumber}`,
    accessibilityLabel: `Repeating row child ${nextNumber}`,
    sequence: nextSequence,
  };

  return { ...field, children: [...field.children, child] };
}

export function removeClinicalFormRepeatChild(
  field: ClinicalFormField,
  childIndex: number,
): ClinicalFormField {
  if (
    field.type !== "repeat" ||
    field.children.length <= 1 ||
    childIndex < 0 ||
    childIndex >= field.children.length
  ) {
    return field;
  }

  return {
    ...field,
    children: field.children.filter((_, index) => index !== childIndex),
  };
}

export function parseClinicalFormOptionLines(value: string) {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [code, ...display] = line.split("|");
      return {
        code,
        display: display.join("|") || code,
      };
    });
}
