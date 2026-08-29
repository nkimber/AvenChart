// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthOptions
{
    public const string SectionName = "Telehealth";

    public bool Enabled { get; init; }
    public string Mode { get; init; } = "Synthetic";
    public string PracticeId { get; init; } = "avenchart-synthetic-practice";
    public string PracticeDisplayName { get; init; } = "AvenChart Synthetic Practice";
    public int FacilityId { get; init; } = 10;
    // Array defaults stay empty because ConfigurationBinder appends configured
    // entries to initialized arrays. Explicit appsettings values avoid a
    // duplicate/ambiguous host or jurisdiction map.
    public string[] BrandedHosts { get; init; } = [];
    public string[] SupportedStates { get; init; } = [];
    public int ReservationLeaseSeconds { get; init; } = 120;
    public string VideoAdapterMode { get; init; } = "NON_PRODUCTION";
    public string PharmacyDirectoryAdapterMode { get; init; } = "NON_PRODUCTION";
}

public static partial class TelehealthRuntimeSafetyPolicy
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex PracticeIdPattern();

    public static bool IsSafe(TelehealthOptions options, IHostEnvironment environment)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return !environment.IsProduction()
            && string.Equals(options.Mode, "Synthetic", StringComparison.Ordinal)
            && PracticeIdPattern().IsMatch(options.PracticeId)
            && options.FacilityId > 0
            && options.ReservationLeaseSeconds is >= 30 and <= 600
            && string.Equals(options.VideoAdapterMode, SyntheticTelehealthVideoProvider.AdapterMode, StringComparison.Ordinal)
            && string.Equals(options.PharmacyDirectoryAdapterMode, SyntheticTelehealthPharmacyDirectory.Mode, StringComparison.Ordinal)
            && options.BrandedHosts.Length > 0
            && options.BrandedHosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.BrandedHosts.Length
            && options.BrandedHosts.All(IsExplicitHost)
            && options.SupportedStates.Order(StringComparer.Ordinal).SequenceEqual(
                new[] { "CA", "FL", "GA" }, StringComparer.Ordinal);
    }

    private static bool IsExplicitHost(string host) =>
        !string.IsNullOrWhiteSpace(host)
        && host == host.Trim()
        && !host.Contains('*', StringComparison.Ordinal)
        && !host.Contains('/', StringComparison.Ordinal)
        && !host.Contains(':', StringComparison.Ordinal);
}

