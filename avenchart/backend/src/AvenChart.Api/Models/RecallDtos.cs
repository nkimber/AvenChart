namespace AvenChart.Api.Models;
public sealed record RecallItem(Guid Id,string PatientId,string PatientName,string RecallDate,string Reason,int? ProviderId,int? FacilityId,string Status,string CreatedAt);
public sealed record RecallRequest(string PatientId,DateOnly RecallDate,string Reason,int? ProviderId,int? FacilityId);
