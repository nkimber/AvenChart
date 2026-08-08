// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record IdentityProviderReadinessResponse(
    string Revision,
    string LifecycleState,
    string ActiveAdapterId,
    string ActiveAdapterKind,
    string EnvironmentBoundary,
    IdentityProviderReadinessCounts Counts,
    IdentityAdapterContractItem Adapter,
    IReadOnlyList<IdentityTypeReadinessItem> IdentityTypes,
    IReadOnlyList<IdentityBoundaryControlItem> BoundaryControls,
    IReadOnlyList<IdentityVerificationItem> Verification,
    IReadOnlyList<IdentityProviderGapItem> Gaps);

public sealed record IdentityProviderReadinessCounts(
    int IdentityTypes,
    int RoutedThroughAdapter,
    int ProductionApproved,
    int CryptographicallyValidated,
    int FacilityScoped,
    int EmergencyEnabled,
    int BlockingGaps);

public sealed record IdentityAdapterContractItem(
    string AdapterId,
    string AdapterKind,
    string Interface,
    string CredentialSource,
    string SubjectKey,
    IReadOnlyList<string> ResolvedClaims,
    IReadOnlyList<string> SessionStates,
    bool ProductionApproved,
    bool ValidatesIssuer,
    bool ValidatesAudience,
    bool ValidatesSignature,
    bool EnforcesMfa,
    bool EnforcesDevicePolicy,
    bool EnforcesFacilityScope);

public sealed record IdentityTypeReadinessItem(
    string IdentityType,
    string State,
    string ResolutionPath,
    string LifecycleCoverage,
    string CapabilityMapping,
    string Evidence,
    bool RoutedThroughAdapter,
    bool ProductionApproved);

public sealed record IdentityBoundaryControlItem(
    string ControlId,
    string State,
    string Contract,
    string Evidence);

public sealed record IdentityVerificationItem(
    string Scenario,
    string ExpectedResult,
    string EvidenceState);

public sealed record IdentityProviderGapItem(
    string GapId,
    string RequiredDecision,
    string OwnerRole,
    string CurrentState,
    bool BlocksProduction);
