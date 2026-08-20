// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

public sealed class OfficeNoteRepository(AvenChartDbContext dbContext)
{
    public async Task<OfficeNotesResponse> GetAsync(
        string activity,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var active = activity.ToLowerInvariant() switch
        {
            "active" => true,
            "inactive" => false,
            _ => (bool?)null
        };
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = Math.Clamp(limit, 1, 100);

        var query = dbContext.OfficeNotes.AsNoTracking();
        if (active.HasValue)
        {
            query = query.Where(note => note.Active == active.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(note => note.CreatedAt)
            .ThenByDescending(note => note.Id)
            .Skip(boundedOffset)
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);
        var notes = entities.Select(ToDto).ToList();

        return new OfficeNotesResponse(notes, total);
    }

    public async Task<OfficeNoteItem?> CreateAsync(
        string? body,
        string author,
        CancellationToken cancellationToken)
    {
        var text = Normalize(body);
        if (text is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var note = new OfficeNoteEntity
        {
            Id = Guid.NewGuid(),
            Body = text,
            Author = author,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.OfficeNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<OfficeNoteItem?> UpdateAsync(
        Guid id,
        string? body,
        CancellationToken cancellationToken)
    {
        var text = Normalize(body);
        if (text is null)
        {
            return null;
        }

        var note = await dbContext.OfficeNotes.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (note is null)
        {
            return null;
        }

        note.Body = text;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<OfficeNoteItem?> SetActivityAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var note = await dbContext.OfficeNotes.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (note is null)
        {
            return null;
        }

        note.Active = active;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.OfficeNotes
            .Where(note => note.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    private static OfficeNoteItem ToDto(OfficeNoteEntity note) =>
        new(
            note.Id,
            note.Body,
            note.Author,
            note.GroupName,
            note.Active,
            note.CreatedAt.ToString("O"),
            note.UpdatedAt.ToString("O"));

    private static string? Normalize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var normalized = body.Trim();
        return normalized.Length > 4000 ? null : normalized;
    }
}
