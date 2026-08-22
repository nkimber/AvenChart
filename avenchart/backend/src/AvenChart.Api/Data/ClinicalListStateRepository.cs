// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

/// <summary>
/// EF-backed state changes for bounded clinical-list entities. Cross-list projections,
/// medication reconciliation, and prescription workflows remain in ClinicalListRepository.
/// </summary>
public sealed class ClinicalListStateRepository(
    AvenChartDbContext dbContext,
    ClinicalListRepository clinicalListRepository)
{
    public async Task<ClinicalListMutationResponse?> CreateAllergyAsync(
        ClinicalAllergyCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var allergyDate))
        {
            return null;
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var allergy = new AllergyEntity
        {
            Id = $"ALG-MODERN-{Guid.NewGuid():N}",
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            Type = "allergy",
            Title = request.Title.Trim(),
            Reaction = NormalizeText(request.Reaction),
            Severity = NormalizeText(request.Severity),
            AllergyDate = allergyDate,
            Comments = NormalizeText(request.Comments),
            Activity = 1,
            ListOptionId = NormalizeText(request.ListOptionId)
        };
        dbContext.Allergies.Add(allergy);
        if (!await SaveNewClinicalContentAsync(cancellationToken))
        {
            return null;
        }
        return await BuildMutationAsync(allergy.Id, patient.CanonicalId, cancellationToken);
    }

    public async Task<ClinicalListMutationResponse?> DeactivateAllergyAsync(
        string allergyId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(allergyId))
        {
            return null;
        }

        var allergy = await dbContext.Allergies.SingleOrDefaultAsync(
            candidate => candidate.Id == allergyId && candidate.Type == "allergy",
            cancellationToken);
        if (allergy is null)
        {
            return null;
        }

        allergy.Activity = 0;
        allergy.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        allergy.Comments = NormalizeText(request.Comments);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildMutationAsync(allergy.Id, allergy.PatientId, cancellationToken);
    }

    public async Task<ClinicalListMutationResponse?> CreateProblemAsync(
        ClinicalProblemCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var problemDate))
        {
            return null;
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var problem = new ProblemEntity
        {
            Id = $"PROB-MODERN-{Guid.NewGuid():N}",
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            Type = "medical_problem",
            Title = request.Title.Trim(),
            Diagnosis = NormalizeText(request.Diagnosis),
            ProblemDate = problemDate,
            Comments = NormalizeText(request.Comments),
            Activity = 1
        };
        dbContext.Problems.Add(problem);
        if (!await SaveNewClinicalContentAsync(cancellationToken))
        {
            return null;
        }
        return await BuildMutationAsync(problem.Id, patient.CanonicalId, cancellationToken);
    }

    public async Task<ClinicalListMutationResponse?> DeactivateProblemAsync(
        string problemId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(problemId))
        {
            return null;
        }

        var problem = await dbContext.Problems.SingleOrDefaultAsync(
            candidate => candidate.Id == problemId && candidate.Type == "medical_problem",
            cancellationToken);
        if (problem is null)
        {
            return null;
        }

        problem.Activity = 0;
        problem.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        problem.Comments = NormalizeText(request.Comments);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildMutationAsync(problem.Id, problem.PatientId, cancellationToken);
    }

    public async Task<ClinicalListMutationResponse?> CreateMedicationAsync(
        ClinicalMedicationCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var medicationDate)
            || !HasBoundedText(actor, 120))
        {
            return null;
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var medication = new MedicationEntity
        {
            Id = $"MED-MODERN-{Guid.NewGuid():N}",
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            Type = "medication",
            Title = request.Title.Trim(),
            Diagnosis = NormalizeText(request.Diagnosis),
            MedicationDate = medicationDate,
            ModifiedDate = medicationDate,
            Comments = NormalizeText(request.Comments),
            Activity = 1,
            LifecycleVersion = 1
        };
        dbContext.Medications.Add(medication);
        dbContext.MedicationLifecycleEvents.Add(CreateMedicationEvent(
            medication.Id,
            "created",
            null,
            1,
            actor.Trim(),
            null,
            0,
            1));
        if (!await SaveNewClinicalContentAsync(cancellationToken))
        {
            return null;
        }
        return await BuildMutationAsync(medication.Id, patient.CanonicalId, cancellationToken);
    }

    public Task<ClinicalMedicationLifecycleMutationResult> DeactivateMedicationAsync(
        string medicationId,
        ClinicalMedicationDeactivateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId)
            || request.ExpectedVersion <= 0
            || !HasBoundedText(request.Comments, 500)
            || !HasBoundedText(actor, 120))
        {
            return Task.FromResult(InvalidMedicationMutation());
        }

        return MutateMedicationAsync(
            medicationId,
            request.ExpectedVersion,
            requiredActivity: 1,
            medication =>
            {
                medication.Activity = 0;
                medication.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
                medication.Comments = NormalizeText(request.Comments);
            },
            CreateMedicationEvent(
                medicationId,
                "deactivated",
                1,
                0,
                actor.Trim(),
                request.Comments.Trim(),
                request.ExpectedVersion,
                request.ExpectedVersion + 1),
            cancellationToken);
    }

    public Task<ClinicalMedicationLifecycleMutationResult> RestoreMedicationAsync(
        string medicationId,
        ClinicalMedicationRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId)
            || request.ExpectedVersion <= 0
            || !HasBoundedText(request.Reason, 500)
            || !HasBoundedText(actor, 120))
        {
            return Task.FromResult(InvalidMedicationMutation());
        }

        return MutateMedicationAsync(
            medicationId,
            request.ExpectedVersion,
            requiredActivity: 0,
            medication =>
            {
                medication.Activity = 1;
                medication.EndDate = null;
            },
            CreateMedicationEvent(
                medicationId,
                "restored",
                0,
                1,
                actor.Trim(),
                request.Reason.Trim(),
                request.ExpectedVersion,
                request.ExpectedVersion + 1),
            cancellationToken);
    }

    public async Task<ClinicalMedicationLifecycleMutationResult> UpdateMedicationAsync(
        string medicationId,
        ClinicalMedicationUpdateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId)
            || !HasBoundedText(request.Title, 255)
            || !HasBoundedText(request.Reason, 500)
            || request.ExpectedVersion <= 0
            || !HasBoundedText(actor, 120)
            || !TryReadDate(request.Date, out var medicationDate))
        {
            return InvalidMedicationMutation();
        }

        return await MutateMedicationAsync(
            medicationId,
            request.ExpectedVersion,
            requiredActivity: 1,
            medication =>
            {
                medication.Title = request.Title.Trim();
                medication.Diagnosis = NormalizeText(request.Diagnosis);
                medication.MedicationDate = medicationDate;
                medication.ModifiedDate = medicationDate;
                medication.Comments = NormalizeText(request.Comments);
            },
            CreateMedicationEvent(
                medicationId,
                "edited",
                null,
                1,
                actor.Trim(),
                request.Reason.Trim(),
                request.ExpectedVersion,
                request.ExpectedVersion + 1),
            cancellationToken);
    }

    public async Task<ClinicalMedicationLifecycleHistoryResponse?> GetMedicationLifecycleHistoryAsync(
        string medicationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId))
        {
            return null;
        }

        var currentVersion = await dbContext.Medications
            .AsNoTracking()
            .Where(medication => medication.Id == medicationId && medication.Type == "medication")
            .Select(medication => (int?)medication.LifecycleVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentVersion is null)
        {
            return null;
        }

        var eventEntities = await dbContext.MedicationLifecycleEvents
            .AsNoTracking()
            .Where(@event => @event.MedicationId == medicationId)
            .OrderByDescending(@event => @event.OccurredAt)
            .ThenByDescending(@event => @event.Id)
            .ToListAsync(cancellationToken);
        var events = eventEntities
            .Select(@event => new ClinicalMedicationLifecycleEventItem(
                @event.Id,
                @event.Action,
                @event.PreviousActivity,
                @event.CurrentActivity,
                @event.Actor,
                @event.Reason,
                @event.ExpectedVersion,
                @event.ResultingVersion,
                @event.OccurredAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        return new ClinicalMedicationLifecycleHistoryResponse(
            medicationId,
            currentVersion.Value,
            events.Count,
            events);
    }

    public async Task<ClinicalListMutationResponse?> CreateImmunizationAsync(
        ClinicalImmunizationCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Vaccine)
            || !TryReadDateTime(request.AdministeredAt, out var administeredAt)
            || !TryReadOptionalDate(request.EducationDate, out var educationDate)
            || !TryReadOptionalDate(request.VisDate, out var visDate)
            || !TryReadOptionalDate(request.ExpirationDate, out var expirationDate))
        {
            return null;
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var immunization = new ImmunizationEntity
        {
            Key = $"IMM-MODERN-{Guid.NewGuid():N}",
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            Encounter = request.Encounter,
            ImmunizationId = request.ImmunizationId,
            CvxCode = NormalizeText(request.CvxCode),
            Vaccine = request.Vaccine.Trim(),
            AdministeredAt = DateTime.SpecifyKind(administeredAt, DateTimeKind.Unspecified),
            Manufacturer = NormalizeText(request.Manufacturer),
            LotNumber = NormalizeText(request.LotNumber),
            AdministeredById = request.AdministeredById ?? patient.ProviderId,
            AdministeredBy = NormalizeText(request.AdministeredBy),
            EducationDate = educationDate,
            VisDate = visDate,
            AmountAdministered = request.AmountAdministered,
            AmountAdministeredUnit = NormalizeText(request.AmountAdministeredUnit),
            ExpirationDate = expirationDate,
            Route = NormalizeText(request.Route),
            AdministrationSite = NormalizeText(request.AdministrationSite),
            CompletionStatus = NormalizeText(request.CompletionStatus),
            InformationSource = NormalizeText(request.InformationSource),
            Note = NormalizeText(request.Note),
            AddedErroneously = 0
        };
        dbContext.Immunizations.Add(immunization);
        if (!await SaveNewClinicalContentAsync(cancellationToken))
        {
            return null;
        }
        return await BuildMutationAsync(
            immunization.Id.ToString(CultureInfo.InvariantCulture),
            patient.CanonicalId,
            cancellationToken);
    }

    public async Task<ClinicalListMutationResponse?> MarkImmunizationEnteredInErrorAsync(
        int immunizationId,
        ClinicalImmunizationErrorRequest request,
        CancellationToken cancellationToken)
    {
        var immunization = await dbContext.Immunizations.SingleOrDefaultAsync(
            candidate => candidate.Id == immunizationId,
            cancellationToken);
        if (immunization is null)
        {
            return null;
        }

        immunization.AddedErroneously = 1;
        immunization.Note = NormalizeText(request.Note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildMutationAsync(
            immunization.Id.ToString(CultureInfo.InvariantCulture),
            immunization.PatientId,
            cancellationToken);
    }

    private async Task<ClinicalMedicationLifecycleMutationResult> MutateMedicationAsync(
        string medicationId,
        int expectedVersion,
        int requiredActivity,
        Action<MedicationEntity> mutate,
        MedicationLifecycleEventEntity lifecycleEvent,
        CancellationToken cancellationToken)
    {
        var medication = await dbContext.Medications.SingleOrDefaultAsync(
            candidate => candidate.Id == medicationId && candidate.Type == "medication",
            cancellationToken);
        if (medication is null)
        {
            return new ClinicalMedicationLifecycleMutationResult(
                ClinicalMedicationLifecycleMutationStatus.NotFound,
                null);
        }

        if (medication.Activity != requiredActivity || medication.LifecycleVersion != expectedVersion)
        {
            return new ClinicalMedicationLifecycleMutationResult(
                ClinicalMedicationLifecycleMutationStatus.Conflict,
                null);
        }

        mutate(medication);
        medication.LifecycleVersion++;
        dbContext.MedicationLifecycleEvents.Add(lifecycleEvent);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ClinicalMedicationLifecycleMutationResult(
                ClinicalMedicationLifecycleMutationStatus.Conflict,
                null);
        }

        var mutation = await BuildMutationAsync(medication.Id, medication.PatientId, cancellationToken);
        return mutation is null
            ? new ClinicalMedicationLifecycleMutationResult(ClinicalMedicationLifecycleMutationStatus.NotFound, null)
            : new ClinicalMedicationLifecycleMutationResult(ClinicalMedicationLifecycleMutationStatus.Updated, mutation);
    }

    private async Task<PatientEntity?> FindPatientAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var normalized = patientId.Trim();
        var isLegacyPid = int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var legacyPid);
        return await dbContext.Patients
            .AsNoTracking()
            .SingleOrDefaultAsync(
                patient => (EF.Functions.ILike(patient.CanonicalId, normalized)
                    || EF.Functions.ILike(patient.PublicId, normalized)
                    || (isLegacyPid && patient.LegacyPid == legacyPid))
                    && patient.MergedIntoPatientId == null
                    && patient.LifecycleStatus == "active"
                    && patient.DeceasedDate == null,
                cancellationToken);
    }

    private async Task<ClinicalListMutationResponse?> BuildMutationAsync(
        string id,
        string patientId,
        CancellationToken cancellationToken)
    {
        var lists = await clinicalListRepository.GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    private async Task<bool> SaveNewClinicalContentAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation
            })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    private static MedicationLifecycleEventEntity CreateMedicationEvent(
        string medicationId,
        string action,
        int? previousActivity,
        int currentActivity,
        string actor,
        string? reason,
        int expectedVersion,
        int resultingVersion) =>
        new()
        {
            MedicationId = medicationId,
            Action = action,
            PreviousActivity = previousActivity,
            CurrentActivity = currentActivity,
            Actor = actor,
            Reason = NormalizeText(reason),
            ExpectedVersion = expectedVersion,
            ResultingVersion = resultingVersion,
            OccurredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

    private static ClinicalMedicationLifecycleMutationResult InvalidMedicationMutation() =>
        new(ClinicalMedicationLifecycleMutationStatus.Invalid, null);

    private static bool HasBoundedText(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= maximumLength;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool TryReadDate(string value, out DateOnly date)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static bool TryReadDateTime(string value, out DateTime dateTime) =>
        DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out dateTime);

    private static bool TryReadOptionalDate(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (TryReadDate(value, out var parsedDate))
        {
            date = parsedDate;
            return true;
        }

        date = null;
        return false;
    }
}
