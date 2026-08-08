// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record ManagedRecordPolicyResponse(
    string Revision,
    string LifecycleState,
    int MaxFileSizeBytes,
    IReadOnlyList<string> AcceptedMediaTypes,
    IReadOnlyList<string> RecordClasses,
    IReadOnlyList<string> SourceTypes,
    IReadOnlyList<string> SensitivityLevels,
    IReadOnlyList<string> States,
    ManagedRecordAdapterStatus StorageAdapter,
    ManagedRecordAdapterStatus ValidationAdapter,
    bool AntiMalwareVerified,
    string EnvironmentBoundary,
    IReadOnlyList<string> ProductionBlockers);

public sealed record ManagedRecordAdapterStatus(
    string AdapterId,
    string State,
    string Evidence);

public sealed record ManagedRecordCreateRequest(
    string PatientId,
    int CategoryId,
    string Title,
    string ServiceDate,
    int? Encounter,
    string RecordClass,
    string SourceType,
    string AuthorName,
    int? FacilityId,
    string Sensitivity,
    string LanguageTag,
    string FileName,
    string MediaType,
    string ContentBase64,
    string ExpectedChecksumSha256,
    string IdempotencyKey,
    string Reason);

public sealed record ManagedRecordClassificationUpdateRequest(
    int ExpectedVersion,
    string RecordClass,
    string SourceType,
    string AuthorName,
    int? FacilityId,
    string Sensitivity,
    string LanguageTag,
    string Reason);

public sealed record ManagedRecordActionRequest(
    int ExpectedVersion,
    string Reason);

public sealed record ManagedRecordListResponse(
    string Revision,
    string PatientId,
    int TotalCount,
    ManagedRecordCounts Counts,
    IReadOnlyList<ManagedRecordItem> Items);

public sealed record ManagedRecordCounts(
    int Captured,
    int Quarantined,
    int Scanning,
    int Failed,
    int Available,
    int Withheld);

public sealed record ManagedRecordItem(
    Guid IntakeId,
    int? DocumentId,
    string PatientId,
    int LegacyPid,
    int CategoryId,
    string CategoryName,
    string Title,
    string ServiceDate,
    int? Encounter,
    string RecordClass,
    string SourceType,
    string AuthorName,
    int? FacilityId,
    string? FacilityName,
    string Sensitivity,
    string LanguageTag,
    string FileName,
    string MediaType,
    int SizeBytes,
    int ContentVersion,
    string ContentChecksumSha256,
    string StorageAdapter,
    string StorageReference,
    string State,
    int WorkflowVersion,
    string AvailabilityStatus,
    string ValidationStatus,
    string ValidationAdapter,
    bool AntiMalwareVerified,
    string? FailureReason,
    string LastActor,
    string LastActionAt,
    string LastReason,
    bool IdempotentReplay,
    IReadOnlyList<string> AvailableActions);

public sealed record ManagedRecordMutationResponse(
    bool IdempotentReplay,
    ManagedRecordItem Intake);

public sealed record ManagedRecordEvent(
    Guid EventId,
    string Action,
    string? FromState,
    string ToState,
    string? FromRecordClass,
    string ToRecordClass,
    string? FromSensitivity,
    string ToSensitivity,
    string Reason,
    string Actor,
    string OccurredAt,
    int WorkflowVersion,
    string ValidationStatus,
    int ContentVersion,
    string ContentChecksumSha256,
    int? DocumentId);

public sealed record ManagedRecordHistoryResponse(
    string Revision,
    Guid IntakeId,
    string CurrentState,
    int CurrentVersion,
    int EventCount,
    IReadOnlyList<ManagedRecordEvent> Events);
