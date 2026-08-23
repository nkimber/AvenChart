// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;

namespace AvenChart.Api.Security;

public static class AuthorizationPolicyCatalog
{
    public const string Revision = "local-acl-access-context-v2";

    private sealed record PermissionFamily(
        string Capability,
        string PermissionName,
        string Section,
        string Permission,
        string Owner,
        IReadOnlyList<string> Levels);

    private static readonly IReadOnlyList<PermissionFamily> Families =
    [
        new("Revenue cycle", "Billing", "acct", "bill", "Revenue-cycle owner", ["view", "write"]),
        new("Administration", "Access control", "admin", "acl", "Practice administrator", ["write"]),
        new("Communications", "Batch communication", "admin", "batchcom", "Patient engagement owner", ["view", "write"]),
        new("Administration", "Practice administration", "admin", "practice", "Practice administrator", ["view", "write"]),
        new("Administration", "Super administration", "admin", "super", "Practice administrator", ["view", "write"]),
        new("Clinical documentation", "Own encounters", "encounters", "auth", "Clinical product owner", ["view", "write"]),
        new("Clinical documentation", "Authorized encounters", "encounters", "auth_a", "Clinical product owner", ["view", "write"]),
        new("Clinical documentation", "Encounter notes", "encounters", "notes", "Clinical product owner", ["addonly", "view", "write"]),
        new("Specialty modules", "Group care", "groups", "gadd", "Clinical product owner", ["view", "write"]),
        new("Inventory", "Inventory adjustments", "inventory", "adjustments", "Practice operations owner", ["view", "write"]),
        new("Inventory", "Inventory destruction", "inventory", "destruction", "Practice operations owner", ["write"]),
        new("Inventory", "Inventory lots", "inventory", "lots", "Practice operations owner", ["view", "write"]),
        new("Inventory", "Inventory purchasing", "inventory", "purchases", "Practice operations owner", ["view", "write"]),
        new("Inventory", "Inventory reporting", "inventory", "reporting", "Practice operations owner", ["view"]),
        new("Inventory", "Inventory sales", "inventory", "sales", "Practice operations owner", ["write"]),
        new("Inventory", "Inventory transfers", "inventory", "transfers", "Practice operations owner", ["write"]),
        new("Scheduling", "Appointments", "patients", "appt", "Operations product owner", ["view", "write"]),
        new("Patient chart", "Demographics", "patients", "demo", "Clinical product owner", ["addonly", "view", "write"]),
        new("Records", "Document deletion", "patients", "docs_rm", "Health-information owner", ["write"]),
        new("Records", "Documents", "patients", "docs", "Health-information owner", ["addonly", "view", "write"]),
        new("Laboratory", "Laboratory results", "patients", "lab", "Laboratory owner", ["addonly", "view", "write"]),
        new("Clinical documentation", "Medical history", "patients", "med", "Clinical product owner", ["view", "write"]),
        new("Communications", "Patient notes", "patients", "notes", "Patient engagement owner", ["addonly", "view", "write"]),
        new("Reporting", "Patient reports", "patients", "pat_rep", "Reporting owner", ["view", "write"]),
        new("Laboratory", "Sign laboratory results", "patients", "sign", "Laboratory owner", ["write"]),
    ];

    private static readonly IReadOnlyList<AuthorizationPolicyRuleItem> Rules = Families
        .SelectMany(family => family.Levels.Select(level => CreateRule(family, level)))
        .OrderBy(rule => rule.Capability, StringComparer.Ordinal)
        .ThenBy(rule => rule.PermissionName, StringComparer.Ordinal)
        .ThenBy(rule => rule.MinimumLevel, StringComparer.Ordinal)
        .ToArray();

    private static readonly IReadOnlyDictionary<string, AuthorizationPolicyRuleItem> RulesByAcl =
        Rules.ToDictionary(
            rule => AclKey(rule.Section, rule.Permission, rule.MinimumLevel),
            StringComparer.Ordinal);

    public static AuthorizationPolicyRuleItem Require(
        string section,
        string permission,
        string minimumLevel)
    {
        if (RulesByAcl.TryGetValue(AclKey(section, permission, minimumLevel), out var rule))
        {
            return rule;
        }

        throw new InvalidOperationException(
            $"ACL requirement '{section}:{permission}:{minimumLevel}' is not registered in SEC-01 policy revision '{Revision}'.");
    }

