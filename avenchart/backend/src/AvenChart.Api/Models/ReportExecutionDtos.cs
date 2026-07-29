using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvenChart.Api.Models;

public sealed record GovernedReportExecutionPolicy(
    string Revision,
    string DefinitionRevision,
    string ScopeRevision,
    string QueueRevision,
    string DatasetId,
    string DatasetVersion,
    string RequiredAsOfDate,
    IReadOnlyList<string> RunStates,
    IReadOnlyList<string> ExecutableRowPolicies,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RowPolicyFamilySupport,
    IReadOnlyList<string> ScopeSources,
    GovernedReportActorScope CurrentActorScope,
    bool OperatorAccess,
    IReadOnlyList<string> DeliveryModes,
    int MaximumDateSpanDays,
    int MaximumRows,
    int PreviewRows,
    bool DurableQueueEnabled,
    int EnqueueDelayMilliseconds,
    int PollIntervalMilliseconds,
    int LeaseSeconds,
    int ExecutionTimeoutSeconds,
    int QueueExpirationMinutes,
    int MaximumAttempts,
    int RetryBaseDelaySeconds,
    bool DefinitionRetentionEnforcedLocally,
    IReadOnlyList<string> RetryableFailureCodes,
    bool ExternalDeliveryEnabled,
    bool ArtifactStorageProductionApproved,
    IReadOnlyList<string> ProductionBlockers);

public sealed record GovernedReportActorScope(
    string Username,
    bool ActiveStaffLinked,
    int? StaffId,
    int? FacilityId,
    string? FacilityCode,
    int AssignedPatientCount);

public sealed record GovernedReportRunRequest(
    string Purpose,
    string RecipientUsername,
    string DeliveryMode,
    string AsOfDate,
    IReadOnlyDictionary<string, string?>? Parameters,
    string IdempotencyKey)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record GovernedReportPreviewRequest(
    string Purpose,
    string RecipientUsername,
    string DeliveryMode,
    string AsOfDate,
    IReadOnlyDictionary<string, string?>? Parameters)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record GovernedReportPreviewResponse(
    Guid DefinitionId,
    Guid RevisionId,
    int RevisionNumber,
    string ReportFamily,
    string RowPolicy,
    string Purpose,
    string RecipientUsername,
    string AsOfDate,
    IReadOnlyDictionary<string, string?> NormalizedParameters,
    string DatasetId,
    string DatasetVersion,
    string ExecutionRevision,
    string ScopeRevision,
    string ScopeSnapshotChecksum,
    int? ScopeFacilityId,
    int? ScopeSubjectCount,
    int TotalRows,
    int PreviewRowLimit,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string ResultChecksum);

public sealed record GovernedReportRunItem(
    string RunId,
    Guid DefinitionId,
    Guid? RevisionId,
    int? RevisionNumber,
    string DefinitionStableKey,
    string DefinitionTitle,
    string ReportFamily,
    string Status,
    string RequestedBy,
    string RecipientUsername,
    string Purpose,
    string RowPolicy,
    string AsOfDate,
    IReadOnlyDictionary<string, string?> NormalizedParameters,
    string DatasetId,
    string DatasetVersion,
    string ExecutionRevision,
    string ScopeRevision,
    string QueueRevision,
    string ScopeSnapshotChecksum,
    int? ScopeFacilityId,
    int? ScopeSubjectCount,
    string DefinitionSnapshotChecksum,
    int LifecycleVersion,
    int AttemptCount,
    int MaxAttempts,
    int ManualRetryCount,
    string? NextAttemptAt,
    string? LastAttemptAt,
    string? LeaseExpiresAt,
    string? QueueExpiresAt,
    string? CancelRequestedAt,
    string? CancelRequestedBy,
    string? CancelReason,
    string RequestedAt,
    string? StartedAt,
    string? FinishedAt,
    int? DurationMs,
    int RowCount,
    string? ResultChecksum,
    int ArtifactBytes,
    string? ArtifactContentType,
    string? ArtifactFileName,
    string? ArtifactExpiresAt,
    string? ArtifactExpiredAt,
    string? FailureCode,
    string? FailureMessage,
    bool? FailureRetryable,
    bool DownloadAvailable,
    bool CanCancel,
    bool CanRetry,
    bool Replay);

public sealed record GovernedReportRunEvent(
    Guid EventId,
    string RunId,
    string Action,
    string? FromStatus,
    string ToStatus,
    string ActorUsername,
    string Reason,
    string OccurredAt,
    IReadOnlyDictionary<string, JsonElement> Details);

public sealed record GovernedReportRunDetail(
    GovernedReportRunItem Run,
    IReadOnlyList<GovernedReportRunEvent> Events);

public sealed record GovernedReportRunListResponse(
    IReadOnlyList<GovernedReportRunItem> Runs,
    int Page,
    int PageSize,
    int Total);

public sealed record GovernedReportOperationsSummary(
    int TotalRuns,
    IReadOnlyDictionary<string, int> StatusCounts,
    int QueuedReady,
    int QueuedDelayed,
    int RunningWithLease,
    int OverdueLeases,
    int PendingCancellations,
    int RetryableFailures,
    int PermanentFailures,
    int QueueExpired,
    int ArtifactExpired,
    int CompletedLast24Hours,
    int FailedLast24Hours,
    int? P95CompletedDurationMs,
    string? OldestQueuedAt);

public sealed record GovernedReportOperationsAlert(
    string Code,
    string Severity,
    int Count,
    string Message,
    string? OldestAt);

public sealed record GovernedReportOperationsResponse(
    string Revision,
    string GeneratedAt,
    string Health,
    int PollIntervalSeconds,
    bool ProductionApproved,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> AttentionConditions,
    GovernedReportOperationsSummary Summary,
    IReadOnlyList<GovernedReportOperationsAlert> Alerts,
    IReadOnlyList<GovernedReportRunItem> Runs,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<string> ProductionBlockers);

public sealed record GovernedReportArtifact(
    string FileName,
    string ContentType,
    byte[] Content,
    string Checksum);

public sealed record GovernedReportLifecycleRequest(
    int ExpectedLifecycleVersion,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed class ReportExecutionConflictException(
    string message,
    GovernedReportRunItem? existingRun = null)
    : Exception(message)
{
    public GovernedReportRunItem? ExistingRun { get; } = existingRun;
}
