// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record EncounterClinicalAlertItem(
    string Key,
    string Title,
    string Severity,
    string Message,
    string Reason);

public sealed record EncounterClinicalAlertsResponse(
    int Encounter,
    IReadOnlyList<EncounterClinicalAlertItem> Alerts);

public sealed record EncounterClinicalAlertAcknowledgementItem(
    string RuleKey,
    string Title,
    string AcknowledgedAt,
    string AcknowledgedBy,
    string? ReopenedAt,
    string? ReopenedBy);

public sealed record EncounterClinicalAlertHistoryResponse(
    int Encounter,
    IReadOnlyList<EncounterClinicalAlertAcknowledgementItem> Acknowledgements);
