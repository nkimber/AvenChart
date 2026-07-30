namespace AvenChart.Api.Models;
public sealed record TrackAnythingItem(int Id, int? ParentId, string Name, string? Description, int Position, bool Active);
public sealed record TrackAnythingResponse(IReadOnlyList<TrackAnythingItem> Items);
public sealed record TrackAnythingRequest(int? ParentId, string Name, string? Description, int Position, bool? Active);
public sealed record TrackAnythingDefinition(int Id, string Name, string? Description, IReadOnlyList<TrackAnythingItem> Items);
public sealed record TrackAnythingEncounterRecord(Guid RecordId, int Encounter, int TrackTypeId, string TrackName, string CreatedAt, string CreatedBy);
public sealed record TrackAnythingEncounterCatalog(
    int Encounter,
    IReadOnlyList<TrackAnythingDefinition> AvailableTracks,
    IReadOnlyList<TrackAnythingEncounterRecord> Records,
    bool IsLocked);
public sealed record TrackAnythingEncounterRecordCreateRequest(int TrackTypeId);
public sealed record TrackAnythingReadingValue(int ItemTypeId, string ItemName, string Value);
public sealed record TrackAnythingReading(Guid ReadingId, string RecordedAt, string RecordedBy, string? UpdatedAt, string? UpdatedBy, IReadOnlyList<TrackAnythingReadingValue> Values);
public sealed record TrackAnythingEncounterRecordDetail(TrackAnythingEncounterRecord Record, IReadOnlyList<TrackAnythingItem> Items, IReadOnlyList<TrackAnythingReading> Readings);
public sealed record TrackAnythingReadingValueRequest(int ItemTypeId, string? Value);
public sealed record TrackAnythingReadingCreateRequest(DateTimeOffset? RecordedAt, IReadOnlyList<TrackAnythingReadingValueRequest> Values);
public sealed record TrackAnythingReadingUpdateRequest(DateTimeOffset RecordedAt, IReadOnlyList<TrackAnythingReadingValueRequest> Values);
public sealed record TrackAnythingPatientHistoryReading(Guid ReadingId, string RecordedAt, string RecordedBy, string? UpdatedAt, string? UpdatedBy, IReadOnlyList<TrackAnythingReadingValue> Values);
public sealed record TrackAnythingPatientHistoryEncounter(Guid RecordId, int Encounter, string EncounterDate, string TrackName, IReadOnlyList<TrackAnythingPatientHistoryReading> Readings);
public sealed record TrackAnythingPatientTrackHistory(int TrackTypeId, string TrackName, IReadOnlyList<TrackAnythingPatientHistoryEncounter> Encounters);
public sealed record TrackAnythingPatientHistoryResponse(string PatientId, IReadOnlyList<TrackAnythingPatientTrackHistory> Tracks);
