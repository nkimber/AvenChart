// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

public sealed class RecallRepository(AvenChartDbContext dbContext)
{
    public async Task<IReadOnlyList<RecallItem>> GetAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Recalls
            .AsNoTracking()
            .Where(recall => recall.Status == "active")
            .OrderBy(recall => recall.RecallDate)
            .Select(recall => new
            {
                Recall = recall,
                recall.Patient.FirstName,
                recall.Patient.LastName
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => ToItem(row.Recall, row.FirstName, row.LastName)).ToList();
    }

    public async Task<RecallItem?> CreateAsync(
        RecallRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return null;
        }

        var patient = await dbContext.Patients
            .AsNoTracking()
            .Where(candidate => candidate.CanonicalId == request.PatientId)
            .Select(candidate => new { candidate.FirstName, candidate.LastName })
            .SingleOrDefaultAsync(cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var recall = new RecallEntity
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            RecallDate = request.RecallDate,
            Reason = request.Reason.Trim(),
            ProviderId = request.ProviderId,
            FacilityId = request.FacilityId,
            Status = "active"
        };
        dbContext.Recalls.Add(recall);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(recall, patient.FirstName, patient.LastName);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.Recalls
            .Where(recall => recall.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    public async Task<IReadOnlyList<RecallActivityItem>?> GetActivityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Recalls.AsNoTracking().AnyAsync(recall => recall.Id == id, cancellationToken))
        {
            return null;
        }

        var activity = await dbContext.RecallActivities
            .AsNoTracking()
            .Where(item => item.RecallId == id)
            .OrderByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return activity.Select(ToActivityItem).ToList();
    }

    public async Task<RecallActivityItem?> AddActivityAsync(
        Guid id,
        RecallActivityRequest request,
        CancellationToken cancellationToken)
    {
        var activityType = request.ActivityType?.Trim().ToLowerInvariant();
        if (activityType is not ("phone" or "postcard" or "label"))
        {
            throw new ArgumentException("Activity type must be phone, postcard, or label.");
        }

        if (!await dbContext.Recalls.AsNoTracking().AnyAsync(recall => recall.Id == id, cancellationToken))
        {
            return null;
        }

        var activity = new RecallActivityEntity
        {
            Id = Guid.NewGuid(),
            RecallId = id,
            ActivityType = activityType,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        dbContext.RecallActivities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToActivityItem(activity);
    }

    private static RecallItem ToItem(RecallEntity recall, string firstName, string lastName) =>
        new(
            recall.Id,
            recall.PatientId,
            $"{lastName}, {firstName}",
            recall.RecallDate.ToString("yyyy-MM-dd"),
            recall.Reason,
            recall.ProviderId,
            recall.FacilityId,
            recall.Status,
            recall.CreatedAt.ToString("O"));

    private static RecallActivityItem ToActivityItem(RecallActivityEntity activity) =>
        new(
            activity.Id,
            activity.ActivityType,
            activity.Note,
            activity.RecordedAt.ToString("O"));
}
