// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvenChart.Api.Models;

public sealed record ReportMetricDefinition(
    string Key,
    string Label,
    string Definition,
    string Unit,
    string SourceField);

public sealed record ReportParameterDefinition(
    string Key,
    string Label,
    string Type,
    bool Required,
    int? MaxSpanDays);

public sealed record ReportSourceDatasetDefinition(
    string Key,
    string Description,
    IReadOnlyList<string> Fields);

public sealed record ReportOutputFieldDefinition(
    string Key,
    string Label,
    string Type,
    string Sensitivity);

public sealed record ReportValidationFixture(
    string DatasetId,
    string Scenario,
    IReadOnlyList<string> ExpectedColumns,
    int? ExpectedRowCount);

public sealed record GovernedReportFamily(
    string Key,
    string Name,
    string Purpose,
    IReadOnlyList<ReportMetricDefinition> MetricDictionary,
    IReadOnlyList<ReportParameterDefinition> ParameterSchema,
    IReadOnlyList<ReportSourceDatasetDefinition> SourceDatasets,
    IReadOnlyList<ReportOutputFieldDefinition> OutputSchema,
    ReportValidationFixture ValidationFixture);

public sealed record ReportDefinitionGovernancePolicy(
    string Revision,
    bool RawSqlAccepted,
    bool ExecutableTemplatesAccepted,
    bool ExternalDeliveryEnabled,
    bool RowPolicyExecutionEnforced,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Sensitivities,
    IReadOnlyList<string> RowPolicies,
    IReadOnlyList<string> AllowedRecipients,
    IReadOnlyList<string> DeliveryModes,
    int MinimumRetentionDays,
    int MaximumRetentionDays,
    IReadOnlyList<GovernedReportFamily> Families,
    IReadOnlyList<string> ProductionBlockers);

public sealed record GovernedReportDefinitionCreateRequest(
    string StableKey,
    string Title,
    string OwnerUsername,
    string Purpose,
    string ReportFamily,
    string Sensitivity,
    string RowPolicy,
    int RetentionDays,
    IReadOnlyList<string>? AllowedRecipients,
    IReadOnlyList<string>? DeliveryModes,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record GovernedReportRevisionCreateRequest(
    int ExpectedLatestRevisionNumber,
    string Title,
    string OwnerUsername,
    string Purpose,
    string ReportFamily,
    string Sensitivity,
    string RowPolicy,
    int RetentionDays,
    IReadOnlyList<string>? AllowedRecipients,
    IReadOnlyList<string>? DeliveryModes,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record GovernedReportTransitionRequest(
    int ExpectedVersion,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record GovernedReportDefinitionSummary(
    Guid DefinitionId,
    string StableKey,
    int GovernanceVersion,
    Guid LatestRevisionId,
    int LatestRevisionNumber,
    string Title,
    string OwnerUsername,
    string ReportFamily,
    string Sensitivity,
    string RowPolicy,
    int? RetentionDays,
    string Status,
    int Version,
    int? ActiveRevisionNumber,
    string UpdatedAt,
    string UpdatedBy,
    bool LegacyReviewRequired);

public sealed record GovernedReportDefinitionListResponse(
    IReadOnlyList<GovernedReportDefinitionSummary> Definitions,
    int Page,
    int PageSize,
    int Total);

public sealed record GovernedReportDefinitionRevision(
    Guid RevisionId,
    Guid DefinitionId,
    int RevisionNumber,
    string Title,
    string OwnerUsername,
    string Purpose,
    string ReportFamily,
    IReadOnlyList<ReportMetricDefinition> MetricDictionary,
    IReadOnlyList<ReportParameterDefinition> ParameterSchema,
    IReadOnlyList<ReportSourceDatasetDefinition> SourceDatasets,
    IReadOnlyList<ReportOutputFieldDefinition> OutputSchema,
    string Sensitivity,
    string RowPolicy,
    int? RetentionDays,
    IReadOnlyList<string> AllowedRecipients,
    IReadOnlyList<string> DeliveryModes,
    ReportValidationFixture ValidationFixture,
    string Status,
    int Version,
    Guid? PredecessorRevisionId,
    string CreatedAt,
    string CreatedBy,
    string UpdatedAt,
    string UpdatedBy,
    string? EffectiveFrom,
    string? EffectiveTo,
    bool LegacyReviewRequired);

public sealed record GovernedReportDefinitionEvent(
    Guid EventId,
    Guid DefinitionId,
    Guid RevisionId,
    int RevisionNumber,
    string Action,
    string? FromStatus,
    string ToStatus,
    string Reason,
    string ActorUsername,
    string OccurredAt,
    string SnapshotChecksum);

public sealed record GovernedReportDefinitionDetail(
    Guid DefinitionId,
    string StableKey,
    int GovernanceVersion,
    Guid LatestRevisionId,
    Guid? ActiveRevisionId,
    IReadOnlyList<GovernedReportDefinitionRevision> Revisions,
    IReadOnlyList<GovernedReportDefinitionEvent> Events);

public sealed class ReportDefinitionConflictException(
    string message,
    int? currentVersion = null,
    string? currentStatus = null)
    : Exception(message)
{
    public int? CurrentVersion { get; } = currentVersion;
    public string? CurrentStatus { get; } = currentStatus;
}
