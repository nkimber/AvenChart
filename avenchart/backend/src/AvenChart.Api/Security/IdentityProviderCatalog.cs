// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;

namespace AvenChart.Api.Security;

public static class IdentityProviderCatalog
{
    public const string Revision = "local-identity-adapter-v1";

    private static readonly IdentityAdapterContractItem Adapter = new(
        LocalDevelopmentStaffIdentityAdapter.Id,
        "local-development-session",
        nameof(IStaffIdentityAdapter),
        "local PostgreSQL authentication tables; no external credential or secret",
        "username",
        ["username", "displayName", "role", "staffId", "session timestamps", "session source"],
        ["issued", "active", "expired", "revoked-by-logout"],
        ProductionApproved: false,
        ValidatesIssuer: false,
        ValidatesAudience: false,
        ValidatesSignature: false,
        EnforcesMfa: false,
        EnforcesDevicePolicy: false,
        EnforcesFacilityScope: false);

    private static readonly IReadOnlyList<IdentityTypeReadinessItem> IdentityTypes =
    [
        new(
            "staff",
            "local-adapter-active",
            $"{nameof(IStaffIdentityAdapter)} -> {LocalDevelopmentStaffIdentityAdapter.Id}",
            "issued, active, expired, and logout-revoked local sessions",
            "local username resolves through the SEC-01 ACL compatibility matrix",
            "all protected staff endpoint filters resolve the session through the adapter seam",
            RoutedThroughAdapter: true,
            ProductionApproved: false),
        new(
            "portal",
            "local-direct-session",
            "patient-portal repository and X-Legacy EHR-Patient-Portal-Session",
            "issued, active, expired, disabled-account, and logout-ended local sessions",
            "session is constrained to its canonical patient record",
            "portal routes remain coupled to the local repository and are not provider-adapter ready",
            RoutedThroughAdapter: false,
            ProductionApproved: false),
        new(
            "service",
            "not-configured",
            "none",
            "none",
            "none",
            "no service-account token or workload-identity contract is configured",
            RoutedThroughAdapter: false,
            ProductionApproved: false),
        new(
            "emergency",
            "disabled-owner-gated",
            "none",
            "none",
            "none",
            "no emergency identity or exceptional-access bypass exists",
            RoutedThroughAdapter: false,
            ProductionApproved: false),
    ];

    private static readonly IReadOnlyList<IdentityBoundaryControlItem> BoundaryControls =
    [
        new(
            "identity.adapter-resolution",
            "locally-enforced",
            "Protected staff endpoints resolve identity through IStaffIdentityAdapter before ACL evaluation.",
            "AccessPermissionFilter uses the registered adapter; a missing or inactive session receives 401."),
        new(
            "identity.revocation",
            "locally-enforced-staff",
            "Ended or expired local staff sessions cannot resolve as authenticated.",
            "AuthRepository session lookup checks ended_at and expires_at at request time."),
        new(
            "identity.correlation",
            "locally-enforced",
            "Every request receives a bounded server-owned correlation identifier.",
            "Correlation middleware validates or creates X-Correlation-ID before identity resolution."),
        new(
            "identity.secret-boundary",
            "locally-enforced",
            "Provider credentials, signing keys, and client secrets cannot be supplied through this registry or Modern UI.",
            "The registry is static/read-only and contains no credential values."),
        new(
            "identity.production-isolation",
            "owner-gated",
            "Local development identities must be disabled when an approved production adapter is selected.",
            "No approved provider adapter or production environment switch exists yet."),
    ];

    private static readonly IReadOnlyList<IdentityVerificationItem> Verification =
    [
        new("missing staff credential", "401 before protected endpoint execution", "covered-local"),
        new("malformed staff session identifier", "401 before protected endpoint execution", "covered-local"),
        new("expired staff session", "401 before protected endpoint execution", "covered-local"),
        new("logout-revoked staff session", "401 before protected endpoint execution", "covered-local"),
        new("missing SEC-01 capability", "403 after authenticated identity resolution", "covered-selected-families"),
        new("invalid issuer, audience, or signature", "401 before protected endpoint execution", "blocked-no-external-provider"),
        new("facility-scope mismatch", "403 before protected endpoint execution", "blocked-no-approved-facility-policy"),
    ];

    private static readonly IReadOnlyList<IdentityProviderGapItem> Gaps =
    [
        new(
            "approved-provider",
            "Select the staff, portal, and service identity provider or providers and accountable tenant owner.",
            "Security and product owners",
            "not-selected",
            BlocksProduction: true),
        new(
            "token-validation",
            "Approve issuer, audience, signing-key rotation, clock-skew, revocation, and refresh rules.",
            "Security owner",
            "not-selected",
            BlocksProduction: true),
        new(
            "assurance-policy",
            "Approve MFA, recovery, device, session-duration, reauthentication, and disable behavior by identity type.",
            "Security and operations owners",
            "not-selected",
            BlocksProduction: true),
        new(
            "claim-and-capability-mapping",
            "Approve external-subject, role, capability, organization, facility, team, and patient-scope mapping.",
            "Security, clinical, and product owners",
            "local username-to-ACL compatibility only",
            BlocksProduction: true),
        new(
            "portal-and-service-adapters",
            "Move portal identities and any service identities behind approved adapter contracts.",
            "Security and integration owners",
            "not-implemented",
            BlocksProduction: true),
        new(
            "emergency-identity-decision",
            "Explicitly select or reject emergency identity and exceptional-access behavior.",
            "Security, privacy, and clinical owners",
            "disabled",
            BlocksProduction: true),
        new(
            "provider-tenant-proof",
            "Supply a non-production tenant and prove expiry, revocation, issuer/audience/signature denial, capability denial, and facility isolation.",
            "Security and operations owners",
            "not-available",
            BlocksProduction: true),
    ];

    public static IdentityProviderReadinessResponse Build()
    {
        return new IdentityProviderReadinessResponse(
            Revision,
            "local-foundation-owner-gated",
            Adapter.AdapterId,
            Adapter.AdapterKind,
            "Local development identities are not an approved production identity source.",
            new IdentityProviderReadinessCounts(
                IdentityTypes.Count,
                IdentityTypes.Count(identityType => identityType.RoutedThroughAdapter),
                IdentityTypes.Count(identityType => identityType.ProductionApproved),
                Adapter.ValidatesSignature ? 1 : 0,
                Adapter.EnforcesFacilityScope ? 1 : 0,
                IdentityTypes.Count(identityType =>
                    identityType.IdentityType == "emergency"
                    && identityType.State == "enabled"),
                Gaps.Count(gap => gap.BlocksProduction)),
            Adapter,
            IdentityTypes,
            BoundaryControls,
            Verification,
            Gaps);
    }
}
