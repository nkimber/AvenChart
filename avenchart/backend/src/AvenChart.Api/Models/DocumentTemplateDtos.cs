namespace AvenChart.Api.Models;
public sealed record DocumentTemplateItem(Guid Id,string Name,string Content,bool Active,string CreatedAt,string UpdatedAt);
public sealed record DocumentTemplateRequest(string Name,string Content,bool Active);
public sealed record DocumentTemplateRenderRequest(string PatientId);
public sealed record DocumentTemplateRenderResult(DocumentTemplateItem Template,string PatientId,string Content);
public sealed record DocumentTemplateBinaryUploadRequest(string FileName,string Mimetype,string ContentBase64);
public sealed record DocumentTemplateBinaryVersion(Guid Id,Guid TemplateId,int Version,string FileName,string Mimetype,int SizeBytes,string Sha256,string CreatedAt);
public sealed record DocumentTemplateBinaryDownload(string FileName,string Mimetype,byte[] Content);
public sealed record DocumentTemplateAttachmentRequest(string PatientId,int CategoryId,int? Encounter,string? DocDate,Guid? BinaryVersionId);
