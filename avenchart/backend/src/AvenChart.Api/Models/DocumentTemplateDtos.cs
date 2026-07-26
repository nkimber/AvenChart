namespace AvenChart.Api.Models;
public sealed record DocumentTemplateItem(Guid Id,string Name,string Content,bool Active,string CreatedAt,string UpdatedAt);
public sealed record DocumentTemplateRequest(string Name,string Content,bool Active);
public sealed record DocumentTemplateRenderRequest(string PatientId);
public sealed record DocumentTemplateRenderResult(DocumentTemplateItem Template,string PatientId,string Content);
