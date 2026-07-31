// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;
public sealed record PatientXmlImportRequest(string PatientId,string Xml);
public sealed record PatientXmlPreview(string PatientId,string FirstName,string LastName,string DateOfBirth,string? Sex,string? Street,string? City,string? State,string? PostalCode,string? PhoneHome,string? PhoneCell,string? Email,IReadOnlyList<string> ChangedFields);
public sealed record PatientXmlImportResult(Guid AuditId,string PatientId,string ImportedAt,IReadOnlyList<string> ChangedFields);
