// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record AzureOperationsUnlockRequest(string Code);

public sealed record AzureOperationsUnlockResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    bool RequiresCodeChange);

public sealed record AzureOperationsChangeCodeRequest(
    string CurrentCode,
    string NewCode);

public sealed record AzureOperationsChangeCodeResponse(
    bool Changed,
    bool RequiresUnlock,
    DateTimeOffset ChangedAt);

public sealed record AzureDeploymentProfileDocument(
    string EnvironmentKind,
    string WorkloadMode,
    string TenantId,
    string SubscriptionId,
    string Location,
    string ResourceGroupName,
    string ResourceNamePrefix,
    string ContainerRegistryName,
    string KeyVaultName,
    string PostgresServerName,
    string ContainerAppsEnvironmentName,
    string ManagedIdentityName,
    string LogAnalyticsWorkspaceName,
    string ContainerAppName,
    string MigrationJobName,
    string DatabaseName,
    string DatabaseAdministratorLogin,
    string DatabasePasswordSecretName,
    int ExpectedNamedUsers,
    int ExpectedConcurrentUsers,
    decimal ApiCpu,
    decimal ApiMemoryGiB,
    decimal UiCpu,
    decimal UiMemoryGiB,
    int MinimumReplicas,
    int MaximumReplicas,
    int HttpConcurrency,
    string PostgresSkuName,
    string PostgresTier,
    int PostgresStorageGiB,
    int ConnectionPoolMaximum,
    int BackupRetentionDays,
    bool EnableGeoRedundantBackup,
    bool EnableHighAvailability,
    string VnetAddressPrefix,
    string InfrastructureSubnetPrefix,
    string DatabaseSubnetPrefix,
    string CustomDomain,
    string DnsZoneResourceId,
    IReadOnlyList<string> AllowedIpRanges,
    string ApiImage,
    string UiImage,
    string SourceRevision,
    int RateLimitPermitLimit,
    int LogRetentionDays,
    int MonthlyBudgetUsd,
    IReadOnlyList<string> AlertEmails,
    string Owner,
    string CostCenter,
    IReadOnlyDictionary<string, string> Tags,
    bool EnableDemoSeed,
    bool EnableDemoReset,
    bool AcknowledgedSyntheticOnly);

public sealed record AzureDeploymentProfileCreateRequest(
    string Name,
    AzureDeploymentProfileDocument Document);

public sealed record AzureDeploymentProfileUpdateRequest(
    string Name,
    int ExpectedVersion,
    AzureDeploymentProfileDocument Document);

public sealed record AzureDeploymentProfileSummary(
    Guid ProfileId,
    string Name,
    string EnvironmentKind,
    string Location,
    string ResourceGroupName,
    int Version,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    bool DeploymentReady,
    int ValidationIssueCount);

public sealed record AzureDeploymentProfileDetail(
    Guid ProfileId,
    string Name,
    AzureDeploymentProfileDocument Document,
    int Version,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    AzureDeploymentProfileAssessment Assessment);

public sealed record AzureDeploymentProfileAssessment(
    bool Valid,
    bool DeploymentReady,
    int MaximumPotentialDatabaseConnections,
    int DatabaseUserConnectionLimit,
    string CostPosture,
    IReadOnlyList<AzureDeploymentValidationIssue> Issues,
    IReadOnlyList<string> ProductionBlockers,
    IReadOnlyList<string> PlannedResources,
    string PricingCalculatorUrl);

public sealed record AzureDeploymentValidationIssue(
    string Field,
    string Code,
    string Severity,
    string Message);

public sealed record AzureDeploymentProfileRevision(
    long RevisionId,
    int Version,
    string Action,
    string ChangedBy,
    DateTimeOffset ChangedAt);

public sealed record AzureDeploymentProfileHistoryResponse(
    Guid ProfileId,
    IReadOnlyList<AzureDeploymentProfileRevision> Revisions);

public sealed record AzureOperationsCapabilityResponse(
    bool Enabled,
    bool PlanExecutionEnabled,
    bool DeploymentExecutionEnabled,
    bool AzureCliAvailable,
    string AzureCliVersion,
    bool Authenticated,
    string? SignedInIdentity,
    string? TenantId,
    string? SubscriptionId,
    string EnvironmentBoundary,
    IReadOnlyList<string> RequiredProviders,
    IReadOnlyList<string> ProductionBlockers);

public sealed record AzureAccessValidationCheck(
    string Check,
    string Status,
    string Message);

public sealed record AzureAccessValidationResponse(
    bool Valid,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AzureAccessValidationCheck> Checks);

public sealed record AzureDeploymentExecutionStartRequest(
    int ExpectedProfileVersion,
    string Confirmation);

public sealed record AzureDeploymentExecutionSummary(
    Guid ExecutionId,
    Guid ProfileId,
    int ProfileVersion,
    string Kind,
    string Status,
    string Phase,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Summary,
    string? Error,
    string? ApplicationUrl,
    string? AzureDeploymentName,
    bool CancellationRequested);

public sealed record AzureDeploymentExecutionEvent(
    long EventId,
    string Level,
    string Phase,
    string Message,
    DateTimeOffset OccurredAt);

public sealed record AzureDeploymentExecutionDetail(
    AzureDeploymentExecutionSummary Execution,
    IReadOnlyList<AzureDeploymentExecutionEvent> Events);

public sealed record AzureDeploymentExecutionListResponse(
    int Total,
    IReadOnlyList<AzureDeploymentExecutionSummary> Executions);

public sealed record AzureDeploymentHealthResponse(
    bool Deployed,
    string? ApplicationUrl,
    string? RevisionName,
    string? RevisionHealthState,
    string UiHealth,
    string ApiLiveness,
    string ApiReadiness,
    DateTimeOffset CheckedAt,
    IReadOnlyList<string> Messages);
