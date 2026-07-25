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
