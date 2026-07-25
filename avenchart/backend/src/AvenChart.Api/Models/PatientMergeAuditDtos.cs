namespace AvenChart.Api.Models;

public sealed record PatientMergeAuditPlanRequest(
    string TargetPatientId,
    string SourcePatientId,
    string? Rationale);

public sealed record PatientMergeAuditPlanResponse(
    Guid AuditId,
    string PlannedAt,
    string PlannedBy,
    string Status,
    string? Rationale,
    PatientMergePreviewResponse Preview);

public sealed record PatientMergeExecutionRequest(Guid AuditId);

public sealed record PatientMergeRollbackRequest(Guid ExecutionId);

public sealed record PatientMergeExecutionTableCount(string TableName, int RecordCount);

public sealed record PatientMergeExecutionResponse(
    Guid ExecutionId,
    Guid AuditId,
    string Status,
    string ExecutedAt,
    string ExecutedBy,
    string TargetPatientId,
    string SourcePatientId,
    IReadOnlyList<PatientMergeExecutionTableCount> MovedRecords,
    IReadOnlyList<string> Safeguards);
