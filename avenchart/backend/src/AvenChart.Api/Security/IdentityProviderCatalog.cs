// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Configuration;

namespace AvenChart.Api.Security;

public static class IdentityProviderCatalog
{
    public const string Revision = "external-subject-mapping-v1";

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
            "local-adapter-active",
            $"{nameof(IPatientPortalIdentityAdapter)} -> {nameof(LocalPatientPortalIdentityAdapter)}",
            "issued, active, expired, disabled-account, and logout-ended local sessions",
            "session is constrained to its canonical patient record",
            "portal routes resolve through a provider-neutral adapter before their server-owned session binding",
            RoutedThroughAdapter: true,
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
            "Provider credentials, signing keys, and client secrets cannot be supplied through this registry or AvenChart UI.",
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
            "Approve provider tenant, assurance, organization, facility, team, and patient-scope mapping. Provider-scoped subject-to-local-principal mappings are governed in AvenChart; local roles and resource grants remain server-owned.",
            "Security, clinical, and product owners",
            "implemented-owner-policy-gated",
            BlocksProduction: true),
        new(
            "portal-and-service-adapters",
            "Approve the portal-provider configuration and move any service identities behind an approved adapter contract.",
            "Security and integration owners",
            "portal adapter implemented; service adapter not implemented",
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

    public static IdentityProviderReadinessResponse Build(IdentityProviderOptions options, bool testIdentityProviderAvailable)
    {
        var adapter = options.IsOidc
            ? new IdentityAdapterContractItem(
                OidcStaffIdentityAdapter.Id,
                "oidc-discovery-jwks",
                nameof(IStaffIdentityAdapter),
                "Authorization: Bearer JWT validated through OIDC discovery and JWKS",
                options.SubjectClaim,
                [options.SubjectClaim, "iss", "aud", "exp", "nbf"],
                ["valid", "expired", "invalid-signature", "invalid-issuer", "invalid-audience", "unknown-local-principal"],
                ProductionApproved: false,
                ValidatesIssuer: true,
                ValidatesAudience: true,
                ValidatesSignature: true,
                EnforcesMfa: false,
                EnforcesDevicePolicy: false,
                EnforcesFacilityScope: true)
            : options.IsTestOidc
                ? new IdentityAdapterContractItem(
                    TestOidcStaffIdentityAdapter.Id,
                    "development-test-oidc",
                    nameof(IStaffIdentityAdapter),
                    "development-only first-party RS256 test identity provider",
                    "sub",
                    ["sub", "iss", "aud", "exp", "nbf"],
                    ["valid", "expired", "invalid-signature", "invalid-issuer", "invalid-audience", "unknown-local-principal"],
                    ProductionApproved: false,
                    ValidatesIssuer: true,
                    ValidatesAudience: true,
                    ValidatesSignature: true,
                    EnforcesMfa: false,
                    EnforcesDevicePolicy: false,
                    EnforcesFacilityScope: true)
                : Adapter;
        var identityTypes = IdentityTypes.Select(identityType => identityType.IdentityType switch
        {
            "staff" => identityType with
            {
                State = options.IsOidc ? "external-oidc-configured" : options.IsTestOidc ? "development-test-oidc" : identityType.State,
                ResolutionPath = options.IsOidc ? $"{nameof(IStaffIdentityAdapter)} -> {OidcStaffIdentityAdapter.Id}" : options.IsTestOidc ? $"{nameof(IStaffIdentityAdapter)} -> {TestOidcStaffIdentityAdapter.Id}" : identityType.ResolutionPath,
                Evidence = options.IsTestOidc && !testIdentityProviderAvailable ? "Test OIDC is configured but its development-only issuer endpoints are unavailable outside Development." : identityType.Evidence,
            },
            "portal" => identityType with
            {
                State = options.IsOidc ? "external-oidc-configured" : options.IsTestOidc ? "development-test-oidc" : identityType.State,
                ResolutionPath = options.IsOidc ? $"{nameof(IPatientPortalIdentityAdapter)} -> {nameof(OidcPatientPortalIdentityAdapter)}" : options.IsTestOidc ? $"{nameof(IPatientPortalIdentityAdapter)} -> {nameof(TestOidcPatientPortalIdentityAdapter)}" : identityType.ResolutionPath,
                Evidence = options.IsOidc ? "Validated bearer subjects resolve only through active provider-to-patient mappings and token-bounded server sessions." : options.IsTestOidc && !testIdentityProviderAvailable ? "Test OIDC is configured but its development-only issuer endpoints are unavailable outside Development." : identityType.Evidence,
            },
            _ => identityType
        }).ToArray();
        return new IdentityProviderReadinessResponse(
            Revision,
            options.IsOidc ? "external-oidc-configured-owner-gated" : options.IsTestOidc ? "development-test-oidc" : "local-foundation-owner-gated",
            adapter.AdapterId,
            adapter.AdapterKind,
            options.IsOidc ? "Configured provider validates bearer tokens and resolves only administrator-governed provider-subject mappings; provider tenant, MFA, and production acceptance remain owner-gated." : "Local and first-party test identities are not approved production identity sources.",
            new IdentityProviderReadinessCounts(
                identityTypes.Length,
                identityTypes.Count(identityType => identityType.RoutedThroughAdapter),
                identityTypes.Count(identityType => identityType.ProductionApproved),
                adapter.ValidatesSignature ? 1 : 0,
                adapter.EnforcesFacilityScope ? 1 : 0,
                identityTypes.Count(identityType =>
                    identityType.IdentityType == "emergency"
                    && identityType.State == "enabled"),
                Gaps.Count(gap => gap.BlocksProduction)),
            adapter,
            identityTypes,
            BoundaryControls,
            Verification,
            Gaps);
    }
}
