// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record AuthorizationPolicyRuleItem(
    string PolicyId,
    string Capability,
    string PermissionName,
    string Section,
    string Permission,
    string MinimumLevel,
    string Owner,
    string PolicyState,
    string ApprovalState,
    string SubjectType,
    string OrganizationScope,
    string FacilityScope,
    string PatientScope,
    string PurposeRequirement,
    string ExceptionalAccess,
    string Enforcement,
    string VerificationState,
    IReadOnlyList<string> OpenGaps);

public sealed record AuthorizationPolicyCatalogCounts(
    int Total,
    int LocallyEnforced,
    int ProductionApproved,
    int FacilityScoped,
    int PatientScoped,
    int PurposeConditioned,
    int ExceptionalAccessDecided);

public sealed record AuthorizationPolicyCatalogResponse(
    string Revision,
    string Classification,
    string EffectiveState,
    IReadOnlyList<AuthorizationPolicyRuleItem> Rules,
    int Total,
    int Returned,
    int Offset,
    int Limit,
    string Query,
    string Gap,
    AuthorizationPolicyCatalogCounts Counts,
    IReadOnlyList<string> RegistryGaps);
