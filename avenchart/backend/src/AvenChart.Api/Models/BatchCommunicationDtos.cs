// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record BatchCommunicationFilter(
    string ProcessType,
    string? Gender,
    bool RequireConsent,
    int? AgeFrom,
    int? AgeTo,
    DateOnly? AppointmentStart,
    DateOnly? AppointmentEnd,
    DateOnly? SeenSince,
    DateOnly? SeenBefore,
    string? SortBy);

public sealed record BatchCommunicationPreviewRequest(BatchCommunicationFilter Filter);

public sealed record BatchCommunicationCampaignCreateRequest(
    BatchCommunicationFilter Filter,
    string? EmailSender,
    string? EmailSubject,
    string? EmailBody);

public sealed record BatchCommunicationRecipient(
    string PatientId,
    string DisplayName,
    string? Email,
    string? PhoneHome,
    string? PhoneCell,
    string? PostalCode,
    string? NextAppointmentDate,
    string? LastAppointmentDate,
    string? LastVisitDate,
    string? RenderedSubject,
    string? RenderedBody);

public sealed record BatchCommunicationPreview(BatchCommunicationFilter Filter, IReadOnlyList<BatchCommunicationRecipient> Recipients);
public sealed record BatchCommunicationCampaign(Guid Id, BatchCommunicationFilter Filter, string ProcessType, string? EmailSender, string? EmailSubject, string? EmailBody, int RecipientCount, string CreatedAt);
public sealed record BatchCommunicationCampaignDetail(BatchCommunicationCampaign Campaign, IReadOnlyList<BatchCommunicationRecipient> Recipients);
