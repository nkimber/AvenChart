// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

/// <summary>
/// EF-backed encounter summary, archival, and vitals mutations. Rich chart projections,
/// SOAP versioning, signatures, and other governed workflows remain in EncounterRepository.
/// </summary>
public sealed class EncounterStateRepository(
    AvenChartDbContext dbContext,
    EncounterRepository encounterRepository)
{
    public async Task<EncounterDetail?> UpdateSummaryAsync(
        int encounter,
        EncounterUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeText(request.Reason);
        if (reason is null)
        {
            return null;
        }

        await EnsureEncounterIsUnlockedAsync(encounter, cancellationToken);
        var entity = await dbContext.Encounters.SingleOrDefaultAsync(
            candidate => candidate.EncounterNumber == encounter,
            cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var sensitivity = NormalizeText(request.Sensitivity);
        var referralSource = NormalizeText(request.ReferralSource);
        var externalId = NormalizeText(request.ExternalId);
        var billingNote = NormalizeText(request.BillingNote);
        var changedFields = new List<string>();
        AddChangedField(changedFields, "reason", entity.Reason, reason);
        AddChangedField(changedFields, "sensitivity", entity.Sensitivity, sensitivity);
        AddChangedField(changedFields, "referralSource", entity.ReferralSource, referralSource);
        AddChangedField(changedFields, "externalId", entity.ExternalId, externalId);
        AddChangedField(changedFields, "posCode", entity.PosCode?.ToString(), request.PosCode?.ToString());
        AddChangedField(changedFields, "billingNote", entity.BillingNote, billingNote);
        if (changedFields.Count > 0)
        {
            entity.Reason = reason;
            entity.Sensitivity = sensitivity;
            entity.ReferralSource = referralSource;
            entity.ExternalId = externalId;
            entity.PosCode = request.PosCode;
            entity.BillingNote = billingNote;
            entity.RowVersion++;
            dbContext.EncounterAuditEvents.Add(CreateAuditEvent(
                encounter,
                username,
                "summary-updated",
                changedFields));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new EncounterStateConflictException(
                    "The encounter changed before the summary update could be saved.");
            }
        }

        return await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
    }

    public async Task<EncounterFormMutationResponse?> CreateVitalsAsync(
        int encounter,
        EncounterVitalsCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(request.DateTime, out var vitalDateTime))
        {
            return null;
        }

        await EnsureEncounterIsUnlockedAsync(encounter, cancellationToken);
        var encounterIdentity = await dbContext.Encounters
            .AsNoTracking()
            .Where(candidate => candidate.EncounterNumber == encounter)
            .Select(candidate => new
            {
                candidate.PatientId,
                candidate.LegacyPid,
                candidate.EncounterNumber
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (encounterIdentity is null)
        {
            return null;
        }

        var vital = new VitalEntity
        {
            PatientId = encounterIdentity.PatientId,
            LegacyPid = encounterIdentity.LegacyPid,
            EncounterNumber = encounterIdentity.EncounterNumber,
            VitalDateTime = DateTime.SpecifyKind(vitalDateTime, DateTimeKind.Unspecified),
            Systolic = request.Systolic,
            Diastolic = request.Diastolic,
            Weight = request.Weight,
            Height = request.Height,
            Temperature = request.Temperature,
            Pulse = request.Pulse,
            Respiration = request.Respiration,
            Bmi = ComputeBmi(request.Weight, request.Height),
            OxygenSaturation = request.OxygenSaturation,
            Note = NormalizeText(request.Note)
        };
        dbContext.Vitals.Add(vital);
        await dbContext.SaveChangesAsync(cancellationToken);
        var detail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return detail is null ? null : new EncounterFormMutationResponse(vital.Id, detail);
    }

    public async Task<bool> ArchiveAsync(
        int encounter,
        EncounterArchiveRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var reason = RequireArchiveReason(request.Reason);
        var entity = await dbContext.Encounters.SingleOrDefaultAsync(
            candidate => candidate.EncounterNumber == encounter,
            cancellationToken);
        if (entity is null || entity.ArchivedAt is not null ||
            entity.ArchiveVersion != request.ExpectedArchiveVersion)
        {
            return false;
        }

        entity.ArchivedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        entity.ArchiveVersion++;
        entity.RowVersion++;
        dbContext.EncounterAuditEvents.Add(CreateAuditEvent(
            encounter,
            username,
            "archived",
            [$"reason:{reason}"]));
        return await SaveArchiveChangeAsync(cancellationToken);
    }

    public async Task<bool> RestoreAsync(
        int encounter,
        EncounterArchiveRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var reason = RequireArchiveReason(request.Reason);
        var entity = await dbContext.Encounters.SingleOrDefaultAsync(
            candidate => candidate.EncounterNumber == encounter,
            cancellationToken);
        if (entity is null || entity.ArchivedAt is null ||
            entity.ArchiveVersion != request.ExpectedArchiveVersion)
        {
            return false;
        }

        entity.ArchivedAt = null;
        entity.ArchiveVersion++;
        entity.RowVersion++;
        dbContext.EncounterAuditEvents.Add(CreateAuditEvent(
            encounter,
            username,
            "restored",
            [$"reason:{reason}"]));
        return await SaveArchiveChangeAsync(cancellationToken);
    }

    private async Task EnsureEncounterIsUnlockedAsync(
        int encounter,
        CancellationToken cancellationToken)
    {
        if (await dbContext.EncounterSignatures.AsNoTracking().AnyAsync(
                signature => signature.EncounterNumber == encounter && signature.IsLock,
                cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }
    }

    private async Task<bool> SaveArchiveChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static EncounterAuditEventEntity CreateAuditEvent(
        int encounter,
        string username,
        string action,
        IReadOnlyList<string> changedFields) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EncounterNumber = encounter,
            OccurredAt = DateTimeOffset.UtcNow,
            Username = username,
            Action = action,
            ChangedFields = string.Join(',', changedFields)
        };

    private static string RequireArchiveReason(string? value)
    {
        var reason = NormalizeText(value);
        return reason is null || reason.Length > 500
            ? throw new ArgumentException(
                "An archive or restore reason of 1 to 500 characters is required.")
            : reason;
    }

    private static void AddChangedField(
        ICollection<string> fields,
        string name,
        string? prior,
        string? updated)
    {
        if (!string.Equals(prior, updated, StringComparison.Ordinal))
        {
            fields.Add(name);
        }
    }

    private static decimal? ComputeBmi(decimal? weight, decimal? height)
    {
        if (weight is null || height is null || height <= 0)
        {
            return null;
        }

        return Math.Round(weight.Value / (height.Value * height.Value) * 703m, 2);
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
