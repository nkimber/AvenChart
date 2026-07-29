using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public static partial class ClinicalFormRuntime
{
    public const string RendererVersion = "local-clinical-form-renderer-v1";
    public const string PolicyRevision = "local-clinical-form-v1";
    public const string SignaturePolicyRevision = "local-clinical-signature-v1";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly IReadOnlyList<string> SupportedFieldTypes =
    [
        "text",
        "multiline",
        "integer",
        "decimal",
        "date",
        "datetime",
        "boolean",
        "select",
        "multiselect",
        "coded",
        "measurement",
        "repeat",
        "computed"
    ];

    public static readonly IReadOnlyList<string> SupportedRuleActions =
        ["show", "hide", "require", "warning", "calculate"];

    public static readonly IReadOnlyList<string> SupportedConditionOperators =
    [
        "equals",
        "not-equals",
        "greater-than",
        "greater-than-or-equal",
        "less-than",
        "less-than-or-equal",
        "is-empty",
        "is-not-empty"
    ];

    private static readonly HashSet<string> SupportedCalculationOperators =
        new(["add", "subtract", "multiply", "divide"], StringComparer.Ordinal);

    private static readonly string[] UnsafeTextFragments =
    [
        "<script",
        "</script",
        "javascript:",
        "vbscript:",
        "data:text/html",
        "onerror=",
        "onclick=",
        "onload=",
        "fetch(",
        "xmlhttprequest",
        "document.cookie",
        "window.location",
        "select * from",
        "insert into ",
        "delete from ",
        "drop table ",
        "alter table ",
        "exec(",
        "execute ",
        "{{",
        "{%"
    ];

    public static ClinicalFormPolicyResponse BuildPolicy() => new(
        PolicyRevision,
        RendererVersion,
        SignaturePolicyRevision,
        SupportedFieldTypes,
        SupportedRuleActions,
        SupportedConditionOperators,
        [
            "draft",
            "in-review",
            "approved",
            "effective",
            "suspended",
            "superseded",
            "retired",
            "rejected"
        ],
        [
            "draft",
            "ready-for-signature",
            "awaiting-co-sign",
            "signed",
            "amended",
            "corrected"
        ],
        [
            "arbitrary-script",
            "raw-html",
            "sql",
            "external-fetch",
            "direct-clinical-persistence",
            "unrestricted-patient-data",
            "unbounded-repeat"
        ],
        [
            "Clinical owner approval of field, rule, signature, and co-sign vocabularies.",
            "Production identity credential and supervision mapping.",
            "Independent author, reviewer, and approver role policy.",
            "Approved terminology and unit services beyond local bounded options.",
            "Representative clinician and accessibility validation for each adopted form.",
            "Historic migration/display-adapter and structured reporting policy.",
            "Retention, legal hold, disclosure, and release policy.",
            "Operational monitoring, recovery, and accountable production acceptance."
        ],
        ArbitraryScriptsAllowed: false,
        RawHtmlAllowed: false,
        ExternalFetchAllowed: false,
        PreviewPersistsClinicalData: false,
        ProductionSignatureStandardApproved: false);

    public static ClinicalFormSchemaDefinition Normalize(
        ClinicalFormSchemaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var stableKey = NormalizeKey(definition.StableKey, "Form stable key", dotted: true);
        var name = NormalizeText(definition.Name, "Form name", 120);
        var purpose = NormalizeText(definition.Purpose, "Clinical purpose", 1000);
        var contextScope = definition.ContextScope?.Trim().ToLowerInvariant();
        if (contextScope is not ("patient" or "encounter"))
        {
            throw new ArgumentException("Context scope must be patient or encounter.");
        }

        var owningService = NormalizeKey(
            definition.OwningService,
            "Owning service",
            dotted: false);
        var capability = NormalizeCapability(definition.Capability);
        var signaturePolicy = definition.SignaturePolicy?.Trim().ToLowerInvariant();
        if (signaturePolicy is not ("author-only" or "author-and-cosigner"))
        {
            throw new ArgumentException(
                "Signature policy must be author-only or author-and-cosigner.");
        }

        var sectionSource = definition.Sections ?? [];
        if (sectionSource.Count is < 1 or > 20)
        {
            throw new ArgumentException("A form must contain 1 to 20 sections.");
        }

        var sections = sectionSource
            .Select(section => new ClinicalFormSectionDefinition(
                NormalizeKey(section.Key, "Section key", dotted: false),
                NormalizeText(section.Title, "Section title", 120),
                ValidateSequence(section.Sequence, "Section"),
                NormalizeOptionalText(section.Description, "Section description", 500)))
            .OrderBy(section => section.Sequence)
            .ThenBy(section => section.Key, StringComparer.Ordinal)
            .ToArray();

        EnsureUnique(
            sections.Select(section => section.Key),
            "Section keys must be unique.");
        EnsureUnique(
            sections.Select(section => section.Sequence.ToString(CultureInfo.InvariantCulture)),
            "Section sequences must be unique.");

        var sectionKeys = sections
            .Select(section => section.Key)
            .ToHashSet(StringComparer.Ordinal);
        var fieldSource = definition.Fields ?? [];
        if (fieldSource.Count is < 1 or > 100)
        {
            throw new ArgumentException("A form must contain 1 to 100 fields.");
        }

        var fields = fieldSource
            .Select(field => NormalizeField(field, sectionKeys, nested: false))
            .OrderBy(field => sections.Single(section => section.Key == field.SectionKey).Sequence)
            .ThenBy(field => field.Sequence)
            .ThenBy(field => field.Key, StringComparer.Ordinal)
            .ToArray();

        var allFieldKeys = FlattenFields(fields)
            .Select(field => field.Key)
            .ToArray();
        EnsureUnique(allFieldKeys, "Field keys, including repeat children, must be unique.");
        foreach (var sectionGroup in fields.GroupBy(field => field.SectionKey))
        {
            EnsureUnique(
                sectionGroup.Select(field => field.Sequence.ToString(CultureInfo.InvariantCulture)),
                $"Field sequences must be unique in section {sectionGroup.Key}.");
        }

        var fieldMap = FlattenFields(fields)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
        var ruleSource = definition.Rules ?? [];
        if (ruleSource.Count > 50)
        {
            throw new ArgumentException("A form may contain at most 50 rules.");
        }

        var rules = ruleSource
            .Select(rule => NormalizeRule(rule, fieldMap))
            .OrderBy(rule => rule.Key, StringComparer.Ordinal)
            .ToArray();
        EnsureUnique(rules.Select(rule => rule.Key), "Rule keys must be unique.");
        EnsureAcyclicRules(rules);

        return new ClinicalFormSchemaDefinition(
            stableKey,
            name,
            purpose,
            contextScope,
            owningService,
            capability,
            signaturePolicy,
            sections,
            fields,
            rules);
    }

    public static ClinicalFormEvaluationResponse Evaluate(
        ClinicalFormSchemaDefinition rawDefinition,
        IReadOnlyDictionary<string, JsonElement>? rawValues)
    {
        var definition = Normalize(rawDefinition);
        var fields = FlattenFields(definition.Fields)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in rawValues ?? new Dictionary<string, JsonElement>())
        {
            values[pair.Key] = pair.Value.Clone();
        }

        var issues = new List<ClinicalFormValidationIssue>();
        foreach (var valueKey in values.Keys.Where(key => !fields.ContainsKey(key)))
        {
            issues.Add(new(
                valueKey,
                "error",
                "The value does not belong to the pinned form revision.",
                null));
        }

        var visible = fields.Keys.ToDictionary(key => key, _ => true, StringComparer.Ordinal);
        var required = fields.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Required,
            StringComparer.Ordinal);
        var evaluations = new List<ClinicalFormRuleEvaluation>();

        foreach (var rule in definition.Rules.Where(rule => rule.Action == "calculate"))
        {
            var triggered = ConditionMatches(rule.Condition, values);
            if (triggered && rule.Calculation is not null)
            {
                var calculated = Calculate(rule.Calculation, values);
                if (calculated is not null)
                {
                    values[rule.TargetFieldKey] = JsonSerializer.SerializeToElement(
                        calculated.Value,
                        JsonOptions);
                }
            }

            evaluations.Add(new(
                rule.Key,
                triggered,
                rule.Action,
                rule.TargetFieldKey,
                triggered
                    ? "The calculation condition matched; the bounded calculation was evaluated."
                    : "The calculation condition did not match."));
        }

        foreach (var rule in definition.Rules.Where(rule => rule.Action != "calculate"))
        {
            var triggered = ConditionMatches(rule.Condition, values);
            switch (rule.Action)
            {
                case "show" when triggered:
                    visible[rule.TargetFieldKey] = true;
                    break;
                case "hide" when triggered:
                    visible[rule.TargetFieldKey] = false;
                    break;
                case "require" when triggered:
                    required[rule.TargetFieldKey] = true;
                    break;
                case "warning" when triggered:
                    issues.Add(new(
                        rule.TargetFieldKey,
                        "warning",
                        rule.Message ?? "The configured warning condition matched.",
                        rule.Key));
                    break;
            }

            evaluations.Add(new(
                rule.Key,
                triggered,
                rule.Action,
                rule.TargetFieldKey,
                triggered
                    ? "The same-form condition matched and the declarative action was applied."
                    : "The same-form condition did not match."));
        }

        foreach (var field in fields.Values)
        {
            values.TryGetValue(field.Key, out var value);
            var hasValue = values.ContainsKey(field.Key) && !IsEmpty(value);
            if (visible[field.Key] && required[field.Key] && !hasValue)
            {
                issues.Add(new(
                    field.Key,
                    "error",
                    $"{field.Label} is required.",
                    null));
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            ValidateValue(field, value, issues);
        }

        return new ClinicalFormEvaluationResponse(
            values.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal),
            visible,
            required,
            issues,
            evaluations,
            issues.All(issue => issue.Severity != "error"));
    }

    public static string SerializeSchema(ClinicalFormSchemaDefinition definition) =>
        JsonSerializer.Serialize(Normalize(definition), JsonOptions);

    public static ClinicalFormSchemaDefinition DeserializeSchema(string json) =>
        Normalize(
            JsonSerializer.Deserialize<ClinicalFormSchemaDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException("The stored form schema is invalid."));

    public static string SerializeEvaluation(ClinicalFormEvaluationResponse evaluation) =>
        JsonSerializer.Serialize(evaluation, JsonOptions);

    public static ClinicalFormEvaluationResponse DeserializeEvaluation(string json) =>
        JsonSerializer.Deserialize<ClinicalFormEvaluationResponse>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored form validation result is invalid.");

    public static IReadOnlyDictionary<string, JsonElement> DeserializeValues(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)
        ?? new Dictionary<string, JsonElement>();

    public static string SerializeValues(IReadOnlyDictionary<string, JsonElement> values) =>
        JsonSerializer.Serialize(values, JsonOptions);

    public static string HashSchema(ClinicalFormSchemaDefinition definition) =>
        Hash(SerializeSchema(definition));

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }

    public static string HashInstance(
        Guid instanceId,
        int definitionRevision,
        int version,
        string state,
        IReadOnlyDictionary<string, JsonElement> values) =>
        Hash(JsonSerializer.Serialize(new
        {
            instanceId,
            definitionRevision,
            version,
            state,
            values
        }, JsonOptions));

    private static ClinicalFormFieldDefinition NormalizeField(
        ClinicalFormFieldDefinition field,
        IReadOnlySet<string> sectionKeys,
        bool nested)
    {
        var key = NormalizeKey(field.Key, "Field key", dotted: false);
        var sectionKey = nested
            ? ""
            : NormalizeKey(field.SectionKey, "Field section key", dotted: false);
        if (!nested && !sectionKeys.Contains(sectionKey))
        {
            throw new ArgumentException($"Field {key} references an unknown section.");
        }

        var label = NormalizeText(field.Label, "Field label", 160);
        var type = field.Type?.Trim().ToLowerInvariant();
        if (!SupportedFieldTypes.Contains(type, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Field {key} uses unsupported type {type}.");
        }

        if (nested && type == "repeat")
        {
            throw new ArgumentException("Nested repeating groups are not supported.");
        }

        var accessibilityLabel = NormalizeText(
            field.AccessibilityLabel,
            $"Accessibility label for {key}",
            200);
        var helpText = NormalizeOptionalText(field.HelpText, $"Help text for {key}", 500);
        var maxLength = field.MaxLength;
        if (type is "text" or "multiline")
        {
            maxLength ??= type == "text" ? 240 : 4000;
            if (maxLength is < 1 or > 10000)
            {
                throw new ArgumentException($"Field {key} max length must be 1 to 10000.");
            }
        }
        else if (maxLength is not null)
        {
            throw new ArgumentException($"Field {key} cannot declare max length for type {type}.");
        }

        var minimum = field.Minimum;
        var maximum = field.Maximum;
        if (minimum is not null && maximum is not null && maximum < minimum)
        {
            throw new ArgumentException($"Field {key} maximum cannot be below its minimum.");
        }

        if (type is not ("integer" or "decimal" or "measurement" or "computed")
            && (minimum is not null || maximum is not null || field.Precision is not null))
        {
            throw new ArgumentException($"Field {key} cannot declare numeric bounds.");
        }

        var precision = field.Precision;
        if (precision is < 0 or > 8)
        {
            throw new ArgumentException($"Field {key} precision must be 0 to 8.");
        }

        var unit = NormalizeOptionalText(field.Unit, $"Unit for {key}", 40);
        if (type == "measurement" && unit is null)
        {
            throw new ArgumentException($"Measurement field {key} requires a unit.");
        }

        if (type is not ("integer" or "decimal" or "measurement" or "computed")
            && unit is not null)
        {
            throw new ArgumentException(
                $"Only numeric, computed, or measurement fields may declare a unit.");
        }

        var codeSystem = NormalizeOptionalText(
            field.CodeSystem,
            $"Code system for {key}",
            120);
        var optionSource = field.Options ?? [];
        var options = optionSource
            .Select(option => new ClinicalFormOptionDefinition(
                NormalizeKey(option.Code, $"Option code for {key}", dotted: false),
                NormalizeText(option.Display, $"Option display for {key}", 160)))
            .ToArray();
        EnsureUnique(options.Select(option => option.Code), $"Option codes for {key} must be unique.");
        if (type is "select" or "multiselect" or "coded")
        {
            if (options.Length is < 1 or > 100)
            {
                throw new ArgumentException(
                    $"Field {key} must contain 1 to 100 bounded options.");
            }
        }
        else if (options.Length > 0)
        {
            throw new ArgumentException($"Field {key} cannot declare options for type {type}.");
        }

        if (type == "coded" && codeSystem is null)
        {
            throw new ArgumentException($"Coded field {key} requires a code system.");
        }

        if (type != "coded" && type is not ("select" or "multiselect") && codeSystem is not null)
        {
            throw new ArgumentException($"Field {key} cannot declare a code system.");
        }

        var children = (field.Children ?? [])
            .Select(child => NormalizeField(child, sectionKeys, nested: true))
            .OrderBy(child => child.Sequence)
            .ThenBy(child => child.Key, StringComparer.Ordinal)
            .ToArray();
        int? repeatMinimum = field.RepeatMinimum;
        int? repeatMaximum = field.RepeatMaximum;
        if (type == "repeat")
        {
            repeatMinimum ??= 0;
            repeatMaximum ??= 10;
            if (repeatMinimum is < 0 or > 20
                || repeatMaximum is < 1 or > 20
                || repeatMaximum < repeatMinimum)
            {
                throw new ArgumentException(
                    $"Repeating group {key} bounds must be between 0 and 20.");
            }

            if (children.Length is < 1 or > 20)
            {
                throw new ArgumentException(
                    $"Repeating group {key} must contain 1 to 20 child fields.");
            }

            EnsureUnique(
                children.Select(child => child.Sequence.ToString(CultureInfo.InvariantCulture)),
                $"Child sequences in repeating group {key} must be unique.");
        }
        else if (children.Length > 0 || repeatMinimum is not null || repeatMaximum is not null)
        {
            throw new ArgumentException(
                $"Only repeating groups may declare child fields or repeat bounds ({key}).");
        }

        if (type == "computed" && !field.ReadOnly)
        {
            throw new ArgumentException($"Computed field {key} must be read-only.");
        }

        if (type != "computed" && field.ReadOnly)
        {
            throw new ArgumentException($"Only computed fields may be read-only ({key}).");
        }

        return new ClinicalFormFieldDefinition(
            key,
            sectionKey,
            label,
            type!,
            ValidateSequence(field.Sequence, "Field"),
            field.Required,
            accessibilityLabel,
            helpText,
            maxLength,
            minimum,
            maximum,
            precision,
            unit,
            codeSystem,
            options,
            repeatMinimum,
            repeatMaximum,
            children,
            field.ReadOnly);
    }

    private static ClinicalFormRuleDefinition NormalizeRule(
        ClinicalFormRuleDefinition rule,
        IReadOnlyDictionary<string, ClinicalFormFieldDefinition> fields)
    {
        var key = NormalizeKey(rule.Key, "Rule key", dotted: false);
        ArgumentNullException.ThrowIfNull(rule.Condition);
        var conditionField = NormalizeKey(
            rule.Condition.FieldKey,
            $"Condition field for rule {key}",
            dotted: false);
        if (!fields.ContainsKey(conditionField))
        {
            throw new ArgumentException($"Rule {key} references an unknown condition field.");
        }

        var conditionOperator = rule.Condition.Operator?.Trim().ToLowerInvariant();
        if (!SupportedConditionOperators.Contains(conditionOperator, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Rule {key} uses an unsupported condition operator.");
        }

        if (conditionOperator is not ("is-empty" or "is-not-empty")
            && rule.Condition.Value is null)
        {
            throw new ArgumentException($"Rule {key} condition requires a bounded value.");
        }

        var action = rule.Action?.Trim().ToLowerInvariant();
        if (!SupportedRuleActions.Contains(action, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Rule {key} uses an unsupported action.");
        }

        var target = NormalizeKey(
            rule.TargetFieldKey,
            $"Target field for rule {key}",
            dotted: false);
        if (!fields.TryGetValue(target, out var targetField))
        {
            throw new ArgumentException($"Rule {key} references an unknown target field.");
        }

        var message = NormalizeOptionalText(rule.Message, $"Message for rule {key}", 500);
        if (action == "warning" && message is null)
        {
            throw new ArgumentException($"Warning rule {key} requires a message.");
        }

        ClinicalFormCalculation? calculation = null;
        if (action == "calculate")
        {
            if (rule.Calculation is null)
            {
                throw new ArgumentException($"Calculation rule {key} requires a calculation.");
            }

            if (targetField.Type != "computed")
            {
                throw new ArgumentException(
                    $"Calculation rule {key} must target a computed field.");
            }

            var calculationOperator =
                rule.Calculation.Operator?.Trim().ToLowerInvariant();
            if (!SupportedCalculationOperators.Contains(calculationOperator!))
            {
                throw new ArgumentException(
                    $"Calculation rule {key} uses an unsupported operator.");
            }

            var operands = rule.Calculation.Operands ?? [];
            if (operands.Count != 2)
            {
                throw new ArgumentException(
                    $"Calculation rule {key} requires exactly two operands.");
            }

            var normalizedOperands = operands
                .Select(operand =>
                {
                    var fieldKey = string.IsNullOrWhiteSpace(operand.FieldKey)
                        ? null
                        : NormalizeKey(
                            operand.FieldKey,
                            $"Calculation operand for rule {key}",
                            dotted: false);
                    if ((fieldKey is null) == (operand.Constant is null))
                    {
                        throw new ArgumentException(
                            $"Each calculation operand in rule {key} must contain one field key or one constant.");
                    }

                    if (fieldKey is not null
                        && (!fields.TryGetValue(fieldKey, out var operandField)
                            || operandField.Type is not (
                                "integer" or "decimal" or "measurement" or "computed")))
                    {
                        throw new ArgumentException(
                            $"Calculation rule {key} references a non-numeric field.");
                    }

                    return new ClinicalFormCalculationOperand(fieldKey, operand.Constant);
                })
                .ToArray();

            if (rule.Calculation.Precision is < 0 or > 8)
            {
                throw new ArgumentException(
                    $"Calculation precision for rule {key} must be 0 to 8.");
            }

            calculation = new(
                calculationOperator!,
                normalizedOperands,
                rule.Calculation.Precision);
        }
        else if (rule.Calculation is not null)
        {
            throw new ArgumentException($"Only calculate rules may contain calculations ({key}).");
        }

        return new ClinicalFormRuleDefinition(
            key,
            new(
                conditionField,
                conditionOperator!,
                rule.Condition.Value?.Clone()),
            action!,
            target,
            message,
            calculation);
    }

    private static IEnumerable<ClinicalFormFieldDefinition> FlattenFields(
        IEnumerable<ClinicalFormFieldDefinition> fields)
    {
        foreach (var field in fields)
        {
            yield return field;
            foreach (var child in FlattenFields(field.Children ?? []))
            {
                yield return child;
            }
        }
    }

    private static void EnsureAcyclicRules(
        IReadOnlyList<ClinicalFormRuleDefinition> rules)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            AddEdge(rule.Condition.FieldKey, rule.TargetFieldKey);
            if (rule.Calculation is not null)
            {
                foreach (var operand in rule.Calculation.Operands
                             .Where(operand => operand.FieldKey is not null))
                {
                    AddEdge(operand.FieldKey!, rule.TargetFieldKey);
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Keys)
        {
            Visit(node);
        }

        void AddEdge(string source, string target)
        {
            if (!graph.TryGetValue(source, out var targets))
            {
                targets = new(StringComparer.Ordinal);
                graph[source] = targets;
            }

            targets.Add(target);
        }

        void Visit(string node)
        {
            if (visited.Contains(node))
            {
                return;
            }

            if (!visiting.Add(node))
            {
                throw new ArgumentException(
                    "Form rules contain a cyclic dependency.");
            }

            if (graph.TryGetValue(node, out var targets))
            {
                foreach (var target in targets)
                {
                    Visit(target);
                }
            }

            visiting.Remove(node);
            visited.Add(node);
        }
    }

    private static bool ConditionMatches(
        ClinicalFormRuleCondition condition,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var found = values.TryGetValue(condition.FieldKey, out var actual);
        return condition.Operator switch
        {
            "is-empty" => !found || IsEmpty(actual),
            "is-not-empty" => found && !IsEmpty(actual),
            "equals" => found && JsonEquals(actual, condition.Value),
            "not-equals" => !found || !JsonEquals(actual, condition.Value),
            "greater-than" => CompareNumbers(actual, condition.Value) is > 0,
            "greater-than-or-equal" => CompareNumbers(actual, condition.Value) is >= 0,
            "less-than" => CompareNumbers(actual, condition.Value) is < 0,
            "less-than-or-equal" => CompareNumbers(actual, condition.Value) is <= 0,
            _ => false
        };
    }

    private static decimal? Calculate(
        ClinicalFormCalculation calculation,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var operands = calculation.Operands
            .Select(operand =>
            {
                if (operand.Constant is not null)
                {
                    return operand.Constant;
                }

                return operand.FieldKey is not null
                    && values.TryGetValue(operand.FieldKey, out var value)
                    && TryGetDecimal(value, out var number)
                        ? number
                        : null;
            })
            .ToArray();
        if (operands.Any(operand => operand is null))
        {
            return null;
        }

        var left = operands[0]!.Value;
        var right = operands[1]!.Value;
        var value = calculation.Operator switch
        {
            "add" => left + right,
            "subtract" => left - right,
            "multiply" => left * right,
            "divide" when right != 0 => left / right,
            _ => (decimal?)null
        };
        return value is null
            ? null
            : decimal.Round(
                value.Value,
                calculation.Precision ?? 2,
                MidpointRounding.AwayFromZero);
    }

    private static void ValidateValue(
        ClinicalFormFieldDefinition field,
        JsonElement value,
        ICollection<ClinicalFormValidationIssue> issues)
    {
        string? error = field.Type switch
        {
            "text" or "multiline" => ValidateString(field, value),
            "integer" => ValidateInteger(field, value),
            "decimal" or "computed" => ValidateDecimal(field, value),
            "date" => ValidateDate(value, dateTime: false),
            "datetime" => ValidateDate(value, dateTime: true),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? null
                : "must be true or false",
            "select" or "coded" => ValidateSingleOption(field, value),
            "multiselect" => ValidateMultipleOptions(field, value),
            "measurement" => ValidateMeasurement(field, value),
            "repeat" => ValidateRepeat(field, value, issues),
            _ => "uses an unsupported value type"
        };

        if (error is not null)
        {
            issues.Add(new(field.Key, "error", $"{field.Label} {error}.", null));
        }
    }

    private static string? ValidateString(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "must be text";
        }

        var text = value.GetString() ?? "";
        return text.Length <= (field.MaxLength ?? int.MaxValue)
            ? null
            : $"must be {field.MaxLength} characters or fewer";
    }

    private static string? ValidateInteger(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
        {
            return "must be a whole number";
        }

        return ValidateRange(field, number);
    }

    private static string? ValidateDecimal(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (!TryGetDecimal(value, out var number))
        {
            return "must be a number";
        }

        var scale = GetDecimalScale(number);
        if (field.Precision is not null && scale > field.Precision)
        {
            return $"must contain no more than {field.Precision} decimal places";
        }

        return ValidateRange(field, number);
    }

    private static string? ValidateRange(
        ClinicalFormFieldDefinition field,
        decimal number)
    {
        if (field.Minimum is not null && number < field.Minimum)
        {
            return $"must be at least {field.Minimum}";
        }

        return field.Maximum is not null && number > field.Maximum
            ? $"must be no more than {field.Maximum}"
            : null;
    }

    private static string? ValidateDate(JsonElement value, bool dateTime)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return dateTime ? "must be an ISO date-time" : "must be an ISO date";
        }

        var text = value.GetString();
        if (dateTime)
        {
            return DateTimeOffset.TryParseExact(
                text,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _)
                ? null
                : "must be an ISO date-time";
        }

        return DateOnly.TryParseExact(
            text,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
            ? null
            : "must be an ISO date";
    }

    private static string? ValidateSingleOption(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "must be one bounded option code";
        }

        var code = value.GetString();
        return field.Options.Any(option => option.Code == code)
            ? null
            : "contains an unknown option code";
    }

    private static string? ValidateMultipleOptions(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return "must be an array of bounded option codes";
        }

        var allowed = field.Options
            .Select(option => option.Code)
            .ToHashSet(StringComparer.Ordinal);
        var selected = value.EnumerateArray().ToArray();
        if (selected.Any(item =>
                item.ValueKind != JsonValueKind.String
                || !allowed.Contains(item.GetString() ?? "")))
        {
            return "contains an unknown option code";
        }

        return selected
            .Select(item => item.GetString())
            .Distinct(StringComparer.Ordinal)
            .Count() == selected.Length
            ? null
            : "contains duplicate option codes";
    }

    private static string? ValidateMeasurement(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("value", out var numberElement)
            || !TryGetDecimal(numberElement, out var number)
            || !value.TryGetProperty("unit", out var unitElement)
            || unitElement.ValueKind != JsonValueKind.String)
        {
            return "must contain a numeric value and its configured unit";
        }

        if (!string.Equals(
                unitElement.GetString(),
                field.Unit,
                StringComparison.Ordinal))
        {
            return $"must use unit {field.Unit}";
        }

        return ValidateRange(field, number);
    }

    private static string? ValidateRepeat(
        ClinicalFormFieldDefinition field,
        JsonElement value,
        ICollection<ClinicalFormValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return "must be a bounded array";
        }

        var rows = value.EnumerateArray().ToArray();
        if (rows.Length < (field.RepeatMinimum ?? 0)
            || rows.Length > (field.RepeatMaximum ?? 0))
        {
            return $"must contain {field.RepeatMinimum} to {field.RepeatMaximum} rows";
        }

        var childMap = field.Children.ToDictionary(child => child.Key, StringComparer.Ordinal);
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.ValueKind != JsonValueKind.Object)
            {
                return "must contain object rows";
            }

            var properties = row.EnumerateObject().ToArray();
            if (properties.Any(property => !childMap.ContainsKey(property.Name)))
            {
                return "contains an unknown child field";
            }

            foreach (var child in field.Children)
            {
                var found = row.TryGetProperty(child.Key, out var childValue);
                if (child.Required && (!found || IsEmpty(childValue)))
                {
                    issues.Add(new(
                        field.Key,
                        "error",
                        $"{field.Label} row {rowIndex + 1}: {child.Label} is required.",
                        null));
                    continue;
                }

                if (found && !IsEmpty(childValue))
                {
                    var childIssues = new List<ClinicalFormValidationIssue>();
                    ValidateValue(child, childValue, childIssues);
                    foreach (var childIssue in childIssues)
                    {
                        issues.Add(childIssue with
                        {
                            FieldKey = field.Key,
                            Message = $"{field.Label} row {rowIndex + 1}: {childIssue.Message}"
                        });
                    }
                }
            }
        }

        return null;
    }

    private static bool JsonEquals(JsonElement actual, JsonElement? expected)
    {
        if (expected is null)
        {
            return actual.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        if (TryGetDecimal(actual, out var actualNumber)
            && TryGetDecimal(expected.Value, out var expectedNumber))
        {
            return actualNumber == expectedNumber;
        }

        return actual.GetRawText() == expected.Value.GetRawText();
    }

    private static int? CompareNumbers(JsonElement actual, JsonElement? expected)
    {
        if (expected is null
            || !TryGetDecimal(actual, out var actualNumber)
            || !TryGetDecimal(expected.Value, out var expectedNumber))
        {
            return null;
        }

        return actualNumber.CompareTo(expectedNumber);
    }

    private static bool TryGetDecimal(JsonElement value, out decimal number)
    {
        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out number))
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("value", out var nested)
            && nested.ValueKind == JsonValueKind.Number
            && nested.TryGetDecimal(out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || value.ValueKind == JsonValueKind.String
        && string.IsNullOrWhiteSpace(value.GetString())
        || value.ValueKind == JsonValueKind.Array
        && value.GetArrayLength() == 0;

    private static int GetDecimalScale(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }

    private static int ValidateSequence(int sequence, string kind)
    {
        if (sequence is < 1 or > 10000)
        {
            throw new ArgumentException($"{kind} sequence must be 1 to 10000.");
        }

        return sequence;
    }

    private static string NormalizeCapability(string value)
    {
        var capability = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(capability)
            || capability.Length > 120
            || !CapabilityPattern().IsMatch(capability))
        {
            throw new ArgumentException(
                "Capability must use module.permission lowercase syntax.");
        }

        return capability;
    }

    private static string NormalizeKey(string value, string label, bool dotted)
    {
        var key = value?.Trim().ToLowerInvariant();
        var pattern = dotted ? StableKeyPattern() : KeyPattern();
        if (string.IsNullOrWhiteSpace(key)
            || key.Length > 80
            || !pattern.IsMatch(key))
        {
            throw new ArgumentException(
                $"{label} must start with a lowercase letter and contain only lowercase letters, numbers, underscores{(dotted ? ", and dots" : "")}.");
        }

        return key;
    }

    private static string NormalizeText(string value, string label, int maximum)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum)
        {
            throw new ArgumentException($"{label} is required and must be {maximum} characters or fewer.");
        }

        EnsureSafeText(text, label);
        return text;
    }

    private static string? NormalizeOptionalText(
        string? value,
        string label,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length > maximum)
        {
            throw new ArgumentException($"{label} must be {maximum} characters or fewer.");
        }

        EnsureSafeText(text, label);
        return text;
    }

    private static void EnsureSafeText(string text, string label)
    {
        var normalized = text.ToLowerInvariant();
        if (UnsafeTextFragments.Any(normalized.Contains))
        {
            throw new ArgumentException(
                $"{label} contains executable, markup, query, or template content.");
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string message)
    {
        if (values.GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(message);
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex(
        "^[a-z][a-z0-9_]*(\\.[a-z0-9_]+)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex StableKeyPattern();

    [GeneratedRegex(
        "^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityPattern();
}
