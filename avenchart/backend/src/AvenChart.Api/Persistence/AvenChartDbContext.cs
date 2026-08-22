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
    public DbSet<AccessGroupEntity> AccessGroups => Set<AccessGroupEntity>();

    public DbSet<AccessGroupPermissionEntity> AccessGroupPermissions => Set<AccessGroupPermissionEntity>();

    public DbSet<AccessPermissionEntity> AccessPermissions => Set<AccessPermissionEntity>();

    public DbSet<AccessUserMembershipEntity> AccessUserMemberships => Set<AccessUserMembershipEntity>();

    public DbSet<AllergyEntity> Allergies => Set<AllergyEntity>();

    public DbSet<AddressBookContactEntity> AddressBookContacts => Set<AddressBookContactEntity>();

    public DbSet<AuthAccountEntity> AuthAccounts => Set<AuthAccountEntity>();

    public DbSet<ChartTrackerEventEntity> ChartTrackerEvents => Set<ChartTrackerEventEntity>();

    public DbSet<ChartTrackerLocationEntity> ChartTrackerLocations => Set<ChartTrackerLocationEntity>();

    public DbSet<ClinicalListAuditEventEntity> ClinicalListAuditEvents => Set<ClinicalListAuditEventEntity>();

    public DbSet<ClinicalWorkflowEventEntity> ClinicalWorkflowEvents => Set<ClinicalWorkflowEventEntity>();

    public DbSet<DocumentTemplateBinaryVersionEntity> DocumentTemplateBinaryVersions => Set<DocumentTemplateBinaryVersionEntity>();

    public DbSet<DocumentTemplateEntity> DocumentTemplates => Set<DocumentTemplateEntity>();

    public DbSet<DocumentTemplateEventEntity> DocumentTemplateEvents => Set<DocumentTemplateEventEntity>();

    public DbSet<EncounterEntity> Encounters => Set<EncounterEntity>();

    public DbSet<EncounterAuditEventEntity> EncounterAuditEvents => Set<EncounterAuditEventEntity>();

    public DbSet<EncounterSignatureEntity> EncounterSignatures => Set<EncounterSignatureEntity>();

    public DbSet<FacilityEntity> Facilities => Set<FacilityEntity>();

    public DbSet<ImmunizationEntity> Immunizations => Set<ImmunizationEntity>();

    public DbSet<LabOrderCatalogEntity> LabOrderCatalog => Set<LabOrderCatalogEntity>();

    public DbSet<LabOrderReferenceEntity> LabOrderReferences => Set<LabOrderReferenceEntity>();

    public DbSet<LabProviderAddressBookEntity> LabProviderAddressBook => Set<LabProviderAddressBookEntity>();

    public DbSet<LabProviderEntity> LabProviders => Set<LabProviderEntity>();

    public DbSet<MedicationEntity> Medications => Set<MedicationEntity>();

    public DbSet<MedicationLifecycleEventEntity> MedicationLifecycleEvents => Set<MedicationLifecycleEventEntity>();

    public DbSet<OfficeNoteEntity> OfficeNotes => Set<OfficeNoteEntity>();

    public DbSet<PatientEntity> Patients => Set<PatientEntity>();

    public DbSet<PatientEducationResourceEntity> PatientEducationResources => Set<PatientEducationResourceEntity>();

    public DbSet<PatientRecordRequestEntity> PatientRecordRequests => Set<PatientRecordRequestEntity>();

    public DbSet<PatientSdohAssessmentEntity> PatientSdohAssessments => Set<PatientSdohAssessmentEntity>();

    public DbSet<ProblemEntity> Problems => Set<ProblemEntity>();

    public DbSet<RecallActivityEntity> RecallActivities => Set<RecallActivityEntity>();

    public DbSet<RecallEntity> Recalls => Set<RecallEntity>();

    public DbSet<RecallLifecycleEventEntity> RecallLifecycleEvents => Set<RecallLifecycleEventEntity>();

    public DbSet<ReferralEntity> Referrals => Set<ReferralEntity>();

    public DbSet<StaffEntity> Staff => Set<StaffEntity>();

    public DbSet<TherapyGroupEntity> TherapyGroups => Set<TherapyGroupEntity>();

    public DbSet<TherapyGroupMemberEntity> TherapyGroupMembers => Set<TherapyGroupMemberEntity>();

    public DbSet<TherapyGroupSessionEntity> TherapyGroupSessions => Set<TherapyGroupSessionEntity>();

    public DbSet<TherapyGroupSessionAttendanceEntity> TherapyGroupSessionAttendance => Set<TherapyGroupSessionAttendanceEntity>();

    public DbSet<TherapyGroupSessionEncounterEntity> TherapyGroupSessionEncounters => Set<TherapyGroupSessionEncounterEntity>();

    public DbSet<TherapyGroupSessionParticipantEntity> TherapyGroupSessionParticipants => Set<TherapyGroupSessionParticipantEntity>();

    public DbSet<VitalEntity> Vitals => Set<VitalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AvenChartDbContext).Assembly);
    }
}
