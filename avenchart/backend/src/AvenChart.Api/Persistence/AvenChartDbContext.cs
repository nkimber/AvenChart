// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Persistence;

/// <summary>
/// Database-first EF Core mapping for incrementally adopted persistence slices.
/// The versioned SQL migration catalog remains the sole schema authority.
/// </summary>
public sealed class AvenChartDbContext(DbContextOptions<AvenChartDbContext> options)
    : DbContext(options)
{
    public DbSet<AddressBookContactEntity> AddressBookContacts => Set<AddressBookContactEntity>();

    public DbSet<ChartTrackerEventEntity> ChartTrackerEvents => Set<ChartTrackerEventEntity>();

    public DbSet<ChartTrackerLocationEntity> ChartTrackerLocations => Set<ChartTrackerLocationEntity>();

    public DbSet<OfficeNoteEntity> OfficeNotes => Set<OfficeNoteEntity>();

    public DbSet<PatientEntity> Patients => Set<PatientEntity>();

    public DbSet<PatientEducationResourceEntity> PatientEducationResources => Set<PatientEducationResourceEntity>();

    public DbSet<PatientRecordRequestEntity> PatientRecordRequests => Set<PatientRecordRequestEntity>();

    public DbSet<PatientSdohAssessmentEntity> PatientSdohAssessments => Set<PatientSdohAssessmentEntity>();

    public DbSet<RecallActivityEntity> RecallActivities => Set<RecallActivityEntity>();

    public DbSet<RecallEntity> Recalls => Set<RecallEntity>();

    public DbSet<StaffEntity> Staff => Set<StaffEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AvenChartDbContext).Assembly);
    }
}
