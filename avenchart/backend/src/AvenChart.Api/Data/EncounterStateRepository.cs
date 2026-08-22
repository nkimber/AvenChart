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
        if (request.ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ExpectedVersion),
                "An encounter version is required to update the summary.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var entity = await GetEncounterForUpdateAsync(encounter, cancellationToken);
        if (entity is null)
        {
            return null;
        }
        await EnsureEncounterIsUnlockedAsync(encounter, cancellationToken);
        if (entity.RowVersion != request.ExpectedVersion)
        {
            throw new EncounterStateConflictException(
                "The encounter changed after this summary was opened. Refresh it before saving.",
                request.ExpectedVersion,
                entity.RowVersion);
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
                    "The encounter changed before the summary update could be saved.",
                    request.ExpectedVersion);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
    }

    public async Task<EncounterFormMutationResponse?> CreateVitalsAsync(
        int encounter,
        EncounterVitalsCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(request.DateTime, out var vitalDateTime))
        {
            throw new ArgumentException("Vital date/time must be a valid timestamp.");
        }

        var validatedVitals = ValidateVitalMeasurements(request);
        var recordedBy = NormalizeText(username)
            ?? throw new ArgumentException("An authenticated staff identity is required to record vitals.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var encounterEntity = await GetEncounterForUpdateAsync(encounter, cancellationToken);
        if (encounterEntity is null)
        {
            return null;
        }
        await EnsureEncounterIsUnlockedAsync(encounter, cancellationToken);

        if (request.CorrectionOfVitalId is { } correctionOfVitalId)
        {
            var correctedVital = await dbContext.Vitals.SingleOrDefaultAsync(
                vital => vital.Id == correctionOfVitalId
                    && vital.EncounterNumber == encounter
                    && vital.LegacyPid == encounterEntity.LegacyPid,
                cancellationToken);
            if (correctedVital is null)
            {
                throw new ArgumentException("The vital selected for correction does not belong to this encounter.");
            }
        }

        var vital = new VitalEntity
        {
            PatientId = encounterEntity.PatientId,
            LegacyPid = encounterEntity.LegacyPid,
            EncounterNumber = encounterEntity.EncounterNumber,
            VitalDateTime = DateTime.SpecifyKind(vitalDateTime, DateTimeKind.Unspecified),
            RecordedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            RecordedBy = recordedBy,
            CorrectionOfVitalId = request.CorrectionOfVitalId,
            CorrectionReason = validatedVitals.CorrectionReason,
            Systolic = request.Systolic,
            Diastolic = request.Diastolic,
            Weight = request.Weight,
            Height = request.Height,
            Temperature = request.Temperature,
            Pulse = request.Pulse,
            Respiration = request.Respiration,
            Bmi = ComputeBmi(request.Weight, request.Height),
            OxygenSaturation = request.OxygenSaturation,
            Note = validatedVitals.Note
        };
        dbContext.Vitals.Add(vital);
        encounterEntity.RowVersion++;
        dbContext.EncounterAuditEvents.Add(CreateAuditEvent(
            encounter,
            recordedBy,
            request.CorrectionOfVitalId is null ? "vitals-recorded" : "vitals-corrected",
            request.CorrectionOfVitalId is null
                ? ["vital-observation"]
                : ["vital-observation", "correction"]));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private Task<EncounterEntity?> GetEncounterForUpdateAsync(
        int encounter,
        CancellationToken cancellationToken) =>
        dbContext.Encounters
            .FromSqlInterpolated($"select * from encounters where encounter = {encounter} for update")
            .SingleOrDefaultAsync(cancellationToken);

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

    private static ValidatedVitals CreateValidatedVitals(string? note, string? correctionReason) =>
        new(NormalizeText(note), NormalizeText(correctionReason));

    private static ValidatedVitals ValidateVitalMeasurements(EncounterVitalsCreateRequest request)
    {
        if (request.Systolic is null
            && request.Diastolic is null
            && request.Weight is null
            && request.Height is null
            && request.Temperature is null
            && request.Pulse is null
            && request.Respiration is null
            && request.OxygenSaturation is null)
        {
            throw new ArgumentException("At least one vital observation is required.");
        }

        ValidateRange("Systolic blood pressure", request.Systolic, 1, 400);
        ValidateRange("Diastolic blood pressure", request.Diastolic, 1, 300);
        if (request.Systolic is not null && request.Diastolic is not null && request.Diastolic >= request.Systolic)
        {
            throw new ArgumentException("Diastolic blood pressure must be lower than systolic blood pressure.");
        }

        ValidateRange("Weight", request.Weight, 0.1m, 2000m);
        ValidateRange("Height", request.Height, 0.1m, 120m);
        ValidateRange("Temperature", request.Temperature, 1m, 150m);
        ValidateRange("Pulse", request.Pulse, 1, 400);
        ValidateRange("Respiration", request.Respiration, 1, 200);
        ValidateRange("Oxygen saturation", request.OxygenSaturation, 0, 100);

        var validated = CreateValidatedVitals(request.Note, request.CorrectionReason);
        if (validated.Note?.Length > 2000)
        {
            throw new ArgumentException("Vital note must not exceed 2,000 characters.");
        }

        if (request.CorrectionOfVitalId is null && validated.CorrectionReason is not null)
        {
            throw new ArgumentException("A correction reason requires a vital selected for correction.");
        }

        if (request.CorrectionOfVitalId is not null
            && (validated.CorrectionReason is null || validated.CorrectionReason.Length is < 3 or > 500))
        {
            throw new ArgumentException("A 3–500 character reason is required when correcting a vital observation.");
        }

        return validated;
    }

    private static void ValidateRange(string name, int? value, int minimum, int maximum)
    {
        if (value is not null && (value < minimum || value > maximum))
        {
            throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateRange(string name, decimal? value, decimal minimum, decimal maximum)
    {
        if (value is not null && (value < minimum || value > maximum))
        {
            throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
        }
    }

    private sealed record ValidatedVitals(string? Note, string? CorrectionReason);
}
