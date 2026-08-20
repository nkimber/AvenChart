// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

public sealed class PatientRecordRequestRepository(AvenChartDbContext dbContext)
{
    public async Task<IReadOnlyList<PatientRecordRequestResponse>> GetAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var requests = await dbContext.PatientRecordRequests
            .AsNoTracking()
            .Where(request => request.PatientId == patient.CanonicalId)
            .OrderByDescending(request => request.RequestedAt)
            .ThenByDescending(request => request.RequestId)
            .ToListAsync(cancellationToken);
        return requests.Select(ToResponse).ToList();
    }

    public async Task<PatientRecordRequestResponse> CreateAsync(
        string patientId,
        string username,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var request = new PatientRecordRequestEntity
        {
            RequestId = Guid.NewGuid(),
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = username,
            RowVersion = 1
        };
        dbContext.PatientRecordRequests.Add(request);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            dbContext.Entry(request).State = EntityState.Detached;
            throw new InvalidOperationException("There is already an open patient record request.");
        }

        return ToResponse(request);
    }

    public async Task<PatientRecordRequestResponse> CompleteAsync(
        string patientId,
        Guid requestId,
        string username,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var request = await dbContext.PatientRecordRequests.SingleOrDefaultAsync(
            candidate =>
                candidate.RequestId == requestId &&
                candidate.PatientId == patient.CanonicalId &&
                candidate.CompletedAt == null,
            cancellationToken);
        if (request is null)
        {
            throw new InvalidOperationException("Only an open patient record request can be completed.");
        }

        request.CompletedAt = DateTimeOffset.UtcNow;
        request.CompletedBy = username;
        request.RowVersion++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The patient record request changed before it could be completed.");
        }

        return ToResponse(request);
    }

    private async Task<PatientIdentity?> ResolvePatientAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var normalized = patientId.Trim();
        var normalizedLower = normalized.ToLowerInvariant();
        var hasLegacyPid = int.TryParse(normalized, out var legacyPid);
        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient =>
                patient.MergedIntoPatientId == null &&
                (patient.CanonicalId.ToLower() == normalizedLower ||
                 patient.PublicId.ToLower() == normalizedLower ||
                 (hasLegacyPid && patient.LegacyPid == legacyPid)))
            .Select(patient => new PatientIdentity(patient.CanonicalId, patient.LegacyPid))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static PatientRecordRequestResponse ToResponse(PatientRecordRequestEntity request) =>
        new(
            request.RequestId,
            request.PatientId,
            request.LegacyPid,
            request.CompletedAt is null ? "Open" : "Completed",
            request.RequestedAt.ToString("O"),
            request.RequestedBy,
            request.CompletedAt?.ToString("O"),
            request.CompletedBy);

    private sealed record PatientIdentity(string CanonicalId, int LegacyPid);
}
