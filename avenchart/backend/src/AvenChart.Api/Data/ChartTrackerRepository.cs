// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

public sealed class ChartTrackerRepository(AvenChartDbContext dbContext)
{
    public async Task<ChartTrackerPatient?> FindAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var normalizedIdentifier = identifier.Trim();
        var patient = await dbContext.Patients
            .AsNoTracking()
            .Where(candidate =>
                candidate.CanonicalId == normalizedIdentifier ||
                candidate.PublicId == normalizedIdentifier)
            .Select(candidate => new
            {
                candidate.CanonicalId,
                candidate.PublicId,
                candidate.FirstName,
                candidate.LastName,
                candidate.DateOfBirth
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var history = await GetHistoryCoreAsync(patient.CanonicalId, cancellationToken);
        return new ChartTrackerPatient(
            patient.CanonicalId,
            patient.PublicId,
            $"{patient.LastName}, {patient.FirstName}",
            patient.DateOfBirth.ToString("yyyy-MM-dd"),
            history.FirstOrDefault());
    }

    public async Task<IReadOnlyList<ChartTrackerEvent>?> GetHistoryAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Patients.AsNoTracking().AnyAsync(
                patient => patient.CanonicalId == patientId,
                cancellationToken))
        {
            return null;
        }

        return await GetHistoryCoreAsync(patientId, cancellationToken);
    }

    public async Task<ChartTrackerOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var locations = await dbContext.ChartTrackerLocations
            .AsNoTracking()
            .Where(location => location.Active)
            .OrderBy(location => location.Position)
            .ThenBy(location => location.Name)
            .Select(location => location.Name)
            .ToListAsync(cancellationToken);
        var staffRows = await dbContext.Staff
            .AsNoTracking()
            .Where(staff => staff.Active)
            .OrderBy(staff => staff.LastName)
            .ThenBy(staff => staff.FirstName)
            .Select(staff => new { staff.Id, staff.FirstName, staff.LastName })
            .ToListAsync(cancellationToken);
        var users = staffRows
            .Select(staff => new ChartTrackerUser(staff.Id, $"{staff.LastName}, {staff.FirstName}"))
            .ToList();
        return new ChartTrackerOptions(locations, users);
    }

    public async Task<ChartTrackerEvent?> RecordAsync(
        string patientId,
        ChartTrackerUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var location = request.Location?.Trim();
        if (request.UserId is null && string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Select a chart location or an active staff member.");
        }

        if (request.UserId is not null)
        {
            location = null;
        }

        if (!await dbContext.Patients.AsNoTracking().AnyAsync(
                patient => patient.CanonicalId == patientId,
                cancellationToken))
        {
            return null;
        }

        if (location is not null && !await dbContext.ChartTrackerLocations.AsNoTracking().AnyAsync(
                candidate => candidate.Active && candidate.Name == location,
                cancellationToken))
        {
            throw new ArgumentException("Select an active chart location.");
        }

        string? userName = null;
        if (request.UserId is not null)
        {
            var user = await dbContext.Staff
                .AsNoTracking()
                .Where(candidate => candidate.Active && candidate.Id == request.UserId.Value)
                .Select(candidate => new { candidate.FirstName, candidate.LastName })
                .SingleOrDefaultAsync(cancellationToken);
            if (user is null)
            {
                throw new ArgumentException("Select an active staff member.");
            }

            userName = $"{user.LastName}, {user.FirstName}";
        }

        var trackerEvent = new ChartTrackerEventEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Location = location,
            UserId = request.UserId
        };
        dbContext.ChartTrackerEvents.Add(trackerEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(trackerEvent, userName);
    }

    private async Task<IReadOnlyList<ChartTrackerEvent>> GetHistoryCoreAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ChartTrackerEvents
            .AsNoTracking()
            .Where(trackerEvent => trackerEvent.PatientId == patientId)
            .OrderByDescending(trackerEvent => trackerEvent.RecordedAt)
            .Select(trackerEvent => new
            {
                TrackerEvent = trackerEvent,
                UserFirstName = trackerEvent.User == null ? null : trackerEvent.User.FirstName,
                UserLastName = trackerEvent.User == null ? null : trackerEvent.User.LastName
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => ToItem(
                row.TrackerEvent,
                row.UserLastName is null ? null : $"{row.UserLastName}, {row.UserFirstName}"))
            .ToList();
    }

    private static ChartTrackerEvent ToItem(ChartTrackerEventEntity trackerEvent, string? userName) =>
        new(
            trackerEvent.Id,
            trackerEvent.Location,
            trackerEvent.UserId,
            userName,
            trackerEvent.RecordedAt.ToString("O"));
}