public static class TelehealthServiceRegistration
{
    public static IServiceCollection AddTelehealth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<TelehealthOptions>()
            .BindConfiguration(TelehealthOptions.SectionName)
            .Validate(options => TelehealthRuntimeSafetyPolicy.IsSafe(options, environment),
                "Enabled telehealth is synthetic-only, requires explicit safe hosts and GA/CA/FL, and cannot run in Production.")
            .ValidateOnStart();
        services.AddScoped<TelehealthRepository>();
        services.AddScoped<TelehealthService>();
        services.AddScoped<TelehealthProspectiveApplicantRepository>();
        services.AddScoped<TelehealthProspectiveApplicantService>();
        services.AddScoped<TelehealthProspectiveSafetyTriageRepository>();
        services.AddScoped<TelehealthProspectiveSafetyTriageService>();
        services.AddScoped<TelehealthProspectiveVisitPurposeRepository>();
        services.AddScoped<TelehealthProspectiveVisitPurposeService>();
        services.AddScoped<TelehealthProspectivePracticeNetworkPrecheckRepository>();
        services.AddScoped<TelehealthProspectivePracticeNetworkPrecheckService>();
        services.AddScoped<TelehealthProspectiveMemberInsuranceDetailsRepository>();
        services.AddScoped<TelehealthProspectiveMemberInsuranceDetailsService>();
        services.AddSingleton<TelehealthProspectiveMemberInsuranceDetailsProtector>();
        services.AddScoped<TelehealthProspectiveEligibilityRepository>();
        services.AddScoped<TelehealthProspectiveEligibilityService>();
        services.AddScoped<TelehealthProspectivePracticeNetworkRepository>();
        services.AddScoped<TelehealthProspectivePracticeNetworkService>();
        services.AddScoped<TelehealthProspectiveIdentityProofingRepository>();
        services.AddScoped<TelehealthProspectiveIdentityProofingService>();
        services.AddScoped<TelehealthApplicantIdentityReviewRepository>();
        services.AddScoped<TelehealthApplicantIdentityReviewService>();
        services.AddScoped<TelehealthApplicantPromotionAuthorizationRepository>();
        services.AddScoped<TelehealthApplicantPromotionAuthorizationService>();
        services.AddScoped<TelehealthApplicantSyntheticPromotionRepository>();
        services.AddScoped<TelehealthApplicantSyntheticPromotionService>();
        services.AddScoped<TelehealthApplicantNoticeRepository>();
        services.AddScoped<TelehealthApplicantNoticeService>();
        services.AddScoped<TelehealthApplicantRegistrationDetailsRepository>();
        services.AddScoped<TelehealthApplicantRegistrationDetailsService>();
        services.AddScoped<TelehealthApplicantInsuranceHandoffRepository>();
        services.AddScoped<TelehealthApplicantInsuranceHandoffService>();
        services.AddScoped<TelehealthApplicantCommunicationAccessRepository>();
        services.AddScoped<TelehealthApplicantCommunicationAccessService>();
        services.AddScoped<TelehealthApplicantDevicePreparationRepository>();
        services.AddScoped<TelehealthApplicantDevicePreparationService>();
        services.AddScoped<TelehealthApplicantClinicalInformationInventoryRepository>();
        services.AddScoped<TelehealthApplicantClinicalInformationInventoryService>();
        services.AddScoped<TelehealthApplicantMedicationInformationRepository>();
        services.AddScoped<TelehealthApplicantMedicationInformationService>();
        services.AddScoped<TelehealthApplicantAllergyInformationRepository>();
        services.AddScoped<TelehealthApplicantAllergyInformationService>();
        services.AddScoped<TelehealthApplicantHealthHistoryInformationRepository>();
        services.AddScoped<TelehealthApplicantHealthHistoryInformationService>();
        services.AddScoped<TelehealthApplicantClinicalInformationSummaryRepository>();
        services.AddScoped<TelehealthApplicantClinicalInformationSummaryService>();
        services.AddScoped<TelehealthApplicantPreRequestReadinessRepository>();
        services.AddScoped<TelehealthApplicantPreRequestReadinessService>();
        services.AddScoped<TelehealthApplicantPracticeReviewSubmissionService>();
        services.AddScoped<TelehealthApplicantPracticeReviewInboxRepository>();
        services.AddScoped<TelehealthApplicantPracticeReviewInboxService>();
        services.AddScoped<TelehealthApplicantPracticeReviewClaimRepository>();
        services.AddScoped<TelehealthApplicantPracticeReviewClaimService>();
        services.AddScoped<TelehealthApplicantPracticeReviewPacketRepository>();
        services.AddScoped<TelehealthApplicantPracticeReviewPacketService>();
        services.AddScoped<TelehealthApplicantPracticeReviewAuthorizationRepository>();
        services.AddScoped<TelehealthApplicantPracticeReviewAuthorizationService>();
        services.AddScoped<TelehealthApplicantRequestCreationRepository>();
        services.AddScoped<TelehealthApplicantRequestCreationService>();
        services.AddScoped<TelehealthApplicantRequestLocationRepository>();
        services.AddScoped<TelehealthApplicantRequestLocationService>();
        services.AddScoped<TelehealthApplicantRequestUniversalSafetyRepository>();
        services.AddScoped<TelehealthApplicantRequestUniversalSafetyService>();
        services.AddScoped<TelehealthApplicantRequestComplaintTriageRepository>();
        services.AddScoped<TelehealthApplicantRequestComplaintTriageService>();
        services.AddScoped<TelehealthApplicantRequestIntakeRepository>();
        services.AddScoped<TelehealthApplicantRequestIntakeService>();
        services.AddScoped<TelehealthApplicantRequestInsuranceSourceRepository>();
        services.AddScoped<TelehealthApplicantRequestInsuranceSourceService>();
        services.AddScoped<TelehealthApplicantRequestEligibilityRepository>();
        services.AddScoped<TelehealthApplicantRequestEligibilityService>();
        services.AddScoped<TelehealthApplicantRequestPracticeNetworkRepository>();
        services.AddScoped<TelehealthApplicantRequestPracticeNetworkService>();
        services.AddScoped<TelehealthApplicantRequestRenderingCandidateRepository>();
        services.AddScoped<TelehealthApplicantRequestRenderingCandidateService>();
        services.AddScoped<TelehealthApplicantRequestParticipationContextRepository>();
        services.AddScoped<TelehealthApplicantRequestParticipationContextService>();
        services.AddScoped<TelehealthApplicantRequestParticipationEvaluationRepository>();
        services.AddScoped<TelehealthApplicantRequestParticipationEvaluationService>();
        services.AddScoped<TelehealthVideoRepository>();
        services.AddScoped<TelehealthVideoService>();
        services.AddScoped<TelehealthConsultationRepository>();
        services.AddScoped<TelehealthConsultationService>();
        services.AddScoped<TelehealthPharmacyRepository>();
        services.AddScoped<TelehealthDispositionRepository>();
        services.AddScoped<TelehealthCompletionReviewRepository>();
        services.AddScoped<TelehealthPrescriptionRepository>();
        services.AddScoped<TelehealthPrescriptionService>();
        services.AddSingleton<ITelehealthTriageEvaluator, SyntheticTelehealthTriageEvaluator>();
        services.AddSingleton<ISyntheticTelehealthComplaintTriageEvaluator,
            SyntheticTelehealthComplaintTriageEvaluator>();
        services.AddSingleton<SyntheticTelehealthProspectivePracticeNetworkCatalog>();
        services.AddSingleton<ITelehealthProspectiveEligibilityGateway, SyntheticTelehealthProspectiveEligibilityGateway>();
        services.AddSingleton<ITelehealthProspectivePracticeNetworkGateway, SyntheticTelehealthProspectivePracticeNetworkGateway>();
        services.AddSingleton<ITelehealthProspectiveIdentityProofingGateway, SyntheticTelehealthProspectiveIdentityProofingGateway>();
        services.AddSingleton<ITelehealthCoverageGateway, SyntheticTelehealthCoverageGateway>();
        services.AddSingleton<ITelehealthVideoProvider, SyntheticTelehealthVideoProvider>();
        services.AddSingleton<IPharmacyDirectory, SyntheticTelehealthPharmacyDirectory>();
        services.AddHealthChecks()
            .AddCheck<TelehealthReadinessHealthCheck>("telehealth", tags: ["ready"]);
        return services;
    }
}
