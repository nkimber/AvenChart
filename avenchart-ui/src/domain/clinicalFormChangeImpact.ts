import type {
  ClinicalFormField,
  ClinicalFormRule,
  ClinicalFormSchema,
  ClinicalFormSection,
} from "../api/clinicalForms.ts";

export type ClinicalFormImpactSeverity = "high" | "review" | "low";

export type ClinicalFormImpactItem = {
  key: string;
  severity: ClinicalFormImpactSeverity;
  title: string;
  description: string;
};

export type ClinicalFormChangeImpact = {
  items: ClinicalFormImpactItem[];
  highCount: number;
  reviewCount: number;
  lowCount: number;
};

const severityRank: Record<ClinicalFormImpactSeverity, number> = {
  low: 0,
  review: 1,
  high: 2,
};

function highestSeverity(
  severities: ClinicalFormImpactSeverity[],
): ClinicalFormImpactSeverity {
  return severities.reduce((highest, severity) =>
    severityRank[severity] > severityRank[highest] ? severity : highest,
  );
}

function describeValue(value: unknown) {
  if (value === null || value === undefined || value === "") return "none";
  if (typeof value === "boolean") return value ? "yes" : "no";
  return String(value);
}

function canonicalJson(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map(canonicalJson).join(",")}]`;
  }
  if (value && typeof value === "object") {
    return `{${Object.entries(value)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, nested]) => `${JSON.stringify(key)}:${canonicalJson(nested)}`)
      .join(",")}}`;
  }
  return JSON.stringify(value) ?? "undefined";
}

function flattenFields(
  fields: ClinicalFormField[],
  parentPath = "",
): Map<string, ClinicalFormField> {
  const flattened = new Map<string, ClinicalFormField>();
  for (const field of fields) {
    const path = parentPath ? `${parentPath}.${field.key}` : field.key;
    flattened.set(path, field);
    for (const [childPath, child] of flattenFields(
      field.children ?? [],
      path,
    )) {
      flattened.set(childPath, child);
    }
  }
  return flattened;
}

function compareMetadata(
  previous: ClinicalFormSchema,
  candidate: ClinicalFormSchema,
): ClinicalFormImpactItem[] {
  const contracts: Array<{
    property: keyof ClinicalFormSchema;
    label: string;
    severity: ClinicalFormImpactSeverity;
  }> = [
    { property: "stableKey", label: "Stable key", severity: "high" },
    { property: "name", label: "Name", severity: "review" },
    { property: "purpose", label: "Clinical purpose", severity: "review" },
    { property: "contextScope", label: "Context scope", severity: "high" },
    { property: "owningService", label: "Owning service", severity: "high" },
    {
      property: "capability",
      label: "Required capability",
      severity: "high",
    },
    {
      property: "signaturePolicy",
      label: "Signature policy",
      severity: "high",
    },
  ];

  return contracts.flatMap(({ property, label, severity }) =>
    previous[property] === candidate[property]
      ? []
      : [
          {
            key: `metadata:${property}`,
            severity,
            title: `${label} changed`,
            description: `${label} changes from ${describeValue(previous[property])} to ${describeValue(candidate[property])}.`,
          },
        ],
  );
}

function compareSections(
  previous: ClinicalFormSection[],
  candidate: ClinicalFormSection[],
): ClinicalFormImpactItem[] {
  const before = new Map(previous.map((section) => [section.key, section]));
  const after = new Map(candidate.map((section) => [section.key, section]));
  const keys = [...new Set([...before.keys(), ...after.keys()])].sort();
  return keys.flatMap<ClinicalFormImpactItem>((key) => {
    const oldSection = before.get(key);
    const newSection = after.get(key);
    if (!oldSection && newSection) {
      return [
        {
          key: `section:${key}:added`,
          severity: "low" as const,
          title: `Section ${key} added`,
          description: `Adds section “${newSection.title}” at sequence ${newSection.sequence}.`,
        },
      ];
    }
    if (oldSection && !newSection) {
      return [
        {
          key: `section:${key}:removed`,
          severity: "high" as const,
          title: `Section ${key} removed`,
          description: `Removes section “${oldSection.title}” and requires review of every field formerly assigned to it.`,
        },
      ];
    }
    if (!oldSection || !newSection) return [];

    const changes: string[] = [];
    if (oldSection.title !== newSection.title) {
      changes.push(
        `title changes from “${oldSection.title}” to “${newSection.title}”`,
      );
    }
    if (oldSection.sequence !== newSection.sequence) {
      changes.push(
        `sequence changes from ${oldSection.sequence} to ${newSection.sequence}`,
      );
    }
    if (oldSection.description !== newSection.description) {
      changes.push("description changes");
    }
    return changes.length === 0
      ? []
      : [
          {
            key: `section:${key}:changed`,
            severity: "review" as const,
            title: `Section ${key} changed`,
            description: `${changes.join("; ")}.`,
          },
        ];
  });
}

function compareField(
  path: string,
  previous: ClinicalFormField,
  candidate: ClinicalFormField,
): ClinicalFormImpactItem[] {
  const changes: Array<{
    severity: ClinicalFormImpactSeverity;
    description: string;
  }> = [];
  const add = (
    severity: ClinicalFormImpactSeverity,
    description: string,
  ) => changes.push({ severity, description });

  if (previous.type !== candidate.type) {
    add("high", `type changes from ${previous.type} to ${candidate.type}`);
  }
  if (previous.sectionKey !== candidate.sectionKey) {
    add(
      "review",
      `section changes from ${previous.sectionKey || "parent repeat"} to ${candidate.sectionKey || "parent repeat"}`,
    );
  }
  if (previous.required !== candidate.required) {
    add(
      candidate.required ? "high" : "review",
      candidate.required
        ? "becomes required"
        : "is no longer unconditionally required",
    );
  }
  if (previous.readOnly !== candidate.readOnly) {
    add(
      "high",
      candidate.readOnly ? "becomes read-only" : "becomes editable",
    );
  }
  if (
    previous.label !== candidate.label ||
    previous.accessibilityLabel !== candidate.accessibilityLabel ||
    previous.helpText !== candidate.helpText
  ) {
    add("review", "label, accessibility label, or help text changes");
  }
  if (previous.sequence !== candidate.sequence) {
    add(
      "low",
      `sequence changes from ${previous.sequence} to ${candidate.sequence}`,
    );
  }

  const compareLowerBound = (
    label: string,
    oldValue: number | null,
    newValue: number | null,
  ) => {
    if (oldValue === newValue) return;
    const restrictive =
      newValue !== null && (oldValue === null || newValue > oldValue);
    add(
      restrictive ? "high" : "low",
      `${label} ${restrictive ? "tightens" : "relaxes"} from ${describeValue(oldValue)} to ${describeValue(newValue)}`,
    );
  };
  const compareUpperBound = (
    label: string,
    oldValue: number | null,
    newValue: number | null,
  ) => {
    if (oldValue === newValue) return;
    const restrictive =
      newValue !== null && (oldValue === null || newValue < oldValue);
    add(
      restrictive ? "high" : "low",
      `${label} ${restrictive ? "tightens" : "relaxes"} from ${describeValue(oldValue)} to ${describeValue(newValue)}`,
    );
  };

  compareUpperBound("maximum length", previous.maxLength, candidate.maxLength);
  compareLowerBound("minimum", previous.minimum, candidate.minimum);
  compareUpperBound("maximum", previous.maximum, candidate.maximum);
  compareLowerBound(
    "minimum repeat count",
    previous.repeatMinimum,
    candidate.repeatMinimum,
  );
  compareUpperBound(
    "maximum repeat count",
    previous.repeatMaximum,
    candidate.repeatMaximum,
  );

  if (previous.precision !== candidate.precision) {
    const restrictive =
      candidate.precision !== null &&
      (previous.precision === null ||
        candidate.precision < previous.precision);
    add(
      restrictive ? "high" : "review",
      `precision changes from ${describeValue(previous.precision)} to ${describeValue(candidate.precision)}`,
    );
  }
  if (previous.unit !== candidate.unit) {
    add(
      "high",
      `unit changes from ${describeValue(previous.unit)} to ${describeValue(candidate.unit)}`,
    );
  }
  if (previous.codeSystem !== candidate.codeSystem) {
    add(
      "high",
      `code system changes from ${describeValue(previous.codeSystem)} to ${describeValue(candidate.codeSystem)}`,
    );
  }

  const oldOptions = new Map(
    previous.options.map((option) => [option.code, option.display]),
  );
  const newOptions = new Map(
    candidate.options.map((option) => [option.code, option.display]),
  );
  const removedOptions = [...oldOptions.keys()].filter(
    (code) => !newOptions.has(code),
  );
  const addedOptions = [...newOptions.keys()].filter(
    (code) => !oldOptions.has(code),
  );
  const renamedOptions = [...oldOptions.keys()].filter(
    (code) =>
      newOptions.has(code) && oldOptions.get(code) !== newOptions.get(code),
  );
  if (removedOptions.length > 0) {
    add("high", `removes option codes ${removedOptions.sort().join(", ")}`);
  }
  if (addedOptions.length > 0) {
    add("review", `adds option codes ${addedOptions.sort().join(", ")}`);
  }
  if (renamedOptions.length > 0) {
    add(
      "review",
      `changes displays for option codes ${renamedOptions.sort().join(", ")}`,
    );
  }

  if (changes.length === 0) return [];
  return [
    {
      key: `field:${path}:changed`,
      severity: highestSeverity(changes.map((change) => change.severity)),
      title: `Field ${path} changed`,
      description: `${changes.map((change) => change.description).join("; ")}.`,
    },
  ];
}

function compareFields(
  previous: ClinicalFormField[],
  candidate: ClinicalFormField[],
): ClinicalFormImpactItem[] {
  const before = flattenFields(previous);
  const after = flattenFields(candidate);
  const paths = [...new Set([...before.keys(), ...after.keys()])].sort();
  return paths.flatMap<ClinicalFormImpactItem>((path) => {
    const oldField = before.get(path);
    const newField = after.get(path);
    if (!oldField && newField) {
      return [
        {
          key: `field:${path}:added`,
          severity: newField.required ? ("high" as const) : ("low" as const),
          title: `Field ${path} added`,
          description: `Adds ${newField.required ? "a required" : "an optional"} ${newField.type} field labeled “${newField.label}”.`,
        },
      ];
    }
    if (oldField && !newField) {
      return [
        {
          key: `field:${path}:removed`,
          severity: "high" as const,
          title: `Field ${path} removed`,
          description: `Removes the ${oldField.type} field labeled “${oldField.label}”; historical revisions remain unchanged.`,
        },
      ];
    }
    return oldField && newField
      ? compareField(path, oldField, newField)
      : [];
  });
}

function compareRule(
  key: string,
  previous: ClinicalFormRule,
  candidate: ClinicalFormRule,
): ClinicalFormImpactItem[] {
  const changes: string[] = [];
  if (canonicalJson(previous.condition) !== canonicalJson(candidate.condition)) {
    changes.push("trigger condition changes");
  }
  if (previous.action !== candidate.action) {
    changes.push(`action changes from ${previous.action} to ${candidate.action}`);
  }
  if (previous.targetFieldKey !== candidate.targetFieldKey) {
    changes.push(
      `target changes from ${previous.targetFieldKey} to ${candidate.targetFieldKey}`,
    );
  }
  if (
    canonicalJson(previous.calculation) !==
    canonicalJson(candidate.calculation)
  ) {
    changes.push("calculation contract changes");
  }
  if (previous.message !== candidate.message) {
    changes.push("warning or explanation text changes");
  }
  return changes.length === 0
    ? []
    : [
        {
          key: `rule:${key}:changed`,
          severity: "high" as const,
          title: `Rule ${key} changed`,
          description: `${changes.join("; ")}.`,
        },
      ];
}

function compareRules(
  previous: ClinicalFormRule[],
  candidate: ClinicalFormRule[],
): ClinicalFormImpactItem[] {
  const before = new Map(previous.map((rule) => [rule.key, rule]));
  const after = new Map(candidate.map((rule) => [rule.key, rule]));
  const keys = [...new Set([...before.keys(), ...after.keys()])].sort();
  return keys.flatMap<ClinicalFormImpactItem>((key) => {
    const oldRule = before.get(key);
    const newRule = after.get(key);
    if (!oldRule && newRule) {
      return [
        {
          key: `rule:${key}:added`,
          severity: "high" as const,
          title: `Rule ${key} added`,
          description: `Adds a ${newRule.action} action targeting ${newRule.targetFieldKey}.`,
        },
      ];
    }
    if (oldRule && !newRule) {
      return [
        {
          key: `rule:${key}:removed`,
          severity: "high" as const,
          title: `Rule ${key} removed`,
          description: `Removes the ${oldRule.action} action targeting ${oldRule.targetFieldKey}.`,
        },
      ];
    }
    return oldRule && newRule ? compareRule(key, oldRule, newRule) : [];
  });
}

export function describeClinicalFormChangeImpact(
  previous: ClinicalFormSchema,
  candidate: ClinicalFormSchema,
): ClinicalFormChangeImpact {
  const items = [
    ...compareMetadata(previous, candidate),
    ...compareSections(previous.sections, candidate.sections),
    ...compareFields(previous.fields, candidate.fields),
    ...compareRules(previous.rules, candidate.rules),
  ];
  return {
    items,
    highCount: items.filter((item) => item.severity === "high").length,
    reviewCount: items.filter((item) => item.severity === "review").length,
    lowCount: items.filter((item) => item.severity === "low").length,
  };
}
