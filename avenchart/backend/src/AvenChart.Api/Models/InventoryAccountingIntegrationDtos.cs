// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record InventoryAccountingIntegrationDecisionDefinition(
    string Mode,
    string FinanceOwner,
    string EffectiveDate,
    string? MappingReference,
    string? ReconciliationReference,
    string Rationale);

public sealed record InventoryAccountingIntegrationDecision(
    Guid DecisionId,
    InventoryAccountingIntegrationDecisionDefinition Definition,
    int Revision,
    string Status,
    string ActivatedAt,
    string ActivatedBy,
    string? SupersededAt,
    string? SupersededBy);

public sealed record InventoryAccountingIntegrationChangeRequest(
    Guid RequestId,
    InventoryAccountingIntegrationDecisionDefinition ProposedDefinition,
    Guid? BaselineDecisionId,
    int? BaselineRevision,
    string Reason,
    string Status,
    int Version,
    string CreatedAt,
    string CreatedBy,
    string UpdatedAt,
    string UpdatedBy);

public sealed record InventoryAccountingIntegrationChangeRequestEvent(
    long EventId,
    string Action,
    string? Note,
    string OccurredAt,
    string Username);

public sealed record InventoryAccountingIntegrationCatalogResponse(
    InventoryAccountingIntegrationDecision? ActiveDecision,
    IReadOnlyList<InventoryAccountingIntegrationChangeRequest> Requests);

public sealed record InventoryAccountingIntegrationChangeRequestDetailResponse(
    InventoryAccountingIntegrationChangeRequest Request,
    InventoryAccountingIntegrationDecision? ActiveDecision,
    IReadOnlyList<InventoryAccountingIntegrationChangeRequestEvent> Events);

public sealed record InventoryAccountingIntegrationChangeRequestCreateRequest(
    InventoryAccountingIntegrationDecisionDefinition ProposedDefinition,
    string Reason);

public sealed record InventoryAccountingIntegrationChangeRequestDecisionRequest(
    int? ExpectedVersion,
    string? Note);

public sealed class InventoryAccountingIntegrationConflictException(string message) : Exception(message);
