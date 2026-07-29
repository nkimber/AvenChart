using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvenChart.Api.Models;

public sealed record GovernedReportExecutionPolicy(
    string Revision,
    string DefinitionRevision,
    string DatasetId,
    string DatasetVersion,
    string RequiredAsOfDate,
    IReadOnlyList<string> RunStates,
    IReadOnlyList<string> ExecutableRowPolicies,
    IReadOnlyList<string> DeliveryModes,
    int MaximumDateSpanDays,
    int MaximumRows,
    int PreviewRows,
    bool ExternalDeliveryEnabled,
    bool ArtifactStorageProductionApproved,
    IReadOnlyList<string> ProductionBlockers);

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
    string DefinitionSnapshotChecksum,
    string RequestedAt,
    string? StartedAt,
    string? FinishedAt,
    int? DurationMs,
    int RowCount,
    string? ResultChecksum,
    int ArtifactBytes,
    string? ArtifactContentType,
    string? ArtifactFileName,
    string? FailureCode,
    string? FailureMessage,
    bool DownloadAvailable,
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

public sealed record GovernedReportArtifact(
    string FileName,
    string ContentType,
    byte[] Content,
    string Checksum);

public sealed class ReportExecutionConflictException(
    string message,
    GovernedReportRunItem? existingRun = null)
    : Exception(message)
{
    public GovernedReportRunItem? ExistingRun { get; } = existingRun;
}
