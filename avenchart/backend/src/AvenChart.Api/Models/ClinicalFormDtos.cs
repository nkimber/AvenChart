using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvenChart.Api.Models;

public sealed record ClinicalFormPolicyResponse(
    string Revision,
    string RendererVersion,
    string SignaturePolicyRevision,
    IReadOnlyList<string> SupportedFieldTypes,
    IReadOnlyList<string> SupportedRuleActions,
    IReadOnlyList<string> SupportedCalculationOperators,
    IReadOnlyList<string> SupportedConditionOperators,
    IReadOnlyList<string> DefinitionStates,
    IReadOnlyList<string> InstanceStates,
    IReadOnlyList<string> ForbiddenCapabilities,
    IReadOnlyList<string> ProductionBlockers,
    bool ArbitraryScriptsAllowed,
    bool RawHtmlAllowed,
    bool ExternalFetchAllowed,
    bool PreviewPersistsClinicalData,
    bool ProductionSignatureStandardApproved);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormSectionDefinition(
    string Key,
    string Title,
    int Sequence,
    string? Description);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormOptionDefinition(
    string Code,
    string Display);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormOptionListReference(
    string ListKey,
    long RevisionId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormFieldDefinition(
    string Key,
    string SectionKey,
    string Label,
    string Type,
    int Sequence,
    bool Required,
    string AccessibilityLabel,
    string? HelpText,
    int? MaxLength,
    decimal? Minimum,
    decimal? Maximum,
    int? Precision,
    string? Unit,
    string? CodeSystem,
    IReadOnlyList<ClinicalFormOptionDefinition> Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClinicalFormOptionListReference? OptionListReference,
    int? RepeatMinimum,
    int? RepeatMaximum,
    IReadOnlyList<ClinicalFormFieldDefinition> Children,
    bool ReadOnly);

public sealed record ClinicalFormOptionListCatalogItem(
    string ListKey,
    string Title,
    long RevisionId,
    string OccurredAt,
    bool Eligible,
    string? Blocker,
    IReadOnlyList<ClinicalFormOptionDefinition> Options);

public sealed record ClinicalFormOptionListCatalogResponse(
    IReadOnlyList<ClinicalFormOptionListCatalogItem> OptionLists);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormRuleCondition(
    string FieldKey,
    string Operator,
    JsonElement? Value);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormCalculationOperand(
    string? FieldKey,
    decimal? Constant);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormCalculation(
    string Operator,
    IReadOnlyList<ClinicalFormCalculationOperand> Operands,
    int? Precision);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormRuleDefinition(
    string Key,
    ClinicalFormRuleCondition Condition,
    string Action,
    string TargetFieldKey,
    string? Message,
    ClinicalFormCalculation? Calculation);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClinicalFormSchemaDefinition(
    string StableKey,
    string Name,
    string Purpose,
    string ContextScope,
    string OwningService,
    string Capability,
    string SignaturePolicy,
    IReadOnlyList<ClinicalFormSectionDefinition> Sections,
    IReadOnlyList<ClinicalFormFieldDefinition> Fields,
    IReadOnlyList<ClinicalFormRuleDefinition> Rules);

public sealed record ClinicalFormDefinitionCreateRequest(
    ClinicalFormSchemaDefinition Definition,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormRevisionCreateRequest(
    ClinicalFormSchemaDefinition Definition,
    int ExpectedLatestRevision,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormDefinitionTransitionRequest(
    int Revision,
    int ExpectedVersion,
    string Reason,
    string? EffectiveFrom,
    string? EffectiveTo)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormDefinitionSummary(
    Guid DefinitionId,
    string StableKey,
    string Name,
    string Purpose,
    string ContextScope,
    int LatestRevision,
    int? EffectiveRevision,
    string LatestStatus,
    int LatestVersion,
    string SignaturePolicy,
    string UpdatedAt,
    string UpdatedBy);

public sealed record ClinicalFormDefinitionListResponse(
    IReadOnlyList<ClinicalFormDefinitionSummary> Definitions,
    int Total,
    int Page,
    int PageSize);

public sealed record ClinicalFormRevisionItem(
    Guid DefinitionId,
    int Revision,
    string Status,
    int Version,
    ClinicalFormSchemaDefinition Definition,
    string RendererVersion,
    string SchemaHash,
    string Author,
    string? ReviewedBy,
    string? ApprovedBy,
    string? EffectiveFrom,
    string? EffectiveTo,
    string CreatedAt,
    string UpdatedAt,
    string UpdatedBy,
    int? PredecessorRevision);

public sealed record ClinicalFormDefinitionEvent(
    long EventId,
    int Revision,
    string Action,
    string? FromStatus,
    string ToStatus,
    string Actor,
    string Reason,
    string OccurredAt,
    string SnapshotHash);

public sealed record ClinicalFormDefinitionDetailResponse(
    ClinicalFormDefinitionSummary Definition,
    ClinicalFormRevisionItem CurrentRevision,
    IReadOnlyList<ClinicalFormRevisionItem> Revisions,
    IReadOnlyList<ClinicalFormDefinitionEvent> Events);

public sealed record ClinicalFormValidationIssue(
    string FieldKey,
    string Severity,
    string Message,
    string? RuleKey);

public sealed record ClinicalFormRuleEvaluation(
    string RuleKey,
    bool Triggered,
    string Action,
    string TargetFieldKey,
    string Explanation);

public sealed record ClinicalFormEvaluationResponse(
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyDictionary<string, bool> VisibleFields,
    IReadOnlyDictionary<string, bool> RequiredFields,
    IReadOnlyList<ClinicalFormValidationIssue> Issues,
    IReadOnlyList<ClinicalFormRuleEvaluation> RuleEvaluations,
    bool Valid);

public sealed record ClinicalFormPreviewRequest(
    ClinicalFormSchemaDefinition Definition,
    IReadOnlyDictionary<string, JsonElement> Values)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormInstanceCreateRequest(
    Guid DefinitionId,
    int? Revision,
    int? EncounterId,
    string IdempotencyKey,
    IReadOnlyDictionary<string, JsonElement>? Values,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormInstanceUpdateRequest(
    int ExpectedVersion,
    IReadOnlyDictionary<string, JsonElement> Values,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormInstanceTransitionRequest(
    int ExpectedVersion,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormInstanceAmendRequest(
    int ExpectedVersion,
    string Reason,
    string IdempotencyKey)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ClinicalFormSignatureItem(
    Guid SignatureId,
    string Role,
    string Signer,
    string Method,
    string PolicyRevision,
    string CredentialContext,
    string SignedAt,
    string ContentHash);

public sealed record ClinicalFormInstanceEvent(
    long EventId,
    int Version,
    string Action,
    string? FromState,
    string ToState,
    string Actor,
    string Reason,
    string OccurredAt,
    string SnapshotHash);

public sealed record ClinicalFormInstanceSummary(
    Guid InstanceId,
    Guid DefinitionId,
    int DefinitionRevision,
    string StableKey,
    string Name,
    string PatientId,
    int? EncounterId,
    string State,
    int Version,
    string Author,
    string SignaturePolicy,
    Guid? PredecessorInstanceId,
    Guid? SuccessorInstanceId,
    string? AmendmentReason,
    string CreatedAt,
    string UpdatedAt,
    string? FinalizedAt,
    string? SignedAt);

public sealed record ClinicalFormInstanceDetailResponse(
    ClinicalFormInstanceSummary Instance,
    ClinicalFormSchemaDefinition Definition,
    IReadOnlyDictionary<string, JsonElement> Values,
    ClinicalFormEvaluationResponse Validation,
    IReadOnlyList<ClinicalFormSignatureItem> Signatures,
    IReadOnlyList<ClinicalFormInstanceEvent> Events);

public sealed record ClinicalFormInstanceListResponse(
    IReadOnlyList<ClinicalFormInstanceSummary> Instances,
    int Total);

public sealed record ClinicalFormRenderResponse(
    ClinicalFormInstanceSummary Instance,
    ClinicalFormSchemaDefinition Definition,
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyList<ClinicalFormSignatureItem> Signatures,
    string ContentHash,
    string RenderedAt,
    string RendererVersion);

public sealed record ClinicalFormFieldDictionaryItem(
    string FieldKey,
    string Path,
    string? ParentFieldKey,
    string SectionKey,
    string SectionTitle,
    string Label,
    string Type,
    bool Required,
    bool Repeating,
    string? CodeSystem,
    string? Unit,
    string ReportColumn);

public sealed record ClinicalFormFieldDictionaryResponse(
    Guid DefinitionId,
    string StableKey,
    int Revision,
    string SchemaHash,
    string RendererVersion,
    IReadOnlyList<ClinicalFormFieldDictionaryItem> Fields);

public sealed record ClinicalFormStructuredExportResponse(
    string ExportFormat,
    string ExportedAt,
    ClinicalFormInstanceSummary Instance,
    ClinicalFormSchemaDefinition Definition,
    string SchemaHash,
    string RendererVersion,
    string ContentHash,
    ClinicalFormFieldDictionaryResponse FieldDictionary,
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyList<ClinicalFormSignatureItem> Signatures);

public sealed record LegacyClinicalFormSnapshotSummary(
    Guid SnapshotId,
    string SourceSystem,
    string SourceBaselineVersion,
    string ExtractionRevision,
    string SourceTable,
    string SourceRowId,
    string SourceRevision,
    string StableKey,
    string Name,
    string PatientId,
    int EncounterId,
    bool SourceActive,
    string? SourceRecordedAt,
    string CapturedAt,
    string RawSha256,
    string AdapterRevision,
    int TargetDefinitionRevision,
    string TargetSchemaHash,
    int UnmappedCount,
    bool ReadOnly,
    bool Converted);

public sealed record LegacyClinicalFormSnapshotListResponse(
    IReadOnlyList<LegacyClinicalFormSnapshotSummary> Snapshots,
    int Total,
    int Returned,
    int Limit);

public sealed record LegacyClinicalFormDisplayField(
    string SourceField,
    string? TargetField,
    string Label,
    JsonElement SourceValue,
    string DisplayValue,
    string MappingState,
    string? MappingNote);

public sealed record LegacyClinicalFormUnmappedFact(
    string SourceField,
    JsonElement SourceValue,
    string Reason);

public sealed record LegacyClinicalFormSnapshotDetailResponse(
    LegacyClinicalFormSnapshotSummary Snapshot,
    string SourceSchema,
    Guid TargetDefinitionId,
    string TargetRendererRevision,
    IReadOnlyDictionary<string, JsonElement> RawValues,
    IReadOnlyList<LegacyClinicalFormDisplayField> Fields,
    IReadOnlyList<LegacyClinicalFormUnmappedFact> UnmappedFacts,
    bool MigrationApproved,
    Guid? GovernedInstanceId);

public sealed record LegacyClinicalFormMigrationManifest(
    Guid ManifestId,
    string StableKey,
    string SourceSystem,
    string SourceBaselineVersion,
    string ExtractionRevision,
    string SourceSchema,
    string SourceTable,
    int TargetDefinitionRevision,
    string TargetSchemaHash,
    string TargetRendererRevision,
    int ManifestRevision,
    int Version,
    string Status,
    JsonElement Contract,
    IReadOnlyList<string> Blockers,
    string ManifestSha256,
    bool ProductionApproved,
    bool ExecutionEnabled,
    string? ReviewedBy,
    string? ReviewedAt,
    string? ApprovedBy,
    string? ApprovedAt,
    string? DecisionReason,
    string CreatedAt,
    string UpdatedAt,
    string UpdatedBy);

public sealed record LegacyClinicalFormMigrationManifestEvent(
    long EventId,
    int Version,
    string Action,
    string? FromStatus,
    string ToStatus,
    string Actor,
    string Reason,
    string OccurredAt,
    string SnapshotSha256);

public sealed record LegacyClinicalFormMigrationManifestDecisionRequest(
    int ExpectedVersion,
    string Reason)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record LegacyClinicalFormMigrationManifestDecisionResponse(
    Guid ManifestId,
    int Version,
    string Status,
    bool ProductionApproved,
    bool ExecutionEnabled,
    LegacyClinicalFormMigrationManifestEvent Decision);

public sealed record LegacyClinicalFormMigrationRowDisposition(
    Guid SnapshotId,
    string SourceRowId,
    bool SourceActive,
    int UnmappedCount,
    string Disposition,
    IReadOnlyList<string> Reasons);

public sealed record LegacyClinicalFormMigrationReconciliation(
    int SourceRows,
    int ActiveRows,
    int InactiveRows,
    int FullyMappedRows,
    int RowsWithUnmappedFacts,
    int EligibleRows,
    int BlockedRows,
    int GovernedInstancesCreated,
    string SourceSnapshotDigest,
    IReadOnlyList<LegacyClinicalFormMigrationRowDisposition> Rows);

public sealed record LegacyClinicalFormMigrationManifestResponse(
    LegacyClinicalFormMigrationManifest Manifest,
    string PatientId,
    LegacyClinicalFormMigrationReconciliation Reconciliation,
    IReadOnlyList<LegacyClinicalFormMigrationManifestEvent> Events,
    IReadOnlyList<string> AllowedActions);

public sealed class LegacyClinicalFormMigrationManifestConflictException(
    string message,
    int currentVersion,
    string currentStatus) : Exception(message)
{
    public int CurrentVersion { get; } = currentVersion;
    public string CurrentStatus { get; } = currentStatus;
}

public sealed class ClinicalFormConflictException(
    string message,
    int? currentVersion = null,
    string? currentState = null) : Exception(message)
{
    public int? CurrentVersion { get; } = currentVersion;
    public string? CurrentState { get; } = currentState;
}
