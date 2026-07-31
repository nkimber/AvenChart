// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record InventoryReplenishmentPolicyDefinition(
    int ItemId,
    int FacilityId,
    decimal ReorderPoint,
    decimal TargetQuantity,
    int LeadTimeDays,
    decimal SafetyStock,
    Guid? PreferredVendorId,
    decimal PackSize,
    decimal ApprovalThreshold,
    string EffectiveDate,
    string ApprovalReference,
    string Rationale);

public sealed record InventoryReplenishmentPolicy(
    Guid PolicyId,
    InventoryReplenishmentPolicyDefinition Definition,
    int Revision,
    string Status,
    string ActivatedAt,
    string ActivatedBy,
    string? SupersededAt,
    string? SupersededBy);

public sealed record InventoryReplenishmentPolicyChangeRequest(
    Guid RequestId,
    InventoryReplenishmentPolicyDefinition ProposedDefinition,
    Guid? BaselinePolicyId,
    int? BaselineRevision,
    string Reason,
    string Status,
    int Version,
    string CreatedAt,
    string CreatedBy,
    string UpdatedAt,
    string UpdatedBy);

public sealed record InventoryReplenishmentPolicyChangeRequestEvent(
    long EventId,
    string Action,
    string? Note,
    string OccurredAt,
    string Username);

public sealed record InventoryReplenishmentPolicyCatalogResponse(
    IReadOnlyList<InventoryReplenishmentPolicy> ActivePolicies,
    IReadOnlyList<InventoryReplenishmentPolicyChangeRequest> Requests);

public sealed record InventoryReplenishmentPolicyChangeRequestDetailResponse(
    InventoryReplenishmentPolicyChangeRequest Request,
    InventoryReplenishmentPolicy? ActivePolicy,
    IReadOnlyList<InventoryReplenishmentPolicyChangeRequestEvent> Events);

public sealed record InventoryReplenishmentPolicyChangeRequestCreateRequest(
    InventoryReplenishmentPolicyDefinition ProposedDefinition,
    string Reason);

public sealed record InventoryReplenishmentPolicyChangeRequestDecisionRequest(
    int? ExpectedVersion,
    string? Note);

public sealed record InventoryReplenishmentRecommendation(
    Guid PolicyId,
    int PolicyRevision,
    int ItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    int FacilityId,
    string FacilityCode,
    string FacilityName,
    decimal OnHand,
    decimal ReorderPoint,
    decimal TargetQuantity,
    int LeadTimeDays,
    decimal SafetyStock,
    Guid? PreferredVendorId,
    string? PreferredVendorName,
    decimal PackSize,
    decimal ApprovalThreshold,
    decimal RecommendedQuantity,
    string EffectiveDate,
    string ApprovalReference,
    bool CanAutoOrder);

public sealed class InventoryReplenishmentPolicyConflictException(string message) : Exception(message);