    public static AuthorizationPolicyCatalogResponse Search(
        string? query,
        string? gap,
        int offset,
        int limit)
    {
        if (offset < 0)
        {
            throw new ArgumentException("Policy offset cannot be negative.");
        }

        if (limit is < 1 or > 100)
        {
            throw new ArgumentException("Policy limit must be between 1 and 100.");
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length > 100)
        {
            throw new ArgumentException("Policy query may not exceed 100 characters.");
        }

        var normalizedGap = string.IsNullOrWhiteSpace(gap)
            ? "all"
            : gap.Trim().ToLowerInvariant();
        if (normalizedGap is not ("all" or "production-approval" or "facility-scope" or "patient-scope" or "purpose" or "exceptional-access"))
        {
            throw new ArgumentException("Policy gap filter is not supported.");
        }

        IEnumerable<AuthorizationPolicyRuleItem> filtered = Rules;
        if (normalizedQuery.Length > 0)
        {
            filtered = filtered.Where(rule =>
                rule.PolicyId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || rule.Capability.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || rule.PermissionName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || rule.Owner.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || rule.Section.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || rule.Permission.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        filtered = normalizedGap switch
        {
            "production-approval" => filtered.Where(rule => rule.ApprovalState != "production-approved"),
            "facility-scope" => filtered.Where(rule => rule.FacilityScope != "resource-enforced"),
            "patient-scope" => filtered.Where(rule => rule.PatientScope != "resource-enforced"),
            "purpose" => filtered.Where(rule => rule.PurposeRequirement != "required"),
            "exceptional-access" => filtered.Where(rule => rule.ExceptionalAccess != "owner-decided"),
            _ => filtered,
        };

        var materialized = filtered.ToArray();
        var page = materialized.Skip(offset).Take(limit).ToArray();
        return new AuthorizationPolicyCatalogResponse(
            Revision,
            "locally-enforced ACL and declared access-context registry",
            "locally-enforced-owner-gated",
            page,
            materialized.Length,
            page.Length,
            offset,
            limit,
            normalizedQuery,
            normalizedGap,
            new AuthorizationPolicyCatalogCounts(
                Rules.Count,
                Rules.Count(rule => rule.PolicyState == "locally-enforced"),
                Rules.Count(rule => rule.ApprovalState == "production-approved"),
                Rules.Count(rule => rule.FacilityScope.Contains("enforced", StringComparison.Ordinal)),
                Rules.Count(rule => rule.PatientScope.Contains("enforced", StringComparison.Ordinal)),
                Rules.Count(rule => rule.PurposeRequirement == "required"),
                Rules.Count(rule => rule.ExceptionalAccess == "owner-decided")),
            [
                "Production policy approval and effective intervals are not selected.",
                "A principal's declared facility is validated and audited; resource-level organization and facility filtering is not yet enforced.",
                "Patient/team minimum-necessary resource filtering is not yet enforced.",
                "Purpose of use is required and principal-granted; endpoint-specific permissible-use rules still need a governed policy matrix.",
                "Emergency or exceptional access is not selected or implemented.",
                "Current allow/deny fixtures prove selected ACL and declared-context combinations, not every rule and resource-scope combination.",
            ]);
    }

    private static AuthorizationPolicyRuleItem CreateRule(
        PermissionFamily family,
        string level)
    {
        var policyId = $"acl.{family.Section}.{family.Permission}.{level}";
        return new AuthorizationPolicyRuleItem(
            policyId,
            family.Capability,
            family.PermissionName,
            family.Section,
            family.Permission,
            level,
            family.Owner,
            "locally-enforced",
            "owner-gated",
            "authenticated-staff",
            "single-local-organization",
            "context-enforced",
            "not-enforced",
            "required",
            "not-selected",
            "server-endpoint-filter+access-context",
            "access-context-fixtures-pending",
            [
                "production-approval",
                "effective-interval",
                "facility-scope",
                "patient-team-scope",
                "exceptional-access-decision",
            ]);
    }

    private static string AclKey(
        string section,
        string permission,
        string minimumLevel) =>
        $"{section.Trim()}:{permission.Trim()}:{minimumLevel.Trim()}";
}
